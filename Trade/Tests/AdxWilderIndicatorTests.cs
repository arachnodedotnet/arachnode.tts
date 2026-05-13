using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class AdxWilderIndicatorTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ReturnsExpectedLengths()
        {
            double[] high = { 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40 };
            double[] low = { 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31, 33, 35, 37, 39 };
            double[] close =
                { 9.5, 11.5, 13.5, 15.5, 17.5, 19.5, 21.5, 23.5, 25.5, 27.5, 29.5, 31.5, 33.5, 35.5, 37.5, 39.5 };
            var period = 14;
            var (adxw, plusDi, minusDi) = AdxWilderIndicator.Calculate(high, low, close, period);
            Assert.AreEqual(high.Length, adxw.Length);
            Assert.AreEqual(high.Length, plusDi.Length);
            Assert.AreEqual(high.Length, minusDi.Length);
            // Check that first value is zero (as in MQL5)
            Assert.AreEqual(0.0, plusDi[0], 1e-6);
            Assert.AreEqual(0.0, minusDi[0], 1e-6);
        }
    }
}