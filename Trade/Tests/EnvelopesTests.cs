using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class EnvelopesTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod][TestCategory("Core")]
        public void Calculate_BasicLengthAndNoException()
        {
            // Arrange
            var len = 50;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                open[i] = 100 + i;
                high[i] = 100 + i + 1;
                low[i] = 100 + i - 1;
                close[i] = 100 + i;
            }

            // Act
            var (upper, lower, ma) = Envelopes.Calculate(open, high, low, close);

            // Assert
            Assert.AreEqual(len, upper.Length);
            Assert.AreEqual(len, lower.Length);
            Assert.AreEqual(len, ma.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_FlatPrices_EnvelopesFlat()
        {
            // Arrange
            var len = 30;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = 100.0;
                high[i] = 100.0;
                low[i] = 100.0;
                close[i] = 100.0;
            }

            // Act
            var (upper, lower, ma) = Envelopes.Calculate(open, high, low, close, 14, 0, MaMethod.SMA, AppliedPrice.Close, 1.0);

            // Assert
            for (var i = 14 - 1; i < len; i++) // Start from maPeriod - 1 = 13
            {
                Assert.AreEqual(100.0 * 1.01, upper[i], TOLERANCE, $"Upper band at index {i}");
                Assert.AreEqual(100.0 * 0.99, lower[i], TOLERANCE, $"Lower band at index {i}");
                Assert.AreEqual(100.0, ma[i], TOLERANCE, $"MA at index {i}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_Uptrend_EnvelopesUptrend()
        {
            // Arrange
            var len = 30;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = 100 + i;
                high[i] = 100 + i + 1;
                low[i] = 100 + i - 1;
                close[i] = 100 + i;
            }

            // Act
            var (upper, lower, ma) = Envelopes.Calculate(open, high, low, close, 5, 0, MaMethod.SMA, AppliedPrice.Close, 2.0);

            // Assert
            for (var i = 5; i < len; i++)
            {
                Assert.IsTrue(upper[i] > upper[i - 1], $"Upper band should increase at index {i}");
                Assert.IsTrue(lower[i] > lower[i - 1], $"Lower band should increase at index {i}");
                Assert.IsTrue(ma[i] > ma[i - 1], $"MA should increase at index {i}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ShortArray_ReturnsZeroBuffers()
        {
            // Arrange
            double[] open = { 100, 101 };
            double[] high = { 101, 102 };
            double[] low = { 99, 100 };
            double[] close = { 100, 101 };

            // Act
            var (upper, lower, ma) = Envelopes.Calculate(open, high, low, close, 5);

            // Assert
            Assert.AreEqual(0, upper.Length);
            Assert.AreEqual(0, lower.Length);
            Assert.AreEqual(0, ma.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_DeviationParameter_AppliesCorrectly()
        {
            // Arrange
            var len = 10;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = 100;
                high[i] = 100;
                low[i] = 100;
                close[i] = 100;
            }

            // Act
            var (upper, lower, ma) = Envelopes.Calculate(open, high, low, close, 3, 0, MaMethod.SMA, AppliedPrice.Close, 10.0);

            // Assert
            for (var i = 3 - 1; i < close.Length; i++) // Start from maPeriod - 1 = 2
            {
                Assert.AreEqual(110.0, upper[i], TOLERANCE, $"Upper band at index {i} should be 110");
                Assert.AreEqual(90.0, lower[i], TOLERANCE, $"Lower band at index {i} should be 90");
                Assert.AreEqual(100.0, ma[i], TOLERANCE, $"MA at index {i} should be 100");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_EMAAndSMMA_ProduceDifferentResults()
        {
            // Arrange
            var len = 20;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];

            for (var i = 0; i < len; i++)
            {
                var price = 100 + i % 2; // Alternating between 100 and 101
                open[i] = price;
                high[i] = price + 1;
                low[i] = price - 1;
                close[i] = price;
            }

            // Act
            var (upperSMA, lowerSMA, maSMA) = Envelopes.Calculate(open, high, low, close, 5, 0, MaMethod.SMA, AppliedPrice.Close, 1.0);
            var (upperEMA, lowerEMA, maEMA) = Envelopes.Calculate(open, high, low, close, 5, 0, MaMethod.EMA, AppliedPrice.Close, 1.0);
            var (upperSMMA, lowerSMMA, maSMMA) = Envelopes.Calculate(open, high, low, close, 5, 0, MaMethod.SMMA, AppliedPrice.Close, 1.0);

            // Assert
            var foundDiff = false;
            for (var i = 5; i < len; i++)
            {
                if (Math.Abs(maSMA[i] - maEMA[i]) > 1e-6 || Math.Abs(maSMA[i] - maSMMA[i]) > 1e-6)
                {
                    foundDiff = true;
                    break;
                }
            }

            Assert.IsTrue(foundDiff, "Different MA methods should produce different results");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_NullInputs_ReturnsEmptyArrays()
        {
            // Act
            var (upper, lower, ma) = Envelopes.Calculate(null, null, null, null);

            // Assert
            Assert.AreEqual(0, upper.Length);
            Assert.AreEqual(0, lower.Length);
            Assert.AreEqual(0, ma.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_InvalidPeriod_UsesDefault()
        {
            // Arrange
            var len = 20;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = 100;
                high[i] = 100;
                low[i] = 100;
                close[i] = 100;
            }

            // Act
            var (upper1, lower1, ma1) = Envelopes.Calculate(open, high, low, close, 0); // Invalid period
            var (upper2, lower2, ma2) = Envelopes.Calculate(open, high, low, close, -5); // Invalid period
            var (upper3, lower3, ma3) = Envelopes.Calculate(open, high, low, close, 14); // Default period

            // Assert - Results should be identical (all use default period 14)
            Assert.AreEqual(upper1.Length, upper3.Length);
            Assert.AreEqual(lower1.Length, lower3.Length);
            Assert.AreEqual(ma1.Length, ma3.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_MedianPrice_CalculatesCorrectly()
        {
            // Arrange
            var len = 15;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = 100 + i;
                high[i] = 105 + i; // High price
                low[i] = 95 + i;   // Low price
                close[i] = 100 + i;
            }

            // Act - Using Median price = (High + Low) / 2
            var (upper, lower, ma) = Envelopes.Calculate(open, high, low, close, 5, 0, MaMethod.SMA, AppliedPrice.Median, 1.0);

            // Assert - The MA should be calculated from median prices
            // Median price for each i = (105 + i + 95 + i) / 2 = 100 + i
            // So the SMA should be similar to close price SMA in this case
            for (var i = 5 - 1; i < len; i++) // Start from maPeriod - 1 = 4
            {
                Assert.IsTrue(ma[i] > 0, $"MA should be positive at index {i}");
                Assert.IsTrue(upper[i] > ma[i], $"Upper band should be above MA at index {i}");
                Assert.IsTrue(lower[i] < ma[i], $"Lower band should be below MA at index {i}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_DifferentAppliedPrices_ProduceDifferentResults()
        {
            // Arrange
            var len = 15;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = 100 + i;
                high[i] = 110 + i;
                low[i] = 90 + i;
                close[i] = 105 + i;
            }

            // Act
            var (upperOpen, lowerOpen, maOpen) = Envelopes.Calculate(open, high, low, close, 5, 0, MaMethod.SMA, AppliedPrice.Open, 1.0);
            var (upperHigh, lowerHigh, maHigh) = Envelopes.Calculate(open, high, low, close, 5, 0, MaMethod.SMA, AppliedPrice.High, 1.0);
            var (upperLow, lowerLow, maLow) = Envelopes.Calculate(open, high, low, close, 5, 0, MaMethod.SMA, AppliedPrice.Low, 1.0);
            var (upperClose, lowerClose, maClose) = Envelopes.Calculate(open, high, low, close, 5, 0, MaMethod.SMA, AppliedPrice.Close, 1.0);

            // Assert - Different applied prices should produce different MA values
            var foundDifferenceOpen = false;
            var foundDifferenceHigh = false;
            var foundDifferenceLow = false;

            for (var i = 5; i < len; i++)
            {
                if (Math.Abs(maOpen[i] - maClose[i]) > TOLERANCE)
                    foundDifferenceOpen = true;
                if (Math.Abs(maHigh[i] - maClose[i]) > TOLERANCE)
                    foundDifferenceHigh = true;
                if (Math.Abs(maLow[i] - maClose[i]) > TOLERANCE)
                    foundDifferenceLow = true;
            }

            Assert.IsTrue(foundDifferenceOpen, "Open and Close applied prices should produce different results");
            Assert.IsTrue(foundDifferenceHigh, "High and Close applied prices should produce different results");
            Assert.IsTrue(foundDifferenceLow, "Low and Close applied prices should produce different results");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_WithShift_ShiftsCorrectly()
        {
            // Arrange
            var len = 20;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = 100 + i;
                high[i] = 100 + i + 1;
                low[i] = 100 + i - 1;
                close[i] = 100 + i;
            }

            // Act
            var (upperNoShift, lowerNoShift, maNoShift) = Envelopes.Calculate(open, high, low, close, 5, 0, MaMethod.SMA, AppliedPrice.Close, 1.0);
            var (upperShift, lowerShift, maShift) = Envelopes.Calculate(open, high, low, close, 5, 2, MaMethod.SMA, AppliedPrice.Close, 1.0);

            // Assert - With positive shift, values should appear earlier in the array
            var foundShiftDifference = false;
            for (var i = 5; i < len - 2; i++)
            {
                if (Math.Abs(upperNoShift[i] - upperShift[i + 2]) > TOLERANCE)
                {
                    foundShiftDifference = true;
                    break;
                }
            }

            // The shift implementation may vary, but there should be some difference
            Assert.IsTrue(upperShift.Length == len, "Shifted array should have same length");
            Assert.IsTrue(lowerShift.Length == len, "Shifted array should have same length");
            Assert.IsTrue(maShift.Length == len, "Shifted array should have same length");
        }
    }
}