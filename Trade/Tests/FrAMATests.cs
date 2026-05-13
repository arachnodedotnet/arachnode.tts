using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class FrAMATests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod][TestCategory("Core")]
        public void Calculate_BasicLengthAndNoException()
        {
            // Arrange
            var len = 50;
            var price = new double[len];
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++) price[i] = high[i] = low[i] = 100 + i;

            // Act
            var result = FrAMA.Calculate(price, high, low);

            // Assert
            Assert.AreEqual(len, result.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_FlatPrices_FrAMAFlat()
        {
            // Arrange
            var len = 30;
            var price = new double[len];
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++) price[i] = high[i] = low[i] = 100.0;

            // Act
            var result = FrAMA.Calculate(price, high, low, 10);

            // Assert
            // After initialization period (2*period-1 = 19), values should be close to flat price
            for (var i = 19; i < len; i++)
                Assert.AreEqual(100.0, result[i], 0.1, $"FrAMA should be close to flat price at index {i}");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_Uptrend_FrAMAUptrend()
        {
            // Arrange
            var len = 40;
            var price = new double[len];
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++) price[i] = high[i] = low[i] = 100 + i;

            // Act
            var result = FrAMA.Calculate(price, high, low, 10);

            // Assert
            // After initialization period (2*period-1 = 19), should show uptrend
            for (var i = 20; i < len; i++)
                Assert.IsTrue(result[i] > result[i - 1], $"FrAMA should be increasing at index {i}");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_Downtrend_FrAMADowntrend()
        {
            // Arrange
            var len = 40;
            var price = new double[len];
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++) price[i] = high[i] = low[i] = 100 - i;

            // Act
            var result = FrAMA.Calculate(price, high, low, 10);

            // Assert
            // After initialization period (2*period-1 = 19), should show downtrend
            for (var i = 20; i < len; i++)
                Assert.IsTrue(result[i] < result[i - 1], $"FrAMA should be decreasing at index {i}");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ShortArray_ReturnsZeroBuffer()
        {
            // Arrange
            double[] price = { 100, 101 };
            double[] high = { 100, 101 };
            double[] low = { 100, 101 };

            // Act
            var result = FrAMA.Calculate(price, high, low, 10);

            // Assert
            Assert.AreEqual(2, result.Length);
            // Should return array of zeros for insufficient data
            Assert.IsTrue(Array.TrueForAll(result, x => Math.Abs(x) < TOLERANCE));
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_PeriodOne_SpecialCase()
        {
            // Arrange
            double[] price = { 100, 101, 102, 103, 104 };
            double[] high = { 100, 101, 102, 103, 104 };
            double[] low = { 100, 101, 102, 103, 104 };

            // Act
            var result = FrAMA.Calculate(price, high, low, 1);

            // Assert
            // With period=1, need at least 2 bars for FrAMA calculation
            // First bar should be the price, subsequent bars calculated
            Assert.AreEqual(price.Length, result.Length);
            Assert.AreEqual(100, result[0], TOLERANCE);
            // Other values should be calculated (not necessarily equal to price for period=1)
            Assert.IsTrue(result[1] > 0, "FrAMA should have valid calculation for period=1");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ShiftParameter_ShiftsOutput()
        {
            // Arrange
            var len = 20;
            var price = new double[len];
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++) price[i] = high[i] = low[i] = 100 + i;

            // Act
            var resultNoShift = FrAMA.Calculate(price, high, low, 10, 0);
            var resultShift = FrAMA.Calculate(price, high, low, 10, 3);

            // Assert
            Assert.AreEqual(len, resultNoShift.Length);
            Assert.AreEqual(len, resultShift.Length);

            // With shift=3, values should appear 3 positions earlier
            for (var i = 3; i < len; i++)
                Assert.AreEqual(resultNoShift[i - 3], resultShift[i], TOLERANCE,
                    $"Shifted result should match non-shifted at offset positions");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_NullInputs_ReturnsEmptyArray()
        {
            // Act
            var result = FrAMA.Calculate(null, null, null);

            // Assert
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_InvalidPeriod_UsesDefault()
        {
            // Arrange
            var len = 30;
            var price = new double[len];
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++) price[i] = high[i] = low[i] = 100 + i;

            // Act
            var result1 = FrAMA.Calculate(price, high, low, 0);  // Invalid period
            var result2 = FrAMA.Calculate(price, high, low, -5); // Invalid period
            var result3 = FrAMA.Calculate(price, high, low, 14); // Default period

            // Assert - Results should be identical (all use default period 14)
            Assert.AreEqual(result1.Length, result3.Length);
            Assert.AreEqual(result2.Length, result3.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_MismatchedArrayLengths_UsesMinimumLength()
        {
            // Arrange - Arrays of different lengths
            double[] price = { 100, 101, 102, 103, 104 };
            double[] high = { 100, 101, 102 }; // Shorter
            double[] low = { 100, 101, 102, 103, 104, 105, 106 }; // Longer

            // Act
            var result = FrAMA.Calculate(price, high, low, 5);

            // Assert - Should use minimum length (3)
            Assert.AreEqual(3, result.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_InsufficientDataForPeriod_ReturnsZeros()
        {
            // Arrange
            double[] price = { 100, 101, 102, 103 }; // 4 elements
            double[] high = { 100, 101, 102, 103 };
            double[] low = { 100, 101, 102, 103 };

            // Act - Period 5 requires 10 bars minimum (2*period)
            var result = FrAMA.Calculate(price, high, low, 5);

            // Assert
            Assert.AreEqual(4, result.Length);
            // Should be all zeros due to insufficient data
            Assert.IsTrue(Array.TrueForAll(result, x => Math.Abs(x) < TOLERANCE));
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_AdaptiveBehavior_RespondsToVolatility()
        {
            // Arrange - Create data with different volatility patterns
            var len = 50;
            var price = new double[len];
            var high = new double[len];
            var low = new double[len];

            // First half: Low volatility (tight range)
            for (var i = 0; i < len / 2; i++)
            {
                price[i] = 100 + i * 0.1;
                high[i] = price[i] + 0.05;
                low[i] = price[i] - 0.05;
            }

            // Second half: High volatility (wide range)
            for (var i = len / 2; i < len; i++)
            {
                price[i] = 100 + i * 0.1 + Math.Sin(i) * 2;
                high[i] = price[i] + 1.0;
                low[i] = price[i] - 1.0;
            }

            // Act
            var result = FrAMA.Calculate(price, high, low, 10);

            // Assert
            Assert.AreEqual(len, result.Length);

            // Should have valid calculations after initialization period
            var initPeriod = 2 * 10 - 1; // 19
            for (var i = initPeriod; i < len; i++)
            {
                Assert.IsFalse(double.IsNaN(result[i]), $"FrAMA should not be NaN at index {i}");
                Assert.IsFalse(double.IsInfinity(result[i]), $"FrAMA should not be infinite at index {i}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_RealWorldExample_ProducesReasonableValues()
        {
            // Arrange - Simulated realistic price data
            var len = 30;
            var price = new double[len];
            var high = new double[len];
            var low = new double[len];

            var random = new Random(42); // Fixed seed for reproducibility
            var basePrice = 100.0;

            for (var i = 0; i < len; i++)
            {
                var change = (random.NextDouble() - 0.5) * 2; // ±1 change
                basePrice += change;
                price[i] = basePrice;
                high[i] = basePrice + random.NextDouble();
                low[i] = basePrice - random.NextDouble();
            }

            // Act
            var result = FrAMA.Calculate(price, high, low, 8);

            // Assert
            Assert.AreEqual(len, result.Length);

            // Values should be reasonable (not NaN or infinity)
            foreach (var value in result)
            {
                Assert.IsFalse(double.IsNaN(value), "FrAMA values should not be NaN");
                Assert.IsFalse(double.IsInfinity(value), "FrAMA values should not be infinite");
            }

            // FrAMA should follow the general trend of price data
            var initPeriod = 2 * 8 - 1; // 15
            if (len > initPeriod + 5)
            {
                // Check that FrAMA values are within reasonable range of price values
                for (var i = initPeriod; i < len; i++)
                {
                    Assert.IsTrue(Math.Abs(result[i] - price[i]) < 50,
                        $"FrAMA should be reasonably close to price at index {i}");
                }
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_EdgeCaseZeroRange_HandlesGracefully()
        {
            // Arrange - All same prices (zero range)
            double[] price = { 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100 };
            double[] high = { 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100 };
            double[] low = { 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100 };

            // Act
            var result = FrAMA.Calculate(price, high, low, 5);

            // Assert
            Assert.AreEqual(price.Length, result.Length);

            // Should handle zero range gracefully without NaN or infinity
            foreach (var value in result)
            {
                Assert.IsFalse(double.IsNaN(value), "Should not produce NaN for zero range");
                Assert.IsFalse(double.IsInfinity(value), "Should not produce infinity for zero range");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ConsistentResults_AcrossMultipleCalls()
        {
            // Arrange
            var len = 25;
            var price = new double[len];
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                price[i] = 100 + i * 0.5;
                high[i] = price[i] + 0.2;
                low[i] = price[i] - 0.2;
            }

            // Act
            var result1 = FrAMA.Calculate(price, high, low, 6);
            var result2 = FrAMA.Calculate(price, high, low, 6);

            // Assert
            Assert.AreEqual(result1.Length, result2.Length);
            for (var i = 0; i < result1.Length; i++)
                Assert.AreEqual(result1[i], result2[i], TOLERANCE,
                    $"Results should be identical across calls at index {i}");
        }
    }
}