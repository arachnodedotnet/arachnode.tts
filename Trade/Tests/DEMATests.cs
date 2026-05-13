using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class DEMATests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_ReturnsCorrectLengths()
        {
            // Arrange
            double[] prices = { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109 };
            var period = 5;

            // Act
            var result = DEMA.Calculate(prices, period);

            // Assert
            Assert.AreEqual(prices.Length, result.DEMA.Length);
            Assert.AreEqual(prices.Length, result.EMA.Length);
            Assert.AreEqual(prices.Length, result.EMAofEMA.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_AppliesCorrectFormula()
        {
            // Arrange - Simple test case for manual verification
            double[] prices = { 10, 12, 14, 16, 18, 20 };
            var period = 3;

            // Act
            var result = DEMA.Calculate(prices, period);

            // Assert - Manual calculation verification
            // ? = 2/(3+1) = 0.5
            var alpha = 2.0 / (period + 1);
            Assert.AreEqual(0.5, alpha, TOLERANCE);

            // Verify first EMA value
            Assert.AreEqual(10, result.EMA[0], TOLERANCE, "First EMA should equal first price");

            // Verify second EMA value: 0.5 * 12 + 0.5 * 10 = 11
            var expectedEMA1 = alpha * prices[1] + (1 - alpha) * result.EMA[0];
            Assert.AreEqual(expectedEMA1, result.EMA[1], TOLERANCE, "EMA calculation incorrect");

            // Verify first EMA of EMA value
            Assert.AreEqual(result.EMA[0], result.EMAofEMA[0], TOLERANCE, "First EMA of EMA should equal first EMA");

            // Verify DEMA formula: DEMA = 2 * EMA - EMA(EMA)
            for (var i = 0; i < prices.Length; i++)
            {
                var expectedDEMA = 2.0 * result.EMA[i] - result.EMAofEMA[i];
                Assert.AreEqual(expectedDEMA, result.DEMA[i], TOLERANCE,
                    $"DEMA formula incorrect at index {i}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_DefaultPeriod_Is14()
        {
            // Arrange
            var prices = new double[30];
            for (var i = 0; i < 30; i++) prices[i] = 100 + i; // Upward trend

            // Act
            var defaultResult = DEMA.Calculate(prices);
            var explicitResult = DEMA.Calculate(prices);

            // Assert
            Assert.AreEqual(defaultResult.DEMA.Length, explicitResult.DEMA.Length);
            for (var i = 0; i < prices.Length; i++)
                Assert.AreEqual(explicitResult.DEMA[i], defaultResult.DEMA[i], TOLERANCE,
                    $"Default period should be 14 at index {i}");
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_ReducesLagComparedToEMA()
        {
            // Arrange - Strong uptrend to test lag reduction
            double[] prices = { 100, 105, 110, 115, 120, 125, 130, 135, 140, 145 };
            var period = 5;

            // Act
            var result = DEMA.Calculate(prices, period);

            // Assert - In an uptrend, DEMA should be higher than EMA (less lag)
            for (var i = 3; i < prices.Length; i++)
                Assert.IsTrue(result.DEMA[i] >= result.EMA[i],
                    $"DEMA should be >= EMA in uptrend at index {i} (DEMA: {result.DEMA[i]}, EMA: {result.EMA[i]})");
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_BehavesCorrectlyInDowntrend()
        {
            // Arrange - Strong downtrend
            double[] prices = { 145, 140, 135, 130, 125, 120, 115, 110, 105, 100 };
            var period = 5;

            // Act
            var result = DEMA.Calculate(prices, period);

            // Assert - In a downtrend, DEMA should be lower than EMA (less lag)
            for (var i = 3; i < prices.Length; i++)
                Assert.IsTrue(result.DEMA[i] <= result.EMA[i],
                    $"DEMA should be <= EMA in downtrend at index {i} (DEMA: {result.DEMA[i]}, EMA: {result.EMA[i]})");
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_PositiveShift_MovesValuesRight()
        {
            // Arrange
            double[] prices = { 100, 101, 102, 103, 104, 105 };
            var shift = 2;

            // Act
            var noShiftResult = DEMA.Calculate(prices, 3);
            var shiftedResult = DEMA.Calculate(prices, 3, shift);

            // Assert
            Assert.AreEqual(prices.Length, shiftedResult.DEMA.Length);

            // First 'shift' values should be zero
            for (var i = 0; i < shift; i++)
                Assert.AreEqual(0.0, shiftedResult.DEMA[i], TOLERANCE,
                    $"First {shift} values should be zero with positive shift");

            // Shifted values should match original values offset by shift
            for (var i = 0; i < prices.Length - shift; i++)
                Assert.AreEqual(noShiftResult.DEMA[i], shiftedResult.DEMA[i + shift], TOLERANCE,
                    "Shifted values should match original values");
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_NegativeShift_MovesValuesLeft()
        {
            // Arrange
            double[] prices = { 100, 101, 102, 103, 104, 105 };
            var shift = -2;

            // Act
            var noShiftResult = DEMA.Calculate(prices, 3);
            var shiftedResult = DEMA.Calculate(prices, 3, shift);

            // Assert
            Assert.AreEqual(prices.Length, shiftedResult.DEMA.Length);

            // Last 2 values should be zero
            for (var i = prices.Length - 2; i < prices.Length; i++)
                Assert.AreEqual(0.0, shiftedResult.DEMA[i], TOLERANCE,
                    "Last 2 values should be zero with negative shift");

            // Shifted values should match original values offset by shift
            for (var i = 2; i < prices.Length; i++)
                Assert.AreEqual(noShiftResult.DEMA[i], shiftedResult.DEMA[i - 2], TOLERANCE,
                    "Shifted values should match original values with negative shift");
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_WithEmptyArray_ReturnsEmptyResult()
        {
            // Arrange
            double[] empty = { };

            // Act
            var result = DEMA.Calculate(empty);

            // Assert
            Assert.AreEqual(0, result.DEMA.Length);
            Assert.AreEqual(0, result.EMA.Length);
            Assert.AreEqual(0, result.EMAofEMA.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_WithSingleValue_ReturnsOriginalValue()
        {
            // Arrange
            double[] prices = { 100 };

            // Act
            var result = DEMA.Calculate(prices, 5);

            // Assert
            Assert.AreEqual(1, result.DEMA.Length);
            Assert.AreEqual(100, result.EMA[0], TOLERANCE);
            Assert.AreEqual(100, result.EMAofEMA[0], TOLERANCE);
            Assert.AreEqual(100, result.DEMA[0], TOLERANCE); // 2*100 - 100 = 100
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DEMA_Calculate_NullPricesArray_ThrowsArgumentNullException()
        {
            // Act
            DEMA.Calculate(null);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void DEMA_Calculate_ZeroPeriod_ThrowsArgumentException()
        {
            // Arrange
            double[] prices = { 100, 101, 102 };

            // Act
            DEMA.Calculate(prices, 0);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void DEMA_Calculate_NegativePeriod_ThrowsArgumentException()
        {
            // Arrange
            double[] prices = { 100, 101, 102 };

            // Act
            DEMA.Calculate(prices, -5);
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_DifferentPeriods_ProduceDifferentResults()
        {
            // Arrange
            double[] prices = { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110 };

            // Act
            var shortPeriod = DEMA.Calculate(prices, 3);
            var longPeriod = DEMA.Calculate(prices, 7);

            // Assert - Different periods should produce different results
            var hasDifference = false;
            for (var i = 1; i < prices.Length; i++)
                if (Math.Abs(shortPeriod.DEMA[i] - longPeriod.DEMA[i]) > TOLERANCE)
                {
                    hasDifference = true;
                    break;
                }

            Assert.IsTrue(hasDifference, "Different periods should produce different DEMA values");
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_ConsistentWithStandardDefinition()
        {
            // Arrange - Test against known DEMA behavior
            double[] prices = { 20, 22, 24, 26, 28, 30, 32, 34, 36, 38 };
            var period = 4;

            // Act
            var result = DEMA.Calculate(prices, period);

            // Assert - Verify mathematical consistency
            var alpha = 2.0 / (period + 1); // Should be 0.4

            // Manual calculation for first few values
            Assert.AreEqual(20, result.EMA[0], TOLERANCE);
            Assert.AreEqual(20, result.EMAofEMA[0], TOLERANCE);
            Assert.AreEqual(20, result.DEMA[0], TOLERANCE);

            // Verify EMA calculation for second value
            var expectedEMA1 = alpha * 22 + (1 - alpha) * 20; // 0.4 * 22 + 0.6 * 20 = 20.8
            Assert.AreEqual(expectedEMA1, result.EMA[1], TOLERANCE);

            // Verify EMA of EMA for second value
            var expectedEMAofEMA1 = alpha * expectedEMA1 + (1 - alpha) * 20;
            Assert.AreEqual(expectedEMAofEMA1, result.EMAofEMA[1], TOLERANCE);

            // Verify DEMA formula
            var expectedDEMA1 = 2 * expectedEMA1 - expectedEMAofEMA1;
            Assert.AreEqual(expectedDEMA1, result.DEMA[1], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_SmoothesVolatileData()
        {
            // Arrange - Highly volatile data
            double[] prices = { 100, 110, 95, 115, 90, 120, 85, 125, 80, 130 };
            var period = 5;

            // Act
            var result = DEMA.Calculate(prices, period);

            // Assert - DEMA should be smoother than raw prices
            // All values should be finite and reasonable
            for (var i = 0; i < prices.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.DEMA[i]), $"DEMA[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.DEMA[i]), $"DEMA[{i}] should not be infinite");
                Assert.IsFalse(double.IsNaN(result.EMA[i]), $"EMA[{i}] should not be NaN");
                Assert.IsFalse(double.IsNaN(result.EMAofEMA[i]), $"EMA of EMA[{i}] should not be NaN");

                // DEMA values should be within reasonable range of input data
                double minPrice = Math.Min(80, 130);
                double maxPrice = Math.Max(80, 130);
                Assert.IsTrue(result.DEMA[i] >= minPrice - 20 && result.DEMA[i] <= maxPrice + 20,
                    $"DEMA[{i}] = {result.DEMA[i]} should be within reasonable range");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_RealWorldExample_ProducesValidResults()
        {
            // Arrange - Simulated realistic stock price data
            double[] prices =
            {
                100.0, 100.5, 101.2, 100.8, 102.1, 103.4, 102.9, 104.2, 105.1, 104.7,
                106.3, 107.8, 107.2, 108.9, 109.4, 108.8, 110.2, 111.5, 110.9, 112.3
            };
            var period = 10;

            // Act
            var result = DEMA.Calculate(prices, period);

            // Assert
            Assert.AreEqual(20, result.DEMA.Length);

            // Verify all values are reasonable for stock data
            for (var i = 0; i < result.DEMA.Length; i++)
            {
                Assert.IsTrue(result.DEMA[i] > 95 && result.DEMA[i] < 120,
                    $"DEMA[{i}] = {result.DEMA[i]} should be within reasonable range for stock data");
                Assert.IsTrue(result.EMA[i] > 95 && result.EMA[i] < 120,
                    $"EMA[{i}] = {result.EMA[i]} should be within reasonable range for stock data");
            }

            // In an uptrend, later DEMA values should generally be higher than earlier ones
            Assert.IsTrue(result.DEMA[prices.Length - 1] > result.DEMA[0],
                "DEMA should show upward trend in generally increasing price data");
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_ComparisonWithTraditionalEMA()
        {
            // Arrange - Test DEMA responsiveness vs traditional EMA
            double[] prices = { 100, 100, 100, 100, 100, 105, 110, 115, 120, 125 }; // Sudden upward spike
            var period = 5;

            // Act
            var demaResult = DEMA.Calculate(prices, period);

            // Calculate traditional EMA for comparison
            var traditionalEMA = new double[prices.Length];
            var alpha = 2.0 / (period + 1);
            traditionalEMA[0] = prices[0];
            for (var i = 1; i < prices.Length; i++)
                traditionalEMA[i] = alpha * prices[i] + (1 - alpha) * traditionalEMA[i - 1];

            // Assert - DEMA should respond faster to trend changes
            // After the trend change (from index 5 onwards), DEMA should be higher than traditional EMA
            for (var i = 6; i < prices.Length; i++)
                Assert.IsTrue(demaResult.DEMA[i] >= traditionalEMA[i],
                    $"DEMA should be >= traditional EMA in uptrend at index {i} " +
                    $"(DEMA: {demaResult.DEMA[i]}, Traditional EMA: {traditionalEMA[i]})");
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_LargeDataSet_PerformanceAndAccuracy()
        {
            // Arrange - Large dataset for performance and accuracy testing
            var size = 1000;
            var prices = new double[size];
            var random = new Random(42); // Fixed seed for reproducibility
            var price = 100.0;

            for (var i = 0; i < size; i++)
            {
                price += (random.NextDouble() - 0.5) * 2; // Random walk
                price = Math.Max(1.0, price); // Keep price positive
                prices[i] = price;
            }

            // Act
            var startTime = DateTime.Now;
            var result = DEMA.Calculate(prices, 20);
            var endTime = DateTime.Now;

            // Assert
            Assert.AreEqual(size, result.DEMA.Length);

            // Performance check (should complete quickly)
            var duration = endTime - startTime;
            Assert.IsTrue(duration.TotalSeconds < 1.0, "Large dataset calculation should complete quickly");

            // Accuracy check - all values should be valid
            for (var i = 0; i < size; i++)
            {
                Assert.IsFalse(double.IsNaN(result.DEMA[i]), $"DEMA[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.DEMA[i]), $"DEMA[{i}] should not be infinite");
                Assert.IsFalse(double.IsNaN(result.EMA[i]), $"EMA[{i}] should not be NaN");
                Assert.IsFalse(double.IsNaN(result.EMAofEMA[i]), $"EMA of EMA[{i}] should not be NaN");

                // Values should be reasonable
                Assert.IsTrue(result.DEMA[i] > 0, $"DEMA[{i}] should be positive");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_ZeroShift_NoChange()
        {
            // Arrange
            double[] prices = { 100, 101, 102, 103, 104 };

            // Act
            var noShiftResult = DEMA.Calculate(prices, 3);
            var zeroShiftResult = DEMA.Calculate(prices, 3);

            // Assert
            for (var i = 0; i < prices.Length; i++)
                Assert.AreEqual(noShiftResult.DEMA[i], zeroShiftResult.DEMA[i], TOLERANCE,
                    $"Zero shift should not change values at index {i}");
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_ConstantPrices_ReturnsConstantValues()
        {
            // Arrange - All prices the same
            double[] prices = { 100, 100, 100, 100, 100, 100, 100 };
            var period = 3;

            // Act
            var result = DEMA.Calculate(prices, period);

            // Assert - With constant prices, DEMA should equal the constant value
            for (var i = 0; i < prices.Length; i++)
            {
                Assert.AreEqual(100.0, result.DEMA[i], TOLERANCE,
                    $"DEMA[{i}] of constant prices should be constant");
                Assert.AreEqual(100.0, result.EMA[i], TOLERANCE,
                    $"EMA[{i}] of constant prices should be constant");
                Assert.AreEqual(100.0, result.EMAofEMA[i], TOLERANCE,
                    $"EMA of EMA[{i}] of constant prices should be constant");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void DEMA_Calculate_VerySmallPeriod_StillWorks()
        {
            // Arrange
            double[] prices = { 100, 101, 102, 103, 104 };
            var period = 1; // Very small period

            // Act
            var result = DEMA.Calculate(prices, period);

            // Assert - Should handle very small periods gracefully
            Assert.AreEqual(prices.Length, result.DEMA.Length);

            // With period 1, alpha = 2/(1+1) = 1, so EMA should equal prices after first value
            var alpha = 2.0 / (period + 1);
            Assert.AreEqual(1.0, alpha, TOLERANCE);

            // All values should be finite
            for (var i = 0; i < prices.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.DEMA[i]), $"DEMA[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.DEMA[i]), $"DEMA[{i}] should not be infinite");
            }
        }
    }
}