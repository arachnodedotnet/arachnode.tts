using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class ROCTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_ReturnsCorrectLength()
        {
            // Arrange
            double[] prices = { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112 };
            var period = 5;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert
            Assert.AreEqual(prices.Length, result.ROC.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_DefaultPeriod_Is12()
        {
            // Arrange
            var prices = CreateTestPrices(20);

            // Act
            var result = ROC.Calculate(prices); // No period specified

            // Assert - First 12 values should be zero
            for (var i = 0; i < 12; i++)
                Assert.AreEqual(0.0, result.ROC[i], TOLERANCE,
                    $"First {12} values should be zero when using default period");

            // Value at index 12 should be calculated
            Assert.AreNotEqual(0.0, result.ROC[12], "Value at index 12 should be calculated");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_FirstPeriodValuesAreZero()
        {
            // Arrange
            double[] prices = { 100, 102, 104, 106, 108, 110, 112, 114, 116, 118 };
            var period = 4;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert
            for (var i = 0; i < period; i++)
                Assert.AreEqual(0.0, result.ROC[i], TOLERANCE,
                    $"ROC[{i}] should be zero (insufficient data)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_CorrectFormula_StandardROC()
        {
            // Arrange - Test the corrected standard ROC formula
            double[] prices = { 100, 110, 120, 130, 140 };
            var period = 2;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert - Standard ROC formula: (prices[i] - prices[i-period]) / prices[i-period] * 100
            Assert.AreEqual(0.0, result.ROC[0], TOLERANCE);
            Assert.AreEqual(0.0, result.ROC[1], TOLERANCE);

            // ROC[2] = (120 - 100) / 100 * 100 = 20.0
            Assert.AreEqual(20.0, result.ROC[2], TOLERANCE);

            // ROC[3] = (130 - 110) / 110 * 100 = 18.181818...
            var expected3 = (130.0 - 110.0) / 110.0 * 100.0;
            Assert.AreEqual(expected3, result.ROC[3], TOLERANCE);

            // ROC[4] = (140 - 120) / 120 * 100 = 16.666666...
            var expected4 = (140.0 - 120.0) / 120.0 * 100.0;
            Assert.AreEqual(expected4, result.ROC[4], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_HandlesZeroPreviousPrice()
        {
            // Arrange - Zero previous price should be handled correctly
            double[] prices = { 0, 110, 120, 130, 140 };
            var period = 2;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert
            Assert.AreEqual(0.0, result.ROC[2], TOLERANCE,
                "ROC should be 0.0 when previous price is zero (avoids division by zero)");

            // ROC[3] = (130 - 110) / 110 * 100 = 18.181818... (normal calculation)
            var expected3 = (130.0 - 110.0) / 110.0 * 100.0;
            Assert.AreEqual(expected3, result.ROC[3], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_HandlesZeroCurrentPrice()
        {
            // Arrange - Zero current price should calculate normally
            double[] prices = { 100, 110, 0, 130, 140 };
            var period = 2;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert
            // ROC[2] = (0 - 100) / 100 * 100 = -100.0 (100% decline)
            Assert.AreEqual(-100.0, result.ROC[2], TOLERANCE,
                "ROC should be -100% when current price drops to zero");

            // ROC[3] = (130 - 110) / 110 * 100 = 18.181818... (normal calculation)
            var expected3 = (130.0 - 110.0) / 110.0 * 100.0;
            Assert.AreEqual(expected3, result.ROC[3], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_WithFlatPrices()
        {
            // Arrange - Constant prices (should give zero rate of change)
            double[] prices = { 100, 100, 100, 100, 100, 100, 100 };
            var period = 3;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert
            for (var i = period; i < prices.Length; i++)
                Assert.AreEqual(0.0, result.ROC[i], TOLERANCE,
                    $"ROC[{i}] should be 0.0 for flat prices");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_WithZeroOrNegativePeriod_UsesDefault()
        {
            // Arrange
            var prices = CreateTestPrices(20);

            // Act
            var resultZero = ROC.Calculate(prices, 0);
            var resultNegative = ROC.Calculate(prices, -5);
            var resultDefault = ROC.Calculate(prices);

            // Assert
            for (var i = 0; i < prices.Length; i++)
            {
                Assert.AreEqual(resultDefault.ROC[i], resultZero.ROC[i], TOLERANCE,
                    "Zero period should use default period (12)");
                Assert.AreEqual(resultDefault.ROC[i], resultNegative.ROC[i], TOLERANCE,
                    "Negative period should use default period (12)");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_WithLargePeriod_HandlesGracefully()
        {
            // Arrange
            double[] prices = { 100, 105, 110 };
            var period = 10; // Larger than array length

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert
            for (var i = 0; i < prices.Length; i++)
                Assert.AreEqual(0.0, result.ROC[i], TOLERANCE,
                    "All values should be zero when period > array length");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_WithEmptyArray_ReturnsEmptyResult()
        {
            // Arrange
            double[] prices = { };

            // Act
            var result = ROC.Calculate(prices);

            // Assert
            Assert.AreEqual(0, result.ROC.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_WithSingleElement_ReturnsZero()
        {
            // Arrange
            double[] prices = { 100.0 };
            var period = 5;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert
            Assert.AreEqual(1, result.ROC.Length);
            Assert.AreEqual(0.0, result.ROC[0], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_WithIncreasingPrices_StandardFormula()
        {
            // Arrange
            double[] prices = { 100, 105, 110, 115, 120, 125, 130 };
            var period = 3;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert - Standard ROC formula
            Assert.AreEqual(0.0, result.ROC[0], TOLERANCE);
            Assert.AreEqual(0.0, result.ROC[1], TOLERANCE);
            Assert.AreEqual(0.0, result.ROC[2], TOLERANCE);

            // ROC[3] = (115 - 100) / 100 * 100 = 15.0
            Assert.AreEqual(15.0, result.ROC[3], TOLERANCE);

            // ROC[4] = (120 - 105) / 105 * 100 = 14.285714...
            var expected4 = (120.0 - 105.0) / 105.0 * 100.0;
            Assert.AreEqual(expected4, result.ROC[4], TOLERANCE);

            // All calculated values should be positive for increasing prices
            for (var i = period; i < prices.Length; i++)
                Assert.IsTrue(result.ROC[i] > 0.0, $"ROC[{i}] should be positive for increasing prices");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_WithDecreasingPrices_StandardFormula()
        {
            // Arrange
            double[] prices = { 130, 125, 120, 115, 110, 105, 100 };
            var period = 3;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert - Standard ROC formula
            // ROC[3] = (115 - 130) / 130 * 100 = -11.538461...
            var expected3 = (115.0 - 130.0) / 130.0 * 100.0;
            Assert.AreEqual(expected3, result.ROC[3], TOLERANCE);

            // All calculated values should be negative for decreasing prices
            for (var i = period; i < prices.Length; i++)
                Assert.IsTrue(result.ROC[i] < 0.0, $"ROC[{i}] should be negative for decreasing prices");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_WithOscillatingPrices()
        {
            // Arrange
            double[] prices = { 100, 110, 95, 115, 90, 120, 85, 125 };
            var period = 2;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert
            Assert.AreEqual(0.0, result.ROC[0], TOLERANCE);
            Assert.AreEqual(0.0, result.ROC[1], TOLERANCE);

            // ROC[2] = (95 - 100) / 100 * 100 = -5.0
            Assert.AreEqual(-5.0, result.ROC[2], TOLERANCE);

            // ROC[3] = (115 - 110) / 110 * 100 = 4.545454...
            var expected3 = (115.0 - 110.0) / 110.0 * 100.0;
            Assert.AreEqual(expected3, result.ROC[3], TOLERANCE);

            // Values should alternate positive/negative with oscillating prices
            Assert.IsTrue(result.ROC[2] < 0.0, "ROC[2] should be negative");
            Assert.IsTrue(result.ROC[3] > 0.0, "ROC[3] should be positive");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_KnownExamples_100PercentIncrease()
        {
            // Arrange - Classic example: price doubles
            double[] prices = { 100, 200 };
            var period = 1;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert
            Assert.AreEqual(0.0, result.ROC[0], TOLERANCE);
            // ROC[1] = (200 - 100) / 100 * 100 = 100.0 (100% increase)
            Assert.AreEqual(100.0, result.ROC[1], TOLERANCE, "Price doubling should give 100% ROC");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_KnownExamples_50PercentDecrease()
        {
            // Arrange - Price drops by half
            double[] prices = { 200, 100 };
            var period = 1;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert
            Assert.AreEqual(0.0, result.ROC[0], TOLERANCE);
            // ROC[1] = (100 - 200) / 200 * 100 = -50.0 (50% decrease)
            Assert.AreEqual(-50.0, result.ROC[1], TOLERANCE, "Price halving should give -50% ROC");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_LongSequence_ConsistentCalculation()
        {
            // Arrange
            var prices = CreateTrendingPrices(30, 100.0, 0.5);
            var period = 10;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert - Verify each calculation manually for standard formula
            for (var i = period; i < prices.Length; i++)
                if (prices[i - period] != 0.0)
                {
                    var expected = (prices[i] - prices[i - period]) / prices[i - period] * 100.0;
                    Assert.AreEqual(expected, result.ROC[i], TOLERANCE,
                        $"ROC calculation incorrect at index {i} (standard formula)");
                }
                else
                {
                    Assert.AreEqual(0.0, result.ROC[i], TOLERANCE,
                        $"ROC should be 0.0 when previous price is zero at index {i}");
                }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_RealisticMarketData()
        {
            // Arrange - Realistic stock price movements
            double[] prices =
            {
                100.0, 101.5, 99.8, 102.3, 104.1, 103.7, 105.2, 104.8, 106.5, 108.2,
                107.9, 109.4, 108.1, 110.7, 112.3
            };
            var period = 10;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert
            for (var i = 0; i < period; i++) Assert.AreEqual(0.0, result.ROC[i], TOLERANCE);

            // ROC[10] = (107.9 - 100.0) / 100.0 * 100 = 7.9
            Assert.AreEqual(7.9, result.ROC[10], TOLERANCE);

            // ROC[11] = (109.4 - 101.5) / 101.5 * 100 = 7.783251...
            var expected11 = (109.4 - 101.5) / 101.5 * 100.0;
            Assert.AreEqual(expected11, result.ROC[11], TOLERANCE);

            // Check all values are reasonable for realistic data
            for (var i = period; i < prices.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.ROC[i]), $"ROC[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.ROC[i]), $"ROC[{i}] should not be infinite");
                Assert.IsTrue(Math.Abs(result.ROC[i]) < 50.0,
                    $"ROC[{i}] = {result.ROC[i]} should be within reasonable bounds for realistic data");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_VerySmallPrices()
        {
            // Arrange
            double[] prices = { 0.001, 0.0012, 0.0009, 0.0015, 0.0011 };
            var period = 2;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert - Standard formula: (prices[i] - prices[i-period]) / prices[i-period] * 100
            // ROC[2] = (0.0009 - 0.001) / 0.001 * 100 = -10.0
            Assert.AreEqual(-10.0, result.ROC[2], TOLERANCE);

            // ROC[3] = (0.0015 - 0.0012) / 0.0012 * 100 = 25.0
            Assert.AreEqual(25.0, result.ROC[3], TOLERANCE);

            // All values should be valid
            for (var i = period; i < prices.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.ROC[i]));
                Assert.IsFalse(double.IsInfinity(result.ROC[i]));
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_HighVolatility()
        {
            // Arrange - High volatility scenario
            double[] prices = { 100, 150, 75, 200, 50, 250 };
            var period = 3;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert - Standard formula
            // ROC[3] = (200 - 100) / 100 * 100 = 100.0
            Assert.AreEqual(100.0, result.ROC[3], TOLERANCE);

            // ROC[4] = (50 - 150) / 150 * 100 = -66.666666...
            var expected4 = (50.0 - 150.0) / 150.0 * 100.0;
            Assert.AreEqual(expected4, result.ROC[4], TOLERANCE);

            // ROC[5] = (250 - 75) / 75 * 100 = 233.333333...
            var expected5 = (250.0 - 75.0) / 75.0 * 100.0;
            Assert.AreEqual(expected5, result.ROC[5], TOLERANCE);

            // All calculated values should be valid numbers
            for (var i = period; i < prices.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.ROC[i]), $"ROC[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.ROC[i]), $"ROC[{i}] should not be infinite");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_ComparisonWithMomentum()
        {
            // Arrange - ROC should be related to Momentum indicator
            // Momentum = (Current / Previous) * 100
            // ROC = ((Current - Previous) / Previous) * 100 = (Current/Previous - 1) * 100 = Momentum - 100
            double[] prices = { 100, 110, 120, 130 };
            var period = 2;

            // Act
            var result = ROC.Calculate(prices, period);

            // Assert
            // ROC[2] = (120 - 100) / 100 * 100 = 20.0
            Assert.AreEqual(20.0, result.ROC[2], TOLERANCE);

            // If Momentum[2] = (120 / 100) * 100 = 120.0, then ROC[2] = 120.0 - 100.0 = 20.0
            var momentumValue = 120.0 / 100.0 * 100.0; // 120.0
            var expectedROC = momentumValue - 100.0; // 20.0
            Assert.AreEqual(expectedROC, result.ROC[2], TOLERANCE, "ROC = Momentum - 100");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ROC_Calculate_BullishAndBearishTrends()
        {
            // Arrange - Test trend identification
            double[] bullishPrices = { 100, 105, 110, 115, 120 }; // Consistent uptrend
            double[] bearishPrices = { 120, 115, 110, 105, 100 }; // Consistent downtrend
            var period = 2;

            // Act
            var bullishResult = ROC.Calculate(bullishPrices, period);
            var bearishResult = ROC.Calculate(bearishPrices, period);

            // Assert - Bullish trend should have positive ROC values
            for (var i = period; i < bullishPrices.Length; i++)
                Assert.IsTrue(bullishResult.ROC[i] > 0.0,
                    $"Bullish ROC[{i}] should be positive, got {bullishResult.ROC[i]}");

            // Bearish trend should have negative ROC values
            for (var i = period; i < bearishPrices.Length; i++)
                Assert.IsTrue(bearishResult.ROC[i] < 0.0,
                    $"Bearish ROC[{i}] should be negative, got {bearishResult.ROC[i]}");
        }

        #region Helper Methods

        private double[] CreateTestPrices(int count)
        {
            var prices = new double[count];
            var basePrice = 100.0;

            for (var i = 0; i < count; i++) prices[i] = basePrice + Math.Sin(i * 0.1) * 5; // Oscillating around 100

            return prices;
        }

        private double[] CreateTrendingPrices(int count, double startPrice, double trendRate)
        {
            var prices = new double[count];

            for (var i = 0; i < count; i++) prices[i] = startPrice + i * trendRate;

            return prices;
        }

        #endregion
    }
}