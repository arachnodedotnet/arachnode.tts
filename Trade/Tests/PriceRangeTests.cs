using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Utils;

namespace Trade.Tests
{
    /// <summary>
    /// Comprehensive unit tests for the PriceRange struct
    /// Ensures correctness of zero-copy range operations
    /// </summary>
    [TestClass]
    public class PriceRangeTests
    {
        private const double TOLERANCE = 1e-10;

        [TestMethod]
        [TestCategory("Core")]
        public void Constructor_ValidInputs_CreatesCorrectRange()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
            
            // Act
            var fullRange = new PriceRange(sourceArray);
            var subRange = new PriceRange(sourceArray, 1, 3);
            
            // Assert
            Assert.AreEqual(5, fullRange.Length);
            Assert.AreEqual(3, subRange.Length);
            Assert.AreEqual(2.0, subRange[0], TOLERANCE);
            Assert.AreEqual(4.0, subRange[2], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Constructor_BoundaryConditions_HandlesSafely()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0 };
            
            // Act & Assert - Out of bounds start
            var range1 = new PriceRange(sourceArray, 10, 5);
            Assert.AreEqual(0, range1.Length);
            
            // Negative start
            var range2 = new PriceRange(sourceArray, -1, 5);
            Assert.AreEqual(3, range2.Length);
            Assert.AreEqual(1.0, range2[0], TOLERANCE);
            
            // Length exceeds array bounds
            var range3 = new PriceRange(sourceArray, 1, 10);
            Assert.AreEqual(2, range3.Length);
            Assert.AreEqual(2.0, range3[0], TOLERANCE);
            
            // Zero length
            var range4 = new PriceRange(sourceArray, 1, 0);
            Assert.AreEqual(0, range4.Length);
            Assert.IsTrue(range4.IsEmpty);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullArray_ThrowsException()
        {
            // Act
            var range = new PriceRange(null, 0, 1);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Indexer_ValidIndices_ReturnsCorrectValues()
        {
            // Arrange
            var sourceArray = new double[] { 10.0, 20.0, 30.0, 40.0, 50.0 };
            var range = new PriceRange(sourceArray, 1, 3); // [20.0, 30.0, 40.0]
            
            // Act & Assert
            Assert.AreEqual(20.0, range[0], TOLERANCE);
            Assert.AreEqual(30.0, range[1], TOLERANCE);
            Assert.AreEqual(40.0, range[2], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void Indexer_InvalidIndex_ThrowsException()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0 };
            var range = new PriceRange(sourceArray, 0, 2);
            
            // Act
            var value = range[5]; // Should throw
        }

        [TestMethod]
        [TestCategory("Core")]
        public void FirstAndLast_ValidRange_ReturnsCorrectValues()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
            var range = new PriceRange(sourceArray, 1, 3); // [2.0, 3.0, 4.0]
            
            // Act & Assert
            Assert.AreEqual(2.0, range.First, TOLERANCE);
            Assert.AreEqual(4.0, range.Last, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(InvalidOperationException))]
        public void First_EmptyRange_ThrowsException()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0 };
            var range = new PriceRange(sourceArray, 1, 0);
            
