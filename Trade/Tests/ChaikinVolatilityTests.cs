using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class ChaikinVolatilityTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_BasicLengthAndNoException()
        {
            var len = 50;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
            }

            var chv = new ChaikinVolatility();
            var result = chv.Calculate(high, low);
            Assert.AreEqual(len, result.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_FlatPrices_CHVZero()
        {
            var len = 30;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100.0;
                low[i] = 90.0;
            }

            var chv = new ChaikinVolatility();
            var result = chv.Calculate(high, low);
            for (var i = 20; i < len; i++)
                Assert.AreEqual(0.0, result[i], 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Uptrend_CHVPositive()
        {
            var len = 40;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i / 2.0;
            }

            var chv = new ChaikinVolatility(5, 5);
            var result = chv.Calculate(high, low);
            var foundPositive = false;
            for (var i = 10; i < len; i++)
                if (result[i] > 0.0)
                    foundPositive = true;
            Assert.IsTrue(foundPositive);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Downtrend_CHVNegative()
        {
            var len = 40;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 140 - i;
                low[i] = 130 - i / 2.0;
            }

            var chv = new ChaikinVolatility(5, 5);
            var result = chv.Calculate(high, low);
            var foundNegative = false;
            for (var i = 10; i < len; i++)
                if (result[i] < 0.0)
                    foundNegative = true;
            Assert.IsTrue(foundNegative);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ShortArray_ReturnsEmptyBuffer()
        {
            double[] high = { 100, 101 };
            double[] low = { 99, 98 };
            var chv = new ChaikinVolatility();
            var result = chv.Calculate(high, low);
            Assert.AreEqual(0, result.Length);
        }

        //[TestMethod]
        [TestCategory("Core")]
        public void Calculate_PeriodOne_MatchesHLChange()
        {
            double[] high = { 100, 101, 102 };
            double[] low = { 99, 98, 97 };
            var chv = new ChaikinVolatility(1, 1, SmoothMethod.SMA);
            var result = chv.Calculate(high, low);
            for (var i = 1; i < high.Length; i++)
            {
                var prevHL = high[i - 1] - low[i - 1];
                var currHL = high[i] - low[i];
                var expected = prevHL != 0.0 ? 100.0 * (currHL - prevHL) / prevHL : 0.0;
                Assert.AreEqual(expected, result[i], 1e-8);
            }
        }

        //[TestMethod]
        [TestCategory("Core")]
        public void Calculate_SMAAndEMA_ProduceDifferentResults()
        {
            var len = 20;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i % 2;
                low[i] = 90 + i % 2;
            }

            var chvSMA = new ChaikinVolatility(5, 5, SmoothMethod.SMA);
            var chvEMA = new ChaikinVolatility(5, 5);
            var resultSMA = chvSMA.Calculate(high, low);
            var resultEMA = chvEMA.Calculate(high, low);
            var foundDiff = false;
            for (var i = 10; i < len; i++)
                if (Math.Abs(resultSMA[i] - resultEMA[i]) > 1e-6)
                {
                    foundDiff = true;
                    break;
                }

            Assert.IsTrue(foundDiff);
        }
    }
}