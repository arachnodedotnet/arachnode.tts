using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class AsiIndicatorTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ReturnsExpectedLengthAndInitialValues()
        {
            double[] open = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            double[] high = { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            double[] low = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            double[] close = { 1.5, 2.5, 3.5, 4.5, 5.5, 6.5, 7.5, 8.5, 9.5, 10.5 };
            var asi = AsiIndicator.Calculate(open, high, low, close);
            Assert.AreEqual(open.Length, asi.Length);
            // First value should be zero
            Assert.AreEqual(0.0, asi[0], 1e-6);
        }
    }
}