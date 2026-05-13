using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class AccumulationDistributionTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ReturnsExpectedAdValues()
        {
            // Example data: high, low, close, volume
            double[] high = { 10, 12, 14, 16 };
            double[] low = { 9, 11, 13, 15 };
            double[] close = { 9.5, 11.5, 13.5, 15.5 };
            long[] volume = { 100, 200, 300, 400 };
            var ad = AccumulationDistribution.Calculate(high, low, close, volume);
            Assert.AreEqual(high.Length, ad.Length);
            // Check first value
            var expected0 = (9.5 - 9 - (10 - 9.5)) / (10 - 9) * 100;
            Assert.AreEqual(expected0, ad[0], 1e-6);
            // Check cumulative property
            var expected1 = (11.5 - 11 - (12 - 11.5)) / (12 - 11) * 200 + ad[0];
            Assert.AreEqual(expected1, ad[1], 1e-6);
        }
    }
}