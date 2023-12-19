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

namespace DSC.USMSInterface
{
    class DBHandler
    {
        public SafeDInQueue in_queue;
        public SafeDOutQueue out_queue;
        public SafeMyDBSequence incseq;
        public SafeMyDBSequence outseq;
        public SafeDOutgoingSMSLog outlog;
        public SafeDIncomingSMSLog inlog;
        public SafeDIncomingLogsAff inlogaff;

        DbConnection DBIncConn;
        DbConnection DBOutConn;
        DbConnection DBStatusConn;
        DbCommand updstatus;
        DbCommand DBInsReport;
        DbCommand DBCheckReport;
        DbCommand DBUpdReport;
        DbCommand DBGetReportMtype;
        DbCommand DBCheckChannel;

        object lockObj = new object();

        public DBHandler(List<string> mtype, string DBInConnS, string DBOutConnS, string DBStatusConnS,string reportidname)
        {
            DBIncConn = new ReConnection(new MySqlConnection(DBInConnS));
            DBIncConn.Open();

            DBOutConn = new ReConnection(new MySqlConnection(DBOutConnS));
            DBOutConn.Open();

            DBStatusConn = new ReConnection(new MySqlConnection(DBStatusConnS));
            DBStatusConn.Open();

            incseq = new SafeMyDBSequence(lockObj, DBIncConn);
            in_queue = new SafeDInQueue(lockObj, DBIncConn);
            inlog = new SafeDIncomingSMSLog(lockObj, DBIncConn);
            inlogaff = new SafeDIncomingLogsAff(lockObj, DBIncConn);

            updstatus = DBStatusConn.CreateCommand();
            updstatus.CommandText =
                "update status set " +
                " status=?status, istatus=?istatus, updated=now() " +
                " where name=?fieldname ";
            updstatus.Parameters.Add(new MySqlParameter("status", MySqlDbType.String));
            updstatus.Parameters.Add(new MySqlParameter("istatus", MySqlDbType.Int32));
            updstatus.Parameters.Add(new MySqlParameter("fieldname", MySqlDbType.String));

            DBInsReport = DBIncConn.CreateCommand();
            DBInsReport.CommandText = "insert into outgoing_reports ("+ reportidname + ",g_id) values (?azercell_id,?g_id)";
            DBInsReport.Parameters.Add(new MySqlParameter("azercell_id", MySqlDbType.String));
            DBInsReport.Parameters.Add(new MySqlParameter("g_id", MySqlDbType.Int64));

            DBCheckReport = DBIncConn.CreateCommand();
            DBCheckReport.CommandText = "select g_id from outgoing_reports where " + reportidname + "=?azercell_id";
            DBCheckReport.Parameters.Add(new MySqlParameter("azercell_id", MySqlDbType.String));

            DBUpdReport = DBIncConn.CreateCommand();
            DBUpdReport.CommandText = "update outgoing_reports set status=?status,statusdt=now() where " + reportidname + "=?azercell_id";
            DBUpdReport.Parameters.Add(new MySqlParameter("azercell_id", MySqlDbType.String));
            DBUpdReport.Parameters.Add(new MySqlParameter("status", MySqlDbType.UByte));

            DBGetReportMtype = DBIncConn.CreateCommand();
            DBGetReportMtype.CommandText = @"select phonefrom from outgoing_sms_server_log where id=?id";
            DBGetReportMtype.Parameters.Add(new MySqlParameter("id", MySqlDbType.Int64));

            DBCheckChannel = DBOutConn.CreateCommand();
            DBCheckChannel.CommandText = "select channel from general_log where id=?id";
            DBCheckChannel.Parameters.Add(new MySqlParameter("id", MySqlDbType.Int64));

            out_queue = new SafeDOutQueue(lockObj, DBOutConn);
            out_queue.SetDefaultMtype(mtype);
            outlog = new SafeDOutgoingSMSLog(lockObj, DBOutConn);
            outseq = new SafeMyDBSequence(lockObj, DBOutConn);
        }

        ~DBHandler()
        {
            DBIncConn.Close();
            DBOutConn.Close();
            DBStatusConn.Close();
        }

        public void UpdStatus(string status,int istatus,string fieldname)
        {
            if (status.Length > 128) status = status.Substring(0, 128);
            lock (lockObj)
            {
                while (true)
                {
                    try
                    {
                        updstatus.Parameters["status"].Value = status;
                        updstatus.Parameters["istatus"].Value = istatus;
                        updstatus.Parameters["fieldname"].Value = fieldname;
                        updstatus.ExecuteNonQuery();
                    }
                    catch (MySqlException ex)
                    {
                        if (ex.Number == 1213) { Thread.Sleep(50); continue; }// ER_LOCK_DEADLOCK
                    }
                    break;
                }
            }
        }

