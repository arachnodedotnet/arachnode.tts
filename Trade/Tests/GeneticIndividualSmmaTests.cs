using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Utils; // Added for PriceRange

namespace Trade.Tests
{
    [TestClass]
    public class GeneticIndividualSmmaTests
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
        public void CalculateSmma_ReturnsExpectedValue()
        {
            var ind = new IndicatorParams { Type = 3, Period = 3 }; // SMMA
            var gi = new GeneticIndividual();
            double[] prices = { 1, 2, 3, 4, 5 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(4.333333333333333, result, 1e-10, "SMMA should be 4.333333...");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateSmma_WithDifferentPeriod_ReturnsExpectedValue()
        {
            var ind = new IndicatorParams { Type = 3, Period = 2 }; // SMMA period 2
            var gi = new GeneticIndividual();
            double[] prices = { 10, 20, 30, 40 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(37.5, result, 1e-10, "SMMA with period 2 should be 37.5");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateSmma_EdgeCase_SingleValue()
        {
            var ind = new IndicatorParams { Type = 3, Period = 1 }; // SMMA period 1
            var gi = new GeneticIndividual();
            double[] prices = { 42.0 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(42.0, result, 1e-10, "SMMA with period 1 should return the single value");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateSmma_DetailedStepByStep_VerifyAlgorithm()
        {
            var ind = new IndicatorParams { Type = 3, Period = 3 }; // SMMA period 3
            var gi = new GeneticIndividual();
            double[] prices = { 100, 200, 300, 400 };

            var result = InvokeIndicator(gi, ind, prices);

            Assert.AreEqual(333.33333333333331, result, 1e-10, "SMMA detailed calculation should be 333.333...");
        }
    }
}