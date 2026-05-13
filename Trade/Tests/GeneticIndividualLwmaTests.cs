using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Utils; // Added for PriceRange wrapper

namespace Trade.Tests
{
    [TestClass]
    public class GeneticIndividualLwmaTests
    {
        // Helper to invoke internal CalculateIndicatorValue with PriceRange (reflection can't use implicit conversions)
        private double InvokeIndicator(GeneticIndividual gi, IndicatorParams ind, double[] prices)
        {
            var method = typeof(GeneticIndividual).GetMethod("CalculateIndicatorValue", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "CalculateIndicatorValue not found");
            var pr = new PriceRange(prices);
            return (double)method.Invoke(gi, new object[] { ind, pr, pr, pr, pr, pr, pr, prices.Length, null });
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateLwma_ReturnsExpectedValue()
        {
            var ind = new IndicatorParams { Type = 4, Period = 3 }; // LWMA
            var gi = new GeneticIndividual();
            double[] prices = { 1, 2, 3, 4, 5 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(4.333333333333333, result, 1e-10, "LWMA should be 4.333333...");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateLwma_WithDifferentPeriod_ReturnsExpectedValue()
        {
            var ind = new IndicatorParams { Type = 4, Period = 2 }; // LWMA period 2
            var gi = new GeneticIndividual();
            double[] prices = { 10, 20, 30, 40 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(36.666666666666664, result, 1e-10, "LWMA with period 2 should be 36.666...");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateLwma_WithSingleValue_ReturnsValue()
        {
            var ind = new IndicatorParams { Type = 4, Period = 1 }; // LWMA period 1
            var gi = new GeneticIndividual();
            double[] prices = { 42.0 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(42.0, result, 1e-10, "LWMA with single value should be the value itself");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateLwma_DetailedCalculation_VerifyWeights()
        {
            var ind = new IndicatorParams { Type = 4, Period = 4 }; // LWMA period 4
            var gi = new GeneticIndividual();
            double[] prices = { 100, 200, 300, 400 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(300.0, result, 1e-10, "LWMA detailed calculation should be 300.0");
        }
    }
}