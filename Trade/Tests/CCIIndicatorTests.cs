using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class CCIIndicatorTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void CCIIndicator_CalculatesCorrectly_ForKnownValues()
        {
            double[] priceBuffer = { 100, 102, 101, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114 };
            var period = 14;
            var idx = priceBuffer.Length - 1;
            var cci = CCIIndicator.Calculate(idx, period, priceBuffer);
            var sma = priceBuffer.Skip(idx - period + 1).Take(period).Average();
            var meanDeviation = 0.0;
            for (var j = 0; j < period; j++)
                meanDeviation += Math.Abs(priceBuffer[idx - j] - sma);
            meanDeviation *= 0.015 / period;
            var m = priceBuffer[idx] - sma;
            var expected = meanDeviation != 0.0 ? m / meanDeviation : 0.0;
            Assert.AreEqual(expected, cci, 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CCIIndicator_ReturnsZero_WhenPeriodTooLarge()
        {
            double[] priceBuffer = { 100, 101, 102 };
            var period = 10;
            var idx = priceBuffer.Length - 1;
            var cci = CCIIndicator.Calculate(idx, period, priceBuffer);
            Assert.AreEqual(0.0, cci, 1e-8);
        }
    }
}