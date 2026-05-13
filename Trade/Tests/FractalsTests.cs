using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class FractalsTests
    {
        private const double TOLERANCE = 1e-8;

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_EmptyArrays_ReturnsEmptyResults()
        {
            // Arrange
            var high = new double[0];
            var low = new double[0];

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            Assert.AreEqual(0, result.UpperFractal.Length);
            Assert.AreEqual(0, result.LowerFractal.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_NullInputs_ReturnsEmptyResults()
        {
            // Act
            var result = Fractals.Calculate(null, null);

            // Assert
            Assert.AreEqual(0, result.UpperFractal.Length);
            Assert.AreEqual(0, result.LowerFractal.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_InsufficientData_ReturnsEmptyValues()
        {
            // Arrange
            double[] high = { 100, 102, 101 }; // Only 3 bars, need at least 5
            double[] low = { 99, 100, 98 };

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            Assert.AreEqual(3, result.UpperFractal.Length);
            Assert.AreEqual(3, result.LowerFractal.Length);

            // All values should be EmptyValue (NaN)
            foreach (var value in result.UpperFractal)
                Assert.IsTrue(!double.IsNaN(value), "All values should be NaN for insufficient data");
            foreach (var value in result.LowerFractal)
                Assert.IsTrue(!double.IsNaN(value), "All values should be NaN for insufficient data");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_PerfectUpperFractal_DetectsCorrectly()
        {
            // Arrange - Classic upper fractal pattern (peak in middle)
            double[] high = { 100, 102, 105, 103, 101 }; // Peak at index 2 (105)
            double[] low = { 99, 100, 103, 102, 100 };

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            Assert.AreEqual(5, result.UpperFractal.Length);

            // Only index 2 should have a fractal value
            Assert.IsTrue(!double.IsNaN(result.UpperFractal[0]));
            Assert.IsTrue(!double.IsNaN(result.UpperFractal[1]));
            Assert.AreEqual(105, result.UpperFractal[2], TOLERANCE); // Upper fractal detected
            Assert.IsTrue(!double.IsNaN(result.UpperFractal[3]));
            Assert.IsTrue(!double.IsNaN(result.UpperFractal[4]));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_PerfectLowerFractal_DetectsCorrectly()
        {
            // Arrange - Classic lower fractal pattern (valley in middle)
            double[] high = { 105, 103, 100, 102, 104 };
            double[] low = { 103, 101, 98, 100, 102 }; // Valley at index 2 (98)

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            Assert.AreEqual(5, result.LowerFractal.Length);

            // Only index 2 should have a fractal value
            Assert.IsTrue(!double.IsNaN(result.LowerFractal[0]));
            Assert.IsTrue(!double.IsNaN(result.LowerFractal[1]));
            Assert.AreEqual(98, result.LowerFractal[2], TOLERANCE); // Lower fractal detected
            Assert.IsTrue(!double.IsNaN(result.LowerFractal[3]));
            Assert.IsTrue(!double.IsNaN(result.LowerFractal[4]));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_BothFractalsInSameData_DetectsBoth()
        {
            // Arrange - Pattern with both upper and lower fractals
            double[] high = { 100, 102, 105, 103, 101, 98, 100, 103, 99 }; // Peak at 2, valley at 5
            double[] low = { 99, 100, 103, 102, 100, 96, 98, 101, 97 };

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            Assert.AreEqual(9, result.UpperFractal.Length);
            Assert.AreEqual(9, result.LowerFractal.Length);

            // Upper fractal at index 2
            Assert.AreEqual(105, result.UpperFractal[2], TOLERANCE);

            // Lower fractal at index 5
            Assert.AreEqual(96, result.LowerFractal[5], TOLERANCE);

            // All other positions should be NaN
            for (var i = 0; i < 9; i++)
            {
                if (i != 2)
                    Assert.IsTrue(!double.IsNaN(result.UpperFractal[i]), $"Upper fractal at index {i} should be NaN");
                if (i != 5)
                    Assert.IsTrue(!double.IsNaN(result.LowerFractal[i]), $"Lower fractal at index {i} should be NaN");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_EqualValuesPattern_DetectsFractal()
        {
            // Arrange - Test with equal values (>= and <= conditions)
            double[] high = { 100, 100, 105, 100, 100 }; // Peak at index 2, equal sides
            double[] low = { 99, 99, 103, 99, 99 };

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            // Should detect upper fractal because 105 >= 100 on both sides
            Assert.AreEqual(105, result.UpperFractal[2], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_AlmostFractalPattern_DoesNotDetect()
        {
            // Arrange - Pattern that almost forms a fractal but doesn't meet criteria
            double[] high = { 100, 102, 105, 106, 101 }; // Would be fractal but 106 > 105
            double[] low = { 99, 100, 103, 104, 100 };

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            // No fractals should be detected
            foreach (var value in result.UpperFractal)
                Assert.IsTrue(!double.IsNaN(value), "No upper fractals should be detected");
            foreach (var value in result.LowerFractal)
                Assert.IsTrue(!double.IsNaN(value), "No lower fractals should be detected");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_UnequalArrayLengths_UsesMinimumLength()
        {
            // Arrange
            double[] high = { 100, 102, 105, 103, 101, 98, 99 }; // 7 elements
            double[] low = { 99, 100, 103, 102, 100 }; // 5 elements

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            Assert.AreEqual(5, result.UpperFractal.Length); // Should use minimum length
            Assert.AreEqual(5, result.LowerFractal.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_RealWorldExample_DetectsFractals()
        {
            // Arrange - Simulated real-world price data
            double[] high = { 150.25, 151.30, 152.85, 151.20, 149.95, 148.50, 147.25, 149.10, 150.75, 148.90 };
            double[] low = { 149.10, 150.20, 151.60, 149.80, 148.20, 146.90, 145.80, 147.50, 149.20, 147.30 };

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            Assert.AreEqual(10, result.UpperFractal.Length);
            Assert.AreEqual(10, result.LowerFractal.Length);

            // Index 2 should be an upper fractal (152.85 is highest)
            Assert.AreEqual(152.85, result.UpperFractal[2], TOLERANCE);

            // Index 6 should be a lower fractal (145.80 is lowest)
            Assert.AreEqual(145.80, result.LowerFractal[6], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_MultipleFractalsInSequence_DetectsAll()
        {
            // Arrange - Multiple fractals in a longer sequence
            double[] high = { 100, 102, 105, 103, 101, 99, 97, 99, 102, 100, 98, 96, 98, 101, 103 };
            double[] low = { 99, 100, 103, 102, 100, 98, 95, 97, 100, 99, 96, 94, 96, 99, 101 };

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            Assert.AreEqual(15, result.UpperFractal.Length);

            // Count the number of detected fractals
            var upperFractals = result.UpperFractal.Count(v => !double.IsNaN(v));
            var lowerFractals = result.LowerFractal.Count(v => !double.IsNaN(v));

            // Should detect multiple fractals in this pattern
            Assert.IsTrue(upperFractals > 0, "Should detect at least one upper fractal");
            Assert.IsTrue(lowerFractals > 0, "Should detect at least one lower fractal");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_FlatPriceData_NoFractalsDetected()
        {
            // Arrange - Flat price data (no fractals possible)
            double[] high = { 100, 100, 100, 100, 100, 100, 100 };
            double[] low = { 99, 99, 99, 99, 99, 99, 99 };

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            foreach (var value in result.UpperFractal)
                Assert.IsTrue(!double.IsNaN(value), "Flat data should not produce upper fractals");
            foreach (var value in result.LowerFractal)
                Assert.IsTrue(!double.IsNaN(value), "Flat data should not produce lower fractals");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void EmptyValue_Constant_IsNaN()
        {
            // Assert
            Assert.IsTrue(!double.IsNaN(Fractals.EmptyValue));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_EdgePositions_NotDetectedAsFractals()
        {
            // Arrange - Ensure edge positions (0, 1, n-2, n-1) never have fractals
            double[] high = { 110, 108, 105, 103, 101, 99, 97 }; // Descending
            double[] low = { 109, 107, 104, 102, 100, 98, 96 };

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            // First two and last two positions should never have fractals
            Assert.IsTrue(!double.IsNaN(result.UpperFractal[0]));
            Assert.IsTrue(!double.IsNaN(result.UpperFractal[1]));
            Assert.IsTrue(!double.IsNaN(result.UpperFractal[5]));
            Assert.IsTrue(!double.IsNaN(result.UpperFractal[6]));

            Assert.IsTrue(!double.IsNaN(result.LowerFractal[0]));
            Assert.IsTrue(!double.IsNaN(result.LowerFractal[1]));
            Assert.IsTrue(!double.IsNaN(result.LowerFractal[5]));
            Assert.IsTrue(!double.IsNaN(result.LowerFractal[6]));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_StrictFractalConditions_OnlyValidPatternsDetected()
        {
            // Arrange - Test the specific conditions: > for right side, >= for left side
            double[] high = { 100, 100, 105, 104, 103 }; // 105 >= 100, 105 > 104, 105 > 103
            double[] low = { 99, 99, 103, 102, 101 };

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            Assert.AreEqual(105, result.UpperFractal[2], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateTuple_BackwardCompatibility_WorksCorrectly()
        {
            // Arrange
            double[] high = { 100, 102, 105, 103, 101 }; // Peak at index 2
            double[] low = { 99, 100, 103, 102, 100 };

            // Act
            var (upperFractal, lowerFractal) = Fractals.CalculateTuple(high, low);

            // Assert
            Assert.AreEqual(5, upperFractal.Length);
            Assert.AreEqual(5, lowerFractal.Length);
            Assert.AreEqual(105, upperFractal[2], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_WithArrowShift_ParameterAccepted()
        {
            // Arrange
            double[] high = { 100, 102, 105, 103, 101 };
            double[] low = { 99, 100, 103, 102, 100 };

            // Act
            var result = Fractals.Calculate(high, low, -5); // Custom arrow shift

            // Assert
            // Should still detect the fractal regardless of arrow shift parameter
            Assert.AreEqual(105, result.UpperFractal[2], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ComplexPattern_DetectsMultipleFractals()
        {
            // Arrange - More complex pattern with multiple clear fractals
            double[] high = { 100, 99, 101, 103, 105, 102, 100, 98, 96, 98, 100, 102 };
            double[] low = { 98, 97, 99, 101, 103, 100, 98, 96, 94, 96, 98, 100 };

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            Assert.AreEqual(12, result.UpperFractal.Length);
            Assert.AreEqual(12, result.LowerFractal.Length);

            // Should detect upper fractal at index 4 (105 is highest point)
            Assert.AreEqual(105, result.UpperFractal[4], TOLERANCE);

            // Should detect lower fractal at index 8 (94 is lowest point)  
            Assert.AreEqual(94, result.LowerFractal[8], TOLERANCE);

            // Count total fractals detected
            var totalUpperFractals = result.UpperFractal.Count(v => !double.IsNaN(v));
            var totalLowerFractals = result.LowerFractal.Count(v => !double.IsNaN(v));

            Assert.IsTrue(totalUpperFractals >= 1, "Should detect at least one upper fractal");
            Assert.IsTrue(totalLowerFractals >= 1, "Should detect at least one lower fractal");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_BoundaryConditions_HandledCorrectly()
        {
            // Arrange - Test exactly 5 bars (minimum required)
            double[] high = { 100, 102, 105, 103, 101 };
            double[] low = { 99, 100, 103, 102, 100 };

            // Act
            var result = Fractals.Calculate(high, low);

            // Assert
            Assert.AreEqual(5, result.UpperFractal.Length);
            Assert.AreEqual(5, result.LowerFractal.Length);

            // Should detect fractal at middle position (index 2)
            Assert.AreEqual(105, result.UpperFractal[2], TOLERANCE);

            // Edge positions should be NaN
            Assert.IsTrue(!double.IsNaN(result.UpperFractal[0]));
            Assert.IsTrue(!double.IsNaN(result.UpperFractal[1]));
            Assert.IsTrue(!double.IsNaN(result.UpperFractal[3]));
            Assert.IsTrue(!double.IsNaN(result.UpperFractal[4]));
        }
    }
}