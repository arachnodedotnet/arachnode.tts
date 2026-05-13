using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class HeikenAshiTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_ReturnsCorrectLengths()
        {
            // Arrange
            double[] open = { 100, 101, 102, 103, 104 };
            double[] high = { 105, 106, 107, 108, 109 };
            double[] low = { 99, 100, 101, 102, 103 };
            double[] close = { 104, 105, 106, 107, 108 };

            // Act
            var result = HeikenAshi.Calculate(open, high, low, close);

            // Assert
            Assert.AreEqual(5, result.Open.Length);
            Assert.AreEqual(5, result.High.Length);
            Assert.AreEqual(5, result.Low.Length);
            Assert.AreEqual(5, result.Close.Length);
            Assert.AreEqual(5, result.Color.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_FirstBarUsesRegularOHLC()
        {
            // Arrange
            double[] open = { 100, 102 };
            double[] high = { 105, 107 };
            double[] low = { 99, 101 };
            double[] close = { 104, 106 };

            // Act
            var result = HeikenAshi.Calculate(open, high, low, close);

            // Assert - First bar should equal regular OHLC
            Assert.AreEqual(100, result.Open[0], TOLERANCE, "First HA Open should equal regular Open");
            Assert.AreEqual(105, result.High[0], TOLERANCE, "First HA High should equal regular High");
            Assert.AreEqual(99, result.Low[0], TOLERANCE, "First HA Low should equal regular Low");
            Assert.AreEqual(104, result.Close[0], TOLERANCE, "First HA Close should equal regular Close");
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_AppliesCorrectFormulas()
        {
            // Arrange - Simple test case for manual verification
            double[] open = { 100, 102, 104 };
            double[] high = { 105, 107, 109 };
            double[] low = { 99, 101, 103 };
            double[] close = { 104, 106, 108 };

            // Act
            var result = HeikenAshi.Calculate(open, high, low, close);

            // Assert - Manual calculation for second bar (index 1)
            // HA Close[1] = (Open[1] + High[1] + Low[1] + Close[1]) / 4 = (102 + 107 + 101 + 106) / 4 = 104
            var expectedHAClose1 = (102 + 107 + 101 + 106) / 4.0;
            Assert.AreEqual(expectedHAClose1, result.Close[1], TOLERANCE, "HA Close formula incorrect");

            // HA Open[1] = (Previous HA Open + Previous HA Close) / 2 = (100 + 104) / 2 = 102
            var expectedHAOpen1 = (result.Open[0] + result.Close[0]) / 2.0;
            Assert.AreEqual(expectedHAOpen1, result.Open[1], TOLERANCE, "HA Open formula incorrect");

            // HA High[1] = Max(High[1], HA Open[1], HA Close[1]) = Max(107, 102, 104) = 107
            var expectedHAHigh1 = Math.Max(high[1], Math.Max(expectedHAOpen1, expectedHAClose1));
            Assert.AreEqual(expectedHAHigh1, result.High[1], TOLERANCE, "HA High formula incorrect");

            // HA Low[1] = Min(Low[1], HA Open[1], HA Close[1]) = Min(101, 102, 104) = 101
            var expectedHALow1 = Math.Min(low[1], Math.Min(expectedHAOpen1, expectedHAClose1));
            Assert.AreEqual(expectedHALow1, result.Low[1], TOLERANCE, "HA Low formula incorrect");
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_ColorLogicCorrect()
        {
            // Arrange - Design data to test color logic
            double[] open = { 100, 100, 100 };
            double[] high = { 105, 105, 105 };
            double[] low = { 99, 99, 99 };
            double[] close = { 104, 99, 102.5 }; // bullish, bearish, neutral

            // Act
            var result = HeikenAshi.Calculate(open, high, low, close);

            // Assert
            // First bar: Open=100, Close=104 -> Bullish (0)
            Assert.AreEqual(0, result.Color[0], "First bar should be bullish (HA Open <= HA Close)");

            // Calculate expected values for second bar
            var haOpen1 = (result.Open[0] + result.Close[0]) / 2.0; // (100 + 104) / 2 = 102
            var haClose1 = (open[1] + high[1] + low[1] + close[1]) / 4.0; // (100 + 105 + 99 + 99) / 4 = 100.75

            // Second bar: HA Open > HA Close -> Bearish (1)
            Assert.AreEqual(1, result.Color[1], "Second bar should be bearish (HA Open > HA Close)");

            // Third bar calculation
            var haOpen2 = (result.Open[1] + result.Close[1]) / 2.0;
            var haClose2 = (open[2] + high[2] + low[2] + close[2]) / 4.0;

            var expectedColor2 = haOpen2 <= haClose2 ? 0 : 1;
            Assert.AreEqual(expectedColor2, result.Color[2], "Third bar color should follow HA Open <= HA Close logic");
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_BullishTrendShowsCorrectColors()
        {
            // Arrange - Strong bullish trend
            double[] open = { 100, 101, 102, 103, 104 };
            double[] high = { 102, 103, 104, 105, 106 };
            double[] low = { 99, 100, 101, 102, 103 };
            double[] close = { 101, 102, 103, 104, 105 };

            // Act
            var result = HeikenAshi.Calculate(open, high, low, close);

            // Assert - Most bars in strong uptrend should be bullish (0)
            var bullishCount = 0;
            for (var i = 0; i < result.Color.Length; i++)
                if (result.Color[i] == 0)
                    bullishCount++;

            Assert.IsTrue(bullishCount >= 3, "Strong bullish trend should have majority bullish bars");
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_BearishTrendShowsCorrectColors()
        {
            // Arrange - Strong bearish trend
            double[] open = { 105, 104, 103, 102, 101 };
            double[] high = { 106, 105, 104, 103, 102 };
            double[] low = { 103, 102, 101, 100, 99 };
            double[] close = { 104, 103, 102, 101, 100 };

            // Act
            var result = HeikenAshi.Calculate(open, high, low, close);

            // Assert - Most bars in strong downtrend should be bearish (1)
            var bearishCount = 0;
            for (var i = 0; i < result.Color.Length; i++)
                if (result.Color[i] == 1)
                    bearishCount++;

            Assert.IsTrue(bearishCount >= 3, "Strong bearish trend should have majority bearish bars");
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_SmoothedValuesAreReasonable()
        {
            // Arrange - Volatile price data
            double[] open = { 100, 105, 95, 110, 90 };
            double[] high = { 110, 115, 105, 120, 100 };
            double[] low = { 95, 90, 85, 95, 80 };
            double[] close = { 108, 92, 108, 92, 98 };

            // Act
            var result = HeikenAshi.Calculate(open, high, low, close);

            // Assert - Heiken Ashi should smooth volatility
            // All values should be finite and reasonable
            for (var i = 0; i < result.Open.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.Open[i]), $"HA Open[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.Open[i]), $"HA Open[{i}] should not be infinite");
                Assert.IsFalse(double.IsNaN(result.High[i]), $"HA High[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.High[i]), $"HA High[{i}] should not be infinite");
                Assert.IsFalse(double.IsNaN(result.Low[i]), $"HA Low[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.Low[i]), $"HA Low[{i}] should not be infinite");
                Assert.IsFalse(double.IsNaN(result.Close[i]), $"HA Close[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.Close[i]), $"HA Close[{i}] should not be infinite");

                // Verify OHLC relationships
                Assert.IsTrue(result.High[i] >= result.Open[i], $"HA High[{i}] should be >= HA Open[{i}]");
                Assert.IsTrue(result.High[i] >= result.Close[i], $"HA High[{i}] should be >= HA Close[{i}]");
                Assert.IsTrue(result.Low[i] <= result.Open[i], $"HA Low[{i}] should be <= HA Open[{i}]");
                Assert.IsTrue(result.Low[i] <= result.Close[i], $"HA Low[{i}] should be <= HA Close[{i}]");
                Assert.IsTrue(result.High[i] >= result.Low[i], $"HA High[{i}] should be >= HA Low[{i}]");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_DifferentArrayLengths_UsesMinimum()
        {
            // Arrange - Arrays of different lengths
            double[] open = { 100, 101, 102, 103 };
            double[] high = { 105, 106, 107 }; // Shortest
            double[] low = { 99, 100, 101, 102, 103 };
            double[] close = { 104, 105, 106, 107, 108, 109 };

            // Act
            var result = HeikenAshi.Calculate(open, high, low, close);

            // Assert - Should use length of shortest array (high = 3)
            Assert.AreEqual(3, result.Open.Length);
            Assert.AreEqual(3, result.High.Length);
            Assert.AreEqual(3, result.Low.Length);
            Assert.AreEqual(3, result.Close.Length);
            Assert.AreEqual(3, result.Color.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_WithEmptyArrays_ReturnsEmptyResult()
        {
            // Arrange
            double[] empty = { };

            // Act
            var result = HeikenAshi.Calculate(empty, empty, empty, empty);

            // Assert
            Assert.AreEqual(0, result.Open.Length);
            Assert.AreEqual(0, result.High.Length);
            Assert.AreEqual(0, result.Low.Length);
            Assert.AreEqual(0, result.Close.Length);
            Assert.AreEqual(0, result.Color.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_WithSingleBar_ReturnsOriginalValues()
        {
            // Arrange
            double[] open = { 100 };
            double[] high = { 105 };
            double[] low = { 99 };
            double[] close = { 104 };

            // Act
            var result = HeikenAshi.Calculate(open, high, low, close);

            // Assert
            Assert.AreEqual(1, result.Open.Length);
            Assert.AreEqual(100, result.Open[0], TOLERANCE);
            Assert.AreEqual(105, result.High[0], TOLERANCE);
            Assert.AreEqual(99, result.Low[0], TOLERANCE);
            Assert.AreEqual(104, result.Close[0], TOLERANCE);
            Assert.AreEqual(0, result.Color[0]); // 100 <= 104, so bullish
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_NullOpenArray_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                // Arrange
                double[] high = { 105 };
                double[] low = { 99 };
                double[] close = { 104 };

                // Act
                HeikenAshi.Calculate(null, high, low, close);
            });
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_NullHighArray_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                // Arrange
                double[] open = { 100 };
                double[] low = { 99 };
                double[] close = { 104 };

                // Act
                HeikenAshi.Calculate(open, null, low, close);
            });
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void HeikenAshi_Calculate_NullLowArray_ThrowsArgumentNullException()
        {
            // Arrange
            double[] open = { 100 };
            double[] high = { 105 };
            double[] close = { 104 };

            // Act
            HeikenAshi.Calculate(open, high, null, close);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void HeikenAshi_Calculate_NullCloseArray_ThrowsArgumentNullException()
        {
            // Arrange
            double[] open = { 100 };
            double[] high = { 105 };
            double[] low = { 99 };

            // Act
            HeikenAshi.Calculate(open, high, low, null);
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_RealWorldExample_ProducesValidResults()
        {
            // Arrange - Simulated realistic stock price data
            double[] open =
            {
                100.0, 101.5, 99.8, 102.3, 104.1, 103.7, 105.2, 104.8, 106.5, 108.2,
                107.9, 109.4, 108.1, 110.7, 112.3, 111.8, 113.5, 115.2, 114.9, 116.7
            };
            double[] high =
            {
                102.5, 103.2, 102.1, 104.8, 106.3, 105.9, 107.4, 107.1, 108.9, 110.6,
                110.3, 111.8, 110.5, 113.2, 114.8, 114.3, 115.9, 117.6, 117.3, 119.1
            };
            double[] low =
            {
                99.5, 100.8, 98.2, 101.7, 103.5, 102.9, 104.6, 103.9, 105.8, 107.5,
                106.8, 108.7, 107.4, 109.9, 111.6, 110.9, 112.8, 114.5, 113.8, 115.9
            };
            double[] close =
            {
                101.2, 102.8, 101.5, 103.9, 105.6, 104.2, 106.8, 106.1, 107.8, 109.5,
                108.6, 110.9, 109.3, 111.8, 113.4, 112.7, 114.6, 116.3, 115.6, 117.8
            };

            // Act
            var result = HeikenAshi.Calculate(open, high, low, close);

            // Assert
            Assert.AreEqual(20, result.Open.Length);

            // Verify all values are reasonable for stock data
            for (var i = 0; i < result.Open.Length; i++)
            {
                Assert.IsTrue(result.Open[i] > 90 && result.Open[i] < 130,
                    $"HA Open[{i}] = {result.Open[i]} should be within reasonable range");
                Assert.IsTrue(result.High[i] > 90 && result.High[i] < 130,
                    $"HA High[{i}] = {result.High[i]} should be within reasonable range");
                Assert.IsTrue(result.Low[i] > 90 && result.Low[i] < 130,
                    $"HA Low[{i}] = {result.Low[i]} should be within reasonable range");
                Assert.IsTrue(result.Close[i] > 90 && result.Close[i] < 130,
                    $"HA Close[{i}] = {result.Close[i]} should be within reasonable range");

                Assert.IsTrue(result.Color[i] == 0 || result.Color[i] == 1,
                    $"HA Color[{i}] should be 0 or 1");
            }

            // In an uptrend, we should see more bullish bars
            var bullishCount = 0;
            for (var i = 0; i < result.Color.Length; i++)
                if (result.Color[i] == 0)
                    bullishCount++;

            // With generally increasing prices, expect majority to be bullish
            Assert.IsTrue(bullishCount > result.Color.Length / 2,
                "Uptrending data should produce majority bullish Heiken Ashi bars");
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_ConsistentWithStandardDefinition()
        {
            // Arrange - Test against known Heiken Ashi values
            double[] open = { 10, 11, 12 };
            double[] high = { 12, 13, 14 };
            double[] low = { 9, 10, 11 };
            double[] close = { 11, 12, 13 };

            // Act
            var result = HeikenAshi.Calculate(open, high, low, close);

            // Assert - Manual verification of standard formulas
            // Bar 0: HA = Regular OHLC
            Assert.AreEqual(10, result.Open[0], TOLERANCE);
            Assert.AreEqual(12, result.High[0], TOLERANCE);
            Assert.AreEqual(9, result.Low[0], TOLERANCE);
            Assert.AreEqual(11, result.Close[0], TOLERANCE);

            // Bar 1:
            // HA Close = (11 + 13 + 10 + 12) / 4 = 11.5
            Assert.AreEqual(11.5, result.Close[1], TOLERANCE);

            // HA Open = (10 + 11) / 2 = 10.5
            Assert.AreEqual(10.5, result.Open[1], TOLERANCE);

            // HA High = Max(13, 10.5, 11.5) = 13
            Assert.AreEqual(13, result.High[1], TOLERANCE);

            // HA Low = Min(10, 10.5, 11.5) = 10
            Assert.AreEqual(10, result.Low[1], TOLERANCE);

            // Bar 2:
            // HA Close = (12 + 14 + 11 + 13) / 4 = 12.5
            Assert.AreEqual(12.5, result.Close[2], TOLERANCE);

            // HA Open = (10.5 + 11.5) / 2 = 11
            Assert.AreEqual(11, result.Open[2], TOLERANCE);

            // HA High = Max(14, 11, 12.5) = 14
            Assert.AreEqual(14, result.High[2], TOLERANCE);

            // HA Low = Min(11, 11, 12.5) = 11
            Assert.AreEqual(11, result.Low[2], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_EqualOpenClose_ReturnsCorrectColor()
        {
            // Arrange - Test edge case where HA Open equals HA Close
            double[] open = { 100, 100 };
            double[] high = { 100, 100 };
            double[] low = { 100, 100 };
            double[] close = { 100, 100 }; // Doji-like scenario

            // Act
            var result = HeikenAshi.Calculate(open, high, low, close);

            // Assert
            // When HA Open <= HA Close (equal), should be bullish (0)
            Assert.AreEqual(0, result.Color[0], "Equal Open/Close should be bullish (0)");
            Assert.AreEqual(0, result.Color[1], "Equal HA Open/HA Close should be bullish (0)");
        }

        [TestMethod][TestCategory("Core")]
        public void HeikenAshi_Calculate_LargeDataSet_PerformanceAndAccuracy()
        {
            // Arrange - Large dataset for performance and accuracy testing
            var size = 1000;
            var open = new double[size];
            var high = new double[size];
            var low = new double[size];
            var close = new double[size];

            var random = new Random(42); // Fixed seed for reproducibility
            var price = 100.0;

            for (var i = 0; i < size; i++)
            {
                var change = (random.NextDouble() - 0.5) * 4; // ±2 price movement
                price = Math.Max(1.0, price + change); // Keep price positive

                open[i] = price;
                high[i] = price + random.NextDouble() * 2;
                low[i] = price - random.NextDouble() * 2;
                close[i] = price + (random.NextDouble() - 0.5) * 3;

                // Ensure OHLC relationships
                high[i] = Math.Max(high[i], Math.Max(open[i], close[i]));
                low[i] = Math.Min(low[i], Math.Min(open[i], close[i]));
            }

            // Act
            var startTime = DateTime.Now;
            var result = HeikenAshi.Calculate(open, high, low, close);
            var endTime = DateTime.Now;

            // Assert
            Assert.AreEqual(size, result.Open.Length);

            // Performance check (should complete quickly)
            var duration = endTime - startTime;
            Assert.IsTrue(duration.TotalSeconds < 1.0, "Large dataset calculation should complete quickly");

            // Accuracy check - all values should be valid
            for (var i = 0; i < size; i++)
            {
                Assert.IsFalse(double.IsNaN(result.Open[i]), $"HA Open[{i}] should not be NaN");
                Assert.IsFalse(double.IsNaN(result.High[i]), $"HA High[{i}] should not be NaN");
                Assert.IsFalse(double.IsNaN(result.Low[i]), $"HA Low[{i}] should not be NaN");
                Assert.IsFalse(double.IsNaN(result.Close[i]), $"HA Close[{i}] should not be NaN");

                Assert.IsTrue(result.Color[i] == 0 || result.Color[i] == 1,
                    $"HA Color[{i}] should be 0 or 1");

                // Skip detailed OHLC validation for performance but spot check first few
                if (i < 10)
                    Assert.IsTrue(result.High[i] >= result.Low[i],
                        $"HA High[{i}] should be >= HA Low[{i}]");
            }
        }
    }
}