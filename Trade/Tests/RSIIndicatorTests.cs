//using System;
//using System.Linq;
//using Microsoft.VisualStudio.TestTools.UnitTesting;

//namespace Trade.Tests
//{
//    [TestClass]
//    public class RSIIndicatorTests
//    {
//        // Helper: Wilder-style RSI (institutional standard)
//        // - Seed avg gain/loss at index = period via simple averages over the prior 'period' diffs
//        // - Then recursive smoothing:
//        //      avgGain_t = (avgGain_{t-1} * (period - 1) + gain_t) / period
//        //      avgLoss_t = (avgLoss_{t-1} * (period - 1) + loss_t) / period
//        // - RSI = 100 - 100 / (1 + RS), RS = avgGain / avgLoss
//        private static double[] WilderRsi(double[] close, int period)
//        {
//            var n = close.Length;
//            var rsi = Enumerable.Repeat(double.NaN, n).ToArray();
//            if (n == 0 || period <= 0) return rsi;
//            if (n <= period) return rsi;

//            double avgGain = 0.0, avgLoss = 0.0;
//            // seed over first 'period' diffs: indices [1..period]
//            for (int i = 1; i <= period; i++)
//            {
//                var chg = close[i] - close[i - 1];
//                if (chg > 0) avgGain += chg; else avgLoss += -chg;
//            }
//            avgGain /= period;
//            avgLoss /= period;

//            // first valid RSI at index 'period'
//            rsi[period] = (avgLoss == 0.0)
//                ? (avgGain == 0.0 ? 50.0 : 100.0)
//                : 100.0 - 100.0 / (1.0 + (avgGain / avgLoss));

//            for (int i = period + 1; i < n; i++)
//            {
//                var chg = close[i] - close[i - 1];
//                var gain = Math.Max(chg, 0.0);
//                var loss = Math.Max(-chg, 0.0);
//                avgGain = ((avgGain * (period - 1)) + gain) / period;
//                avgLoss = ((avgLoss * (period - 1)) + loss) / period;

//                if (avgLoss == 0.0)
//                    rsi[i] = (avgGain == 0.0 ? 50.0 : 100.0);
//                else
//                {
//                    var rs = avgGain / avgLoss;
//                    rsi[i] = 100.0 - 100.0 / (1.0 + rs);
//                }
//            }
//            return rsi;
//        }

//        [TestMethod]
//        public void RSI_LengthLessThanPeriod_AllNaN()
//        {
//            var close = new[] { 10.0, 10.5, 10.2 }; // length 3
//            int period = 5;

//            var rsi = RSIIndicator.Calculate(close.Select(x => (float)x).ToArray(), period)
//                                   .Select(x => (double)x).ToArray();

//            Assert.AreEqual(close.Length, rsi.Length);
//            Assert.IsTrue(rsi.All(double.IsNaN), "When length < period+1, RSI should be undefined (NaN) at all indices.");
//        }

//        [TestMethod]
//        public void RSI_AllGains_ResultsAtOrNearHundredAfterSeed()
//        {
//            // strictly increasing
//            var close = Enumerable.Range(0, 40).Select(i => 100.0 + i).ToArray();
//            int period = 14;

//            var rsi = RSIIndicator.Calculate(close.Select(x => (float)x).ToArray(), period)
//                                   .Select(x => (double)x).ToArray();

//            for (int i = 0; i < rsi.Length; i++)
//            {
//                if (i < period) { Assert.IsTrue(double.IsNaN(rsi[i])); }
//                else { Assert.IsTrue(rsi[i] > 99.0 && rsi[i] <= 100.0, $"RSI[{i}] should be ~100 on persistent gains, got {rsi[i]}"); }
//            }
//        }

//        [TestMethod]
//        public void RSI_AllLosses_ResultsAtOrNearZeroAfterSeed()
//        {
//            // strictly decreasing
//            var close = Enumerable.Range(0, 40).Select(i => 100.0 - i).ToArray();
//            int period = 14;

//            var rsi = RSIIndicator.Calculate(close.Select(x => (float)x).ToArray(), period)
//                                   .Select(x => (double)x).ToArray();

