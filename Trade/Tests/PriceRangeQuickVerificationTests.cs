using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Utils;

namespace Trade.Tests
{
    [TestClass]
    public class PriceRangeQuickVerificationTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void QuickTest_PriceRange_BasicFunctionality()
        {
            // Simple test to verify PriceRange works
            var array = new double[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
            var range = new PriceRange(array, 1, 3); // [2.0, 3.0, 4.0]
            
            Assert.AreEqual(3, range.Length);
            Assert.AreEqual(2.0, range[0], 1e-10);
            Assert.AreEqual(9.0, range.Sum(), 1e-10); // 2+3+4=9
            
            Console.WriteLine("? PriceRange basic functionality verified!");
            Console.WriteLine($"   Length: {range.Length}");
            Console.WriteLine($"   First: {range[0]}");
            Console.WriteLine($"   Sum: {range.Sum()}");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void QuickTest_Performance_ArrayCopyVsPriceRange()
        {
            const int size = 1000;
            const int iterations = 10;
            
            // Create test data
            var testData = new double[size];
            for (int i = 0; i < size; i++)
                testData[i] = i * 0.1;
                
            // Test array copying approach
            var watch1 = System.Diagnostics.Stopwatch.StartNew();
            for (int iter = 0; iter < iterations; iter++)
            {
                for (int i = 0; i < size; i++)
                {
                    // Simulate array copying (O(n²) approach)
                    var copy = new double[i + 1];
                    for (int j = 0; j <= i; j++)
                        copy[j] = testData[j];
                    
                    // Simulate calculation
                    var sum = 0.0;
                    for (int j = 0; j < copy.Length; j++)
                        sum += copy[j];
                }
            }
            watch1.Stop();
            
            // Test PriceRange approach
            var watch2 = System.Diagnostics.Stopwatch.StartNew();
            for (int iter = 0; iter < iterations; iter++)
            {
                for (int i = 0; i < size; i++)
                {
                    // Use PriceRange (O(1) approach)
                    var range = new PriceRange(testData, 0, i + 1);
                    
                    // Simulate calculation
                    var sum = range.Sum();
                }
            }
            watch2.Stop();
            
            var improvement = (double)watch1.ElapsedMilliseconds / Math.Max(watch2.ElapsedMilliseconds, 1);
            
            Console.WriteLine($"\n=== QUICK PERFORMANCE COMPARISON ===");
            Console.WriteLine($"Array Copying: {watch1.ElapsedMilliseconds}ms");
            Console.WriteLine($"PriceRange   : {watch2.ElapsedMilliseconds}ms");
            Console.WriteLine($"Improvement  : {improvement:F1}x faster");
            
            Assert.IsTrue(improvement > 1.0, $"PriceRange should be faster: {improvement:F1}x");
        }
    }
}