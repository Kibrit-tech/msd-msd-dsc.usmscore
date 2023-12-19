using System;
using System.Data.Common;

using DSC.Core.Common;
using DSC.Core.Database.LogOperations;

namespace DSC.UsmsCore.Models.Database
{
    public class SafeDIncomingSmsLog : DIncomingSmsLog
    {
        private readonly object _lockObj;

        public SafeDIncomingSmsLog(object lockObj, DbConnection conn) : base(conn)
        {
            _lockObj = lockObj;
        }

        public new void Log(long id, DateTime dt, string phoneFrom, string phoneTo, string smsText, Operator oper)
        {
            lock (_lockObj) base.Log(id, dt, phoneFrom, phoneTo, smsText, oper);
        }
    }
}