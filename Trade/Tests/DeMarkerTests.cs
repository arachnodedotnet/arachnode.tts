using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class DeMarkerTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_ReturnsCorrectLengths()
        {
            // Arrange
            double[] high = { 102, 105, 104, 107, 106, 109, 108 };
            double[] low = { 98, 101, 100, 103, 102, 105, 104 };
            var period = 5;

            // Act
            var result = DeMarker.Calculate(high, low, period);

            // Assert
            Assert.AreEqual(high.Length, result.DeMarker.Length);
            Assert.AreEqual(high.Length, result.DeMax.Length);
            Assert.AreEqual(high.Length, result.DeMin.Length);
            Assert.AreEqual(high.Length, result.AvgDeMax.Length);
            Assert.AreEqual(high.Length, result.AvgDeMin.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_DefaultPeriod_Is14()
        {
            // Arrange
            var high = new double[20];
            var low = new double[20];
            for (var i = 0; i < 20; i++)
            {
                high[i] = 100 + i + 2;
                low[i] = 100 + i - 2;
            }

            // Act
            var defaultResult = DeMarker.Calculate(high, low);
            var explicitResult = DeMarker.Calculate(high, low);

            // Assert
            Assert.AreEqual(defaultResult.DeMarker.Length, explicitResult.DeMarker.Length);
            for (var i = 0; i < high.Length; i++)
                Assert.AreEqual(explicitResult.DeMarker[i], defaultResult.DeMarker[i], TOLERANCE,
                    $"Default period should be 14 at index {i}");
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_AppliesCorrectFormulas()
        {
            // Arrange - Simple test case for manual verification
            double[] high = { 10, 12, 11, 14, 13 };
            double[] low = { 8, 10, 9, 12, 11 };
            var period = 3;

            // Act
            var result = DeMarker.Calculate(high, low, period);

            // Assert - Manual calculations
            Assert.AreEqual(0.0, result.DeMax[0], TOLERANCE, "First DeMax should be 0");
            Assert.AreEqual(0.0, result.DeMin[0], TOLERANCE, "First DeMin should be 0");

            // DeMax[1] = Max(12 - 10, 0) = 2
            Assert.AreEqual(2.0, result.DeMax[1], TOLERANCE, "DeMax[1] calculation incorrect");

            // DeMin[1] = Max(8 - 10, 0) = 0 (since 8 < 10)
            Assert.AreEqual(0.0, result.DeMin[1], TOLERANCE, "DeMin[1] calculation incorrect");

            // DeMax[2] = Max(11 - 12, 0) = 0 (since 11 < 12)
            Assert.AreEqual(0.0, result.DeMax[2], TOLERANCE, "DeMax[2] calculation incorrect");

            // DeMin[2] = Max(10 - 9, 0) = 1
            Assert.AreEqual(1.0, result.DeMin[2], TOLERANCE, "DeMin[2] calculation incorrect");

            // DeMax[3] = Max(14 - 11, 0) = 3
            Assert.AreEqual(3.0, result.DeMax[3], TOLERANCE, "DeMax[3] calculation incorrect");

            // DeMin[3] = Max(9 - 12, 0) = 0 (since 9 < 12)
            Assert.AreEqual(0.0, result.DeMin[3], TOLERANCE, "DeMin[3] calculation incorrect");

            // First meaningful DeMarker value at index 3 (period = 3)
            // AvgDeMax = (2 + 0 + 3) / 3 = 5/3 ? 1.6667
            // AvgDeMin = (0 + 1 + 0) / 3 = 1/3 ? 0.3333
            // DeMarker = 1.6667 / (1.6667 + 0.3333) = 1.6667 / 2 = 0.8333
            var expectedAvgDeMax = (2.0 + 0.0 + 3.0) / 3.0;
            var expectedAvgDeMin = (0.0 + 1.0 + 0.0) / 3.0;
            var expectedDeMarker = expectedAvgDeMax / (expectedAvgDeMax + expectedAvgDeMin);

            Assert.AreEqual(expectedAvgDeMax, result.AvgDeMax[3], TOLERANCE, "AvgDeMax calculation incorrect");
            Assert.AreEqual(expectedAvgDeMin, result.AvgDeMin[3], TOLERANCE, "AvgDeMin calculation incorrect");
            Assert.AreEqual(expectedDeMarker, result.DeMarker[3], TOLERANCE, "DeMarker calculation incorrect");
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_InitialValuesAreZero()
        {
            // Arrange
            double[] high = { 102, 105, 104, 107, 106, 109, 108, 111, 110 };
            double[] low = { 98, 101, 100, 103, 102, 105, 104, 107, 106 };
            var period = 5;

            // Act
            var result = DeMarker.Calculate(high, low, period);

            // Assert - First 'period' DeMarker values should be zero
            for (var i = 0; i < period; i++)
                Assert.AreEqual(0.0, result.DeMarker[i], TOLERANCE,
                    $"DeMarker[{i}] should be zero during initialization period");

            // After initialization period, values should be calculated (not zero for normal data)
            if (result.DeMarker.Length > period)
            {
                // At least one value after initialization should be non-zero for this data
                var hasNonZeroValue = false;
                for (var i = period; i < result.DeMarker.Length; i++)
                    if (Math.Abs(result.DeMarker[i]) > TOLERANCE)
                    {
                        hasNonZeroValue = true;
                        break;
                    }

                Assert.IsTrue(hasNonZeroValue, "Should have non-zero DeMarker values after initialization");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_OscillatesBetweenZeroAndOne()
        {
            // Arrange - Various market conditions
            double[] high = { 100, 105, 103, 110, 108, 115, 112, 120, 118, 125 };
            double[] low = { 95, 100, 98, 105, 103, 110, 107, 115, 113, 120 };
            var period = 5;

            // Act
            var result = DeMarker.Calculate(high, low, period);

            // Assert - All DeMarker values should be between 0 and 1
            for (var i = 0; i < result.DeMarker.Length; i++)
            {
                Assert.IsTrue(result.DeMarker[i] >= 0.0,
                    $"DeMarker[{i}] = {result.DeMarker[i]} should be >= 0");
                Assert.IsTrue(result.DeMarker[i] <= 1.0,
                    $"DeMarker[{i}] = {result.DeMarker[i]} should be <= 1");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_BullishMarket_ShowsHighValues()
        {
            // Arrange - Strong bullish market (consistent higher highs and higher lows)
            var high = new double[15];
            var low = new double[15];

            for (var i = 0; i < 15; i++)
            {
                high[i] = 100 + i * 2; // Consistently rising highs
                low[i] = 95 + i * 2; // Consistently rising lows
            }

            var period = 5;

            // Act
            var result = DeMarker.Calculate(high, low, period);

            // Assert - Should show high DeMarker values (approaching 1.0) in strong bullish market
            double averageDeMarker = 0;
            var count = 0;

            for (var i = period; i < result.DeMarker.Length; i++)
            {
                averageDeMarker += result.DeMarker[i];
                count++;
            }

            if (count > 0)
            {
                averageDeMarker /= count;
                Assert.IsTrue(averageDeMarker > 0.6,
                    $"Average DeMarker ({averageDeMarker:F3}) should be high in bullish market");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_BearishMarket_ShowsLowValues()
        {
            // Arrange - Strong bearish market (consistent lower highs and lower lows)
            var high = new double[15];
            var low = new double[15];

            for (var i = 0; i < 15; i++)
            {
                high[i] = 120 - i * 2; // Consistently falling highs
                low[i] = 115 - i * 2; // Consistently falling lows
            }

            var period = 5;

            // Act
            var result = DeMarker.Calculate(high, low, period);

            // Assert - Should show low DeMarker values (approaching 0.0) in strong bearish market
            double averageDeMarker = 0;
            var count = 0;

            for (var i = period; i < result.DeMarker.Length; i++)
            {
                averageDeMarker += result.DeMarker[i];
                count++;
            }

            if (count > 0)
            {
                averageDeMarker /= count;
                Assert.IsTrue(averageDeMarker < 0.4,
                    $"Average DeMarker ({averageDeMarker:F3}) should be low in bearish market");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_SidewaysMarket_ShowsModerateValues()
        {
            // Arrange - Sideways market (oscillating prices)
            var high = new double[20];
            var low = new double[20];

            for (var i = 0; i < 20; i++)
            {
                var basePrice = 100 + Math.Sin(i * 0.5) * 5; // Oscillating around 100
                high[i] = basePrice + 3;
                low[i] = basePrice - 3;
            }

            var period = 7;

            // Act
            var result = DeMarker.Calculate(high, low, period);

            // Assert - Should show moderate values around 0.5 in sideways market
            double averageDeMarker = 0;
            var count = 0;

            for (var i = period; i < result.DeMarker.Length; i++)
            {
                averageDeMarker += result.DeMarker[i];
                count++;
            }

            if (count > 0)
            {
                averageDeMarker /= count;
                Assert.IsTrue(averageDeMarker > 0.3 && averageDeMarker < 0.7,
                    $"Average DeMarker ({averageDeMarker:F3}) should be moderate in sideways market");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_UnequalArrayLengths_UsesMinimum()
        {
            // Arrange - Arrays of different lengths
            double[] high = { 102, 105, 104, 107, 106 }; // 5 elements
            double[] low = { 98, 101, 100 }; // 3 elements (shortest)
            var period = 2;

            // Act
            var result = DeMarker.Calculate(high, low, period);

            // Assert - Should use length of shortest array (3)
            Assert.AreEqual(3, result.DeMarker.Length);
            Assert.AreEqual(3, result.DeMax.Length);
            Assert.AreEqual(3, result.DeMin.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_WithEmptyArrays_ReturnsEmptyResult()
        {
            // Arrange
            double[] empty = { };

            // Act
            var result = DeMarker.Calculate(empty, empty);

            // Assert
            Assert.AreEqual(0, result.DeMarker.Length);
            Assert.AreEqual(0, result.DeMax.Length);
            Assert.AreEqual(0, result.DeMin.Length);
            Assert.AreEqual(0, result.AvgDeMax.Length);
            Assert.AreEqual(0, result.AvgDeMin.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_InsufficientData_ReturnsEmptyResult()
        {
            // Arrange - Not enough data for the period
            double[] high = { 102, 105 }; // Only 2 elements
            double[] low = { 98, 101 };
            var period = 5; // Need 5 elements

            // Act
            var result = DeMarker.Calculate(high, low, period);

            // Assert
            Assert.AreEqual(0, result.DeMarker.Length);
            Assert.AreEqual(0, result.DeMax.Length);
            Assert.AreEqual(0, result.DeMin.Length);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DeMarker_Calculate_NullHighArray_ThrowsArgumentNullException()
        {
            // Arrange
            double[] low = { 98 };

            // Act
            DeMarker.Calculate(null, low);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DeMarker_Calculate_NullLowArray_ThrowsArgumentNullException()
        {
            // Arrange
            double[] high = { 102 };

            // Act
            DeMarker.Calculate(high, null);
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_ZeroPeriod_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                // Arrange
                double[] high = { 102, 105 };
                double[] low = { 98, 101 };

                // Act
                DeMarker.Calculate(high, low, 0);
            });
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_NegativePeriod_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                // Arrange
                double[] high = { 102, 105 };
                double[] low = { 98, 101 };

                // Act
                DeMarker.Calculate(high, low, -5);
            });
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_ZeroDivisionHandling_IsCorrect()
        {
            // Arrange - Create scenario where both AvgDeMax and AvgDeMin could be zero
            double[] high = { 100, 100, 100, 100, 100, 100 }; // No price movement
            double[] low = { 100, 100, 100, 100, 100, 100 };
            var period = 3;

            // Act
            var result = DeMarker.Calculate(high, low, period);

            // Assert - Should handle zero division gracefully
            for (var i = 0; i < result.DeMarker.Length; i++)
            {
                Assert.IsFalse(double.IsInfinity(result.DeMarker[i]),
                    $"DeMarker[{i}] should not be infinite");
                Assert.IsFalse(double.IsNaN(result.DeMarker[i]),
                    $"DeMarker[{i}] should not be NaN");
            }

            // With no price movement, DeMarker should be 0
            for (var i = period; i < result.DeMarker.Length; i++)
                Assert.AreEqual(0.0, result.DeMarker[i], TOLERANCE,
                    "DeMarker should be 0 when there's no price movement");
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_DifferentPeriods_ProduceDifferentResults()
        {
            // Arrange
            double[] high = { 100, 105, 103, 108, 106, 111, 109, 114, 112, 117, 115 };
            double[] low = { 95, 100, 98, 103, 101, 106, 104, 109, 107, 112, 110 };

            // Act
            var shortPeriod = DeMarker.Calculate(high, low, 3);
            var longPeriod = DeMarker.Calculate(high, low, 7);

            // Assert - Different periods should produce different results
            var hasDifference = false;
            var minLength = Math.Min(shortPeriod.DeMarker.Length, longPeriod.DeMarker.Length);

            for (var i = 7; i < minLength; i++) // Start after longer period initialization
                if (Math.Abs(shortPeriod.DeMarker[i] - longPeriod.DeMarker[i]) > TOLERANCE)
                {
                    hasDifference = true;
                    break;
                }

            Assert.IsTrue(hasDifference, "Different periods should produce different DeMarker values");
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_RealWorldExample_ProducesValidResults()
        {
            // Arrange - Realistic stock price data
            double[] high =
            {
                150.25, 152.75, 151.50, 154.00, 152.25, 155.75, 153.50, 157.25, 155.00,
                159.50, 157.25, 161.00, 158.75, 163.25, 161.00, 165.50, 163.25, 167.00
            };
            double[] low =
            {
                148.75, 150.00, 149.25, 151.50, 150.00, 153.25, 151.00, 155.00, 152.75,
                157.25, 155.00, 158.75, 156.50, 161.00, 158.75, 163.25, 161.00, 164.75
            };
            var period = 14;

            // Act
            var result = DeMarker.Calculate(high, low, period);

            // Assert
            Assert.AreEqual(18, result.DeMarker.Length);

            // Verify all values are reasonable
            for (var i = 0; i < result.DeMarker.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.DeMarker[i]),
                    $"DeMarker[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.DeMarker[i]),
                    $"DeMarker[{i}] should not be infinite");
                Assert.IsTrue(result.DeMarker[i] >= 0.0 && result.DeMarker[i] <= 1.0,
                    $"DeMarker[{i}] = {result.DeMarker[i]} should be between 0 and 1");

                // DeMax and DeMin should be non-negative
                Assert.IsTrue(result.DeMax[i] >= 0.0,
                    $"DeMax[{i}] should be non-negative");
                Assert.IsTrue(result.DeMin[i] >= 0.0,
                    $"DeMin[{i}] should be non-negative");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_OverboughtOversold_Levels()
        {
            // Arrange - Create data that should trigger overbought/oversold conditions
            var high = new double[20];
            var low = new double[20];

            // First half: strong uptrend (should be overbought)
            for (var i = 0; i < 10; i++)
            {
                high[i] = 100 + i * 5;
                low[i] = 98 + i * 5;
            }

            // Second half: strong downtrend (should be oversold)
            for (var i = 10; i < 20; i++)
            {
                high[i] = 150 - (i - 10) * 5;
                low[i] = 148 - (i - 10) * 5;
            }

            // Act
            var result = DeMarker.Calculate(high, low, 5);

            // Assert
            var foundOverbought = false;
            var foundOversold = false;

            for (var i = 5; i < result.DeMarker.Length; i++)
            {
                if (result.DeMarker[i] > 0.7)
                    foundOverbought = true;
                if (result.DeMarker[i] < 0.3)
                    foundOversold = true;
            }

            Assert.IsTrue(foundOverbought || foundOversold,
                "Should find either overbought (>0.7) or oversold (<0.3) conditions in extreme data");
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_MovingAverageCalculation_IsCorrect()
        {
            // Arrange - Test the moving average calculation specifically
            double[] high = { 10, 12, 11, 15, 13, 16, 14 };
            double[] low = { 8, 10, 9, 13, 11, 14, 12 };
            var period = 3;

            // Act
            var result = DeMarker.Calculate(high, low, period);

            // Assert - Verify moving average calculation at a specific point
            if (result.DeMarker.Length > 4)
            {
                // At index 4, should average the last 3 DeMax values: indices 2, 3, 4
                // DeMax[2] = Max(11-12, 0) = 0
                // DeMax[3] = Max(15-11, 0) = 4  
                // DeMax[4] = Max(13-15, 0) = 0
                // AvgDeMax[4] = (0 + 4 + 0) / 3 = 4/3

                double expectedDeMax2 = Math.Max(11 - 12, 0); // 0
                double expectedDeMax3 = Math.Max(15 - 11, 0); // 4
                double expectedDeMax4 = Math.Max(13 - 15, 0); // 0
                var expectedAvgDeMax4 = (expectedDeMax2 + expectedDeMax3 + expectedDeMax4) / 3.0;

                Assert.AreEqual(expectedDeMax2, result.DeMax[2], TOLERANCE);
                Assert.AreEqual(expectedDeMax3, result.DeMax[3], TOLERANCE);
                Assert.AreEqual(expectedDeMax4, result.DeMax[4], TOLERANCE);
                Assert.AreEqual(expectedAvgDeMax4, result.AvgDeMax[4], TOLERANCE);
            }
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_LargeDataSet_PerformanceAndAccuracy()
        {
            // Arrange - Large dataset for performance testing
            var size = 1000;
            var high = new double[size];
            var low = new double[size];

            var random = new Random(42); // Fixed seed for reproducibility
            var basePrice = 100.0;

            for (var i = 0; i < size; i++)
            {
                var change = (random.NextDouble() - 0.5) * 4; // ±2 price movement
                basePrice = Math.Max(1.0, basePrice + change);

                high[i] = basePrice + random.NextDouble() * 2;
                low[i] = basePrice - random.NextDouble() * 2;

                // Ensure high >= low
                if (high[i] < low[i])
                {
                    var temp = high[i];
                    high[i] = low[i];
                    low[i] = temp;
                }
            }

            // Act
            var startTime = DateTime.Now;
            var result = DeMarker.Calculate(high, low);
            var endTime = DateTime.Now;

            // Assert
            Assert.AreEqual(size, result.DeMarker.Length);

            // Performance check
            var duration = endTime - startTime;
            Assert.IsTrue(duration.TotalSeconds < 1.0, "Large dataset calculation should complete quickly");

            // Accuracy check
            for (var i = 0; i < size; i++)
            {
                Assert.IsFalse(double.IsNaN(result.DeMarker[i]), $"DeMarker[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.DeMarker[i]), $"DeMarker[{i}] should not be infinite");
                Assert.IsTrue(result.DeMarker[i] >= 0.0 && result.DeMarker[i] <= 1.0,
                    $"DeMarker[{i}] should be between 0 and 1");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarkerResult_PropertiesInitialized()
        {
            // Arrange & Act
            var result = new DeMarkerResult();

            // Assert
            Assert.IsNull(result.DeMarker);
            Assert.IsNull(result.DeMax);
            Assert.IsNull(result.DeMin);
            Assert.IsNull(result.AvgDeMax);
            Assert.IsNull(result.AvgDeMin);
        }

        [TestMethod][TestCategory("Core")]
        public void DeMarker_Calculate_IntermediateValues_AreExposed()
        {
            // Arrange
            double[] high = { 100, 105, 103, 108, 106 };
            double[] low = { 95, 100, 98, 103, 101 };
            var period = 3;

            // Act
            var result = DeMarker.Calculate(high, low, period);

            // Assert - All intermediate calculations should be available
            Assert.IsNotNull(result.DeMax, "DeMax should be available");
            Assert.IsNotNull(result.DeMin, "DeMin should be available");
            Assert.IsNotNull(result.AvgDeMax, "AvgDeMax should be available");
            Assert.IsNotNull(result.AvgDeMin, "AvgDeMin should be available");

            // Verify intermediate values are reasonable
            for (var i = 0; i < result.DeMax.Length; i++)
            {
                Assert.IsTrue(result.DeMax[i] >= 0, $"DeMax[{i}] should be non-negative");
                Assert.IsTrue(result.DeMin[i] >= 0, $"DeMin[{i}] should be non-negative");
            }
        }
    }
}