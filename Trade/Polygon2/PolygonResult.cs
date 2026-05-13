namespace Trade.Polygon2
{
    /// <summary>
    ///     Data transfer object representing Polygon.io minute aggregate result structure.
    ///     Maps directly to the JSON response format from Polygon.io API endpoints for minute-level market data.
    ///     Used for deserializing market data responses and converting them to internal PriceRecord format.
    ///     Property names use single characters to match Polygon.io's compact JSON field naming convention.
    /// </summary>
    public class PolygonResult
    {
        #region Timing Information

        /// <summary>
        ///     Timestamp - the start time of this minute interval in Unix nanoseconds (UTC).
        ///     Represents the precise moment when this minute period began.
        ///     Must be converted to DateTime for use in .NET applications and time-based analysis.
        ///     Polygon.io uses nanosecond precision for high-frequency trading applications.
        /// </summary>
        public long t { get; set; }

        #endregion

        #region OHLC Price Data

        /// <summary>
        ///     Opening price - the first traded price when the market opened for this time period.
        ///     Represents the price at which the first transaction occurred during the minute interval.
        ///     Used as the 'Open' value in OHLC (Open-High-Low-Close) price bars.
        /// </summary>
        public double o { get; set; }

        /// <summary>
        ///     Highest price - the maximum price reached during this minute interval.
        ///     Represents the peak trading price within the time period.
        ///     Used as the 'High' value in OHLC price bars for technical analysis and charting.
        /// </summary>
        public double h { get; set; }

        /// <summary>
        ///     Lowest price - the minimum price reached during this minute interval.
        ///     Represents the lowest trading price within the time period.
        ///     Used as the 'Low' value in OHLC price bars for support/resistance analysis.
        /// </summary>
        public double l { get; set; }

        /// <summary>
        ///     Closing price - the last traded price when the market closed for this time period.
        ///     Represents the final transaction price during the minute interval.
        ///     Used as the 'Close' value in OHLC price bars and for price movement calculations.
        /// </summary>
        public double c { get; set; }

        #endregion

        #region Volume and Transaction Data

        /// <summary>
        ///     Total volume - the number of shares/contracts traded during this minute interval.
        ///     Represents the cumulative trading activity and liquidity for the time period.
        ///     Used for volume analysis, liquidity assessment, and market strength indicators.
        /// </summary>
        public double v { get; set; }

        /// <summary>
        ///     Volume-weighted average price (VWAP) for this minute interval.
        ///     Calculated as the total dollar amount traded divided by the total volume.
        ///     Provides a more accurate representation of the average price than simple arithmetic mean.
        ///     Used for institutional trading benchmarks and fair value assessment.
        /// </summary>
        public double vw { get; set; }

        /// <summary>
        ///     Number of transactions - the total count of individual trades executed during this minute.
        ///     Represents the frequency of trading activity regardless of trade size.
        ///     Used for analyzing market microstructure and trade execution patterns.
        ///     Higher transaction counts often indicate increased market interest or algorithmic activity.
        /// </summary>
        public int n { get; set; }

        #endregion
    }
}