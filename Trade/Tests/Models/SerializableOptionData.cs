using System;
using Trade.Polygon2;

namespace Trade.Tests.Models
{
    /// <summary>
    /// Serializable representation of option contract data for testing and verification
    /// Contains key metrics and timestamps for validation purposes
    /// </summary>
    [Serializable]
    public class SerializableOptionData
    {
        public DateTime Expiration { get; set; }
        public OptionType OptionType { get; set; }
        public double Strike { get; set; }
        public int RecordCount { get; set; }
        public double FirstPrice { get; set; }
        public double LastPrice { get; set; }
        public DateTime FirstTimestamp { get; set; }
        public DateTime LastTimestamp { get; set; }
        public double AverageVolume { get; set; }
        public double MaxPrice { get; set; }
        public double MinPrice { get; set; }
    }
}