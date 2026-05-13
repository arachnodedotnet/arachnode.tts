using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class RSITests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_BasicLengthAndNoException()
        {
            var len = 100;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100 + i;
            var result = RSI.Calculate(prices);
            Assert.AreEqual(len, result.RSI.Length);
            Assert.AreEqual(len, result.PosBuffer.Length);
            Assert.AreEqual(len, result.NegBuffer.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_FlatPrices_RSIIs50()
        {
            var len = 30;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100.0;
            var result = RSI.Calculate(prices);
            for (var i = 14; i < len; i++)
                Assert.AreEqual(50.0, result.RSI[i], 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Uptrend_RSIHigh()
        {
            var len = 30;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100 + i;
            var result = RSI.Calculate(prices);
            for (var i = 14; i < len; i++)
                Assert.IsTrue(result.RSI[i] > 80.0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Downtrend_RSILow()
        {
            var len = 30;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100 - i;
            var result = RSI.Calculate(prices);
            for (var i = 14; i < len; i++)
                Assert.IsTrue(result.RSI[i] < 20.0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_AlternatingPrices_RSINear50()
        {
            var len = 30;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = i % 2 == 0 ? 100 : 101;
            var result = RSI.Calculate(prices);
            for (var i = 14; i < len; i++)
                Assert.IsTrue(Math.Abs(result.RSI[i] - 50.0) < 10.0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ShortArray_ReturnsZeroBuffers()
        {
            double[] prices = { 100, 101, 102 };
            var result = RSI.Calculate(prices);
            Assert.AreEqual(3, result.RSI.Length);
            Assert.IsTrue(Array.TrueForAll(result.RSI, x => Math.Abs(x) < 1e-8));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_PeriodOne_MatchesPriceChange()
        {
            double[] prices = { 100, 101, 100, 102, 99 };
            var result = RSI.Calculate(prices, 1);
            // RSI[1] should be 100 (up), RSI[2] should be 0 (down), etc.
            Assert.AreEqual(100.0, result.RSI[1], 1e-8);
            Assert.AreEqual(0.0, result.RSI[2], 1e-8);
            Assert.AreEqual(100.0, result.RSI[3], 1e-8);
            Assert.AreEqual(0.0, result.RSI[4], 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_PosNegBuffers_ValidValues()
        {
            double[] prices = { 100, 102, 101, 103, 99, 104 };
            var result = RSI.Calculate(prices);
            for (var i = 0; i < prices.Length; i++)
            {
                Assert.IsTrue(result.PosBuffer[i] >= 0.0);
                Assert.IsTrue(result.NegBuffer[i] >= 0.0);
            }
        }
    }
}