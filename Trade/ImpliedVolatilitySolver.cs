using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Trade.Interfaces;

namespace Trade
{
    /// <summary>
    ///     ImpliedVolatilitySolver caches IV calculations and provides option pricing using the Black-Scholes model.
    ///     This class handles both forward pricing (given volatility) and inverse pricing (solving for implied volatility).
    /// </summary>
    public class ImpliedVolatilitySolver : IImpliedVolatilitySolver
    {
        #region Deprecated Methods (kept for backward compatibility)

        /// <summary>
        ///     Helper method for standard deviation calculation.
        ///     [DEPRECATED] Use CalculateStandardDeviation instead.
        /// </summary>
        [Obsolete("Use CalculateStandardDeviation method instead")]
        private double StdDev(double[] values)
        {
            return CalculateStandardDeviation(values);
        }

        #endregion

        #region Constants

        // Constants for mathematical calculations
        private const double DEFAULT_VOLATILITY_FALLBACK = 0.2; // 20% annual volatility fallback
        private const int TRADING_DAYS_PER_YEAR = 252; // Standard trading days for volatility annualization
        private const double DAYS_PER_YEAR = 365.0; // Calendar days for time calculations
        private const double MIN_VOLATILITY = 0.001; // Minimum volatility bound (0.1%)
        private const double MAX_VOLATILITY = 5.0; // Maximum volatility bound (500%)
        private const double DEFAULT_TOLERANCE = 1e-6; // Improved precision for IV solving
        private const int DEFAULT_MAX_ITERATIONS = 100; // Maximum iterations for bisection method

        #endregion

        #region Private Fields

        // Cache IV for (underlying, strike, expiration, type) combinations
        private readonly Dictionary<string, double> ivCache = new Dictionary<string, double>();

        // Map trading dates to closing prices
        private readonly Dictionary<DateTime, double> closePriceByDate = new Dictionary<DateTime, double>();

        #endregion

        #region Data Loading Methods

        /// <summary>
        ///     Load OHLC prices from CSV file and map dates to closing prices.
        /// </summary>
        /// <param name="ohlcCsvPath">Path to CSV file with format: Date,Open,High,Low,Close,Volume</param>
        public void LoadClosePrices(string ohlcCsvPath)
        {
            if (!File.Exists(ohlcCsvPath)) return;

            var lines = File.ReadAllLines(ohlcCsvPath);
            foreach (var line in lines.Skip(1)) // skip header
            {
                var parts = line.Split(',');
                if (parts.Length < 5) continue;

                if (DateTime.TryParse(parts[0], out var date) &&
                    double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var close) &&
                    close > 0) // Ensure positive price
                    closePriceByDate[date.Date] = close;
            }
        }

        /// <summary>
        ///     Pre-load implied volatilities from options market data CSV file.
        /// </summary>
        /// <param name="optionsCsvPath">
        ///     Path to CSV with format:
        ///     ValuationDate,Strike,TimeToExpiration,RiskFreeRate,DividendYield,MarketPrice,IsCall
        /// </param>
        public void LoadOptionsAndSolveIVs(string optionsCsvPath)
        {
            if (!File.Exists(optionsCsvPath)) return;

            var lines = File.ReadAllLines(optionsCsvPath);
            foreach (var line in lines.Skip(1)) // skip header
            {
                var parts = line.Split(',');
                if (parts.Length < 7) continue;

                try
                {
                    if (!DateTime.TryParse(parts[0], out var date)) continue;

                    var K = double.Parse(parts[1], CultureInfo.InvariantCulture);
                    var T = double.Parse(parts[2], CultureInfo.InvariantCulture);
                    var r = double.Parse(parts[3], CultureInfo.InvariantCulture);
                    var q = double.Parse(parts[4], CultureInfo.InvariantCulture);
                    var marketPrice = double.Parse(parts[5], CultureInfo.InvariantCulture);
                    var isCall = bool.Parse(parts[6]);

                    // Skip if no corresponding close price for this date
                    if (!closePriceByDate.TryGetValue(date.Date, out var S)) continue;

                    var iv = SolveIV(S, K, T, r, q, marketPrice, isCall);

                    if (!double.IsNaN(iv))
                    {
                        var key = CreateCacheKey(S, K, T, r, q, marketPrice, isCall);
                        ivCache[key] = iv;
                    }
                }
                catch (Exception)
                {
                    // Skip malformed lines
                }
            }
        }

