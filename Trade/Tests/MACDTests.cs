using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class MACDTests
    {
        [TestMethod][TestCategory("Core")]
        public void Calculate_BasicLengthAndNoException()
        {
            var len = 100;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100 + i;
            var result = MACD.Calculate(prices);
            Assert.AreEqual(len, result.MACD.Length);
            Assert.AreEqual(len, result.Signal.Length);
            Assert.AreEqual(len, result.FastEMA.Length);
            Assert.AreEqual(len, result.SlowEMA.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_FlatPrices_AllZeroExceptInit()
        {
            var len = 50;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100.0;
            var result = MACD.Calculate(prices);
            for (var i = 0; i < len; i++)
            {
                Assert.IsTrue(Math.Abs(result.MACD[i]) < 1e-8);
                Assert.IsTrue(Math.Abs(result.Signal[i]) < 1e-6); // EMA converges to zero, allow looser tolerance
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_Uptrend_PositiveMACD()
        {
            var len = 60;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100 + i;
            var result = MACD.Calculate(prices);
            for (var i = 30; i < len; i++)
                Assert.IsTrue(result.MACD[i] > 0.0);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_Downtrend_NegativeMACD()
        {
            var len = 60;
            var prices = new double[len];
            for (var i = 0; i < len; i++) prices[i] = 100 - i;
            var result = MACD.Calculate(prices);
            for (var i = 30; i < len; i++)
                Assert.IsTrue(result.MACD[i] < 0.0);
        }
    }
}