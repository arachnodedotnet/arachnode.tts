using System;
using System.Collections.Generic;

namespace Trade.Interfaces
{
    /// <summary>
    ///     Interface for implied volatility calculations and option pricing using the Black-Scholes model.
    ///     This interface handles both forward pricing (given volatility) and inverse pricing (solving for implied
    ///     volatility).
    ///     Provides comprehensive functionality for loading market data, caching calculations, and generating pricing grids.
    ///     Used by ImpliedVolatilitySolver and other option pricing components in the trading system.
    /// </summary>
    public interface IImpliedVolatilitySolver
    {
        #region Data Loading Methods

        /// <summary>
        ///     Load OHLC prices from CSV file and map dates to closing prices.
        ///     This creates the foundation dataset for underlying price lookups used in option calculations.
        /// </summary>
        /// <param name="ohlcCsvPath">Path to CSV file with format: Date,Open,High,Low,Close,Volume</param>
        void LoadClosePrices(string ohlcCsvPath);

        /// <summary>
        ///     Pre-load implied volatilities from options market data CSV file.
        ///     Solves IV for each option contract and caches results for fast retrieval.
        ///     This is useful for backtesting or analyzing historical option market data.
        /// </summary>
        /// <param name="optionsCsvPath">
        ///     Path to CSV with format:
        ///     ValuationDate,Strike,TimeToExpiration,RiskFreeRate,DividendYield,MarketPrice,IsCall
        /// </param>
        void LoadOptionsAndSolveIVs(string optionsCsvPath);

        /// <summary>
        ///     Load close prices and option market data, then solve for implied volatilities.
        ///     Convenience method that combines LoadClosePrices and LoadOptionsAndSolveIVs operations.
        /// </summary>
        /// <param name="ohlcCsvPath">Path to OHLC data CSV file</param>
        /// <param name="optionsMarketDataCsvPath">Path to options market data CSV file</param>
        void LoadWithMarketData(string ohlcCsvPath, string optionsMarketDataCsvPath);

        /// <summary>
        ///     Load close prices and precompute theoretical option prices using a fixed implied volatility.
        ///     This method is useful for scenario analysis or when using a constant volatility assumption.
        /// </summary>
        /// <param name="ohlcCsvPath">Path to OHLC CSV file</param>
        /// <param name="riskFreeRate">Risk-free interest rate as decimal (e.g., 0.05 for 5%)</param>
        /// <param name="dividendYield">Dividend yield as decimal (e.g., 0.02 for 2%)</param>
        /// <param name="fixedIV">Fixed implied volatility to use for all calculations (e.g., 0.20 for 20%)</param>
        /// <param name="isCall">True for call options, false for put options</param>
        void Load(string ohlcCsvPath, double riskFreeRate, double dividendYield, double fixedIV, bool isCall);

        #endregion

        #region Core Pricing Methods

        /// <summary>
        ///     Calculate Black-Scholes option price using the standard formula.
        ///     This is the fundamental option pricing method used throughout the system.
        ///     Formula: C = S*e^(-qT)*N(d1) - K*e^(-rT)*N(d2) for calls
        ///     P = K*e^(-rT)*N(-d2) - S*e^(-qT)*N(-d1) for puts
        /// </summary>
        /// <param name="S">Current underlying stock price (must be positive)</param>
        /// <param name="K">Strike price of the option (must be positive)</param>
        /// <param name="T">Time to expiration in years (must be non-negative, e.g., 0.25 for 3 months)</param>
        /// <param name="r">Risk-free interest rate as decimal (e.g., 0.05 for 5% annual rate)</param>
        /// <param name="q">Dividend yield as decimal (e.g., 0.02 for 2% annual yield)</param>
        /// <param name="sigma">Volatility as decimal (e.g., 0.2 for 20% annual volatility, must be positive)</param>
        /// <param name="isCall">True for call option, false for put option</param>
        /// <returns>Theoretical option price based on Black-Scholes formula</returns>
        /// <exception cref="ArgumentException">Thrown when input parameters are invalid (non-positive prices, negative time, etc.)</exception>
        double BlackScholesPrice(double S, double K, double T, double r, double q, double sigma, bool isCall);

        /// <summary>
        ///     Solve for implied volatility using improved bisection method with dynamic bounds.
        ///     This inverse calculation finds the volatility that makes the theoretical price equal the market price.
        ///     Uses adaptive bounds and enhanced convergence criteria for robust solutions.
        /// </summary>
        /// <param name="S">Current underlying stock price</param>
        /// <param name="K">Strike price of the option</param>
        /// <param name="T">Time to expiration in years</param>
        /// <param name="r">Risk-free interest rate as decimal</param>
        /// <param name="q">Dividend yield as decimal</param>
        /// <param name="marketPrice">Observed market price of the option (must be positive)</param>
        /// <param name="isCall">True for call option, false for put option</param>
        /// <param name="tolerance">Convergence tolerance for bisection method (default: 1e-6)</param>
        /// <param name="maxIterations">Maximum iterations to prevent infinite loops (default: 100)</param>
        /// <returns>Implied volatility as decimal, or NaN if no solution exists within bounds</returns>
        double SolveIV(double S, double K, double T, double r, double q, double marketPrice, bool isCall,
            double tolerance = 1e-6, int maxIterations = 100);

        #endregion

        #region Cached Retrieval Methods