        /// <summary>
        ///     Load close prices and option market data, then solve for implied volatilities.
        /// </summary>
        /// <param name="ohlcCsvPath">Path to OHLC data</param>
        /// <param name="optionsMarketDataCsvPath">Path to options market data</param>
        public void LoadWithMarketData(string ohlcCsvPath, string optionsMarketDataCsvPath)
        {
            LoadClosePrices(ohlcCsvPath);
            LoadOptionsAndSolveIVs(optionsMarketDataCsvPath);
        }

        /// <summary>
        ///     Load close prices and precompute theoretical option prices using a fixed implied volatility.
        /// </summary>
        /// <param name="ohlcCsvPath">Path to OHLC CSV file</param>
        /// <param name="riskFreeRate">Risk-free rate</param>
        /// <param name="dividendYield">Dividend yield</param>
        /// <param name="fixedIV">Fixed implied volatility to use for all calculations</param>
        /// <param name="isCall">True for call, false for put</param>
        public void Load(string ohlcCsvPath, double riskFreeRate, double dividendYield, double fixedIV, bool isCall)
        {
            LoadClosePrices(ohlcCsvPath);

            // Precompute theoretical option prices using fixed IV
            foreach (var kvp in closePriceByDate)
            {
                var date = kvp.Key;
                var closeInt = (int)Math.Round(kvp.Value);

                for (var strike = closeInt - 20; strike <= closeInt + 20; strike++)
                {
                    if (strike <= 0) continue;

                    for (var days = 1; days <= 4; days++)
                    {
                        var T = days / DAYS_PER_YEAR;
                        var key = CreateDateBasedCacheKey(date, strike, days, riskFreeRate, dividendYield, isCall);
                        ivCache[key] = fixedIV;
                    }
                }
            }
        }

        #endregion

        #region Core Black-Scholes Methods

        /// <summary>
        ///     Calculate Black-Scholes option price using the standard formula.
        /// </summary>
        /// <param name="S">Current stock price (must be positive)</param>
        /// <param name="K">Strike price (must be positive)</param>
        /// <param name="T">Time to expiration in years (must be non-negative)</param>
        /// <param name="r">Risk-free rate as decimal (e.g., 0.05 for 5%)</param>
        /// <param name="q">Dividend yield as decimal (e.g., 0.02 for 2%)</param>
        /// <param name="sigma">Volatility as decimal (e.g., 0.2 for 20%, must be positive)</param>
        /// <param name="isCall">True for call option, false for put option</param>
        /// <returns>Theoretical option price</returns>
        /// <exception cref="ArgumentException">Thrown when input parameters are invalid</exception>
        public double BlackScholesPrice(double S, double K, double T, double r, double q, double sigma, bool isCall)
        {
            // Input validation
            if (S <= 0) throw new ArgumentException("Stock price must be positive", nameof(S));
            if (K <= 0) throw new ArgumentException("Strike price must be positive", nameof(K));
            if (T < 0) throw new ArgumentException("Time to expiration cannot be negative", nameof(T));
            if (sigma < 0) throw new ArgumentException("Volatility cannot be negative", nameof(sigma));

            // Handle special cases
            if (T == 0)
                // At expiration, option worth is intrinsic value
                return Math.Max(isCall ? S - K : K - S, 0);

            if (sigma == 0)
            {
                // Zero volatility case - deterministic pricing
                var discountedForward = S * Math.Exp(-q * T);
                var discountedStrike = K * Math.Exp(-r * T);
                return Math.Max(isCall ? discountedForward - discountedStrike : discountedStrike - discountedForward,
                    0);
            }

            // Calculate d1 and d2 parameters
            var d1 = (Math.Log(S / K) + (r - q + 0.5 * sigma * sigma) * T) / (sigma * Math.Sqrt(T));
            var d2 = d1 - sigma * Math.Sqrt(T);

            if (isCall)
                // Call option: C = S*e^(-qT)*N(d1) - K*e^(-rT)*N(d2)
                return S * Math.Exp(-q * T) * NormCdf(d1) - K * Math.Exp(-r * T) * NormCdf(d2);
            // Put option: P = K*e^(-rT)*N(-d2) - S*e^(-qT)*N(-d1)
            return K * Math.Exp(-r * T) * NormCdf(-d2) - S * Math.Exp(-q * T) * NormCdf(-d1);
        }

