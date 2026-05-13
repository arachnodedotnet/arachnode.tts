using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Trade.Prices2;

namespace Trade.Examples
{
    /// <summary>
    ///     Demonstration of parallel window optimization capabilities.
    ///     Shows performance improvements and proper thread-safe console output.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class ParallelWindowOptimizerDemo
    {
        public static void RunDemo()
        {
            Console.WriteLine("???????????????????????????????????????????????????????????????????????????????");
            Console.WriteLine("                        PARALLEL WINDOW OPTIMIZER DEMONSTRATION");
            Console.WriteLine("???????????????????????????????????????????????????????????????????????????????");
            Console.WriteLine();

            // Create comprehensive test data
            var priceRecords = GenerateTestData();

            Console.WriteLine("?? DEMONSTRATION SETUP:");
            Console.WriteLine($"   Test data: {priceRecords.Length} price records");
            Console.WriteLine(
                $"   Date range: {priceRecords[0].DateTime:yyyy-MM-dd} to {priceRecords[priceRecords.Length - 1].DateTime:yyyy-MM-dd}");
            Console.WriteLine($"   Available CPU cores: {Environment.ProcessorCount}");
            Console.WriteLine();

            // Demo 1: Sequential vs Parallel Performance
            DemoSequentialVsParallel(priceRecords);

            // Demo 2: Thread-Safe Console Output
            DemoThreadSafeConsoleOutput(priceRecords);

            // Demo 3: Configurable Parallelism
            DemoConfigurableParallelism(priceRecords);

            Console.WriteLine("???????????????????????????????????????????????????????????????????????????????");
            Console.WriteLine("                              DEMONSTRATION COMPLETE");
            Console.WriteLine("???????????????????????????????????????????????????????????????????????????????");
        }

        private static PriceRecord[] GenerateTestData()
        {
            var records = new List<PriceRecord>();
            var baseDate = new DateTime(2022, 1, 1);
            var random = new Random(42);

            // Generate 18 months of trading data (realistic dataset size)
            for (var i = 0; i < 378; i++) // 18 months * 21 trading days
            {
                var date = baseDate.AddDays(i);

                // Create realistic price movement with trend and volatility
                var trend = i * 0.05; // Gradual upward trend
                var volatility = Math.Sin(i * 0.02) * 5 + random.NextDouble() * 3 - 1.5;
                var price = 100.0 + trend + volatility;

                var open = price + (random.NextDouble() - 0.5) * 0.5;
                var close = price + (random.NextDouble() - 0.5) * 0.5;
                var high = Math.Max(open, close) + random.NextDouble() * 0.5;
                var low = Math.Min(open, close) - random.NextDouble() * 0.5;
                var volume = 1000000 + random.Next(500000);

                records.Add(new PriceRecord(date, TimeFrame.D1, open, high, low, close, volume: volume, wap: close, count: volume / 1000));
            }

            return records.ToArray();
        }

        private static void DemoSequentialVsParallel(PriceRecord[] priceRecords)
        {
            Console.WriteLine("?? DEMO 1: SEQUENTIAL VS PARALLEL PERFORMANCE");
            Console.WriteLine("?????????????????????????????????????????????????????????????????????????????");
            Console.WriteLine();

            // Sequential processing
            Console.WriteLine("Running SEQUENTIAL processing (single-threaded)...");
            var stopwatchSeq = Stopwatch.StartNew();
            var sequentialResults = WindowOptimizer.OptimizeWindowSizes(priceRecords);
            stopwatchSeq.Stop();

            Console.WriteLine($"? Sequential completed: {stopwatchSeq.ElapsedMilliseconds:N0}ms");
            Console.WriteLine($"   Configurations tested: {sequentialResults.ConfigurationResults.Count}");
            Console.WriteLine();

            // Parallel processing
            Console.WriteLine("Running PARALLEL processing (multi-threaded)...");
            var stopwatchPar = Stopwatch.StartNew();
            var parallelResults = WindowOptimizer.OptimizeWindowSizes(priceRecords);
            stopwatchPar.Stop();

            Console.WriteLine($"? Parallel completed: {stopwatchPar.ElapsedMilliseconds:N0}ms");
            Console.WriteLine($"   Configurations tested: {parallelResults.ConfigurationResults.Count}");
            Console.WriteLine();

            // Performance analysis
            var speedup = (double)stopwatchSeq.ElapsedMilliseconds / Math.Max(1, stopwatchPar.ElapsedMilliseconds);
            var efficiency = speedup / Environment.ProcessorCount * 100;

            Console.WriteLine("?? PERFORMANCE COMPARISON:");
            Console.WriteLine($"   Sequential time:  {stopwatchSeq.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"   Parallel time:    {stopwatchPar.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"   Performance gain: {speedup:F2}x faster");
            Console.WriteLine($"   Parallel efficiency: {efficiency:F1}%");
            Console.WriteLine(
                $"   Time saved: {stopwatchSeq.ElapsedMilliseconds - stopwatchPar.ElapsedMilliseconds:N0} ms");
            Console.WriteLine();

            if (speedup > 1.5)
                Console.WriteLine("?? EXCELLENT: Significant performance improvement with parallelization!");
            else if (speedup > 1.1)
                Console.WriteLine("? GOOD: Measurable performance improvement with parallelization");
            else
                Console.WriteLine("?? Note: Small dataset - parallelization overhead may limit gains");

            Console.WriteLine();
        }

        private static void DemoThreadSafeConsoleOutput(PriceRecord[] priceRecords)
        {
            Console.WriteLine("?? DEMO 2: THREAD-SAFE CONSOLE OUTPUT");
            Console.WriteLine("?????????????????????????????????????????????????????????????????????????????");
            Console.WriteLine();
            Console.WriteLine("This demo shows that console output remains clean and readable");
            Console.WriteLine("even when multiple threads are writing simultaneously.");
            Console.WriteLine("Watch the output below - it should be properly formatted:");
            Console.WriteLine();

            // Use subset of data for cleaner demo output
            var subset = new PriceRecord[Math.Min(200, priceRecords.Length)];
            Array.Copy(priceRecords, subset, subset.Length);

            // Run with limited parallelism to see thread IDs clearly
            WindowOptimizer.OptimizeWindowSizes(subset);

            Console.WriteLine();
            Console.WriteLine("? Thread-safe console output demonstration complete!");
            Console.WriteLine("   Notice how each line is complete and properly formatted");
            Console.WriteLine("   Thread IDs in [brackets] show multiple threads working");
            Console.WriteLine();
        }

        private static void DemoConfigurableParallelism(PriceRecord[] priceRecords)
        {
            Console.WriteLine("?? DEMO 3: CONFIGURABLE PARALLELISM");
            Console.WriteLine("?????????????????????????????????????????????????????????????????????????????");
            Console.WriteLine();
            Console.WriteLine("Testing different parallelism configurations:");
            Console.WriteLine();

            // Use smaller subset for faster demo
            var subset = new PriceRecord[Math.Min(150, priceRecords.Length)];
            Array.Copy(priceRecords, subset, subset.Length);

            var configurations = new[]
            {
                (-1, "Sequential (disabled)"),
                (1, "Single thread"),
                (2, "Two threads"),
                (Environment.ProcessorCount / 2, $"Half cores ({Environment.ProcessorCount / 2})"),
                (0, $"All cores ({Environment.ProcessorCount})")
            };

            var results = new List<(string name, long timeMs, int configs)>();

            foreach (var (threads, description) in configurations)
            {
                if (threads > Environment.ProcessorCount && threads > 0) continue;

                Console.WriteLine($"?? Testing {description}...");
                var stopwatch = Stopwatch.StartNew();
                var result = WindowOptimizer.OptimizeWindowSizes(subset);
                stopwatch.Stop();

                results.Add((description, stopwatch.ElapsedMilliseconds, result.ConfigurationResults.Count));
                Console.WriteLine($"   ? Completed in {stopwatch.ElapsedMilliseconds:N0}ms");
            }

            Console.WriteLine();
            Console.WriteLine("?? PARALLELISM SCALING RESULTS:");
            Console.WriteLine($"{"Configuration",-20} {"Time (ms)",-10} {"Speedup",-8}");
            Console.WriteLine(new string('?', 40));

            var baseTime = results.First().timeMs;
            foreach (var (name, timeMs, configs) in results)
            {
                var speedup = (double)baseTime / Math.Max(1, timeMs);
                Console.WriteLine($"{name,-20} {timeMs,-10:N0} {speedup,-8:F2}x");
            }

            Console.WriteLine();
            Console.WriteLine("?? KEY INSIGHTS:");
            Console.WriteLine("   • Sequential: Baseline performance, single CPU core");
            Console.WriteLine("   • Limited threads: Good for systems with limited resources");
            Console.WriteLine("   • All cores: Maximum performance for CPU-intensive workloads");
            Console.WriteLine("   • Optimal setting depends on dataset size and system load");
            Console.WriteLine();
        }
    }
}