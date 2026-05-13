using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class MassIndexTests
    {
        [TestMethod][TestCategory("Core")]
        public void Calculate_BasicLengthAndNoException()
        {
            var len = 100;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
            }

            var result = MassIndex.Calculate(high, low);
            Assert.AreEqual(len, result.MI.Length);
            Assert.AreEqual(len, result.HL.Length);
            Assert.AreEqual(len, result.EMA_HL.Length);
            Assert.AreEqual(len, result.EMA_EMA_HL.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_FlatPrices_MIZero()
        {
            var len = 30;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100.0;
                low[i] = 100.0;
            }

            var result = MassIndex.Calculate(high, low);
            for (var i = 0; i < len; i++)
                Assert.AreEqual(0.0, result.MI[i], 1e-8);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_Uptrend_MIZero()
        {
            var len = 30;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
            }

            var result = MassIndex.Calculate(high, low);
            for (var i = 0; i < len; i++)
                Assert.AreEqual(0.0, result.MI[i], 1e-8);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_Downtrend_MIZero()
        {
            var len = 30;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 130 - i;
                low[i] = 120 - i;
            }

            var result = MassIndex.Calculate(high, low);
            for (var i = 0; i < len; i++)
                Assert.AreEqual(0.0, result.MI[i], 1e-8);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ShortArray_ReturnsZeroBuffers()
        {
            double[] high = { 100, 101 };
            double[] low = { 99, 98 };
            var result = MassIndex.Calculate(high, low);
            Assert.AreEqual(2, result.MI.Length);
            Assert.IsTrue(Array.TrueForAll(result.MI, x => Math.Abs(x) < 1e-8));
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_PeriodOne_MatchesHL()
        {
            double[] high = { 100, 101, 102 };
            double[] low = { 99, 98, 97 };
            var result = MassIndex.Calculate(high, low, 1, 1, 1);
            for (var i = 0; i < high.Length; i++)
            {
                var hl = high[i] - low[i];
                Assert.AreEqual(hl, result.HL[i], 1e-8);
                Assert.AreEqual(hl, result.EMA_HL[i], 1e-8);
                Assert.AreEqual(hl, result.EMA_EMA_HL[i], 1e-8);
                Assert.AreEqual(hl / hl, result.MI[i], 1e-8);
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ManualCheck_KnownValues()
        {
            double[] high = { 10, 12, 14, 16, 18 };
            double[] low = { 5, 7, 9, 11, 13 };
            var result = MassIndex.Calculate(high, low, 2, 2, 2);
            // HL: [5,5,5,5,5]
            for (var i = 0; i < 5; i++)
                Assert.AreEqual(5.0, result.HL[i], 1e-8);
            // EMA_HL: first = 5, next = 5, etc.
            for (var i = 0; i < 5; i++)
                Assert.AreEqual(5.0, result.EMA_HL[i], 1e-8);
            // EMA_EMA_HL: first = 5, next = 5, etc.
            for (var i = 0; i < 5; i++)
                Assert.AreEqual(5.0, result.EMA_EMA_HL[i], 1e-8);
            // MI: for i >= 3, sum of 2 values = 5/5 + 5/5 = 2
            for (var i = 3; i < 5; i++)
                Assert.AreEqual(2.0, result.MI[i], 1e-8);
        }
    }
}