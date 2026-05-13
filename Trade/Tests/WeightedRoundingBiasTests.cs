using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Trade.Tests
{
    [TestClass]
    public class WeightedRoundingBiasTests
    {
        private GeneticIndividual _gi;

        [TestInitialize]
        public void Setup()
        {
            _gi = new GeneticIndividual
            {
                SignalCombination = SignalCombinationMethod.Weighted,
                IndicatorWeights = new double[] { 1.0, 1.0, 1.0 }
            };
        }

        [TestMethod][TestCategory("Core")]
        public void Weighted_AvoidsNegativeBias_WithSymmetricRounding()
        {
            // Case: weighted sum just below -0.5 should round to -1, not 0
            var deltas = new int[] { -1, 0, 0 }; // raw = -1.0 -> -1
            var result1 = _gi.AggregateDeltas(deltas);
            Assert.AreEqual(-1, result1, "Exact -1 should remain -1");

            // Simulate fractional scenario by adjusting weights
            _gi.IndicatorWeights = new double[] { -0.6, 0.0, 0.0 }; // sum = -0.6 -> should round to -1
            var result2 = _gi.AggregateDeltas(new int[] { 1, 0, 0 });
            Assert.AreEqual(-1, result2, "-0.6 should round away from zero to -1");

            _gi.IndicatorWeights = new double[] { -0.49, 0.0, 0.0 }; // sum = -0.49 -> should round to 0
            var result3 = _gi.AggregateDeltas(new int[] { 1, 0, 0 });
            Assert.AreEqual(0, result3, "-0.49 should round to 0");

            _gi.IndicatorWeights = new double[] { 0.49, 0.0, 0.0 }; // sum = +0.49 -> 0
            var result4 = _gi.AggregateDeltas(new int[] { 1, 0, 0 });
            Assert.AreEqual(0, result4, "+0.49 should round to 0");

            _gi.IndicatorWeights = new double[] { 0.51, 0.0, 0.0 }; // sum = +0.51 -> +1
            var result5 = _gi.AggregateDeltas(new int[] { 1, 0, 0 });
            Assert.AreEqual(1, result5, "+0.51 should round to +1");
        }

        [TestMethod][TestCategory("Core")]
        public void Weighted_RoundingMatchesMathRoundAwayFromZero()
        {
            double[] testValues = { -2.6, -2.5, -2.4, -1.6, -1.5, -1.4, -0.6, -0.5, -0.4, 0.4, 0.5, 0.6, 1.4, 1.5, 1.6 };

            foreach (var v in testValues)
            {
                // Encode value using a single weighted delta (delta=1 * weight=v)
                _gi.IndicatorWeights = new double[] { v };
                var result = _gi.AggregateDeltas(new int[] { 1 });
                var expected = (int)Math.Round(v, 0, MidpointRounding.AwayFromZero);
                Assert.AreEqual(expected, result, $"Expected rounding of {v} to be {expected}, got {result}");
            }
        }
    }
}