        /// <summary>
        ///     Solve for implied volatility using improved bisection method with dynamic bounds.
        /// </summary>
        /// <param name="S">Current stock price</param>
        /// <param name="K">Strike price</param>
        /// <param name="T">Time to expiration in years</param>
        /// <param name="r">Risk-free rate</param>
        /// <param name="q">Dividend yield</param>
        /// <param name="marketPrice">Observed market price of the option</param>
        /// <param name="isCall">True for call, false for put</param>
        /// <param name="tol">Convergence tolerance (default: 1e-6)</param>
        /// <param name="maxIter">Maximum iterations (default: 100)</param>
        /// <returns>Implied volatility, or NaN if no solution exists</returns>
        public double SolveIV(double S, double K, double T, double r, double q, double marketPrice, bool isCall,
            double tol = DEFAULT_TOLERANCE, int maxIter = DEFAULT_MAX_ITERATIONS)
        {
            // Input validation
            if (S <= 0 || K <= 0 || T <= 0 || marketPrice <= 0)
                return double.NaN;

            // Check if market price is within reasonable bounds
            var intrinsicValue = Math.Max(isCall ? S - K : K - S, 0);
            if (marketPrice < intrinsicValue)
                return double.NaN; // Market price below intrinsic value is impossible

            // Dynamic bounds based on market conditions
            var lower = MIN_VOLATILITY;
            var upper = Math.Max(MAX_VOLATILITY, Math.Abs(Math.Log(S / K)) / Math.Sqrt(T) * 2);

            // Check if solution exists within bounds
            var priceLower = BlackScholesPrice(S, K, T, r, q, lower, isCall);
            var priceUpper = BlackScholesPrice(S, K, T, r, q, upper, isCall);

            // Ensure market price is bracketed
            if ((marketPrice < priceLower && marketPrice < priceUpper) ||
                (marketPrice > priceLower && marketPrice > priceUpper))
                return double.NaN; // No solution exists

            // Bisection method with improved convergence
            var iv = (lower + upper) / 2.0;

            for (var i = 0; i < maxIter; i++)
            {
                var price = BlackScholesPrice(S, K, T, r, q, iv, isCall);
                var priceDiff = price - marketPrice;

                if (Math.Abs(priceDiff) < tol)
                    return iv;

                if (priceDiff > 0)
                    upper = iv;
                else
                    lower = iv;

                iv = (lower + upper) / 2.0;

                // Check for convergence on volatility
                if (upper - lower < tol)
                    return iv;
            }

            return iv; // Return best approximation if max iterations reached
        }

        #endregion

        #region Caching and Retrieval Methods

