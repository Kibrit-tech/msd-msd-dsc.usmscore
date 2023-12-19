using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using DSC.Core.Database.QueueOperations;
using DSC.Core.Logging;
using DSC.Core.Translitter;
using DSC.SmppCore.Exceptions;
using DSC.SmppCore.SMPP;
using DSC.UsmsCore.Models;
using DSC.UsmsCore.Models.Database;
using DSC.Core.Threading;

namespace DSC.UsmsCore
{
    public class UsmsCore
    {
        private readonly SmppCore.SmppCore _smpp;
        private readonly DbHandler _dbh;
        private readonly Thread _outThread;
        private readonly WaitForMax _maxWaiter;
        private int maxThread = 10;
        private bool _doProcess;

        private readonly LogHelper _logger;
        private readonly object _locker;

        private readonly EventWaitHandle _dequeueWh;
        private readonly Timer _dequeueTimer;
        private readonly Timer _connectedCheckTimer;
        private int _portion;

        private readonly UsmsSettings _sett;
        private int count = 0;

        public UsmsCore(UsmsSettings sett, string inConnectionString, string outConnectionString, string statusConnectionString)
        {
            _sett = sett;

            _locker = new object();

            //_logger = new LogHelper(_sett.LogPath.Replace(".txt", "UsmsCore2.txt"));

            _dbh = new DbHandler(_sett.OutQueueMTypes, _sett.ReportIdName, inConnectionString, outConnectionString, statusConnectionString);

            _smpp = new SmppCore.SmppCore(_sett.IpEndPoints, _sett.Login, _sett.Password,
                                          _sett.ConnTimout, _sett.ReconnTimeout, _sett.ReconnSmsTimeout, _sett.EnqTimeout,
                                          _sett.LogPath.Replace(".txt", "SmppCore2.txt"), _sett.SystemType, _sett.SendLongSms,
                                          MyIncomeSms, MyConnected, MyDisconnected);
            _doProcess = true;

            _dequeueWh = new EventWaitHandle(false, EventResetMode.AutoReset);
            _dequeueTimer = new Timer(DequeueTimer, null, Timeout.Infinite, _sett.DequeueTimeout);
            _connectedCheckTimer = new Timer(ConnectedCheckTimer, null, Timeout.Infinite, (int)_sett.StatusPeriod.TotalMilliseconds);

            _maxWaiter = new WaitForMax(maxThread);

            ThreadPool.SetMaxThreads(maxThread, maxThread);
            ThreadPool.SetMinThreads(maxThread, maxThread);

            _outThread = new Thread(Process);
        }

        public void Start()
        {
            _smpp.Start();
            _outThread.Start();
            _dequeueTimer.Change(0, _sett.DequeueTimeout);
            _connectedCheckTimer.Change(0, (int)_sett.StatusPeriod.TotalMilliseconds);
        }

        public void Stop()
        {
            _dequeueTimer.Change(0, Timeout.Infinite);
            _connectedCheckTimer.Change(0, Timeout.Infinite);
            _doProcess = false;
            _smpp.Stop();
            _dequeueWh.Set();
            _maxWaiter.DontWaitAnyMore();
            _outThread.Join();
        }

        private void Process()
        {
            while (_doProcess)
            {
                try
                {
                    if (!_maxWaiter.WaitAndIncrement()) continue;
                    ThreadPool.QueueUserWorkItem(ProcessRequest);
                }
                catch (Exception ex)
                {

                }

            }
        }