            // Act
            var first = range.First; // Should throw
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Last_EmptyRange_ThrowsException()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0 };
            var range = new PriceRange(sourceArray, 1, 0);
            
            // Act
            var last = range.Last; // Should throw
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ImplicitConversion_Array_CreatesFullRange()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0, 4.0 };
            
            // Act
            PriceRange range = sourceArray; // Implicit conversion
            
            // Assert
            Assert.AreEqual(4, range.Length);
            Assert.AreEqual(1.0, range[0], TOLERANCE);
            Assert.AreEqual(4.0, range[3], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Enumeration_ForeachLoop_IteratesCorrectly()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
            var range = new PriceRange(sourceArray, 1, 3); // [2.0, 3.0, 4.0]
            
            // Act
            var sum = 0.0;
            var count = 0;
            foreach (var value in range)
            {
                sum += value;
                count++;
            }
            
            // Assert
            Assert.AreEqual(3, count);
            Assert.AreEqual(9.0, sum, TOLERANCE); // 2.0 + 3.0 + 4.0 = 9.0
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ToArray_CreatesCorrectCopy()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
            var range = new PriceRange(sourceArray, 1, 3); // [2.0, 3.0, 4.0]
            
            // Act
            var copy = range.ToArray();
            
            // Assert
            Assert.AreEqual(3, copy.Length);
            Assert.AreEqual(2.0, copy[0], TOLERANCE);
            Assert.AreEqual(3.0, copy[1], TOLERANCE);
            Assert.AreEqual(4.0, copy[2], TOLERANCE);
            
            // Verify it's a separate copy
            copy[0] = 99.0;
            Assert.AreEqual(2.0, range[0], TOLERANCE); // Original unchanged
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Skip_ValidCount_ReturnsCorrectRange()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
            var range = new PriceRange(sourceArray, 0, 5); // [1.0, 2.0, 3.0, 4.0, 5.0]
            
            // Act
            var skipped = range.Skip(2); // [3.0, 4.0, 5.0]
            
            // Assert
            Assert.AreEqual(3, skipped.Length);
            Assert.AreEqual(3.0, skipped[0], TOLERANCE);
            Assert.AreEqual(5.0, skipped[2], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Skip_BoundaryConditions_HandlesSafely()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0 };
            var range = new PriceRange(sourceArray);
            
            // Act & Assert
            // Skip zero
            var skip0 = range.Skip(0);
            Assert.AreEqual(3, skip0.Length);
            
            // Skip negative
            var skipNeg = range.Skip(-1);
            Assert.AreEqual(3, skipNeg.Length);
            
            // Skip all
            var skipAll = range.Skip(3);
            Assert.AreEqual(0, skipAll.Length);
            Assert.IsTrue(skipAll.IsEmpty);
            
            // Skip more than available
            var skipMore = range.Skip(10);
            Assert.AreEqual(0, skipMore.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Take_ValidCount_ReturnsCorrectRange()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
            var range = new PriceRange(sourceArray);
            
            // Act
            var taken = range.Take(3); // [1.0, 2.0, 3.0]
            
            // Assert
            Assert.AreEqual(3, taken.Length);
            Assert.AreEqual(1.0, taken[0], TOLERANCE);
            Assert.AreEqual(3.0, taken[2], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Take_BoundaryConditions_HandlesSafely()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0 };
            var range = new PriceRange(sourceArray);
            
            // Act & Assert
            // Take zero
            var take0 = range.Take(0);
            Assert.AreEqual(0, take0.Length);
            Assert.IsTrue(take0.IsEmpty);
            
            // Take negative
            var takeNeg = range.Take(-1);
            Assert.AreEqual(0, takeNeg.Length);
            
            // Take all
            var takeAll = range.Take(3);
            Assert.AreEqual(3, takeAll.Length);
            
            // Take more than available
            var takeMore = range.Take(10);
            Assert.AreEqual(3, takeMore.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Slice_ValidParameters_ReturnsCorrectRange()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
            var range = new PriceRange(sourceArray);
            
            // Act
            var slice = range.Slice(1, 3); // [2.0, 3.0, 4.0]
            
            // Assert
            Assert.AreEqual(3, slice.Length);
            Assert.AreEqual(2.0, slice[0], TOLERANCE);
            Assert.AreEqual(4.0, slice[2], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Sum_ValidRange_ReturnsCorrectSum()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0, 4.0 };
            var range = new PriceRange(sourceArray);
            
            // Act
            var sum = range.Sum();
            
            // Assert
            Assert.AreEqual(10.0, sum, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Sum_EmptyRange_ReturnsZero()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0 };
            var range = new PriceRange(sourceArray, 1, 0);
            
            // Act
            var sum = range.Sum();
            
            // Assert
            Assert.AreEqual(0.0, sum, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Average_ValidRange_ReturnsCorrectAverage()
        {
            // Arrange
            var sourceArray = new double[] { 2.0, 4.0, 6.0, 8.0 };
            var range = new PriceRange(sourceArray);
            
            // Act
            var average = range.Average();
            
            // Assert
            Assert.AreEqual(5.0, average, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Average_EmptyRange_ThrowsException()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0 };
            var range = new PriceRange(sourceArray, 1, 0);
            
            // Act
            var average = range.Average(); // Should throw
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Min_ValidRange_ReturnsCorrectMinimum()
        {
            // Arrange
            var sourceArray = new double[] { 3.0, 1.0, 4.0, 2.0 };
            var range = new PriceRange(sourceArray);
            
            // Act
            var min = range.Min();
            
            // Assert
            Assert.AreEqual(1.0, min, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Max_ValidRange_ReturnsCorrectMaximum()
        {
            // Arrange
            var sourceArray = new double[] { 3.0, 1.0, 4.0, 2.0 };
            var range = new PriceRange(sourceArray);
            
            // Act
            var max = range.Max();
            
            // Assert
            Assert.AreEqual(4.0, max, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Min_EmptyRange_ThrowsException()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0 };
            var range = new PriceRange(sourceArray, 1, 0);
            
            // Act
            var min = range.Min(); // Should throw
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Max_EmptyRange_ThrowsException()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0 };
            var range = new PriceRange(sourceArray, 1, 0);
            
            // Act
            var max = range.Max(); // Should throw
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Contains_ExistingValue_ReturnsTrue()
        {
            // Arrange
            var sourceArray = new double[] { 1.5, 2.7, 3.9, 4.1 };
            var range = new PriceRange(sourceArray);
            
            // Act & Assert
            Assert.IsTrue(range.Contains(2.7));
            Assert.IsTrue(range.Contains(4.1));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Contains_NonExistingValue_ReturnsFalse()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0 };
            var range = new PriceRange(sourceArray);
            
            // Act & Assert
            Assert.IsFalse(range.Contains(4.0));
            Assert.IsFalse(range.Contains(0.5));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Contains_EmptyRange_ReturnsFalse()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0 };
            var range = new PriceRange(sourceArray, 1, 0);
            
            // Act & Assert
            Assert.IsFalse(range.Contains(1.0));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ToString_VariousRanges_ReturnsAppropriateString()
        {
            // Arrange
            var sourceArray = new double[] { 1.5, 2.7, 3.9, 4.1, 5.3 };
            
            // Act & Assert
            var emptyRange = new PriceRange(sourceArray, 1, 0);
            Assert.IsTrue(emptyRange.ToString().Contains("Empty"));
            
            var singleRange = new PriceRange(sourceArray, 1, 1);
            Assert.IsTrue(singleRange.ToString().Contains("2.70"));
            
            var smallRange = new PriceRange(sourceArray, 1, 2);
            Assert.IsTrue(smallRange.ToString().Contains("2.70") && smallRange.ToString().Contains("3.90"));
            
            var largeRange = new PriceRange(sourceArray);
            Assert.IsTrue(largeRange.ToString().Contains("1.50") && largeRange.ToString().Contains("5.30") && largeRange.ToString().Contains("5 elements"));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ChainedOperations_SkipTakeSlice_WorkCorrectly()
        {
            // Arrange
            var sourceArray = new double[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0 };
            var range = new PriceRange(sourceArray);
            
            // Act - Chain operations: Skip first 2, take 6, then slice middle 4
            var result = range.Skip(2).Take(6).Slice(1, 4); // Should be [4.0, 5.0, 6.0, 7.0]
            
            // Assert
            Assert.AreEqual(4, result.Length);
            Assert.AreEqual(4.0, result[0], TOLERANCE);
            Assert.AreEqual(5.0, result[1], TOLERANCE);
            Assert.AreEqual(6.0, result[2], TOLERANCE);
            Assert.AreEqual(7.0, result[3], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void LargeRange_Operations_PerformEfficiently()
        {
            // Arrange - Create large array
            const int size = 100000;
            var largeArray = new double[size];
            for (int i = 0; i < size; i++)
                largeArray[i] = i * 0.1;
            
            var range = new PriceRange(largeArray);
            
            // Act & Assert - Operations should complete quickly without copying
            var subRange = range.Skip(1000).Take(10000);
            Assert.AreEqual(10000, subRange.Length);
            Assert.AreEqual(100.0, subRange[0], TOLERANCE);
            
            var sum = subRange.Take(100).Sum();
            Assert.IsTrue(sum > 0); // Verify calculation works
            
            var avg = subRange.Take(100).Average();
            Assert.IsTrue(avg > 0); // Verify calculation works
        }
    }
}