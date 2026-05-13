namespace Trade.Polygon2
{
    /// <summary>
    ///     Option type enumeration for representing call and put option contracts.
    ///     Used throughout the trading system for option classification, pricing calculations,
    ///     and trade execution logic in conjunction with Polygon.io API integration.
    ///     This enum provides type safety for option contract identification and processing.
    /// </summary>
    public enum OptionType
    {
        /// <summary>
        ///     Call option - gives the holder the right to buy the underlying asset at the strike price.
        ///     Call options are typically purchased when expecting the underlying asset price to rise.
        ///     The holder profits when the underlying price exceeds (strike price + premium paid).
        ///     Used in bullish trading strategies and covered call writing.
        /// </summary>
        Call,

        /// <summary>
        ///     Put option - gives the holder the right to sell the underlying asset at the strike price.
        ///     Put options are typically purchased when expecting the underlying asset price to fall.
        ///     The holder profits when the underlying price falls below (strike price - premium paid).
        ///     Used in bearish trading strategies and protective put positions.
        /// </summary>
        Put
    }
}