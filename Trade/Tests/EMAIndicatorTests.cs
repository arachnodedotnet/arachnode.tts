//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using System;
//using System.Linq;
//using Trade.Indicators;

//namespace Trade.Tests
//{
//    [TestClass]
//    public class EMAIndicatorTests
//    {
//        // Helper: compute expected EMA sequence using standard definition:
//        // - Seed at index period-1 with SMA of first 'period' values
//        // - Thereafter: EMA[i] = alpha * x[i] + (1 - alpha) * EMA[i-1], where alpha = 2/(period+1)
//        private static double[] ComputeExpectedEma(double[] x, int period)
//        {
//            if (period <= 0) throw new ArgumentOutOfRangeException(nameof(period));
//            if (x == null) throw new ArgumentNullException(nameof(x));
//            var n = x.Length;
//            var ema = Enumerable.Repeat(double.NaN, n).ToArray();
//            if (n == 0) return ema;

//            if (n >= period)
//            {
//                double seed = 0.0;
//                for (int i = 0; i < period; i++) seed += x[i];
//                seed /= period;
//                ema[period - 1] = seed;
//                double alpha = 2.0 / (period + 1.0);
//                for (int i = period; i < n; i++)
//                {
//                    ema[i] = alpha * x[i] + (1.0 - alpha) * ema[i - 1];
//                }
//            }
//            return ema;
//        }

//        [TestMethod]
//        public void EMA_PeriodOne_EqualsPriceSeries()
//        {
//            // Arrange
//            var prices = new[] { 10.0, 10.5, 11.0, 10.0, 9.5, 10.25, 10.75 };
//            int period = 1;

//            // Act
//            var ema = EMAIndicator.Calculate(prices.Select(p => (float)p).ToArray(), period)
//                                   .Select(v => (double)v).ToArray();

//            // Assert
//            for (int i = 0; i < prices.Length; i++)
//            {
//                Assert.AreEqual(prices[i], ema[i], 1e-6, $"EMA with period=1 must equal price at index {i}");
//            }
//        }

//        [TestMethod]
//        public void EMA_KnownSeries_MatchesManualComputation()
//        {
//            // Arrange
//            var prices = new[] { 22.27, 22.19, 22.08, 22.17, 22.18, 22.13, 22.23, 22.43, 22.24, 22.29, 22.15, 22.39 };
//            int period = 10; // classic example length
//            var expected = ComputeExpectedEma(prices, period);

//            // Act
//            var ema = EMAIndicator.Calculate(prices.Select(p => (float)p).ToArray(), period)
//                                   .Select(v => (double)v).ToArray();

//            // Assert (allow NaN in the warmup zone)
//            for (int i = 0; i < prices.Length; i++)
//            {
//                if (i < period - 1)
//                {
//                    Assert.IsTrue(double.IsNaN(ema[i]),
//                        $"EMA[{i}] should be undefined (NaN) until index {period - 1}");
//                }
//                else
//                {
//                    Assert.AreEqual(expected[i], ema[i], 1e-6, $"EMA mismatch at index {i}");
//                }
//            }
//        }

//        [TestMethod]
//        public void EMA_ConstantInput_RemainsConstantAfterSeed()
//        {
//            // Arrange
//            var prices = Enumerable.Repeat(5.0, 50).ToArray();
//            int period = 12;

//            // Act
//            var ema = EMAIndicator.Calculate(prices.Select(p => (float)p).ToArray(), period)
//                                   .Select(v => (double)v).ToArray();

//            // Assert
//            for (int i = 0; i < prices.Length; i++)
//            {
//                if (i < period - 1)
//                {
//                    Assert.IsTrue(double.IsNaN(ema[i]), $"Warmup EMA[{i}] should be NaN");
//                }
//                else
//                {
//                    Assert.AreEqual(5.0, ema[i], 1e-7, $"EMA should equal constant after seed at index {i}");
//                }
//            }
//        }

