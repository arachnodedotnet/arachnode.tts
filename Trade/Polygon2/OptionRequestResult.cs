using System;
using System.Collections.Generic;
using System.Linq;

namespace Trade.Polygon2
{
    /// <summary>
    ///     Result container for generated option requests
    /// </summary>
    public class OptionRequestResult
    {
        public List<OptionRequest> CallRequests { get; set; } = new List<OptionRequest>();
        public List<OptionRequest> PutRequests { get; set; } = new List<OptionRequest>();
        public List<OptionRequest> AllRequests => CallRequests.Concat(PutRequests).ToList();
        public int TotalUniqueRequests => AllRequests.Count;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int ProcessedDays { get; set; }

        public override string ToString()
        {
            return $"Generated {CallRequests.Count} call requests and {PutRequests.Count} put requests " +
                   $"({TotalUniqueRequests} total) from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd} " +
                   $"over {ProcessedDays} trading days";
        }
    }
}