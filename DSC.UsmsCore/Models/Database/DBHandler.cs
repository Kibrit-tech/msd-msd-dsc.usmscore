using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;

using DSC.Core.Database;

using MySql.Data.MySqlClient;

namespace DSC.UsmsCore.Models.Database
{
    internal class DbHandler
    {
        public SafeDInQueue InQueue;
        public SafeDOutQueue OutQueue;
        public SafeMyDbSequence InSeq;
        public SafeMyDbSequence OutSeq;
        public SafeDOutgoingSmsLog OutgoingSmsLog;
        public SafeDIncomingSmsLog IncomingSmsLog;

        private readonly DbConnection _dbInConnection;
        private readonly DbConnection _dbOutConnection;
        private readonly DbConnection _dbStatusConnection;

        private readonly DbCommand _dbUpdateAlertStatus;
        private readonly DbCommand _dbUpdateReportGeneralId;
        private readonly DbCommand _dbInsertReport;
        private readonly DbCommand _dbCheckReport;
        private readonly DbCommand _dbUpdateReport;
        private readonly DbCommand _dbGetReportMtype;

        private readonly object _lockObj;

        public DbHandler(IEnumerable<string> mtype, string reportIdName, string inConnectionString, string outConnectionString, string statusConnectionString)
        {
            _dbInConnection = new ReConnection(new MySqlConnection(inConnectionString));
            _dbInConnection.Open();

            _dbOutConnection = new ReConnection(new MySqlConnection(outConnectionString));
            _dbOutConnection.Open();

            _dbStatusConnection = new ReConnection(new MySqlConnection(statusConnectionString));
            _dbStatusConnection.Open();

            _lockObj = new object();

            InSeq = new SafeMyDbSequence(_lockObj, _dbInConnection);
            InQueue = new SafeDInQueue(_lockObj, _dbInConnection);
            IncomingSmsLog = new SafeDIncomingSmsLog(_lockObj, _dbInConnection);

            OutQueue = new SafeDOutQueue(_lockObj, _dbOutConnection);
            OutQueue.SetDefaultMtype(mtype);
            OutgoingSmsLog = new SafeDOutgoingSmsLog(_lockObj, _dbOutConnection);
            OutSeq = new SafeMyDbSequence(_lockObj, _dbOutConnection);

            _dbUpdateAlertStatus = _dbStatusConnection.CreateCommand();
            _dbUpdateAlertStatus.CommandText = @"update status set status=?status, istatus=?istatus, updated=now() where name=?fieldname ";
            _dbUpdateAlertStatus.Parameters.Add(new MySqlParameter("status", MySqlDbType.String));
            _dbUpdateAlertStatus.Parameters.Add(new MySqlParameter("istatus", MySqlDbType.Int32));
            _dbUpdateAlertStatus.Parameters.Add(new MySqlParameter("fieldname", MySqlDbType.String));

            _dbUpdateReportGeneralId = _dbInConnection.CreateCommand();
            _dbUpdateReportGeneralId.CommandText = $"update outgoing_reports set g_id=?g_id, status=null, statusdt=null where {reportIdName}=?report_id and smpp=?smpp";
            _dbUpdateReportGeneralId.Parameters.Add(new MySqlParameter("report_id", MySqlDbType.String));
            _dbUpdateReportGeneralId.Parameters.Add(new MySqlParameter("smpp", MySqlDbType.String));
            _dbUpdateReportGeneralId.Parameters.Add(new MySqlParameter("g_id", MySqlDbType.Int64));

            _dbInsertReport = _dbInConnection.CreateCommand();
            _dbInsertReport.CommandText = $"insert into outgoing_reports ({reportIdName}, smpp, g_id) values (?report_id, ?smpp, ?g_id)";
            _dbInsertReport.Parameters.Add(new MySqlParameter("report_id", MySqlDbType.String));
            _dbInsertReport.Parameters.Add(new MySqlParameter("smpp", MySqlDbType.String));
            _dbInsertReport.Parameters.Add(new MySqlParameter("g_id", MySqlDbType.Int64));

            _dbCheckReport = _dbInConnection.CreateCommand();
            _dbCheckReport.CommandText = $"select g_id from outgoing_reports where {reportIdName}=?report_id and smpp=?smpp";
            _dbCheckReport.Parameters.Add(new MySqlParameter("report_id", MySqlDbType.String));
            _dbCheckReport.Parameters.Add(new MySqlParameter("smpp", MySqlDbType.String));

            _dbUpdateReport = _dbInConnection.CreateCommand();
            _dbUpdateReport.CommandText = "update outgoing_reports set status=?status, statusdt=now() where g_id=?g_id";
            _dbUpdateReport.Parameters.Add(new MySqlParameter("g_id", MySqlDbType.Int64));
            _dbUpdateReport.Parameters.Add(new MySqlParameter("status", MySqlDbType.UByte));

            _dbGetReportMtype = _dbInConnection.CreateCommand();
            _dbGetReportMtype.CommandText = @"select phonefrom from outgoing_sms_server_log where id=?id";
            _dbGetReportMtype.Parameters.Add(new MySqlParameter("id", MySqlDbType.Int64));
        }