        /// <summary>
        ///     Get cached implied volatility or solve and cache if not present.
        ///     This method provides efficient access to IV calculations with automatic caching.
        ///     Includes fallback to default volatility if calculation fails.
        /// </summary>
        /// <param name="S">Current underlying stock price</param>
        /// <param name="K">Strike price of the option</param>
        /// <param name="T">Time to expiration in years</param>
        /// <param name="r">Risk-free interest rate as decimal</param>
        /// <param name="q">Dividend yield as decimal</param>
        /// <param name="marketPrice">Market price of the option</param>
        /// <param name="isCall">True for call option, false for put option</param>
        /// <returns>Implied volatility (cached or newly calculated)</returns>
        double GetIV(double S, double K, double T, double r, double q, double marketPrice, bool isCall);

        /// <summary>
        ///     Get theoretical option price using cached implied volatility.
        ///     Looks up previously calculated IV for similar option parameters and applies it to pricing.
        ///     Falls back to default volatility if no cached IV is available.
        /// </summary>
        /// <param name="S">Current underlying stock price</param>
        /// <param name="K">Strike price of the option</param>
        /// <param name="T">Time to expiration in years</param>
        /// <param name="r">Risk-free interest rate as decimal</param>
        /// <param name="q">Dividend yield as decimal</param>
        /// <param name="isCall">True for call option, false for put option</param>
        /// <returns>Theoretical option price using cached IV, or price with fallback IV</returns>
        double GetOptionPrice(double S, double K, double T, double r, double q, bool isCall);

        /// <summary>
        ///     Get theoretical option price for specific date and parameters using cached IV.
        ///     This method is optimized for backtesting scenarios where you need prices for specific historical dates.
        /// </summary>
        /// <param name="date">Trading date for the calculation</param>
        /// <param name="closePrice">Underlying price at market close for the specified date</param>
        /// <param name="strike">Strike price of the option</param>
        /// <param name="daysToExpiration">Number of calendar days until expiration</param>
        /// <param name="riskFreeRate">Risk-free interest rate as decimal</param>
        /// <param name="dividendYield">Dividend yield as decimal</param>
        /// <param name="isCall">True for call option, false for put option</param>
        /// <returns>Theoretical option price for the specified date and parameters</returns>
        double GetOption(DateTime date, double closePrice, int strike, int daysToExpiration,
            double riskFreeRate, double dividendYield, bool isCall);

        /// <summary>
        ///     Get closing price for a specific trading date.
        ///     Utility method for retrieving underlying stock prices from loaded OHLC data.
        /// </summary>
        /// <param name="date">Trading date to look up</param>
        /// <returns>Closing price for the date, or 0.0 if date not found in loaded data</returns>
        double GetClosePrice(DateTime date);

        #endregion

        #region Advanced Analysis Methods

        /// <summary>
        ///     Generate a time series of implied volatilities for a given option contract using daily close prices.
        ///     Creates a historical view of how IV would have changed over time for a constant option specification.
        ///     Useful for volatility surface analysis and understanding market sentiment changes.
        /// </summary>
        /// <param name="strike">Strike price of the option to analyze</param>
        /// <param name="timeToExpiration">Constant time to expiration in years for the analysis</param>
        /// <param name="riskFreeRate">Risk-free interest rate as decimal</param>
        /// <param name="dividendYield">Dividend yield as decimal</param>
        /// <param name="marketPrice">Reference market price for IV calculation</param>
        /// <param name="isCall">True for call option, false for put option</param>
        /// <returns>Dictionary mapping trading dates to calculated implied volatilities</returns>
        Dictionary<DateTime, double> GetDailyIVSeries(double strike, double timeToExpiration,
            double riskFreeRate, double dividendYield, double marketPrice, bool isCall);

        /// <summary>
        ///     Generate implied volatility grid for strikes around current price and short-term expirations.
        ///     Creates IVs for strikes from (close-20) to (close+20) and expirations from 1 to 4 days.
        ///     This grid is useful for understanding the implied volatility surface and smile effects.
        /// </summary>
        /// <param name="riskFreeRate">Risk-free interest rate as decimal</param>
        /// <param name="dividendYield">Dividend yield as decimal</param>
        /// <param name="marketPrice">Reference market price for IV calculation</param>
        /// <param name="isCall">True for call options, false for put options</param>
        /// <returns>Nested dictionary: Date -> Strike -> DaysToExpiration -> ImpliedVolatility</returns>
        Dictionary<DateTime, Dictionary<int, Dictionary<int, double>>> GetIVGridForDatesAndStrikes(
            double riskFreeRate, double dividendYield, double marketPrice, bool isCall);

        /// <summary>
        ///     Simulate option price grid using rolling historical volatility.
        ///     Calculates historical volatility using log returns over a rolling window, then applies it to option pricing.
        ///     This method is useful for comparing theoretical prices based on historical vol vs. market prices.
        /// </summary>
        /// <param name="riskFreeRate">Risk-free interest rate as decimal</param>
        /// <param name="dividendYield">Dividend yield as decimal</param>
        /// <param name="isCall">True for call options, false for put options</param>
        /// <param name="windowSizeDays">Rolling window size in trading days for historical volatility calculation (default: 30)</param>
        /// <returns>Nested dictionary: Date -> Strike -> DaysToExpiration -> OptionPrice</returns>
        Dictionary<DateTime, Dictionary<int, Dictionary<int, double>>> SimulateOptionPriceGridWithHistoricalVolatility(
            double riskFreeRate, double dividendYield, bool isCall, int windowSizeDays = 30);

        #endregion
    }
}