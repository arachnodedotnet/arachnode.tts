using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class WADTests
    {
        private const double TOLERANCE = 1e-8;
        private const double FOREX_POINT = 0.00001; // Standard Forex pip

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_ReturnsCorrectLength()
        {
            // Arrange
            double[] high = { 102, 105, 104, 107, 106 };
            double[] low = { 98, 101, 100, 103, 102 };
            double[] close = { 100, 103, 102, 105, 104 };

            // Act
            var result = WAD.Calculate(high, low, close);

            // Assert
            Assert.AreEqual(5, result.WAD.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_FirstValueIsZero()
        {
            // Arrange
            double[] high = { 102, 105 };
            double[] low = { 98, 101 };
            double[] close = { 100, 103 };

            // Act
            var result = WAD.Calculate(high, low, close);

            // Assert
            Assert.AreEqual(0.0, result.WAD[0], TOLERANCE, "First WAD value should always be zero");
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_AppliesCorrectFormulas()
        {
            // Arrange - Simple test case for manual verification
            double[] high = { 12, 15, 13 };
            double[] low = { 8, 11, 9 };
            double[] close = { 10, 13, 11 };

            // Act
            var result = WAD.Calculate(high, low, close, 0.01);

            // Assert - Manual calculations
            Assert.AreEqual(0.0, result.WAD[0], TOLERANCE);

            // Bar 1: Close(13) > Previous Close(10) - Bullish
            // TRH = Max(15, 10) = 15
            // TRL = Min(11, 10) = 10  
            // WAD[1] = WAD[0] + (13 - 10) = 0 + 3 = 3
            Assert.AreEqual(3.0, result.WAD[1], TOLERANCE);

            // Bar 2: Close(11) < Previous Close(13) - Bearish
            // TRH = Max(13, 13) = 13
            // TRL = Min(9, 13) = 9
            // WAD[2] = WAD[1] + (11 - 13) = 3 + (-2) = 1
            Assert.AreEqual(1.0, result.WAD[2], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_EqualCloses_NoChange()
        {
            // Arrange - Test equal closes within tolerance
            double[] high = { 102, 105, 104 };
            double[] low = { 98, 101, 100 };
            double[] close = { 100, 103, 103 }; // Last two closes equal

            // Act
            var result = WAD.Calculate(high, low, close, 0.01);

            // Assert
            Assert.AreEqual(0.0, result.WAD[0], TOLERANCE);

            // First change: 103 > 100 (bullish)
            var expectedWAD1 = 0.0 + (103 - Math.Min(101, 100)); // 0 + (103 - 100) = 3
            Assert.AreEqual(expectedWAD1, result.WAD[1], TOLERANCE);

            // Second change: 103 = 103 (no change)
            Assert.AreEqual(result.WAD[1], result.WAD[2], TOLERANCE, "WAD should not change when closes are equal");
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_BullishTrend_AccumulatesPositively()
        {
            // Arrange - Consistent bullish trend
            double[] high = { 101, 102, 103, 104, 105 };
            double[] low = { 99, 100, 101, 102, 103 };
            double[] close = { 100, 101, 102, 103, 104 }; // Steady increase

            // Act
            var result = WAD.Calculate(high, low, close, 0.01);

            // Assert - WAD should generally increase in bullish trend
            Assert.AreEqual(0.0, result.WAD[0], TOLERANCE);

            for (var i = 1; i < result.WAD.Length; i++)
                Assert.IsTrue(result.WAD[i] > result.WAD[i - 1],
                    $"WAD should increase in bullish trend at index {i}");
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_BearishTrend_AccumulatesNegatively()
        {
            // Arrange - Consistent bearish trend
            double[] high = { 105, 104, 103, 102, 101 };
            double[] low = { 103, 102, 101, 100, 99 };
            double[] close = { 104, 103, 102, 101, 100 }; // Steady decrease

            // Act
            var result = WAD.Calculate(high, low, close, 0.01);

            // Assert - WAD should generally decrease in bearish trend
            Assert.AreEqual(0.0, result.WAD[0], TOLERANCE);

            for (var i = 1; i < result.WAD.Length; i++)
                Assert.IsTrue(result.WAD[i] < result.WAD[i - 1],
                    $"WAD should decrease in bearish trend at index {i}");
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_TrueRangeCalculation_IsCorrect()
        {
            // Arrange - Test specific True Range scenarios
            double[] high = { 10, 12, 8 }; // Note: third bar has low high
            double[] low = { 8, 10, 6 };
            double[] close = { 9, 11, 7 };

            // Act
            var result = WAD.Calculate(high, low, close, 0.01);

            // Assert - Manual verification of True Range calculations
            Assert.AreEqual(0.0, result.WAD[0], TOLERANCE);

            // Bar 1: Close(11) > Previous Close(9)
            // TRH = Max(12, 9) = 12
            // TRL = Min(10, 9) = 9
            // WAD[1] = 0 + (11 - 9) = 2
            Assert.AreEqual(2.0, result.WAD[1], TOLERANCE);

            // Bar 2: Close(7) < Previous Close(11)
            // TRH = Max(8, 11) = 11  (Previous close is higher than current high!)
            // TRL = Min(6, 11) = 6
            // WAD[2] = 2 + (7 - 11) = 2 + (-4) = -2
            Assert.AreEqual(-2.0, result.WAD[2], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_UnequalArrayLengths_UsesMinimum()
        {
            // Arrange - Arrays of different lengths
            double[] high = { 102, 105, 104, 107 }; // 4 elements
            double[] low = { 98, 101, 100 }; // 3 elements (shortest)
            double[] close = { 100, 103, 102, 105, 104 }; // 5 elements

            // Act
            var result = WAD.Calculate(high, low, close);

            // Assert - Should use length of shortest array (3)
            Assert.AreEqual(3, result.WAD.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_WithEmptyArrays_ReturnsEmptyResult()
        {
            // Arrange
            double[] empty = { };

            // Act
            var result = WAD.Calculate(empty, empty, empty);

            // Assert
            Assert.AreEqual(0, result.WAD.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_WithSingleValue_ReturnsZero()
        {
            // Arrange
            double[] high = { 102 };
            double[] low = { 98 };
            double[] close = { 100 };

            // Act
            var result = WAD.Calculate(high, low, close);

            // Assert
            Assert.AreEqual(1, result.WAD.Length);
            Assert.AreEqual(0.0, result.WAD[0], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_NullHighArray_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                // Arrange
                double[] low = { 98 };
                double[] close = { 100 };

                // Act
                WAD.Calculate(null, low, close);
            });
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_NullLowArray_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                // Arrange
                double[] high = { 102 };
                double[] close = { 100 };

                // Act
                WAD.Calculate(high, null, close);
            });
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_NullCloseArray_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                // Arrange
                double[] high = { 102 };
                double[] low = { 98 };

                // Act
                WAD.Calculate(high, low, null);
            });
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_ZeroPoint_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                // Arrange
                double[] high = { 102 };
                double[] low = { 98 };
                double[] close = { 100 };

                // Act
                WAD.Calculate(high, low, close, 0.0);
            });
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_NegativePoint_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                // Arrange
                double[] high = { 102 };
                double[] low = { 98 };
                double[] close = { 100 };

                // Act
                WAD.Calculate(high, low, close, -0.01);
            });
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void WAD_Calculate_TooLargePoint_ThrowsArgumentException()
        {
            Assert.ThrowsExactly<ArgumentException>(() =>
            {
                // Arrange
                double[] high = { 102 };
                double[] low = { 98 };
                double[] close = { 100 };

                // Act
                WAD.Calculate(high, low, close, 0.5); // Too large point size
            });
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_DefaultPoint_UsesForexDefault()
        {
            // Arrange
            double[] high = { 1.2345, 1.2355 };
            double[] low = { 1.2335, 1.2345 };
            double[] close = { 1.2340, 1.2350 };

            // Act - Don't specify point parameter
            var result = WAD.Calculate(high, low, close);

            // Assert - Should use default point = 0.00001
            Assert.AreEqual(2, result.WAD.Length);
            Assert.AreEqual(0.0, result.WAD[0], TOLERANCE);

            // Manual calculation with default point
            // TRL = Min(1.2345, 1.2340) = 1.2340
            // WAD[1] = 0 + (1.2350 - 1.2340) = 0.0010
            Assert.AreEqual(0.0010, result.WAD[1], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_DifferentPointSizes_AffectsEqualityComparison()
        {
            // Arrange - Test point size effects on equality comparison
            double[] high = { 100.0, 100.5 };
            double[] low = { 99.0, 99.5 };
            double[] close = { 99.5, 99.501 }; // Very small difference

            // Act
            var resultSmallPoint = WAD.Calculate(high, low, close, 0.0001); // Strict comparison
            var resultLargePoint = WAD.Calculate(high, low, close, 0.01); // Loose comparison

            // Assert
            // With small point, 99.501 != 99.5, so should calculate change
            Assert.AreNotEqual(resultSmallPoint.WAD[1], resultSmallPoint.WAD[0],
                "Small point should detect difference");

            // With large point, 99.501 ? 99.5, so should be no change
            Assert.AreEqual(resultLargePoint.WAD[1], resultLargePoint.WAD[0], TOLERANCE,
                "Large point should treat values as equal");
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_RealWorldForexExample()
        {
            // Arrange - Realistic EUR/USD price data
            double[] high = { 1.1850, 1.1865, 1.1845, 1.1870, 1.1855 };
            double[] low = { 1.1820, 1.1840, 1.1825, 1.1850, 1.1835 };
            double[] close = { 1.1835, 1.1860, 1.1830, 1.1865, 1.1840 };
            var point = 0.00001; // Standard Forex pip

            // Act
            var result = WAD.Calculate(high, low, close, point);

            // Assert
            Assert.AreEqual(5, result.WAD.Length);
            Assert.AreEqual(0.0, result.WAD[0], TOLERANCE);

            // All values should be finite and reasonable
            for (var i = 0; i < result.WAD.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.WAD[i]), $"WAD[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.WAD[i]), $"WAD[{i}] should not be infinite");
                // For Forex, WAD values should typically be in reasonable range
                Assert.IsTrue(Math.Abs(result.WAD[i]) < 1.0, $"WAD[{i}] should be within reasonable range for Forex");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_RealWorldStockExample()
        {
            // Arrange - Realistic stock price data
            double[] high = { 150.25, 152.75, 151.50, 154.00, 152.25 };
            double[] low = { 148.75, 150.00, 149.25, 151.50, 150.00 };
            double[] close = { 149.50, 151.25, 150.75, 152.50, 151.00 };
            var point = 0.01; // Penny stocks

            // Act
            var result = WAD.Calculate(high, low, close, point);

            // Assert
            Assert.AreEqual(5, result.WAD.Length);
            Assert.AreEqual(0.0, result.WAD[0], TOLERANCE);

            // All values should be finite and reasonable
            for (var i = 0; i < result.WAD.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.WAD[i]), $"WAD[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.WAD[i]), $"WAD[{i}] should not be infinite");
                // For stocks, WAD values can be larger
                Assert.IsTrue(Math.Abs(result.WAD[i]) < 100.0,
                    $"WAD[{i}] should be within reasonable range for stocks");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_VolatileMarket_HandlesCorrectly()
        {
            // Arrange - Highly volatile market conditions
            double[] high = { 100, 120, 95, 130, 85, 125, 90 };
            double[] low = { 95, 105, 80, 110, 75, 105, 80 };
            double[] close = { 98, 115, 88, 125, 82, 120, 85 };

            // Act
            var result = WAD.Calculate(high, low, close, 0.01);

            // Assert
            Assert.AreEqual(7, result.WAD.Length);

            // All values should be finite despite volatility
            for (var i = 0; i < result.WAD.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.WAD[i]), $"WAD[{i}] should not be NaN in volatile market");
                Assert.IsFalse(double.IsInfinity(result.WAD[i]), $"WAD[{i}] should not be infinite in volatile market");
            }

            // WAD should accumulate significant changes in volatile market
            var totalChange = Math.Abs(result.WAD[result.WAD.Length - 1] - result.WAD[0]);
            Assert.IsTrue(totalChange > 0, "WAD should accumulate changes in volatile market");
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_CumulativeProperty_IsCorrect()
        {
            // Arrange - Test the cumulative nature of WAD
            double[] high = { 105, 110, 108, 112 };
            double[] low = { 95, 100, 98, 102 };
            double[] close = { 100, 105, 103, 107 };

            // Act
            var result = WAD.Calculate(high, low, close, 0.01);

            // Assert - Each WAD value should build on the previous
            Assert.AreEqual(0.0, result.WAD[0], TOLERANCE);

            // Manually verify cumulative calculation
            // WAD[1]: Close(105) > Previous(100), TRL = Min(100, 100) = 100, WAD = 0 + (105-100) = 5
            Assert.AreEqual(5.0, result.WAD[1], TOLERANCE);

            // WAD[2]: Close(103) < Previous(105), TRH = Max(108, 105) = 108, WAD = 5 + (103-108) = 0
            Assert.AreEqual(0.0, result.WAD[2], TOLERANCE);

            // WAD[3]: Close(107) > Previous(103), TRL = Min(102, 103) = 102, WAD = 0 + (107-102) = 5
            Assert.AreEqual(5.0, result.WAD[3], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_EqualityTolerance_WorksCorrectly()
        {
            // Arrange - Test floating-point equality handling
            double[] high = { 100.0, 100.0 };
            double[] low = { 99.0, 99.0 };
            double[] close = { 99.5, 99.50001 }; // Very small difference

            // Act
            var resultStrictTolerance = WAD.Calculate(high, low, close, 0.000001); // Very strict
            var resultLooseTolerance = WAD.Calculate(high, low, close, 0.001); // Loose

            // Assert
            // Strict tolerance should detect the difference
            Assert.AreNotEqual(resultStrictTolerance.WAD[0], resultStrictTolerance.WAD[1]);

            // Loose tolerance should treat as equal
            Assert.AreEqual(resultLooseTolerance.WAD[0], resultLooseTolerance.WAD[1], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_LargeDataSet_PerformanceAndAccuracy()
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

                close[i] = basePrice;
                high[i] = basePrice + random.NextDouble() * 2;
                low[i] = basePrice - random.NextDouble() * 2;

                // Ensure OHLC relationships
                high[i] = Math.Max(high[i], close[i]);
                low[i] = Math.Min(low[i], close[i]);
            }

            // Act
            var startTime = DateTime.Now;
            var result = WAD.Calculate(high, low, close, 0.01);
            var endTime = DateTime.Now;

            // Assert
            Assert.AreEqual(size, result.WAD.Length);

            // Performance check
            var duration = endTime - startTime;
            Assert.IsTrue(duration.TotalSeconds < 1.0, "Large dataset calculation should complete quickly");

            // Accuracy check
            for (var i = 0; i < size; i++)
            {
                Assert.IsFalse(double.IsNaN(result.WAD[i]), $"WAD[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.WAD[i]), $"WAD[{i}] should not be infinite");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_ConsistentWithWilliamsDefinition()
        {
            // Arrange - Test case that follows Larry Williams' original definition
            double[] high = { 50, 52, 49, 53, 51 };
            double[] low = { 48, 50, 47, 51, 49 };
            double[] close = { 49, 51, 48, 52, 50 };

            // Act
            var result = WAD.Calculate(high, low, close, 0.01);

            // Assert - Step-by-step verification following Williams' definition
            Assert.AreEqual(0.0, result.WAD[0], TOLERANCE);

            // Bar 1: Close(51) > Previous(49) - Bullish
            // TRH = Max(52, 49) = 52, TRL = Min(50, 49) = 49
            // WAD[1] = 0 + (51 - 49) = 2
            Assert.AreEqual(2.0, result.WAD[1], TOLERANCE);

            // Bar 2: Close(48) < Previous(51) - Bearish  
            // TRH = Max(49, 51) = 51, TRL = Min(47, 51) = 47
            // WAD[2] = 2 + (48 - 51) = -1
            Assert.AreEqual(-1.0, result.WAD[2], TOLERANCE);

            // Bar 3: Close(52) > Previous(48) - Bullish
            // TRH = Max(53, 48) = 53, TRL = Min(51, 48) = 48
            // WAD[3] = -1 + (52 - 48) = 3
            Assert.AreEqual(3.0, result.WAD[3], TOLERANCE);

            // Bar 4: Close(50) < Previous(52) - Bearish
            // TRH = Max(51, 52) = 52, TRL = Min(49, 52) = 49  
            // WAD[4] = 3 + (50 - 52) = 1
            Assert.AreEqual(1.0, result.WAD[4], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void WADResult_PropertiesInitialized()
        {
            // Arrange & Act
            var result = new WADResult();

            // Assert
            Assert.IsNull(result.WAD);
        }

        [TestMethod][TestCategory("Core")]
        public void WAD_Calculate_ZeroPriceMovement_HandlesCorrectly()
        {
            // Arrange - No price movement scenario
            double[] high = { 100, 100, 100 };
            double[] low = { 100, 100, 100 };
            double[] close = { 100, 100, 100 };

            // Act
            var result = WAD.Calculate(high, low, close, 0.01);

            // Assert - WAD should remain unchanged with no price movement
            Assert.AreEqual(0.0, result.WAD[0], TOLERANCE);
            Assert.AreEqual(0.0, result.WAD[1], TOLERANCE);
            Assert.AreEqual(0.0, result.WAD[2], TOLERANCE);
        }
    }
}