        /// <summary>
        ///     Get cached implied volatility or solve and cache if not present.
        /// </summary>
        /// <param name="S">Current stock price</param>
        /// <param="K">Strike price</param>
        /// <param="T">Time to expiration in years</param>
        /// <param name="r">Risk-free rate</param>
        /// <param name="q">Dividend yield</param>
        /// <param name="marketPrice">Market price of option</param>
        /// <param name="isCall">True for call, false for put</param>
        /// <returns>Implied volatility</returns>
        public double GetIV(double S, double K, double T, double r, double q, double marketPrice, bool isCall)
        {
            var key = CreateCacheKey(S, K, T, r, q, marketPrice, isCall);

            if (ivCache.TryGetValue(key, out var cachedIv))
                return cachedIv;

            var iv = SolveIV(S, K, T, r, q, marketPrice, isCall);

            if (!double.IsNaN(iv))
                ivCache[key] = iv;

            return double.IsNaN(iv) ? DEFAULT_VOLATILITY_FALLBACK : iv;
        }

        /// <summary>
        ///     Get theoretical option price using cached implied volatility.
        /// </summary>
        /// <param name="S">Current stock price</param>
        /// <param="K">Strike price</param>
        /// <param name="T">Time to expiration in years</param>
        /// <param="r">Risk-free rate</param>
        /// <param="q">Dividend yield</param>
        /// <param name="isCall">True for call, false for put</param>
        /// <returns>Theoretical option price using cached IV, or price with fallback IV</returns>
        public double GetOptionPrice(double S, double K, double T, double r, double q, bool isCall)
        {
            var keyPrefix = CreateCacheKeyPrefix(S, K, T, r, q);

            // Find matching cached IV for this option configuration
            var match = ivCache.FirstOrDefault(kvp =>
                kvp.Key.StartsWith(keyPrefix) &&
                kvp.Key.EndsWith($"|{isCall}"));

            var iv = match.Key != null && match.Value > 0 ? match.Value : DEFAULT_VOLATILITY_FALLBACK;

            return BlackScholesPrice(S, K, T, r, q, iv, isCall);
        }

        /// <summary>
        ///     Get theoretical option price for specific date and parameters using cached IV.
        /// </summary>
        /// <param name="date">Trading date</param>
        /// <param name="closePrice">Underlying price at close</param>
        /// <param name="strike">Strike price</param>
        /// <param name="daysToExpiration">Days until expiration</param>
        /// <param name="riskFreeRate">Risk-free rate</param>
        /// <param name="dividendYield">Dividend yield</param>
        /// <param name="isCall">True for call, false for put</param>
        /// <returns>Theoretical option price</returns>
        public double GetOption(DateTime date, double closePrice, int strike, int daysToExpiration,
            double riskFreeRate, double dividendYield, bool isCall)
        {
            var T = daysToExpiration / DAYS_PER_YEAR;
            var key = CreateDateBasedCacheKey(date, strike, daysToExpiration, riskFreeRate, dividendYield, isCall);

            var iv = ivCache.TryGetValue(key, out var cachedIv) ? cachedIv : DEFAULT_VOLATILITY_FALLBACK;

            return BlackScholesPrice(closePrice, strike, T, riskFreeRate, dividendYield, iv, isCall);
        }

        /// <summary>
        ///     Get closing price for a specific date.
        /// </summary>
        /// <param name="date">Trading date</param>
        /// <returns>Closing price, or 0.0 if not found</returns>
        public double GetClosePrice(DateTime date)
        {
            return closePriceByDate.TryGetValue(date.Date, out var close) ? close : 0.0;
        }

        #endregion

        #region Grid Generation Methods

        /// <summary>
        ///     Generate a time series of implied volatilities for a given option contract using daily close prices.
        /// </summary>
        /// <param name="strike">Strike price of the option</param>
        /// <param name="timeToExpiration">Time to expiration in years</param>
        /// <param name="riskFreeRate">Risk-free rate</param>
        /// <param name="dividendYield">Dividend yield</param>
        /// <param name="marketPrice">Market price of the option</param>
        /// <param name="isCall">True for call, false for put</param>
        /// <returns>Dictionary mapping dates to implied volatilities</returns>
        public Dictionary<DateTime, double> GetDailyIVSeries(double strike, double timeToExpiration,
            double riskFreeRate, double dividendYield, double marketPrice, bool isCall)
        {
            var ivSeries = new Dictionary<DateTime, double>();

            foreach (var kvp in closePriceByDate)
            {
                var S = kvp.Value;
                var iv = SolveIV(S, strike, timeToExpiration, riskFreeRate, dividendYield, marketPrice, isCall);

                if (!double.IsNaN(iv))
                    ivSeries[kvp.Key] = iv;
            }

            return ivSeries;
        }

