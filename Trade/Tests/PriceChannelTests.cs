using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class PriceChannelTests
    {
        [TestMethod][TestCategory("Core")]
        public void Calculate_BasicLengthAndNoException()
        {
            var len = 100;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
            }

            var result = PriceChannel.Calculate(high, low);
            Assert.AreEqual(len, result.Upper.Length);
            Assert.AreEqual(len, result.Lower.Length);
            Assert.AreEqual(len, result.Median.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_FlatPrices_AllEqual()
        {
            var len = 30;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100.0;
                low[i] = 100.0;
            }

            var result = PriceChannel.Calculate(high, low, 10);
            for (var i = 10; i < len; i++)
            {
                Assert.AreEqual(100.0, result.Upper[i], 1e-8);
                Assert.AreEqual(100.0, result.Lower[i], 1e-8);
                Assert.AreEqual(100.0, result.Median[i], 1e-8);
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_Uptrend_UpperFollowsHigh()
        {
            var len = 30;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
            }

            var result = PriceChannel.Calculate(high, low, 5);
            for (var i = 5; i < len; i++)
            {
                Assert.AreEqual(high[i], result.Upper[i], 1e-8);
                Assert.AreEqual(low[i - 4], result.Lower[i], 1e-8);
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_Downtrend_LowerFollowsLow()
        {
            var len = 30;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 130 - i;
                low[i] = 120 - i;
            }

            var result = PriceChannel.Calculate(high, low, 5);
            for (var i = 5; i < len; i++)
            {
                Assert.AreEqual(high[i - 4], result.Upper[i], 1e-8);
                Assert.AreEqual(low[i], result.Lower[i], 1e-8);
            }
        }


        [TestMethod][TestCategory("Core")]
        public void Calculate_ManualCheck_KnownValues()
        {
            double[] high = { 10, 12, 14, 16, 18 };
            double[] low = { 5, 7, 9, 11, 13 };
            var result = PriceChannel.Calculate(high, low, 3);
            // At i=3, highest of [12,14,16]=16, lowest of [7,9,11]=7
            Assert.AreEqual(16, result.Upper[3], 1e-8);
            Assert.AreEqual(7, result.Lower[3], 1e-8);
            Assert.AreEqual((16 + 7) / 2.0, result.Median[3], 1e-8);
        }
    }
}