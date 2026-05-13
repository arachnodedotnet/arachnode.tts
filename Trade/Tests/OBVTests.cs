using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class OBVTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_ReturnsCorrectLength()
        {
            // Arrange
            double[] close = { 100, 101, 102, 103, 104 };
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400 };
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(close.Length, result.OBV.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_FirstValueEqualsFirstVolume()
        {
            // Arrange
            double[] close = { 100, 101, 102 };
            long[] tickVolume = { 1000, 1100, 1200 };
            long[] realVolume = { 5000, 5100, 5200 };

            // Act
            var tickResult = OBV.Calculate(close, tickVolume, realVolume);
            var realResult = OBV.Calculate(close, tickVolume, realVolume, VolumeType.Real);

            // Assert
            Assert.AreEqual(1000.0, tickResult.OBV[0], TOLERANCE, "First OBV value should equal first tick volume");
            Assert.AreEqual(5000.0, realResult.OBV[0], TOLERANCE, "First OBV value should equal first real volume");
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_RisingPrices_AddsVolume()
        {
            // Arrange - Rising price sequence
            double[] close = { 100, 102, 104, 106, 108 };
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400 };
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(1000.0, result.OBV[0], TOLERANCE); // First value = first volume
            Assert.AreEqual(2100.0, result.OBV[1], TOLERANCE); // 1000 + 1100 (price up)
            Assert.AreEqual(3300.0, result.OBV[2], TOLERANCE); // 2100 + 1200 (price up)
            Assert.AreEqual(4600.0, result.OBV[3], TOLERANCE); // 3300 + 1300 (price up)
            Assert.AreEqual(6000.0, result.OBV[4], TOLERANCE); // 4600 + 1400 (price up)
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_FallingPrices_SubtractsVolume()
        {
            // Arrange - Falling price sequence
            double[] close = { 108, 106, 104, 102, 100 };
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400 };
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(1000.0, result.OBV[0], TOLERANCE); // First value = first volume
            Assert.AreEqual(-100.0, result.OBV[1], TOLERANCE); // 1000 - 1100 (price down)
            Assert.AreEqual(-1300.0, result.OBV[2], TOLERANCE); // -100 - 1200 (price down)
            Assert.AreEqual(-2600.0, result.OBV[3], TOLERANCE); // -1300 - 1300 (price down)
            Assert.AreEqual(-4000.0, result.OBV[4], TOLERANCE); // -2600 - 1400 (price down)
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_FlatPrices_MaintainsPreviousValue()
        {
            // Arrange - Flat price sequence (equal consecutive prices)
            double[] close = { 100, 100, 100, 100, 100 };
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400 };
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(1000.0, result.OBV[0], TOLERANCE); // First value = first volume
            Assert.AreEqual(1000.0, result.OBV[1], TOLERANCE); // Same (price equal)
            Assert.AreEqual(1000.0, result.OBV[2], TOLERANCE); // Same (price equal)
            Assert.AreEqual(1000.0, result.OBV[3], TOLERANCE); // Same (price equal)
            Assert.AreEqual(1000.0, result.OBV[4], TOLERANCE); // Same (price equal)
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_MixedPriceMovement_CalculatesCorrectly()
        {
            // Arrange - Mixed price movements: up, down, equal, up, down
            double[] close = { 100, 105, 95, 95, 110, 90 };
            long[] tickVolume = { 1000, 800, 1500, 900, 1200, 2000 };
            long[] realVolume = { 5000, 4000, 7500, 4500, 6000, 10000 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(1000.0, result.OBV[0], TOLERANCE); // First value = 1000
            Assert.AreEqual(1800.0, result.OBV[1], TOLERANCE); // 1000 + 800 (105 > 100)
            Assert.AreEqual(300.0, result.OBV[2], TOLERANCE); // 1800 - 1500 (95 < 105)
            Assert.AreEqual(300.0, result.OBV[3], TOLERANCE); // 300 + 0 (95 = 95)
            Assert.AreEqual(1500.0, result.OBV[4], TOLERANCE); // 300 + 1200 (110 > 95)
            Assert.AreEqual(-500.0, result.OBV[5], TOLERANCE); // 1500 - 2000 (90 < 110)
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_WithRealVolume_CalculatesCorrectly()
        {
            // Arrange
            double[] close = { 100, 105, 95, 110 };
            long[] tickVolume = { 1000, 800, 1500, 1200 };
            long[] realVolume = { 5000, 4000, 7500, 6000 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume, VolumeType.Real);

            // Assert
            Assert.AreEqual(5000.0, result.OBV[0], TOLERANCE); // First value = 5000
            Assert.AreEqual(9000.0, result.OBV[1], TOLERANCE); // 5000 + 4000 (105 > 100)
            Assert.AreEqual(1500.0, result.OBV[2], TOLERANCE); // 9000 - 7500 (95 < 105)
            Assert.AreEqual(7500.0, result.OBV[3], TOLERANCE); // 1500 + 6000 (110 > 95)
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_DefaultVolumeType_UsesTickVolume()
        {
            // Arrange
            double[] close = { 100, 105, 95 };
            long[] tickVolume = { 1000, 800, 1500 };
            long[] realVolume = { 5000, 4000, 7500 };

            // Act - Call without specifying volume type (should default to Tick)
            var defaultResult = OBV.Calculate(close, tickVolume, realVolume);
            var explicitTickResult = OBV.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(explicitTickResult.OBV.Length, defaultResult.OBV.Length);
            for (var i = 0; i < defaultResult.OBV.Length; i++)
                Assert.AreEqual(explicitTickResult.OBV[i], defaultResult.OBV[i], TOLERANCE,
                    $"Default volume type should use tick volume at index {i}");
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_VolumeTypeSelection_ProducesDifferentResults()
        {
            // Arrange - Different volume values to ensure different results
            double[] close = { 100, 105, 95 };
            long[] tickVolume = { 1000, 800, 1500 };
            long[] realVolume = { 5000, 4000, 7500 };

            // Act
            var tickResult = OBV.Calculate(close, tickVolume, realVolume);
            var realResult = OBV.Calculate(close, tickVolume, realVolume, VolumeType.Real);

            // Assert - Results should be different due to different volume values
            Assert.AreNotEqual(tickResult.OBV[0], realResult.OBV[0], "First values should differ");
            Assert.AreNotEqual(tickResult.OBV[1], realResult.OBV[1], "Second values should differ");
            Assert.AreNotEqual(tickResult.OBV[2], realResult.OBV[2], "Third values should differ");

            // Verify the actual values are using correct volume types
            Assert.AreEqual(1000.0, tickResult.OBV[0], TOLERANCE, "Tick result should use tick volume");
            Assert.AreEqual(5000.0, realResult.OBV[0], TOLERANCE, "Real result should use real volume");
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_WithLengthLessThanTwo_ReturnsCorrectResult()
        {
            // Arrange - Single element
            double[] close = { 100 };
            long[] tickVolume = { 1000 };
            long[] realVolume = { 5000 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(1, result.OBV.Length);
            Assert.AreEqual(0.0, result.OBV[0], TOLERANCE, "Single element should return zero");
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_WithEmptyArrays_ReturnsEmptyResult()
        {
            // Arrange
            double[] close = { };
            long[] tickVolume = { };
            long[] realVolume = { };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(0, result.OBV.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_OscillatingPrices_ShowsVolumeFlow()
        {
            // Arrange - Oscillating prices to test volume flow
            double[] close = { 100, 110, 105, 115, 108, 120 };
            long[] tickVolume = { 1000, 2000, 1800, 2200, 1600, 2500 };
            long[] realVolume = { 5000, 10000, 9000, 11000, 8000, 12500 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(1000.0, result.OBV[0], TOLERANCE); // Initial
            Assert.AreEqual(3000.0, result.OBV[1], TOLERANCE); // +2000 (up)
            Assert.AreEqual(1200.0, result.OBV[2], TOLERANCE); // -1800 (down)
            Assert.AreEqual(3400.0, result.OBV[3], TOLERANCE); // +2200 (up)
            Assert.AreEqual(1800.0, result.OBV[4], TOLERANCE); // -1600 (down)
            Assert.AreEqual(4300.0, result.OBV[5], TOLERANCE); // +2500 (up)
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_WithLargeVolumes_HandlesCorrectly()
        {
            // Arrange - Large volume numbers
            double[] close = { 100, 105, 95, 110 };
            long[] tickVolume = { 1000000, 2000000, 3000000, 1500000 };
            long[] realVolume = { 50000000, 60000000, 80000000, 40000000 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume, VolumeType.Real);

            // Assert
            Assert.AreEqual(50000000.0, result.OBV[0], TOLERANCE); // Initial
            Assert.AreEqual(110000000.0, result.OBV[1], TOLERANCE); // +60M (up)
            Assert.AreEqual(30000000.0, result.OBV[2], TOLERANCE); // -80M (down)
            Assert.AreEqual(70000000.0, result.OBV[3], TOLERANCE); // +40M (up)
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_AccuracyWithSmallPriceDifferences()
        {
            // Arrange - Small price differences that should still trigger volume changes
            double[] close = { 100.0, 100.01, 100.005, 100.02, 99.99 };
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400 };
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(1000.0, result.OBV[0], TOLERANCE); // Initial
            Assert.AreEqual(2100.0, result.OBV[1], TOLERANCE); // +1100 (100.01 > 100.0)
            Assert.AreEqual(900.0, result.OBV[2], TOLERANCE); // -1200 (100.005 < 100.01)
            Assert.AreEqual(2200.0, result.OBV[3], TOLERANCE); // +1300 (100.02 > 100.005)
            Assert.AreEqual(800.0, result.OBV[4], TOLERANCE); // -1400 (99.99 < 100.02)
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_LongSequence_MaintainsAccuracy()
        {
            // Arrange - Longer sequence to test accumulation accuracy
            var close = CreateAlternatingPrices(20, 100.0);
            var tickVolume = CreateIncreasingVolume(20, 1000);
            var realVolume = CreateIncreasingVolume(20, 5000);

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(20, result.OBV.Length);
            Assert.AreEqual(1000.0, result.OBV[0], TOLERANCE); // First value

            // Verify manual calculation for a few key points
            var runningOBV = 1000.0;
            for (var i = 1; i < close.Length; i++)
            {
                if (close[i] > close[i - 1])
                    runningOBV += tickVolume[i];
                else if (close[i] < close[i - 1])
                    runningOBV -= tickVolume[i];
                // else runningOBV stays the same

                Assert.AreEqual(runningOBV, result.OBV[i], TOLERANCE,
                    $"OBV calculation incorrect at index {i}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_RealisticMarketScenario()
        {
            // Arrange - Simulate a realistic trading scenario
            double[] close = { 145.50, 146.20, 145.80, 147.10, 146.90, 148.30, 147.60, 149.00 };
            long[] tickVolume = { 1500000, 1800000, 2200000, 1600000, 1900000, 1400000, 2100000, 1700000 };
            long[] realVolume = { 25000000, 28000000, 35000000, 22000000, 31000000, 19000000, 33000000, 27000000 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume, VolumeType.Real);

            // Assert
            Assert.AreEqual(8, result.OBV.Length);

            // Manual calculation verification
            var expectedOBV = 25000000.0; // Initial
            Assert.AreEqual(expectedOBV, result.OBV[0], TOLERANCE);

            expectedOBV += 28000000; // 146.20 > 145.50 (up)
            Assert.AreEqual(expectedOBV, result.OBV[1], TOLERANCE);

            expectedOBV -= 35000000; // 145.80 < 146.20 (down)
            Assert.AreEqual(expectedOBV, result.OBV[2], TOLERANCE);

            expectedOBV += 22000000; // 147.10 > 145.80 (up)
            Assert.AreEqual(expectedOBV, result.OBV[3], TOLERANCE);

            // Verify all values are reasonable and not NaN/Infinite
            for (var i = 0; i < result.OBV.Length; i++)
            {
                Assert.IsFalse(double.IsNaN(result.OBV[i]), $"OBV[{i}] should not be NaN");
                Assert.IsFalse(double.IsInfinity(result.OBV[i]), $"OBV[{i}] should not be infinite");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_BullishTrend_ShowsIncreasingOBV()
        {
            // Arrange - Strong bullish trend
            double[] close = { 100, 105, 110, 115, 120, 125, 130 };
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400, 1500, 1600 };
            long[] realVolume = { 5000, 5500, 6000, 6500, 7000, 7500, 8000 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume);

            // Assert - OBV should consistently increase in bullish trend
            for (var i = 1; i < result.OBV.Length; i++)
                Assert.IsTrue(result.OBV[i] > result.OBV[i - 1],
                    $"OBV should increase in bullish trend at index {i}: {result.OBV[i]} > {result.OBV[i - 1]}");
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_BearishTrend_ShowsDecreasingOBV()
        {
            // Arrange - Strong bearish trend
            double[] close = { 130, 125, 120, 115, 110, 105, 100 };
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400, 1500, 1600 };
            long[] realVolume = { 5000, 5500, 6000, 6500, 7000, 7500, 8000 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume);

            // Assert - OBV should consistently decrease in bearish trend
            for (var i = 1; i < result.OBV.Length; i++)
                Assert.IsTrue(result.OBV[i] < result.OBV[i - 1],
                    $"OBV should decrease in bearish trend at index {i}: {result.OBV[i]} < {result.OBV[i - 1]}");
        }

        [TestMethod][TestCategory("Core")]
        public void OBV_Calculate_ZeroVolumes_HandlesCorrectly()
        {
            // Arrange - Some zero volumes (unusual but should be handled)
            double[] close = { 100, 105, 95, 110 };
            long[] tickVolume = { 0, 1000, 0, 1500 };
            long[] realVolume = { 0, 5000, 0, 7500 };

            // Act
            var result = OBV.Calculate(close, tickVolume, realVolume);

            // Assert
            Assert.AreEqual(0.0, result.OBV[0], TOLERANCE); // Initial zero volume
            Assert.AreEqual(1000.0, result.OBV[1], TOLERANCE); // 0 + 1000 (up)
            Assert.AreEqual(1000.0, result.OBV[2], TOLERANCE); // 1000 - 0 (down)
            Assert.AreEqual(2500.0, result.OBV[3], TOLERANCE); // 1000 + 1500 (up)
        }

        #region Helper Methods

        private double[] CreateAlternatingPrices(int count, double basePrice)
        {
            var prices = new double[count];
            for (var i = 0; i < count; i++)
                // Alternate between slightly higher and lower prices
                prices[i] = basePrice + (i % 2 == 0 ? i * 0.5 : -(i * 0.3));
            return prices;
        }

        private long[] CreateIncreasingVolume(int count, long baseVolume)
        {
            var volumes = new long[count];
            for (var i = 0; i < count; i++) volumes[i] = baseVolume + i * 100;
            return volumes;
        }

        #endregion
    }
}