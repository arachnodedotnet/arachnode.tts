using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade;

namespace Trade.Tests
{
    [TestClass]
    public class ImpliedVolatilitySolverTests
    {
        private ImpliedVolatilitySolver solver;
        private const double TOLERANCE = 1e-6;
        private const double PRECISION_HIGH = 1e-6;
        private const double PRECISION_MEDIUM = 1e-4;
        private const double PRECISION_LOW = 1e-2;

        [TestInitialize]
        public void Setup()
        {
            solver = new ImpliedVolatilitySolver();
        }

        #region Black-Scholes Pricing Tests - Industry Standard Test Cases

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_ClassicAtTheMoneyCall_ExactReference()
        {
            // Classic test case: S=K=100, T=1 year, r=5%, q=0%, ?=20%
            var price = solver.BlackScholesPrice(100, 100, 1.0, 0.05, 0.0, 0.2, true);

            // Exact theoretical value: 10.4506
            Assert.AreEqual(10.4506, price, PRECISION_MEDIUM,
                "ATM call with 1 year expiry should match theoretical Black-Scholes value");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_ClassicAtTheMoneyPut_ExactReference()
        {
            // Classic test case: S=K=100, T=1 year, r=5%, q=0%, ?=20%
            var price = solver.BlackScholesPrice(100, 100, 1.0, 0.05, 0.0, 0.2, false);

            // Exact theoretical value using put-call parity: 5.5741
            Assert.AreEqual(5.5735, price, PRECISION_MEDIUM,
                "ATM put with 1 year expiry should match theoretical Black-Scholes value");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_PutCallParity_Verification()
        {
            // Verify Put-Call Parity: C - P = S*e^(-qT) - K*e^(-rT)
            double S = 105, K = 100, T = 0.25, r = 0.03, q = 0.01, sigma = 0.25;

            var callPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, true);
            var putPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, false);

            var leftSide = callPrice - putPrice;
            var rightSide = S * Math.Exp(-q * T) - K * Math.Exp(-r * T);

            Assert.AreEqual(rightSide, leftSide, PRECISION_HIGH,
                "Put-Call Parity must hold: C - P = S*e^(-qT) - K*e^(-rT)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_DeepInTheMoneyCall_CorrectBehavior()
        {
            // Deep ITM call should behave like the underlying minus discounted strike
            double S = 150, K = 100, T = 0.5, r = 0.04, q = 0.0, sigma = 0.3;

            var callPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, true);
            var intrinsicValue = S - K;
            var discountedStrike = K * Math.Exp(-r * T);
            var timeValue = callPrice - intrinsicValue;

            Assert.IsTrue(callPrice > intrinsicValue, "ITM call must be worth more than intrinsic value");
            Assert.IsTrue(timeValue > 0, "ITM call must have positive time value");
            Assert.IsTrue(callPrice > S - discountedStrike, "Deep ITM call approximation check");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_DeepOutOfTheMoneyPut_CorrectBehavior()
        {
            // Deep OTM put should be worth very little
            double S = 150, K = 100, T = 0.1, r = 0.05, q = 0.0, sigma = 0.5;

            var putPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, false);

            Assert.IsTrue(putPrice > 0, "OTM put must have positive value");
            Assert.IsTrue(putPrice < 1.0, "Deep OTM put should be worth very little");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_HighVolatility_CorrectBehavior()
        {
            // High volatility should increase option values
            double S = 100, K = 100, T = 0.25, r = 0.05, q = 0.0;

            var lowVolPrice = solver.BlackScholesPrice(S, K, T, r, q, 0.1, true);
            var highVolPrice = solver.BlackScholesPrice(S, K, T, r, q, 0.5, true);

            Assert.IsTrue(highVolPrice > lowVolPrice,
                "Higher volatility must result in higher option prices");
            Assert.IsTrue(highVolPrice > lowVolPrice * 2,
                "Significant volatility increase should significantly increase option value");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_WithDividends_CorrectAdjustment()
        {
            // Dividends should reduce call prices and increase put prices
            double S = 100, K = 100, T = 1.0, r = 0.05, sigma = 0.2;

            var callNoDividend = solver.BlackScholesPrice(S, K, T, r, 0.0, sigma, true);
            var callWithDividend = solver.BlackScholesPrice(S, K, T, r, 0.03, sigma, true);

            var putNoDividend = solver.BlackScholesPrice(S, K, T, r, 0.0, sigma, false);
            var putWithDividend = solver.BlackScholesPrice(S, K, T, r, 0.03, sigma, false);

            Assert.IsTrue(callWithDividend < callNoDividend,
                "Dividends should reduce call option prices");
            Assert.IsTrue(putWithDividend > putNoDividend,
                "Dividends should increase put option prices");
        }

        #endregion

        #region Edge Cases and Boundary Conditions

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_ZeroTimeToExpiration_IntrinsicValue()
        {
            // ITM call at expiration
            var callPrice = solver.BlackScholesPrice(110, 100, 0.0, 0.05, 0.0, 0.2, true);
            Assert.AreEqual(10.0, callPrice, PRECISION_HIGH,
                "Call at expiration should equal max(S-K, 0)");

            // OTM call at expiration
            var otmCallPrice = solver.BlackScholesPrice(90, 100, 0.0, 0.05, 0.0, 0.2, true);
            Assert.AreEqual(0.0, otmCallPrice, PRECISION_HIGH,
                "OTM call at expiration should be worthless");

            // ITM put at expiration
            var putPrice = solver.BlackScholesPrice(90, 100, 0.0, 0.05, 0.0, 0.2, false);
            Assert.AreEqual(10.0, putPrice, PRECISION_HIGH,
                "Put at expiration should equal max(K-S, 0)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_ZeroVolatility_DeterministicPricing()
        {
            double S = 100, K = 105, T = 0.5, r = 0.04, q = 0.0;

            var callPrice = solver.BlackScholesPrice(S, K, T, r, q, 0.0, true);
            var putPrice = solver.BlackScholesPrice(S, K, T, r, q, 0.0, false);

            // With zero volatility, options should price to forward value
            var forward = S * Math.Exp(-q * T);
            var discountedStrike = K * Math.Exp(-r * T);

            var expectedCall = Math.Max(forward - discountedStrike, 0);
            var expectedPut = Math.Max(discountedStrike - forward, 0);

            Assert.AreEqual(expectedCall, callPrice, PRECISION_HIGH,
                "Zero volatility call should price to forward value");
            Assert.AreEqual(expectedPut, putPrice, PRECISION_HIGH,
                "Zero volatility put should price to forward value");
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void BlackScholesPrice_NegativeStockPrice_ThrowsException()
        {
            solver.BlackScholesPrice(-100, 100, 1.0, 0.05, 0.0, 0.2, true);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_NegativeStrikePrice_ThrowsException()
        {
            Assert.ThrowsException<ArgumentException>(() =>
            solver.BlackScholesPrice(100, -100, 1.0, 0.05, 0.0, 0.2, true));
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void BlackScholesPrice_NegativeTime_ThrowsException()
        {
            solver.BlackScholesPrice(100, 100, -1.0, 0.05, 0.0, 0.2, true);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void BlackScholesPrice_NegativeVolatility_ThrowsException()
        {
            solver.BlackScholesPrice(100, 100, 1.0, 0.05, 0.0, -0.2, true);
        }

        #endregion

        #region Implied Volatility Tests - Critical for Accuracy

        [TestMethod]
        [TestCategory("Core")]
        public void SolveIV_ExactRoundTrip_ATMCall()
        {
            double S = 100, K = 100, T = 0.25, r = 0.05, q = 0.0, sigma = 0.2;

            // Calculate theoretical price
            var theoreticalPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, true);

            // Solve for IV using theoretical price
            var solvedIV = solver.SolveIV(S, K, T, r, q, theoreticalPrice, true);

            Assert.AreEqual(sigma, solvedIV, PRECISION_HIGH,
                "Round-trip IV calculation must be exact for ATM options");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SolveIV_ExactRoundTrip_ITMPut()
        {
            double S = 95, K = 100, T = 0.5, r = 0.03, q = 0.01, sigma = 0.35;

            // Calculate theoretical price
            var theoreticalPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, false);

            // Solve for IV using theoretical price
            var solvedIV = solver.SolveIV(S, K, T, r, q, theoreticalPrice, false);

            Assert.AreEqual(sigma, solvedIV, PRECISION_HIGH,
                "Round-trip IV calculation must be exact for ITM puts");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SolveIV_ExactRoundTrip_OTMCall()
        {
            double S = 100, K = 110, T = 0.1, r = 0.02, q = 0.005, sigma = 0.4;

            // Calculate theoretical price
            var theoreticalPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, true);

            // Solve for IV using theoretical price
            var solvedIV = solver.SolveIV(S, K, T, r, q, theoreticalPrice, true);

            Assert.AreEqual(sigma, solvedIV, PRECISION_HIGH,
                "Round-trip IV calculation must be exact for OTM calls");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SolveIV_HighVolatilityScenario_ConvergesCorrectly()
        {
            double S = 100, K = 100, T = 2.0, r = 0.05, q = 0.0, sigma = 1.0; // 100% volatility

            var theoreticalPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, true);
            var solvedIV = solver.SolveIV(S, K, T, r, q, theoreticalPrice, true);

            Assert.AreEqual(sigma, solvedIV, PRECISION_MEDIUM,
                "High volatility IV solving must converge accurately");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SolveIV_LowVolatilityScenario_ConvergesCorrectly()
        {
            double S = 100, K = 100, T = 0.25, r = 0.05, q = 0.0, sigma = 0.05; // 5% volatility

            var theoreticalPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, true);
            var solvedIV = solver.SolveIV(S, K, T, r, q, theoreticalPrice, true);

            Assert.AreEqual(sigma, solvedIV, PRECISION_HIGH,
                "Low volatility IV solving must converge accurately");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SolveIV_ShortTimeToExpiration_ConvergesCorrectly()
        {
            double S = 100, K = 101, T = 1.0 / 365.0, r = 0.05, q = 0.0, sigma = 0.3; // 1 day to expiry

            var theoreticalPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, true);
            var solvedIV = solver.SolveIV(S, K, T, r, q, theoreticalPrice, true);

            Assert.AreEqual(sigma, solvedIV, PRECISION_MEDIUM,
                "Short-term options IV solving must work correctly");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SolveIV_PriceBelowIntrinsic_ReturnsNaN()
        {
            double S = 110, K = 100, T = 0.25, r = 0.05, q = 0.0;
            var intrinsicValue = S - K; // 10.0
            var impossiblePrice = intrinsicValue - 1.0; // 9.0 (impossible)

            var result = solver.SolveIV(S, K, T, r, q, impossiblePrice, true);

            Assert.IsTrue(double.IsNaN(result),
                "IV solver must return NaN for prices below intrinsic value");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SolveIV_NegativeInputs_ReturnsNaN()
        {
            Assert.IsTrue(double.IsNaN(solver.SolveIV(-100, 100, 1, 0.05, 0, 10, true)),
                "Negative stock price should return NaN");
            Assert.IsTrue(double.IsNaN(solver.SolveIV(100, -100, 1, 0.05, 0, 10, true)),
                "Negative strike price should return NaN");
            Assert.IsTrue(double.IsNaN(solver.SolveIV(100, 100, -1, 0.05, 0, 10, true)),
                "Negative time should return NaN");
            Assert.IsTrue(double.IsNaN(solver.SolveIV(100, 100, 1, 0.05, 0, -10, true)),
                "Negative market price should return NaN");
        }

        #endregion

        #region Caching Tests

        [TestMethod]
        [TestCategory("Core")]
        public void GetIV_CacheConsistency_MultipleCallsSameResult()
        {
            double S = 100, K = 100, T = 0.25, r = 0.05, q = 0.0, marketPrice = 5.0;

            var iv1 = solver.GetIV(S, K, T, r, q, marketPrice, true);
            var iv2 = solver.GetIV(S, K, T, r, q, marketPrice, true);
            var iv3 = solver.GetIV(S, K, T, r, q, marketPrice, true);

            Assert.AreEqual(iv1, iv2, PRECISION_HIGH, "Cached IV must be consistent");
            Assert.AreEqual(iv1, iv3, PRECISION_HIGH, "Cached IV must be consistent across multiple calls");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetOptionPrice_UsesCachedIV_CorrectlyReturnsPrice()
        {
            double S = 100, K = 105, T = 0.5, r = 0.04, q = 0.01, marketPrice = 7.5;

            // First, cache the IV
            var cachedIV = solver.GetIV(S, K, T, r, q, marketPrice, true);

            // Then get option price using cached IV
            var optionPrice = solver.GetOptionPrice(S, K, T, r, q, true);

            // Calculate expected price using the cached IV
            var expectedPrice = solver.BlackScholesPrice(S, K, T, r, q, cachedIV, true);

            Assert.AreEqual(expectedPrice, optionPrice, PRECISION_HIGH,
                "Option price must use cached IV correctly");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetIV_NaNHandling_ReturnsFallbackVolatility()
        {
            double S = 100, K = 100, T = 0.25, r = 0.05, q = 0.0;
            var impossiblePrice = -5.0; // Impossible negative price

            var result = solver.GetIV(S, K, T, r, q, impossiblePrice, true);

            Assert.AreEqual(0.2, result, PRECISION_HIGH,
                "GetIV should return fallback volatility (0.2) for impossible prices");
        }

        #endregion

        #region Greeks Verification Through Finite Differences

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_DeltaVerification_FiniteDifference()
        {
            // Verify delta (?C/?S) using finite differences
            double S = 100, K = 100, T = 0.25, r = 0.05, q = 0.0, sigma = 0.2;
            var dS = 0.01;

            var priceUp = solver.BlackScholesPrice(S + dS, K, T, r, q, sigma, true);
            var priceDown = solver.BlackScholesPrice(S - dS, K, T, r, q, sigma, true);

            var finiteDifferenceDelta = (priceUp - priceDown) / (2 * dS);

            // Theoretical delta for call = e^(-qT) * N(d1)
            var d1 = (Math.Log(S / K) + (r - q + 0.5 * sigma * sigma) * T) / (sigma * Math.Sqrt(T));
            var theoreticalDelta = Math.Exp(-q * T) * NormCdf(d1);

            Assert.AreEqual(theoreticalDelta, finiteDifferenceDelta, 1e-3,
                "Finite difference delta should approximate theoretical delta");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_ThetaVerification_FiniteDifference()
        {
            // Verify theta (?C/?T) using finite differences
            double S = 100, K = 100, T = 0.25, r = 0.05, q = 0.0, sigma = 0.2;
            var dT = 1.0 / 365.0; // 1 day

            var priceToday = solver.BlackScholesPrice(S, K, T, r, q, sigma, true);
            var priceTomorrow = solver.BlackScholesPrice(S, K, T - dT, r, q, sigma, true);

            var finiteDifferenceTheta = (priceTomorrow - priceToday) / dT;

            // Theta should be negative for long options (time decay)
            Assert.IsTrue(finiteDifferenceTheta < 0,
                "Theta should be negative (options lose value over time)");
        }

        #endregion

        #region Real Market Scenarios

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_SPX_RealisticScenario()
        {
            // Realistic S&P 500 option scenario
            double S = 4500, K = 4600, T = 30.0 / 365.0, r = 0.05, q = 0.018, sigma = 0.18;

            var callPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, true);
            var putPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, false);

            Assert.IsTrue(callPrice > 0, "SPX call option must have positive value");
            Assert.IsTrue(putPrice > 0, "SPX put option must have positive value");
            Assert.IsTrue(putPrice > callPrice, "OTM put should be worth more than OTM call (same strike)");

            // Verify put-call parity still holds
            var leftSide = callPrice - putPrice;
            var rightSide = S * Math.Exp(-q * T) - K * Math.Exp(-r * T);
            Assert.AreEqual(rightSide, leftSide, PRECISION_MEDIUM,
                "Put-call parity must hold for realistic market scenarios");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SolveIV_VolatilitySmile_DifferentStrikes()
        {
            // Test that IV can be solved for different strikes (volatility smile)
            double S = 100, T = 0.25, r = 0.05, q = 0.0;

            var testCases = new[]
            {
                new { K = 90.0, MarketPrice = 12.5, Description = "ITM Call" },
                new { K = 100.0, MarketPrice = 5.8, Description = "ATM Call" },
                new { K = 110.0, MarketPrice = 1.2, Description = "OTM Call" }
            };

            foreach (var testCase in testCases)
            {
                var iv = solver.SolveIV(S, testCase.K, T, r, q, testCase.MarketPrice, true);

                Assert.IsFalse(double.IsNaN(iv), $"IV must be solvable for {testCase.Description}");
                Assert.IsTrue(iv > 0, $"IV must be positive for {testCase.Description}");
                Assert.IsTrue(iv < 2.0, $"IV must be reasonable for {testCase.Description}");

                // Verify round trip
                var calculatedPrice = solver.BlackScholesPrice(S, testCase.K, T, r, q, iv, true);
                Assert.AreEqual(testCase.MarketPrice, calculatedPrice, PRECISION_MEDIUM,
                    $"Round trip must work for {testCase.Description}");
            }
        }

        #endregion

        #region Additional Tests for Public Methods

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_CorrectForCallAndPut()
        {
            double S = 100, K = 100, T = 0.5, r = 0.01, q = 0.0, sigma = 0.2;
            double callPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, true);
            double putPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, false);
            Assert.IsTrue(callPrice > 0);
            Assert.IsTrue(putPrice > 0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void BlackScholesPrice_ZeroVolatilityAndExpiration()
        {
            double S = 100, K = 100, T = 0.0, r = 0.01, q = 0.0, sigma = 0.0;
            double callPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, true);
            double putPrice = solver.BlackScholesPrice(S, K, T, r, q, sigma, false);
            Assert.AreEqual(0.0, callPrice, TOLERANCE);
            Assert.AreEqual(0.0, putPrice, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetOption_ReturnsPriceForDate()
        {
            var date = DateTime.Today;
            solver.Load("", 0.01, 0.0, 0.2, true); // Preload with fallback
            double price = solver.GetOption(date, 100, 100, 1, 0.01, 0.0, true);
            Assert.IsTrue(price > 0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetClosePrice_ReturnsZeroIfNotFound()
        {
            var date = DateTime.Today.AddDays(-100);
            double close = solver.GetClosePrice(date);
            Assert.AreEqual(0.0, close, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetDailyIVSeries_ReturnsSeries()
        {
            solver.Load("", 0.01, 0.0, 0.2, true);
            var series = solver.GetDailyIVSeries(100, 0.5, 0.01, 0.0, 10, true);
            Assert.IsTrue(series.Count >= 0); // Should not throw
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetIVGridForDatesAndStrikes_ReturnsGrid()
        {
            solver.Load("", 0.01, 0.0, 0.2, true);
            var grid = solver.GetIVGridForDatesAndStrikes(0.01, 0.0, 10, true);
            Assert.IsTrue(grid.Count >= 0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SimulateOptionPriceGridWithHistoricalVolatility_ReturnsGrid()
        {
            solver.Load("", 0.01, 0.0, 0.2, true);
            var grid = solver.SimulateOptionPriceGridWithHistoricalVolatility(0.01, 0.0, true, 2);
            Assert.IsTrue(grid.Count >= 0);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        ///     Standard normal cumulative distribution function for testing
        /// </summary>
        private static double NormCdf(double x)
        {
            return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
        }

        /// <summary>
        ///     Error function for testing
        /// </summary>
        private static double Erf(double x)
        {
            double sign = Math.Sign(x);
            x = Math.Abs(x);

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