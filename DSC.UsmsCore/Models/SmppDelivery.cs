using System.Collections.Generic;

namespace DSC.UsmsCore.Models
{
    public class SmppDelivery
    {
        private static readonly Dictionary<string, byte> DeliveryStatuses = new Dictionary<string, byte>
        {
            {"ACCEPTD", 6},
            {"DELIVRD", 2},
            {"REJECTD", 8},
            {"UNDELIV", 5},
            {"EXPIRED", 3}
        };

        public static byte GetDeliveryStatus(string deliveryStat)
        {
            if(DeliveryStatuses.ContainsKey(deliveryStat))
            {
                return DeliveryStatuses[deliveryStat];
            }

            return 0;
        }
    }
}