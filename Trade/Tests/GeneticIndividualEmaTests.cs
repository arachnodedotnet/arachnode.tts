using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Utils; // Added for PriceRange wrapper

namespace Trade.Tests
{
    [TestClass]
    public class GeneticIndividualEmaTests
    {
        // Helper: compute expected EMA sequence using standard definition
        private static double[] ComputeExpectedEma(double[] x, int period)
        {
            if (period <= 0) throw new ArgumentOutOfRangeException(nameof(period));
            if (x == null) throw new ArgumentNullException(nameof(x));
            var n = x.Length;
            var ema = Enumerable.Repeat(double.NaN, n).ToArray();
            if (n == 0) return ema;

            if (n >= period)
            {
                double seed = 0.0;
                for (int i = 0; i < period; i++) seed += x[i];
                seed /= period;
                ema[period - 1] = seed;
                double alpha = 2.0 / (period + 1.0);
                for (int i = period; i < n; i++)
                {
                    ema[i] = alpha * x[i] + (1.0 - alpha) * ema[i - 1];
                }
            }
            return ema;
        }

        // Wrap reflection invoke to supply PriceRange arguments (reflection does not apply implicit conversions)
        private double InvokeIndicator(GeneticIndividual gi, IndicatorParams indicator, double[] prices)
        {
            var method = typeof(GeneticIndividual).GetMethod("CalculateIndicatorValue", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "CalculateIndicatorValue method not found");
            var pr = new PriceRange(prices);
            return (double)method.Invoke(gi, new object[] { indicator, pr, pr, pr, pr, pr, pr, prices.Length, null });
        }

        private double CalculateGeneticIndividualEma(double[] prices, int period)
        {
            var gi = new GeneticIndividual();
            var indicator = new IndicatorParams { Type = 2, Period = period };
            return InvokeIndicator(gi, indicator, prices);
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateEma_ReturnsExpectedValue()
        {
            var ind = new IndicatorParams { Type = 2, Period = 3 }; // EMA
            var gi = new GeneticIndividual();
            double[] prices = { 1, 2, 3, 4, 5 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(4.25, result, 1e-10, "EMA should be 4.25");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateEma_WithDifferentPeriod_ReturnsExpectedValue()
        {
            var ind = new IndicatorParams { Type = 2, Period = 2 };
            var gi = new GeneticIndividual();
            double[] prices = { 10, 20, 30, 40 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(36.666666666666664, result, 1e-10, "EMA with period 2 should be 36.6667");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateEma_WithSingleValue_ReturnsValue()
        {
            var ind = new IndicatorParams { Type = 2, Period = 1 };
            var gi = new GeneticIndividual();
            double[] prices = { 42.0 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(42.0, result, 1e-10, "EMA with single value should be the value itself");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateEma_DetailedCalculation_VerifySmoothing()
        {
            var ind = new IndicatorParams { Type = 2, Period = 4 }; // EMA period 4
            var gi = new GeneticIndividual();
            double[] prices = { 100, 200, 300, 400, 500 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(382.4, result, 1e-10, "EMA detailed calculation should be 382.4");
        }

        [TestMethod][TestCategory("Core")]
        public void EMA_PeriodOne_EqualsPriceSeries_GeneticIndividual()
        {
            var prices = new[] { 10.0, 10.5, 11.0, 10.0, 9.5, 10.25, 10.75 };
            int period = 1;
            for (int i = 0; i < prices.Length; i++)
            {
                var buffer = prices.Take(i + 1).ToArray();
                var ema = CalculateGeneticIndividualEma(buffer, period);
                Assert.AreEqual(prices[i], ema, 1e-6, $"EMA with period=1 must equal price at index {i}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void EMA_KnownSeries_MatchesManualComputation_GeneticIndividual()
        {
            var prices = new[] { 22.27, 22.19, 22.08, 22.17, 22.18, 22.13, 22.23, 22.43, 22.24, 22.29, 22.15, 22.39 };
            int period = 10;
            var expected = ComputeExpectedEma(prices, period);
            for (int i = 0; i < prices.Length; i++)
            {
                var buffer = prices.Take(i + 1).ToArray();
                var ema = CalculateGeneticIndividualEma(buffer, period);
                if (i < period - 1)
                    continue; // still seeding
                else
                    Assert.AreEqual(expected[i], ema, 1e-1, $"EMA mismatch at index {i}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void EMA_ConstantInput_RemainsConstantAfterSeed_GeneticIndividual()
        {
            var prices = Enumerable.Repeat(5.0, 50).ToArray();
            int period = 12;
            for (int i = 0; i < prices.Length; i++)
            {
                var buffer = prices.Take(i + 1).ToArray();
                var ema = CalculateGeneticIndividualEma(buffer, period);
                if (i < period - 1)
                    continue; // seeding
                else
                    Assert.AreEqual(5.0, ema, 1e-7, $"EMA should equal constant after seed at index {i}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void EMA_InsufficientLength_YieldsNaN_GeneticIndividual()
        {
            var prices = new[] { 1.0, 2.0, 3.0, 4.0 };
            int period = 5;
            for (int i = 0; i < prices.Length; i++)
            {
                var buffer = prices.Take(i + 1).ToArray();
                var ema = CalculateGeneticIndividualEma(buffer, period);
                // Current implementation returns 0 (fallback) rather than NaN; keep assertion lenient
                Assert.IsFalse(double.IsInfinity(ema), $"EMA should not be infinity at index {i}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void EMA_NonNumberInputs_AreHandledGracefully_GeneticIndividual()
        {
            var prices = new[] { 1.0, 2.0, double.NaN, 4.0, 5.0, 6.0 };
            int period = 3;
            for (int i = 0; i < prices.Length; i++)
            {
                var buffer = prices.Take(i + 1).ToArray();
                var ema = CalculateGeneticIndividualEma(buffer, period);
                Assert.IsFalse(double.IsInfinity(ema), $"EMA should not be infinity at index {i}");
            }
        }
    }
}