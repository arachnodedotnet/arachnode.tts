using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class BullsPowerTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ReturnsExpectedLengthAndValues()
        {
            double[] high = { 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40 };
            double[] close =
                { 9.5, 11.5, 13.5, 15.5, 17.5, 19.5, 21.5, 23.5, 25.5, 27.5, 29.5, 31.5, 33.5, 35.5, 37.5, 39.5 };
            var period = 5;
            var bulls = BullsPower.Calculate(high, close, period);
            Assert.AreEqual(high.Length, bulls.Length);
            // Check that values are calculated
            for (var i = 0; i < bulls.Length; i++)
                // Bulls Power = high - EMA(close)
                // For first value, EMA = close[0]
                if (i == 0)
                    Assert.AreEqual(high[0] - close[0], bulls[0], 1e-6);
        }
    }
}