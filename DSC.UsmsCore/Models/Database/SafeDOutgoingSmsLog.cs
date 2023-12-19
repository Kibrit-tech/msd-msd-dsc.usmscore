using System;
using System.Data.Common;

using DSC.Core.Common;
using DSC.Core.Database.LogOperations;

namespace DSC.UsmsCore.Models.Database
{
    public class SafeDOutgoingSmsLog : DOutgoingSmsLog
    {
        private readonly object _lockObj;

        public SafeDOutgoingSmsLog(object lockObj, DbConnection conn) : base(conn)
        {
            _lockObj = lockObj;
        }

        public new void Log(long id, long generalId, DateTime dt, int result, Operator oper)
        {
            lock (_lockObj) base.Log(id, generalId, dt, result, oper);
        }
    }
}