        /// <summary>
        ///     Generate implied volatility grid for strikes around current price and short-term expirations.
        ///     Creates IVs for strikes from (close-20) to (close+20) and expirations from 1 to 4 days.
        /// </summary>
        /// <param name="riskFreeRate">Risk-free rate</param>
        /// <param name="dividendYield">Dividend yield</param>
        /// <param name="marketPrice">Reference market price for IV calculation</param>
        /// <param name="isCall">True for call, false for put</param>
        /// <returns>Nested dictionary: Date -> Strike -> DaysToExpiration -> ImpliedVolatility</returns>
        public Dictionary<DateTime, Dictionary<int, Dictionary<int, double>>> GetIVGridForDatesAndStrikes(
            double riskFreeRate, double dividendYield, double marketPrice, bool isCall)
        {
            var result = new Dictionary<DateTime, Dictionary<int, Dictionary<int, double>>>();

            foreach (var kvp in closePriceByDate)
            {
                var date = kvp.Key;
                var closeInt = (int)Math.Round(kvp.Value);
                var strikeDict = new Dictionary<int, Dictionary<int, double>>();

                // Generate grid for strikes around current price
                for (var strike = closeInt - 20; strike <= closeInt + 20; strike++)
                {
                    if (strike <= 0) continue; // Skip non-positive strikes

                    var expiryDict = new Dictionary<int, double>();

                    // Generate grid for short-term expirations
                    for (var days = 1; days <= 4; days++)
                    {
                        var T = days / DAYS_PER_YEAR;
                        var S = kvp.Value;

                        var iv = SolveIV(S, strike, T, riskFreeRate, dividendYield, marketPrice, isCall);

                        if (!double.IsNaN(iv))
                            expiryDict[days] = iv;
                    }

                    if (expiryDict.Count > 0)
                        strikeDict[strike] = expiryDict;
                }

                if (strikeDict.Count > 0)
                    result[date] = strikeDict;
            }

            return result;
        }

