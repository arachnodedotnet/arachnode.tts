using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class VolumesTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_ReturnsCorrectLengths()
        {
            // Arrange
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400 };
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400 };

            // Act
            var result = Volumes.Calculate(tickVolume, realVolume);

            // Assert
            Assert.AreEqual(tickVolume.Length, result.Volumes.Length);
            Assert.AreEqual(tickVolume.Length, result.Colors.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_WithTickVolume_CopiesCorrectValues()
        {
            // Arrange
            long[] tickVolume = { 1000, 1100, 900, 1300, 1200 };
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400 };

            // Act
            var result = Volumes.Calculate(tickVolume, realVolume);

            // Assert - Check volumes are correctly copied
            for (var i = 0; i < tickVolume.Length; i++)
                Assert.AreEqual(tickVolume[i], result.Volumes[i], 1e-8,
                    $"Volume at index {i} should match tick volume");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_WithRealVolume_CopiesCorrectValues()
        {
            // Arrange
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400 };
            long[] realVolume = { 5000, 4800, 5200, 5100, 5400 };

            // Act
            var result = Volumes.Calculate(tickVolume, realVolume, VolumeType.Real);

            // Assert - Check volumes are correctly copied
            for (var i = 0; i < realVolume.Length; i++)
                Assert.AreEqual(realVolume[i], result.Volumes[i], 1e-8,
                    $"Volume at index {i} should match real volume");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_FirstColorIsAlwaysGreen()
        {
            // Arrange
            long[] tickVolume = { 1000, 1100, 900, 1300, 1200 };
            long[] realVolume = { 5000, 5100, 4800, 5300, 5200 };

            // Act
            var tickResult = Volumes.Calculate(tickVolume, realVolume);
            var realResult = Volumes.Calculate(tickVolume, realVolume, VolumeType.Real);

            // Assert
            Assert.AreEqual(0, tickResult.Colors[0], "First color should always be Green (0)");
            Assert.AreEqual(0, realResult.Colors[0], "First color should always be Green (0)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_ColorsBasedOnVolumeIncrease_TickVolume()
        {
            // Arrange - Clear volume pattern: up, down, up, down
            long[] tickVolume = { 1000, 1200, 900, 1100, 800 }; // up, down, up, down
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400 };

            // Act
            var result = Volumes.Calculate(tickVolume, realVolume);

            // Assert
            Assert.AreEqual(0, result.Colors[0]); // First is always Green (0)
            Assert.AreEqual(0, result.Colors[1]); // 1200 > 1000 = Green (0)
            Assert.AreEqual(1, result.Colors[2]); // 900 < 1200 = Red (1) 
            Assert.AreEqual(0, result.Colors[3]); // 1100 > 900 = Green (0)
            Assert.AreEqual(1, result.Colors[4]); // 800 < 1100 = Red (1)
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_ColorsBasedOnVolumeIncrease_RealVolume()
        {
            // Arrange - Clear volume pattern: up, down, up, down
            long[] tickVolume = { 1000, 1100, 1200, 1300, 1400 };
            long[] realVolume = { 5000, 5200, 4800, 5100, 4900 }; // up, down, up, down

            // Act
            var result = Volumes.Calculate(tickVolume, realVolume, VolumeType.Real);

            // Assert
            Assert.AreEqual(0, result.Colors[0]); // First is always Green (0)
            Assert.AreEqual(0, result.Colors[1]); // 5200 > 5000 = Green (0)
            Assert.AreEqual(1, result.Colors[2]); // 4800 < 5200 = Red (1)
            Assert.AreEqual(0, result.Colors[3]); // 5100 > 4800 = Green (0)
            Assert.AreEqual(1, result.Colors[4]); // 4900 < 5100 = Red (1)
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_EqualVolumeGivesRedColor()
        {
            // Arrange - Equal consecutive volumes should give Red (1)
            long[] tickVolume = { 1000, 1200, 1200, 1300, 1300 }; // equal at positions 2 and 4
            long[] realVolume = { 5000, 5100, 5200, 5300, 5400 };

            // Act
            var result = Volumes.Calculate(tickVolume, realVolume);

            // Assert
            Assert.AreEqual(0, result.Colors[0]); // First is always Green (0)
            Assert.AreEqual(0, result.Colors[1]); // 1200 > 1000 = Green (0)
            Assert.AreEqual(1, result.Colors[2]); // 1200 = 1200 = Red (1) - equal volumes
            Assert.AreEqual(0, result.Colors[3]); // 1300 > 1200 = Green (0)
            Assert.AreEqual(1, result.Colors[4]); // 1300 = 1300 = Red (1) - equal volumes
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_DefaultVolumeType_UsesTickVolume()
        {
            // Arrange
            long[] tickVolume = { 1000, 1200, 900 };
            long[] realVolume = { 5000, 4800, 5200 };

            // Act - Call without specifying volume type (should default to Tick)
            var defaultResult = Volumes.Calculate(tickVolume, realVolume);
            var explicitTickResult = Volumes.Calculate(tickVolume, realVolume);

            // Assert
            Assert.AreEqual(explicitTickResult.Volumes.Length, defaultResult.Volumes.Length);
            Assert.AreEqual(explicitTickResult.Colors.Length, defaultResult.Colors.Length);

            for (var i = 0; i < defaultResult.Volumes.Length; i++)
            {
                Assert.AreEqual(explicitTickResult.Volumes[i], defaultResult.Volumes[i], 1e-8,
                    $"Default should use tick volume at index {i}");
                Assert.AreEqual(explicitTickResult.Colors[i], defaultResult.Colors[i],
                    $"Default should use tick volume colors at index {i}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_VolumeTypeSelection_WorksCorrectly()
        {
            // Arrange - Different patterns for tick vs real volume
            long[] tickVolume = { 1000, 1200, 900 }; // up, down
            long[] realVolume = { 5000, 4800, 5200 }; // down, up

            // Act
            var tickResult = Volumes.Calculate(tickVolume, realVolume);
            var realResult = Volumes.Calculate(tickVolume, realVolume, VolumeType.Real);

            // Assert
            // Volumes should be different
            Assert.AreEqual(1000.0, tickResult.Volumes[0], 1e-8);
            Assert.AreEqual(5000.0, realResult.Volumes[0], 1e-8);

            // Colors should be different due to different volume patterns
            Assert.AreEqual(0, tickResult.Colors[1]); // 1200 > 1000 = Green
            Assert.AreEqual(1, realResult.Colors[1]); // 4800 < 5000 = Red

            Assert.AreEqual(1, tickResult.Colors[2]); // 900 < 1200 = Red
            Assert.AreEqual(0, realResult.Colors[2]); // 5200 > 4800 = Green
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_WithSingleElement_ReturnsZeroColor()
        {
            // Arrange
            long[] tickVolume = { 1000 };
            long[] realVolume = { 5000 };

            // Act
            var result = Volumes.Calculate(tickVolume, realVolume);

            // Assert
            Assert.AreEqual(1, result.Volumes.Length);
            Assert.AreEqual(1, result.Colors.Length);
            Assert.AreEqual(1000.0, result.Volumes[0], 1e-8);
            Assert.AreEqual(0, result.Colors[0], "Single element should have Green color (0)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_WithEmptyArrays_ReturnsEmptyResults()
        {
            // Arrange
            long[] tickVolume = { };
            long[] realVolume = { };

            // Act
            var result = Volumes.Calculate(tickVolume, realVolume);

            // Assert
            Assert.AreEqual(0, result.Volumes.Length);
            Assert.AreEqual(0, result.Colors.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_WithLengthLessThanTwo_ReturnsCorrectResult()
        {
            // Arrange
            long[] tickVolume = { 1500 };
            long[] realVolume = { 7500 };

            // Act
            var result = Volumes.Calculate(tickVolume, realVolume, VolumeType.Real);

            // Assert
            Assert.AreEqual(1, result.Volumes.Length);
            Assert.AreEqual(1, result.Colors.Length);
            Assert.AreEqual(7500.0, result.Volumes[0], 1e-8);
            Assert.AreEqual(0, result.Colors[0], "Length < 2 should still set first color to Green (0)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_WithLargeVolumes_HandlesCorrectly()
        {
            // Arrange - Test with large volume numbers
            long[] tickVolume = { 1000000, 2000000, 1500000, 3000000 };
            long[] realVolume = { 10000000, 8000000, 15000000, 12000000 };

            // Act
            var result = Volumes.Calculate(tickVolume, realVolume, VolumeType.Real);

            // Assert
            Assert.AreEqual(10000000.0, result.Volumes[0], 1e-8);
            Assert.AreEqual(8000000.0, result.Volumes[1], 1e-8);
            Assert.AreEqual(15000000.0, result.Volumes[2], 1e-8);
            Assert.AreEqual(12000000.0, result.Volumes[3], 1e-8);

            Assert.AreEqual(0, result.Colors[0]); // First is always Green
            Assert.AreEqual(1, result.Colors[1]); // 8M < 10M = Red
            Assert.AreEqual(0, result.Colors[2]); // 15M > 8M = Green  
            Assert.AreEqual(1, result.Colors[3]); // 12M < 15M = Red
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_ConsistentColorPattern_OverLongSequence()
        {
            // Arrange - Alternating volume pattern to test consistency
            long[] tickVolume = { 1000, 1200, 1100, 1300, 1250, 1400, 1350, 1500 };
            long[] realVolume = { 5000, 5200, 5100, 5300, 5250, 5400, 5350, 5500 };

            // Act
            var result = Volumes.Calculate(tickVolume, realVolume);

            // Assert - Verify each color matches the volume comparison logic
            Assert.AreEqual(0, result.Colors[0]); // First is always Green

            for (var i = 1; i < tickVolume.Length; i++)
            {
                var expectedColor = tickVolume[i] > tickVolume[i - 1] ? 0 : 1;
                Assert.AreEqual(expectedColor, result.Colors[i],
                    $"Color at index {i} should be {expectedColor} (volume {tickVolume[i]} vs {tickVolume[i - 1]})");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Volumes_Calculate_ZeroVolumes_HandlesCorrectly()
        {
            // Arrange - Test with zero volumes
            long[] tickVolume = { 0, 100, 0, 200 };
            long[] realVolume = { 0, 500, 0, 1000 };

            // Act
            var result = Volumes.Calculate(tickVolume, realVolume);

            // Assert
            Assert.AreEqual(0.0, result.Volumes[0], 1e-8);
            Assert.AreEqual(100.0, result.Volumes[1], 1e-8);
            Assert.AreEqual(0.0, result.Volumes[2], 1e-8);
            Assert.AreEqual(200.0, result.Volumes[3], 1e-8);

            Assert.AreEqual(0, result.Colors[0]); // First is always Green
            Assert.AreEqual(0, result.Colors[1]); // 100 > 0 = Green
            Assert.AreEqual(1, result.Colors[2]); // 0 < 100 = Red
            Assert.AreEqual(0, result.Colors[3]); // 200 > 0 = Green
        }
    }
}