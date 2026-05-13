using System;
using Trade.Polygon2;

namespace Trade.Tests.Models
{
    /// <summary>
    /// Result of a tree deserialization operation
    /// Contains performance metrics, integrity check results, and deserialized tree reference
    /// </summary>
    [Serializable]
    public class DeserializationResult
    {
        public bool Success { get; set; }
        public long DeserializationTimeMs { get; set; }
        public bool IntegrityCheck { get; set; }
        public int SampleTests { get; set; }
        public int SampleMatches { get; set; }
        public string ErrorMessage { get; set; }
        public OptionsCompressionTree OptionsCompressionTree { get; set; }
    }
}