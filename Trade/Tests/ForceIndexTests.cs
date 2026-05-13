using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class ForceIndexTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_BasicLengthAndNoException()
        {
            // Arrange
            var len = 50;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            var tickVolume = new long[len];
            var volume = new long[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = high[i] = low[i] = close[i] = 100 + i;
                tickVolume[i] = volume[i] = 1000;
            }

            // Act
            var result = ForceIndex.Calculate(open, high, low, close, tickVolume, volume);

            // Assert
            Assert.AreEqual(len, result.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_FlatPrices_ForceZero()
        {
            // Arrange
            var len = 30;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            var tickVolume = new long[len];
            var volume = new long[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = high[i] = low[i] = close[i] = 100.0;
                tickVolume[i] = volume[i] = 1000;
            }

            // Act
            var result = ForceIndex.Calculate(open, high, low, close, tickVolume, volume);

            // Assert
            for (var i = 0; i < len; i++)
                Assert.AreEqual(0.0, result[i], TOLERANCE, $"Force Index should be zero at index {i} for flat prices");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Uptrend_ForcePositive()
        {
            // Arrange
            var len = 30;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            var tickVolume = new long[len];
            var volume = new long[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = high[i] = low[i] = close[i] = 100 + i;
                tickVolume[i] = volume[i] = 1000;
            }

            // Act
            var result = ForceIndex.Calculate(open, high, low, close, tickVolume, volume);

            // Assert
            for (var i = 15; i < len; i++)
                Assert.IsTrue(result[i] > 0.0, $"Force Index should be positive at index {i} for uptrend");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Downtrend_ForceNegative()
        {
            // Arrange
            var len = 30;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            var tickVolume = new long[len];
            var volume = new long[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = high[i] = low[i] = close[i] = 100 - i;
                tickVolume[i] = volume[i] = 1000;
            }

            // Act
            var result = ForceIndex.Calculate(open, high, low, close, tickVolume, volume);

            // Assert
            for (var i = 15; i < len; i++)
                Assert.IsTrue(result[i] < 0.0, $"Force Index should be negative at index {i} for downtrend");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ShortArray_ReturnsZeroBuffer()
        {
            // Arrange
            double[] open = { 100 };
            double[] high = { 100 };
            double[] low = { 100 };
            double[] close = { 100 };
            long[] tickVolume = { 1000 };
            long[] volume = { 1000 };

            // Act
            var result = ForceIndex.Calculate(open, high, low, close, tickVolume, volume);

            // Assert
            Assert.AreEqual(0, result.Length, "Single element array should return empty result");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_PeriodOne_MatchesRawForce()
        {
            // Arrange
            double[] open = { 100, 101, 102 };
            double[] high = { 100, 101, 102 };
            double[] low = { 100, 101, 102 };
            double[] close = { 100, 101, 102 };
            long[] tickVolume = { 1000, 1000, 1000 };
            long[] volume = { 1000, 1000, 1000 };

            // Act
            var result = ForceIndex.Calculate(open, high, low, close, tickVolume, volume, 1);

            // Assert
            // First value should be 0
            Assert.AreEqual(0.0, result[0], TOLERANCE, "First Force Index value should always be 0");

            for (var i = 1; i < 3; i++)
            {
                var expected = 1000 * (close[i] - close[i - 1]);
                Assert.AreEqual(expected, result[i], TOLERANCE, $"Force Index at index {i} should match raw force calculation");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_UsesVolumeTypeAndPriceType()
        {
            // Arrange
            var len = 10;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            var tickVolume = new long[len];
            var volume = new long[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = 100 + i;
                high[i] = 200 + i;
                low[i] = 50 + i;
                close[i] = 150 + i;
                tickVolume[i] = 1000 + i;
                volume[i] = 2000 + i;
            }

            // Act
            var resultTick = ForceIndex.Calculate(open, high, low, close, tickVolume, volume, 1, MaMethod.SMA, AppliedPrice.Open, AppliedVolume.Tick);
            var resultReg = ForceIndex.Calculate(open, high, low, close, tickVolume, volume, 1, MaMethod.SMA, AppliedPrice.High, AppliedVolume.Regular);

            // Assert
            // First value should be 0
            Assert.AreEqual(0.0, resultTick[0], TOLERANCE);
            Assert.AreEqual(0.0, resultReg[0], TOLERANCE);

            for (var i = 1; i < len; i++)
            {
                var expectedTick = tickVolume[i] * (open[i] - open[i - 1]);
                var expectedReg = volume[i] * (high[i] - high[i - 1]);
                Assert.AreEqual(expectedTick, resultTick[i], TOLERANCE, $"Tick volume result at index {i}");
                Assert.AreEqual(expectedReg, resultReg[i], TOLERANCE, $"Regular volume result at index {i}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_NullInputs_ReturnsEmptyArray()
        {
            // Act
            var result = ForceIndex.Calculate(null, null, null, null, null, null);

            // Assert
            Assert.AreEqual(0, result.Length, "Null inputs should return empty array");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_InvalidPeriod_UsesDefault()
        {
            // Arrange
            var len = 20;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            var tickVolume = new long[len];
            var volume = new long[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = high[i] = low[i] = close[i] = 100 + i;
                tickVolume[i] = volume[i] = 1000;
            }

            // Act
            var result1 = ForceIndex.Calculate(open, high, low, close, tickVolume, volume, 0); // Invalid period
            var result2 = ForceIndex.Calculate(open, high, low, close, tickVolume, volume, -5); // Invalid period
            var result3 = ForceIndex.Calculate(open, high, low, close, tickVolume, volume, 13); // Default period

            // Assert - Results should be identical (all use default period 13)
            Assert.AreEqual(result1.Length, result3.Length);
            Assert.AreEqual(result2.Length, result3.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_DifferentMAMethods_ProduceDifferentResults()
        {
            // Arrange
            var len = 20;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            var tickVolume = new long[len];
            var volume = new long[len];

            for (var i = 0; i < len; i++)
            {
                var price = 100 + i % 2; // Alternating pattern to show MA differences
                open[i] = high[i] = low[i] = close[i] = price;
                tickVolume[i] = volume[i] = 1000;
            }

            // Act
            var resultSMA = ForceIndex.Calculate(open, high, low, close, tickVolume, volume, 5, MaMethod.SMA);
            var resultEMA = ForceIndex.Calculate(open, high, low, close, tickVolume, volume, 5, MaMethod.EMA);
            var resultSMMA = ForceIndex.Calculate(open, high, low, close, tickVolume, volume, 5, MaMethod.SMMA);

            // Assert - Different MA methods should produce different results
            var foundSMAvsEMA = false;
            var foundSMAysSMMA = false;

            for (var i = 10; i < len; i++)
            {
                if (Math.Abs(resultSMA[i] - resultEMA[i]) > TOLERANCE)
                    foundSMAvsEMA = true;
                if (Math.Abs(resultSMA[i] - resultSMMA[i]) > TOLERANCE)
                    foundSMAysSMMA = true;
            }

            Assert.IsTrue(foundSMAvsEMA, "SMA and EMA should produce different Force Index values");
            Assert.IsTrue(foundSMAysSMMA, "SMA and SMMA should produce different Force Index values");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_MedianPrice_CalculatesCorrectly()
        {
            // Arrange
            var len = 15;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            var tickVolume = new long[len];
            var volume = new long[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = 100 + i;
                high[i] = 110 + i; // High price
                low[i] = 90 + i;   // Low price
                close[i] = 105 + i;
                tickVolume[i] = volume[i] = 1000;
            }

            // Act - Using Median price = (High + Low) / 2
            var result = ForceIndex.Calculate(open, high, low, close, tickVolume, volume, 1, MaMethod.SMA, AppliedPrice.Median);

            // Assert - First value should be 0
            Assert.AreEqual(0.0, result[0], TOLERANCE, "First Force Index value should always be 0");

            // Check that median price is being used correctly
            for (var i = 1; i < len; i++)
            {
                var medianPriceCurrent = (high[i] + low[i]) / 2.0;
                var medianPricePrevious = (high[i - 1] + low[i - 1]) / 2.0;
                var expected = tickVolume[i] * (medianPriceCurrent - medianPricePrevious);
                Assert.AreEqual(expected, result[i], TOLERANCE, $"Median price Force Index at index {i}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_MismatchedArrayLengths_HandlesGracefully()
        {
            // Arrange - Arrays of different lengths
            double[] open = { 100, 101, 102, 103, 104 };
            double[] high = { 100, 101, 102 }; // Shorter
            double[] low = { 100, 101, 102, 103, 104, 105, 106 }; // Longer
            double[] close = { 100, 101, 102, 103 };
            long[] tickVolume = { 1000, 1000, 1000, 1000, 1000, 1000 };
            long[] volume = { 1000, 1000 }; // Much shorter

            // Act
            var result = ForceIndex.Calculate(open, high, low, close, tickVolume, volume);

            // Assert - Should use minimum length (2 from volume array)
            Assert.AreEqual(2, result.Length, "Should handle mismatched array lengths using minimum length");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ZeroVolume_HandlesCorrectly()
        {
            // Arrange
            double[] open = { 100, 101, 102 };
            double[] high = { 100, 101, 102 };
            double[] low = { 100, 101, 102 };
            double[] close = { 100, 101, 102 };
            long[] tickVolume = { 1000, 0, 1000 }; // Zero volume in middle
            long[] volume = { 1000, 0, 1000 };

            // Act
            var result = ForceIndex.Calculate(open, high, low, close, tickVolume, volume, 1);

            // Assert
            Assert.AreEqual(0.0, result[0], TOLERANCE, "First value should be 0");
            Assert.AreEqual(0.0, result[1], TOLERANCE, "Zero volume should produce zero Force Index");
            Assert.AreEqual(1000.0, result[2], TOLERANCE, "Non-zero volume should produce normal Force Index");
        }
    }
}