using System;

namespace Trade.Tests.Models
{
    /// <summary>
    /// Metadata information for custom serialization operations
    /// Contains type information and field mappings for property-by-property serialization
    /// </summary>
    [Serializable]
    public class SerializationMetadata
    {
        public string OriginalTypeName { get; set; }
        public DateTime SerializationTimestamp { get; set; }
        public int FieldCount { get; set; }
        public string[] FieldNames { get; set; }
        public string[] FieldTypes { get; set; }
    }
}