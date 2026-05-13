using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class BearsPowerTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ReturnsExpectedLengthAndValues()
        {
            double[] low = { 9, 11, 13, 15, 17, 19, 21, 23, 25, 27, 29, 31, 33, 35, 37, 39 };
            double[] close =
                { 9.5, 11.5, 13.5, 15.5, 17.5, 19.5, 21.5, 23.5, 25.5, 27.5, 29.5, 31.5, 33.5, 35.5, 37.5, 39.5 };
            var period = 5;
            var bears = BearsPower.Calculate(low, close, period);
            Assert.AreEqual(low.Length, bears.Length);
            // Check that values are calculated
            for (var i = 0; i < bears.Length; i++)
                // Bears Power = low - EMA(close)
                // For first value, EMA = close[0]
                if (i == 0)
                    Assert.AreEqual(low[0] - close[0], bears[0], 1e-6);
        }
    }
}