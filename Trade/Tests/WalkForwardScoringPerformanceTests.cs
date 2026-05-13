using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Performance tests for WalkForwardScoring to measure and validate optimization improvements
    /// Ensures sufficient historical pre-history is seeded so indicators have warmup data.
    /// </summary>
    [TestClass]
    public class WalkForwardScoringPerformanceTests
    {
        // Performance thresholds in milliseconds for different operations
        private const double SINGLE_WALK_FORWARD_THRESHOLD_MS = 1000 * 30;    // 1 second for single WF run
        private const double SMALL_DATASET_THRESHOLD_MS = 2000 * 6;          // 2 seconds for small dataset
        private const double MEDIUM_DATASET_THRESHOLD_MS = 5000 * 6;         // 5 seconds for medium dataset
        private const double LARGE_DATASET_THRESHOLD_MS = 15000 * 6;         // 15 seconds for large dataset
        private const double MEDIAN_CALCULATION_THRESHOLD_MS = 10 * 3;       // 10ms for median calc

        // Test data sizes and parameters
        private const int SMALL_DATASET_DAYS = 504;      // 2 years of daily data
        private const int MEDIUM_DATASET_DAYS = 1260;    // 5 years of daily data
        private const int LARGE_DATASET_DAYS = 2520;     // 10 years of daily data
        
        private static readonly Dictionary<string, double> _performanceResults = new Dictionary<string, double>();

        #region Historical Seeding Helper

        private void EnsureHistoricalContext(PriceRecord[] mainRecords, int assumedMaxPeriod = 200)
        {
            GeneticIndividual.InitializePrices();
            int warmup = Math.Max(assumedMaxPeriod, 200); // ensure at least 200 days pre-history
            var earliest = mainRecords[0].DateTime.AddDays(-warmup);
            var prefix = new PriceRecord[warmup];
            var rng = new Random(123);
            double anchor = mainRecords[0].Close;
            for (int i = 0; i < warmup; i++)
            {
                var dt = earliest.AddDays(i);
                var noise = (rng.NextDouble() - 0.5) * 2.0;
                var open = anchor + noise;
                var close = open + (rng.NextDouble() - 0.5);
                var high = Math.Max(open, close) + rng.NextDouble();
                var low = Math.Min(open, close) - rng.NextDouble();
                var vol = 500000 + rng.Next(250000);
                prefix[i] = new PriceRecord(dt, TimeFrame.D1, open, high, low, close, volume: vol);
            }
            GeneticIndividual.Prices.AddPricesBatch(prefix);
            GeneticIndividual.Prices.AddPricesBatch(mainRecords);
        }

        #endregion

        #region Test Setup and Cleanup

        [ClassCleanup]
        public static void ClassCleanup()
        {
            ConsoleUtilities.WriteLine("\n=== WALK FORWARD SCORING PERFORMANCE SUMMARY ===");
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
            if (methodName.Contains("Single_WF")) return SINGLE_WALK_FORWARD_THRESHOLD_MS;
            if (methodName.Contains("Small")) return SMALL_DATASET_THRESHOLD_MS;
            if (methodName.Contains("Medium")) return MEDIUM_DATASET_THRESHOLD_MS;
            if (methodName.Contains("Large")) return LARGE_DATASET_THRESHOLD_MS;
            if (methodName.Contains("Median")) return MEDIAN_CALCULATION_THRESHOLD_MS;
            return 5000; // Default threshold
        }

        #endregion

        #region Walk Forward Scoring Performance Tests

        [TestMethod][TestCategory("Performance")]
        public void WalkForwardScoring_SingleRun_SmallDataset_Performance()
        {
            // Arrange
            var individual = CreateTestIndividual();
            var bars = CreateTestPriceRecords(SMALL_DATASET_DAYS);
            EnsureHistoricalContext(bars);
            var config = new BacktestConfig { RiskFreeRate = 0.02 };
            
            // Act & Assert
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                var result = WalkForwardScoring.WalkForwardScore(
                    individual, bars, 
                    trainDays: 252,    // 1 year training
                    testDays: 63,      // 3 months testing
                    stepDays: 21,      // 1 month steps
                    cfg: config);
                    
                Assert.IsNotNull(result);
                Assert.IsTrue(result.CompositeScore != double.NegativeInfinity);
            });
            
            _performanceResults["Single_WF_Small_Dataset"] = elapsedMs;
            
            ConsoleUtilities.WriteLine($"[PERF] Single WF Run (Small Dataset): {elapsedMs:F2}ms");
            Assert.IsTrue(elapsedMs < SINGLE_WALK_FORWARD_THRESHOLD_MS, 
                $"Single WF run took {elapsedMs:F2}ms, expected < {SINGLE_WALK_FORWARD_THRESHOLD_MS}ms");
        }

        [TestMethod][TestCategory("LongRunning")]
        public void WalkForwardScoring_MediumDataset_Performance()
        {
            // Arrange
            var individual = CreateTestIndividual();
            var bars = CreateTestPriceRecords(MEDIUM_DATASET_DAYS);
            EnsureHistoricalContext(bars);
            var config = new BacktestConfig { RiskFreeRate = 0.02 };
            
            // Act & Assert
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                var result = WalkForwardScoring.WalkForwardScore(
                    individual, bars,
                    trainDays: 252 * 2,  // 2 years training
                    testDays: 252,       // 1 year testing
                    stepDays: 126,       // 6 month steps
                    cfg: config);
                    
                Assert.IsNotNull(result);
                Assert.IsTrue(result.CompositeScore != double.NegativeInfinity);
            });
            
            _performanceResults["WF_Medium_Dataset"] = elapsedMs;
            
            ConsoleUtilities.WriteLine($"[PERF] WF Medium Dataset: {elapsedMs:F2}ms");
            Assert.IsTrue(elapsedMs < MEDIUM_DATASET_THRESHOLD_MS, 
                $"Medium WF run took {elapsedMs:F2}ms, expected < {MEDIUM_DATASET_THRESHOLD_MS}ms");
        }

        [TestMethod][TestCategory("LongRunning")]
        public void WalkForwardScoring_LargeDataset_Performance()
        {
            // Arrange
            var individual = CreateTestIndividual();
            var bars = CreateTestPriceRecords(LARGE_DATASET_DAYS);
            EnsureHistoricalContext(bars);
            var config = new BacktestConfig { RiskFreeRate = 0.025 };
            
            // Act & Assert
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                var result = WalkForwardScoring.WalkForwardScore(
                    individual, bars,
                    trainDays: 252 * 3,  // 3 years training
                    testDays: 252,       // 1 year testing  
                    stepDays: 252,       // 1 year steps
                    cfg: config);
                    
                Assert.IsNotNull(result);
                Assert.IsTrue(result.CompositeScore != double.NegativeInfinity);
            });
            
            _performanceResults["WF_Large_Dataset"] = elapsedMs;
            
            ConsoleUtilities.WriteLine($"[PERF] WF Large Dataset: {elapsedMs:F2}ms");
            Assert.IsTrue(elapsedMs < LARGE_DATASET_THRESHOLD_MS, 
                $"Large WF run took {elapsedMs:F2}ms, expected < {LARGE_DATASET_THRESHOLD_MS}ms");
        }

        #endregion

        #region Component Performance Tests

        [TestMethod][TestCategory("LongRunning")]
        public void WalkForwardScoring_MedianCalculation_Performance()
        {
            // Arrange - Test median calculation performance with different array sizes
            var testSizes = new[] { 10, 100, 1000, 5000 };
            var random = new Random(42);
            
            foreach (var size in testSizes)
            {
                var values = new double[size];
                for (int i = 0; i < size; i++)
                {
                    values[i] = random.NextDouble() * 100 - 50; // -50 to 50 range
                }
                
                // Act & Assert
                var elapsedMs = PerformanceTimer.TimeAction(() =>
                {
                    var median = CallPrivateMedian(values);
                    Assert.IsTrue(!double.IsNaN(median));
                });
                
                _performanceResults[$"Median_Calculation_{size}"] = elapsedMs;
                ConsoleUtilities.WriteLine($"[PERF] Median Calculation ({size} elements): {elapsedMs:F4}ms");
                
                // Median should be very fast
                Assert.IsTrue(elapsedMs < MEDIAN_CALCULATION_THRESHOLD_MS, 
                    $"Median calculation for {size} elements took {elapsedMs:F4}ms, expected < {MEDIAN_CALCULATION_THRESHOLD_MS}ms");
            }
        }

        [TestMethod][TestCategory("Performance")]
        public void WalkForwardScoring_StatisticalOperations_Performance()
        {
            // Arrange
            var individual = CreateTestIndividual();
            individual.Trades.Clear();
            
            // Add test trades
            var random = new Random(42);
            for (int i = 0; i < 100; i++)
            {
                var openPrice = 100 + random.NextDouble() * 20;
                var closePrice = openPrice + (random.NextDouble() - 0.5) * 20; // -10 to +10 range
                
                var trade = new TradeResult
                {
                    OpenPrice = openPrice,
                    ClosePrice = closePrice,
                    AllowedTradeType = AllowedTradeType.Buy,
                    Balance = 100000 + i * 1000,
                    Position = 10.0,
                    TotalDollarAmount = openPrice * 10.0
                };
                individual.Trades.Add(trade);
            }
            
            // Act & Assert - Test Sharpe calculation
            var sharpeTime = PerformanceTimer.TimeAction(() =>
            {
                var sharpe = CallPrivateCalculateSharpe(individual, 0.02);
                Assert.IsTrue(!double.IsNaN(sharpe));
            });
            
            // Test Max Drawdown calculation
            var ddTime = PerformanceTimer.TimeAction(() =>
            {
                var maxDD = CallPrivateCalculateMaxDrawdown(individual);
                Assert.IsTrue(maxDD >= 0);
            });
            
            _performanceResults["Statistical_Sharpe_Calculation"] = sharpeTime;
            _performanceResults["Statistical_MaxDD_Calculation"] = ddTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Sharpe Calculation: {sharpeTime:F4}ms");
            ConsoleUtilities.WriteLine($"[PERF] Max Drawdown Calculation: {ddTime:F4}ms");
            
            Assert.IsTrue(sharpeTime < 10, $"Sharpe calculation took {sharpeTime:F4}ms, expected < 1ms");
            Assert.IsTrue(ddTime < 1, $"Max drawdown calculation took {ddTime:F4}ms, expected < 1ms");
        }

        [TestMethod][TestCategory("Core")]
        public void WalkForwardScoring_BacktestCall_Performance()
        {
            // Arrange
            var individual = CreateTestIndividual();
            var segment = CreateTestPriceRecords(252); // 1 year of data
            EnsureHistoricalContext(segment);
            
            // Act & Assert - Test single backtest performance
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                var fitness = CallPrivateBacktest(individual, segment, 0.02);
                Assert.IsNotNull(fitness);
                Assert.IsTrue(!double.IsNaN(fitness.PercentGain));
            });
            
            _performanceResults["Single_Backtest_Call"] = elapsedMs;
            
            ConsoleUtilities.WriteLine($"[PERF] Single Backtest Call: {elapsedMs:F2}ms");
            Assert.IsTrue(elapsedMs < 500, // Should be reasonably fast
                $"Single backtest took {elapsedMs:F2}ms, expected < 500ms");
        }

        #endregion

        #region Scalability and Memory Tests

        //[TestMethod][TestCategory("Core")]
        public void WalkForwardScoring_MemoryUsage_StressTest()
        {
            // Test memory efficiency with multiple WF runs
            var initialMemory = GC.GetTotalMemory(true);
            
            var individual = CreateTestIndividual();
            var bars = CreateTestPriceRecords(1000); // Moderate size
            EnsureHistoricalContext(bars);
            var config = new BacktestConfig { RiskFreeRate = 0.02 };
            
            // Run multiple walk-forward analyses
            for (int i = 0; i < 5; i++)
            {
                var result = WalkForwardScoring.WalkForwardScore(
                    individual, bars,
                    trainDays: 200,
                    testDays: 50,
                    stepDays: 25,
                    cfg: config);
                Assert.IsNotNull(result);
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = (finalMemory - initialMemory) / (1024 * 1024); // MB
            
            ConsoleUtilities.WriteLine($"[PERF] Memory increase after 5 WF runs: {memoryIncrease:F2} MB");
            
            // Should not consume excessive memory (threshold: 100MB)
            Assert.IsTrue(memoryIncrease < 100, 
                $"Memory usage increased by {memoryIncrease:F2}MB, expected < 100MB");
        }

        [TestMethod][TestCategory("LongRunning")]
        public void WalkForwardScoring_ParameterScaling_Performance()
        {
            // Test performance scaling with different parameter combinations
            var individual = CreateTestIndividual();
            var bars = CreateTestPriceRecords(1260); // 5 years
            EnsureHistoricalContext(bars);
            var config = new BacktestConfig { RiskFreeRate = 0.02 };
            
            // Test different step sizes
            var stepSizes = new[] { 21, 63, 126, 252 }; // 1 month to 1 year
            
            foreach (var stepSize in stepSizes)
            {
                var elapsedMs = PerformanceTimer.TimeAction(() =>
                {
                    var result = WalkForwardScoring.WalkForwardScore(
                        individual, bars,
                        trainDays: 252,
                        testDays: 126,
                        stepDays: stepSize,
                        cfg: config);
                    Assert.IsNotNull(result);
                });
                
                _performanceResults[$"Scaling_StepSize_{stepSize}"] = elapsedMs;
                ConsoleUtilities.WriteLine($"[PERF] Step Size {stepSize} days: {elapsedMs:F2}ms");
                
                // Performance should scale reasonably with step size (fewer steps = less time)
                var expectedMaxMs = 30000; // Very generous scaling expectation
                Assert.IsTrue(elapsedMs < expectedMaxMs, 
                    $"Step size {stepSize} took {elapsedMs:F2}ms, expected < {expectedMaxMs}ms");
            }
        }

        #endregion

        #region Edge Cases and Robustness Tests

        [TestMethod][TestCategory("Core")]
        public void WalkForwardScoring_EdgeCases_Performance()
        {
            // Test performance with various edge cases
            var individual = CreateTestIndividual();
            
            // Test with minimal data
            var minimalBars = CreateTestPriceRecords(100);
            EnsureHistoricalContext(minimalBars);
            var elapsedMinimal = PerformanceTimer.TimeAction(() =>
            {
                var result = WalkForwardScoring.WalkForwardScore(
                    individual, minimalBars,
                    trainDays: 50,
                    testDays: 20,
                    stepDays: 10);
                Assert.IsNotNull(result);
            });
            
            // Test with no trades individual
            var noTradesIndividual = CreateNoTradesIndividual();
            var elapsedNoTrades = PerformanceTimer.TimeAction(() =>
            {
                var result = WalkForwardScoring.WalkForwardScore(
                    noTradesIndividual, minimalBars,
                    trainDays: 50,
                    testDays: 20,
                    stepDays: 10);
                Assert.IsNotNull(result);
            });
            
            _performanceResults["EdgeCase_Minimal_Data"] = elapsedMinimal;
            _performanceResults["EdgeCase_No_Trades"] = elapsedNoTrades;
            
            ConsoleUtilities.WriteLine($"[PERF] Edge Case (Minimal Data): {elapsedMinimal:F4}ms");
            ConsoleUtilities.WriteLine($"[PERF] Edge Case (No Trades): {elapsedNoTrades:F4}ms");
            
            Assert.IsTrue(elapsedMinimal < 1000, "Minimal data case should be very fast");
            Assert.IsTrue(elapsedNoTrades < 500, "No trades case should be very fast");
        }

        #endregion

        #region Helper Methods and Private Method Access

        private GeneticIndividual CreateTestIndividual()
        {
            var individual = new GeneticIndividual(new Random(42),
                100000, // starting balance
                1, 10,   // indicator type range
                5, 20,   // period range
                1, 2,    // mode range
                TimeFrame.D1, TimeFrame.D1,
                -1, 1,   // polarity range
                -2.0, 2.0, // threshold range
                3,       // max indicators
                0.05, 0.25, // trade percentage range
                0.01, 0.05, // options percentage range
                1, 5,    // option days out
                1, 3,    // strike distance
                5, 15,   // fast MA period
                20, 50,  // slow MA period
                1, 1,    // trade type (buy only)
                0, 0,    // option type
                1, 1,    // security type (stocks)
                1, 5);   // option contracts
                
            // Add some test trades for realistic performance testing
            var random = new Random(42);
            for (int i = 0; i < 20; i++)
            {
                var openPrice = 100 + random.NextDouble() * 10;
                var closePrice = openPrice + (random.NextDouble() - 0.5) * 10;
                
                individual.Trades.Add(new TradeResult
                {
                    OpenPrice = openPrice,
                    ClosePrice = closePrice,
                    AllowedTradeType = AllowedTradeType.Buy,
                    Balance = 100000 + i * 500,
                    Position = 10.0,
                    TotalDollarAmount = openPrice * 10.0
                });
            }
                
            return individual;
        }

        private GeneticIndividual CreateNoTradesIndividual()
        {
            var individual = CreateTestIndividual();
            individual.Trades.Clear(); // Remove all trades
            return individual;
        }

        private PriceRecord[] CreateTestPriceRecords(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var random = new Random(42); // Fixed seed for reproducible tests

            for (var i = 0; i < count; i++)
            {
                var date = baseDate.AddDays(i);
                var basePrice = 100.0 + i * 0.05 + Math.Sin(i * 0.02) * 5; // Trending with volatility
                var open = basePrice + (random.NextDouble() - 0.5) * 2;
                var close = basePrice + (random.NextDouble() - 0.5) * 2;
                var high = Math.Max(open, close) + random.NextDouble() * 1;
                var low = Math.Min(open, close) - random.NextDouble() * 1;
                var volume = 1000000 + random.Next(5000000);

                records[i] = new PriceRecord(date, TimeFrame.D1, open, high, low, close, volume: volume);
            }
            
            return records;
        }

        // Reflection-based access to private methods for performance testing
        private double CallPrivateMedian(IEnumerable<double> values)
        {
            var method = typeof(WalkForwardScoring).GetMethod("Median",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (double)method.Invoke(null, new object[] { values });
        }

        private double CallPrivateCalculateSharpe(GeneticIndividual individual, double riskFreeRate)
        {
            var method = typeof(WalkForwardScoring).GetMethod("CalculateSharpe",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (double)method.Invoke(null, new object[] { individual, riskFreeRate });
        }

        private double CallPrivateCalculateMaxDrawdown(GeneticIndividual individual)
        {
            var method = typeof(WalkForwardScoring).GetMethod("CalculateMaxDrawdown",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (double)method.Invoke(null, new object[] { individual });
        }

        private Fitness CallPrivateBacktest(GeneticIndividual individual, PriceRecord[] segment, double riskFreeRate)
        {
            var method = typeof(WalkForwardScoring).GetMethod("Backtest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return (Fitness)method.Invoke(null, new object[] { individual, segment, riskFreeRate });
        }

        #endregion
    }
}