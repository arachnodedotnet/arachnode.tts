using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class AwesomeOscillatorTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ReturnsExpectedLengthAndInitialValues()
        {
            double[] high = { 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40 };
            double[] low = { 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31, 33, 35, 37, 39 };
            int fastPeriod = 5, slowPeriod = 14;
            var ao = AwesomeOscillator.Calculate(high, low, fastPeriod, slowPeriod);
            Assert.AreEqual(high.Length, ao.Length);
            // First slowPeriod-1 values should be zero
            for (var i = 0; i < slowPeriod - 1; i++)
                Assert.AreEqual(0.0, ao[i], 1e-6);
            // AO[slowPeriod] should be nonzero if enough data
            if (ao.Length > slowPeriod)
                Assert.AreNotEqual(0.0, ao[slowPeriod], 1e-6);
        }
    }
}