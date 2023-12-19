using System.Data.Common;

using DSC.Core.Database.LogOperations;

namespace DSC.UsmsCore.Models.Database
{
    public class SafeDIncomingLogsAff : DIncomingLogsAff
    {
        private readonly object _lockObj;

        public SafeDIncomingLogsAff(object lockObj, DbConnection conn) : base(conn)
        {
            _lockObj = lockObj;
        }

        public new void Log(long id, string aff)
        {
            lock (_lockObj) base.Log(id, aff);
        }
    }
}