using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Performance tests for GeneticIndividual.Constructors to measure and validate optimization improvements
    /// </summary>
    [TestClass]
    public class GeneticIndividualConstructorsPerformanceTests
    {
        // Performance thresholds in milliseconds for different operations
        private const double SINGLE_CONSTRUCTOR_THRESHOLD_MS = 10;      // Single individual creation
        private const double BATCH_CONSTRUCTOR_THRESHOLD_MS = 1000;     // 1000 individuals creation
        private const double LARGE_BATCH_THRESHOLD_MS = 5000;           // 5000 individuals creation
        private const double POSITION_SIZING_INIT_THRESHOLD_MS = 1;     // Position sizing initialization
        private const double SCALE_OUT_GENERATION_THRESHOLD_MS = 0.1;   // Scale-out fraction generation

        // Test iteration counts
        private const int WARMUP_ITERATIONS = 10;
        private const int PERFORMANCE_ITERATIONS = 1000;
        private const int LARGE_PERFORMANCE_ITERATIONS = 5000;
        
        private static readonly Dictionary<string, double> _performanceResults = new Dictionary<string, double>();

        #region Test Setup and Cleanup

        [ClassCleanup]
        public static void ClassCleanup()
        {
            ConsoleUtilities.WriteLine("\n=== GENETIC INDIVIDUAL CONSTRUCTORS PERFORMANCE SUMMARY ===");
            ConsoleUtilities.WriteLine("Method                                    | Avg Time (ms) | Threshold  | Status");
            ConsoleUtilities.WriteLine("------------------------------------------|---------------|------------|--------");
            
            foreach (var result in _performanceResults.OrderBy(r => r.Value))
            {
                var threshold = GetThresholdForMethod(result.Key);
                var status = result.Value <= threshold ? "PASS" : "FAIL";
                ConsoleUtilities.WriteLine($"{result.Key,-41} | {result.Value,13:F4} | {threshold,10:F1} | {status}");
            }
            ConsoleUtilities.WriteLine("================================================================================");
        }

        private static double GetThresholdForMethod(string methodName)
        {
            if (methodName.Contains("Single")) return SINGLE_CONSTRUCTOR_THRESHOLD_MS;
            if (methodName.Contains("Batch_1000")) return BATCH_CONSTRUCTOR_THRESHOLD_MS;
            if (methodName.Contains("Batch_5000")) return LARGE_BATCH_THRESHOLD_MS;
            if (methodName.Contains("PositionSizing")) return POSITION_SIZING_INIT_THRESHOLD_MS;
            if (methodName.Contains("ScaleOut")) return SCALE_OUT_GENERATION_THRESHOLD_MS;
            return 100; // Default threshold
        }

        #endregion

        #region Single Constructor Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void GeneticIndividual_SingleConstruction_Performance()
        {
            // Arrange
            var random = new Random(42); // Fixed seed for reproducible tests
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var individual = CreateTestIndividual(random);
                Assert.IsNotNull(individual);
                Assert.IsNotNull(individual.Indicators);
                Assert.IsTrue(individual.Indicators.Count > 0);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["Single_Constructor_Full"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Single GeneticIndividual Construction: {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < SINGLE_CONSTRUCTOR_THRESHOLD_MS, 
                $"Single construction took {avgTime:F4}ms, expected < {SINGLE_CONSTRUCTOR_THRESHOLD_MS}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void GeneticIndividual_BackCompatConstructor_Performance()
        {
            // Arrange
            var random = new Random(42);
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var individual = CreateTestIndividualBackCompat(random);
                Assert.IsNotNull(individual);
                Assert.IsNotNull(individual.Indicators);
                Assert.IsTrue(individual.Indicators.Count > 0);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["Single_BackCompat_Constructor"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Back-Compat Constructor: {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < SINGLE_CONSTRUCTOR_THRESHOLD_MS, 
                $"Back-compat construction took {avgTime:F4}ms, expected < {SINGLE_CONSTRUCTOR_THRESHOLD_MS}ms");
        }

        #endregion

        #region Batch Constructor Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void GeneticIndividual_BatchConstruction_1000_Performance()
        {
            // Arrange
            var random = new Random(42);
            var individuals = new List<GeneticIndividual>(1000);
            
            // Act & Assert
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    var individual = CreateTestIndividual(random);
                    individuals.Add(individual);
                }
            });
            
            _performanceResults["Batch_Constructor_1000"] = elapsedMs;
            
            ConsoleUtilities.WriteLine($"[PERF] Batch Construction (1000 individuals): {elapsedMs:F2}ms total");
            ConsoleUtilities.WriteLine($"[PERF] Average per individual: {elapsedMs / 1000:F4}ms");
            
            Assert.IsTrue(elapsedMs < BATCH_CONSTRUCTOR_THRESHOLD_MS, 
                $"Batch construction (1000) took {elapsedMs:F2}ms, expected < {BATCH_CONSTRUCTOR_THRESHOLD_MS}ms");
            Assert.AreEqual(1000, individuals.Count);
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void GeneticIndividual_BatchConstruction_5000_Performance()
        {
            // Arrange
            var random = new Random(42);
            var individuals = new List<GeneticIndividual>(5000);
            
            // Act & Assert
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                for (int i = 0; i < 5000; i++)
                {
                    var individual = CreateTestIndividual(random);
                    individuals.Add(individual);
                }
            });
            
            _performanceResults["Batch_Constructor_5000"] = elapsedMs;
            
            ConsoleUtilities.WriteLine($"[PERF] Large Batch Construction (5000 individuals): {elapsedMs:F2}ms total");
            ConsoleUtilities.WriteLine($"[PERF] Average per individual: {elapsedMs / 5000:F4}ms");
            
            Assert.IsTrue(elapsedMs < LARGE_BATCH_THRESHOLD_MS, 
                $"Large batch construction (5000) took {elapsedMs:F2}ms, expected < {LARGE_BATCH_THRESHOLD_MS}ms");
            Assert.AreEqual(5000, individuals.Count);
        }

        #endregion

        #region Component Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void GeneticIndividual_PositionSizingInit_Performance()
        {
            // Arrange
            var random = new Random(42);
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                // Create individual to trigger position sizing initialization
                var individual = CreateSimpleTestIndividual(random);
                // Verify position sizing was considered
                Assert.IsNotNull(individual);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["PositionSizing_Init"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Position Sizing Initialization: {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < POSITION_SIZING_INIT_THRESHOLD_MS, 
                $"Position sizing init took {avgTime:F4}ms, expected < {POSITION_SIZING_INIT_THRESHOLD_MS}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void GeneticIndividual_ScaleOutGeneration_Performance()
        {
            // Arrange
            var random = new Random(42);
            var totalContracts = 10.0;
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var fractions = CallPrivateGenerateValidScaleOutFractions(random, totalContracts);
                Assert.IsNotNull(fractions);
                Assert.AreEqual(8, fractions.Length);
                
                // Verify fractions sum to approximately 1.0
                var sum = fractions.Sum();
                Assert.IsTrue(Math.Abs(sum - 1.0) < 0.001, $"Fractions sum to {sum}, expected ~1.0");
            }, PERFORMANCE_ITERATIONS * 10); // More iterations for micro-benchmark
            
            _performanceResults["ScaleOut_Generation"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Scale-Out Fraction Generation: {avgTime:F6}ms avg");
            Assert.IsTrue(avgTime < SCALE_OUT_GENERATION_THRESHOLD_MS, 
                $"Scale-out generation took {avgTime:F6}ms, expected < {SCALE_OUT_GENERATION_THRESHOLD_MS}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void GeneticIndividual_IndicatorGeneration_Performance()
        {
            // Arrange
            var random = new Random(42);
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var individual = CreateTestIndividualWithManyIndicators(random, 10); // 10 indicators
                Assert.IsNotNull(individual);
                Assert.IsTrue(individual.Indicators.Count <= 10);
                
                // Verify all indicators have valid properties
                foreach (var indicator in individual.Indicators)
                {
                    Assert.IsTrue(indicator.Type >= Program.IndicatorTypeMin);
                    Assert.IsTrue(indicator.Type <= Program.IndicatorTypeMax);
                    Assert.IsTrue(indicator.Period >= Program.IndicatorPeriodMin);
                    Assert.IsTrue(indicator.Period <= Program.IndicatorPeriodMax);
                }
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["Indicator_Generation_10"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Indicator Generation (10 indicators): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < SINGLE_CONSTRUCTOR_THRESHOLD_MS * 2, // More lenient for many indicators
                $"Indicator generation took {avgTime:F4}ms, expected < {SINGLE_CONSTRUCTOR_THRESHOLD_MS * 2}ms");
        }

        #endregion

        #region Memory and Scalability Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void GeneticIndividual_MemoryUsage_StressTest()
        {
            // Test memory efficiency with large number of constructions
            var initialMemory = GC.GetTotalMemory(true);
            
            var random = new Random(42);
            var individuals = new List<GeneticIndividual>(2000);
            
            // Create many individuals to stress test memory usage
            for (int i = 0; i < 2000; i++)
            {
                var individual = CreateTestIndividual(random);
                individuals.Add(individual);
            }
            
            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = (finalMemory - initialMemory) / (1024 * 1024); // MB
            
            ConsoleUtilities.WriteLine($"[PERF] Memory increase after 2000 constructions: {memoryIncrease:F2} MB");
            ConsoleUtilities.WriteLine($"[PERF] Average memory per individual: {memoryIncrease * 1024 / 2000:F2} KB");
            
            // Should not consume excessive memory (threshold: 100MB)
            Assert.IsTrue(memoryIncrease < 100, 
                $"Memory usage increased by {memoryIncrease:F2}MB, expected < 100MB");
            
            // Verify all individuals are valid
            Assert.AreEqual(2000, individuals.Count);
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void GeneticIndividual_VariableIndicatorCount_Performance()
        {
            // Test performance scaling with different numbers of indicators
            var random = new Random(42);
            var indicatorCounts = new[] { 1, 3, 5, 10, 15 };
            
            foreach (var indicatorCount in indicatorCounts)
            {
                var elapsedMs = PerformanceTimer.TimeAction(() =>
                {
                    for (int i = 0; i < 100; i++) // 100 individuals per test
                    {
                        var individual = CreateTestIndividualWithManyIndicators(random, indicatorCount);
                        // The constructor uses random generation within the max, so check <= instead of ==
                        Assert.IsTrue(individual.Indicators.Count <= indicatorCount);
                        Assert.IsTrue(individual.Indicators.Count >= 1); // Should always have at least 1
                    }
                });
                
                var avgPerIndividual = elapsedMs / 100.0;
                ConsoleUtilities.WriteLine($"[PERF] Construction with max {indicatorCount} indicators: {avgPerIndividual:F4}ms avg per individual");
                _performanceResults[$"Variable_Indicators_{indicatorCount}"] = avgPerIndividual;
                
                // Performance should scale roughly linearly with indicator count
                var expectedMaxMs = indicatorCount * 2.0; // Very generous scaling expectation
                Assert.IsTrue(avgPerIndividual < expectedMaxMs, 
                    $"Construction with max {indicatorCount} indicators took {avgPerIndividual:F4}ms, expected < {expectedMaxMs}ms");
            }
        }

        #endregion

        #region Edge Case Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void GeneticIndividual_EdgeCases_Performance()
        {
            // Test performance with various edge cases
            var random = new Random(42);
            
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                // Test with minimum parameters
                var individual1 = CreateMinimalTestIndividual(random);
                Assert.IsNotNull(individual1);
                
                // Test with maximum parameters (where reasonable)
                var individual2 = CreateMaximalTestIndividual(random);
                Assert.IsNotNull(individual2);
                
                // Test with zero contracts (edge case)
                var individual3 = CreateTestIndividualWithZeroContracts(random);
                Assert.IsNotNull(individual3);
            }, PERFORMANCE_ITERATIONS / 3); // Fewer iterations since we create 3 individuals per iteration
            
            _performanceResults["EdgeCases_Mixed"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Edge Cases Handling: {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < SINGLE_CONSTRUCTOR_THRESHOLD_MS * 1.5, // Slightly more lenient
                $"Edge cases handling took {avgTime:F4}ms, expected < {SINGLE_CONSTRUCTOR_THRESHOLD_MS * 1.5}ms");
        }

        #endregion

        #region Helper Methods

        private GeneticIndividual CreateTestIndividual(Random random)
        {
            return new GeneticIndividual(random,
                Program.StartingBalance,
                Program.IndicatorTypeMin, Program.IndicatorTypeMax,
                Program.IndicatorPeriodMin, Program.IndicatorPeriodMax,
                Program.IndicatorModeMin, Program.IndicatorModeMax,
                Program.IndicatorTimeFrameMin, Program.IndicatorTimeFrameMax,
                Program.IndicatorPolarityMin, Program.IndicatorPolarityMax,
                Program.IndicatorThresholdMin, Program.IndicatorThresholdMax,
                Program.MaxIndicators,
                Program.TradePercentageForStocksMin, Program.TradePercentageForStocksMax,
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

        private GeneticIndividual CreateTestIndividualBackCompat(Random random)
        {
            return new GeneticIndividual(random,
                Program.StartingBalance,
                Program.IndicatorTypeMin, Program.IndicatorTypeMax,
                Program.IndicatorPeriodMin, Program.IndicatorPeriodMax,
                Program.IndicatorModeMin, Program.IndicatorModeMax,
                Program.IndicatorTimeFrameMin, Program.IndicatorTimeFrameMax,
                Program.IndicatorPolarityMin, Program.IndicatorPolarityMax,
                Program.IndicatorThresholdMin, Program.IndicatorThresholdMax,
                Program.MaxIndicators,
                Program.TradePercentageForStocksMin, Program.TradePercentageForStocksMax,
                Program.OptionDaysOutMin, Program.OptionDaysOutMax,
                Program.OptionStrikeDistanceMin, Program.OptionStrikeDistanceMax,
                Program.FastMAPeriodMin, Program.FastMAPeriodMax,
                Program.SlowMAPeriodMin, Program.SlowMAPeriodMax,
                Program.AllowedTradeTypeMin, Program.AllowedTradeTypeMax,
                Program.AllowedOptionTypeMin, Program.AllowedOptionTypeMax,
                Program.AllowedSecurityTypeMin, Program.AllowedSecurityTypeMax,
                Program.NumberOfOptionContractsMin, Program.NumberOfOptionContractsMax);
        }

        private GeneticIndividual CreateSimpleTestIndividual(Random random)
        {
            // Create with minimal parameters for focused testing
            return new GeneticIndividual(random,
                100000, // startingBalance
                1, 5,   // indicatorType range
                5, 20,  // indicatorPeriod range
                1, 2,   // indicatorMode range
                TimeFrame.D1, TimeFrame.D1, // timeFrame range
                -1, 1,  // polarity range
                -2.0, 2.0, // threshold range
                1,      // maxIndicators (minimal)
                0.05, 0.25, // stocks trade percentage
                0.01, 0.05, // options trade percentage
                1, 5,   // option days out
                1, 3,   // strike distance
                3, 8,   // fast MA period
                10, 20, // slow MA period
                1, 1,   // trade type (buy only)
                0, 0,   // option type
                1, 1,   // security type (stocks only)
                1, 5);  // option contracts
        }

        private GeneticIndividual CreateTestIndividualWithManyIndicators(Random random, int indicatorCount)
        {
            // Override maxIndicators to create individual with specific number of indicators
            var individual = new GeneticIndividual(random,
                Program.StartingBalance,
                Program.IndicatorTypeMin, Program.IndicatorTypeMax,
                Program.IndicatorPeriodMin, Program.IndicatorPeriodMax,
                Program.IndicatorModeMin, Program.IndicatorModeMax,
                Program.IndicatorTimeFrameMin, Program.IndicatorTimeFrameMax,
                Program.IndicatorPolarityMin, Program.IndicatorPolarityMax,
                Program.IndicatorThresholdMin, Program.IndicatorThresholdMax,
                indicatorCount, // Use specific indicator count
                Program.TradePercentageForStocksMin, Program.TradePercentageForStocksMax,
                Program.TradePercentageForOptionsMin, Program.TradePercentageForOptionsMax,
                Program.OptionDaysOutMin, Program.OptionDaysOutMax,
                Program.OptionStrikeDistanceMin, Program.OptionStrikeDistanceMax,
                Program.FastMAPeriodMin, Program.FastMAPeriodMax,
                Program.SlowMAPeriodMin, Program.SlowMAPeriodMax,
                Program.AllowedTradeTypeMin, Program.AllowedTradeTypeMax,
                Program.AllowedOptionTypeMin, Program.AllowedOptionTypeMax,
                Program.AllowedSecurityTypeMin, Program.AllowedSecurityTypeMax,
                Program.NumberOfOptionContractsMin, Program.NumberOfOptionContractsMax);
            
            return individual;
        }

        private GeneticIndividual CreateMinimalTestIndividual(Random random)
        {
            return new GeneticIndividual(random,
                1000,   // minimal balance
                1, 2,   // minimal indicator type range
                2, 5,   // minimal period range
                1, 1,   // single mode
                TimeFrame.D1, TimeFrame.D1, // single timeframe
                1, 1,   // single polarity
                0.1, 0.5, // small threshold range
                1,      // single indicator
                0.01, 0.02, // minimal trade percentages
                0.001, 0.002, // minimal options percentages
                1, 2,   // minimal option days
                1, 1,   // single strike distance
                2, 3,   // minimal MA periods
                5, 6,
                1, 1,   // single trade type
                1, 1,   // single option type
                1, 1,   // single security type
                1, 2);  // minimal contracts
        }

        private GeneticIndividual CreateMaximalTestIndividual(Random random)
        {
            return new GeneticIndividual(random,
                1000000, // large balance
                1, 50,   // full indicator type range
                5, 200,  // large period range
                1, 10,   // full mode range
                TimeFrame.M1, TimeFrame.D1, // full timeframe range
                -5, 5,   // full polarity range
                -10.0, 10.0, // large threshold range
                Program.MaxIndicators, // maximum indicators
                0.01, 0.90, // full stocks range
                0.001, 0.05, // full options range
                1, 30,   // full option days range
                1, 10,   // full strike distance range
                1, 20,   // full MA period ranges
                21, 50,
                1, 2,    // full trade type range
                1, 2,    // full option type range
                1, 2,    // full security type range
                1, 20);  // large contract range
        }

        private GeneticIndividual CreateTestIndividualWithZeroContracts(Random random)
        {
            return new GeneticIndividual(random,
                Program.StartingBalance,
                Program.IndicatorTypeMin, Program.IndicatorTypeMax,
                Program.IndicatorPeriodMin, Program.IndicatorPeriodMax,
                Program.IndicatorModeMin, Program.IndicatorModeMax,
                Program.IndicatorTimeFrameMin, Program.IndicatorTimeFrameMax,
                Program.IndicatorPolarityMin, Program.IndicatorPolarityMax,
                Program.IndicatorThresholdMin, Program.IndicatorThresholdMax,
                Program.MaxIndicators,
                Program.TradePercentageForStocksMin, Program.TradePercentageForStocksMax,
                Program.TradePercentageForOptionsMin, Program.TradePercentageForOptionsMax,
                Program.OptionDaysOutMin, Program.OptionDaysOutMax,
                Program.OptionStrikeDistanceMin, Program.OptionStrikeDistanceMax,
                Program.FastMAPeriodMin, Program.FastMAPeriodMax,
                Program.SlowMAPeriodMin, Program.SlowMAPeriodMax,
                Program.AllowedTradeTypeMin, Program.AllowedTradeTypeMax,
                Program.AllowedOptionTypeMin, Program.AllowedOptionTypeMax,
                Program.AllowedSecurityTypeMin, Program.AllowedSecurityTypeMax,
                0, 0); // Zero contracts - edge case
        }

        // Reflection-based method to test private static method
        private double[] CallPrivateGenerateValidScaleOutFractions(Random rng, double totalContracts)
        {
            // Try the optimized method name first
            var method = typeof(GeneticIndividual).GetMethod("GenerateValidScaleOutFractionsOptimized",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            // Fallback to original method name if optimized version not found
            if (method == null)
            {
                method = typeof(GeneticIndividual).GetMethod("GenerateValidScaleOutFractions",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            }
            
            if (method == null)
            {
                throw new InvalidOperationException("Could not find GenerateValidScaleOutFractions or GenerateValidScaleOutFractionsOptimized method");
            }
            
            return (double[])method.Invoke(null, new object[] { rng, totalContracts });
        }

        #endregion
    }
}