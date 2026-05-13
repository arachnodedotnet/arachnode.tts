//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using Microsoft.VisualStudio.TestTools.UnitTesting;

//namespace Trade
//{
//    [TestClass]
//    public class ParallelWindowOptimizerTests
//    {
//        private static readonly Random _random = new Random(42); // Fixed seed for reproducibility

//        [TestMethod]
//        public void TestParallelVsSequentialPerformance()
//        {
//            // Create test price data
//            var baseDate = new DateTime(2023, 1, 1);
//            var priceRecords = new List<PriceRecord>();

//            // Generate 2 years of daily data for testing
//            for (int i = 0; i < 500; i++)
//            {
//                var date = baseDate.AddDays(i);
//                var price = 100.0 + Math.Sin(i * 0.01) * 10;
//                var record = new PriceRecord(date, price, price + 1, price - 1, price + 0.5, 1000, price, 100, true);
//                priceRecords.Add(record);
//            }

//            var priceArray = priceRecords.ToArray();

//            Console.WriteLine("=== Parallel vs Sequential Window Optimizer Performance Test ===");
//            Console.WriteLine($"Testing with {priceArray.Length} price records");
//            Console.WriteLine();

//            // Test sequential processing (disable parallelism)
//            Console.WriteLine("?? Running Sequential Processing...");
//            var stopwatchSequential = Stopwatch.StartNew();
//            var sequentialResults = WindowOptimizer.OptimizeWindowSizes(priceArray, maxDegreeOfParallelism: -1);
//            stopwatchSequential.Stop();

//            Console.WriteLine($"? Sequential completed in {stopwatchSequential.ElapsedMilliseconds:N0}ms");
//            Console.WriteLine($"   Configurations tested: {sequentialResults.ConfigurationResults.Count}");
//            Console.WriteLine();

//            // Test parallel processing (use all cores)
//            Console.WriteLine("? Running Parallel Processing...");
//            var stopwatchParallel = Stopwatch.StartNew();
//            var parallelResults = WindowOptimizer.OptimizeWindowSizes(priceArray, maxDegreeOfParallelism: 0);
//            stopwatchParallel.Stop();

//            Console.WriteLine($"? Parallel completed in {stopwatchParallel.ElapsedMilliseconds:N0}ms");
//            Console.WriteLine($"   Configurations tested: {parallelResults.ConfigurationResults.Count}");
//            Console.WriteLine();

//            // Performance comparison
//            double speedup = (double)stopwatchSequential.ElapsedMilliseconds / Math.Max(1, stopwatchParallel.ElapsedMilliseconds);
//            double efficiency = speedup / Environment.ProcessorCount * 100;

//            Console.WriteLine("?? Performance Analysis:");
//            Console.WriteLine($"   Sequential time: {stopwatchSequential.ElapsedMilliseconds:N0}ms");
//            Console.WriteLine($"   Parallel time:   {stopwatchParallel.ElapsedMilliseconds:N0}ms");
//            Console.WriteLine($"   Speedup:         {speedup:F2}x");
//            Console.WriteLine($"   CPU cores:       {Environment.ProcessorCount}");
//            Console.WriteLine($"   Efficiency:      {efficiency:F1}%");
//            Console.WriteLine();

//            // Verify results are consistent
//            Assert.AreEqual(sequentialResults.ConfigurationResults.Count, parallelResults.ConfigurationResults.Count,
//                "Both methods should test the same number of configurations");

//            // Results might differ slightly due to floating point precision in parallel operations
//            // but should be very close
//            var sequentialBestScore = sequentialResults.OptimalConfiguration.OverallScore;
//            var parallelBestScore = parallelResults.OptimalConfiguration.OverallScore;
//            double scoreDifference = Math.Abs(sequentialBestScore - parallelBestScore);

//            Console.WriteLine("?? Results Consistency Check:");
//            Console.WriteLine($"   Sequential best score: {sequentialBestScore:F6}");
//            Console.WriteLine($"   Parallel best score:   {parallelBestScore:F6}");
//            Console.WriteLine($"   Score difference:      {scoreDifference:F6}");

//            Assert.IsTrue(scoreDifference < 0.001, "Results should be nearly identical between sequential and parallel");

//            // Performance should improve with parallelization (unless overhead dominates)
//            if (speedup > 1.2)
//            {
//                Console.WriteLine("? Significant performance improvement with parallelization!");
//            }
//            else if (speedup > 1.0)
//            {
//                Console.WriteLine("? Modest performance improvement with parallelization");
//            }
//            else
//            {
//                Console.WriteLine("?? Parallelization overhead may be dominating for this dataset size");
//            }

