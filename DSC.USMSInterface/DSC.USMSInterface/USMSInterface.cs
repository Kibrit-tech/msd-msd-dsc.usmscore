using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.Common;
using MySql.Data.MySqlClient;
using DSC.Commons;
using System.Threading;
using System.Net;
using DSC.TempCommons;
using DSC.USMPP;
using System.Reflection;
using System.IO;

namespace DSC.USMSInterface
{
    public class USMSInterface
    {
        USMPP.USMPP _conn;

        DBHandler _dbh;

        Logger _logger;

        Thread _thout;

        bool _doprocess;

        readonly EventWaitHandle _dequeueWh = new EventWaitHandle(false, EventResetMode.AutoReset);
        Timer _tDequeue;
        Timer _tConnectedCheck;
        int _portion;

        string DBInConnS, DBOutConnS, DBStatusConnS;
        USMSSettings sett;

        public USMSInterface(USMSSettings sett,string DBInConnS, string DBOutConnS, string DBStatusConnS)
        {
            this.sett = sett;
            this.DBInConnS = DBInConnS;
            this.DBOutConnS = DBOutConnS;
            this.DBStatusConnS = DBStatusConnS;
        }

        private void Init()
        {
            _logger = new Logger(sett.LogPath.Replace(".txt", "1.txt"));
            _dbh = new DBHandler(sett.OutQueueMType,DBInConnS,DBOutConnS,DBStatusConnS,sett.reportidname);

            _conn = new USMPP.USMPP(sett.IPES,
                sett.Login,
                sett.Pass,
                sett.connTimout, sett.reconnTimeout, sett.reconnSmsTimeout, sett.enqTimeout,
                sett.LogPath,
                sett.SystemType,
                sett.sendlongsms,
                MyIncomeSms,
                MyConnected,
                MyDisconnected
                );

            
            _doprocess = true;

            _tDequeue = new Timer(DequeueTimer, null, Timeout.Infinite, sett.DequeueTimeout);
            _tConnectedCheck = new Timer(ConnectedCheckTimer, null, Timeout.Infinite, (int)sett.StatusPeriod.TotalMilliseconds);
            _thout = new Thread(new ThreadStart(Process));
        }

        public void Start()
        {
            Init();
            _conn.Start();
            _thout.Start();
            _tDequeue.Change(0, sett.DequeueTimeout);
            _tConnectedCheck.Change(0, (int)sett.StatusPeriod.TotalMilliseconds);
        }

        public void Stop()
        {
            _tDequeue.Change(0, Timeout.Infinite);
            _tConnectedCheck.Change(0, Timeout.Infinite);
            _doprocess = false;
            _conn.Stop();
            _dequeueWh.Set();
            _thout.Join();
        }