        private void ProcessRequest(object callback)
        {
            lock(_locker)
            {
                _portion = 0;
                while (_portion < _sett.MessagesInTimeout)
                {
                    var token = new DOutQueueToken();
                    if (_dbh.OutQueue.DequeueStart(token))
                    {
                        try
                        {
                            if (_sett.SendLongSms)
                            {
                                count++; // for test
                                SendTokenLong(token);
                            }
                            else
                            {
                                SendToken(token);
                            }
                            _dbh.OutQueue.DequeueEnd(token.Mtype, token.Id);
                        }
                        catch (Exception exc)
                        {
                            if (exc is SmppConnectionDropException) continue;
                            if (exc is SmppTimeoutException || exc is SmppDisconnectedException)
                            {
                                //Thread.Sleep(1000);
                                continue;
                            }

                            if (exc is SocketException && ((SocketException)exc).SocketErrorCode == SocketError.Success)
                            {
                                //Thread.Sleep(3000);
                                continue;
                            }

                            throw;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
           

            _dequeueWh.WaitOne();
            _maxWaiter.Decrement();
        }

        private void SendTokenLong(DOutQueueToken token)
        {
            var tempud = token.Dcs == 0
                ? (_sett.UseGsmEncoding ? Gsm.Encoding.GetBytes(token.SmsText) : Encoding.ASCII.GetBytes(token.SmsText))
                : Convert.FromBase64String(token.SmsText);

            string reportId;
            var doReport = _sett.ReportAll || _sett.DoReportList.Contains(token.FromNumber);
            var result = _smpp.SendSms(token.FromNumber, token.ToNumber, (byte)token.Dcs, tempud, DateTime.MinValue,
                                       token.Udhi == 1, _sett.MessageResponseTimeout, out reportId, doReport);
            if (result == SmppStatusCode.ESME_ROK && doReport && !string.IsNullOrWhiteSpace(reportId))
            {
                if (_dbh.IfReportExist(reportId, _sett.SmppName))
                {
                    _dbh.UpdateReportGeneralIdForDuplicate(reportId, _sett.SmppName, token.Id);
                }
                else
                {
                    _dbh.InsertReport(reportId, _sett.SmppName, token.Id);
                }
            }

            _dbh.OutgoingSmsLog.Log(_dbh.OutSeq.GiveSequence("out_sms"), token.Id, DateTime.Now, (int)result);
            _portion++;
        }

        private void SendToken(DOutQueueToken token)
        {
            byte[] tempud;
            int maxLength;

            if (token.Dcs == 0)
            {
                maxLength = 160;
                tempud = _sett.UseGsmEncoding ? Gsm.Encoding.GetBytes(token.SmsText) : Encoding.ASCII.GetBytes(token.SmsText);
            }
            else
            {
                maxLength = 140;
                tempud = Convert.FromBase64String(token.SmsText);
            }

            var doReport = _sett.ReportAll || _sett.DoReportList.Contains(token.FromNumber);

            do
            {
                byte[] curUd;
                if (tempud.Length <= maxLength)
                {
                    curUd = tempud;
                    tempud = new byte[0];
                }
                else
                {
                    curUd = new byte[maxLength];
                    Array.Copy(tempud, curUd, maxLength);
                    Array.Copy(tempud, maxLength, tempud, 0, tempud.Length - maxLength);
                    Array.Resize(ref tempud, tempud.Length - maxLength);
                }

                string messageId;
                var result = _smpp.SendSms(token.FromNumber, token.ToNumber, (byte)token.Dcs,
                                           curUd, DateTime.MinValue, token.Udhi == 1, _sett.MessageResponseTimeout, out messageId,
                                           doReport);
                if (result == SmppStatusCode.ESME_ROK && doReport)
                {
                    if (_dbh.IfReportExist(messageId, _sett.SmppName))
                    {
                        _dbh.UpdateReportGeneralIdForDuplicate(messageId, _sett.SmppName, token.Id);
                    }
                    else
                    {
                        _dbh.InsertReport(messageId, _sett.SmppName, token.Id);
                    }
                }

                _dbh.OutgoingSmsLog.Log(_dbh.OutSeq.GiveSequence("out_sms"), token.Id, DateTime.Now, (int)result);
                _portion++;
            } while (tempud.Length > 0);
        }

        private bool MyIncomeSms(SmppPdu pdu)
        {
            if (pdu.EsmClass == 4)
            {
                return MyReportSms(pdu);
            }

            if (!_sett.DontTranslitList.Contains(pdu.DestinationAddr))
            {
                pdu.ShortMessage = Tranlitter.Translit(pdu.ShortMessage);
            }

            var inToken = new DInQueueToken
            {
                Channel = _sett.InQueueChannel,
                Dt = DateTime.Now,
                FromNumber = pdu.SourceAddr,
                Mtype = _sett.PrefixForMtype + pdu.DestinationAddr,
                SmsText = pdu.ShortMessage
            };

            var id = _dbh.InSeq.GiveSequence("request");
            inToken.Id = id;

            if (_sett.AddDcsToDestList.Contains(pdu.DestinationAddr))
            {
                if (pdu.DataCoding != 0)
                {
                    inToken.ToNumber = "&" + pdu.DataCoding.ToString("X2") + pdu.DestinationAddr;
                }
                else
                {
                    inToken.ToNumber = pdu.DestinationAddr;
                }
            }
            else
            {
                inToken.ToNumber = pdu.DestinationAddr;
            }

            if (!_sett.IgnoreIncominList.Contains(inToken.ToNumber))
            {
                try
                {
                    _dbh.InQueue.Enqueue(inToken);
                }
                catch
                {
                    lock (_locker)
                    {
                        _logger.Log($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}:\t Process failed while enqueue token. id={id}, smsText={inToken.SmsText}\r\n");
                    }
                }
            }

            try
            {
                _dbh.IncomingSmsLog.Log(id, DateTime.Now, pdu.SourceAddr, pdu.DestinationAddr, pdu.ShortMessage);
            }
            catch
            {
                lock (_locker)
                {
                    _logger.Log($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}:\t Process failed while adding sms to incoming_sms_log. id={id}, smsText={pdu.ShortMessage}\r\n");
                }
            }

            return true;
        }

        private bool MyReportSms(SmppPdu pdu)
        {
            try
            {
                string reportId;
                byte status;
                var error = 0;
                if (_sett.ReportKind == ReportKind.Normal)
                {
                    if (!pdu.Optionals.ContainsKey(SmppOptionalTag.ReceiptedMessageId) || !pdu.Optionals.ContainsKey(SmppOptionalTag.MessageState))
                    {
                        return true;
                    }

                    if (_sett.ShouldParseReport) // here we should parse Report Message
                    {
                        //id:65799398 sub:001 dlvrd:001 submit date:1612011338 done date:1612011338 stat:UNDELIV err:905 Text:Test SMS for report
                        var errorParam = pdu.ShortMessage.Split(' ').FirstOrDefault(param => param.StartsWith("err"));
                        if (errorParam != null && errorParam.Contains(':'))
                        {
                            int.TryParse(errorParam.Split(':')[1], out error);
                        }
                    }

                    reportId = pdu.Optionals[SmppOptionalTag.ReceiptedMessageId].StringValue.Trim((char)0x00);
                    status = error == 905
                        ? (byte)51
                        : pdu.Optionals[SmppOptionalTag.MessageState].BytesValue[0]; //51 is double SMS response
                }
                else if (_sett.ReportKind == ReportKind.Mandarin) //ReportKind.Mandarin
                {
                    try
                    {
                        var shortMessage = pdu.ShortMessage.Split(':');
                        //"id:166157226230786151566598729 sub:000 dlvrd:000 submit date:1603110954 done date:1603110954 stat:DELIVRD err:002 text: t"
                        reportId = new string((from c in shortMessage[1] where char.IsDigit(c) select c).ToArray());
                        status = (byte)int.Parse(new string((from c in shortMessage[7] where char.IsDigit(c) select c).ToArray()));
                    }
                    catch (Exception exc)
                    {
                        lock (_locker)
                        {
                            _logger.Log($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}:\t Process failed in MyReportSms Mandarin. Exception message = {exc.Message}\r\n");
                        }
                        return true;
                    }
                }
                else //ReportKind.Orange
                {
                    try
                    {
                        var deliveryStatus = string.Empty;
                        reportId = string.Empty;
                        var idParam = pdu.ShortMessage.Split(' ').FirstOrDefault(param => param.StartsWith("id"));
                        if (idParam != null && idParam.Contains(':'))
                        {
                            reportId = idParam.Split(':')[1];
                        }

                        var deliveryStatusParam = pdu.ShortMessage.Split(' ').FirstOrDefault(param => param.StartsWith("stat"));
                        if (deliveryStatusParam != null && deliveryStatusParam.Contains(':'))
                        {
                            deliveryStatus = deliveryStatusParam.Split(':')[1];
                        }

                        status = SmppDelivery.GetDeliveryStatus(deliveryStatus);
                    }
                    catch (Exception exc)
                    {
                        lock (_locker)
                        {
                            _logger.Log($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}:\t Process failed in MyReportSms Orange. Exception message = {exc.Message}\r\n");
                        }
                        return true;
                    }
                }

                long generalId = -1;
                var retries = 0;
                while (generalId == -1 && retries <= 5)
                {
                    generalId = _dbh.UpdateReport(reportId, _sett.SmppName, status);
                    Thread.Sleep(100);
                    retries++;
                }

                if (generalId != -1 && _sett.DoQueueReport || _sett.DoQueueReportList.Contains(pdu.DestinationAddr))
                {
                    var mtype = pdu.DestinationAddr;
                    var shortNumber = pdu.DestinationAddr;
                    if (_sett.InQueueChannel == "GMS-SMS") // gms report
                    {
                        mtype = _dbh.GetReportMtype(generalId);
                        shortNumber = mtype;
                    }
                    else if (_sett.InQueueChannel == "SMS-ATL-BULK" || _sett.InQueueChannel == "SMS-ATAT-BULK") //for updating report statuses via Request Queue
                    {
                        mtype = "BULK_" + mtype;
                    }
                    _dbh.InQueue.Enqueue(_dbh.InSeq.GiveSequence("request"), mtype, "REPORT", DateTime.Now, pdu.SourceAddr, shortNumber, generalId + "\t" + status);
                }
            }
            catch (Exception exc)
            {
                lock (_locker)
                {
                    _logger.Log($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}:\t Process failed in MyReportSms. Exception message = {exc.Message}\r\n");
                }
            }

            return true;
        }

        private void MyConnected(IPEndPoint ipEndPoint)
        {
            var index = -1;
            for (var i = 0; i < _sett.IpEndPoints.Count; i++)
            {
                if (_sett.IpEndPoints[i].ToString() == ipEndPoint.ToString())
                {
                    index = i;
                }
            }

            if (index >= 0)
            {
                _dbh.UpdateStatus("connected", 0, _sett.FieldNames[index]);
            }
        }

        private void MyDisconnected(string commandStatus, IPEndPoint ipEndPoint)
        {
            var index = -1;
            for (var i = 0; i < _sett.IpEndPoints.Count; i++)
            {
                if (_sett.IpEndPoints[i].ToString() == ipEndPoint.ToString())
                {
                    index = i;
                }
            }

            if (index >= 0)
            {
                _dbh.UpdateStatus("not connected - " + commandStatus, 1, _sett.FieldNames[index]);
            }
        }

        private void DequeueTimer(object state)
        {
            _dequeueWh.Set();
        }

        private void ConnectedCheckTimer(object state)
        {
            foreach (var smscConnection in _smpp.SmscConnections)
            {
                if (!smscConnection.IsConnected)
                {
                    MyDisconnected("", smscConnection.IpEndPoint);
                }
                else
                {
                    MyConnected(smscConnection.IpEndPoint);
                }
            }
        }
    }
}