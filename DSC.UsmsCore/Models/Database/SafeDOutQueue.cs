using System.Data.Common;

using DSC.Core.Database.QueueOperations;

namespace DSC.UsmsCore.Models.Database
{
    public class SafeDOutQueue : DOutQueue
    {
        private readonly object _lockObj;

        public SafeDOutQueue(object lockObj, DbConnection conn) : base(conn)
        {
            _lockObj = lockObj;
        }

        public new bool DequeueStart(DOutQueueToken token)
        {
            lock (_lockObj) return base.DequeueStart(token);
        }

        public new void DequeueEnd(string mtype, long id)
        {
            lock (_lockObj) base.DequeueEnd(mtype, id);
        }
    }
}