        /// <summary>
        ///     Simulate option price grid using rolling historical volatility.
        ///     Calculates historical volatility using log returns over a rolling window.
        /// </summary>
        /// <param name="riskFreeRate">Risk-free rate</param>
        /// <param name="dividendYield">Dividend yield</param>
        /// <param name="isCall">True for call, false for put</param>
        /// <param name="window">Rolling window size in days (default: 30)</param>
        /// <returns>Nested dictionary: Date -> Strike -> DaysToExpiration -> OptionPrice</returns>
        public Dictionary<DateTime, Dictionary<int, Dictionary<int, double>>>
            SimulateOptionPriceGridWithHistoricalVolatility(
                double riskFreeRate, double dividendYield, bool isCall, int window = 30)
        {
            // Calculate rolling historical volatility
            var dates = closePriceByDate.Keys.OrderBy(d => d).ToList();
            var prices = dates.Select(d => closePriceByDate[d]).ToList();
            var volByDate = new Dictionary<DateTime, double>();

            for (var i = window; i < prices.Count; i++)
            {
                var windowPrices = prices.Skip(i - window).Take(window).ToArray();
                var logReturns = new double[window - 1];

                // Calculate log returns
                for (var j = 1; j < window; j++)
                    if (windowPrices[j - 1] > 0 && windowPrices[j] > 0)
                        logReturns[j - 1] = Math.Log(windowPrices[j] / windowPrices[j - 1]);

                var stdDev = CalculateStandardDeviation(logReturns);
                var annualizedVol = stdDev * Math.Sqrt(TRADING_DAYS_PER_YEAR);
                volByDate[dates[i]] = annualizedVol;
            }

            // Generate option price grid using historical volatility
            var result = new Dictionary<DateTime, Dictionary<int, Dictionary<int, double>>>();

            foreach (var date in volByDate.Keys)
            {
                var S = closePriceByDate[date];
                var sigma = volByDate[date];
                var closeInt = (int)Math.Round(S);
                var strikeDict = new Dictionary<int, Dictionary<int, double>>();

                for (var strike = closeInt - 20; strike <= closeInt + 20; strike++)
                {
                    if (strike <= 0) continue;

                    var expiryDict = new Dictionary<int, double>();

                    for (var days = 1; days <= 4; days++)
                    {
                        var T = days / DAYS_PER_YEAR;
                        var price = BlackScholesPrice(S, strike, T, riskFreeRate, dividendYield, sigma, isCall);
                        expiryDict[days] = price;
                    }

                    strikeDict[strike] = expiryDict;
                }

                result[date] = strikeDict;
            }

            return result;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        ///     Create cache key for IV storage using parameter combination.
        /// </summary>
        private string CreateCacheKey(double S, double K, double T, double r, double q, double marketPrice, bool isCall)
        {
            return $"{S:F2}|{K:F2}|{T:F4}|{r:F4}|{q:F4}|{marketPrice:F2}|{isCall}";
        }

        /// <summary>
        ///     Create cache key prefix for option price lookup.
        /// </summary>
        private string CreateCacheKeyPrefix(double S, double K, double T, double r, double q)
        {
            return $"{S:F2}|{K:F2}|{T:F4}|{r:F4}|{q:F4}";
        }

        /// <summary>
        ///     Create date-based cache key for precomputed option prices.
        /// </summary>
        private string CreateDateBasedCacheKey(DateTime date, int strike, int days, double r, double q, bool isCall)
        {
            return $"{date:yyyy-MM-dd}|{strike}|{days}|{r:F4}|{q:F4}|{isCall}";
        }

        /// <summary>
        ///     Calculate sample standard deviation of an array of values.
        ///     Uses Bessel's correction (n-1 denominator) for unbiased estimate.
        /// </summary>
        /// <param name="values">Array of values</param>
        /// <returns>Sample standard deviation</returns>
        private double CalculateStandardDeviation(double[] values)
        {
            if (values.Length <= 1) return 0.0;

            var mean = values.Average();
            var sumSquaredDeviations = values.Select(v => (v - mean) * (v - mean)).Sum();
            return Math.Sqrt(sumSquaredDeviations / (values.Length - 1)); // Bessel's correction
        }

        /// <summary>
        ///     Standard normal cumulative distribution function.
        ///     Uses the complementary error function for improved accuracy.
        /// </summary>
        /// <param name="x">Input value</param>
        /// <returns>
        ///     Cumulative probability P(Z <= x) where Z ~ N(0,1)</returns>
        private double NormCdf(double x)
        {
            return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
        }

        /// <summary>
        ///     Error function approximation using Abramowitz and Stegun formula 7.1.26.
        ///     Provides accuracy to about 1.5 × 10^-7 for all real x.
        /// </summary>
        /// <param name="x">Input value</param>
        /// <returns>erf(x) = (2/?) ?[0,x] e^(-t²) dt</returns>
        private double Erf(double x)
        {
            // Abramowitz and Stegun formula 7.1.26
            double sign = Math.Sign(x);
            x = Math.Abs(x);

            // Constants for the approximation
            const double a1 = 0.254829592;
            const double a2 = -0.284496736;
            const double a3 = 1.421413741;
            const double a4 = -1.453152027;
            const double a5 = 1.061405429;
            const double p = 0.3275911;

            var t = 1.0 / (1.0 + p * x);
            var y = 1.0 - ((((a5 * t + a4) * t + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

            return sign * y;
        }

        #endregion
    }
}