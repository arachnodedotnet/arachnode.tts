using System;
using System.Diagnostics;
using System.Linq;
using Trade.Prices2;
using Trade.Utils;

namespace Trade.Tests
{
    /// <summary>
    /// Manual test runner to verify PriceRange optimization performance
    /// </summary>
    public class ManualPerformanceTest
    {
        public static void RunPerformanceComparison()
        {
            Console.WriteLine("=== PRICERANGE PERFORMANCE VERIFICATION ===");
            
            // Create test data
            var testData = CreateTestPriceRecords(200);
            
            // Warm up
            Console.WriteLine("Warming up both methods...");
            GeneticIndividual.AnalyzeIndicatorRanges(testData.Take(10).ToArray());
            GeneticIndividual.AnalyzeIndicatorRangesSlow(testData.Take(10).ToArray());
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            // Test optimized method
            Console.WriteLine("Testing OPTIMIZED method...");
            var memBefore1 = GC.GetTotalMemory(true);
            var gen0Before1 = GC.CollectionCount(0);
            var sw1 = Stopwatch.StartNew();
            
            GeneticIndividual.AnalyzeIndicatorRanges(testData);
            
            sw1.Stop();
            var gen0After1 = GC.CollectionCount(0);
            GC.Collect();
            var memAfter1 = GC.GetTotalMemory(true);
            var memUsed1 = memAfter1 - memBefore1;
            var gc1 = gen0After1 - gen0Before1;
            
            // Test legacy method
            Console.WriteLine("Testing LEGACY method...");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var memBefore2 = GC.GetTotalMemory(true);
            var gen0Before2 = GC.CollectionCount(0);
            var sw2 = Stopwatch.StartNew();
            
            GeneticIndividual.AnalyzeIndicatorRangesSlow(testData);
            
            sw2.Stop();
            var gen0After2 = GC.CollectionCount(0);
            GC.Collect();
            var memAfter2 = GC.GetTotalMemory(true);
            var memUsed2 = memAfter2 - memBefore2;
            var gc2 = gen0After2 - gen0Before2;
            
            // Calculate improvements
            var speedImprovement = (double)sw2.ElapsedMilliseconds / Math.Max(sw1.ElapsedMilliseconds, 1);
            var memoryImprovement = (double)memUsed2 / Math.Max(memUsed1, 1);
            
            Console.WriteLine("\n=== RESULTS ===");
            Console.WriteLine($"Dataset Size: {testData.Length} records");
            Console.WriteLine($"Optimized: {sw1.ElapsedMilliseconds:N0}ms, {memUsed1 / 1024.0:N1}KB, {gc1} GCs");
            Console.WriteLine($"Legacy   : {sw2.ElapsedMilliseconds:N0}ms, {memUsed2 / 1024.0:N1}KB, {gc2} GCs");
            Console.WriteLine($"Speed Improvement: {speedImprovement:F1}x faster");
            Console.WriteLine($"Memory Improvement: {memoryImprovement:F1}x less memory");
            Console.WriteLine($"GC Improvement: {gc2 - gc1} fewer collections");
            
            // Test PriceRange basic functionality
            Console.WriteLine("\n=== PRICERANGE FUNCTIONALITY TEST ===");
            var testArray = new double[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0 };
            var range = new PriceRange(testArray, 2, 5); // [3.0, 4.0, 5.0, 6.0, 7.0]
            
            Console.WriteLine($"Source Array: [{string.Join(", ", testArray)}]");
            Console.WriteLine($"PriceRange(2, 5): Length={range.Length}, First={range.First}, Last={range.Last}");
            Console.WriteLine($"Sum: {range.Sum():F1}, Average: {range.Average():F1}");
            
            var values = range.ToList();
            Console.WriteLine($"Values: [{string.Join(", ", values)}]");
            
            if (Math.Abs(range.Sum() - 25.0) < 1e-10 && range.Length == 5)
            {
                Console.WriteLine("? PriceRange functionality verified!");
            }
            else
            {
                Console.WriteLine("? PriceRange functionality failed!");
            }
            
            Console.WriteLine("\n=== PERFORMANCE ANALYSIS ===");
            if (speedImprovement > 1.0)
            {
                Console.WriteLine($"? Speed optimization successful: {speedImprovement:F1}x improvement");
            }
            else
            {
                Console.WriteLine($"??  Speed optimization minimal: {speedImprovement:F1}x improvement");
            }
            
            if (memoryImprovement > 1.0)
            {
                Console.WriteLine($"? Memory optimization successful: {memoryImprovement:F1}x less memory");
            }
            else
            {
                Console.WriteLine($"??  Memory optimization minimal: {memoryImprovement:F1}x less memory");
            }
            
            Console.WriteLine("==========================================");
        }
        
        private static PriceRecord[] CreateTestPriceRecords(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var rng = new Random(42); // Fixed seed for reproducible tests
            
            for (var i = 0; i < count; i++)
            {
                var trend = i * 0.02;
                var cycle = Math.Sin(i * 0.05) * 3;
                var noise = (rng.NextDouble() - 0.5) * 1;
                var price = 100.0 + trend + cycle + noise;
                
                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price * 0.999,
                    price * 1.003,
                    price * 0.997,
                    price,
                    volume: 1000 + rng.Next(3000),
                    wap: price,
                    count: 100 + rng.Next(200)
                );
            }
            
            return records;
        }
    }
}