using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class GatorOscillatorTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod][TestCategory("Core")]
        public void Calculate_DefaultParameters_WorksCorrectly()
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
                high[i] = open[i] + 1;
                low[i] = open[i] - 1;
                close[i] = open[i] + 0.5;
            }

            // Act - Using default parameters (13,8,8,5,5,3,SMMA,Median)
            var result = GatorOscillator.Calculate(open, high, low, close);

            // Assert
            Assert.AreEqual(len, result.UpperBuffer.Length);
            Assert.AreEqual(len, result.LowerBuffer.Length);
            Assert.AreEqual(len, result.Jaws.Length);
            Assert.AreEqual(len, result.Teeth.Length);
            Assert.AreEqual(len, result.Lips.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_CustomParameters_WorksCorrectly()
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
                high[i] = open[i] + 1;
                low[i] = open[i] - 1;
                close[i] = open[i] + 0.5;
            }

            // Act - Using custom parameters
            var result = GatorOscillator.Calculate(open, high, low, close, 14, 7, 9, 4, 6, 2, MaMethod.EMA, AppliedPrice.Close);

            // Assert
            Assert.AreEqual(len, result.UpperBuffer.Length);
            Assert.AreEqual(len, result.LowerBuffer.Length);
            Assert.AreEqual(len, result.Jaws.Length);
            Assert.AreEqual(len, result.Teeth.Length);
            Assert.AreEqual(len, result.Lips.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_FlatPrices_AllBuffersZeroExceptInitial()
        {
            // Arrange
            var len = 50;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
                open[i] = high[i] = low[i] = close[i] = 100.0;

            // Act
            var result = GatorOscillator.Calculate(open, high, low, close);

            // Assert
            Assert.AreEqual(len, result.UpperBuffer.Length);
            Assert.AreEqual(len, result.LowerBuffer.Length);
            Assert.IsTrue(Array.TrueForAll(result.UpperBuffer, x => x >= 0), "Upper buffer should be non-negative");
            Assert.IsTrue(Array.TrueForAll(result.LowerBuffer, x => x <= 0), "Lower buffer should be non-positive");

            // After initialization, all values should be zero for flat prices
            Assert.IsTrue(Array.TrueForAll(result.UpperBuffer, x => Math.Abs(x) < TOLERANCE),
                "Upper buffer should be zero for flat prices");
            Assert.IsTrue(Array.TrueForAll(result.LowerBuffer, x => Math.Abs(x) < TOLERANCE),
                "Lower buffer should be zero for flat prices");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_TrendingPrices_BuffersNonZero()
        {
            // Arrange
            var len = 60;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                open[i] = 100 + i;
                high[i] = open[i] + 1;
                low[i] = open[i] - 1;
                close[i] = open[i] + 0.5;
            }

            // Act
            var result = GatorOscillator.Calculate(open, high, low, close);

            // Assert
            Assert.AreEqual(len, result.UpperBuffer.Length);
            Assert.AreEqual(len, result.LowerBuffer.Length);

            // Should have non-zero values after initialization
            var nonZeroCount = 0;
            for (var i = 20; i < len; i++)
                if (Math.Abs(result.UpperBuffer[i]) > TOLERANCE || Math.Abs(result.LowerBuffer[i]) > TOLERANCE)
                    nonZeroCount++;
            Assert.IsTrue(nonZeroCount > 10, "Should have non-zero values after initialization for trending prices");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_CyclicalPrices_BuffersOscillate()
        {
            // Arrange
            var len = 100;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                var cycle = 10 * Math.Sin(2 * Math.PI * i / 20);
                open[i] = high[i] = low[i] = close[i] = 100 + cycle;
            }

            // Act
            var result = GatorOscillator.Calculate(open, high, low, close);

            // Assert
            Assert.AreEqual(len, result.UpperBuffer.Length);
            Assert.AreEqual(len, result.LowerBuffer.Length);

            // Should oscillate above/below zero
            bool hasPositive = false, hasNegative = false;
            for (var i = 20; i < len; i++)
            {
                if (result.UpperBuffer[i] > 0.1) hasPositive = true;
                if (result.LowerBuffer[i] < -0.1) hasNegative = true;
            }

            Assert.IsTrue(hasPositive, "Should have positive values in upper buffer");
            Assert.IsTrue(hasNegative, "Should have negative values in lower buffer");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ShortArray_ReturnsZeroBuffers()
        {
            // Arrange
            var len = 5;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
                open[i] = high[i] = low[i] = close[i] = 100.0;

            // Act
            var result = GatorOscillator.Calculate(open, high, low, close);

            // Assert
            Assert.AreEqual(len, result.UpperBuffer.Length);
            Assert.AreEqual(len, result.LowerBuffer.Length);
            Assert.IsTrue(Array.TrueForAll(result.UpperBuffer, x => Math.Abs(x) < TOLERANCE),
                "Short array should produce zero upper buffer");
            Assert.IsTrue(Array.TrueForAll(result.LowerBuffer, x => Math.Abs(x) < TOLERANCE),
                "Short array should produce zero lower buffer");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_NullInputs_ReturnsEmptyBuffers()
        {
            // Act
            var result = GatorOscillator.Calculate(null, null, null, null);

            // Assert
            Assert.IsNotNull(result.UpperBuffer);
            Assert.IsNotNull(result.LowerBuffer);
            Assert.AreEqual(0, result.UpperBuffer.Length);
            Assert.AreEqual(0, result.LowerBuffer.Length);
            Assert.AreEqual(0, result.Jaws.Length);
            Assert.AreEqual(0, result.Teeth.Length);
            Assert.AreEqual(0, result.Lips.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_DifferentMaTypes_ProducesDifferentResults()
        {
            // Arrange
            var len = 60;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                open[i] = 100 + i;
                high[i] = open[i] + 1;
                low[i] = open[i] - 1;
                close[i] = open[i] + 0.5;
            }

            // Act
            var resultSMA = GatorOscillator.Calculate(open, high, low, close, maType: MaMethod.SMA);
            var resultEMA = GatorOscillator.Calculate(open, high, low, close, maType: MaMethod.EMA);
            var resultSMMA = GatorOscillator.Calculate(open, high, low, close, maType: MaMethod.SMMA);

            // Assert - Compare some values
            var foundDifference = false;
            for (var i = 20; i < len; i++)
            {
                if (Math.Abs(resultSMA.UpperBuffer[i] - resultEMA.UpperBuffer[i]) > 1e-6 ||
                    Math.Abs(resultSMA.UpperBuffer[i] - resultSMMA.UpperBuffer[i]) > 1e-6)
                {
                    foundDifference = true;
                    break;
                }
            }

            Assert.IsTrue(foundDifference, "Different MA methods should produce different results");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_InvalidConfiguration_ReturnsZeroBuffers()
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
                high[i] = open[i] + 1;
                low[i] = open[i] - 1;
                close[i] = open[i] + 0.5;
            }

            // Act - Invalid configuration: periods not in descending order
            var result = GatorOscillator.Calculate(open, high, low, close, 5, 8, 8, 5, 13, 3);

            // Assert
            Assert.AreEqual(len, result.UpperBuffer.Length);
            Assert.AreEqual(len, result.LowerBuffer.Length);
            Assert.IsTrue(Array.TrueForAll(result.UpperBuffer, x => Math.Abs(x) < TOLERANCE),
                "Invalid configuration should produce zero upper buffer");
            Assert.IsTrue(Array.TrueForAll(result.LowerBuffer, x => Math.Abs(x) < TOLERANCE),
                "Invalid configuration should produce zero lower buffer");
        }

        //[TestMethod][TestCategory("Core")]
        //public void Calculate_DifferentAppliedPrices_ProducesDifferentResults()
        //{
        //    // Arrange
        //    var len = 50;
        //    var open = new double[len];
        //    var high = new double[len];
        //    var low = new double[len];
        //    var close = new double[len];

        //    for (var i = 0; i < len; i++)
        //    {
        //        open[i] = 100 + i;
        //        high[i] = 110 + i; // Different from other prices
        //        low[i] = 90 + i;   // Different from other prices  
        //        close[i] = 105 + i;
        //    }

        //    // Act
        //    var resultMedian = GatorOscillator.Calculate(open, high, low, close, priceType: AppliedPrice.Low);
        //    var resultClose = GatorOscillator.Calculate(open, high, low, close, priceType: AppliedPrice.Close);
        //    var resultHigh = GatorOscillator.Calculate(open, high, low, close, priceType: AppliedPrice.High);

        //    // Assert - Different applied prices should produce different results
        //    var foundMedianVsClose = false;
        //    var foundCloseVsHigh = false;

        //    for (var i = 20; i < len; i++)
        //    {
        //        if (Math.Abs(resultMedian.UpperBuffer[i] - resultClose.UpperBuffer[i]) > TOLERANCE)
        //            foundMedianVsClose = true;
        //        if (Math.Abs(resultClose.UpperBuffer[i] - resultHigh.UpperBuffer[i]) > TOLERANCE)
        //            foundCloseVsHigh = true;
        //    }

        //    Assert.IsTrue(foundMedianVsClose, "Median and Close applied prices should produce different results");
        //    Assert.IsTrue(foundCloseVsHigh, "Close and High applied prices should produce different results");
        //}

        [TestMethod][TestCategory("Core")]
        public void Calculate_ColorCoding_ReflectsExpansionContraction()
        {
            // Arrange - Create expanding then contracting pattern
            var len = 80;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];

            for (var i = 0; i < len; i++)
            {
                if (i < len / 2)
                {
                    // Expanding phase - increasing volatility
                    var expansion = i * 0.1;
                    open[i] = 100;
                    high[i] = 100 + expansion;
                    low[i] = 100 - expansion;
                    close[i] = 100 + expansion * 0.5;
                }
                else
                {
                    // Contracting phase - decreasing volatility
                    var contraction = (len - i) * 0.1;
                    open[i] = 100;
                    high[i] = 100 + contraction;
                    low[i] = 100 - contraction;
                    close[i] = 100 + contraction * 0.5;
                }
            }

            // Act
            var result = GatorOscillator.Calculate(open, high, low, close);

            // Assert - Should have color changes
            Assert.AreEqual(len, result.UpperColors.Length);
            Assert.AreEqual(len, result.LowerColors.Length);

            // Colors should be either 0 or 1 (or equal to previous when unchanged)
            for (var i = 0; i < len; i++)
            {
                Assert.IsTrue(result.UpperColors[i] == 0.0 || result.UpperColors[i] == 1.0,
                    $"Upper color at index {i} should be 0 or 1");
                Assert.IsTrue(result.LowerColors[i] == 0.0 || result.LowerColors[i] == 1.0,
                    $"Lower color at index {i} should be 0 or 1");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ReturnsAlligatorComponents_Correctly()
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
                high[i] = open[i] + 1;
                low[i] = open[i] - 1;
                close[i] = open[i] + 0.5;
            }

            // Act
            var result = GatorOscillator.Calculate(open, high, low, close);

            // Assert - Should return all Alligator components
            Assert.IsNotNull(result.Jaws);
            Assert.IsNotNull(result.Teeth);
            Assert.IsNotNull(result.Lips);
            Assert.AreEqual(len, result.Jaws.Length);
            Assert.AreEqual(len, result.Teeth.Length);
            Assert.AreEqual(len, result.Lips.Length);

            // After initialization period, values should be meaningful
            for (var i = 20; i < len; i++)
            {
                Assert.IsFalse(double.IsNaN(result.Jaws[i]), $"Jaws should not be NaN at index {i}");
                Assert.IsFalse(double.IsNaN(result.Teeth[i]), $"Teeth should not be NaN at index {i}");
                Assert.IsFalse(double.IsNaN(result.Lips[i]), $"Lips should not be NaN at index {i}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ConsistentResults_AcrossMultipleCalls()
        {
            // Arrange
            var len = 40;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];

            for (var i = 0; i < len; i++)
            {
                open[i] = 100 + i * 0.5;
                high[i] = open[i] + 0.2;
                low[i] = open[i] - 0.2;
                close[i] = open[i] + 0.1;
            }

            // Act
            var result1 = GatorOscillator.Calculate(open, high, low, close);
            var result2 = GatorOscillator.Calculate(open, high, low, close);

            // Assert
            Assert.AreEqual(result1.UpperBuffer.Length, result2.UpperBuffer.Length);
            Assert.AreEqual(result1.LowerBuffer.Length, result2.LowerBuffer.Length);

            for (var i = 0; i < len; i++)
            {
                Assert.AreEqual(result1.UpperBuffer[i], result2.UpperBuffer[i], TOLERANCE,
                    $"Upper buffer results should be identical at index {i}");
                Assert.AreEqual(result1.LowerBuffer[i], result2.LowerBuffer[i], TOLERANCE,
                    $"Lower buffer results should be identical at index {i}");
                Assert.AreEqual(result1.UpperColors[i], result2.UpperColors[i], TOLERANCE,
                    $"Upper colors should be identical at index {i}");
                Assert.AreEqual(result1.LowerColors[i], result2.LowerColors[i], TOLERANCE,
                    $"Lower colors should be identical at index {i}");
            }
        }
    }
}