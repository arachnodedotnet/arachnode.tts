using System;

namespace Trade.Tests.Models
{
    /// <summary>
    /// Result of a tree serialization operation
    /// Contains performance metrics and success status
    /// </summary>
    [Serializable]
    public class SerializationResult
    {
        public bool Success { get; set; }
        public double SerializedSizeKB { get; set; }
        public long SerializationTimeMs { get; set; }
        public string FilePath { get; set; }
        public string ErrorMessage { get; set; }
    }
}