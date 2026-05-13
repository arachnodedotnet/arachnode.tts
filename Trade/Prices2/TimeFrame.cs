namespace Trade.Prices2
{
    /// <summary>
    ///     Enumeration defining supported time intervals for price data aggregation and analysis.
    ///     Values represent the duration in minutes for each time frame, enabling efficient
    ///     time-based calculations and data aggregation throughout the trading system.
    ///     Used extensively for chart timeframes, technical analysis, and backtesting operations.
    /// </summary>
    public enum TimeFrame
    {
        /// <summary>
        ///     1-minute timeframe for high-frequency analysis and scalping strategies.
        ///     Provides the finest granularity of market data for precise entry/exit timing.
        ///     Used for real-time trading, order flow analysis, and microstructure studies.
        ///     Value: 1 minute
        /// </summary>
        M1 = 1,

        /// <summary>
        ///     5-minute timeframe commonly used for short-term trading and quick trend analysis.
        ///     Balances detail with noise reduction for active trading strategies.
        ///     Popular for day trading, momentum strategies, and technical pattern recognition.
        ///     Value: 5 minutes
        /// </summary>
        M5 = 5,

        /// <summary>
        ///     10-minute timeframe for intermediate short-term analysis and trend filtering.
        ///     Provides smoother price action while maintaining reasonable detail.
        ///     Used for swing entries, trend confirmation, and multi-timeframe analysis.
        ///     Value: 10 minutes
        /// </summary>
        M10 = 10,

        /// <summary>
        ///     15-minute timeframe widely used for intraday trading and technical analysis.
        ///     Standard timeframe for many day trading strategies and chart pattern analysis.
        ///     Offers good balance between detail and trend clarity for active traders.
        ///     Value: 15 minutes
        /// </summary>
        M15 = 15,

        /// <summary>
        ///     30-minute timeframe for intermediate-term intraday analysis and swing trading.
        ///     Commonly used for trend following, support/resistance levels, and position sizing.
        ///     Provides clearer trend direction while filtering out short-term market noise.
        ///     Value: 30 minutes
        /// </summary>
        M30 = 30,

        /// <summary>
        ///     1-hour timeframe for broader trend analysis and swing trading strategies.
        ///     Standard timeframe for position trading, trend identification, and major support/resistance.
        ///     Excellent for multi-day analysis and intermediate-term technical indicators.
        ///     Value: 60 minutes (1 hour)
        /// </summary>
        H1 = 60,

        /// <summary>
        ///     4-hour timeframe for longer-term analysis and position trading strategies.
        ///     Used for major trend identification, weekly planning, and risk management.
        ///     Provides clear trend direction while filtering out intraday volatility.
        ///     Value: 240 minutes (4 hours)
        /// </summary>
        H4 = 240,

        /// <summary>
        ///     Daily timeframe for long-term analysis, backtesting, and strategic planning.
        ///     The most commonly used timeframe for fundamental analysis and portfolio management.
        ///     Essential for historical backtesting, monthly/yearly performance analysis, and asset allocation.
        ///     Value: 1440 minutes (24 hours/1 day)
        /// </summary>
        D1 = 1440,
        BridgeBar
    }
}