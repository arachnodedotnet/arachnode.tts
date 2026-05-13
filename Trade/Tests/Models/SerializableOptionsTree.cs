using System;
using System.Collections.Generic;
using Trade.Polygon2;

namespace Trade.Tests.Models
{
    /// <summary>
    /// Serializable wrapper for options compression tree with metadata and sample data
    /// Used for testing serialization/deserialization integrity
    /// </summary>
    [Serializable]
    public class SerializableOptionsTree
    {
        public OptionsCompressionTree Tree { get; set; }
        public CompressionStats CompressionStats { get; set; }
        public TreeMetadata TreeMetadata { get; set; }
        public Dictionary<string, SerializableOptionData> SampleData { get; set; }
        public DateTime SerializationTimestamp { get; set; }
    }
}