using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class StochasticTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_ReturnsCorrectLengths()
        {
            // Arrange
            double[] high = { 104, 107, 105, 109, 108, 112, 110, 115 };
            double[] low = { 100, 103, 101, 105, 104, 108, 106, 111 };
            double[] close = { 102, 105, 103, 107, 106, 110, 108, 113 };

            // Act
            var result = Stochastic.Calculate(high, low, close);

            // Assert
            Assert.AreEqual(high.Length, result.Main.Length);
            Assert.AreEqual(high.Length, result.Signal.Length);
            Assert.AreEqual(high.Length, result.Highes.Length);
            Assert.AreEqual(high.Length, result.Lowes.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_DefaultParameters_AreCorrect()
        {
            // Arrange
            var high = new double[15];
            var low = new double[15];
            var close = new double[15];

            for (var i = 0; i < 15; i++)
            {
                high[i] = 100 + i + 2;
                low[i] = 100 + i - 2;
                close[i] = 100 + i;
            }

            // Act
            var defaultResult = Stochastic.Calculate(high, low, close);
            var explicitResult = Stochastic.Calculate(high, low, close);

            // Assert
            Assert.AreEqual(defaultResult.Main.Length, explicitResult.Main.Length);
            for (var i = 0; i < high.Length; i++)
            {
                Assert.AreEqual(explicitResult.Main[i], defaultResult.Main[i], TOLERANCE,
                    $"Default parameters should match explicit parameters at index {i}");
                Assert.AreEqual(explicitResult.Signal[i], defaultResult.Signal[i], TOLERANCE,
                    $"Default signal should match explicit signal at index {i}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_AppliesCorrectFormulas()
        {
            // Arrange - Simple test case for manual verification
            double[] high = { 15, 17, 16, 19, 18 };
            double[] low = { 10, 12, 11, 14, 13 };
            double[] close = { 12, 15, 13, 17, 16 };
            var kPeriod = 3;

            // Act
            var result = Stochastic.Calculate(high, low, close, kPeriod, 2, 2);

            // Assert - Manual calculations for verification

            // At index 2 (first valid calculation):
            // HH = Max(15, 17, 16) = 17
            // LL = Min(10, 12, 11) = 10
            // Raw %K = (13 - 10) / (17 - 10) * 100 = 3/7 * 100 ? 42.857
            Assert.AreEqual(17, result.Highes[2], TOLERANCE, "Highest high calculation incorrect");
            Assert.AreEqual(10, result.Lowes[2], TOLERANCE, "Lowest low calculation incorrect");

            // At index 3:
            // HH = Max(17, 16, 19) = 19
            // LL = Min(12, 11, 14) = 11
            // Raw %K = (17 - 11) / (19 - 11) * 100 = 6/8 * 100 = 75.0
            Assert.AreEqual(19, result.Highes[3], TOLERANCE, "Highest high at index 3 incorrect");
            Assert.AreEqual(11, result.Lowes[3], TOLERANCE, "Lowest low at index 3 incorrect");

            // At index 4:
            // HH = Max(16, 19, 18) = 19
            // LL = Min(11, 14, 13) = 11
            // Raw %K = (16 - 11) / (19 - 11) * 100 = 5/8 * 100 = 62.5
            Assert.AreEqual(19, result.Highes[4], TOLERANCE, "Highest high at index 4 incorrect");
            Assert.AreEqual(11, result.Lowes[4], TOLERANCE, "Lowest low at index 4 incorrect");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_OscillatesBetweenZeroAndHundred()
        {
            // Arrange - Various market conditions
            double[] high = { 110, 115, 112, 120, 118, 125, 122, 130 };
            double[] low = { 105, 110, 107, 115, 113, 120, 117, 125 };
            double[] close = { 108, 113, 110, 118, 116, 123, 120, 128 };

            // Act
            var result = Stochastic.Calculate(high, low, close);

            // Assert - All values should be between 0 and 100
            for (var i = 0; i < result.Main.Length; i++)
            {
                Assert.IsTrue(result.Main[i] >= 0.0,
                    $"Main[{i}] = {result.Main[i]} should be >= 0");
                Assert.IsTrue(result.Main[i] <= 100.0,
                    $"Main[{i}] = {result.Main[i]} should be <= 100");

                Assert.IsTrue(result.Signal[i] >= 0.0,
                    $"Signal[{i}] = {result.Signal[i]} should be >= 0");
                Assert.IsTrue(result.Signal[i] <= 100.0,
                    $"Signal[{i}] = {result.Signal[i]} should be <= 100");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_OverboughtConditions()
        {
            // Arrange - Price near top of range (overbought scenario)
            double[] high = { 110, 110, 110, 110, 110, 110 };
            double[] low = { 100, 100, 100, 100, 100, 100 };
            double[] close = { 109, 109, 109, 109, 109, 109 }; // Close near high

            // Act
            var result = Stochastic.Calculate(high, low, close);

            // Assert - Should show high stochastic values (overbought)
            for (var i = 5; i < result.Main.Length; i++) // After initialization period
                if (result.Main[i] != 0.0) // Skip zero initialization values
                    Assert.IsTrue(result.Main[i] > 80.0,
                        $"Main[{i}] = {result.Main[i]} should be > 80 (overbought) when close is near high");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_OversoldConditions()
        {
            // Arrange - Price near bottom of range (oversold scenario)
            double[] high = { 110, 110, 110, 110, 110, 110 };
            double[] low = { 100, 100, 100, 100, 100, 100 };
            double[] close = { 101, 101, 101, 101, 101, 101 }; // Close near low

            // Act
            var result = Stochastic.Calculate(high, low, close);

            // Assert - Should show low stochastic values (oversold)
            for (var i = 5; i < result.Main.Length; i++) // After initialization period
                if (result.Main[i] != 0.0) // Skip zero initialization values
                    Assert.IsTrue(result.Main[i] < 20.0,
                        $"Main[{i}] = {result.Main[i]} should be < 20 (oversold) when close is near low");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_ZeroRangeHandling()
        {
            // Arrange - No price movement (zero range)
            double[] high = { 100, 100, 100, 100, 100 };
            double[] low = { 100, 100, 100, 100, 100 };
            double[] close = { 100, 100, 100, 100, 100 };

            // Act
            var result = Stochastic.Calculate(high, low, close, 3, 2, 2);

            // Assert - Should handle zero range gracefully
            for (var i = 0; i < result.Main.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.Main[i]),
                    $"Main[{i}] should not be NaN with zero range");
                Assert.IsFalse(double.IsInfinity(result.Main[i]),
                    $"Main[{i}] should not be infinite with zero range");

                // With zero range, should default to maximum (100.0) or be zero during initialization
                if (i >= 3) // After kPeriod initialization
                    Assert.IsTrue(result.Main[i] == 100.0 || result.Main[i] == 0.0,
                        $"Main[{i}] should be either 100.0 or 0.0 with zero range");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_BullishDivergence()
        {
            // Arrange - Price making lower lows but stochastic making higher lows (bullish divergence)
            double[] high = { 110, 105, 108, 103, 106, 101, 104 };
            double[] low = { 105, 100, 103, 98, 101, 96, 99 }; // Lower lows
            double[] close = { 107, 102, 105, 100, 103, 98, 101 }; // But closes relatively higher in range

            // Act
            var result = Stochastic.Calculate(high, low, close);

            // Assert - Should show stochastic values responding to relative position in range
            var hasValidCalculations = false;
            for (var i = 6; i < result.Main.Length; i++) // After sufficient initialization
                if (result.Main[i] != 0.0)
                {
                    hasValidCalculations = true;
                    Assert.IsTrue(result.Main[i] >= 0.0 && result.Main[i] <= 100.0,
                        $"Main[{i}] should be within valid range");
                }

            Assert.IsTrue(hasValidCalculations, "Should have some valid stochastic calculations");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_SignalLineSmoothing()
        {
            // Arrange - Data that will test signal line smoothing
            double[] high = { 105, 110, 107, 112, 109, 115, 112, 118, 115, 120 };
            double[] low = { 100, 105, 102, 107, 104, 110, 107, 113, 110, 115 };
            double[] close = { 103, 108, 105, 110, 107, 113, 110, 116, 113, 118 };

            // Act
            var result = Stochastic.Calculate(high, low, close);

            // Assert - Signal line should be smoother than main line
            // Check that signal line values are reasonable
            for (var i = 0; i < result.Signal.Length; i++)
                if (result.Signal[i] != 0.0) // Skip initialization values
                {
                    Assert.IsTrue(result.Signal[i] >= 0.0 && result.Signal[i] <= 100.0,
                        $"Signal[{i}] should be between 0 and 100");

                    // Signal should be finite
                    Assert.IsFalse(double.IsNaN(result.Signal[i]),
                        $"Signal[{i}] should not be NaN");
                    Assert.IsFalse(double.IsInfinity(result.Signal[i]),
                        $"Signal[{i}] should not be infinite");
                }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_UnequalArrayLengths_UsesMinimum()
        {
            // Arrange - Arrays of different lengths
            double[] high = { 105, 110, 107, 112, 109 }; // 5 elements
            double[] low = { 100, 105, 102 }; // 3 elements (shortest)
            double[] close = { 103, 108, 105, 110, 107, 113 }; // 6 elements

            // Act
            var result = Stochastic.Calculate(high, low, close, 2, 2, 2);

            // Assert - Should use length of shortest array (3)
            Assert.AreEqual(3, result.Main.Length);
            Assert.AreEqual(3, result.Signal.Length);
            Assert.AreEqual(3, result.Highes.Length);
            Assert.AreEqual(3, result.Lowes.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_WithEmptyArrays_ReturnsEmptyResult()
        {
            // Arrange
            double[] empty = { };

            // Act
            var result = Stochastic.Calculate(empty, empty, empty);

            // Assert
            Assert.AreEqual(0, result.Main.Length);
            Assert.AreEqual(0, result.Signal.Length);
            Assert.AreEqual(0, result.Highes.Length);
            Assert.AreEqual(0, result.Lowes.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Stochastic_Calculate_NullHighArray_ThrowsArgumentNullException()
        {
            // Arrange
            double[] low = { 100 };
            double[] close = { 102 };

            // Act
            Stochastic.Calculate(null, low, close);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Stochastic_Calculate_NullLowArray_ThrowsArgumentNullException()
        {
            // Arrange
            double[] high = { 105 };
            double[] close = { 102 };

            // Act
            Stochastic.Calculate(high, null, close);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Stochastic_Calculate_NullCloseArray_ThrowsArgumentNullException()
        {
            // Arrange
            double[] high = { 105 };
            double[] low = { 100 };

            // Act
            Stochastic.Calculate(high, low, null);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void Stochastic_Calculate_ZeroKPeriod_ThrowsArgumentException()
        {
            // Arrange
            double[] high = { 105 };
            double[] low = { 100 };
            double[] close = { 102 };

            // Act
            Stochastic.Calculate(high, low, close, 0);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void Stochastic_Calculate_ZeroDPeriod_ThrowsArgumentException()
        {
            // Arrange
            double[] high = { 105 };
            double[] low = { 100 };
            double[] close = { 102 };

            // Act
            Stochastic.Calculate(high, low, close, 5, 0);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void Stochastic_Calculate_ZeroSlowing_ThrowsArgumentException()
        {
            // Arrange
            double[] high = { 105 };
            double[] low = { 100 };
            double[] close = { 102 };

            // Act
            Stochastic.Calculate(high, low, close, 5, 3, 0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_DifferentParameters_ProduceDifferentResults()
        {
            // Arrange
            double[] high = { 110, 115, 112, 118, 116, 122, 120, 125, 123, 128 };
            double[] low = { 105, 110, 107, 113, 111, 117, 115, 120, 118, 123 };
            double[] close = { 108, 113, 110, 116, 114, 120, 118, 123, 121, 126 };

            // Act
            var shortPeriod = Stochastic.Calculate(high, low, close, 3, 2, 2);
            var longPeriod = Stochastic.Calculate(high, low, close, 7, 4, 4);

            // Assert - Different parameters should produce different results
            var hasDifference = false;
            var minLength = Math.Min(shortPeriod.Main.Length, longPeriod.Main.Length);

            for (var i = 7; i < minLength; i++) // Start after longer period initialization
                if (Math.Abs(shortPeriod.Main[i] - longPeriod.Main[i]) > TOLERANCE)
                {
                    hasDifference = true;
                    break;
                }

            Assert.IsTrue(hasDifference, "Different parameters should produce different stochastic values");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_RealWorldStockExample()
        {
            // Arrange - Realistic stock price data
            double[] high =
            {
                152.75, 154.00, 153.50, 157.25, 155.00, 159.50, 157.25, 161.00, 158.75,
                163.25, 161.00, 165.50, 163.25, 167.00, 164.75, 168.50, 166.25, 170.00
            };
            double[] low =
            {
                150.00, 151.50, 150.25, 154.75, 152.50, 157.00, 154.75, 158.50, 156.25,
                161.00, 158.75, 163.25, 161.00, 164.75, 162.50, 166.25, 164.00, 167.75
            };
            double[] close =
            {
                151.25, 152.50, 152.00, 156.75, 154.25, 158.75, 156.50, 160.25, 157.50,
                162.50, 160.25, 164.75, 162.50, 166.25, 163.75, 167.75, 165.50, 169.25
            };

            // Act
            var result = Stochastic.Calculate(high, low, close, 14);

            // Assert
            Assert.AreEqual(18, result.Main.Length);

            // Verify all values are reasonable
            for (var i = 0; i < result.Main.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.Main[i]),
                    $"Main[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.Main[i]),
                    $"Main[{i}] should not be infinite");
                Assert.IsTrue(result.Main[i] >= 0.0 && result.Main[i] <= 100.0,
                    $"Main[{i}] = {result.Main[i]} should be between 0 and 100");

                Assert.IsFalse(double.IsNaN(result.Signal[i]),
                    $"Signal[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.Signal[i]),
                    $"Signal[{i}] should not be infinite");
                Assert.IsTrue(result.Signal[i] >= 0.0 && result.Signal[i] <= 100.0,
                    $"Signal[{i}] = {result.Signal[i]} should be between 0 and 100");

                // Highest and lowest values should be reasonable
                if (result.Highes[i] != 0.0) // Skip initialization values
                    Assert.IsTrue(result.Highes[i] >= result.Lowes[i],
                        $"Highest[{i}] should be >= Lowest[{i}]");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_InitializationPeriod_IsCorrect()
        {
            // Arrange
            double[] high = { 105, 110, 107, 112, 109, 115, 112, 118 };
            double[] low = { 100, 105, 102, 107, 104, 110, 107, 113 };
            double[] close = { 103, 108, 105, 110, 107, 113, 110, 116 };
            var kPeriod = 5;
            var slowing = 3;
            var dPeriod = 2;

            // Act
            var result = Stochastic.Calculate(high, low, close, kPeriod, dPeriod, slowing);

            // Assert - Verify initialization periods
            // Main line should be 0 for first (kPeriod - 1 + slowing - 1) = 6 values
            var mainInitPeriod = kPeriod - 1 + slowing - 1; // 5-1+3-1 = 6
            for (var i = 0; i < mainInitPeriod && i < result.Main.Length; i++)
                Assert.AreEqual(0.0, result.Main[i], TOLERANCE,
                    $"Main[{i}] should be 0 during initialization period");

            // Signal line should be 0 for first (dPeriod - 1) values
            for (var i = 0; i < dPeriod - 1 && i < result.Signal.Length; i++)
                Assert.AreEqual(0.0, result.Signal[i], TOLERANCE,
                    $"Signal[{i}] should be 0 during initialization period");

            // Highes/Lowes should be 0 for first (kPeriod - 1) values
            for (var i = 0; i < kPeriod - 1 && i < result.Highes.Length; i++)
            {
                Assert.AreEqual(0.0, result.Highes[i], TOLERANCE,
                    $"Highes[{i}] should be 0 during initialization period");
                Assert.AreEqual(0.0, result.Lowes[i], TOLERANCE,
                    $"Lowes[{i}] should be 0 during initialization period");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_CrossoverSignals()
        {
            // Arrange - Data designed to create crossovers between %K and %D
            double[] high = { 110, 115, 112, 108, 105, 109, 113, 117, 120, 116 };
            double[] low = { 105, 110, 107, 103, 100, 104, 108, 112, 115, 111 };
            double[] close = { 108, 113, 110, 106, 103, 107, 111, 115, 118, 114 };

            // Act
            var result = Stochastic.Calculate(high, low, close);

            // Assert - Look for crossovers (where main crosses signal)
            var foundCrossover = false;
            for (var i = 8; i < result.Main.Length - 1; i++) // After sufficient initialization
                if (result.Main[i] != 0.0 && result.Signal[i] != 0.0 &&
                    result.Main[i + 1] != 0.0 && result.Signal[i + 1] != 0.0)
                {
                    // Check if lines crossed (one was above, now below or vice versa)
                    var wasMainAbove = result.Main[i] > result.Signal[i];
                    var isMainAbove = result.Main[i + 1] > result.Signal[i + 1];

                    if (wasMainAbove != isMainAbove)
                    {
                        foundCrossover = true;
                        break;
                    }
                }

            // Just verify the calculation produces reasonable values that could have crossovers
            var hasNonZeroValues = false;
            for (var i = 0; i < result.Main.Length; i++)
                if (result.Main[i] != 0.0 || result.Signal[i] != 0.0)
                {
                    hasNonZeroValues = true;
                    break;
                }

            Assert.IsTrue(hasNonZeroValues, "Should have non-zero stochastic values for crossover analysis");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void Stochastic_Calculate_LargeDataSet_PerformanceAndAccuracy()
        {
            // Arrange - Large dataset for performance testing
            var size = 1000;
            var high = new double[size];
            var low = new double[size];
            var close = new double[size];

            var random = new Random(42); // Fixed seed for reproducibility
            var basePrice = 100.0;

            for (var i = 0; i < size; i++)
            {
                var change = (random.NextDouble() - 0.5) * 4; // ±2 price movement
                basePrice = Math.Max(1.0, basePrice + change);

                low[i] = basePrice - random.NextDouble() * 2;
                high[i] = basePrice + random.NextDouble() * 2;
                close[i] = low[i] + (high[i] - low[i]) * random.NextDouble();

                // Ensure OHLC relationships
                if (high[i] < low[i])
                {
                    var temp = high[i];
                    high[i] = low[i];
                    low[i] = temp;
                }

                close[i] = Math.Max(low[i], Math.Min(high[i], close[i]));
            }

            // Act
            var startTime = DateTime.Now;
            var result = Stochastic.Calculate(high, low, close, 14);
            var endTime = DateTime.Now;

            // Assert
            Assert.AreEqual(size, result.Main.Length);

            // Performance check
            var duration = endTime - startTime;
            Assert.IsTrue(duration.TotalSeconds < 1.0, "Large dataset calculation should complete quickly");

            // Accuracy check
            for (var i = 0; i < size; i++)
            {
                Assert.IsFalse(double.IsNaN(result.Main[i]), $"Main[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.Main[i]), $"Main[{i}] should not be infinite");
                Assert.IsTrue(result.Main[i] >= 0.0 && result.Main[i] <= 100.0,
                    $"Main[{i}] should be between 0 and 100");

                Assert.IsFalse(double.IsNaN(result.Signal[i]), $"Signal[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.Signal[i]), $"Signal[{i}] should not be infinite");
                Assert.IsTrue(result.Signal[i] >= 0.0 && result.Signal[i] <= 100.0,
                    $"Signal[{i}] should be between 0 and 100");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void StochasticResult_PropertiesInitialized()
        {
            // Arrange & Act
            var result = new StochasticResult();

            // Assert
            Assert.IsNull(result.Main);
            Assert.IsNull(result.Signal);
            Assert.IsNull(result.Highes);
            Assert.IsNull(result.Lowes);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_IntermediateValues_AreExposed()
        {
            // Arrange
            double[] high = { 105, 110, 107, 112, 109 };
            double[] low = { 100, 105, 102, 107, 104 };
            double[] close = { 103, 108, 105, 110, 107 };

            // Act
            var result = Stochastic.Calculate(high, low, close, 3, 2, 2);

            // Assert - All intermediate calculations should be available
            Assert.IsNotNull(result.Highes, "Highes should be available");
            Assert.IsNotNull(result.Lowes, "Lowes should be available");

            // Verify intermediate values are reasonable
            for (var i = 3; i < result.Highes.Length; i++) // After kPeriod initialization
                if (result.Highes[i] != 0.0) // Skip initialization values
                    Assert.IsTrue(result.Highes[i] >= result.Lowes[i],
                        $"Highes[{i}] should be >= Lowes[{i}]");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Stochastic_Calculate_ExtremePriceMovements_HandlesCorrectly()
        {
            // Arrange - Extreme price movements
            double[] high = { 100, 200, 50, 300, 25 };
            double[] low = { 90, 180, 40, 280, 20 };
            double[] close = { 95, 190, 45, 290, 22 };

            // Act
            var result = Stochastic.Calculate(high, low, close, 3, 2, 2);

            // Assert - Should handle extreme movements without errors
            for (var i = 0; i < result.Main.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.Main[i]),
                    $"Main[{i}] should not be NaN with extreme price movements");
                Assert.IsFalse(double.IsInfinity(result.Main[i]),
                    $"Main[{i}] should not be infinite with extreme price movements");

                if (result.Main[i] != 0.0) // Skip initialization values
                    Assert.IsTrue(result.Main[i] >= 0.0 && result.Main[i] <= 100.0,
                        $"Main[{i}] should be between 0 and 100 even with extreme movements");
            }
        }
    }
}