        public void InsReport(string azc_id, long g_id)
        {
            lock (lockObj)
            {
                DBInsReport.Parameters["azercell_id"].Value = azc_id;
                DBInsReport.Parameters["g_id"].Value = g_id;
                DBInsReport.ExecuteNonQuery();
            }
        }

        public long UpdReport(string azc_id, byte status)
        {
            lock (lockObj)
            {
                DBCheckReport.Parameters["azercell_id"].Value = azc_id;
                object res = DBCheckReport.ExecuteScalar();
                if (res == null || res == DBNull.Value)
                {
                    return -1;
                }
                else
                {
                    DBUpdReport.Parameters["azercell_id"].Value = azc_id;
                    DBUpdReport.Parameters["status"].Value = status;
                    DBUpdReport.ExecuteNonQuery();
                    return Convert.ToInt64(res);
                }
            }
        }

        public string GetReportMtype(long genId)
        {
            lock (lockObj)
            {
                DBGetReportMtype.Parameters["id"].Value = genId;
                return DBGetReportMtype.ExecuteScalar().ToString();
            }
        }

        public string checkforward(DOutQueueToken token)
        {
            lock (lockObj)
            {
                DBCheckChannel.Parameters["id"].Value = token.id;
                object ochannel = DBCheckChannel.ExecuteScalar();
                string channel = "";
                if (ochannel != null) channel = Convert.ToString(ochannel);
                if (channel == "FORWWP") return "NewWapPortal";
                return "";
            }
        }
    }

    public class SafeDInQueue : DInQueue
    {
        object lockObj;
        public SafeDInQueue(object lockObj, DbConnection conn):base(conn)
        {
            this.lockObj = lockObj;
        }

        public new void Enqueue(DInQueueToken token)
        {
            lock(lockObj) base.Enqueue(token.id, token.mtype, token.channel, token.dt, token.fromnumber, token.tonumber, token.smstext);
        }

        public new void Enqueue(long id, string mtype, string channel, DateTime dt, string fromnumber, string tonumber, string smstext)
        {
            lock (lockObj) base.Enqueue(id, mtype, channel, dt, fromnumber, tonumber, smstext);
        }
    }

    public class SafeDOutQueue : DOutQueue
    {
        object lockObj;
        public SafeDOutQueue(object lockObj, DbConnection conn)
            : base(conn)
        {
            this.lockObj = lockObj;
        }

        public new bool DequeueStart(DOutQueueToken token)
        {
            lock (lockObj) return base.DequeueStart(token);
        }

        public new void DequeueEnd(string mtype, long id)
        {
            lock(lockObj) base.DequeueEnd(mtype, id);
        }
    }

    public class SafeMyDBSequence : MyDBSequence
    {
        object lockObj;
        public SafeMyDBSequence(object lockObj, DbConnection conn)
            : base(conn)
        {
            this.lockObj = lockObj;
        }

        public new long GiveSequence(string sname)
        {
            lock (lockObj) return base.GiveSequence(sname);
        }
    }

    public class SafeDOutgoingSMSLog : DOutgoingSMSLog 
    {
        object lockObj;
        public SafeDOutgoingSMSLog(object lockObj, DbConnection conn)
            : base(conn)
        {
            this.lockObj = lockObj;
        }

        public new void Log(long id, long gen_id, DateTime dt, int result)
        {
            lock (lockObj) base.Log(id, gen_id, dt, result);
        }
    }

    public class SafeDIncomingSMSLog : DIncomingSMSLog
    {
        object lockObj;
        public SafeDIncomingSMSLog(object lockObj, DbConnection conn)
            : base(conn)
        {
            this.lockObj = lockObj;
        }

        public new void Log(long id, DateTime dt, string phonefrom, string phoneto, string smstext)
        {
            lock (lockObj) base.Log(id, dt, phonefrom, phoneto, smstext);
        }
    }

    public class SafeDIncomingLogsAff : DIncomingLogsAff
    {
        object lockObj;
        public SafeDIncomingLogsAff(object lockObj, DbConnection conn)
            : base(conn)
        {
            this.lockObj = lockObj;
        }

        public new void Log(long id, string aff)
        {
            lock (lockObj) base.Log(id,aff);
        }
    }

}
