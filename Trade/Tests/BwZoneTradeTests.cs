using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class BwZoneTradeTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ReturnsExpectedLengthAndColors()
        {
            double[] open = { 1, 2, 3, 4, 5 };
            double[] high = { 2, 3, 4, 5, 6 };
            double[] low = { 0, 1, 2, 3, 4 };
            double[] close = { 1.5, 2.5, 3.5, 4.5, 5.5 };
            double[] ac = { 0, 1, 2, 1, 0 };
            double[] ao = { 0, 1, 2, 1, 0 };
            var colors = BwZoneTrade.Calculate(open, high, low, close, ac, ao);
            Assert.AreEqual(open.Length, colors.Length);
            // First candle is always Gray
            Assert.AreEqual(2.0, colors[0], 1e-6);
            // Second candle should be Green (ac[1]>ac[0] && ao[1]>ao[0])
            Assert.AreEqual(0.0, colors[1], 1e-6);
            // Fourth candle should be Red (ac[3]<ac[2] && ao[3]<ao[2])
            Assert.AreEqual(1.0, colors[3], 1e-6);
        }
    }
}