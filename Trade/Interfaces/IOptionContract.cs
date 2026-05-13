using System;

namespace Trade.Interfaces
{
    /// <summary>
    ///     Interface representing a single option contract for implied volatility (IV) calculation.
    ///     Defines the essential parameters required for Black-Scholes option pricing models and IV solving algorithms.
    ///     This interface is used by ImpliedVolatilitySolver, OptionContract implementations, and other option pricing
    ///     components throughout the trading system to ensure consistent parameter handling and calculations.
    /// </summary>
    public interface IOptionContract
    {
        /// <summary>
        ///     The valuation date for which the underlying asset price is valued and option parameters are set.
        ///     This represents the current trading date or specific historical date for analysis purposes.
        ///     Used as the reference point for time-to-expiration calculations and market data lookups.
        ///     Must be a valid trading date (typically excludes weekends and market holidays).
        /// </summary>
        DateTime ValuationDate { get; set; }

        /// <summary>
        ///     The strike price of the option contract (also known as the exercise price).
        ///     This is the predetermined price at which the option holder can buy (for calls) or sell (for puts)
        ///     the underlying asset when exercising the option.
        ///     Must be a positive value and typically aligns with standard option strike increments for the underlying.
        ///     Used as the 'K' parameter in Black-Scholes pricing formulas.
        /// </summary>
        double Strike { get; set; }

        /// <summary>
        ///     Time to expiration expressed in years as a decimal fraction.
        ///     Represents the remaining time until the option contract expires and becomes worthless.
        ///     Examples: 0.25 for 3 months (quarterly options), 0.0274 for 10 calendar days, 0.00396 for 1 trading day.
        ///     Must be non-negative, with 0.0 representing options at expiration (intrinsic value only).
        ///     Used as the 'T' parameter in Black-Scholes calculations for time decay modeling.
        /// </summary>
        double TimeToExpiration { get; set; }

        /// <summary>
        ///     Risk-free interest rate expressed as a decimal (annual rate).
        ///     Represents the theoretical return on a risk-free investment (typically US Treasury rates).
        ///     Examples: 0.05 for 5% annual rate, 0.0025 for 0.25% (near-zero rate environment).
        ///     Should match the time horizon of the option (use Treasury rate with similar maturity).
        ///     Used as the 'r' parameter in Black-Scholes for discounting future cash flows to present value.
        /// </summary>
        double RiskFreeRate { get; set; }

        /// <summary>
        ///     Continuous dividend yield of the underlying asset expressed as a decimal (annual rate).
        ///     Represents the expected dividend payments over the option's remaining life, continuously compounded.
        ///     Examples: 0.02 for 2% annual dividend yield, 0.0 for non-dividend paying stocks.
        ///     For index options, this often represents the average dividend yield of index components.
        ///     Used as the 'q' parameter in Black-Scholes dividend-adjusted pricing model.
        /// </summary>
        double DividendYield { get; set; }

        /// <summary>
        ///     Current market price of the option contract as observed in the options market.
        ///     This is the actual traded price used for implied volatility calculations and model validation.
        ///     Must be positive and typically exceeds the option's intrinsic value (time value > 0).
        ///     For American options, should reflect any early exercise premium above European option value.
        ///     Used as the target price when solving for implied volatility using numerical methods.
        /// </summary>
        double MarketPrice { get; set; }

        /// <summary>
        ///     Option type indicator specifying the contract's rights and obligations.
        ///     True indicates a call option: gives the holder the right to BUY the underlying at the strike price.
        ///     False indicates a put option: gives the holder the right to SELL the underlying at the strike price.
        ///     This flag determines which Black-Scholes formula variant to use in pricing calculations.
        ///     Critical for correct payoff calculations and Greeks (delta, gamma, theta, vega, rho) computation.
        /// </summary>
        bool IsCall { get; set; }
    }
}