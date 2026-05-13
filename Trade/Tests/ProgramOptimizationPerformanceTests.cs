using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Performance tests for Program.Optimization class methods to measure and validate optimization improvements
    /// Ensures sufficient pre-history is seeded for indicator warmup in all scenarios.
    /// </summary>
    [TestClass]
    public class ProgramOptimizationPerformanceTests
    {
        // Performance thresholds in milliseconds for different operations
        private const double WALKFORWARD_SINGLE_WINDOW_THRESHOLD_MS = 5000;  // 5 seconds per window
        private const double GENETIC_ALGORITHM_THRESHOLD_MS = 12000;         // 10 seconds for GA
        private const double ENHANCED_GA_THRESHOLD_MS = 15000;               // 15 seconds for enhanced GA
        private const double WALKFORWARD_ANALYSIS_THRESHOLD_MS = 60000;      // 60 seconds for full analysis

        // Test iteration counts
        private const int WARMUP_ITERATIONS = 2;
        private const int PERFORMANCE_ITERATIONS = 5;
        
        private static readonly Dictionary<string, double> _performanceResults = new Dictionary<string, double>();

        #region Test Setup and Cleanup

        [ClassCleanup]
        public static void ClassCleanup()
        {
            ConsoleUtilities.WriteLine("\n=== PROGRAM OPTIMIZATION PERFORMANCE SUMMARY ===");
            ConsoleUtilities.WriteLine("Method                                    | Avg Time (ms) | Threshold  | Status");
            ConsoleUtilities.WriteLine("------------------------------------------|---------------|------------|--------");
            
            foreach (var result in _performanceResults.OrderBy(r => r.Value))
            {
                var threshold = GetThresholdForMethod(result.Key);
                var status = result.Value <= threshold ? "PASS" : "FAIL";
                ConsoleUtilities.WriteLine($"{result.Key,-41} | {result.Value,13:F2} | {threshold,10:F0} | {status}");
            }
            ConsoleUtilities.WriteLine("================================================================================");
        }

        private static double GetThresholdForMethod(string methodName)
        {
            if (methodName.Contains("WalkforwardAnalysis")) return WALKFORWARD_ANALYSIS_THRESHOLD_MS;
            if (methodName.Contains("EnhancedGA")) return ENHANCED_GA_THRESHOLD_MS;
            if (methodName.Contains("GeneticAlgorithm")) return GENETIC_ALGORITHM_THRESHOLD_MS;
            if (methodName.Contains("SingleWindow")) return WALKFORWARD_SINGLE_WINDOW_THRESHOLD_MS;
            return 10000; // Default threshold
        }

        #endregion

        #region Walkforward Analysis Performance Tests

        //[TestMethod]
        public void RunWalkforwardAnalysisWithDates_SmallDataset_Performance()
        {
            // Arrange
            var priceRecords = CreateTestPriceRecords(252); // 1 year of data
            SetupTestParameters(smallDataset: true, priceRecords);
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivateRunWalkforwardAnalysisWithDates(priceRecords);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Windows.Count >= 0);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["WalkforwardAnalysis_Small_252"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Walkforward Analysis (252 records): {avgTime:F2}ms avg");
            Assert.IsTrue(avgTime < WALKFORWARD_ANALYSIS_THRESHOLD_MS / 2, 
                $"Small walkforward analysis took {avgTime:F2}ms, expected < {WALKFORWARD_ANALYSIS_THRESHOLD_MS / 2}ms");
        }

        //[TestMethod]
        public void RunWalkforwardAnalysisWithDates_MediumDataset_Performance()
        {
            // Arrange
            var priceRecords = CreateTestPriceRecords(756); // 3 years of data
            SetupTestParameters(smallDataset: false, priceRecords);
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivateRunWalkforwardAnalysisWithDates(priceRecords);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Windows.Count >= 0);
            }, PERFORMANCE_ITERATIONS / 2); // Fewer iterations for larger dataset
            
            _performanceResults["WalkforwardAnalysis_Medium_756"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Walkforward Analysis (756 records): {avgTime:F2}ms avg");
            Assert.IsTrue(avgTime < WALKFORWARD_ANALYSIS_THRESHOLD_MS, 
                $"Medium walkforward analysis took {avgTime:F2}ms, expected < {WALKFORWARD_ANALYSIS_THRESHOLD_MS}ms");
        }

        //[TestMethod]
        public void RunWalkforwardAnalysisWithDates_LargeDataset_Performance()
        {
            // Arrange
            var priceRecords = CreateTestPriceRecords(1260); // 5 years of data
            SetupTestParameters(smallDataset: false, priceRecords);
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivateRunWalkforwardAnalysisWithDates(priceRecords);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Windows.Count >= 0);
            }, 2); // Very few iterations for large dataset
            
            _performanceResults["WalkforwardAnalysis_Large_1260"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Walkforward Analysis (1260 records): {avgTime:F2}ms avg");
            Assert.IsTrue(avgTime < WALKFORWARD_ANALYSIS_THRESHOLD_MS * 2, 
                $"Large walkforward analysis took {avgTime:F2}ms, expected < {WALKFORWARD_ANALYSIS_THRESHOLD_MS * 2}ms");
        }

        #endregion

        #region Enhanced Genetic Algorithm Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void RunEnhancedGeneticAlgorithmWithHistoricalTracking_Performance()
        {
            // Arrange
            var trainingRecords = CreateTestPriceRecords(126); // 6 months training
            var validationRecords = CreateTestPriceRecords(21); // 1 month validation
            // ensure both training and validation have pre-history seeded
            SetupTestParameters(smallDataset: true, trainingRecords); // seeds historical once
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivateRunEnhancedGeneticAlgorithmWithHistoricalTracking(
                    trainingRecords, validationRecords, 0);
                Assert.IsNotNull(result);
                Assert.IsNotNull(result.Indicators);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["EnhancedGA_HistoricalTracking"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Enhanced GA with Historical Tracking: {avgTime:F2}ms avg");
            Assert.IsTrue(avgTime < ENHANCED_GA_THRESHOLD_MS, 
                $"Enhanced GA took {avgTime:F2}ms, expected < {ENHANCED_GA_THRESHOLD_MS}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void RunGeneticAlgorithm_Performance()
        {
            // Arrange
            var priceRecords = CreateTestPriceRecords(126); // 6 months of data
            SetupTestParameters(smallDataset: true, priceRecords);
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivateRunGeneticAlgorithm(priceRecords, runInParallel: false);
                Assert.IsNotNull(result);
                Assert.IsNotNull(result.Indicators);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["GeneticAlgorithm_Basic"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Basic Genetic Algorithm: {avgTime:F2}ms avg");
            Assert.IsTrue(avgTime < GENETIC_ALGORITHM_THRESHOLD_MS, 
                $"Basic GA took {avgTime:F2}ms, expected < {GENETIC_ALGORITHM_THRESHOLD_MS}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void RunEnhancedGeneticAlgorithm_Performance()
        {
            // Arrange
            var trainingRecords = CreateTestPriceRecords(126); // 6 months training
            var validationRecords = CreateTestPriceRecords(21); // 1 month validation
            SetupTestParameters(smallDataset: true, trainingRecords);
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivateRunEnhancedGeneticAlgorithm(trainingRecords, validationRecords);
                Assert.IsNotNull(result);
                Assert.IsNotNull(result.Indicators);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["EnhancedGA_Basic"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Enhanced GA Basic: {avgTime:F2}ms avg");
            Assert.IsTrue(avgTime < ENHANCED_GA_THRESHOLD_MS, 
                $"Enhanced GA basic took {avgTime:F2}ms, expected < {ENHANCED_GA_THRESHOLD_MS}ms");
        }

        #endregion

        #region Algorithm Component Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void TournamentSelection_Performance()
        {
            // Arrange
            var population = CreateTestPopulation(100);
            var random = new Random(42);
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var selected = CallPrivateTournamentSelection(population, random);
                Assert.IsNotNull(selected);
            }, 10000); // Many iterations for micro-benchmark
            
            _performanceResults["TournamentSelection_Micro"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Tournament Selection: {avgTime:F6}ms avg");
            Assert.IsTrue(avgTime < 1.0, 
                $"Tournament selection took {avgTime:F6}ms, expected < 1.0ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void MutateIndividual_Performance()
        {
            // Arrange
            var parent = CreateTestIndividual();
            var random = new Random(42);
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var mutated = CallPrivateMutateIndividual(parent, random);
                Assert.IsNotNull(mutated);
            }, 1000); // Many iterations for micro-benchmark
            
            _performanceResults["MutateIndividual_Micro"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Individual Mutation: {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < 10.0, 
                $"Individual mutation took {avgTime:F4}ms, expected < 10.0ms");
        }

        #endregion

        #region Memory and Scalability Tests

        //[TestMethod]
        public void WalkforwardAnalysis_MemoryUsage_StressTest()
        {
            // Test memory efficiency with multiple runs
            var initialMemory = GC.GetTotalMemory(true);
            
            var priceRecords = CreateTestPriceRecords(500);
            SetupTestParameters(smallDataset: false, priceRecords);
            
            // Run multiple times to stress test memory usage
            for (int i = 0; i < 5; i++)
            {
                var result = CallPrivateRunWalkforwardAnalysisWithDates(priceRecords);
                Assert.IsNotNull(result);
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = (finalMemory - initialMemory) / (1024 * 1024); // MB
            
            ConsoleUtilities.WriteLine($"[PERF] Memory increase after 5 walkforward runs: {memoryIncrease:F2} MB");
            
            // Should not consume excessive memory (threshold: 100MB)
            Assert.IsTrue(memoryIncrease < 100, 
                $"Memory usage increased by {memoryIncrease:F2}MB, expected < 100MB");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void GeneticAlgorithm_Performance_Baseline()
        {
            // Test baseline performance with current Program constants
            var priceRecords = CreateTestPriceRecords(126);
            SetupTestParameters(smallDataset: true, priceRecords);
            
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                var result = CallPrivateRunGeneticAlgorithm(priceRecords, runInParallel: false);
                Assert.IsNotNull(result);
            });
            
            ConsoleUtilities.WriteLine($"[PERF] GA Baseline (PopSize={Program.PopulationSize}, Gen={Program.Generations}): {elapsedMs:F2}ms");
            _performanceResults["GA_Baseline_Performance"] = elapsedMs;
            
            // Performance should be reasonable with current constants
            Assert.IsTrue(elapsedMs < GENETIC_ALGORITHM_THRESHOLD_MS * 2, 
                $"GA baseline took {elapsedMs:F2}ms, expected < {GENETIC_ALGORITHM_THRESHOLD_MS * 2}ms");
        }

        #endregion

        #region Helper Methods and Private Method Invocation

        private void SetupTestParameters(bool smallDataset, PriceRecord[] primaryRecords = null)
        {
            ConsoleUtilities.WriteLine($"[SETUP] Running {(smallDataset ? "small" : "large")} dataset performance test");
            ConsoleUtilities.WriteLine($"[SETUP] Using PopulationSize={Program.PopulationSize}, Generations={Program.Generations}");
            if (primaryRecords != null)
                EnsureHistoricalContext(primaryRecords);
        }

        /// <summary>
        /// Ensure static historical data (GeneticIndividual.Prices) has sufficient pre-history before earliest record
        /// to satisfy any indicator warmup period across all generated indicators.
        /// </summary>
        private void EnsureHistoricalContext(PriceRecord[] mainRecords)
        {
            GeneticIndividual.InitializePrices();
            // Pre-history length: max indicator period * 3 (fallback if constant unknown use 200)
            int warmup = Math.Max(200, Program.IndicatorPeriodMax * 3);
            var earliest = mainRecords[0].DateTime.AddDays(-warmup);
            var prefix = new PriceRecord[warmup];
            var rng = new Random(123);
            double basePrice = mainRecords[0].Close; // anchor around first price
            for (int i = 0; i < warmup; i++)
            {
                var dt = earliest.AddDays(i);
                var noise = (rng.NextDouble() - 0.5) * 2.0;
                var open = basePrice + noise;
                var close = open + (rng.NextDouble() - 0.5);
                var high = Math.Max(open, close) + rng.NextDouble();
                var low = Math.Min(open, close) - rng.NextDouble();
                var vol = 500000 + rng.Next(250000);
                prefix[i] = new PriceRecord(dt, TimeFrame.D1, open, high, low, close, volume: vol, wap: close, count: 1);
            }
            GeneticIndividual.Prices.AddPricesBatch(prefix);
            // Also add the main records so any global lookups have full continuity (some code may reference static cache)
            GeneticIndividual.Prices.AddPricesBatch(mainRecords);
        }

        private PriceRecord[] CreateTestPriceRecords(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var random = new Random(42);
            for (var i = 0; i < count; i++)
            {
                var date = baseDate.AddDays(i);
                var basePrice = 100.0 + i * 0.05 + Math.Sin(i * 0.02) * 10;
                var open = basePrice + (random.NextDouble() - 0.5) * 2;
                var close = basePrice + (random.NextDouble() - 0.5) * 2;
                var high = Math.Max(open, close) + random.NextDouble();
                var low = Math.Min(open, close) - random.NextDouble();
                var volume = 1000000 + random.Next(5000000);
                records[i] = new PriceRecord(date, TimeFrame.D1, open, high, low, close, volume: volume, wap: close, count: 1);
            }
            return records;
        }

        private List<GeneticIndividual> CreateTestPopulation(int size)
        {
            var population = new List<GeneticIndividual>();
            var random = new Random(42);
            for (int i = 0; i < size; i++)
            {
                var individual = CreateTestIndividual();
                individual.Fitness = new Fitness(random.NextDouble() * 1000 - 500, random.NextDouble() * 20 - 10);
                population.Add(individual);
            }
            return population;
        }

        private GeneticIndividual CreateTestIndividual()
        {
            return new GeneticIndividual(new Random(42),
                Program.StartingBalance,
                Program.IndicatorTypeMin, Program.IndicatorTypeMax,
                Program.IndicatorPeriodMin, Program.IndicatorPeriodMax,
                Program.IndicatorModeMin, Program.IndicatorModeMax,
                Program.IndicatorTimeFrameMin, Program.IndicatorTimeFrameMax,
                Program.IndicatorPolarityMin, Program.IndicatorPolarityMax,
                Program.IndicatorThresholdMin, Program.IndicatorThresholdMax,
                Program.MaxIndicators, Program.TradePercentageForStocksMin, Program.TradePercentageForStocksMax,
                Program.TradePercentageForOptionsMin, Program.TradePercentageForOptionsMax,
                Program.OptionDaysOutMin, Program.OptionDaysOutMax,
                Program.OptionStrikeDistanceMin, Program.OptionStrikeDistanceMax,
                Program.FastMAPeriodMin, Program.FastMAPeriodMax,
                Program.SlowMAPeriodMin, Program.SlowMAPeriodMax,
                Program.AllowedTradeTypeMin, Program.AllowedTradeTypeMax,
                Program.AllowedOptionTypeMin, Program.AllowedOptionTypeMax,
                Program.AllowedSecurityTypeMin, Program.AllowedSecurityTypeMax,
                Program.NumberOfOptionContractsMin, Program.NumberOfOptionContractsMax);
        }

        // Private method invocation using reflection for performance testing
        private Program.WalkforwardResults CallPrivateRunWalkforwardAnalysisWithDates(PriceRecord[] priceRecords)
        {
            var method = typeof(Program).GetMethod("RunWalkforwardAnalysisWithDates",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (Program.WalkforwardResults)method.Invoke(null, new object[] { priceRecords });
        }

        private GeneticIndividual CallPrivateRunEnhancedGeneticAlgorithmWithHistoricalTracking(
            PriceRecord[] trainingSplit, PriceRecord[] validationSplit, int windowIndex)
        {
            var method = typeof(Program).GetMethod("RunEnhancedGeneticAlgorithmWithHistoricalTracking",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (GeneticIndividual)method.Invoke(null, new object[] { trainingSplit, validationSplit, windowIndex });
        }

        private GeneticIndividual CallPrivateRunGeneticAlgorithm(PriceRecord[] priceRecords, bool runInParallel)
        {
            var method = typeof(Program).GetMethod("RunGeneticAlgorithm",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (GeneticIndividual)method.Invoke(null, new object[] { priceRecords, runInParallel });
        }

        private GeneticIndividual CallPrivateRunEnhancedGeneticAlgorithm(
            PriceRecord[] trainingRecords, PriceRecord[] validationRecords)
        {
            var method = typeof(Program).GetMethod("RunEnhancedGeneticAlgorithm",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (GeneticIndividual)method.Invoke(null, new object[] { trainingRecords, validationRecords });
        }

        private GeneticIndividual CallPrivateTournamentSelection(List<GeneticIndividual> population, Random rng)
        {
            var method = typeof(Program).GetMethod("TournamentSelection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (GeneticIndividual)method.Invoke(null, new object[] { population, rng });
        }

        private GeneticIndividual CallPrivateMutateIndividual(GeneticIndividual parent, Random rng)
        {
            var method = typeof(Program).GetMethod("MutateIndividual",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (GeneticIndividual)method.Invoke(null, new object[] { parent, rng });
        }

        #endregion
    }
}