using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Trade.Tests
{
    [TestClass]
    public class GeneticIndividualMaximalFitnessTests
    {
        [TestMethod][TestCategory("Core")]
        public void CalculateMaximalFitness_Buffer100To200_ReturnsExpected()
        {
            // Arrange: buffer from 100 to 200, strictly increasing
            var buffer = Enumerable.Range(0, 100).Select(i => 200.0 - i)
                .Concat(Enumerable.Range(0, 101).Select(i => 100.0 + i))
                .ToArray();
            // Act
            var result = GeneticIndividual.CalculateMaximalFitness(buffer);
            // Assert: only one trade, buy at 100, sell at 200
            var expectedDollarGain = 200.0 - 100.0;
            var expectedPercentGain = (200.0 - 100.0) / 100.0 * 100.0;
            Assert.AreEqual(expectedDollarGain, result.DollarGain, 1e-6);
            Assert.AreEqual(expectedPercentGain, result.PercentGain, 1e-6);
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateMaximalFitness_Buffer200To100_ReturnsExpected()
        {
            // Arrange: buffer from 200 to 100, strictly decreasing
            var buffer = Enumerable.Range(0, 100).Select(i => 100.0 + i)
                .Concat(Enumerable.Range(0, 101).Select(i => 200.0 - i))
                .ToArray(); // Act
            var result = GeneticIndividual.CalculateMaximalFitness(buffer);
            Assert.AreEqual(100, result.DollarGain, 1e-6);
            Assert.AreEqual(50, result.PercentGain, 1e-6);
        }
    }
}