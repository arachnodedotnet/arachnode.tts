using Amazon.Runtime.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trade.Form4
{
    /// <summary>
    /// Represents an individual Form 4 purchase signal (real-time tracking)
    /// Uses filing date (when WE discovered it) as the signal date
    /// </summary>
    public class IndividualForm4Signal
    {
        public bool Cached{ get; set; }
        public string XmlUrl { get; set; }
        public string Ticker { get; set; }
        public DateTime SignalDate { get; set; }         // Filing date (when we found it)
        public DateTime? TransactionDate { get; set; }    // Actual transaction date
        public DateTime? PeriodOfReport { get; set; }     // Period end date for the Form 4 filing
        public decimal PurchaseValue { get; set; }
        public string ReportingOwner { get; set; }
        public string OfficerTitle { get; set; }
        public bool IsDirector { get; set; }
        public bool IsOfficer { get; set; }
        public bool IsTenPercentOwner { get; set; }
        public bool IsOther { get; set; }
        public decimal SharesTransacted { get; set; }
        public decimal PricePerShare { get; set; }
        public string AccessionNumber { get; set; }
        public string CIK { get; set; }
        public bool Aff10b5One { get; set; }
        public PriceActionMetrics PriceAction { get; set; }

        public override string ToString()
        {
            var role = IsOfficer && !string.IsNullOrEmpty(OfficerTitle) ? OfficerTitle :
                IsOfficer ? "Officer" :
                IsDirector ? "Director" : "Insider";

            return $"{Ticker} | Filed: {SignalDate:yyyy-MM-dd} | {ReportingOwner} ({role}) | ${PurchaseValue:N0} | ${AccessionNumber}";
        }
    }
}
