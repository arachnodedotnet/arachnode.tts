using System;
using Trade.Polygon2;

namespace Trade.Tests.Models
{
    /// <summary>
    /// Verification information for a single option contract
    /// Contains contract details and associated verification points
    /// </summary>
    [Serializable]
    public class ContractVerificationInfo
    {
        public string OptionSymbol { get; set; }
        public DateTime ExpirationDate { get; set; }
        public OptionType OptionType { get; set; }
        public double StrikePrice { get; set; }
        public VerificationPoint[] VerificationPoints { get; set; }
    }
}