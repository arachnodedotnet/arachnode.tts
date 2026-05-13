using System;

namespace Trade.Tests.Models
{
    /// <summary>
    /// Result of comprehensive integrity verification tests
    /// Contains detailed statistics about verification success rates
    /// </summary>
    [Serializable]
    public class IntegrityVerificationResult
    {
        public int TotalLookupTests { get; set; }
        public int SuccessfulLookups { get; set; }
        public int FailedLookups { get; set; }
        public int PriceMismatches { get; set; }
        public double IntegrityRate { get; set; }
    }
}