        public void UpdateStatus(string status, int iStatus, string fieldName)
        {
            if(status.Length > 128) status = status.Substring(0, 128);

            lock (_lockObj)
            {
                while (true)
                {
                    try
                    {
                        _dbUpdateAlertStatus.Parameters["status"].Value = status;
                        _dbUpdateAlertStatus.Parameters["istatus"].Value = iStatus;
                        _dbUpdateAlertStatus.Parameters["fieldname"].Value = fieldName;
                        _dbUpdateAlertStatus.ExecuteNonQuery();
                    }
                    catch (MySqlException exc)
                    {
                        if(exc.Number == 1213)
                        {
                            Thread.Sleep(50);
                            continue;
                        } // ER_LOCK_DEADLOCK
                    }

                    break;
                }
            }
        }

        public bool IfReportExist(string reportId, string smppName)
        {
            lock (_lockObj)
            {
                _dbCheckReport.Parameters["report_id"].Value = reportId;
                _dbCheckReport.Parameters["smpp"].Value = smppName;
                var generalId = _dbCheckReport.ExecuteScalar();

                if (generalId == null || generalId == DBNull.Value)
                {
                    return false;
                }

                return true;
            }
        }

        public void UpdateReportGeneralIdForDuplicate(string reportId, string smppName, long gId)
        {
            lock (_lockObj)
            {
                _dbUpdateReportGeneralId.Parameters["report_id"].Value = reportId;
                _dbUpdateReportGeneralId.Parameters["smpp"].Value = smppName;
                _dbUpdateReportGeneralId.Parameters["g_id"].Value = gId;
                _dbUpdateReportGeneralId.ExecuteNonQuery();
            }
        }

        public void InsertReport(string reportId, string smppName, long gId)
        {
            lock (_lockObj)
            {
                _dbInsertReport.Parameters["report_id"].Value = reportId;
                _dbInsertReport.Parameters["smpp"].Value = smppName;
                _dbInsertReport.Parameters["g_id"].Value = gId;
                _dbInsertReport.ExecuteNonQuery();
            }
        }

        public long UpdateReport(string reportId, string smppName, byte status)
        {
            lock (_lockObj)
            {
                _dbCheckReport.Parameters["report_id"].Value = reportId;
                _dbCheckReport.Parameters["smpp"].Value = smppName;
                var generalId = _dbCheckReport.ExecuteScalar();

                if(generalId == null || generalId == DBNull.Value)
                {
                    return -1;
                }

                _dbUpdateReport.Parameters["g_id"].Value = generalId;
                _dbUpdateReport.Parameters["status"].Value = status;
                _dbUpdateReport.ExecuteNonQuery();

                return Convert.ToInt64(generalId);
            }
        }

        public string GetReportMtype(long generalId)
        {
            lock (_lockObj)
            {
                _dbGetReportMtype.Parameters["id"].Value = generalId;
                return _dbGetReportMtype.ExecuteScalar().ToString();
            }
        }

        ~DbHandler()
        {
            _dbInConnection.Close();
            _dbOutConnection.Close();
            _dbStatusConnection.Close();
        }
    }
}