        void Process()
        {
            while (_doprocess)
            {
                _portion = 0;
                while (_portion < sett.MessagesInTimeout)
                {
                    DOutQueueToken token = new DOutQueueToken();
                    if (_dbh.out_queue.DequeueStart(token))
                    {
                        try
                        {
                            string forward = "";
                            if (sett.shouldForwardOutgoing) forward = _dbh.checkforward(token);
                            if (forward == "")
                            {
                                if (sett.sendlongsms) SendTokenLong(token); else SendToken(token);
                            }
                            else
                                _dbh.out_queue.Enqueue(token.id, token.request_id, forward, token.dt, token.fromnumber, token.tonumber, token.smstext, token.dcs, token.udhi);
                            _dbh.out_queue.DequeueEnd(token.mtype, token.id);
                        }
                        catch (Exception ex)
                        {
                            if (!(ex is SMPPConnectionDropException)) throw ex;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                _dequeueWh.WaitOne();
            }
        }

        void SendTokenLong(DOutQueueToken token)
        {
            if (token.tonumber == "")
            {
                _dbh.outlog.Log(_dbh.outseq.GiveSequence("out_sms"), token.id, DateTime.Now, -100500);
                return;
            }
            byte[] tempud = (token.dcs == 0) ? 
                (sett.UseGSMEncoding ? GSM.encoding.GetBytes(token.smstext) : Encoding.ASCII.GetBytes(token.smstext) ): 
                Convert.FromBase64String(token.smstext);
            string messageId = "";
            bool doreport = sett.reportall ? true : sett.DoReport.Contains(token.fromnumber);
            SMPPStatusCode result = _conn.SendSMS(token.fromnumber, token.tonumber, (byte)token.dcs,
                                tempud, DateTime.MinValue, (token.udhi == 1), sett.MessageRespTimeout, out messageId, doreport);
            if (doreport & messageId != string.Empty)
            {
                try { _dbh.InsReport(messageId, token.id); }
                catch (MySqlException ex)
                {
                    if (ex.Number == 1062)
                        lock (_logger) _logger.Echo("While executing InsReport exception message was: " + ex.Message + "\t requestId:" + token.id);
                    else throw;
                }
            }
            _dbh.outlog.Log(_dbh.outseq.GiveSequence("out_sms"), token.id, DateTime.Now, (int)result);
            _portion++;
        }

        void SendToken(DOutQueueToken token)
        {
            if (token.tonumber == "")
            {
                _dbh.outlog.Log(_dbh.outseq.GiveSequence("out_sms"), token.id, DateTime.Now, -100500);
                return;
            }
            byte[] tempud;
            int maxlen;

            if (token.dcs == 0)
            {
                maxlen = 160;
                tempud = sett.UseGSMEncoding ? GSM.encoding.GetBytes(token.smstext) : Encoding.ASCII.GetBytes(token.smstext);
            }
            else
            {
                maxlen = 140;
                tempud = Convert.FromBase64String(token.smstext);
            }

            string messageId = "";
            bool doreport = sett.DoReport.Contains(token.fromnumber);

            do
            {
                byte[] cur_ud;
                if (tempud.Length <= maxlen)
                {
                    cur_ud = tempud;
                    tempud = new byte[0];
                }
                else
                {
                    cur_ud = new byte[maxlen];
                    Array.Copy(tempud, cur_ud, maxlen);
                    Array.Copy(tempud, maxlen, tempud, 0, tempud.Length - maxlen);
                    Array.Resize(ref tempud, tempud.Length - maxlen);
                }

                SMPPStatusCode result = _conn.SendSMS(token.fromnumber, token.tonumber, (byte)token.dcs,
                                    cur_ud, DateTime.MinValue, (token.udhi == 1), sett.MessageRespTimeout, out messageId, doreport);
                if (doreport)
                {
                    try { _dbh.InsReport(messageId, token.id); }
                    catch (MySqlException ex)
                    {
                        if (ex.Number == 1062)
                            lock (_logger) _logger.Echo("While executing InsReport exception message was: " + ex.Message + "\t requestId:" + token.id);
                        else throw;
                    }
                }
                _dbh.outlog.Log(_dbh.outseq.GiveSequence("out_sms"), token.id, DateTime.Now, (int)result);
                _portion++;
            }
            while (tempud.Length > 0);
        }


        void DequeueTimer(Object state)
        {
            _dequeueWh.Set();
        }

        void ConnectedCheckTimer(Object state)
        {
            foreach (SMSCConnection c in _conn.SmscConns)
            {
                if (!c.IsConnected)
                {
                    MyDisconnected("", c._ipe);
                }
                else
                {
                    MyConnected(c._ipe);
                }
            }
        }

        bool MyIncomeSms(SMPPPDU pdu)
        {
            if (pdu.EsmClass == 4) return MyReportSms(pdu);

            try
            {
                if (!sett.DontTranslit.Contains(pdu.DestinationAddr))
                    pdu.ShortMessage = Tranlitter.Translit(pdu.ShortMessage);
            }
            catch
            {
                //should log it somewhere
            }

            DInQueueToken inTocken = new DInQueueToken();
            long id = -1;
            id = _dbh.incseq.GiveSequence("request");
            inTocken.channel = sett.IncQueueChannel;
            inTocken.dt = DateTime.Now;
            inTocken.fromnumber = pdu.SourceAddr;
            inTocken.id = id;
            inTocken.mtype = sett.PrefixForMtype + pdu.DestinationAddr;
            inTocken.smstext = pdu.ShortMessage;
            if (sett.AddDCSToDest.Contains(pdu.DestinationAddr))
            {
                if (pdu.DataCoding != 0) inTocken.tonumber = "&" + pdu.DataCoding.ToString("X2") + pdu.DestinationAddr; else inTocken.tonumber = pdu.DestinationAddr;
            }
            else
            {
                inTocken.tonumber = pdu.DestinationAddr;
            }

            if (!sett.IgnoreInc.Contains(inTocken.tonumber))
            {
                try
                {
                    _dbh.in_queue.Enqueue(inTocken);
                }
                catch
                {
                    sett.failsms(inTocken.smstext);
                }
            }


            try
            {
                _dbh.inlog.Log(id, DateTime.Now, pdu.SourceAddr, pdu.DestinationAddr, pdu.ShortMessage);
            }
            catch
            {
                sett.failsms(inTocken.smstext);
            }


            if (sett.DoLogIncAff.Keys.Contains(inTocken.tonumber))
            {
                try
                {
                    _dbh.inlogaff.Log(id, sett.DoLogIncAff[inTocken.tonumber]);
                }
                catch
                {
                    sett.failsms(inTocken.smstext);
                }
            }

            return true;
        }

        bool MyReportSms(SMPPPDU pdu)
        {
            if (sett.IgnoreReport.Contains(pdu.DestinationAddr)) return true;

            try
            {
                string azcId = "";
                byte status = 0;
                int error = 0;
                if (sett.reportKind == ReportKind.Normal)
                {
                    if (!pdu.Optionals.ContainsKey(SMPPOptionalTag.ReceiptedMessageId) || !pdu.Optionals.ContainsKey(SMPPOptionalTag.MessageState)) return true;

                    if (sett.ShouldParseReport) // here we should parse Report Message
                    {
                        //id:65799398 sub:001 dlvrd:001 submit date:1612011338 done date:1612011338 stat:UNDELIV err:905 Text:Test SMS for report
                        var errorParam = pdu.ShortMessage.Split(' ').FirstOrDefault(param => param.StartsWith("err"));
                        if (errorParam != null && errorParam.Contains(':'))
                        {
                            int.TryParse(errorParam.Split(':')[1], out error);
                        }
                    }

                    azcId = pdu.Optionals[SMPPOptionalTag.ReceiptedMessageId].StringValue.Trim(new char[] { (char)0x00 });
                    status = error == 905 ? (byte)51 : pdu.Optionals[SMPPOptionalTag.MessageState].BytesValue[0];  //51 is double SMS response
                }
                else //ReportKind.Mandarin
                {
                    try
                    {
                        string[] shortm = pdu.ShortMessage.Split(':');//"id:166157226230786151566598729 sub:000 dlvrd:000 submit date:1603110954 done date:1603110954 stat:DELIVRD err:002 text: t"
                        azcId = new string((from c in shortm[1] where Char.IsDigit(c) select c).ToArray<char>());
                        status = (byte)int.Parse(new string((from c in shortm[7] where Char.IsDigit(c) select c).ToArray<char>()));

                    }
                    catch (Exception E)
                    {
                        sett.failsms("report:" + E.Message);
                        return true;
                    }
                }

                long gId = -1;
                int retries = 0;
                while (gId == -1 && retries <= 5)
                {
                    gId = _dbh.UpdReport(azcId, status);
                    Thread.Sleep(100);
                    retries++;
                }

                if ((gId != -1 && !sett.dontqueuereport) || sett.DoQueueReport.Contains(pdu.DestinationAddr))
                {
                    var mtype = pdu.DestinationAddr;
                    var shortNumber = pdu.DestinationAddr;
                    if (sett.IncQueueChannel == "GMS-SMS") // gms report
                    {
                        mtype = _dbh.GetReportMtype(gId);
                        shortNumber = mtype;
                    }
                    else if (sett.IncQueueChannel == "SMS-ATL-BULK") //for updating report statuses via Request Queue
                    {
                        mtype = "BULK_" + mtype;
                    }
                    _dbh.in_queue.Enqueue(_dbh.incseq.GiveSequence("request"), mtype, "REPORT", DateTime.Now, pdu.SourceAddr, shortNumber, gId.ToString() + "\t" + status.ToString());
                }
            }
            catch (Exception ex)
            {
                sett.failsms("report:" + ex.Message);
            }
            return true;
        }

        void MyConnected(IPEndPoint ipe)
        {
            int iIpe = -1;
            for (int i = 0; i < sett.IPES.Count; i++)
            {
                if (sett.IPES[i] == ipe)
                {
                    iIpe = i;
                }
            }
            if (iIpe >= 0)
            {
                _dbh.UpdStatus("connected", 0, sett.FieldNames[iIpe]);
            }
        }

        void MyDisconnected(string commandStatus, IPEndPoint ipe)
        {
            int iIpe = -1;
            for (int i = 0; i < sett.IPES.Count; i++)
            {
                if (sett.IPES[i] == ipe)
                {
                    iIpe = i;
                }
            }

            if (iIpe >= 0)
            {
                _dbh.UpdStatus("not connected - " + commandStatus, 1, sett.FieldNames[iIpe]);
            }
        }
    }
}
