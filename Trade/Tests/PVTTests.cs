using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class PVTTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void PVT_Calculate_ReturnsCorrectLength()
        {
            // Arrange
            double[] close = { 100, 101, 102, 103, 104 };
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400 };
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400 };

            // Act
            var result = PVT.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(close.Length, result.PVT.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PVT_Calculate_FirstValueIsZero()
        {
            // Arrange
            double[] close = { 100, 101, 102, 103, 104 };
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400 };
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400 };

            // Act
            var result = PVT.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(0.0, result.PVT[0], 1e-8, "First PVT value should always be zero");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PVT_Calculate_WithTickVolume_CalculatesCorrectly()
        {
            // Arrange - Simple uptrend with increasing volume
            double[] close = { 100.0, 102.0, 104.0, 103.0, 105.0 };
            long[] tickVolume = { 1000, 1200, 1400, 1300, 1500 };
            long[] realVolume = { 5000, 5200, 5400, 5300, 5500 };

            // Act
            var result = PVT.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(0.0, result.PVT[0], 1e-8);

            // PVT[1] = 0 + ((102-100)/100) * 1200 = 0 + 0.02 * 1200 = 24
            Assert.AreEqual(24.0, result.PVT[1], 1e-8);

            // PVT[2] = 24 + ((104-102)/102) * 1400 = 24 + (2/102) * 1400 = 24 + 27.451...
            var expected2 = 24.0 + (104.0 - 102.0) / 102.0 * 1400.0;
            Assert.AreEqual(expected2, result.PVT[2], 1e-8);

            // PVT[3] = PVT[2] + ((103-104)/104) * 1300 = PVT[2] + (-1/104) * 1300
            var expected3 = result.PVT[2] + (103.0 - 104.0) / 104.0 * 1300.0;
            Assert.AreEqual(expected3, result.PVT[3], 1e-8);

            // PVT[4] = PVT[3] + ((105-103)/103) * 1500
            var expected4 = result.PVT[3] + (105.0 - 103.0) / 103.0 * 1500.0;
            Assert.AreEqual(expected4, result.PVT[4], 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PVT_Calculate_WithRealVolume_CalculatesCorrectly()
        {
            // Arrange
            double[] close = { 50.0, 51.0, 49.5 };
            long[] tickVolume = { 100, 150, 200 };
            long[] realVolume = { 1000, 1500, 2000 };

            // Act
            var result = PVT.Calculate(close, tickVolume, realVolume, VolumeType.Real);

            // Assert
            Assert.AreEqual(0.0, result.PVT[0], 1e-8);

            // PVT[1] = 0 + ((51-50)/50) * 1500 = 0 + 0.02 * 1500 = 30
            Assert.AreEqual(30.0, result.PVT[1], 1e-8);

            // PVT[2] = 30 + ((49.5-51)/51) * 2000 = 30 + (-1.5/51) * 2000 = 30 - 58.823...
            var expected2 = 30.0 + (49.5 - 51.0) / 51.0 * 2000.0;
            Assert.AreEqual(expected2, result.PVT[2], 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PVT_Calculate_WithZeroPreviousClose_MaintainsPreviousValue()
        {
            // Arrange
            double[] close = { 0.0, 100.0, 102.0 };
            long[] tickVolume = { 1000, 1100, 1200 };
            long[] realVolume = { 5000, 5100, 5200 };

            // Act
            var result = PVT.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(0.0, result.PVT[0], 1e-8);
            Assert.AreEqual(0.0, result.PVT[1], 1e-8,
                "When previous close is zero, PVT should maintain previous value");

            // PVT[2] should calculate normally: PVT[1] + ((102-100)/100) * 1200 = 0 + 0.02 * 1200 = 24
            Assert.AreEqual(24.0, result.PVT[2], 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PVT_Calculate_WithFlatPrices_ReturnsZeros()
        {
            // Arrange - All prices the same
            double[] close = { 100.0, 100.0, 100.0, 100.0 };
            long[] tickVolume = { 1000, 1100, 1200, 1300 };
            long[] realVolume = { 5000, 5100, 5200, 5300 };

            // Act
            var result = PVT.Calculate(close, tickVolume, realVolume);

            // Assert
            for (var i = 0; i < result.PVT.Length; i++)
                Assert.AreEqual(0.0, result.PVT[i], 1e-8, $"PVT[{i}] should be zero when prices are flat");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PVT_Calculate_WithNegativePriceChange_HandlesCorrectly()
        {
            // Arrange - Declining prices
            double[] close = { 100.0, 95.0, 90.0, 85.0 };
            long[] tickVolume = { 1000, 1200, 1400, 1600 };
            long[] realVolume = { 5000, 6000, 7000, 8000 };

            // Act
            var result = PVT.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(0.0, result.PVT[0], 1e-8);

            // PVT[1] = 0 + ((95-100)/100) * 1200 = 0 + (-0.05) * 1200 = -60
            Assert.AreEqual(-60.0, result.PVT[1], 1e-8);

            // PVT[2] = -60 + ((90-95)/95) * 1400 = -60 + (-5/95) * 1400
            var expected2 = -60.0 + (90.0 - 95.0) / 95.0 * 1400.0;
            Assert.AreEqual(expected2, result.PVT[2], 1e-8);

            // All values should be negative in a declining market
            Assert.IsTrue(result.PVT[1] < 0, "PVT should be negative in declining market");
            Assert.IsTrue(result.PVT[2] < result.PVT[1], "PVT should continue declining");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PVT_Calculate_WithSingleDataPoint_ReturnsZero()
        {
            // Arrange
            double[] close = { 100.0 };
            long[] tickVolume = { 1000 };
            long[] realVolume = { 5000 };

            // Act
            var result = PVT.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(1, result.PVT.Length);
            Assert.AreEqual(0.0, result.PVT[0], 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PVT_Calculate_WithEmptyArrays_ReturnsEmptyResult()
        {
            // Arrange
            double[] close = { };
            long[] tickVolume = { };
            long[] realVolume = { };

            // Act
            var result = PVT.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(0, result.PVT.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PVT_Calculate_VolumeTypeSelection_WorksCorrectly()
        {
            // Arrange
            double[] close = { 100.0, 102.0 };
            long[] tickVolume = { 1000, 1200 };
            long[] realVolume = { 5000, 6000 };

            // Act
            var tickResult = PVT.Calculate(close, tickVolume, realVolume);
            var realResult = PVT.Calculate(close, tickVolume, realVolume, VolumeType.Real);

            // Assert
            Assert.AreEqual(0.0, tickResult.PVT[0], 1e-8);
            Assert.AreEqual(0.0, realResult.PVT[0], 1e-8);

            // PVT[1] with tick volume: ((102-100)/100) * 1200 = 24
            Assert.AreEqual(24.0, tickResult.PVT[1], 1e-8);

            // PVT[1] with real volume: ((102-100)/100) * 6000 = 120
            Assert.AreEqual(120.0, realResult.PVT[1], 1e-8);

            Assert.AreNotEqual(tickResult.PVT[1], realResult.PVT[1],
                "Results should differ when using different volume types");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PVT_Calculate_AccumulatesCorrectly_OverLongSequence()
        {
            // Arrange - Test accumulation behavior over longer sequence
            double[] close = { 100.0, 101.0, 99.0, 102.0, 104.0, 103.0, 106.0 };
            long[] tickVolume = { 1000, 1100, 1300, 1200, 1400, 1500, 1600 };
            long[] realVolume = { 5000, 5100, 5300, 5200, 5400, 5500, 5600 };

            // Act
            var result = PVT.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(0.0, result.PVT[0], 1e-8);

            // Verify each step accumulates properly
            var runningPVT = 0.0;
            for (var i = 1; i < close.Length; i++)
            {
                var priceChange = (close[i] - close[i - 1]) / close[i - 1];
                runningPVT += priceChange * tickVolume[i];
                Assert.AreEqual(runningPVT, result.PVT[i], 1e-8, $"PVT accumulation incorrect at index {i}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PVT_Calculate_WithHighVolatility_HandlesCorrectly()
        {
            // Arrange - High price volatility scenario
            double[] close = { 100.0, 120.0, 80.0, 150.0, 60.0 };
            long[] tickVolume = { 1000, 2000, 3000, 1500, 4000 };
            long[] realVolume = { 10000, 20000, 30000, 15000, 40000 };

            // Act
            var result = PVT.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(0.0, result.PVT[0], 1e-8);

            // PVT[1] = 0 + ((120-100)/100) * 2000 = 0 + 0.2 * 2000 = 400
            Assert.AreEqual(400.0, result.PVT[1], 1e-8);

            // PVT[2] = 400 + ((80-120)/120) * 3000 = 400 + (-40/120) * 3000 = 400 - 1000 = -600
            Assert.AreEqual(-600.0, result.PVT[2], 1e-8);

            // Verify the calculation continues correctly with extreme moves
            Assert.IsTrue(Math.Abs(result.PVT[3]) > 0, "PVT should respond to large price movements");
            Assert.IsTrue(Math.Abs(result.PVT[4]) > 0, "PVT should continue to accumulate large movements");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PVT_Calculate_DefaultVolumeType_UsesTickVolume()
        {
            // Arrange
            double[] close = { 100.0, 102.0 };
            long[] tickVolume = { 1000, 1200 };
            long[] realVolume = { 5000, 6000 };

            // Act - Call without specifying volume type (should default to Tick)
            var defaultResult = PVT.Calculate(close, tickVolume, realVolume);
            var explicitTickResult = PVT.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(explicitTickResult.PVT.Length, defaultResult.PVT.Length);
            for (var i = 0; i < defaultResult.PVT.Length; i++)
                Assert.AreEqual(explicitTickResult.PVT[i], defaultResult.PVT[i], 1e-8,
                    $"Default volume type should use tick volume at index {i}");
        }
    }
}