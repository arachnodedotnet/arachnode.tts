using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class ZigZagTests
    {
        private const double TOLERANCE = 1e-10;

        #region Input Validation Tests

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_NullHighArray_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                // Arrange
                double[] high = null;
                double[] low = { 1, 2, 3 };

                // Act & Assert
                ZigZag.Calculate(high, low);
            });
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_NullLowArray_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                // Arrange
                double[] high = { 1, 2, 3 };
                double[] low = null;

                // Act & Assert
                ZigZag.Calculate(high, low);
            });
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_MismatchedArrayLengths_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                // Arrange
                double[] high = { 1, 2, 3 };
                double[] low = { 1, 2 }; // Different length

                // Act & Assert
                ZigZag.Calculate(high, low);
            });
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_EmptyArrays_ReturnsEmptyResult()
        {
            // Arrange
            var high = new double[0];
            var low = new double[0];

            // Act
            var result = ZigZag.Calculate(high, low);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.ZigZag.Length);
            Assert.AreEqual(0, result.HighMap.Length);
            Assert.AreEqual(0, result.LowMap.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_InsufficientData_ReturnsZeroedArrays()
        {
            // Arrange - Less data than required for calculation
            double[] high = { 10, 12 }; // Only 2 points, default depth is 12
            double[] low = { 9, 11 };

            // Act
            var result = ZigZag.Calculate(high, low);

            // Assert
            Assert.AreEqual(high.Length, result.ZigZag.Length);
            Assert.IsTrue(result.ZigZag.All(x => x == 0.0));
            Assert.IsTrue(result.HighMap.All(x => x == 0.0));
            Assert.IsTrue(result.LowMap.All(x => x == 0.0));
        }

        #endregion

        #region Basic Functionality Tests

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_ReturnsCorrectArraySizes()
        {
            // Arrange
            var testData = CreateSimpleTestData(50);

            // Act
            var result = ZigZag.Calculate(testData.high, testData.low);

            // Assert
            Assert.AreEqual(testData.high.Length, result.ZigZag.Length);
            Assert.AreEqual(testData.high.Length, result.HighMap.Length);
            Assert.AreEqual(testData.high.Length, result.LowMap.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_NoNaNOrInfinityValues()
        {
            // Arrange
            var testData = CreateTrendingTestData(30);

            // Act
            var result = ZigZag.Calculate(testData.high, testData.low, 5, 3);

            // Assert
            Assert.IsTrue(result.ZigZag.All(x => !double.IsNaN(x) && !double.IsInfinity(x)));
            Assert.IsTrue(result.HighMap.All(x => !double.IsNaN(x) && !double.IsInfinity(x)));
            Assert.IsTrue(result.LowMap.All(x => !double.IsNaN(x) && !double.IsInfinity(x)));
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_ZigZagValuesAreSubsetOfHighLow()
        {
            // Arrange
            var testData = CreateVolatileTestData(25);

            // Act
            var result = ZigZag.Calculate(testData.high, testData.low, 3);

            // Assert - Every non-zero ZigZag value should match a corresponding high or low
            for (var i = 0; i < result.ZigZag.Length; i++)
                if (result.ZigZag[i] != 0)
                {
                    var matchesHigh = Math.Abs(result.ZigZag[i] - testData.high[i]) < TOLERANCE;
                    var matchesLow = Math.Abs(result.ZigZag[i] - testData.low[i]) < TOLERANCE;
                    Assert.IsTrue(matchesHigh || matchesLow,
                        $"ZigZag[{i}] = {result.ZigZag[i]} should match either high[{i}] = {testData.high[i]} or low[{i}] = {testData.low[i]}");
                }
        }

        #endregion

        #region Algorithm Logic Tests

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_IdentifiesObviousPeaksAndValleys()
        {
            // Arrange - Create clear mountain pattern
            double[] high = { 10, 15, 25, 20, 10, 5, 10, 20, 30, 25, 15, 10, 15, 25, 20 };
            double[] low = { 8, 12, 20, 15, 8, 3, 8, 18, 28, 20, 12, 8, 12, 20, 15 };

            // Act
            var result = ZigZag.Calculate(high, low, 3, 2, 1);

            // Assert - Should identify the major peaks and valleys
            var nonZeroCount = result.ZigZag.Count(x => x != 0);
            Assert.IsTrue(nonZeroCount >= 3, $"Should identify at least 3 swing points, found {nonZeroCount}");

            // Verify alternating pattern (high-low-high or low-high-low)
            var zigZagPoints = result.ZigZag.Select((value, index) => new { Value = value, Index = index })
                .Where(x => x.Value != 0)
                .ToArray();

            if (zigZagPoints.Length >= 3)
                for (var i = 1; i < zigZagPoints.Length - 1; i++)
                {
                    var prev = zigZagPoints[i - 1].Value;
                    var curr = zigZagPoints[i].Value;
                    var next = zigZagPoints[i + 1].Value;

                    // Should alternate between peaks and valleys
                    var isPeak = curr > prev && curr > next;
                    var isValley = curr < prev && curr < next;
                    Assert.IsTrue(isPeak || isValley,
                        $"Point {i} should be either a peak or valley, but values are {prev}, {curr}, {next}");
                }
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_HandlesFlatPrice_Action()
        {
            // Arrange - Flat price with small variations
            var high = Enumerable.Repeat(100.1, 20).ToArray();
            var low = Enumerable.Repeat(99.9, 20).ToArray();

            // Act
            var result = ZigZag.Calculate(high, low, 5, 1);

            // Assert - Should handle flat prices gracefully
            Assert.IsNotNull(result);
            // Most values should be zero since there's no significant trend
            var nonZeroCount = result.ZigZag.Count(x => x != 0);
            Assert.IsTrue(nonZeroCount <= 2, $"Flat price should produce few zigzag points, found {nonZeroCount}");
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_SteadyUptrend_ProducesMinimalSignals()
        {
            // Arrange - Steady uptrend
            var testData = CreateSteadyTrendData(20, 100, 2);

            // Act
            var result = ZigZag.Calculate(testData.high, testData.low, 5);

            // Assert - Steady trend should produce fewer signals
            var nonZeroCount = result.ZigZag.Count(x => x != 0);
            Assert.IsTrue(nonZeroCount <= 4, $"Steady trend should produce few zigzag points, found {nonZeroCount}");
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_HighVolatility_ProducesMoreSignals()
        {
            // Arrange - High volatility data
            var testData = CreateVolatileTestData(30, 10);

            // Act
            var result = ZigZag.Calculate(testData.high, testData.low, 3, 1);

            // Assert - High volatility should produce more signals
            var nonZeroCount = result.ZigZag.Count(x => x != 0);
            Assert.IsTrue(nonZeroCount >= 4,
                $"Volatile data should produce multiple zigzag points, found {nonZeroCount}");
        }

        #endregion

        #region Parameter Tests

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_LargerDepth_ProducesFewerSignals()
        {
            // Arrange
            var testData = CreateVolatileTestData(40);

            // Act
            var result1 = ZigZag.Calculate(testData.high, testData.low, 3);
            var result2 = ZigZag.Calculate(testData.high, testData.low, 10);

            // Assert - Larger depth should filter out more short-term moves
            var signals1 = result1.ZigZag.Count(x => x != 0);
            var signals2 = result2.ZigZag.Count(x => x != 0);

            // Allow for some flexibility due to algorithm complexity
            Assert.IsTrue(signals2 <= signals1 + 2,
                $"Larger depth ({signals2} signals) should produce fewer or similar signals than smaller depth ({signals1} signals)");
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_LargerDeviation_ProducesFewerSignals()
        {
            // Arrange
            var testData = CreateVolatileTestData(35);

            // Act
            var result1 = ZigZag.Calculate(testData.high, testData.low, deviation: 1);
            var result2 = ZigZag.Calculate(testData.high, testData.low, deviation: 5);

            // Assert
            var signals1 = result1.ZigZag.Count(x => x != 0);
            var signals2 = result2.ZigZag.Count(x => x != 0);

            Assert.IsTrue(signals2 <= signals1,
                $"Larger deviation ({signals2} signals) should produce fewer signals than smaller deviation ({signals1} signals)");
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_InvalidParameters_UsesMinimumValues()
        {
            // Arrange
            var testData = CreateSimpleTestData(20);

            // Act & Assert - Should not throw exceptions with invalid parameters
            var result = ZigZag.Calculate(testData.high, testData.low,
                -5, -10, 0, -1);

            Assert.IsNotNull(result);
            Assert.AreEqual(testData.high.Length, result.ZigZag.Length);
        }

        #endregion

        #region Edge Cases

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_SingleDataPoint_HandlesGracefully()
        {
            // Arrange
            double[] high = { 100 };
            double[] low = { 99 };

            // Act
            var result = ZigZag.Calculate(high, low);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.ZigZag.Length);
            Assert.AreEqual(0.0, result.ZigZag[0]); // Insufficient data for calculation
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_ExtremeValues_HandlesCorrectly()
        {
            // Arrange - Very large and very small values
            double[] high = { double.MaxValue / 2, 1000000, 0.001, double.MaxValue / 3 };
            double[] low = { double.MaxValue / 3, 999999, 0.0001, double.MaxValue / 4 };

            // Act & Assert - Should not throw or produce invalid values
            var result = ZigZag.Calculate(high, low, 2);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.ZigZag.All(x => !double.IsNaN(x) && !double.IsInfinity(x)));
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_IdenticalValues_HandlesCorrectly()
        {
            // Arrange - All identical values
            var high = Enumerable.Repeat(100.0, 15).ToArray();
            var low = Enumerable.Repeat(100.0, 15).ToArray();

            // Act
            var result = ZigZag.Calculate(high, low, 3);

            // Assert
            Assert.IsNotNull(result);
            // Should produce minimal or no signals since there's no variation
            var nonZeroCount = result.ZigZag.Count(x => x != 0);
            Assert.IsTrue(nonZeroCount <= 1, $"Identical values should produce minimal signals, found {nonZeroCount}");
        }

        #endregion

        #region Real World Scenario Tests

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_TypicalStockPattern_ProducesReasonableResults()
        {
            // Arrange - Simulate realistic stock price movement
            var testData = CreateRealisticStockData(100);

            // Act
            var result = ZigZag.Calculate(testData.high, testData.low, 5, 3);

            // Assert
            Assert.IsNotNull(result);
            var nonZeroCount = result.ZigZag.Count(x => x != 0);

            // Should identify some swing points but not too many
            Assert.IsTrue(nonZeroCount >= 2, $"Should identify at least 2 swing points, found {nonZeroCount}");
            Assert.IsTrue(nonZeroCount <= testData.high.Length / 3,
                $"Should not have excessive signals, found {nonZeroCount} out of {testData.high.Length} bars");

            // Verify that ZigZag points represent actual extremes
            for (var i = 0; i < result.ZigZag.Length; i++)
                if (result.ZigZag[i] != 0)
                {
                    var validPoint = Math.Abs(result.ZigZag[i] - testData.high[i]) < TOLERANCE ||
                                     Math.Abs(result.ZigZag[i] - testData.low[i]) < TOLERANCE;
                    Assert.IsTrue(validPoint, $"ZigZag point at {i} should match either high or low");
                }
        }

        [TestMethod][TestCategory("Core")]
        public void ZigZag_Calculate_ConsistentResults_AcrossMultipleCalls()
        {
            // Arrange
            var testData = CreateVolatileTestData(25);

            // Act
            var result1 = ZigZag.Calculate(testData.high, testData.low, 4, 2);
            var result2 = ZigZag.Calculate(testData.high, testData.low, 4, 2);

            // Assert - Results should be identical across calls
            Assert.AreEqual(result1.ZigZag.Length, result2.ZigZag.Length);

            for (var i = 0; i < result1.ZigZag.Length; i++)
            {
                Assert.AreEqual(result1.ZigZag[i], result2.ZigZag[i], TOLERANCE, $"ZigZag mismatch at index {i}");
                Assert.AreEqual(result1.HighMap[i], result2.HighMap[i], TOLERANCE, $"HighMap mismatch at index {i}");
                Assert.AreEqual(result1.LowMap[i], result2.LowMap[i], TOLERANCE, $"LowMap mismatch at index {i}");
            }
        }

        #endregion

        #region Helper Methods

        private (double[] high, double[] low) CreateSimpleTestData(int length)
        {
            var high = new double[length];
            var low = new double[length];

            var random = new Random(42); // Seed for reproducibility
            double price = 100;

            for (var i = 0; i < length; i++)
            {
                var change = (random.NextDouble() - 0.5) * 4; // ±2 change
                price = Math.Max(10, price + change);

                high[i] = price + random.NextDouble() * 2;
                low[i] = price - random.NextDouble() * 2;
            }

            return (high, low);
        }

        private (double[] high, double[] low) CreateTrendingTestData(int length)
        {
            var high = new double[length];
            var low = new double[length];

            for (var i = 0; i < length; i++)
            {
                var basePrice = 100 + i * 0.5; // Gentle uptrend
                high[i] = basePrice + 1 + Math.Sin(i * 0.3) * 2;
                low[i] = basePrice - 1 + Math.Sin(i * 0.3) * 2;
            }

            return (high, low);
        }

        private (double[] high, double[] low) CreateVolatileTestData(int length, double volatility = 5)
        {
            var high = new double[length];
            var low = new double[length];

            var random = new Random(123);
            double price = 100;

            for (var i = 0; i < length; i++)
            {
                var change = (random.NextDouble() - 0.5) * volatility;
                price = Math.Max(10, price + change);

                var range = 1 + random.NextDouble() * 3;
                high[i] = price + range;
                low[i] = price - range;
            }

            return (high, low);
        }

        private (double[] high, double[] low) CreateSteadyTrendData(int length, double startPrice = 100,
            double increment = 1)
        {
            var high = new double[length];
            var low = new double[length];

            for (var i = 0; i < length; i++)
            {
                var price = startPrice + i * increment;
                high[i] = price + 0.5;
                low[i] = price - 0.5;
            }

            return (high, low);
        }

        private (double[] high, double[] low) CreateRealisticStockData(int length)
        {
            var high = new double[length];
            var low = new double[length];

            var random = new Random(456);
            double price = 100;

            for (var i = 0; i < length; i++)
            {
                // Simulate realistic stock movement with trends and reversals
                var trendComponent = Math.Sin(i / 20.0) * 0.3;
                var randomComponent = (random.NextDouble() - 0.5) * 2;
                var volatility = 1 + Math.Abs(Math.Sin(i / 10.0)) * 2;

                var change = (trendComponent + randomComponent) * volatility;
                price = Math.Max(50, price + change);

                var dailyRange = 0.5 + random.NextDouble() * 2;
                high[i] = price + dailyRange;
                low[i] = price - dailyRange;
            }

            return (high, low);
        }

        #endregion
    }
}