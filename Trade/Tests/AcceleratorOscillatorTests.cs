using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class AcceleratorOscillatorTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ReturnsExpectedAcValues()
        {
            // Use more realistic price data that has some volatility
            // Instead of perfectly linear data, use data with variations
            double[] high =
            {
                100.0, 101.5, 103.2, 102.8, 105.1, 106.7, 104.9, 108.2, 107.5, 110.3,
                112.1, 109.8, 113.5, 115.2, 114.6, 117.8, 116.4, 119.9, 121.3, 118.7,
                122.5, 124.8, 123.1, 126.4, 128.9, 127.2, 130.7, 132.1, 129.8, 133.5,
                135.9, 134.2, 137.8, 139.4, 136.9, 140.2, 142.7, 141.1, 144.8, 146.3,
                143.9, 147.2, 149.1, 147.5, 150.8
            };
            double[] low =
            {
                98.5, 99.2, 101.1, 100.3, 103.4, 104.8, 102.7, 106.1, 105.2, 108.1,
                109.8, 107.5, 111.2, 112.9, 112.1, 115.3, 114.2, 117.6, 118.9, 116.4,
                120.1, 122.4, 120.8, 124.1, 126.5, 124.9, 128.3, 129.7, 127.4, 131.1,
                133.4, 131.8, 135.4, 136.9, 134.5, 137.8, 140.2, 138.7, 142.3, 143.8,
                141.5, 144.8, 146.6, 145.1, 148.3
            };

            var ac = AcceleratorOscillator.Calculate(high, low);

            // AC calculation requires SLOW_PERIOD (34) + FAST_PERIOD (5) - 2 = 37 initialization values
            const int EXPECTED_ZERO_COUNT = 37;

            // Defensive check: make sure we have enough data to test
            Assert.AreEqual(high.Length, ac.Length, "Output length should match input length");
            Assert.IsTrue(ac.Length >= EXPECTED_ZERO_COUNT,
                "Test data must have at least 37 elements to validate zero period");

            // First 37 values should be zero (initialization period)
            for (var i = 0; i < EXPECTED_ZERO_COUNT; i++)
                Assert.AreEqual(0.0, ac[i], 1e-6, $"AC[{i}] should be zero during initialization period");

            // Check that later values are not all zero
            var foundNonZero = false;
            for (var i = EXPECTED_ZERO_COUNT; i < ac.Length; i++)
                if (Math.Abs(ac[i]) > 1e-6)
                {
                    foundNonZero = true;
                    break;
                }

            Assert.IsTrue(foundNonZero, "Should have non-zero values after initialization period");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_HandlesInsufficientData()
        {
            // Test with insufficient data (less than SLOW_PERIOD = 34)
            double[] high = { 10, 12, 14, 16, 18 };
            double[] low = { 9, 11, 13, 15, 17 };
            var ac = AcceleratorOscillator.Calculate(high, low);
            // Should return empty array for insufficient data
            Assert.AreEqual(0, ac.Length, "Should return empty array when data length < SLOW_PERIOD (34)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ValidatesParameterConsistency()
        {
            // Test that the periods used are consistent with the algorithm
            const int FAST_PERIOD = 5;
            const int SLOW_PERIOD = 34;

            // Create data with more points to ensure we have enough non-zero AC values
            // Need at least SLOW_PERIOD + FAST_PERIOD + extra points for meaningful testing
            var dataLength = SLOW_PERIOD + FAST_PERIOD + 15; // 54 points total
            var high = new double[dataLength];
            var low = new double[dataLength];

            // Create data with significant momentum changes to ensure non-zero AC values
            // Use alternating trends and volatility spikes
            for (var i = 0; i < dataLength; i++)
            {
                double basePrice = 100;

                // Create phases: trending up, sideways, trending down, volatile
                if (i < 15)
                    // Strong uptrend
                    basePrice = 100 + i * 2.0;
                else if (i < 25)
                    // Sideways movement
                    basePrice = 130 + Math.Sin(i * 0.5) * 3.0;
                else if (i < 40)
                    // Downtrend
                    basePrice = 133 - (i - 25) * 1.5;
                else
                    // High volatility period with rapid changes
                    basePrice = 110 + Math.Sin(i * 0.8) * 8.0 + Math.Cos(i * 1.3) * 5.0;

                // Add some random variation
                var rand = new Random(i + 123); // Different seed for each point
                var variation = (rand.NextDouble() - 0.5) * 4.0;

                high[i] = basePrice + Math.Abs(variation) + 2.0;
                low[i] = basePrice - Math.Abs(variation) - 2.0;
            }

            var ac = AcceleratorOscillator.Calculate(high, low);

            // Should have same length as input
            Assert.AreEqual(dataLength, ac.Length);

            // First (SLOW_PERIOD-1 + FAST_PERIOD-1) values should be zero
            var zeroValueCount = SLOW_PERIOD - 1 + FAST_PERIOD - 1; // 33 + 4 = 37
            for (var i = 0; i < zeroValueCount; i++)
                Assert.AreEqual(0.0, ac[i], 1e-6, $"AC[{i}] should be zero during initialization period");

            // At least one value after the initialization period should be non-zero
            var hasNonZeroValue = false;
            for (var i = zeroValueCount; i < ac.Length; i++)
                if (Math.Abs(ac[i]) > 1e-6)
                {
                    hasNonZeroValue = true;
                    break;
                }

            Assert.IsTrue(hasNonZeroValue, "Should have at least one non-zero AC value after initialization period");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ProducesReasonableValues()
        {
            // Additional test to verify the calculation produces expected behavior
            // Use a sine wave pattern to create clear momentum changes
            const int dataLength = 60;
            var high = new double[dataLength];
            var low = new double[dataLength];

            for (var i = 0; i < dataLength; i++)
            {
                var angle = i * Math.PI / 10; // Creates oscillation
                var basePrice = 100 + 20 * Math.Sin(angle);
                high[i] = basePrice + 2;
                low[i] = basePrice - 2;
            }

            var ac = AcceleratorOscillator.Calculate(high, low);

            Assert.AreEqual(dataLength, ac.Length);

            // Should have zeros for initialization period
            for (var i = 0; i < 37; i++) Assert.AreEqual(0.0, ac[i], 1e-6);

            // Should have some variation in the latter part
            double maxAbs = 0;
            for (var i = 37; i < ac.Length; i++) maxAbs = Math.Max(maxAbs, Math.Abs(ac[i]));
            Assert.IsTrue(maxAbs > 1e-6, "Should have meaningful AC values with sine wave data");
        }
    }
}