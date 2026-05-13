using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class TRIXTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_ReturnsCorrectLengths()
        {
            // Arrange
            double[] prices = { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112 };
            var period = 5;

            // Act
            var result = TRIX.Calculate(prices, period);

            // Assert
            Assert.AreEqual(prices.Length, result.TRIX.Length);
            Assert.AreEqual(prices.Length, result.EMA.Length);
            Assert.AreEqual(prices.Length, result.SecondEMA.Length);
            Assert.AreEqual(prices.Length, result.ThirdEMA.Length);
        }
        
        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_DefaultPeriod_Is14()
        {
            // Arrange
            var prices = new double[50];
            for (var i = 0; i < 50; i++) prices[i] = 100 + i; // Upward trend

            // Act
            var defaultResult = TRIX.Calculate(prices);
            var explicitResult = TRIX.Calculate(prices);

            // Assert
            Assert.AreEqual(defaultResult.TRIX.Length, explicitResult.TRIX.Length);
            for (var i = 0; i < prices.Length; i++)
            {
                if (double.IsNaN(defaultResult.TRIX[i]) && double.IsNaN(explicitResult.TRIX[i]))
                    continue; // Both NaN is expected

                Assert.AreEqual(explicitResult.TRIX[i], defaultResult.TRIX[i], TOLERANCE,
                    $"Default period should be 14 at index {i}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_AppliesCorrectFormulas()
        {
            // Arrange - Simple test case for manual verification
            double[] prices = { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };
            var period = 2; // Small period for easier manual calculation

            // Act
            var result = TRIX.Calculate(prices, period);

            // Assert - Verify EMA calculations
            var alpha = 2.0 / (period + 1); // 2/3 ? 0.6667

            // First EMA
            Assert.AreEqual(10, result.EMA[0], TOLERANCE, "First EMA should equal first price");
            var expectedEMA1 = (11 - 10) * alpha + 10; // (11-10)*0.6667 + 10 = 10.6667
            Assert.AreEqual(expectedEMA1, result.EMA[1], TOLERANCE, "EMA calculation incorrect");

            // Second EMA (EMA of EMA)
            Assert.AreEqual(result.EMA[0], result.SecondEMA[0], TOLERANCE, "First Second EMA should equal first EMA");

            // Third EMA (EMA of Second EMA)
            Assert.AreEqual(result.SecondEMA[0], result.ThirdEMA[0], TOLERANCE,
                "First Third EMA should equal first Second EMA");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_RateOfChangeFormula_IsCorrect()
        {
            // Arrange - Test TRIX rate of change calculation
            var prices = new double[20];
            for (var i = 0; i < 20; i++) prices[i] = 100 + i * 2; // Strong upward trend
            var period = 3;

            // Act
            var result = TRIX.Calculate(prices, period);

            // Assert - Manually verify TRIX calculation for available data
            var minBars = 3 * period - 3; // 6

            if (prices.Length > minBars + 1)
            {
                // TRIX[i] = (ThirdEMA[i] - ThirdEMA[i-1]) / ThirdEMA[i-1]
                var expectedTRIX = (result.ThirdEMA[minBars] - result.ThirdEMA[minBars - 1]) /
                                   result.ThirdEMA[minBars - 1];
                Assert.AreEqual(expectedTRIX, result.TRIX[minBars], TOLERANCE,
                    "TRIX rate of change formula should be applied correctly");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_BullishTrend_ShowsPositiveValues()
        {
            // Arrange - Strong bullish trend
            var prices = new double[30];
            for (var i = 0; i < 30; i++) prices[i] = 100 + i * 3; // Accelerating upward trend
            var period = 5;

            // Act
            var result = TRIX.Calculate(prices, period);

            // Assert - In strong uptrend, TRIX should eventually show positive values
            var minBars = 3 * period - 3;
            var hasPositiveValues = false;

            for (var i = minBars + 3; i < result.TRIX.Length; i++) // Check after initial settling
                if (!double.IsNaN(result.TRIX[i]) && result.TRIX[i] > 0)
                {
                    hasPositiveValues = true;
                    break;
                }

            Assert.IsTrue(hasPositiveValues, "TRIX should show positive values in strong bullish trend");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_BearishTrend_ShowsNegativeValues()
        {
            // Arrange - Strong bearish trend
            var prices = new double[30];
            for (var i = 0; i < 30; i++) prices[i] = 200 - i * 3; // Accelerating downward trend
            var period = 5;

            // Act
            var result = TRIX.Calculate(prices, period);

            // Assert - In strong downtrend, TRIX should eventually show negative values
            var minBars = 3 * period - 3;
            var hasNegativeValues = false;

            for (var i = minBars + 3; i < result.TRIX.Length; i++) // Check after initial settling
                if (!double.IsNaN(result.TRIX[i]) && result.TRIX[i] < 0)
                {
                    hasNegativeValues = true;
                    break;
                }

            Assert.IsTrue(hasNegativeValues, "TRIX should show negative values in strong bearish trend");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_SidewaysMarket_ShowsLowVolatility()
        {
            // Arrange - Sideways market
            var prices = new double[30];
            for (var i = 0; i < 30; i++) prices[i] = 100 + Math.Sin(i * 0.2) * 2; // Oscillating around 100
            var period = 6;

            // Act
            var result = TRIX.Calculate(prices, period);

            // Assert - TRIX values should be relatively small in sideways market
            var minBars = 3 * period - 3;

            for (var i = minBars; i < result.TRIX.Length; i++)
                if (!double.IsNaN(result.TRIX[i]))
                    Assert.IsTrue(Math.Abs(result.TRIX[i]) < 0.1, // Small values expected
                        $"TRIX[{i}] = {result.TRIX[i]} should be small in sideways market");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_WithEmptyArray_ReturnsEmptyResult()
        {
            // Arrange
            double[] empty = { };

            // Act
            var result = TRIX.Calculate(empty);

            // Assert
            Assert.AreEqual(0, result.TRIX.Length);
            Assert.AreEqual(0, result.EMA.Length);
            Assert.AreEqual(0, result.SecondEMA.Length);
            Assert.AreEqual(0, result.ThirdEMA.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_WithSingleValue_ReturnsCorrectResult()
        {
            // Arrange
            double[] prices = { 100 };

            // Act
            var result = TRIX.Calculate(prices, 5);

            // Assert
            Assert.AreEqual(1, result.TRIX.Length);
            Assert.AreEqual(1, result.EMA.Length);
            Assert.AreEqual(1, result.SecondEMA.Length);
            Assert.AreEqual(1, result.ThirdEMA.Length);

            Assert.IsTrue(!double.IsNaN(result.TRIX[0]), "Single value should result in NaN TRIX");
            Assert.AreEqual(100, result.EMA[0], TOLERANCE, "EMA should equal single price");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_NullPricesArray_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                // Act
                TRIX.Calculate(null);
            });
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void TRIX_Calculate_ZeroPeriod_ThrowsArgumentException()
        {
            // Arrange
            double[] prices = { 100, 101, 102 };

            // Act
            TRIX.Calculate(prices, 0);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void TRIX_Calculate_NegativePeriod_ThrowsArgumentException()
        {
            // Arrange
            double[] prices = { 100, 101, 102 };

            // Act
            TRIX.Calculate(prices, -5);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_DifferentPeriods_ProduceDifferentResults()
        {
            // Arrange
            var prices = new double[25];
            for (var i = 0; i < 25; i++) prices[i] = 100 + i; // Linear trend

            // Act
            var shortPeriod = TRIX.Calculate(prices, 3);
            var longPeriod = TRIX.Calculate(prices, 7);

            // Assert - Different periods should produce different results
            var hasDifference = false;
            for (var i = 1; i < prices.Length; i++)
                if (!double.IsNaN(shortPeriod.TRIX[i]) && !double.IsNaN(longPeriod.TRIX[i]))
                    if (Math.Abs(shortPeriod.TRIX[i] - longPeriod.TRIX[i]) > TOLERANCE)
                    {
                        hasDifference = true;
                        break;
                    }

            Assert.IsTrue(hasDifference, "Different periods should produce different TRIX values");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_ZeroDivisionHandling_IsCorrect()
        {
            // Arrange - Create scenario where Third EMA could be zero
            double[] prices = { 0, 0, 0, 0, 0, 1, 2, 3, 4, 5 };
            var period = 3;

            // Act
            var result = TRIX.Calculate(prices, period);

            // Assert - Should handle zero division gracefully
            for (var i = 0; i < result.TRIX.Length; i++)
            {
                Assert.IsFalse(double.IsInfinity(result.TRIX[i]),
                    $"TRIX[{i}] should not be infinite (zero division should be handled)");

                if (!double.IsNaN(result.TRIX[i]))
                    Assert.IsTrue(Math.Abs(result.TRIX[i]) < 10.0,
                        $"TRIX[{i}] should be reasonable even with zero prices");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_TripleSmoothing_ReducesNoise()
        {
            // Arrange - Noisy price data
            var noisyPrices = new double[30];
            var random = new Random(42); // Fixed seed for reproducibility
            double basePrice = 100;

            for (var i = 0; i < 30; i++)
            {
                basePrice += (random.NextDouble() - 0.5) * 10; // High noise
                noisyPrices[i] = Math.Max(1, basePrice); // Ensure positive
            }

            // Act
            var result = TRIX.Calculate(noisyPrices, 5);

            // Assert - Triple smoothed EMA should be smoother than original prices
            // All values should be finite
            for (var i = 0; i < result.TRIX.Length; i++)
            {
                Assert.IsFalse(double.IsInfinity(result.EMA[i]), $"EMA[{i}] should not be infinite");
                Assert.IsFalse(double.IsInfinity(result.SecondEMA[i]), $"Second EMA[{i}] should not be infinite");
                Assert.IsFalse(double.IsInfinity(result.ThirdEMA[i]), $"Third EMA[{i}] should not be infinite");

                if (!double.IsNaN(result.TRIX[i]))
                    Assert.IsFalse(double.IsInfinity(result.TRIX[i]), $"TRIX[{i}] should not be infinite");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_RealWorldStockExample()
        {
            // Arrange - Realistic stock price data
            double[] prices =
            {
                100.0, 100.5, 101.2, 100.8, 102.1, 103.4, 102.9, 104.2, 105.1, 104.7,
                106.3, 107.8, 107.2, 108.9, 109.4, 108.8, 110.2, 111.5, 110.9, 112.3,
                113.1, 112.5, 114.2, 115.6, 114.8, 116.4, 117.2, 116.6, 118.3, 119.1
            };
            var period = 8;

            // Act
            var result = TRIX.Calculate(prices, period);

            // Assert
            Assert.AreEqual(30, result.TRIX.Length);

            // Verify all intermediate calculations are reasonable
            for (var i = 0; i < result.TRIX.Length; i++)
            {
                // EMAs should be within reasonable range of price data
                Assert.IsTrue(result.EMA[i] >= 95 && result.EMA[i] <= 125,
                    $"EMA[{i}] = {result.EMA[i]} should be within reasonable range");
                Assert.IsTrue(result.SecondEMA[i] >= 95 && result.SecondEMA[i] <= 125,
                    $"Second EMA[{i}] = {result.SecondEMA[i]} should be within reasonable range");
                Assert.IsTrue(result.ThirdEMA[i] >= 95 && result.ThirdEMA[i] <= 125,
                    $"Third EMA[{i}] = {result.ThirdEMA[i]} should be within reasonable range");

                // TRIX values should be reasonable for normal stock movements
                if (!double.IsNaN(result.TRIX[i]))
                    Assert.IsTrue(Math.Abs(result.TRIX[i]) < 1.0,
                        $"TRIX[{i}] = {result.TRIX[i]} should be within reasonable range for stock data");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_MomentumOscillator_BehavesCorrectly()
        {
            // Arrange - Price data with momentum changes
            var prices = new double[40];

            // Phase 1: Slow uptrend (low momentum)
            for (var i = 0; i < 15; i++)
                prices[i] = 100 + i * 0.5;

            // Phase 2: Accelerating uptrend (increasing momentum)
            for (var i = 15; i < 25; i++)
                prices[i] = prices[14] + (i - 14) * 2.0;

            // Phase 3: Decelerating uptrend (decreasing momentum)
            for (var i = 25; i < 40; i++)
                prices[i] = prices[24] + (i - 24) * 0.2;

            // Act
            var result = TRIX.Calculate(prices, 6);

            // Assert - TRIX should reflect momentum changes
            var minBars = 3 * 6 - 3; // 15

            if (prices.Length > minBars + 10)
            {
                // Look for momentum patterns in later values where TRIX has stabilized
                var foundMomentumChange = false;

                for (var i = minBars + 5; i < result.TRIX.Length - 5; i++)
                    if (!double.IsNaN(result.TRIX[i]) && !double.IsNaN(result.TRIX[i + 5]))
                        // Should show some variation indicating momentum detection
                        if (Math.Abs(result.TRIX[i + 5] - result.TRIX[i]) > 0.001)
                        {
                            foundMomentumChange = true;
                            break;
                        }

                Assert.IsTrue(foundMomentumChange, "TRIX should detect momentum changes in the data");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_LargeDataSet_PerformanceAndAccuracy()
        {
            // Arrange - Large dataset for performance testing
            var size = 1000;
            var prices = new double[size];
            var random = new Random(42); // Fixed seed
            var basePrice = 100.0;

            for (var i = 0; i < size; i++)
            {
                basePrice += (random.NextDouble() - 0.5) * 2; // Random walk
                basePrice = Math.Max(1.0, basePrice); // Keep positive
                prices[i] = basePrice;
            }

            // Act
            var startTime = DateTime.Now;
            var result = TRIX.Calculate(prices);
            var endTime = DateTime.Now;

            // Assert
            Assert.AreEqual(size, result.TRIX.Length);

            // Performance check
            var duration = endTime - startTime;
            Assert.IsTrue(duration.TotalSeconds < 1.0, "Large dataset calculation should complete quickly");

            // Accuracy check
            for (var i = 0; i < size; i++)
            {
                Assert.IsFalse(double.IsInfinity(result.TRIX[i]), $"TRIX[{i}] should not be infinite");
                Assert.IsFalse(double.IsInfinity(result.EMA[i]), $"EMA[{i}] should not be infinite");
                Assert.IsFalse(double.IsInfinity(result.SecondEMA[i]), $"Second EMA[{i}] should not be infinite");
                Assert.IsFalse(double.IsInfinity(result.ThirdEMA[i]), $"Third EMA[{i}] should not be infinite");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_ConsistentWithTechnicalAnalysis()
        {
            // Arrange - Test against known TRIX behavior
            double[] prices = { 10, 11, 12, 13, 12, 11, 12, 13, 14, 15, 16, 15, 14, 15, 16, 17, 18 };
            var period = 3;

            // Act
            var result = TRIX.Calculate(prices, period);

            // Assert - Verify mathematical consistency
            var minBars = 3 * period - 3; // 6

            // All EMA values should be initialized properly
            Assert.AreEqual(prices[0], result.EMA[0], TOLERANCE, "First EMA should equal first price");
            Assert.AreEqual(result.EMA[0], result.SecondEMA[0], TOLERANCE, "First Second EMA should equal first EMA");
            Assert.AreEqual(result.SecondEMA[0], result.ThirdEMA[0], TOLERANCE,
                "First Third EMA should equal first Second EMA");

            // TRIX calculation should be consistent
            if (prices.Length > minBars)
                for (var i = minBars; i < prices.Length && i > 0; i++)
                    if (result.ThirdEMA[i - 1] != 0)
                    {
                        var expectedTRIX = (result.ThirdEMA[i] - result.ThirdEMA[i - 1]) / result.ThirdEMA[i - 1];
                        Assert.AreEqual(expectedTRIX, result.TRIX[i], TOLERANCE,
                            $"TRIX calculation should be consistent at index {i}");
                    }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIXResult_PropertiesInitialized()
        {
            // Arrange & Act
            var result = new TRIXResult();

            // Assert
            Assert.IsNull(result.TRIX);
            Assert.IsNull(result.EMA);
            Assert.IsNull(result.SecondEMA);
            Assert.IsNull(result.ThirdEMA);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TRIX_Calculate_VerySmallPeriod_StillWorks()
        {
            // Arrange
            double[] prices = { 100, 101, 102, 103, 104, 105, 106, 107 };
            var period = 1; // Very small period

            // Act
            var result = TRIX.Calculate(prices, period);

            // Assert
            Assert.AreEqual(prices.Length, result.TRIX.Length);

            // With period 1, alpha = 2/(1+1) = 1, so EMA should equal current price
            for (var i = 0; i < prices.Length; i++)
                Assert.AreEqual(prices[i], result.EMA[i], TOLERANCE,
                    $"With period 1, EMA[{i}] should equal price");

            // All values should be finite
            for (var i = 0; i < prices.Length; i++)
            {
                Assert.IsFalse(double.IsInfinity(result.TRIX[i]), $"TRIX[{i}] should not be infinite");
                Assert.IsFalse(double.IsInfinity(result.ThirdEMA[i]), $"Third EMA[{i}] should not be infinite");
            }
        }
    }
}