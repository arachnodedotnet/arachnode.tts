using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class UltimateOscillatorTests
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

            var result = UltimateOscillator.Calculate(high, low, close);
            Assert.AreEqual(len, result.UO.Length);
            Assert.AreEqual(len, result.BP.Length);
            Assert.AreEqual(len, result.FastATR.Length);
            Assert.AreEqual(len, result.MiddleATR.Length);
            Assert.AreEqual(len, result.SlowATR.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_FlatPrices_UOZero()
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

            var result = UltimateOscillator.Calculate(high, low, close);
            for (var i = 28; i < len; i++)
                Assert.AreEqual(0.0, result.UO[i], 1e-8);
        }


        [TestMethod][TestCategory("Core")]
        public void Calculate_Downtrend_UOLow()
        {
            var len = 40;
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 130 - i;
                low[i] = 120 - i;
                close[i] = 120 - i;
            }

            var result = UltimateOscillator.Calculate(high, low, close);
            for (var i = 28; i < len; i++)
                Assert.IsTrue(result.UO[i] < 20.0);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ShortArray_ReturnsZeroBuffers()
        {
            double[] high = { 100, 101 };
            double[] low = { 99, 98 };
            double[] close = { 100, 99 };
            var result = UltimateOscillator.Calculate(high, low, close);
            Assert.AreEqual(2, result.UO.Length);
            Assert.IsTrue(Array.TrueForAll(result.UO, x => Math.Abs(x) < 1e-8));
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_PeriodOne_MatchesFormula()
        {
            double[] high = { 100, 101, 102 };
            double[] low = { 99, 98, 97 };
            double[] close = { 99, 100, 101 };
            var result = UltimateOscillator.Calculate(high, low, close, 1, 1, 1, 1, 1);
            for (var i = 1; i < high.Length; i++)
            {
                var bp = close[i] - Math.Min(low[i], close[i - 1]);
                var tr = Math.Max(high[i], close[i - 1]) - Math.Min(low[i], close[i - 1]);
                var expected = tr != 0.0 ? bp / tr * 100.0 : 0.0;
                Assert.AreEqual(expected, result.UO[i], 1e-8);
            }
        }
    }
}