using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class OsMATests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_BasicLengthAndNoException()
        {
            var len = 100;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100 + i;
            var result = OsMA.Calculate(prices);
            Assert.AreEqual(len, result.OsMA.Length);
            Assert.AreEqual(len, result.MACD.Length);
            Assert.AreEqual(len, result.Signal.Length);
            Assert.AreEqual(len, result.FastEMA.Length);
            Assert.AreEqual(len, result.SlowEMA.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_FlatPrices_AllZeroExceptInit()
        {
            var len = 50;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100.0;
            var result = OsMA.Calculate(prices);
            for (var i = 0; i < len; i++)
            {
                Assert.IsTrue(Math.Abs(result.OsMA[i]) < 1e-8);
                Assert.IsTrue(Math.Abs(result.MACD[i]) < 1e-8);
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Uptrend_PositiveMACD()
        {
            var len = 60;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100 + i;
            var result = OsMA.Calculate(prices);
            for (var i = 30; i < len; i++)
                Assert.IsTrue(result.MACD[i] > 0.0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Downtrend_NegativeMACD()
        {
            var len = 60;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100 - i;
            var result = OsMA.Calculate(prices);
            for (var i = 30; i < len; i++)
                Assert.IsTrue(result.MACD[i] < 0.0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_AlternatingPrices_OsMAOscillates()
        {
            var len = 40;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = i % 2 == 0 ? 100 : 101;
            var result = OsMA.Calculate(prices);
            bool hasPositive = false, hasNegative = false;
            for (var i = 10; i < len; i++)
            {
                if (result.OsMA[i] > 0.01) hasPositive = true;
                if (result.OsMA[i] < -0.01) hasNegative = true;
            }

            Assert.IsTrue(hasPositive);
            Assert.IsTrue(hasNegative);
        }
    }
}