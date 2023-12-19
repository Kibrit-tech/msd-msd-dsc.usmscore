using System.Data.Common;

using DSC.Core.Database;

namespace DSC.UsmsCore.Models.Database
{
    public class SafeMyDbSequence : MyDbSequence
    {
        private readonly object _lockObj;

        public SafeMyDbSequence(object lockObj, DbConnection conn) : base(conn)
        {
            _lockObj = lockObj;
        }

        public new long GiveSequence(string sname)
        {
            lock (_lockObj) return base.GiveSequence(sname);
        }
    }
}