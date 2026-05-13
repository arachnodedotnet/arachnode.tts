using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class AtrIndicatorTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ReturnsExpectedLengthAndInitialValues()
        {
            double[] high = { 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40 };
            double[] low = { 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31, 33, 35, 37, 39 };
            double[] close =
                { 9.5, 11.5, 13.5, 15.5, 17.5, 19.5, 21.5, 23.5, 25.5, 27.5, 29.5, 31.5, 33.5, 35.5, 37.5, 39.5 };
            var period = 14;
            var atr = AtrIndicator.Calculate(high, low, close, period);
            Assert.AreEqual(high.Length, atr.Length);

            // First ATR values should be zero during initialization period (0 to period-1)
            for (var i = 0; i < period; i++)
                Assert.AreEqual(0.0, atr[i], 1e-6, $"ATR[{i}] should be zero during initialization period");

            // ATR[period] should be the first calculated value (non-zero)
            if (atr.Length > period)
                Assert.AreNotEqual(0.0, atr[period], 1e-6, $"ATR[{period}] should be the first calculated ATR value");

            // ATR[period+1] should also be nonzero if enough data
            if (atr.Length > period + 1)
                Assert.AreNotEqual(0.0, atr[period + 1], 1e-6, $"ATR[{period + 1}] should be calculated");
        }
    }
}