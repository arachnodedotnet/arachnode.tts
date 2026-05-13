using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class AlligatorIndicatorTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ReturnsExpectedLengths()
        {
            double[] high = { 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40 };
            double[] low = { 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31, 33, 35, 37, 39 };
            int jawsPeriod = 13, jawsShift = 8;
            int teethPeriod = 8, teethShift = 5;
            int lipsPeriod = 5, lipsShift = 3;
            var (jaws, teeth, lips) = AlligatorIndicator.Calculate(high, low, jawsPeriod, jawsShift, teethPeriod,
                teethShift, lipsPeriod, lipsShift);
            Assert.AreEqual(high.Length, jaws.Length);
            Assert.AreEqual(high.Length, teeth.Length);
            Assert.AreEqual(high.Length, lips.Length);
            // Check that first values are zero (as in MQL5)
            Assert.AreEqual(0.0, jaws[0], 1e-6);
            Assert.AreEqual(0.0, teeth[0], 1e-6);
            Assert.AreEqual(0.0, lips[0], 1e-6);
        }
    }
}