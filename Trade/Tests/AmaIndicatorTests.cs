using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class AmaIndicatorTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ReturnsExpectedLengthAndInitialValues()
        {
            double[] price = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
            int amaPeriod = 10, fastPeriod = 2, slowPeriod = 30;
            var ama = AmaIndicator.Calculate(price, amaPeriod, fastPeriod, slowPeriod);
            Assert.AreEqual(price.Length, ama.Length);
            // First amaPeriod-1 values should be zero
            for (var i = 0; i < amaPeriod - 1; i++)
                Assert.AreEqual(0.0, ama[i], 1e-6);
            // ama[amaPeriod-1] should equal price[amaPeriod-1]
            Assert.AreEqual(price[amaPeriod - 1], ama[amaPeriod - 1], 1e-6);
        }
    }
}