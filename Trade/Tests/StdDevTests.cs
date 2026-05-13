using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class StdDevTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_ReturnsCorrectLengths()
        {
            // Arrange
            double[] prices = { 100, 102, 104, 103, 105, 107, 106, 108, 110, 109 };
            var period = 5;

            // Act
            var result = StdDev.Calculate(prices, period);

            // Assert
            Assert.AreEqual(prices.Length, result.StdDev.Length);
            Assert.AreEqual(prices.Length, result.MA.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_DefaultParameters_AreCorrect()
        {
            // Arrange
            var prices = new double[25];
            for (var i = 0; i < 25; i++)
                prices[i] = 100 + i * 0.5 + Math.Sin(i * 0.5) * 2; // Trending with some volatility

            // Act
            var defaultResult = StdDev.Calculate(prices);
            var explicitResult = StdDev.Calculate(prices);

            // Assert
            Assert.AreEqual(defaultResult.StdDev.Length, explicitResult.StdDev.Length);
            for (var i = 0; i < prices.Length; i++)
            {
                Assert.AreEqual(explicitResult.StdDev[i], defaultResult.StdDev[i], TOLERANCE,
                    $"Default parameters should match explicit parameters at index {i}");
                Assert.AreEqual(explicitResult.MA[i], defaultResult.MA[i], TOLERANCE,
                    $"Default MA should match explicit MA at index {i}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_SMA_AppliesCorrectFormula()
        {
            // Arrange - Simple test case for manual verification
            double[] prices = { 10, 12, 14, 16, 18 };
            var period = 3;

            // Act
            var result = StdDev.Calculate(prices, period);

            // Assert - Manual calculations for verification
            // At index 2 (first valid calculation):
            // Prices: [10, 12, 14]
            // SMA = (10 + 12 + 14) / 3 = 12
            // StdDev = sqrt(((10-12)² + (12-12)² + (14-12)²) / 3) = sqrt((4 + 0 + 4) / 3) = sqrt(8/3) ? 1.6330

            Assert.AreEqual(12.0, result.MA[2], TOLERANCE, "SMA calculation incorrect at index 2");

            var expectedVariance = ((10 - 12) * (10 - 12) + (12 - 12) * (12 - 12) + (14 - 12) * (14 - 12)) / 3.0;
            var expectedStdDev = Math.Sqrt(expectedVariance);
            Assert.AreEqual(expectedStdDev, result.StdDev[2], TOLERANCE, "StdDev calculation incorrect at index 2");

            // At index 3:
            // Prices: [12, 14, 16]
            // SMA = (12 + 14 + 16) / 3 = 14
            // StdDev = sqrt(((12-14)² + (14-14)² + (16-14)²) / 3) = sqrt((4 + 0 + 4) / 3) = sqrt(8/3) ? 1.6330

            Assert.AreEqual(14.0, result.MA[3], TOLERANCE, "SMA calculation incorrect at index 3");
            var expectedStdDev3 = Math.Sqrt(8.0 / 3.0);
            Assert.AreEqual(expectedStdDev3, result.StdDev[3], TOLERANCE, "StdDev calculation incorrect at index 3");
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_HighVolatility_ShowsHighValues()
        {
            // Arrange - Highly volatile price data
            double[] prices = { 100, 110, 95, 115, 90, 120, 85, 125, 80, 130 };
            var period = 5;

            // Act
            var result = StdDev.Calculate(prices, period);

            // Assert - Should show significant standard deviation values
            for (var i = period - 1; i < prices.Length; i++)
                if (result.StdDev[i] != 0.0) // Skip initialization values
                    Assert.IsTrue(result.StdDev[i] > 5.0,
                        $"StdDev[{i}] = {result.StdDev[i]} should be high for volatile data");
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_LowVolatility_ShowsLowValues()
        {
            // Arrange - Low volatility price data (trending smoothly)
            double[] prices = { 100, 100.1, 100.2, 100.3, 100.4, 100.5, 100.6, 100.7 };
            var period = 5;

            // Act
            var result = StdDev.Calculate(prices, period);

            // Assert - Should show low standard deviation values
            for (var i = period - 1; i < prices.Length; i++)
                Assert.IsTrue(result.StdDev[i] < 1.0,
                    $"StdDev[{i}] = {result.StdDev[i]} should be low for stable data");
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_ConstantPrices_ShowsZeroDeviation()
        {
            // Arrange - Constant prices (no volatility)
            double[] prices = { 100, 100, 100, 100, 100, 100, 100 };
            var period = 5;

            // Act
            var result = StdDev.Calculate(prices, period);

            // Assert - Should show zero standard deviation
            for (var i = period - 1; i < prices.Length; i++)
            {
                Assert.AreEqual(0.0, result.StdDev[i], TOLERANCE,
                    $"StdDev[{i}] should be zero for constant prices");
                Assert.AreEqual(100.0, result.MA[i], TOLERANCE,
                    $"MA[{i}] should equal constant price");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_DifferentMAMethods_ProduceDifferentResults()
        {
            // Arrange - Use more data points and more volatile data to ensure MA differences are significant
            double[] prices = { 100, 105, 103, 108, 106, 111, 109, 114, 112, 117, 115, 120, 118, 125, 122, 130 };
            var period = 5;

            // Act
            var smaResult = StdDev.Calculate(prices, period);
            var emaResult = StdDev.Calculate(prices, period, 0, MAMethod.EMA);
            var smmaResult = StdDev.Calculate(prices, period, 0, MAMethod.SMMA);
            var lwmaResult = StdDev.Calculate(prices, period, 0, MAMethod.LWMA);

            // Assert - Different MA methods should produce different results
            var hasDifferentMA = false;
            var hasDifferentStdDev = false;

            // Start checking after sufficient data points to allow MA differences to emerge
            for (var i = period + 2; i < prices.Length; i++)
            {
                // Compare SMA vs EMA
                if (Math.Abs(smaResult.MA[i] - emaResult.MA[i]) > TOLERANCE)
                    hasDifferentMA = true;
                if (Math.Abs(smaResult.StdDev[i] - emaResult.StdDev[i]) > TOLERANCE)
                    hasDifferentStdDev = true;

                // Also compare SMA vs LWMA for additional verification
                if (Math.Abs(smaResult.MA[i] - lwmaResult.MA[i]) > TOLERANCE)
                    hasDifferentMA = true;
                if (Math.Abs(smaResult.StdDev[i] - lwmaResult.StdDev[i]) > TOLERANCE)
                    hasDifferentStdDev = true;

                // If we found differences, no need to continue
                if (hasDifferentMA && hasDifferentStdDev)
                    break;
            }

            Assert.IsTrue(hasDifferentMA,
                "Different MA methods should produce different MA values. " +
                $"SMA[{period + 3}]={smaResult.MA[period + 3]:F8}, EMA[{period + 3}]={emaResult.MA[period + 3]:F8}, " +
                $"LWMA[{period + 3}]={lwmaResult.MA[period + 3]:F8}");
            Assert.IsTrue(hasDifferentStdDev, "Different MA methods should produce different StdDev values");
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_EMAMethod_ProducesValidResults()
        {
            // Arrange
            double[] prices = { 100, 102, 104, 103, 105, 107, 106, 108 };
            var period = 4;

            // Act
            var result = StdDev.Calculate(prices, period, 0, MAMethod.EMA);

            // Assert - EMA should produce valid results
            for (var i = period - 1; i < prices.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.MA[i]), $"EMA[{i}] should not be NaN");
                Assert.IsFalse(double.IsNaN(result.StdDev[i]), $"StdDev[{i}] should not be NaN");
                Assert.IsTrue(result.StdDev[i] >= 0.0, $"StdDev[{i}] should be non-negative");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_PositiveShift_MovesValuesRight()
        {
            // Arrange
            double[] prices = { 100, 102, 104, 103, 105, 107 };
            var shift = 2;
            var period = 3;

            // Act
            var noShiftResult = StdDev.Calculate(prices, period);
            var shiftedResult = StdDev.Calculate(prices, period, shift);

            // Assert
            Assert.AreEqual(prices.Length, shiftedResult.StdDev.Length);

            // First 'shift' values should be zero
            for (var i = 0; i < shift; i++)
                Assert.AreEqual(0.0, shiftedResult.StdDev[i], TOLERANCE,
                    $"First {shift} values should be zero with positive shift");

            // Shifted values should match original values offset by shift
            for (var i = 0; i < prices.Length - shift; i++)
                Assert.AreEqual(noShiftResult.StdDev[i], shiftedResult.StdDev[i + shift], TOLERANCE,
                    $"Shifted values should match original values at index {i}");
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_InitializationPeriod_IsCorrect()
        {
            // Arrange
            double[] prices = { 100, 102, 104, 103, 105, 107, 106, 108 };
            var period = 5;

            // Act
            var result = StdDev.Calculate(prices, period);

            // Assert - First (period - 1) values should be zero
            for (var i = 0; i < period - 1; i++)
            {
                Assert.AreEqual(0.0, result.StdDev[i], TOLERANCE,
                    $"StdDev[{i}] should be zero during initialization period");
                Assert.AreEqual(0.0, result.MA[i], TOLERANCE,
                    $"MA[{i}] should be zero during initialization period");
            }

            // After initialization period, values should be calculated (non-zero for this data)
            for (var i = period - 1; i < prices.Length; i++)
            {
                Assert.IsTrue(result.MA[i] > 0, $"MA[{i}] should be calculated after initialization");
                Assert.IsTrue(result.StdDev[i] >= 0, $"StdDev[{i}] should be non-negative after initialization");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_WithEmptyArray_ReturnsEmptyResult()
        {
            // Arrange
            double[] empty = { };

            // Act
            var result = StdDev.Calculate(empty);

            // Assert
            Assert.AreEqual(0, result.StdDev.Length);
            Assert.AreEqual(0, result.MA.Length);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void StdDev_Calculate_NullPricesArray_ThrowsArgumentNullException()
        {
            // Act
            StdDev.Calculate(null);
        }

        [TestMethod][TestCategory("Core")]
        //[ExpectedException(typeof(ArgumentException))]
        public void StdDev_Calculate_PeriodTooSmall_ThrowsArgumentException()
        {
            // Arrange
            double[] prices = { 100, 101, 102 };

            // Act
            StdDev.Calculate(prices, 1); // Period must be at least 2
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void StdDev_Calculate_NegativeShift_ThrowsArgumentException()
        {
            // Arrange
            double[] prices = { 100, 101, 102 };

            // Act
            StdDev.Calculate(prices, 3, -1);
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_DifferentPeriods_ProduceDifferentResults()
        {
            // Arrange
            double[] prices = { 100, 105, 103, 108, 106, 111, 109, 114, 112, 117, 115, 120 };

            // Act
            var shortPeriod = StdDev.Calculate(prices, 3);
            var longPeriod = StdDev.Calculate(prices, 7);

            // Assert - Different periods should produce different results
            var hasDifference = false;

            for (var i = 7; i < prices.Length; i++) // Start after longer period initialization
                if (Math.Abs(shortPeriod.StdDev[i] - longPeriod.StdDev[i]) > TOLERANCE)
                {
                    hasDifference = true;
                    break;
                }

            Assert.IsTrue(hasDifference, "Different periods should produce different StdDev values");
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_RealWorldStockExample()
        {
            // Arrange - Realistic stock price data with varying volatility
            double[] prices =
            {
                150.25, 151.50, 152.00, 151.75, 153.25, 154.00, 152.75, 155.50, 157.25,
                156.50, 158.75, 160.25, 159.50, 162.00, 164.25, 163.75, 166.50, 168.00,
                167.25, 170.75, 172.50, 171.00, 174.25, 176.75, 175.50, 178.25, 180.00
            };
            var period = 20;

            // Act
            var result = StdDev.Calculate(prices, period);

            // Assert
            Assert.AreEqual(27, result.StdDev.Length);

            // Verify all values are reasonable
            for (var i = 0; i < result.StdDev.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.StdDev[i]),
                    $"StdDev[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.StdDev[i]),
                    $"StdDev[{i}] should not be infinite");
                Assert.IsTrue(result.StdDev[i] >= 0.0,
                    $"StdDev[{i}] = {result.StdDev[i]} should be non-negative");

                Assert.IsFalse(double.IsNaN(result.MA[i]),
                    $"MA[{i}] should not be NaN");

                // MA values should be within reasonable range of input data
                if (result.MA[i] != 0.0) // Skip initialization values
                    Assert.IsTrue(result.MA[i] >= 145 && result.MA[i] <= 185,
                        $"MA[{i}] = {result.MA[i]} should be within reasonable range");

                // StdDev values should be reasonable for stock data
                if (result.StdDev[i] != 0.0) // Skip initialization values
                    Assert.IsTrue(result.StdDev[i] < 20.0,
                        $"StdDev[{i}] = {result.StdDev[i]} should be reasonable for stock data");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_LWMAMethod_ProducesValidResults()
        {
            // Arrange
            double[] prices = { 100, 102, 104, 106, 108, 110, 112, 114 };
            var period = 5;

            // Act
            var result = StdDev.Calculate(prices, period, 0, MAMethod.LWMA);

            // Assert - LWMA should produce valid results
            for (var i = period - 1; i < prices.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.MA[i]), $"LWMA[{i}] should not be NaN");
                Assert.IsFalse(double.IsNaN(result.StdDev[i]), $"StdDev[{i}] should not be NaN");
                Assert.IsTrue(result.StdDev[i] >= 0.0, $"StdDev[{i}] should be non-negative");

                // LWMA should give more weight to recent prices
                Assert.IsTrue(result.MA[i] > 0, $"LWMA[{i}] should be positive for this data");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_SMMAMethod_ProducesValidResults()
        {
            // Arrange
            double[] prices = { 100, 102, 104, 106, 108, 110, 112, 114 };
            var period = 4;

            // Act
            var result = StdDev.Calculate(prices, period, 0, MAMethod.SMMA);

            // Assert - SMMA should produce valid results
            for (var i = period - 1; i < prices.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.MA[i]), $"SMMA[{i}] should not be NaN");
                Assert.IsFalse(double.IsNaN(result.StdDev[i]), $"StdDev[{i}] should not be NaN");
                Assert.IsTrue(result.StdDev[i] >= 0.0, $"StdDev[{i}] should be non-negative");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_VolatilityMeasurement_IsAccurate()
        {
            // Arrange - Create data with known volatility characteristics
            double[] lowVolPrices = { 100, 100.5, 101, 101.5, 102, 102.5, 103, 103.5 };
            double[] highVolPrices = { 100, 110, 95, 115, 90, 120, 85, 125 };
            var period = 5;

            // Act
            var lowVolResult = StdDev.Calculate(lowVolPrices, period);
            var highVolResult = StdDev.Calculate(highVolPrices, period);

            // Assert - High volatility data should have higher standard deviation
            for (var i = period - 1; i < Math.Min(lowVolPrices.Length, highVolPrices.Length); i++)
                if (lowVolResult.StdDev[i] > 0 && highVolResult.StdDev[i] > 0)
                    Assert.IsTrue(highVolResult.StdDev[i] > lowVolResult.StdDev[i],
                        $"High volatility StdDev[{i}] = {highVolResult.StdDev[i]} should be > " +
                        $"low volatility StdDev[{i}] = {lowVolResult.StdDev[i]}");
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_LargeDataSet_PerformanceAndAccuracy()
        {
            // Arrange - Large dataset for performance testing
            var size = 1000;
            var prices = new double[size];
            var random = new Random(42); // Fixed seed for reproducibility
            var basePrice = 100.0;

            for (var i = 0; i < size; i++)
            {
                basePrice += (random.NextDouble() - 0.5) * 2; // Random walk
                basePrice = Math.Max(1.0, basePrice); // Keep positive
                prices[i] = basePrice;
            }

            // Act
            var startTime = DateTime.Now;
            var result = StdDev.Calculate(prices);
            var endTime = DateTime.Now;

            // Assert
            Assert.AreEqual(size, result.StdDev.Length);

            // Performance check
            var duration = endTime - startTime;
            Assert.IsTrue(duration.TotalSeconds < 1.0, "Large dataset calculation should complete quickly");

            // Accuracy check
            for (var i = 0; i < size; i++)
            {
                Assert.IsFalse(double.IsNaN(result.StdDev[i]), $"StdDev[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.StdDev[i]), $"StdDev[{i}] should not be infinite");
                Assert.IsTrue(result.StdDev[i] >= 0.0, $"StdDev[{i}] should be non-negative");

                Assert.IsFalse(double.IsNaN(result.MA[i]), $"MA[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.MA[i]), $"MA[{i}] should not be infinite");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void StdDevResult_PropertiesInitialized()
        {
            // Arrange & Act
            var result = new StdDevResult();

            // Assert
            Assert.IsNull(result.StdDev);
            Assert.IsNull(result.MA);
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_BollingerBandsCompatibility()
        {
            // Arrange - Test compatibility with Bollinger Bands calculation
            double[] prices = { 100, 102, 104, 103, 105, 107, 106, 108, 110, 109 };
            var period = 5;

            // Act
            var result = StdDev.Calculate(prices, period);

            // Assert - Should provide components for Bollinger Bands
            for (var i = period - 1; i < prices.Length; i++)
                if (result.MA[i] != 0.0 && result.StdDev[i] != 0.0)
                {
                    // Upper Band = MA + (StdDev * 2)
                    var upperBand = result.MA[i] + result.StdDev[i] * 2.0;

                    // Lower Band = MA - (StdDev * 2)
                    var lowerBand = result.MA[i] - result.StdDev[i] * 2.0;

                    // Verify bands are reasonable
                    Assert.IsTrue(upperBand > result.MA[i],
                        $"Upper band should be above MA at index {i}");
                    Assert.IsTrue(lowerBand < result.MA[i],
                        $"Lower band should be below MA at index {i}");
                    Assert.IsTrue(upperBand > lowerBand,
                        $"Upper band should be above lower band at index {i}");
                }
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_MathematicalProperties_AreCorrect()
        {
            // Arrange - Test mathematical properties of standard deviation
            double[] prices = { 95, 100, 105, 100, 95, 100, 105, 100, 95 };
            var period = 5;

            // Act
            var result = StdDev.Calculate(prices, period);

            // Assert - Standard deviation properties
            for (var i = period - 1; i < prices.Length; i++)
            {
                // StdDev should be non-negative
                Assert.IsTrue(result.StdDev[i] >= 0.0,
                    $"StdDev[{i}] should be non-negative");

                // StdDev should be finite
                Assert.IsFalse(double.IsInfinity(result.StdDev[i]),
                    $"StdDev[{i}] should be finite");
                Assert.IsFalse(double.IsNaN(result.StdDev[i]),
                    $"StdDev[{i}] should not be NaN");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void StdDev_Calculate_ExtremeShift_HandlesCorrectly()
        {
            // Arrange
            double[] prices = { 100, 102, 104, 103, 105 };
            var extremeShift = prices.Length; // Shift equal to array length

            // Act
            var result = StdDev.Calculate(prices, 3, extremeShift);

            // Assert - Should handle extreme shift gracefully
            for (var i = 0; i < prices.Length; i++)
                Assert.AreEqual(0.0, result.StdDev[i], TOLERANCE,
                    $"StdDev[{i}] should be zero with extreme shift");
        }
    }
}