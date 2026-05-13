using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Trade.Tests
{
    [TestClass]
    public class FitnessTests2
    {
        [TestMethod][TestCategory("Core")]
        public void Constructor_WithParameters_SetsPropertiesCorrectly()
        {
            var fitness = new Fitness(100.5, 12.3, 99.9);

            Assert.AreEqual(100.5, fitness.DollarGain);
            Assert.AreEqual(12.3, fitness.PercentGain);
            Assert.AreEqual(99.9, fitness.FitnessScore);
        }

        [TestMethod][TestCategory("Core")]
        public void Constructor_WithoutParameters_InitializesPropertiesToDefaults()
        {
            var fitness = new Fitness();

            Assert.AreEqual(0, fitness.DollarGain);
            Assert.AreEqual(0, fitness.PercentGain);
            Assert.IsNull(fitness.FitnessScore);
        }

        [TestMethod][TestCategory("Core")]
        public void Properties_SetAndGet_WorkCorrectly()
        {
            var fitness = new Fitness();

            fitness.DollarGain = 200.0;
            fitness.PercentGain = 15.5;
            fitness.FitnessScore = 88.8;

            Assert.AreEqual(200.0, fitness.DollarGain);
            Assert.AreEqual(15.5, fitness.PercentGain);
            Assert.AreEqual(88.8, fitness.FitnessScore);
        }

        [TestMethod][TestCategory("Core")]
        public void FitnessScore_CanBeSetToNull()
        {
            var fitness = new Fitness();
            fitness.FitnessScore = null;

            Assert.IsNull(fitness.FitnessScore);
        }
    }
}