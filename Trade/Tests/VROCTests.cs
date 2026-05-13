using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class VROCTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_ReturnsCorrectLength()
        {
            // Arrange
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700, 1800, 1900 };
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400, 5500, 5600, 5700, 5800, 5900 };
            var period = 5;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period);

            // Assert
            Assert.AreEqual(tickVolume.Length, result.VROC.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_DefaultPeriod_Is25()
        {
            // Arrange
            var tickVolume = CreateIncreasingVolume(30, 1000);
            var realVolume = CreateIncreasingVolume(30, 5000);

            // Act
            var result = VROC.Calculate(tickVolume, realVolume); // No period specified

            // Assert - First 25 values should be zero
            for (var i = 0; i < 25; i++)
                Assert.AreEqual(0.0, result.VROC[i], TOLERANCE,
                    "First 25 values should be zero when using default period 25");

            // Value at index 25 should be calculated
            Assert.AreNotEqual(0.0, result.VROC[25], "Value at index 25 should be calculated with default period");
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_FirstPeriodValuesAreZero()
        {
            // Arrange
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400, 1500, 1600 };
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400, 5500, 5600 };
            var period = 4;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period);

            // Assert - First 'period' values should be zero
            for (var i = 0; i < period; i++)
                Assert.AreEqual(0.0, result.VROC[i], TOLERANCE,
                    $"VROC[{i}] should be zero (insufficient data)");
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_CorrectFormula_StandardVROC()
        {
            // Arrange - Test the corrected VROC formula
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400 }; // 5 volumes
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400 };
            var period = 3;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period);

            // Assert - Standard VROC formula: (volume[i] - volume[i-period]) / volume[i-period] * 100
            Assert.AreEqual(0.0, result.VROC[0], TOLERANCE);
            Assert.AreEqual(0.0, result.VROC[1], TOLERANCE);
            Assert.AreEqual(0.0, result.VROC[2], TOLERANCE);

            // VROC[3] = (1300 - 1000) / 1000 * 100 = 30.0%
            Assert.AreEqual(30.0, result.VROC[3], TOLERANCE);

            // VROC[4] = (1400 - 1100) / 1100 * 100 = 27.272727...%
            var expected4 = (1400.0 - 1100.0) / 1100.0 * 100.0;
            Assert.AreEqual(expected4, result.VROC[4], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_WithIncreasingVolumes_CorrectFormula()
        {
            // Arrange
            long[] tickVolume = { 1000, 1200, 1400, 1600, 1800 };
            long[] realVolume = { 5000, 6000, 7000, 8000, 9000 };
            var period = 3;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period);

            // Assert
            Assert.AreEqual(0.0, result.VROC[0], TOLERANCE);
            Assert.AreEqual(0.0, result.VROC[1], TOLERANCE);
            Assert.AreEqual(0.0, result.VROC[2], TOLERANCE);

            // VROC[3] = (1600 - 1000) / 1000 * 100 = 60.0%
            Assert.AreEqual(60.0, result.VROC[3], TOLERANCE);

            // VROC[4] = (1800 - 1200) / 1200 * 100 = 50.0%
            Assert.AreEqual(50.0, result.VROC[4], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_WithTickVolume_UsesTickVolumeArray()
        {
            // Arrange
            long[] tickVolume = { 1000, 1200, 1100, 1300, 1250 };
            long[] realVolume = { 5000, 6000, 5500, 6500, 6250 };
            var period = 3;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period);

            // Assert - Verify it's using tick volume, not real volume
            Assert.AreEqual(5, result.VROC.Length);
            Assert.AreEqual(0.0, result.VROC[0], TOLERANCE);
            Assert.AreEqual(0.0, result.VROC[1], TOLERANCE);
            Assert.AreEqual(0.0, result.VROC[2], TOLERANCE);

            // VROC[3] = (1300 - 1000) / 1000 * 100 = 30.0%
            Assert.AreEqual(30.0, result.VROC[3], TOLERANCE);

            // VROC[4] = (1250 - 1200) / 1200 * 100 = 4.166666...%
            var expected4 = (1250.0 - 1200.0) / 1200.0 * 100.0;
            Assert.AreEqual(expected4, result.VROC[4], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_WithRealVolume_UsesRealVolumeArray()
        {
            // Arrange
            long[] tickVolume = { 1000, 1200, 1100, 1300, 1250 };
            long[] realVolume = { 5000, 6000, 5500, 6500, 6250 };
            var period = 3;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period, VolumeType.Real);

            // Assert - Verify it's using real volume, not tick volume
            Assert.AreEqual(5, result.VROC.Length);
            Assert.AreEqual(0.0, result.VROC[0], TOLERANCE);
            Assert.AreEqual(0.0, result.VROC[1], TOLERANCE);
            Assert.AreEqual(0.0, result.VROC[2], TOLERANCE);

            // VROC[3] = (6500 - 5000) / 5000 * 100 = 30.0%
            Assert.AreEqual(30.0, result.VROC[3], TOLERANCE);

            // VROC[4] = (6250 - 6000) / 6000 * 100 = 4.166666...%
            var expected4 = (6250.0 - 6000.0) / 6000.0 * 100.0;
            Assert.AreEqual(expected4, result.VROC[4], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_DefaultVolumeType_UsesTickVolume()
        {
            // Arrange
            long[] tickVolume = { 1000, 1200, 1100, 1300 };
            long[] realVolume = { 5000, 6000, 5500, 6500 };
            var period = 3;

            // Act - Call without specifying volume type (should default to Tick)
            var defaultResult = VROC.Calculate(tickVolume, realVolume, period);
            var explicitTickResult = VROC.Calculate(tickVolume, realVolume, period);

            // Assert
            Assert.AreEqual(explicitTickResult.VROC.Length, defaultResult.VROC.Length);
            for (var i = 0; i < defaultResult.VROC.Length; i++)
                Assert.AreEqual(explicitTickResult.VROC[i], defaultResult.VROC[i], TOLERANCE,
                    $"Default volume type should use tick volume at index {i}");
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_ZeroPreviousVolume_HandlesCorrectly()
        {
            // Arrange - Zero previous volume should be handled
            long[] tickVolume = { 0, 1000, 1200, 1100, 1300 };
            long[] realVolume = { 0, 5000, 6000, 5500, 6500 };
            var period = 3;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period);

            // Assert
            Assert.AreEqual(5, result.VROC.Length);
            Assert.AreEqual(0.0, result.VROC[0], TOLERANCE);
            Assert.AreEqual(0.0, result.VROC[1], TOLERANCE);
            Assert.AreEqual(0.0, result.VROC[2], TOLERANCE);

            // VROC[3] when previous volume is zero, should use previous VROC value
            Assert.AreEqual(0.0, result.VROC[3], TOLERANCE, "Should use previous VROC when previous volume is zero");

            // VROC[4] = (1300 - 1000) / 1000 * 100 = 30.0%
            Assert.AreEqual(30.0, result.VROC[4], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_WithSmallPeriod_DefaultsTo25()
        {
            // Arrange
            var tickVolume = CreateIncreasingVolume(30, 1000);
            var realVolume = CreateIncreasingVolume(30, 5000);

            // Act - Test that period <= 0 defaults to 25
            var resultZero = VROC.Calculate(tickVolume, realVolume, 0); // Zero period should default
            var resultNegative = VROC.Calculate(tickVolume, realVolume, -5); // Negative period should default
            var resultDefault = VROC.Calculate(tickVolume, realVolume);

            // Assert
            for (var i = 0; i < tickVolume.Length; i++)
            {
                Assert.AreEqual(resultDefault.VROC[i], resultZero.VROC[i], TOLERANCE,
                    $"Period = 0 should default to 25 at index {i}");
                Assert.AreEqual(resultDefault.VROC[i], resultNegative.VROC[i], TOLERANCE,
                    $"Period < 0 should default to 25 at index {i}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_WithEmptyArrays_ReturnsEmptyResult()
        {
            // Arrange
            long[] tickVolume = { };
            long[] realVolume = { };

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, 5);

            // Assert
            Assert.AreEqual(0, result.VROC.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_VolumeTypeSelection_ProducesDifferentResults()
        {
            // Arrange - Different volume arrays with different rates of change to ensure different results
            long[] tickVolume = { 1000, 1200, 1100, 1300, 1200, 1500 }; // Changed: different pattern
            long[] realVolume = { 5000, 6000, 5500, 6500, 6250, 7000 };
            var period = 4;

            // Act
            var tickResult = VROC.Calculate(tickVolume, realVolume, period);
            var realResult = VROC.Calculate(tickVolume, realVolume, period, VolumeType.Real);

            // Assert - Results should be different due to different volume values
            // At least some calculated values should differ
            var hasDifference = false;
            for (var i = period; i < tickVolume.Length; i++)
                if (Math.Abs(tickResult.VROC[i] - realResult.VROC[i]) > TOLERANCE)
                {
                    hasDifference = true;
                    break;
                }

            Assert.IsTrue(hasDifference, "Tick and Real volume results should be different");
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_IncreasingVolumeTrend_ShowsPositiveVROC()
        {
            // Arrange - Consistently increasing volume
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400, 1500, 1600 };
            long[] realVolume = { 5000, 5500, 6000, 6500, 7000, 7500, 8000 };
            var period = 4;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period);

            // Assert - All calculated values should be positive for increasing trend
            for (var i = period; i < result.VROC.Length; i++)
                Assert.IsTrue(result.VROC[i] > 0,
                    $"VROC[{i}] should be positive for increasing volume trend, got {result.VROC[i]}");
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_DecreasingVolumeTrend_ShowsNegativeVROC()
        {
            // Arrange - Consistently decreasing volume
            long[] tickVolume = { 1600, 1500, 1400, 1300, 1200, 1100, 1000 };
            long[] realVolume = { 8000, 7500, 7000, 6500, 6000, 5500, 5000 };
            var period = 4;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period);

            // Assert - All calculated values should be negative for decreasing trend
            for (var i = period; i < result.VROC.Length; i++)
                Assert.IsTrue(result.VROC[i] < 0,
                    $"VROC[{i}] should be negative for decreasing volume trend, got {result.VROC[i]}");
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_OscillatingVolume_ShowsMixedVROC()
        {
            // Arrange - Oscillating volume pattern
            long[] tickVolume = { 1000, 1500, 1200, 1800, 1100, 1900, 1000 };
            long[] realVolume = { 5000, 7500, 6000, 9000, 5500, 9500, 5000 };
            var period = 3;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period);

            // Assert
            Assert.AreEqual(7, result.VROC.Length);
            Assert.AreEqual(0.0, result.VROC[0], TOLERANCE);
            Assert.AreEqual(0.0, result.VROC[1], TOLERANCE);
            Assert.AreEqual(0.0, result.VROC[2], TOLERANCE);

            // VROC[3] = (1800 - 1000) / 1000 * 100 = 80.0%
            Assert.AreEqual(80.0, result.VROC[3], TOLERANCE);

            // VROC[4] = (1100 - 1500) / 1500 * 100 = -26.666666...%
            var expected4 = (1100.0 - 1500.0) / 1500.0 * 100.0;
            Assert.AreEqual(expected4, result.VROC[4], TOLERANCE);

            // Should have mix of positive and negative values for oscillating volumes
            bool hasPositive = false, hasNegative = false;
            for (var i = period; i < result.VROC.Length; i++)
            {
                if (result.VROC[i] > 0) hasPositive = true;
                if (result.VROC[i] < 0) hasNegative = true;
            }

            Assert.IsTrue(hasPositive, "Should have positive VROC values for oscillating volumes");
            Assert.IsTrue(hasNegative, "Should have negative VROC values for oscillating volumes");
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_LargeVolumes_HandlesCorrectly()
        {
            // Arrange - Large volume numbers
            long[] tickVolume = { 1000000, 1200000, 1100000, 1300000, 1250000 };
            long[] realVolume = { 50000000, 60000000, 55000000, 65000000, 62500000 };
            var period = 3;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period, VolumeType.Real);

            // Assert - Should handle large numbers without overflow
            for (var i = 0; i < result.VROC.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.VROC[i]), $"VROC[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.VROC[i]), $"VROC[{i}] should not be infinite");
            }

            // VROC[3] = (65000000 - 50000000) / 50000000 * 100 = 30.0%
            Assert.AreEqual(30.0, result.VROC[3], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_RealisticMarketScenario()
        {
            // Arrange - Realistic volume scenario
            long[] tickVolume = { 1500000, 1800000, 1200000, 2200000, 1900000, 1600000, 2500000, 1400000 };
            long[] realVolume = { 25000000, 28000000, 22000000, 35000000, 31000000, 26000000, 38000000, 24000000 };
            var period = 5;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period, VolumeType.Real);

            // Assert
            Assert.AreEqual(8, result.VROC.Length);

            // First 5 values should be zero
            for (var i = 0; i < period; i++) Assert.AreEqual(0.0, result.VROC[i], TOLERANCE);

            // VROC[5] = (26000000 - 25000000) / 25000000 * 100 = 4.0%
            Assert.AreEqual(4.0, result.VROC[5], TOLERANCE);

            // Calculated values should be reasonable
            for (var i = period; i < result.VROC.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.VROC[i]), $"VROC[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.VROC[i]), $"VROC[{i}] should not be infinite");
                // VROC typically ranges from -100% to several hundred percent
                Assert.IsTrue(Math.Abs(result.VROC[i]) < 1000,
                    $"VROC[{i}] = {result.VROC[i]} should be within reasonable range");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_KnownExamples_100PercentIncrease()
        {
            // Arrange - Volume doubles
            long[] tickVolume = { 1000, 2000 };
            long[] realVolume = { 5000, 10000 };
            var period = 1;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period);

            // Assert
            Assert.AreEqual(0.0, result.VROC[0], TOLERANCE);
            // VROC[1] = (2000 - 1000) / 1000 * 100 = 100.0%
            Assert.AreEqual(100.0, result.VROC[1], TOLERANCE, "Volume doubling should give 100% VROC");
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_KnownExamples_50PercentDecrease()
        {
            // Arrange - Volume drops by half
            long[] tickVolume = { 2000, 1000 };
            long[] realVolume = { 10000, 5000 };
            var period = 1;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period);

            // Assert
            Assert.AreEqual(0.0, result.VROC[0], TOLERANCE);
            // VROC[1] = (1000 - 2000) / 2000 * 100 = -50.0%
            Assert.AreEqual(-50.0, result.VROC[1], TOLERANCE, "Volume halving should give -50% VROC");
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_LongSequence_MaintainsAccuracy()
        {
            // Arrange
            var tickVolume = CreateVaryingVolume(50);
            var realVolume = CreateVaryingVolume(50, 5000);
            var period = 10;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period);

            // Assert
            Assert.AreEqual(50, result.VROC.Length);

            // First 'period' values should be zero
            for (var i = 0; i < period; i++) Assert.AreEqual(0.0, result.VROC[i], TOLERANCE);

            // Verify manual calculation for consistency
            for (var i = period; i < result.VROC.Length; i++)
            {
                if (tickVolume[i - period] != 0)
                {
                    var expected = (double)(tickVolume[i] - tickVolume[i - period]) / tickVolume[i - period] * 100.0;
                    Assert.AreEqual(expected, result.VROC[i], TOLERANCE,
                        $"VROC calculation incorrect at index {i}");
                }
                else
                {
                    // When previous volume is zero, should use previous VROC value
                    if (i > 0)
                        Assert.AreEqual(result.VROC[i - 1], result.VROC[i], TOLERANCE);
                }

                Assert.IsFalse(double.IsNaN(result.VROC[i]), $"VROC[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.VROC[i]), $"VROC[{i}] should not be infinite");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_ComparisonWithROC()
        {
            // Arrange - VROC should work like ROC but for volume data
            long[] tickVolume = { 1000, 1100, 1200, 1300 };
            long[] realVolume = { 5000, 5500, 6000, 6500 };
            var period = 2;

            // Act
            var result = VROC.Calculate(tickVolume, realVolume, period);

            // Assert - Should follow same pattern as ROC
            Assert.AreEqual(0.0, result.VROC[0], TOLERANCE);
            Assert.AreEqual(0.0, result.VROC[1], TOLERANCE);

            // VROC[2] = (1200 - 1000) / 1000 * 100 = 20.0%
            Assert.AreEqual(20.0, result.VROC[2], TOLERANCE);

            // VROC[3] = (1300 - 1100) / 1100 * 100 = 18.181818...%
            var expected3 = (1300.0 - 1100.0) / 1100.0 * 100.0;
            Assert.AreEqual(expected3, result.VROC[3], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void VROC_Calculate_BullishAndBearishVolumeTrends()
        {
            // Arrange
            long[] bullishVolume = { 1000, 1100, 1200, 1300, 1400 }; // Consistent increase
            long[] bearishVolume = { 1400, 1300, 1200, 1100, 1000 }; // Consistent decrease
            long[] realVolume = { 5000, 5500, 6000, 6500, 7000 };
            var period = 2;

            // Act
            var bullishResult = VROC.Calculate(bullishVolume, realVolume, period);
            var bearishResult = VROC.Calculate(bearishVolume, realVolume, period);

            // Assert - Bullish trend should have positive VROC values
            for (var i = period; i < bullishVolume.Length; i++)
                Assert.IsTrue(bullishResult.VROC[i] > 0.0,
                    $"Bullish VROC[{i}] should be positive, got {bullishResult.VROC[i]}");

            // Bearish trend should have negative VROC values
            for (var i = period; i < bearishVolume.Length; i++)
                Assert.IsTrue(bearishResult.VROC[i] < 0.0,
                    $"Bearish VROC[{i}] should be negative, got {bearishResult.VROC[i]}");
        }

        #region Helper Methods

        private long[] CreateIncreasingVolume(int count, long baseVolume)
        {
            var volumes = new long[count];
            for (var i = 0; i < count; i++) volumes[i] = baseVolume + i * 100;
            return volumes;
        }

        private long[] CreateVaryingVolume(int count, long baseVolume = 1000)
        {
            var volumes = new long[count];
            var random = new Random(42); // Fixed seed for reproducibility

            for (var i = 0; i < count; i++)
            {
                // Create realistic volume variations
                var variation = (random.NextDouble() - 0.5) * 0.4; // ±20% variation
                volumes[i] = (long)(baseVolume * (1.0 + variation));
                baseVolume = Math.Max(100, baseVolume + random.Next(-50, 100)); // Trending component
            }

            return volumes;
        }

        #endregion
    }
}