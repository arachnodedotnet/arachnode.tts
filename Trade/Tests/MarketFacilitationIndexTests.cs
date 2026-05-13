using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class MarketFacilitationIndexTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod][TestCategory("Core")]
        public void Calculate_EmptyArrays_ReturnsEmptyResults()
        {
            // Arrange
            var high = new double[0];
            var low = new double[0];
            var volume = new long[0];

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.MFI.Length);
            Assert.AreEqual(0, result.ColorIndex.Length);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Calculate_NullHighArray_ThrowsArgumentNullException()
        {
            // Arrange
            double[] high = null;
            double[] low = { 1.0 };
            long[] volume = { 1000 };

            // Act
            MarketFacilitationIndex.Calculate(high, low, volume);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Calculate_NullLowArray_ThrowsArgumentNullException()
        {
            // Arrange
            double[] high = { 2.0 };
            double[] low = null;
            long[] volume = { 1000 };

            // Act
            MarketFacilitationIndex.Calculate(high, low, volume);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Calculate_NullVolumeArray_ThrowsArgumentNullException()
        {
            // Arrange
            double[] high = { 2.0 };
            double[] low = { 1.0 };
            long[] volume = null;

            // Act
            MarketFacilitationIndex.Calculate(high, low, volume);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void Calculate_ZeroPoint_ThrowsArgumentException()
        {
            // Arrange
            double[] high = { 1.2345 };
            double[] low = { 1.2300 };
            long[] volume = { 1000 };

            // Act
            MarketFacilitationIndex.Calculate(high, low, volume, 0.0);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void Calculate_NegativePoint_ThrowsArgumentException()
        {
            // Arrange
            double[] high = { 1.2345 };
            double[] low = { 1.2300 };
            long[] volume = { 1000 };

            // Act
            MarketFacilitationIndex.Calculate(high, low, volume, -0.0001);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_UnequalArrayLengths_UsesMinimumLength()
        {
            // Arrange
            double[] high = { 100, 102, 105, 103, 101, 98, 99 }; // 7 elements
            double[] low = { 99, 100, 103, 102, 100 }; // 5 elements  
            long[] volume = { 1000, 1100, 1200 }; // 3 elements

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume);

            // Assert
            Assert.AreEqual(3, result.MFI.Length); // Should use minimum length (3)
            Assert.AreEqual(3, result.ColorIndex.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_SingleBar_ReturnsCorrectValues()
        {
            // Arrange
            double[] high = { 1.2345 };
            double[] low = { 1.2300 };
            long[] volume = { 1000 };
            var point = 0.0001;

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume, point);

            // Assert
            Assert.AreEqual(1, result.MFI.Length);
            Assert.AreEqual(1, result.ColorIndex.Length);

            // MFI = (High - Low) / Point / Volume
            // = (1.2345 - 1.2300) / 0.0001 / 1000 = 0.0045 / 0.0001 / 1000 = 45 / 1000 = 0.045
            var expectedMFI = (1.2345 - 1.2300) / point / 1000;
            Assert.AreEqual(expectedMFI, result.MFI[0], TOLERANCE);

            // First bar uses default true values for mfiUp and volUp, so Green (0)
            Assert.AreEqual(0, result.ColorIndex[0]);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ZeroVolume_HandlesCorrectly()
        {
            // Arrange
            double[] high = { 1.2345, 1.2350, 1.2355 };
            double[] low = { 1.2300, 1.2305, 1.2310 };
            long[] volume = { 1000, 0, 1500 }; // Zero volume in middle

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume);

            // Assert
            Assert.AreEqual(3, result.MFI.Length);

            // First bar should calculate normally
            Assert.IsTrue(result.MFI[0] > 0);

            // Second bar should inherit previous value due to zero volume
            Assert.AreEqual(result.MFI[0], result.MFI[1], TOLERANCE);

            // Third bar should calculate normally
            Assert.IsTrue(result.MFI[2] > 0);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_FirstBarZeroVolume_SetsZero()
        {
            // Arrange
            double[] high = { 1.2345, 1.2350 };
            double[] low = { 1.2300, 1.2305 };
            long[] volume = { 0, 1000 }; // Zero volume in first bar

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume);

            // Assert
            Assert.AreEqual(0.0, result.MFI[0], TOLERANCE);
            Assert.IsTrue(result.MFI[1] > 0);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_MFIUpVolumeUp_ReturnsGreenColor()
        {
            // Arrange - MFI increasing, Volume increasing
            double[] high = { 1.2300, 1.2450 }; // Much wider spread on second bar
            double[] low = { 1.2250, 1.2250 }; // Same low to ensure bigger range
            long[] volume = { 2000, 3000 }; // Volume increasing
            var point = 0.0001;

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume, point);

            // Assert
            // First bar: default Green (0)
            Assert.AreEqual(0, result.ColorIndex[0]); // Green

            // Manual calculation verification:
            // Bar 1 MFI = (1.2300 - 1.2250) / 0.0001 / 2000 = 0.005 / 0.0001 / 2000 = 50 / 2000 = 0.025
            // Bar 2 MFI = (1.2450 - 1.2250) / 0.0001 / 3000 = 0.02 / 0.0001 / 3000 = 200 / 3000 = 0.0667
            // MFI up (0.0667 > 0.025), Volume up (3000 > 2000) = Green
            Assert.AreEqual(0, result.ColorIndex[1]); // Green
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_MFIDownVolumeDown_ReturnsBrownColor()
        {
            // Arrange - MFI decreasing, Volume decreasing
            double[] high = { 1.2400, 1.2300 }; // Narrower spread on second bar
            double[] low = { 1.2200, 1.2250 };
            long[] volume = { 2000, 1000 }; // Volume decreasing
            var point = 0.0001;

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume, point);

            // Assert
            // Second bar: MFI down, Volume down = Brown
            Assert.AreEqual(1, result.ColorIndex[1]); // Brown
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_MFIUpVolumeDown_ReturnsBlueColor()
        {
            // Arrange - MFI increasing, Volume decreasing
            double[] high = { 1.2300, 1.2400 }; // Much wider spread
            double[] low = { 1.2250, 1.2200 };
            long[] volume = { 2000, 100 }; // Volume decreasing significantly

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume);

            // Assert
            // Second bar: MFI up (much wider spread more than compensates for less volume), Volume down = Blue
            Assert.AreEqual(2, result.ColorIndex[1]); // Blue
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_MFIDownVolumeUp_ReturnsPinkColor()
        {
            // Arrange - MFI decreasing, Volume increasing
            double[] high = { 1.2400, 1.2350 }; // Narrower spread
            double[] low = { 1.2200, 1.2300 };
            long[] volume = { 100, 5000 }; // Volume increasing significantly

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume);

            // Assert
            // Second bar: MFI down (narrower spread and much more volume), Volume up = Pink
            Assert.AreEqual(3, result.ColorIndex[1]); // Pink
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_BillWilliamsColorInterpretation_CorrectMeaning()
        {
            // Bill Williams color meanings:
            // Green (0): MFI up + Volume up = Market is facilitating (trending)
            // Brown (1): MFI down + Volume down = Market is squat (prepare for breakout)
            // Blue (2): MFI up + Volume down = Fake movement (fade the move)
            // Pink (3): MFI down + Volume up = Market is eating (stopping volume)

            // Arrange - Create a sequence demonstrating all colors
            double[] high = { 1.2300, 1.2400, 1.2350, 1.2380, 1.2320 };
            double[] low = { 1.2200, 1.2300, 1.2300, 1.2350, 1.2280 };
            long[] volume = { 1000, 2000, 1500, 200, 3000 };

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume);

            // Assert
            Assert.AreEqual(5, result.ColorIndex.Length);

            // Verify all color indices are valid (0-3)
            foreach (var color in result.ColorIndex)
                Assert.IsTrue(color >= 0 && color <= 3, "Color index should be between 0-3");

            // Should contain multiple different colors in this varied pattern
            var uniqueColors = result.ColorIndex.Distinct().ToArray();
            Assert.IsTrue(uniqueColors.Length >= 2, "Should contain at least 2 different colors in this pattern");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_RealWorldExample_ProducesReasonableValues()
        {
            // Arrange - Simulated real EUR/USD data
            double[] high = { 1.1850, 1.1865, 1.1845, 1.1870, 1.1855, 1.1880, 1.1875 };
            double[] low = { 1.1820, 1.1840, 1.1825, 1.1850, 1.1835, 1.1860, 1.1850 };
            long[] volume = { 1500, 2200, 1800, 2800, 1200, 3500, 2100 };
            var point = 0.0001; // Forex pip

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume, point);

            // Assert
            Assert.AreEqual(7, result.MFI.Length);
            Assert.AreEqual(7, result.ColorIndex.Length);

            // All MFI values should be positive for normal price ranges
            foreach (var mfi in result.MFI)
                Assert.IsTrue(mfi >= 0, "MFI values should be non-negative for normal price ranges");

            // All color indices should be valid (0-3)
            foreach (var color in result.ColorIndex)
                Assert.IsTrue(color >= 0 && color <= 3, "Color index should be between 0-3");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_DefaultPoint_UsesForexDefault()
        {
            // Arrange
            double[] high = { 1.2345 };
            double[] low = { 1.2300 };
            long[] volume = { 1000 };

            // Act - Don't specify point parameter
            var result = MarketFacilitationIndex.Calculate(high, low, volume);

            // Assert
            // Should use default point = 0.0001
            var expectedMFI = (1.2345 - 1.2300) / 0.0001 / 1000;
            Assert.AreEqual(expectedMFI, result.MFI[0], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_DifferentPointSizes_ScalesCorrectly()
        {
            // Arrange
            double[] high = { 100.50 };
            double[] low = { 100.00 };
            long[] volume = { 1000 };

            // Act - Test different point sizes
            var forexResult = MarketFacilitationIndex.Calculate(high, low, volume);
            var stockResult = MarketFacilitationIndex.Calculate(high, low, volume, 0.01);

            // Assert
            // Stock MFI should be 100x smaller due to larger point size
            Assert.AreEqual(forexResult.MFI[0] / 100, stockResult.MFI[0], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_IdenticalConsecutiveBars_HandlesEqualityCorrectly()
        {
            // Arrange - Test the equality conditions in color logic
            double[] high = { 1.2300, 1.2300 }; // Identical high
            double[] low = { 1.2250, 1.2250 }; // Identical low
            long[] volume = { 1000, 1000 }; // Identical volume

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume);

            // Assert
            // MFI should be identical
            Assert.AreEqual(result.MFI[0], result.MFI[1], TOLERANCE);

            // mfiUp = false (equal, not >), volUp = false (equal, not >)
            // Should be Brown color (both down)
            Assert.AreEqual(1, result.ColorIndex[1]);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_LargeDataSet_PerformsEfficiently()
        {
            // Arrange - Large dataset to test performance
            var size = 10000;
            var high = new double[size];
            var low = new double[size];
            var volume = new long[size];

            var random = new Random(42); // Fixed seed for reproducibility
            var basePrice = 1.2000;

            for (var i = 0; i < size; i++)
            {
                var spread = random.NextDouble() * 0.01; // 0-100 pips spread
                high[i] = basePrice + spread;
                low[i] = basePrice;
                volume[i] = random.Next(1000, 10000);
                basePrice += (random.NextDouble() - 0.5) * 0.005; // Random walk
            }

            // Act & Assert - Should complete without timeout
            var stopwatch = Stopwatch.StartNew();
            var result = MarketFacilitationIndex.Calculate(high, low, volume);
            stopwatch.Stop();

            Assert.AreEqual(size, result.MFI.Length);
            Assert.AreEqual(size, result.ColorIndex.Length);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, "Should complete within 1 second");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_MFIFormula_MatchesExpectedCalculation()
        {
            // Arrange - Manual calculation test
            double[] high = { 1.2350 };
            double[] low = { 1.2300 };
            long[] volume = { 2000 };
            var point = 0.0001;

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume, point);

            // Assert - Manual calculation
            var priceRange = 1.2350 - 1.2300; // = 0.0050
            var pointsRange = priceRange / point; // = 0.0050 / 0.0001 = 50
            var expectedMFI = pointsRange / 2000; // = 50 / 2000 = 0.025
            Assert.AreEqual(0.025, result.MFI[0], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void MarketFacilitationIndexResult_PropertiesInitialized()
        {
            // Arrange & Act
            var result = new MarketFacilitationIndexResult();

            // Assert
            Assert.IsNull(result.MFI);
            Assert.IsNull(result.ColorIndex);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ZeroPriceRange_ProducesZeroMFI()
        {
            // Arrange - High equals Low (no price movement)
            double[] high = { 1.2300, 1.2300 };
            double[] low = { 1.2300, 1.2300 };
            long[] volume = { 1000, 1500 };

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume);

            // Assert
            Assert.AreEqual(0.0, result.MFI[0], TOLERANCE);
            Assert.AreEqual(0.0, result.MFI[1], TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_FormulaDocumentation_VerifiesCorrectImplementation()
        {
            // This test documents and verifies the correct Bill Williams MFI formula
            // MFI = (High - Low) / Volume (normalized by point size)

            // Arrange
            double[] high = { 1.2345 };
            double[] low = { 1.2300 };
            long[] volume = { 1000 };
            var point = 0.0001;

            // Act
            var result = MarketFacilitationIndex.Calculate(high, low, volume, point);

            // Assert - Step by step calculation
            var priceRange = high[0] - low[0]; // 0.0045
            var pointsInRange = priceRange / point; // 45 points
            var mfiValue = pointsInRange / volume[0]; // 45 / 1000 = 0.045

            Assert.AreEqual(mfiValue, result.MFI[0], TOLERANCE);

            // This formula measures how much price movement occurs per unit of volume
            // Higher MFI = more price movement per unit of volume (more efficient market)
            // Lower MFI = less price movement per unit of volume (less efficient market)
        }
    }
}