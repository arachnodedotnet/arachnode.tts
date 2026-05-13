using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Utils; // Added for PriceRange wrapper

namespace Trade.Tests
{
    [TestClass]
    public class GeneticIndividualSmaTests
    {
        // Helper to invoke internal CalculateIndicatorValue with PriceRange parameters
        private double InvokeIndicator(GeneticIndividual gi, IndicatorParams ind, double[] prices)
        {
            var method = typeof(GeneticIndividual).GetMethod("CalculateIndicatorValue", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "CalculateIndicatorValue not found");
            var pr = new PriceRange(prices);
            return (double)method.Invoke(gi, new object[] { ind, pr, pr, pr, pr, pr, pr, prices.Length, null });
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateSma_ReturnsCorrectAverage()
        {
            var ind = new IndicatorParams { Type = 1, Period = 3 }; // SMA
            var gi = new GeneticIndividual();
            double[] prices = { 1, 2, 3, 4, 5 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(4.0, result, 1e-6, "SMA of last 3 values should be 4.0");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateSma_WithDifferentPeriod_ReturnsCorrectAverage()
        {
            var ind = new IndicatorParams { Type = 1, Period = 2 }; // SMA period 2
            var gi = new GeneticIndividual();
            double[] prices = { 10, 20, 30, 40 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(35.0, result, 1e-6, "SMA of last 2 values should be 35.0");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateSma_WithSingleValue_ReturnsValue()
        {
            var ind = new IndicatorParams { Type = 1, Period = 1 }; // SMA period 1
            var gi = new GeneticIndividual();
            double[] prices = { 42.0 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(42.0, result, 1e-6, "SMA of single value should be the value itself");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateSma_WithLargePeriod_ReturnsFullAverage()
        {
            var ind = new IndicatorParams { Type = 1, Period = 5 }; // Full buffer
            var gi = new GeneticIndividual();
            double[] prices = { 10, 20, 30, 40, 50 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(30.0, result, 1e-6, "SMA of all values should be 30.0");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateSma_WithDecimalValues_HandlesCorrectly()
        {
            var ind = new IndicatorParams { Type = 1, Period = 3 }; // SMA
            var gi = new GeneticIndividual();
            double[] prices = { 100.25, 100.75, 101.50, 102.25, 103.00 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(102.25, result, 1e-10, "SMA should handle decimal values precisely");
        }
    }
}