using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class TEMATests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_BasicLengthAndNoException()
        {
            var len = 100;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100 + i;
            var result = TEMA.Calculate(prices);
            Assert.AreEqual(len, result.TEMA.Length);
            Assert.AreEqual(len, result.EMA.Length);
            Assert.AreEqual(len, result.EMAofEMA.Length);
            Assert.AreEqual(len, result.EMAofEMAofEMA.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_FlatPrices_TEMAFlat()
        {
            var len = 50;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100.0;
            var result = TEMA.Calculate(prices);
            for (var i = 0; i < len; i++)
                Assert.IsTrue(Math.Abs(result.TEMA[i] - 100.0) < 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Uptrend_TEMAUptrend()
        {
            var len = 50;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100 + i;
            var result = TEMA.Calculate(prices);
            for (var i = 30; i < len; i++)
                Assert.IsTrue(result.TEMA[i] > result.TEMA[i - 1]);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Downtrend_TEMADowntrend()
        {
            var len = 50;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100 - i;
            var result = TEMA.Calculate(prices);
            for (var i = 30; i < len; i++)
                Assert.IsTrue(result.TEMA[i] < result.TEMA[i - 1]);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ShortArray_ReturnsZeroBuffers()
        {
            double[] prices = { 100, 101, 102 };
            var result = TEMA.Calculate(prices);
            Assert.AreEqual(3, result.TEMA.Length);
            Assert.IsTrue(Array.TrueForAll(result.TEMA, x => Math.Abs(x) < 1e-8));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_PeriodOne_MatchesPrice()
        {
            double[] prices = { 100, 101, 102, 103, 104 };
            var result = TEMA.Calculate(prices, 1);
            for (var i = 0; i < prices.Length; i++)
                Assert.AreEqual(prices[i], result.TEMA[i], 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ShiftParameter_ShiftsOutput()
        {
            var len = 30;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100 + i;
            var resultNoShift = TEMA.Calculate(prices);
            var resultShift = TEMA.Calculate(prices, 14, 5);
            for (var i = 0; i < len - 5; i++)
                Assert.AreEqual(resultNoShift.TEMA[i], resultShift.TEMA[i + 5], 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Buffers_ValidValues()
        {
            double[] prices = { 100, 102, 101, 103, 99, 104 };
            var result = TEMA.Calculate(prices);
            for (var i = 0; i < prices.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.EMA[i]));
                Assert.IsFalse(double.IsNaN(result.EMAofEMA[i]));
                Assert.IsFalse(double.IsNaN(result.EMAofEMAofEMA[i]));
            }
        }
    }
}