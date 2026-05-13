using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class WPRTests
    {
        [TestMethod][TestCategory("Core")]
        public void Calculate_BasicLengthAndNoException()
        {
            var len = 100;
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
                close[i] = 95 + i;
            }

            var result = WPR.Calculate(high, low, close);
            Assert.AreEqual(len, result.WPR.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_FlatPrices_WPRZero()
        {
            var len = 30;
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100.0;
                low[i] = 100.0;
                close[i] = 100.0;
            }

            var result = WPR.Calculate(high, low, close, 10);
            for (var i = 9; i < len; i++)
                Assert.AreEqual(0.0, result.WPR[i], 1e-8);
        }

        //[TestMethod][TestCategory("Core")]
        public void Calculate_Uptrend_WPRNearZero()
        {
            var len = 30;
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
                close[i] = 95 + i;
            }

            var result = WPR.Calculate(high, low, close, 10);
            for (var i = 20; i < len; i++)
                Assert.IsTrue(result.WPR[i] > -10.0 && result.WPR[i] < 0.1);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_Downtrend_WPRNearMinus100()
        {
            var len = 30;
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 130 - i;
                low[i] = 120 - i;
                close[i] = 120 - i;
            }

            var result = WPR.Calculate(high, low, close, 10);
            for (var i = 20; i < len; i++)
                Assert.IsTrue(result.WPR[i] < -90.0 && result.WPR[i] > -100.1);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ShortArray_ReturnsZeroBuffer()
        {
            double[] high = { 100, 101 };
            double[] low = { 99, 98 };
            double[] close = { 100, 99 };
            var result = WPR.Calculate(high, low, close, 5);
            Assert.AreEqual(2, result.WPR.Length);
            Assert.IsTrue(Array.TrueForAll(result.WPR, x => Math.Abs(x) < 1e-8));
        }

        //[TestMethod][TestCategory("Core")]
        public void Calculate_PeriodOne_MatchesFormula()
        {
            double[] high = { 100, 101, 102 };
            double[] low = { 99, 98, 97 };
            double[] close = { 99, 100, 101 };
            var result = WPR.Calculate(high, low, close, 1);
            for (var i = 0; i < high.Length; i++)
            {
                var expected = -(high[i] - close[i]) * 100.0 / (high[i] - low[i]);
                Assert.AreEqual(expected, result.WPR[i], 1e-8);
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ManualCheck_KnownValues()
        {
            double[] high = { 10, 12, 14, 16, 18 };
            double[] low = { 5, 7, 9, 11, 13 };
            double[] close = { 8, 9, 10, 11, 12 };
            var result = WPR.Calculate(high, low, close, 3);
            // At i=2, highest of [10,12,14]=14, lowest of [5,7,9]=5, close=10
            var expected = -(14 - 10) * 100.0 / (14 - 5);
            Assert.AreEqual(expected, result.WPR[2], 1e-8);
        }
    }
}