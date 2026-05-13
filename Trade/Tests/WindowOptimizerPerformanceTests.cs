using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Performance tests for WindowOptimizer class to measure and validate optimization improvements
    /// </summary>
    //[TestClass]
    public class WindowOptimizerPerformanceTests
    {
        private const int SMALL_DATASET_SIZE = 252;    // 1 year of data
        private const int MEDIUM_DATASET_SIZE = 756;   // 3 years of data
        private const int LARGE_DATASET_SIZE = 2520;   // 10 years of data
        
        // Performance thresholds in milliseconds
        private const double SMALL_DATASET_THRESHOLD_MS = 1000;   // 1 second for small dataset
        private const double MEDIUM_DATASET_THRESHOLD_MS = 5000;  // 5 seconds for medium dataset  
        private const double LARGE_DATASET_THRESHOLD_MS = 30000;  // 30 seconds for large dataset

        [TestMethod]
        public void OptimizeWindowSizes_SmallDataset_PerformanceBenchmark()
        {
            // Arrange
            var priceRecords = CreateTestPriceRecords(SMALL_DATASET_SIZE);
            
            // Act & Assert
            var (result, elapsedMs) = PerformanceTimer.TimeFunction(() => 
                WindowOptimizer.OptimizeWindowSizes(priceRecords));
            
            // Verify results are valid
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ConfigurationResults.Count > 0);
            
            // Performance assertion
            Assert.IsTrue(elapsedMs < SMALL_DATASET_THRESHOLD_MS, 
                $"Small dataset optimization took {elapsedMs:F2}ms, expected < {SMALL_DATASET_THRESHOLD_MS}ms");
            
            ConsoleUtilities.WriteLine($"[PERF] Small dataset ({SMALL_DATASET_SIZE} records): {elapsedMs:F2}ms");
        }

        [TestMethod]
        public void OptimizeWindowSizes_MediumDataset_PerformanceBenchmark()
        {
            // Arrange
            var priceRecords = CreateTestPriceRecords(MEDIUM_DATASET_SIZE);
            
            // Act & Assert
            var (result, elapsedMs) = PerformanceTimer.TimeFunction(() => 
                WindowOptimizer.OptimizeWindowSizes(priceRecords));
            
            // Verify results are valid
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ConfigurationResults.Count > 0);
            
            // Performance assertion
            Assert.IsTrue(elapsedMs < MEDIUM_DATASET_THRESHOLD_MS, 
                $"Medium dataset optimization took {elapsedMs:F2}ms, expected < {MEDIUM_DATASET_THRESHOLD_MS}ms");
            
            ConsoleUtilities.WriteLine($"[PERF] Medium dataset ({MEDIUM_DATASET_SIZE} records): {elapsedMs:F2}ms");
        }

        [TestMethod]
        public void OptimizeWindowSizes_LargeDataset_PerformanceBenchmark()
        {
            // Arrange
            var priceRecords = CreateTestPriceRecords(LARGE_DATASET_SIZE);
            
            // Act & Assert
            var (result, elapsedMs) = PerformanceTimer.TimeFunction(() => 
                WindowOptimizer.OptimizeWindowSizes(priceRecords));
            
            // Verify results are valid
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ConfigurationResults.Count > 0);
            
            // Performance assertion  
            Assert.IsTrue(elapsedMs < LARGE_DATASET_THRESHOLD_MS, 
                $"Large dataset optimization took {elapsedMs:F2}ms, expected < {LARGE_DATASET_THRESHOLD_MS}ms");
            
            ConsoleUtilities.WriteLine($"[PERF] Large dataset ({LARGE_DATASET_SIZE} records): {elapsedMs:F2}ms");
        }

        [TestMethod]
        public void GenerateWindowConfigurations_Performance()
        {
            var dataSizes = new[] { 252, 756, 1260, 2520 };
            
            foreach (var dataSize in dataSizes)
            {
                var elapsedMs = PerformanceTimer.TimeAction(() =>
                {
                    var configs = GetPrivateWindowConfigurations(dataSize);
                    Assert.IsTrue(configs.Count > 0);
                });
                
                ConsoleUtilities.WriteLine($"[PERF] GenerateWindowConfigurations({dataSize}): {elapsedMs:F3}ms");
                
                // Should be very fast - configuration generation is O(1) relative to data size
                Assert.IsTrue(elapsedMs < 100, $"Configuration generation took {elapsedMs:F2}ms, expected < 100ms");
            }
        }

        [TestMethod]
        public void RunWalkforwardAnalysisWithConfiguration_Performance()
        {
            var priceRecords = CreateTestPriceRecords(500);
            var config = new WindowOptimizer.WindowConfiguration
            {
                TrainingSize = 100,
                TestingSize = 30, 
                StepSize = 10
            };
            
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                var result = CallPrivateRunWalkforwardAnalysisWithConfiguration(priceRecords, config);
                Assert.IsNotNull(result.Windows);
            });
            
            ConsoleUtilities.WriteLine($"[PERF] Single walkforward configuration: {elapsedMs:F2}ms");
            
            // Individual walkforward should be reasonably fast
            Assert.IsTrue(elapsedMs < 2000, $"Single walkforward took {elapsedMs:F2}ms, expected < 2000ms");
        }

        [TestMethod]
        public void AnalyzeWindowConfiguration_Performance()
        {
            // Create test data
            var walkforwardResult = new WindowOptimizer.WalkforwardResults
            {
                Windows = Enumerable.Range(0, 10)
                    .Select(_ => new WindowOptimizer.WalkforwardWindow 
                    { 
                        TrainingPerformance = 5, 
                        TestPerformance = 4, 
                        PerformanceGap = 1 
                    })
                    .ToList(),
                AverageTrainingPerformance = 5,
                AverageTestPerformance = 4,
                AveragePerformanceGap = 1,
                ConsistencyScore = 5,
                OverfittingFrequency = 10
            };
            
            var config = new WindowOptimizer.WindowConfiguration 
            { 
                TrainingSize = 100, 
                TestingSize = 20, 
                StepSize = 5 
            };
            
            var avgElapsedMs = PerformanceTimer.TimeActionAverage(() =>
            {
                var analysis = CallPrivateAnalyzeWindowConfiguration(walkforwardResult, config);
                Assert.IsTrue(analysis.RobustnessScore >= 0.0);
            }, 1000);
            
            ConsoleUtilities.WriteLine($"[PERF] AnalyzeWindowConfiguration (avg of 1000): {avgElapsedMs:F4}ms");
            
            // Analysis should be very fast
            Assert.IsTrue(avgElapsedMs < 1.0, $"Window analysis took {avgElapsedMs:F4}ms average, expected < 1.0ms");
        }

        [TestMethod]
        public void CalculateStandardDeviation_Performance()
        {
            var testSizes = new[] { 10, 100, 1000, 10000 };
            var random = new Random(42);
            
            foreach (var size in testSizes)
            {
                var values = Enumerable.Range(0, size).Select(_ => random.NextDouble()).ToArray();
                
                var avgElapsedMs = PerformanceTimer.TimeActionAverage(() =>
                {
                    var stdDev = CallPrivateCalculateStandardDeviation(values);
                    Assert.IsTrue(stdDev >= 0);
                }, 1000);
                
                ConsoleUtilities.WriteLine($"[PERF] CalculateStandardDeviation({size} values): {avgElapsedMs:F4}ms");
                
                // Should scale roughly linearly with array size
                var expectedMaxMs = Math.Max(0.1, size / 10000.0); // Very generous threshold
                Assert.IsTrue(avgElapsedMs < expectedMaxMs, 
                    $"StdDev calculation for {size} values took {avgElapsedMs:F4}ms, expected < {expectedMaxMs:F4}ms");
            }
        }

        [TestMethod]  
        public void CalculateRiskAdjustedReturn_Performance()
        {
            var tradeCounts = new[] { 10, 50, 100, 500 };
            var random = new Random(42);
            
            foreach (var tradeCount in tradeCounts)
            {
                var individual = new GeneticIndividual();
                individual.Trades = Enumerable.Range(0, tradeCount)
                    .Select(_ => new TradeResult 
                    { 
                        OpenPrice = 100,
                        ClosePrice = 100 + (random.NextDouble() - 0.5) * 20,
                        AllowedTradeType = AllowedTradeType.Buy
                    })
                    .ToList();
                
                var avgElapsedMs = PerformanceTimer.TimeActionAverage(() =>
                {
                    var sharpe = CallPrivateCalculateRiskAdjustedReturn(individual);
                    Assert.IsFalse(double.IsNaN(sharpe));
                }, 1000);
                
                ConsoleUtilities.WriteLine($"[PERF] CalculateRiskAdjustedReturn({tradeCount} trades): {avgElapsedMs:F4}ms");
                
                // Should be very fast even with many trades
                Assert.IsTrue(avgElapsedMs < 0.5, 
                    $"Risk adjusted return for {tradeCount} trades took {avgElapsedMs:F4}ms, expected < 0.5ms");
            }
        }

        [TestMethod]
        public void FindOptimalConfiguration_Performance()
        {
            var configCounts = new[] { 10, 50, 100, 500 };
            var random = new Random(42);
            
            foreach (var configCount in configCounts)
            {
                var analyses = Enumerable.Range(0, configCount)
                    .Select(i => new WindowOptimizer.WindowConfigurationAnalysis
                    {
                        OverallScore = random.NextDouble(),
                        IsRecommended = random.NextDouble() > 0.7,
                        Configuration = new WindowOptimizer.WindowConfiguration
                        {
                            TrainingSize = 100,
                            TestingSize = 20,
                            StepSize = 5
                        }
                    })
                    .ToList();
                
                var avgElapsedMs = PerformanceTimer.TimeActionAverage(() =>
                {
                    var best = CallPrivateFindOptimalConfiguration(analyses);
                    Assert.IsTrue(best.OverallScore >= 0);
                }, 1000);
                
                ConsoleUtilities.WriteLine($"[PERF] FindOptimalConfiguration({configCount} configs): {avgElapsedMs:F4}ms");
                
                // Should scale well even with many configurations
                var expectedMaxMs = Math.Max(0.1, configCount / 1000.0);
                Assert.IsTrue(avgElapsedMs < expectedMaxMs, 
                    $"Finding optimal config from {configCount} took {avgElapsedMs:F4}ms, expected < {expectedMaxMs:F4}ms");
            }
        }

        [TestMethod]
        public void DisplayWindowOptimizationResults_Performance()
        {
            var results = CreateTestOptimizationResults(50); // 50 configurations
            
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                WindowOptimizer.DisplayWindowOptimizationResults(results);
            });
            
            ConsoleUtilities.WriteLine($"[PERF] DisplayWindowOptimizationResults: {elapsedMs:F2}ms");
            
            // Display should be reasonably fast
            Assert.IsTrue(elapsedMs < 500, $"Display took {elapsedMs:F2}ms, expected < 500ms");
        }

        [TestMethod]
        public void MemoryUsage_StressTest()
        {
            // Test memory efficiency with large datasets
            var initialMemory = GC.GetTotalMemory(true);
            
            var priceRecords = CreateTestPriceRecords(2000); // Large dataset
            var result = WindowOptimizer.OptimizeWindowSizes(priceRecords);
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = (finalMemory - initialMemory) / (1024 * 1024); // MB
            
            ConsoleUtilities.WriteLine($"[PERF] Memory increase: {memoryIncrease:F2} MB");
            
            // Should not consume excessive memory (threshold: 100MB)
            Assert.IsTrue(memoryIncrease < 100, 
                $"Memory usage increased by {memoryIncrease:F2}MB, expected < 100MB");
            
            // Verify results are still valid
            Assert.IsNotNull(result);
            Assert.IsTrue(result.ConfigurationResults.Count > 0);
        }

        [TestMethod]
        public void ConcurrencyStressTest()
        {
            // Test performance under concurrent access (simulating multiple optimization runs)
            var priceRecords = CreateTestPriceRecords(500);
            var tasks = new System.Threading.Tasks.Task[4];
            var results = new WindowOptimizer.WindowSizeOptimizationResults[4];
            var times = new double[4];
            
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    var taskStopwatch = Stopwatch.StartNew();
                    results[index] = WindowOptimizer.OptimizeWindowSizes(priceRecords);
                    taskStopwatch.Stop();
                    times[index] = taskStopwatch.Elapsed.TotalMilliseconds;
                });
            }
            
            System.Threading.Tasks.Task.WaitAll(tasks);
            stopwatch.Stop();
            
            // Verify all results are valid
            for (int i = 0; i < 4; i++)
            {
                Assert.IsNotNull(results[i]);
                Assert.IsTrue(results[i].ConfigurationResults.Count > 0);
                ConsoleUtilities.WriteLine($"[PERF] Concurrent task {i+1}: {times[i]:F2}ms");
            }
            
            ConsoleUtilities.WriteLine($"[PERF] Total concurrent execution: {stopwatch.ElapsedMilliseconds:F2}ms");
            ConsoleUtilities.WriteLine($"[PERF] Average per task: {times.Average():F2}ms");
            
            // Concurrent execution shouldn't take more than 2x sequential time
            var avgTaskTime = times.Average();
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < avgTaskTime * 2.5, 
                "Concurrent execution suggests contention or resource issues");
        }

        [TestMethod]
        public void PerformanceRegression_BaselineBenchmark()
        {
            // This test establishes a baseline for performance regression detection
            var testDataSizes = new[] { 252, 756, 1260 };
            var results = new List<(int size, double timeMs, int configCount)>();
            
            ConsoleUtilities.WriteLine("[PERF] === PERFORMANCE BASELINE BENCHMARK ===");
            
            foreach (var dataSize in testDataSizes)
            {
                var priceRecords = CreateTestPriceRecords(dataSize);
                
                var (result, elapsedMs) = PerformanceTimer.TimeFunction(() => 
                    WindowOptimizer.OptimizeWindowSizes(priceRecords));
                
                results.Add((dataSize, elapsedMs, result.ConfigurationResults.Count));
                
                ConsoleUtilities.WriteLine($"[PERF] {dataSize} records: {elapsedMs:F2}ms, {result.ConfigurationResults.Count} configs");
            }
            
            // Store results for future regression comparison
            ConsoleUtilities.WriteLine("[PERF] === BASELINE SUMMARY ===");
            ConsoleUtilities.WriteLine($"[PERF] Small (252): {results[0].timeMs:F2}ms");
            ConsoleUtilities.WriteLine($"[PERF] Medium (756): {results[1].timeMs:F2}ms"); 
            ConsoleUtilities.WriteLine($"[PERF] Large (1260): {results[2].timeMs:F2}ms");
            
            // Basic sanity checks
            Assert.IsTrue(results.All(r => r.timeMs > 0));
            Assert.IsTrue(results.All(r => r.configCount > 0));
        }

        // Helper methods for private access
        private List<WindowOptimizer.WindowConfiguration> GetPrivateWindowConfigurations(int totalDataPoints)
        {
            var method = typeof(WindowOptimizer).GetMethod("GenerateWindowConfigurations",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (List<WindowOptimizer.WindowConfiguration>)method.Invoke(null, new object[] { totalDataPoints });
        }

        private WindowOptimizer.WindowConfigurationAnalysis CallPrivateAnalyzeWindowConfiguration(
            WindowOptimizer.WalkforwardResults walkforward, WindowOptimizer.WindowConfiguration config)
        {
            var method = typeof(WindowOptimizer).GetMethod("AnalyzeWindowConfiguration",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (WindowOptimizer.WindowConfigurationAnalysis)method.Invoke(null,
                new object[] { walkforward, config });
        }

        private WindowOptimizer.WindowConfigurationAnalysis CallPrivateFindOptimalConfiguration(
            List<WindowOptimizer.WindowConfigurationAnalysis> analyses)
        {
            var method = typeof(WindowOptimizer).GetMethod("FindOptimalConfiguration",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (WindowOptimizer.WindowConfigurationAnalysis)method.Invoke(null, new object[] { analyses });
        }

        private double CallPrivateCalculateStandardDeviation(double[] values)
        {
            var method = typeof(WindowOptimizer).GetMethod("CalculateStandardDeviation",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (double)method.Invoke(null, new object[] { values });
        }

        private double CallPrivateCalculateRiskAdjustedReturn(GeneticIndividual individual, double riskFreeRate = 0.02)
        {
            var method = typeof(WindowOptimizer).GetMethod("CalculateRiskAdjustedReturn",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (double)method.Invoke(null, new object[] { individual, riskFreeRate });
        }

        private WindowOptimizer.WalkforwardResults CallPrivateRunWalkforwardAnalysisWithConfiguration(
            PriceRecord[] priceRecords, WindowOptimizer.WindowConfiguration config)
        {
            var method = typeof(WindowOptimizer).GetMethod("RunWalkforwardAnalysisWithConfiguration",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (WindowOptimizer.WalkforwardResults)method.Invoke(null, new object[] { priceRecords, config });
        }

        // Test data creation helpers
        private PriceRecord[] CreateTestPriceRecords(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var random = new Random(42); // Fixed seed for reproducible tests

            for (var i = 0; i < count; i++)
            {
                var date = baseDate.AddDays(i);
                var basePrice = 100.0 + i * 0.05 + Math.Sin(i * 0.02) * 10; // Trending with volatility
                var open = basePrice + (random.NextDouble() - 0.5) * 2;
                var close = basePrice + (random.NextDouble() - 0.5) * 2;
                var high = Math.Max(open, close) + random.NextDouble() * 1;
                var low = Math.Min(open, close) - random.NextDouble() * 1;
                var volume = 1000000 + random.Next(5000000); // Realistic volume

                records[i] = new PriceRecord(date, TimeFrame.D1, open, high, low, close, volume: volume, wap: close, count: 1);
            }
            return records;
        }

        private WindowOptimizer.WindowSizeOptimizationResults CreateTestOptimizationResults(int configCount)
        {
            var results = new WindowOptimizer.WindowSizeOptimizationResults(true);
            var random = new Random(42);

            for (int i = 0; i < configCount; i++)
            {
                var analysis = new WindowOptimizer.WindowConfigurationAnalysis
                {
                    Configuration = new WindowOptimizer.WindowConfiguration
                    {
                        TrainingSize = 100 + i * 20,
                        TestingSize = 20 + i * 5,
                        StepSize = 5 + i,
                        TrainingMonths = (100 + i * 20) / 21.0,
                        TestingMonths = (20 + i * 5) / 21.0,
                        StepWeeks = (5 + i) / 5.0
                    },
                    RobustnessScore = random.NextDouble(),
                    ConsistencyScore = random.NextDouble(),
                    EfficiencyScore = random.NextDouble(),
                    StatisticalPower = random.NextDouble(),
                    OverallScore = random.NextDouble(),
                    IsRecommended = random.NextDouble() > 0.7,
                    WalkforwardResults = new WindowOptimizer.WalkforwardResults
                    {
                        Windows = new List<WindowOptimizer.WalkforwardWindow>(),
                        AverageTestPerformance = random.NextDouble() * 20 - 5,
                        ConsistencyScore = random.NextDouble() * 30,
                        OverfittingFrequency = random.NextDouble() * 50
                    }
                };
                results.ConfigurationResults.Add(analysis);
            }

            results.OptimalConfiguration = results.ConfigurationResults.OrderByDescending(r => r.OverallScore).First();
            results.Recommendations = new List<string> { "Test recommendation 1", "Test recommendation 2" };

            return results;
        }
    }
}