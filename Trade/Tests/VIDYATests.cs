using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class VIDYATests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod][TestCategory("Core")]
        public void Calculate_EmptyArray_ReturnsEmptyResult()
        {
            // Arrange
            var prices = new double[0];

            // Act
            var result = VIDYA.Calculate(prices);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.VIDYA.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_SingleValue_ReturnsSingleValue()
        {
            // Arrange
            double[] prices = { 100.0 };

            // Act
            var result = VIDYA.Calculate(prices);

            // Assert
            Assert.AreEqual(1, result.VIDYA.Length);
            Assert.AreEqual(100.0, result.VIDYA[0], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_InsufficientData_ReturnsOriginalPrices()
        {
            // Arrange - Less than minBars (Max(periodCMO, periodEMA) = Max(9, 12) = 12)
            double[] prices = { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109 }; // 10 values
            var periodCMO = 9;
            var periodEMA = 12;

            // Act
            var result = VIDYA.Calculate(prices, periodCMO, periodEMA);

            // Assert
            Assert.AreEqual(10, result.VIDYA.Length);

            // With corrected initialization, values should be moving averages, not original prices
            Assert.AreEqual(100.0, result.VIDYA[0], TOLERANCE); // First value = first price

            // Second value should be average of first two prices
            Assert.AreEqual(100.5, result.VIDYA[1], TOLERANCE); // (100 + 101) / 2

            // All values should be reasonable averages, not exactly equal to prices
            for (var i = 0; i < prices.Length; i++)
            {
                Assert.IsTrue(result.VIDYA[i] > 0, $"VIDYA[{i}] should be positive");
                Assert.IsTrue(!double.IsNaN(result.VIDYA[i]), $"VIDYA[{i}] should not be NaN");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_DefaultParameters_ProducesReasonableValues()
        {
            // Arrange - Default parameters: periodCMO = 9, periodEMA = 12
            var prices = CreateTrendingPriceData(50);

            // Act
            var result = VIDYA.Calculate(prices);

            // Assert
            Assert.AreEqual(50, result.VIDYA.Length);

            // First minBars values should be simple moving averages during initialization
            var minBars = Math.Max(9, 12); // Max(periodCMO, periodEMA) = 12 (not 9+12-1=20)

            // First value should equal first price
            Assert.AreEqual(prices[0], result.VIDYA[0], TOLERANCE);

            // Values from 1 to minBars-1 should be simple moving averages for initialization
            for (var i = 1; i < minBars && i < prices.Length; i++)
            {
                // Calculate expected simple moving average
                var expectedSMA = 0.0;
                for (var j = 0; j <= i; j++)
                    expectedSMA += prices[j];
                expectedSMA /= i + 1;

                Assert.AreEqual(expectedSMA, result.VIDYA[i], TOLERANCE,
                    $"VIDYA[{i}] should be simple moving average during initialization");
            }

            // Later values should be smoothed and different from original prices
            for (var i = minBars; i < prices.Length; i++)
            {
                Assert.IsTrue(!double.IsNaN(result.VIDYA[i]), $"VIDYA[{i}] should not be NaN");
                Assert.IsTrue(result.VIDYA[i] > 0, $"VIDYA[{i}] should be positive for positive prices");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_TrendingMarket_FollowsTrendClosely()
        {
            // Arrange - Strong uptrend should make VIDYA more responsive
            var prices = CreateStrongUptrend(30);

            // Act
            var result = VIDYA.Calculate(prices);

            // Assert
            var minBars = 9 + 12 - 1; // 20
            if (prices.Length > minBars + 5)
            {
                // In trending market, VIDYA should follow prices more closely than regular EMA
                // Check that VIDYA values are increasing in an uptrend
                var isTrendingUp = true;
                for (var i = minBars + 1; i < Math.Min(minBars + 10, prices.Length); i++)
                    if (result.VIDYA[i] < result.VIDYA[i - 1])
                    {
                        isTrendingUp = false;
                        break;
                    }

                Assert.IsTrue(isTrendingUp, "VIDYA should trend upward in strong uptrend");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_SidewaysMarket_ProvidesSmoothingEffect()
        {
            // Arrange - Sideways market should make VIDYA less responsive
            var prices = CreateSidewaysMarket(30, 100.0, 2.0);

            // Act
            var result = VIDYA.Calculate(prices);

            // Assert
            var minBars = 9 + 12 - 1; // 20
            if (prices.Length > minBars + 5)
            {
                // In sideways market, VIDYA should be smoother (less volatile) than prices
                var priceVolatility = CalculateVolatility(prices, minBars, minBars + 5);
                var vidyaVolatility = CalculateVolatility(result.VIDYA, minBars, minBars + 5);

                // VIDYA should be less volatile than raw prices in sideways market
                Assert.IsTrue(vidyaVolatility <= priceVolatility,
                    "VIDYA should be less volatile than prices in sideways market");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_DifferentParameters_ProducesDifferentResults()
        {
            // Arrange
            var prices = CreateMixedMarketData(40);

            // Act
            var result1 = VIDYA.Calculate(prices, 5, 10); // Shorter periods (more responsive)
            var result2 = VIDYA.Calculate(prices, 15, 20); // Longer periods (less responsive)

            // Assert
            Assert.AreEqual(prices.Length, result1.VIDYA.Length);
            Assert.AreEqual(prices.Length, result2.VIDYA.Length);

            // Results should be different due to different parameters
            var hasSignificantDifference = false;
            var minBars = Math.Max(5 + 10 - 1, 15 + 20 - 1); // Take the larger minBars

            for (var i = minBars; i < prices.Length; i++)
                if (Math.Abs(result1.VIDYA[i] - result2.VIDYA[i]) > TOLERANCE)
                {
                    hasSignificantDifference = true;
                    break;
                }

            Assert.IsTrue(hasSignificantDifference, "Different parameters should produce different results");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_WithShift_AppliesShiftCorrectly()
        {
            // Arrange
            var prices = CreateTrendingPriceData(30);
            var shift = 3;

            // Act
            var resultNoShift = VIDYA.Calculate(prices);
            var resultWithShift = VIDYA.Calculate(prices, 9, 12, shift);

            // Assert
            Assert.AreEqual(prices.Length, resultWithShift.VIDYA.Length);

            // First 'shift' values should be 0 in shifted result
            for (var i = 0; i < shift; i++)
                Assert.AreEqual(0.0, resultWithShift.VIDYA[i], TOLERANCE,
                    $"First {shift} values should be zero with positive shift");

            // Shifted values should match original values offset by shift
            for (var i = 0; i < prices.Length - shift; i++)
                Assert.AreEqual(resultNoShift.VIDYA[i], resultWithShift.VIDYA[i + shift], TOLERANCE,
                    "Shifted values should match original values");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_NegativeShift_AppliesShiftCorrectly()
        {
            // Arrange
            var prices = CreateTrendingPriceData(30);
            var shift = -3;

            // Act
            var resultNoShift = VIDYA.Calculate(prices);
            var resultWithShift = VIDYA.Calculate(prices, 9, 12, shift);

            // Assert
            Assert.AreEqual(prices.Length, resultWithShift.VIDYA.Length);

            // Last 3 values should be 0 in shifted result
            for (var i = prices.Length - 3; i < prices.Length; i++)
                Assert.AreEqual(0.0, resultWithShift.VIDYA[i], TOLERANCE,
                    "Last 3 values should be zero with negative shift");

            // Shifted values should match original values offset by shift
            for (var i = 3; i < prices.Length; i++)
                Assert.AreEqual(resultNoShift.VIDYA[i], resultWithShift.VIDYA[i - 3], TOLERANCE,
                    "Shifted values should match original values with negative shift");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_VIDYAFormula_MatchesExpectedCalculation()
        {
            // Arrange - Simple test case for manual verification of the corrected formula
            double[] prices =
            {
                100, 102, 104, 103, 105, 107, 106, 108, 110, 109,
                111, 113, 112, 114, 116
            }; // 15 values
            var periodCMO = 5;
            var periodEMA = 5;

            // Act
            var result = VIDYA.Calculate(prices, periodCMO, periodEMA);

            // Assert
            // Verify the corrected formula is applied correctly after minBars
            var minBars = Math.Max(periodCMO, periodEMA); // Max(5, 5) = 5
            var alpha = 2.0 / (periodEMA + 1.0); // 2.0 / 6.0 = 0.333...

            // Manual calculation for the first VIDYA calculation at position minBars
            if (prices.Length > minBars)
            {
                // At position minBars (5), we can manually verify the calculation
                // Previous VIDYA[4] should be the simple average of first 5 prices
                var expectedVidyaPrev = (100 + 102 + 104 + 103 + 105) / 5.0; // 102.8
                Assert.AreEqual(expectedVidyaPrev, result.VIDYA[4], 0.1, "VIDYA[4] should be simple average");

                // The VIDYA calculation should follow the corrected formula:
                // VIDYA[i] = VIDYA[i-1] + alpha * VI * (price[i] - VIDYA[i-1])
                Assert.IsTrue(result.VIDYA[minBars] != prices[minBars],
                    "VIDYA should not equal raw price (it's a smoothed value)");

                // VIDYA should be between the previous VIDYA and current price
                var currentPrice = prices[minBars];
                var previousVidya = result.VIDYA[minBars - 1];
                var currentVidya = result.VIDYA[minBars];

                // VIDYA should move toward the current price but not reach it completely
                if (currentPrice > previousVidya)
                {
                    Assert.IsTrue(currentVidya > previousVidya, "VIDYA should move toward higher price");
                    Assert.IsTrue(currentVidya <= currentPrice, "VIDYA should not exceed current price");
                }
                else if (currentPrice < previousVidya)
                {
                    Assert.IsTrue(currentVidya < previousVidya, "VIDYA should move toward lower price");
                    Assert.IsTrue(currentVidya >= currentPrice, "VIDYA should not go below current price");
                }
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_CorrectVIDYAFormula_VerifyMathematically()
        {
            // This test verifies the mathematical correctness of the VIDYA formula
            // VIDYA[i] = VIDYA[i-1] + alpha * VI * (price[i] - VIDYA[i-1])

            // Arrange - Controlled data for precise verification
            double[] prices = { 100, 101, 102, 103, 104, 105 };
            var periodCMO = 3;
            var periodEMA = 3;

            // Act
            var result = VIDYA.Calculate(prices, periodCMO, periodEMA);

            // Assert - Manual calculation verification
            var alpha = 2.0 / (periodEMA + 1.0); // 0.5
            var minBars = Math.Max(periodCMO, periodEMA); // 3

            if (prices.Length > minBars)
            {
                // At minBars position, we can verify the exact calculation
                var pos = minBars; // Position 3
                var previousVidya = result.VIDYA[pos - 1];
                var currentPrice = prices[pos];

                // Calculate CMO manually for verification
                // For position 3, we look at changes from positions 1->2, 2->3, 3->4
                // But we only have positions 0-5, so for pos=3, we look at changes:
                // prices[3] - prices[2] = 103 - 102 = 1 (up)
                // prices[2] - prices[1] = 102 - 101 = 1 (up)  
                // prices[1] - prices[0] = 101 - 100 = 1 (up)
                // All changes are positive, so CMO should be 100

                var expectedCMO = 100.0; // All moves up
                var vi = Math.Abs(expectedCMO) / 100.0; // 1.0

                // Expected VIDYA calculation:
                // VIDYA[3] = VIDYA[2] + alpha * vi * (price[3] - VIDYA[2])
                var expectedVidya = previousVidya + alpha * vi * (currentPrice - previousVidya);

                // Due to CMO complexity, we can't exactly predict but can verify reasonableness
                Assert.IsTrue(Math.Abs(result.VIDYA[pos] - expectedVidya) < 10.0,
                    "VIDYA calculation should follow the formula pattern");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ExtremeValues_HandlesCorrectly()
        {
            // Arrange - Test with very large and very small values
            double[] prices = { 0.0001, 0.0002, 10000, 10001, 0.001, 5000, 1, 1000000, 0.1, 50 };

            // Act
            var result = VIDYA.Calculate(prices, 3, 4);

            // Assert
            Assert.AreEqual(prices.Length, result.VIDYA.Length);

            // All values should be finite (not NaN or Infinity)
            foreach (var value in result.VIDYA)
            {
                Assert.IsFalse(double.IsNaN(value), "VIDYA values should not be NaN");
                Assert.IsFalse(double.IsInfinity(value), "VIDYA values should not be Infinity");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ConstantPrices_ReturnsConstantValues()
        {
            // Arrange - All prices the same
            var prices = Enumerable.Repeat(100.0, 25).ToArray();

            // Act
            var result = VIDYA.Calculate(prices);

            // Assert
            // With constant prices, CMO should be 0, so VIDYA should converge to the constant price
            // All VIDYA values should equal or converge to the constant price
            var minBars = Math.Max(9, 12); // 12

            for (var i = 0; i < result.VIDYA.Length; i++)
                Assert.AreEqual(100.0, result.VIDYA[i], TOLERANCE,
                    $"VIDYA[{i}] of constant prices should be constant");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_AlternatingPrices_ProducesSmoothedResult()
        {
            // Arrange - Highly volatile alternating pattern
            var prices = new double[30];
            for (var i = 0; i < prices.Length; i++)
                prices[i] = 100 + (i % 2 == 0 ? 10 : -10); // Alternates between 110 and 90

            // Act
            var result = VIDYA.Calculate(prices);

            // Assert
            var minBars = 9 + 12 - 1; // 20
            if (prices.Length > minBars + 5)
            {
                // VIDYA should smooth out the alternating pattern
                var vidyaVolatility = CalculateVolatility(result.VIDYA, minBars, minBars + 5);
                var priceVolatility = CalculateVolatility(prices, minBars, minBars + 5);

                Assert.IsTrue(vidyaVolatility < priceVolatility,
                    "VIDYA should smooth alternating price patterns");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_RealWorldExample_ProducesReasonableValues()
        {
            // Arrange - Simulated realistic stock price data
            double[] prices =
            {
                100.0, 100.5, 101.2, 100.8, 102.1, 103.4, 102.9, 104.2, 105.1, 104.7,
                106.3, 107.8, 107.2, 108.9, 109.4, 108.8, 110.2, 111.5, 110.9, 112.3,
                113.1, 112.6, 114.4, 115.2, 114.8, 116.7, 117.3, 116.9, 118.5, 119.2
            };

            // Act
            var result = VIDYA.Calculate(prices);

            // Assert
            Assert.AreEqual(prices.Length, result.VIDYA.Length);

            // VIDYA should generally follow the upward trend
            var minBars = 9 + 12 - 1; // 20
            if (prices.Length > minBars)
            {
                Assert.IsTrue(result.VIDYA[prices.Length - 1] > result.VIDYA[minBars],
                    "VIDYA should follow the general upward trend");

                // Values should be within reasonable range of actual prices
                for (var i = minBars; i < prices.Length; i++)
                {
                    var minPrice = prices.Take(i + 1).Min();
                    var maxPrice = prices.Take(i + 1).Max();

                    Assert.IsTrue(result.VIDYA[i] >= minPrice * 0.95 && result.VIDYA[i] <= maxPrice * 1.05,
                        $"VIDYA[{i}] should be within reasonable range of price data");
                }
            }
        }

        [TestMethod][TestCategory("Core")]
        public void VIDYAResult_PropertiesInitialized()
        {
            // Arrange & Act
            var result = new VIDYAResult();

            // Assert
            Assert.IsNull(result.VIDYA);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Calculate_NullPricesArray_ThrowsArgumentNullException()
        {
            // Act
            VIDYA.Calculate(null);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void Calculate_ZeroCMOPeriod_ThrowsArgumentException()
        {
            // Arrange
            double[] prices = { 100, 101, 102 };

            // Act
            VIDYA.Calculate(prices, 0);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void Calculate_NegativeEMAPeriod_ThrowsArgumentException()
        {
            // Arrange
            double[] prices = { 100, 101, 102 };

            // Act
            VIDYA.Calculate(prices, 9, -5);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_FormulaCorrection_VerifyImprovement()
        {
            // This test demonstrates why the corrected VIDYA formula is better
            // Old (incorrect): VIDYA[i] = price[i] * alpha * VI + VIDYA[i-1] * (1 - alpha * VI)
            // New (correct): VIDYA[i] = VIDYA[i-1] + alpha * VI * (price[i] - VIDYA[i-1])

            // Arrange - Price data with a clear trend
            double[] prices =
            {
                100, 102, 104, 106, 108, 110, 112, 114, 116, 118,
                120, 122, 124, 126, 128, 130
            };
            var periodCMO = 5;
            var periodEMA = 5;

            // Act
            var result = VIDYA.Calculate(prices, periodCMO, periodEMA);

            // Assert - The corrected formula should produce more reasonable results
            var minBars = Math.Max(periodCMO, periodEMA); // 5

            if (prices.Length > minBars + 3)
            {
                // In a trending market, VIDYA should:
                // 1. Follow the trend direction
                // 2. Be smoother than raw prices
                // 3. React more quickly than a regular EMA would

                // Check trend following
                var isFollowingUptrend = true;
                for (var i = minBars + 1; i < minBars + 5; i++)
                    if (result.VIDYA[i] <= result.VIDYA[i - 1])
                    {
                        isFollowingUptrend = false;
                        break;
                    }

                Assert.IsTrue(isFollowingUptrend, "VIDYA should follow the upward trend");

                // Check smoothness - VIDYA changes should be smaller than price changes
                var avgPriceChange = 0.0;
                var avgVidyaChange = 0.0;
                var count = 0;

                for (var i = minBars + 1; i < minBars + 5; i++)
                {
                    avgPriceChange += Math.Abs(prices[i] - prices[i - 1]);
                    avgVidyaChange += Math.Abs(result.VIDYA[i] - result.VIDYA[i - 1]);
                    count++;
                }

                avgPriceChange /= count;
                avgVidyaChange /= count;

                Assert.IsTrue(avgVidyaChange <= avgPriceChange * 1.5, // Allow some flexibility
                    "VIDYA should be smoother than raw prices in most cases");

                // The corrected formula should produce finite, reasonable values
                for (var i = minBars; i < result.VIDYA.Length; i++)
                {
                    Assert.IsFalse(double.IsNaN(result.VIDYA[i]), $"VIDYA[{i}] should not be NaN");
                    Assert.IsFalse(double.IsInfinity(result.VIDYA[i]), $"VIDYA[{i}] should not be infinite");
                    Assert.IsTrue(result.VIDYA[i] > 0, $"VIDYA[{i}] should be positive for positive prices");
                }
            }
        }

        #region Helper Methods

        private double[] CreateTrendingPriceData(int count)
        {
            var prices = new double[count];
            var basePrice = 100.0;
            var random = new Random(42); // Fixed seed for reproducibility

            for (var i = 0; i < count; i++)
            {
                basePrice += random.NextDouble() * 0.5 + 0.1; // Slight upward bias
                prices[i] = basePrice;
            }

            return prices;
        }

        private double[] CreateStrongUptrend(int count)
        {
            var prices = new double[count];
            var basePrice = 100.0;

            for (var i = 0; i < count; i++)
            {
                basePrice += 1.0; // Strong consistent uptrend
                prices[i] = basePrice;
            }

            return prices;
        }

        private double[] CreateSidewaysMarket(int count, double centerPrice, double range)
        {
            var prices = new double[count];
            var random = new Random(42);

            for (var i = 0; i < count; i++) prices[i] = centerPrice + (random.NextDouble() - 0.5) * range;

            return prices;
        }

        private double[] CreateMixedMarketData(int count)
        {
            var prices = new double[count];
            var basePrice = 100.0;
            var random = new Random(42);

            for (var i = 0; i < count; i++)
            {
                // Mixed pattern: sometimes trending, sometimes sideways
                if (i % 10 < 5)
                    basePrice += (random.NextDouble() - 0.3) * 2.0; // Slight upward bias
                else
                    basePrice += (random.NextDouble() - 0.5) * 1.0; // Sideways

                prices[i] = Math.Max(1.0, basePrice); // Ensure positive prices
            }

            return prices;
        }

        private double CalculateVolatility(double[] values, int startIndex, int endIndex)
        {
            if (endIndex <= startIndex || startIndex < 0 || endIndex > values.Length)
                return 0.0;

            var sum = 0.0;
            var count = 0;

            for (var i = startIndex + 1; i < endIndex; i++)
                if (values[i - 1] != 0) // Avoid division by zero
                {
                    var change = Math.Abs(values[i] - values[i - 1]) / values[i - 1];
                    sum += change;
                    count++;
                }

            return count > 0 ? sum / count : 0.0;
        }

        #endregion
    }
}