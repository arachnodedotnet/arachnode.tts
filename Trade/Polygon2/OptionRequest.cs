using System;

namespace Trade.Polygon2
{
    /// <summary>
    ///     Represents an option contract request for Polygon.io API data retrieval operations.
    ///     This class encapsulates all the necessary parameters for requesting option market data,
    ///     including contract specifications, timing information, and API routing details.
    ///     Used throughout the trading system for option data fetching, caching, and validation.
    /// </summary>
    public class OptionRequest
    {
        #region Core Contract Properties

        /// <summary>
        ///     The underlying asset symbol for which the option is written (e.g., "SPY", "AAPL", "TSLA").
        ///     This represents the stock or ETF that the option contract references.
        ///     Used for grouping related option contracts and underlying price lookups.
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        ///     The complete option symbol in OCC (Options Clearing Corporation) format.
        ///     Format example: "SPY240814C00390000" where:
        ///     - SPY: underlying symbol
        ///     - 240814: expiration date (YYMMDD)
        ///     - C: option type (C=Call, P=Put)
        ///     - 00390000: strike price in 8-digit format ($390.00)
        ///     This is the primary identifier used by Polygon.io API for option data requests.
        /// </summary>
        public string OptionSymbol { get; set; }

        /// <summary>
        ///     The expiration date of the option contract.
        ///     This is the last trading day for the option before it expires and becomes worthless.
        ///     Used for calculating time to expiration and filtering options by expiration cycles.
        ///     Typically falls on the third Friday of each month for standard options.
        /// </summary>
        public DateTime ExpirationDate { get; set; }

        /// <summary>
        ///     The strike price at which the option can be exercised.
        ///     For call options: the price at which the holder can buy the underlying asset.
        ///     For put options: the price at which the holder can sell the underlying asset.
        ///     Used in option pricing calculations and profit/loss analysis.
        /// </summary>
        public double StrikePrice { get; set; }

        /// <summary>
        ///     The type of option contract: Call or Put.
        ///     Call options give the right to buy the underlying at the strike price.
        ///     Put options give the right to sell the underlying at the strike price.
        ///     Critical for determining option payoff characteristics and pricing models.
        /// </summary>
        public OptionType OptionType { get; set; }

        #endregion

        #region Request Context Properties

        /// <summary>
        ///     The date when this option request was generated or submitted.
        ///     Used for tracking request timing, cache invalidation, and historical analysis.
        ///     Helps determine if cached option data is still current and relevant.
        /// </summary>
        public DateTime RequestDate { get; set; }

        /// <summary>
        ///     The price of the underlying asset at the time of the request.
        ///     Used for calculating moneyness (in-the-money, at-the-money, out-of-the-money).
        ///     Important for option selection algorithms and relative value analysis.
        ///     Provides context for why this particular option was requested.
        /// </summary>
        public double UnderlyingPrice { get; set; }

        /// <summary>
        ///     The specific Polygon.io API endpoint URL or path for retrieving this option's data.
        ///     Allows for different data types (quotes, trades, aggregates) and API versions.
        ///     Used for routing requests to the appropriate Polygon.io service endpoints.
        ///     May include query parameters for date ranges, time intervals, and data formats.
        /// </summary>
        public string ApiEndpoint { get; set; }

        #endregion

        #region Object Override Methods

        /// <summary>
        ///     Determines equality based on option contract specifications.
        ///     Two option requests are considered equal if they reference the same option contract,
        ///     regardless of when the request was made or other contextual information.
        /// </summary>
        /// <param name="obj">The object to compare with this instance</param>
        /// <returns>True if the objects represent the same option contract, false otherwise</returns>
        public override bool Equals(object obj)
        {
            if (obj is OptionRequest other)
                return OptionSymbol == other.OptionSymbol &&
                       ExpirationDate == other.ExpirationDate &&
                       Math.Abs(StrikePrice - other.StrikePrice) < 0.01;

            return false;
        }

        /// <summary>
        ///     Generates a hash code based on the core option contract identifiers.
        ///     Uses option symbol, expiration date, and strike price for consistent hashing.
        ///     This enables efficient use in hash-based collections like Dictionary and HashSet.
        /// </summary>
        /// <returns>A hash code for the current option request</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(OptionSymbol, ExpirationDate, StrikePrice);
        }

        /// <summary>
        ///     Returns a human-readable string representation of the option request.
        ///     Includes key contract details formatted for easy reading and debugging.
        ///     Format: "OptionSymbol OptionType Strike:$XX.XX Exp:YYYY-MM-DD Underlying:$XXX.XX"
        /// </summary>
        /// <returns>A formatted string describing the option request</returns>
        public override string ToString()
        {
            return
                $"{OptionSymbol} {OptionType} Strike:{StrikePrice:F2} Exp:{ExpirationDate:yyyy-MM-dd} Underlying:{UnderlyingPrice:F2}";
        }

        #endregion
    }
}