using System;
using System.Collections.Generic;
using Trade.Prices2;

namespace Trade.Polygon2
{
    /// <summary>
    ///     Result container for minute data retrieval
    /// </summary>
    public class MinuteDataResult
    {
        public List<PriceRecord> MinuteRecords { get; set; } = new List<PriceRecord>();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalMinutes => MinuteRecords.Count;
        public int TradingDays { get; set; }
        public double AverageMinutesPerDay => TradingDays > 0 ? (double)TotalMinutes / TradingDays : 0;

        public override string ToString()
        {
            return $"Retrieved {TotalMinutes} minute records from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd} " +
                   $"across {TradingDays} trading days (avg {AverageMinutesPerDay:F1} minutes/day)";
        }
    }
}