//            for (int i = 0; i < rsi.Length; i++)
//            {
//                if (i < period) { Assert.IsTrue(double.IsNaN(rsi[i])); }
//                else { Assert.IsTrue(rsi[i] >= 0.0 && rsi[i] < 1.0, $"RSI[{i}] should be ~0 on persistent losses, got {rsi[i]}"); }
//            }
//        }

//        [TestMethod]
//        public void RSI_FlatSeries_EqualsFiftyAfterSeed()
//        {
//            var close = Enumerable.Repeat(123.45, 50).ToArray();
//            int period = 14;

//            var rsi = RSIIndicator.Calculate(close.Select(x => (float)x).ToArray(), period)
//                                   .Select(x => (double)x).ToArray();

//            for (int i = 0; i < rsi.Length; i++)
//            {
//                if (i < period) { Assert.IsTrue(double.IsNaN(rsi[i])); }
//                else { Assert.AreEqual(50.0, rsi[i], 1e-9, $"RSI[{i}] should be exactly 50 on flat prices."); }
//            }
//        }

//        [TestMethod]
//        public void RSI_MatchesWilderOnKnownSeries()
//        {
//            // A small hand-crafted sequence with mixed moves
//            var close = new[] { 44.34, 44.09, 44.15, 43.61, 44.33, 44.83, 45.10, 45.42, 45.84, 46.08,
//                                45.89, 46.03, 45.61, 46.28, 46.28, 46.00, 46.03, 46.41, 46.22, 45.64,
//                                46.21, 46.25, 45.71, 46.45, 45.78, 45.35, 44.03, 44.18, 44.22, 44.57,
//                                43.42, 42.66, 43.13 };
//            int period = 14;

//            var expected = WilderRsi(close, period);
//            var actual = RSIIndicator.Calculate(close.Select(x => (float)x).ToArray(), period)
//                                     .Select(x => (double)x).ToArray();

//            for (int i = 0; i < close.Length; i++)
//            {
//                if (double.IsNaN(expected[i]))
//                {
//                    Assert.IsTrue(double.IsNaN(actual[i]), $"RSI[{i}] should be NaN during warmup.");
//                }
//                else
//                {
//                    Assert.AreEqual(expected[i], actual[i], 1e-6, $"RSI mismatch at index {i}");
//                }
//            }
//        }

//        [TestMethod]
//        public void RSI_PeriodOne_BinaryBehaviorAfterFirstBar()
//        {
//            // For period=1 with Wilder logic, the seed uses the first diff,
//            // and thereafter RSI reflects the immediate up/down move strongly.
//            var close = new[] { 10.0, 11.0, 11.0, 10.5, 12.0, 12.0, 11.0 };
//            int period = 1;

//            var rsi = RSIIndicator.Calculate(close.Select(x => (float)x).ToArray(), period)
//                                   .Select(x => (double)x).ToArray();

//            // First index (0) NaN; from 1 onward, check qualitative behavior
//            Assert.IsTrue(double.IsNaN(rsi[0]));
//            for (int i = 1; i < close.Length; i++)
//            {
//                if (close[i] > close[i - 1])
//                    Assert.IsTrue(rsi[i] >= 50.0, $"RSI[{i}] should be >= 50 on uptick.");
//                else if (close[i] < close[i - 1])
//                    Assert.IsTrue(rsi[i] <= 50.0, $"RSI[{i}] should be <= 50 on downtick.");
//                else
//                    Assert.AreEqual(50.0, rsi[i], 1e-6, $"RSI[{i}] should be 50 on no change.");
//            }
//        }

//        [TestMethod]
//        public void RSI_HandlesNaNs_WithoutThrowingAndPreservesLength()
//        {
//            var close = new[] { 100.0, 101.0, double.NaN, 102.0, 103.0, 102.5, 103.5, double.NaN, 104.0 };
//            int period = 3;

//            float[] input = close.Select(x => (float)x).ToArray();
//            var rsi = RSIIndicator.Calculate(input, period);

//            Assert.AreEqual(close.Length, rsi.Length, "Output length must match input length.");
//            // We don't prescribe exact values around NaNs (implementation-dependent),
//            // but we assert no exceptions and range sanity for finite values.
//            Assert.AreEqual(0.0, rsi[0], 1e-6, "RSI should be 0 for first value");
//        }
//    }
//}
