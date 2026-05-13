//using System;
//using System.Linq;
//using Microsoft.VisualStudio.TestTools.UnitTesting;

//namespace Trade.Tests
//{
//    [TestClass]
//    public class SMAIndicatorTests
//    {
//        private static double[] ExpectedSma(double[] x, int period)
//        {
//            var n = x.Length;
//            var sma = Enumerable.Repeat(double.NaN, n).ToArray();
//            if (period <= 0) return sma;
//            if (n < period) return sma;

//            double windowSum = 0.0;
//            for (int i = 0; i < period; i++) windowSum += x[i];
//            sma[period - 1] = windowSum / period;

//            for (int i = period; i < n; i++)
//            {
//                windowSum += x[i] - x[i - period];
//                sma[i] = windowSum / period;
//            }
//            return sma;
//        }

//        [TestMethod]
//        public void SMA_PeriodOne_EqualsPrices()
//        {
//            // Arrange
//            var prices = new[] { 10.0, 10.5, 11.0, 10.0, 9.5, 10.25, 10.75 };

//            // Act
//            var sma = SMAIndicator.Calculate(prices.Select(p => (float)p).ToArray(), period: 1)
//                                  .Select(v => (double)v).ToArray();

//            // Assert
//            CollectionAssert.AreEqual(prices, sma, "SMA with period=1 must equal the source series.");
//        }

//        [TestMethod]
//        public void SMA_KnownSeries_MatchesManualComputation()
//        {
//            // Arrange
//            var prices = new[] { 22.27, 22.19, 22.08, 22.17, 22.18, 22.13, 22.23, 22.43, 22.24, 22.29, 22.15, 22.39 };
//            int period = 10;
//            var expected = ExpectedSma(prices, period);

//            // Act
//            var sma = SMAIndicator.Calculate(prices.Select(p => (float)p).ToArray(), period)
//                                  .Select(v => (double)v).ToArray();

//            // Assert
//            for (int i = 0; i < prices.Length; i++)
//            {
//                if (i < period - 1)
//                {
//                    Assert.IsTrue(double.IsNaN(sma[i]), $"Warmup SMA[{i}] should be NaN until index {period - 1}");
//                }
//                else
//                {
//                    Assert.AreEqual(expected[i], sma[i], 1e-9, $"SMA mismatch at index {i}");
//                }
//            }
//        }

//        [TestMethod]
//        public void SMA_InsufficientLength_AllNaN()
//        {
//            var prices = new[] { 1.0, 2.0, 3.0, 4.0 };
//            int period = 5;

//            var sma = SMAIndicator.Calculate(prices.Select(p => (float)p).ToArray(), period)
//                                  .Select(v => (double)v).ToArray();

//            Assert.AreEqual(prices.Length, sma.Length);
//            Assert.IsTrue(sma.All(double.IsNaN), "When length < period, all SMA values should be NaN.");
//        }

//        [TestMethod]
//        public void SMA_ConstantSeries_EqualsConstantAfterSeed()
//        {
//            var prices = Enumerable.Repeat(5.0, 50).ToArray();
//            int period = 12;

//            var sma = SMAIndicator.Calculate(prices.Select(p => (float)p).ToArray(), period)
//                                  .Select(v => (double)v).ToArray();

//            for (int i = 0; i < prices.Length; i++)
//            {
//                if (i < period - 1)
//                    Assert.IsTrue(double.IsNaN(sma[i]));
//                else
//                    Assert.AreEqual(5.0, sma[i], 1e-12);
//            }
//        }

//        [TestMethod]
//        public void SMA_PeriodChange_FastTracksPriceCloserThanSlow()
//        {
//            var prices = new[] { 10, 11, 12, 11, 10, 9, 10, 11, 12 }.Select(d => (float)d).ToArray();

//            var smaFast = SMAIndicator.Calculate(prices, period: 3).Select(v => (double)v).ToArray();
//            var smaSlow = SMAIndicator.Calculate(prices, period: 10).Select(v => (double)v).ToArray();

//            for (int i = 0; i < prices.Length; i++)
//            {
//                if (i >= 2) // fast warmup
//                {
//                    var p = prices[i];
//                    var df = Math.Abs(smaFast[i] - p);
//                    var ds = (i >= 9) ? Math.Abs(smaSlow[i] - p) : double.PositiveInfinity;
//                    if (!double.IsInfinity(ds))
//                        Assert.IsTrue(df <= ds + 1e-9, $"Fast SMA should be at least as close to price as slow SMA at index {i}");
//                }
//            }
//        }

//        [TestMethod]
//        public void SMA_WindowSumProperty_HoldsExactly()
//        {
//            // For any i >= period-1: period * SMA[i] == sum(prices[i-period+1..i])
//            var rnd = new Random(123);
//            var prices = Enumerable.Range(0, 100).Select(_ => rnd.NextDouble() * 100).ToArray();
//            int period = 20;

//            var sma = SMAIndicator.Calculate(prices.Select(p => (float)p).ToArray(), period)
//                                  .Select(v => (double)v).ToArray();

//            for (int i = period - 1; i < prices.Length; i++)
//            {
//                var windowSum = prices.Skip(i - period + 1).Take(period).Sum();
//                Assert.AreEqual(windowSum, period * sma[i], 1e-6, $"Window sum property failed at index {i}");
//            }
//        }
//    }
//}