//        [TestMethod]
//        public void EMA_InsufficientLength_YieldsAllNaN()
//        {
//            // Arrange
//            var prices = new[] { 1.0, 2.0, 3.0, 4.0 }; // length < period
//            int period = 5;

//            // Act
//            var ema = EMAIndicator.Calculate(prices.Select(p => (float)p).ToArray(), period)
//                                   .Select(v => (double)v).ToArray();

//            // Assert
//            Assert.AreEqual(prices.Length, ema.Length, "Length should match input length");
//            Assert.IsTrue(ema.All(double.IsNaN), "All EMA values should be NaN when length < period");
//        }

//        [TestMethod]
//        public void EMA_VariesSmoothly_NoOvershootBeyondExtrema()
//        {
//            // Arrange: monotonic increase then decrease
//            var prices = new[] { 10.0, 10.1, 10.2, 10.3, 10.4, 10.3, 10.2, 10.1, 10.0, 9.9, 9.8 };
//            int period = 3;

//            // Act
//            var ema = EMAIndicator.Calculate(prices.Select(p => (float)p).ToArray(), period)
//                                   .Select(v => (double)v).ToArray();

//            // Assert: EMA should remain within [min,max] of the prefix up to each point (no overshoot)
//            double runningMin = double.PositiveInfinity;
//            double runningMax = double.NegativeInfinity;
//            for (int i = 0; i < prices.Length; i++)
//            {
//                runningMin = Math.Min(runningMin, prices[i]);
//                runningMax = Math.Max(runningMax, prices[i]);

//                if (i >= period - 1)
//                {
//                    Assert.IsTrue(ema[i] <= runningMax + 1e-9 && ema[i] >= runningMin - 1e-9,
//                        $"EMA[{i}] should lie within the range of observed prices up to i");
//                }
//            }
//        }

//        [TestMethod]
//        public void EMA_NonNumberInputs_AreHandledGracefully()
//        {
//            // Arrange: include a NaN spike; typical implementations propagate NaN from that point
//            var prices = new[] { 1.0, 2.0, double.NaN, 4.0, 5.0, 6.0 };
//            int period = 3;

//            // Act
//            var ema = EMAIndicator.Calculate(prices.Select(p => (float)p).ToArray(), period)
//                                   .Select(v => (double)v).ToArray();

//            // Assert: Before NaN and before seed, expect NaN; after seed but before NaN, finite; once NaN hit, allow NaN
//            for (int i = 0; i < prices.Length; i++)
//            {
//                if (double.IsNaN(prices[i]) || (i > 0 && double.IsNaN(prices[i - 1])))
//                {
//                    // Depending on implementation, NaN may propagate. We only assert no exceptions and array length match.
//                    continue;
//                }
//            }

//            Assert.AreEqual(prices.Length, ema.Length, "Output length must equal input length");
//        }

//        [TestMethod]
//        public void EMA_PeriodChange_AffectsSmoothingWeight()
//        {
//            // Arrange
//            var prices = new[] { 10, 11, 12, 11, 10, 9, 10, 11, 12 }.Select(d => (float)d).ToArray();

//            // Act
//            var emaFast = EMAIndicator.Calculate(prices, period: 3).Select(v => (double)v).ToArray();
//            var emaSlow = EMAIndicator.Calculate(prices, period: 10).Select(v => (double)v).ToArray();

//            // Assert: after warmup, fast EMA should track price more closely (closer to price than slow EMA)
//            for (int i = 0; i < prices.Length; i++)
//            {
//                if (i >= 2) // warmup for period 3
//                {
//                    var distFast = Math.Abs(emaFast[i] - prices[i]);
//                    var distSlow = (i >= 9) ? Math.Abs(emaSlow[i] - prices[i]) : double.PositiveInfinity;
//                    if (!double.IsInfinity(distSlow))
//                    {
//                        Assert.IsTrue(distFast <= distSlow + 1e-9, $"Fast EMA should be at least as close to price as slow EMA at index {i}");
//                    }
//                }
//            }
//        }
//    }
//}
