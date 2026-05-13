using System;
using System.Collections.Generic;

namespace Trade.IVPreCalc2
{
    /// <summary>
    /// Represents daily aggregated price data for a contract.
    /// Contains OHLC data, volume, and metadata about the source files
    /// that contributed to the daily aggregation.
    /// </summary>
    internal sealed class DailyContractPrice
    {
        public DateTime Date { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
        public int RecordCount { get; set; }
        public DateTime? FirstTimestamp { get; set; }
        public DateTime? LastTimestamp { get; set; }
        public List<string> SourceFiles { get; set; } = new List<string>();
        
        /// <summary>
        /// Captures each close price encountered during the day's aggregation
        /// </summary>
        public List<double> ClosePrices { get; set; } = new List<double>();
        
        /// <summary>
        /// The first and last contributing file for the day (by minute time)
        /// </summary>
        public string FirstSourceFile { get; set; }
        public string LastSourceFile { get; set; }
    }
}