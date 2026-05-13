using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class BollingerBandsTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ReturnsExpectedLengthAndInitialValues()
        {
            double[] price = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
            var period = 5;
            var deviations = 2.0;
            var shift = 0;
            var (middle, upper, lower) = BollingerBands.Calculate(price, period, deviations, shift);
            Assert.AreEqual(price.Length, middle.Length);
            Assert.AreEqual(price.Length, upper.Length);
            Assert.AreEqual(price.Length, lower.Length);
            // First period-1 values should be zero
            for (var i = 0; i < period - 1; i++)
            {
                Assert.AreEqual(0.0, middle[i], 1e-6);
                Assert.AreEqual(0.0, upper[i], 1e-6);
                Assert.AreEqual(0.0, lower[i], 1e-6);
            }

            // Middle[period-1] should be average of first 'period' prices
            var expectedMiddle = price.Skip(0).Take(period).Average();
            Assert.AreEqual(expectedMiddle, middle[period - 1], 1e-6);
        }
    }
}