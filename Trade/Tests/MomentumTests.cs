using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class MomentumTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_ReturnsCorrectLength()
        {
            // Arrange
            double[] prices = { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110 };
            var period = 5;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            Assert.AreEqual(prices.Length, result.Momentum.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_DefaultPeriod_Is14()
        {
            // Arrange
            var prices = CreateTestPrices(20);

            // Act
            var result = Momentum.Calculate(prices); // No period specified

            // Assert - First 14 values should be zero
            for (var i = 0; i < 14; i++)
                Assert.AreEqual(0.0, result.Momentum[i], TOLERANCE,
                    $"First {14} values should be zero when using default period");

            // Value at index 14 should be calculated
            Assert.AreNotEqual(0.0, result.Momentum[14], "Value at index 14 should be calculated");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_FirstPeriodValuesAreZero()
        {
            // Arrange
            double[] prices = { 100, 102, 104, 106, 108, 110, 112, 114, 116, 118 };
            var period = 4;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            for (var i = 0; i < period; i++)
                Assert.AreEqual(0.0, result.Momentum[i], TOLERANCE,
                    $"Momentum[{i}] should be zero (insufficient data)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_CalculatesCorrectValues()
        {
            // Arrange - Simple test case with known values
            double[] prices = { 100, 105, 110, 115, 120, 125, 130, 135, 140, 145 };
            var period = 4;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            // Momentum[4] = (120 / 100) * 100 = 120.0
            Assert.AreEqual(120.0, result.Momentum[4], TOLERANCE);

            // Momentum[5] = (125 / 105) * 100 = 119.047619...
            var expected5 = 125.0 / 105.0 * 100.0;
            Assert.AreEqual(expected5, result.Momentum[5], TOLERANCE);

            // Momentum[6] = (130 / 110) * 100 = 118.181818...
            var expected6 = 130.0 / 110.0 * 100.0;
            Assert.AreEqual(expected6, result.Momentum[6], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_HandlesDecreasingPrices()
        {
            // Arrange - Decreasing price series
            double[] prices = { 150, 145, 140, 135, 130, 125, 120, 115, 110, 105 };
            var period = 3;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            // Momentum[3] = (135 / 150) * 100 = 90.0
            Assert.AreEqual(90.0, result.Momentum[3], TOLERANCE);

            // Momentum[4] = (130 / 145) * 100 = 89.655172...
            var expected4 = 130.0 / 145.0 * 100.0;
            Assert.AreEqual(expected4, result.Momentum[4], TOLERANCE);

            // All calculated values should be less than 100 (bearish momentum)
            for (var i = period; i < prices.Length; i++)
                Assert.IsTrue(result.Momentum[i] < 100.0, $"Momentum[{i}] should be < 100 for declining prices");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_HandlesFlatPrices()
        {
            // Arrange - Constant prices (no momentum)
            double[] prices = { 100, 100, 100, 100, 100, 100, 100, 100 };
            var period = 3;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            for (var i = period; i < prices.Length; i++)
                Assert.AreEqual(100.0, result.Momentum[i], TOLERANCE,
                    $"Momentum[{i}] should be exactly 100.0 for flat prices");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_HandlesZeroPreviousPrice()
        {
            // Arrange - Zero price in the lookback period
            double[] prices = { 0, 100, 105, 110, 115, 120 };
            var period = 3;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            Assert.AreEqual(0.0, result.Momentum[3], TOLERANCE,
                "Momentum should be 0.0 when previous price is zero");

            // Momentum[4] = (115 / 100) * 100 = 115.0 (normal calculation)
            Assert.AreEqual(115.0, result.Momentum[4], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_WithPeriodOne_CalculatesCorrectly()
        {
            // Arrange
            double[] prices = { 100, 110, 105, 120, 95 };
            var period = 1;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            Assert.AreEqual(0.0, result.Momentum[0], TOLERANCE); // First value always zero

            // Momentum[1] = (110 / 100) * 100 = 110.0
            Assert.AreEqual(110.0, result.Momentum[1], TOLERANCE);

            // Momentum[2] = (105 / 110) * 100 = 95.454545...
            var expected2 = 105.0 / 110.0 * 100.0;
            Assert.AreEqual(expected2, result.Momentum[2], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_WithZeroOrNegativePeriod_UsesDefault()
        {
            // Arrange
            var prices = CreateTestPrices(20);

            // Act
            var resultZero = Momentum.Calculate(prices, 0);
            var resultNegative = Momentum.Calculate(prices, -5);
            var resultDefault = Momentum.Calculate(prices);

            // Assert
            for (var i = 0; i < prices.Length; i++)
            {
                Assert.AreEqual(resultDefault.Momentum[i], resultZero.Momentum[i], TOLERANCE,
                    "Zero period should use default period (14)");
                Assert.AreEqual(resultDefault.Momentum[i], resultNegative.Momentum[i], TOLERANCE,
                    "Negative period should use default period (14)");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_WithLargePeriod_HandlesGracefully()
        {
            // Arrange
            double[] prices = { 100, 105, 110 };
            var period = 10; // Larger than array length

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            for (var i = 0; i < prices.Length; i++)
                Assert.AreEqual(0.0, result.Momentum[i], TOLERANCE,
                    "All values should be zero when period > array length");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_WithEmptyArray_ReturnsEmptyResult()
        {
            // Arrange
            double[] prices = { };

            // Act
            var result = Momentum.Calculate(prices);

            // Assert
            Assert.AreEqual(0, result.Momentum.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_WithSingleElement_ReturnsZero()
        {
            // Arrange
            double[] prices = { 100.0 };
            var period = 5;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            Assert.AreEqual(1, result.Momentum.Length);
            Assert.AreEqual(0.0, result.Momentum[0], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_WithOscillatingPrices()
        {
            // Arrange - Oscillating price pattern
            double[] prices = { 100, 110, 95, 115, 90, 120, 85, 125 };
            var period = 2;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            Assert.AreEqual(0.0, result.Momentum[0], TOLERANCE);
            Assert.AreEqual(0.0, result.Momentum[1], TOLERANCE);

            // Momentum[2] = (95 / 100) * 100 = 95.0
            Assert.AreEqual(95.0, result.Momentum[2], TOLERANCE);

            // Momentum[3] = (115 / 110) * 100 = 104.545454...
            var expected3 = 115.0 / 110.0 * 100.0;
            Assert.AreEqual(expected3, result.Momentum[3], TOLERANCE);

            // Momentum[4] = (90 / 95) * 100 = 94.736842...
            var expected4 = 90.0 / 95.0 * 100.0;
            Assert.AreEqual(expected4, result.Momentum[4], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_WithHighVolatility()
        {
            // Arrange - High volatility scenario
            double[] prices = { 100, 150, 75, 200, 50, 250, 25, 300 };
            var period = 3;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            // Momentum[3] = (200 / 100) * 100 = 200.0
            Assert.AreEqual(200.0, result.Momentum[3], TOLERANCE);

            // Momentum[4] = (50 / 150) * 100 = 33.333333...
            var expected4 = 50.0 / 150.0 * 100.0;
            Assert.AreEqual(expected4, result.Momentum[4], TOLERANCE);

            // All calculated values should be valid numbers
            for (var i = period; i < prices.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.Momentum[i]), $"Momentum[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.Momentum[i]), $"Momentum[{i}] should not be infinite");
                Assert.IsTrue(result.Momentum[i] >= 0.0, $"Momentum[{i}] should be non-negative");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_LongSequence_ConsistentCalculation()
        {
            // Arrange - Test consistency over longer sequence
            var prices = CreateTrendingPrices(50, 100.0, 0.5); // 50 prices starting at 100, trending up
            var period = 10;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            // Verify each calculation manually for consistency
            for (var i = period; i < prices.Length; i++)
                if (prices[i - period] != 0)
                {
                    var expected = prices[i] / prices[i - period] * 100.0;
                    Assert.AreEqual(expected, result.Momentum[i], TOLERANCE,
                        $"Momentum calculation incorrect at index {i}");
                }
                else
                {
                    Assert.AreEqual(0.0, result.Momentum[i], TOLERANCE,
                        $"Momentum should be 0.0 when previous price is zero at index {i}");
                }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_RealisticMarketData()
        {
            // Arrange - Simulate realistic stock price movement
            double[] prices =
            {
                100.0, 101.5, 99.8, 102.3, 104.1, 103.7, 105.2, 104.8, 106.5, 108.2,
                107.9, 109.4, 108.1, 110.7, 112.3, 111.8, 113.5, 115.1, 114.6, 116.8
            };
            var period = 14;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            // First 14 values should be zero
            for (var i = 0; i < period; i++) Assert.AreEqual(0.0, result.Momentum[i], TOLERANCE);

            // Calculate expected values for verification
            Assert.AreEqual(112.3 / 100.0 * 100.0, result.Momentum[14], TOLERANCE);
            Assert.AreEqual(111.8 / 101.5 * 100.0, result.Momentum[15], TOLERANCE);
            Assert.AreEqual(113.5 / 99.8 * 100.0, result.Momentum[16], TOLERANCE);

            // All momentum values should be reasonable for this data set
            for (var i = period; i < prices.Length; i++)
                Assert.IsTrue(result.Momentum[i] > 90.0 && result.Momentum[i] < 130.0,
                    $"Momentum[{i}] = {result.Momentum[i]} should be in reasonable range for realistic data");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_VerySmallPrices()
        {
            // Arrange - Very small price values
            double[] prices = { 0.001, 0.0012, 0.0009, 0.0015, 0.0011, 0.0018 };
            var period = 3;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            // Momentum[3] = (0.0015 / 0.001) * 100 = 150.0
            Assert.AreEqual(150.0, result.Momentum[3], TOLERANCE);

            // Momentum[4] = (0.0011 / 0.0012) * 100 = 91.666666...
            var expected4 = 0.0011 / 0.0012 * 100.0;
            Assert.AreEqual(expected4, result.Momentum[4], TOLERANCE);

            // All values should be valid
            for (var i = period; i < prices.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.Momentum[i]));
                Assert.IsFalse(double.IsInfinity(result.Momentum[i]));
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_ExtremelyLargePrices()
        {
            // Arrange - Very large price values
            double[] prices = { 1000000, 1100000, 950000, 1200000, 1050000 };
            var period = 2;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            // Momentum[2] = (950000 / 1000000) * 100 = 95.0
            Assert.AreEqual(95.0, result.Momentum[2], TOLERANCE);

            // Momentum[3] = (1200000 / 1100000) * 100 = 109.090909...
            var expected3 = 1200000.0 / 1100000.0 * 100.0;
            Assert.AreEqual(expected3, result.Momentum[3], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_BullishMomentum_IdentifiesTrend()
        {
            // Arrange - Strong uptrend
            double[] prices = { 100, 105, 110, 115, 120, 125, 130, 135, 140, 145 };
            var period = 5;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            for (var i = period; i < prices.Length; i++)
                Assert.IsTrue(result.Momentum[i] > 100.0,
                    $"Bullish momentum should be > 100 at index {i}, got {result.Momentum[i]}");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Momentum_Calculate_BearishMomentum_IdentifiesTrend()
        {
            // Arrange - Strong downtrend
            double[] prices = { 145, 140, 135, 130, 125, 120, 115, 110, 105, 100 };
            var period = 5;

            // Act
            var result = Momentum.Calculate(prices, period);

            // Assert
            for (var i = period; i < prices.Length; i++)
                Assert.IsTrue(result.Momentum[i] < 100.0,
                    $"Bearish momentum should be < 100 at index {i}, got {result.Momentum[i]}");
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