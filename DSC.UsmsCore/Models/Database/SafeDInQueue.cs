using System;
using System.Data.Common;

using DSC.Core.Common;
using DSC.Core.Database.QueueOperations;

namespace DSC.UsmsCore.Models.Database
{
    public class SafeDInQueue : DInQueue
    {
        private readonly object _lockObj;

        public SafeDInQueue(object lockObj, DbConnection conn) : base(conn)
        {
            _lockObj = lockObj;
        }

        public new void Enqueue(DInQueueToken token, Operator oper)
        {
            lock (_lockObj)
                base.Enqueue(token.Id, token.Mtype, token.Channel, token.Dt, token.FromNumber, token.ToNumber, token.SmsText, oper);
        }

        public new void Enqueue(long id, string mtype, string channel, DateTime dt, string fromNumber, string toNumber,
                                string smsText, Operator oper)
        {
            lock (_lockObj) base.Enqueue(id, mtype, channel, dt, fromNumber, toNumber, smsText, oper);
        }
    }
}