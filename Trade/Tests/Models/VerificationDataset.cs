using System;

namespace Trade.Tests.Models
{
    /// <summary>
    /// Complete dataset for verifying tree integrity
    /// Contains all contracts and verification points with time range metadata
    /// </summary>
    [Serializable]
    public class VerificationDataset
    {
        public ContractVerificationInfo[] Contracts { get; set; }
        public int UniqueContracts { get; set; }
        public int VerificationPoints { get; set; }
        public DateTime EarliestDate { get; set; }
        public DateTime LatestDate { get; set; }
    }
}