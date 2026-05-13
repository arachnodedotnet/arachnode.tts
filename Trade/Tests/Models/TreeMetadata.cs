using System;

namespace Trade.Tests.Models
{
    /// <summary>
    /// Metadata about the structure and characteristics of an options compression tree
    /// </summary>
    [Serializable]
    public class TreeMetadata
    {
        public int TotalNodes { get; set; }
        public int UniquePriceValues { get; set; }
        public int TreeDepth { get; set; }
        public double CompressionRatio { get; set; }
    }
}