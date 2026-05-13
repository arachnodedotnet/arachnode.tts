using System;

namespace Trade.Tests.Models
{
    /// <summary>
    /// A specific data point used for verifying tree integrity
    /// Contains expected values that should match tree lookups
    /// </summary>
    [Serializable]
    public class VerificationPoint
    {
        public DateTime Timestamp { get; set; }
        public double ExpectedPrice { get; set; }
        public double ExpectedVolume { get; set; }
        public int RecordIndex { get; set; }
    }
}