//            Console.WriteLine();
//            Console.WriteLine("=== Test Complete ===");
//        }

//        [TestMethod]
//        public void TestLimitedParallelism()
//        {
//            // Create smaller test data for limited parallelism test
//            var baseDate = new DateTime(2023, 1, 1);
//            var priceRecords = new List<PriceRecord>();

//            for (int i = 0; i < 300; i++)
//            {
//                var date = baseDate.AddDays(i);
//                var price = 100.0 + i * 0.1;
//                var record = new PriceRecord(date, price, price + 0.5, price - 0.5, price + 0.25, 1000, price, 100, true);
//                priceRecords.Add(record);
//            }

//            var priceArray = priceRecords.ToArray();

//            Console.WriteLine("=== Limited Parallelism Test ===");
//            Console.WriteLine($"Testing with {priceArray.Length} price records");
//            Console.WriteLine($"Available CPU cores: {Environment.ProcessorCount}");
//            Console.WriteLine();

//            // Test with different parallelism limits
//            var parallelismLevels = new[] { 1, 2, 4, Environment.ProcessorCount };
//            var results = new List<(int threads, long timeMs, int configs)>();

//            foreach (var maxThreads in parallelismLevels)
//            {
//                if (maxThreads > Environment.ProcessorCount) continue;

//                Console.WriteLine($"?? Testing with max {maxThreads} thread(s)...");
//                var stopwatch = Stopwatch.StartNew();
//                var testResults = WindowOptimizer.OptimizeWindowSizes(priceArray, maxDegreeOfParallelism: maxThreads);
//                stopwatch.Stop();

//                results.Add((maxThreads, stopwatch.ElapsedMilliseconds, testResults.ConfigurationResults.Count));
//                Console.WriteLine($"   Completed in {stopwatch.ElapsedMilliseconds:N0}ms, tested {testResults.ConfigurationResults.Count} configs");
//            }

//            Console.WriteLine();
//            Console.WriteLine("?? Parallelism Scaling Analysis:");
//            Console.WriteLine($"{"Threads",-8} {"Time (ms)",-10} {"Configs",-8} {"Speedup",-8}");
//            Console.WriteLine(new string('-', 40));

//            var baseTime = results.First().timeMs;
//            foreach (var (threads, timeMs, configs) in results)
//            {
//                double speedup = (double)baseTime / Math.Max(1, timeMs);
//                Console.WriteLine($"{threads,-8} {timeMs,-10:N0} {configs,-8} {speedup,-8:F2}x");
//            }

//            // Verify all tests produced same number of configurations
//            var configCounts = results.Select(r => r.configs).Distinct().ToList();
//            Assert.AreEqual(1, configCounts.Count, "All parallelism levels should test the same configurations");

//            Console.WriteLine();
//            Console.WriteLine("? Limited parallelism test completed successfully");
//        }

//        [TestMethod]
//        public void TestThreadSafetyWithConcurrentAccess()
//        {
//            // Test that console output remains readable during parallel execution
//            var baseDate = new DateTime(2023, 1, 1);
//            var priceRecords = new List<PriceRecord>();

//            for (int i = 0; i < 200; i++)
//            {
//                var date = baseDate.AddDays(i);
//                var price = 100.0 + _random.NextDouble() * 10;
//                var record = new PriceRecord(date, price, price + 0.5, price - 0.5, price + 0.25, 1000, price, 100, true);
//                priceRecords.Add(record);
//            }

//            var priceArray = priceRecords.ToArray();

//            Console.WriteLine("=== Thread Safety and Console Output Test ===");
//            Console.WriteLine($"Testing thread-safe console output with {priceArray.Length} price records");
//            Console.WriteLine("Watch for garbled output - there should be none!");
//            Console.WriteLine();

//            // Run with parallel processing - if console output is garbled, thread safety failed
//            var results = WindowOptimizer.OptimizeWindowSizes(priceArray, maxDegreeOfParallelism: 0);

//            Console.WriteLine();
//            Console.WriteLine("? Thread safety test completed");
//            Console.WriteLine($"   If output above is readable and properly formatted, thread safety is working");
//            Console.WriteLine($"   Configurations tested: {results.ConfigurationResults.Count}");

//            // Verify we got reasonable results
//            Assert.IsTrue(results.ConfigurationResults.Count > 0, "Should have tested some configurations");
//            Assert.IsNotNull(results.OptimalConfiguration, "Should have found an optimal configuration");
//        }
//    }
//}

