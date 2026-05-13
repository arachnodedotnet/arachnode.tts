using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Performance tests for RiskMetrics class to measure and validate optimization improvements
    /// </summary>
    [TestClass]
    public class RiskMetricsPerformanceTests
    {
        // Performance thresholds in milliseconds for different operations
        private const double SHARPE_CALCULATION_THRESHOLD_MS = 10.0;  // Per 1000 trades
        private const double DRAWDOWN_CALCULATION_THRESHOLD_MS = 15.0; // Per 1000 trades
        private const double CAGR_CALCULATION_THRESHOLD_MS = 5.0;      // Per 1000 trades

        // Test iteration counts
        private const int WARMUP_ITERATIONS = 10;
        private const int PERFORMANCE_ITERATIONS = 1000;
        
        private static readonly Dictionary<string, double> _performanceResults = new Dictionary<string, double>();

        #region Test Setup and Cleanup

        [ClassCleanup]
        public static void ClassCleanup()
        {
            ConsoleUtilities.WriteLine("\n=== RISK METRICS PERFORMANCE SUMMARY ===");
            ConsoleUtilities.WriteLine("Method                    | Avg Time (ms) | Threshold | Status");
            ConsoleUtilities.WriteLine("--------------------------|---------------|-----------|--------");
            
            foreach (var result in _performanceResults.OrderBy(r => r.Value))
            {
                var threshold = GetThresholdForMethod(result.Key);
                var status = result.Value <= threshold ? "PASS" : "FAIL";
                ConsoleUtilities.WriteLine($"{result.Key,-25} | {result.Value,13:F4} | {threshold,9:F1} | {status}");
            }
            ConsoleUtilities.WriteLine("========================================================");
        }

        private static double GetThresholdForMethod(string methodName)
        {
            if (methodName.Contains("Sharpe")) return SHARPE_CALCULATION_THRESHOLD_MS;
            if (methodName.Contains("Drawdown")) return DRAWDOWN_CALCULATION_THRESHOLD_MS;
            if (methodName.Contains("CAGR")) return CAGR_CALCULATION_THRESHOLD_MS;
            return 10.0; // Default threshold
        }

        #endregion

        #region Sharpe Ratio Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculateSharpe_SmallTradeSet_Performance()
        {
            // Arrange
            var individual = CreateTestIndividual(10); // 10 trades
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var sharpe = RiskMetrics.CalculateSharpe(individual);
                Assert.IsFalse(double.IsNaN(sharpe));
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["Sharpe_10_Trades"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Sharpe calculation (10 trades): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < SHARPE_CALCULATION_THRESHOLD_MS / 100, 
                $"Sharpe calculation for 10 trades took {avgTime:F4}ms, expected < {SHARPE_CALCULATION_THRESHOLD_MS / 100}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculateSharpe_MediumTradeSet_Performance()
        {
            // Arrange
            var individual = CreateTestIndividual(100); // 100 trades
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var sharpe = RiskMetrics.CalculateSharpe(individual);
                Assert.IsFalse(double.IsNaN(sharpe));
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["Sharpe_100_Trades"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Sharpe calculation (100 trades): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < SHARPE_CALCULATION_THRESHOLD_MS / 10, 
                $"Sharpe calculation for 100 trades took {avgTime:F4}ms, expected < {SHARPE_CALCULATION_THRESHOLD_MS / 10}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculateSharpe_LargeTradeSet_Performance()
        {
            // Arrange
            var individual = CreateTestIndividual(1000); // 1000 trades
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var sharpe = RiskMetrics.CalculateSharpe(individual);
                Assert.IsFalse(double.IsNaN(sharpe));
            }, PERFORMANCE_ITERATIONS / 10); // Fewer iterations for large dataset
            
            _performanceResults["Sharpe_1000_Trades"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Sharpe calculation (1000 trades): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < SHARPE_CALCULATION_THRESHOLD_MS, 
                $"Sharpe calculation for 1000 trades took {avgTime:F4}ms, expected < {SHARPE_CALCULATION_THRESHOLD_MS}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculateSharpe_StressTest_Performance()
        {
            // Arrange
            var individual = CreateTestIndividual(5000); // 5000 trades - stress test
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var sharpe = RiskMetrics.CalculateSharpe(individual);
                Assert.IsFalse(double.IsNaN(sharpe));
            }, 100); // Fewer iterations for stress test
            
            _performanceResults["Sharpe_5000_Trades"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Sharpe calculation (5000 trades): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < SHARPE_CALCULATION_THRESHOLD_MS * 5, 
                $"Sharpe calculation for 5000 trades took {avgTime:F4}ms, expected < {SHARPE_CALCULATION_THRESHOLD_MS * 5}ms");
        }

        #endregion

        #region Max Drawdown Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculateMaxDrawdown_SmallTradeSet_Performance()
        {
            // Arrange
            var individual = CreateTestIndividual(10); // 10 trades
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var drawdown = RiskMetrics.CalculateMaxDrawdown(individual);
                Assert.IsTrue(drawdown >= 0);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["Drawdown_10_Trades"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Max Drawdown calculation (10 trades): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < DRAWDOWN_CALCULATION_THRESHOLD_MS / 100, 
                $"Max Drawdown calculation for 10 trades took {avgTime:F4}ms, expected < {DRAWDOWN_CALCULATION_THRESHOLD_MS / 100}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculateMaxDrawdown_MediumTradeSet_Performance()
        {
            // Arrange
            var individual = CreateTestIndividual(100); // 100 trades
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var drawdown = RiskMetrics.CalculateMaxDrawdown(individual);
                Assert.IsTrue(drawdown >= 0);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["Drawdown_100_Trades"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Max Drawdown calculation (100 trades): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < DRAWDOWN_CALCULATION_THRESHOLD_MS / 10, 
                $"Max Drawdown calculation for 100 trades took {avgTime:F4}ms, expected < {DRAWDOWN_CALCULATION_THRESHOLD_MS / 10}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculateMaxDrawdown_LargeTradeSet_Performance()
        {
            // Arrange
            var individual = CreateTestIndividual(1000); // 1000 trades
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var drawdown = RiskMetrics.CalculateMaxDrawdown(individual);
                Assert.IsTrue(drawdown >= 0);
            }, PERFORMANCE_ITERATIONS / 10); // Fewer iterations for large dataset
            
            _performanceResults["Drawdown_1000_Trades"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Max Drawdown calculation (1000 trades): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < DRAWDOWN_CALCULATION_THRESHOLD_MS, 
                $"Max Drawdown calculation for 1000 trades took {avgTime:F4}ms, expected < {DRAWDOWN_CALCULATION_THRESHOLD_MS}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculateMaxDrawdown_StressTest_Performance()
        {
            // Arrange
            var individual = CreateTestIndividual(5000); // 5000 trades - stress test
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var drawdown = RiskMetrics.CalculateMaxDrawdown(individual);
                Assert.IsTrue(drawdown >= 0);
            }, 100); // Fewer iterations for stress test
            
            _performanceResults["Drawdown_5000_Trades"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Max Drawdown calculation (5000 trades): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < DRAWDOWN_CALCULATION_THRESHOLD_MS * 5, 
                $"Max Drawdown calculation for 5000 trades took {avgTime:F4}ms, expected < {DRAWDOWN_CALCULATION_THRESHOLD_MS * 5}ms");
        }

        #endregion

        #region CAGR Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculateCagr_SmallTradeSet_Performance()
        {
            // Arrange
            var trades = CreateTestTrades(10); // 10 trades
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var cagr = RiskMetrics.CalculateCagr(100000, 120000, trades);
                Assert.IsFalse(double.IsNaN(cagr));
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["CAGR_10_Trades"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] CAGR calculation (10 trades): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < CAGR_CALCULATION_THRESHOLD_MS / 100, 
                $"CAGR calculation for 10 trades took {avgTime:F4}ms, expected < {CAGR_CALCULATION_THRESHOLD_MS / 100}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculateCagr_MediumTradeSet_Performance()
        {
            // Arrange
            var trades = CreateTestTrades(100); // 100 trades
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var cagr = RiskMetrics.CalculateCagr(100000, 150000, trades);
                Assert.IsFalse(double.IsNaN(cagr));
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["CAGR_100_Trades"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] CAGR calculation (100 trades): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < CAGR_CALCULATION_THRESHOLD_MS / 10, 
                $"CAGR calculation for 100 trades took {avgTime:F4}ms, expected < {CAGR_CALCULATION_THRESHOLD_MS / 10}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculateCagr_LargeTradeSet_Performance()
        {
            // Arrange
            var trades = CreateTestTrades(1000); // 1000 trades
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var cagr = RiskMetrics.CalculateCagr(100000, 200000, trades);
                Assert.IsFalse(double.IsNaN(cagr));
            }, PERFORMANCE_ITERATIONS / 10); // Fewer iterations for large dataset
            
            _performanceResults["CAGR_1000_Trades"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] CAGR calculation (1000 trades): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < CAGR_CALCULATION_THRESHOLD_MS, 
                $"CAGR calculation for 1000 trades took {avgTime:F4}ms, expected < {CAGR_CALCULATION_THRESHOLD_MS}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculateCagr_StressTest_Performance()
        {
            // Arrange
            var trades = CreateTestTrades(5000); // 5000 trades - stress test
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var cagr = RiskMetrics.CalculateCagr(100000, 250000, trades);
                Assert.IsFalse(double.IsNaN(cagr));
            }, 100); // Fewer iterations for stress test
            
            _performanceResults["CAGR_5000_Trades"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] CAGR calculation (5000 trades): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < CAGR_CALCULATION_THRESHOLD_MS * 5, 
                $"CAGR calculation for 5000 trades took {avgTime:F4}ms, expected < {CAGR_CALCULATION_THRESHOLD_MS * 5}ms");
        }

        #endregion

        #region Memory and Concurrency Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void RiskMetrics_MemoryUsage_StressTest()
        {
            // Test memory efficiency with large datasets
            var initialMemory = GC.GetTotalMemory(true);
            
            var individual = CreateTestIndividual(2000); // Large dataset
            var trades = individual.Trades;
            
            // Run all calculations multiple times to stress test memory usage
            for (int i = 0; i < 100; i++)
            {
                var sharpe = RiskMetrics.CalculateSharpe(individual);
                var drawdown = RiskMetrics.CalculateMaxDrawdown(individual);
                var cagr = RiskMetrics.CalculateCagr(100000, 150000, trades);
            }
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = (finalMemory - initialMemory) / (1024 * 1024); // MB
            
            ConsoleUtilities.WriteLine($"[PERF] Memory increase: {memoryIncrease:F2} MB");
            
            // Should not consume excessive memory (threshold: 50MB)
            Assert.IsTrue(memoryIncrease < 50, 
                $"Memory usage increased by {memoryIncrease:F2}MB, expected < 50MB");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void RiskMetrics_ConcurrentAccess_Performance()
        {
            // Test performance under concurrent access
            var individual = CreateTestIndividual(500);
            var trades = individual.Trades;
            var tasks = new System.Threading.Tasks.Task[4];
            var times = new double[4];
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    var taskStopwatch = System.Diagnostics.Stopwatch.StartNew();
                    for (int j = 0; j < 250; j++) // 250 iterations per task
                    {
                        var sharpe = RiskMetrics.CalculateSharpe(individual);
                        var drawdown = RiskMetrics.CalculateMaxDrawdown(individual);
                        var cagr = RiskMetrics.CalculateCagr(100000, 150000, trades);
                    }
                    taskStopwatch.Stop();
                    times[index] = taskStopwatch.Elapsed.TotalMilliseconds;
                });
            }
            
            System.Threading.Tasks.Task.WaitAll(tasks);
            stopwatch.Stop();
            
            // Verify all tasks completed successfully
            for (int i = 0; i < 4; i++)
            {
                ConsoleUtilities.WriteLine($"[PERF] Concurrent task {i+1}: {times[i]:F2}ms");
                Assert.IsTrue(times[i] > 0, $"Task {i+1} did not complete successfully");
            }
            
            ConsoleUtilities.WriteLine($"[PERF] Total concurrent execution: {stopwatch.ElapsedMilliseconds:F2}ms");
            ConsoleUtilities.WriteLine($"[PERF] Average per task: {times.Average():F2}ms");
            
            // Concurrent execution should complete within reasonable time
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000, 
                "Concurrent execution took too long - suggests performance issues");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void RiskMetrics_EdgeCases_Performance()
        {
            // Test performance with edge cases
            var emptyIndividual = new GeneticIndividual();
            var singleTradeIndividual = CreateTestIndividual(1);
            var identicalReturnsIndividual = CreateIdenticalReturnsIndividual(100);
            
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                // Empty trades
                var sharpe1 = RiskMetrics.CalculateSharpe(emptyIndividual);
                var drawdown1 = RiskMetrics.CalculateMaxDrawdown(emptyIndividual);
                var cagr1 = RiskMetrics.CalculateCagr(100000, 100000, new List<TradeResult>());
                
                // Single trade
                var sharpe2 = RiskMetrics.CalculateSharpe(singleTradeIndividual);
                var drawdown2 = RiskMetrics.CalculateMaxDrawdown(singleTradeIndividual);
                var cagr2 = RiskMetrics.CalculateCagr(100000, 105000, singleTradeIndividual.Trades);
                
                // Identical returns
                var sharpe3 = RiskMetrics.CalculateSharpe(identicalReturnsIndividual);
                var drawdown3 = RiskMetrics.CalculateMaxDrawdown(identicalReturnsIndividual);
                
                // Validate results are reasonable
                Assert.IsTrue(sharpe1 == 0.0 || double.IsPositiveInfinity(sharpe1));
                Assert.IsTrue(drawdown1 >= 0);
                Assert.IsFalse(double.IsNaN(sharpe2));
                Assert.IsTrue(double.IsPositiveInfinity(sharpe3) || sharpe3 >= 0);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["EdgeCases_Mixed"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Edge cases handling: {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < 1.0, 
                $"Edge cases handling took {avgTime:F4}ms, expected < 1.0ms");
        }

        #endregion

        #region Helper Methods

        private GeneticIndividual CreateTestIndividual(int tradeCount)
        {
            var individual = new GeneticIndividual();
            individual.StartingBalance = 100000;
            individual.Trades = new List<TradeResult>();
            
            var random = new Random(42); // Fixed seed for reproducible tests
            var balance = individual.StartingBalance;
            var baseDate = DateTime.Today.AddDays(-tradeCount);
            
            for (int i = 0; i < tradeCount; i++)
            {
                var baseOpenPrice = 100.0 + random.NextDouble() * 50; // 100-150 range
                var percentGain = (random.NextDouble() - 0.4) * 20; // -8% to +12% range, slightly positive bias
                var closePrice = baseOpenPrice * (1.0 + percentGain / 100.0);
                
                var dollarGain = balance * (percentGain / 100.0);
                balance += dollarGain;
                
                var openDate = baseDate.AddDays(i * 2); // Every other day
                var closeDate = openDate.AddDays(1);
                
                individual.Trades.Add(new TradeResult
                {
                    OpenPrice = baseOpenPrice,
                    ClosePrice = closePrice,
                    Balance = balance,
                    OpenIndex = i * 2,
                    CloseIndex = i * 2 + 1,
                    Position = 100, // 100 shares
                    PositionInDollars = baseOpenPrice * 100,
                    PriceRecordForOpen = CreatePriceRecord(openDate, baseOpenPrice),
                    PriceRecordForClose = CreatePriceRecord(closeDate, closePrice),
                    AllowedTradeType = AllowedTradeType.Buy,
                    AllowedSecurityType = AllowedSecurityType.Stock
                });
            }
            
            return individual;
        }
        
        private List<TradeResult> CreateTestTrades(int tradeCount)
        {
            return CreateTestIndividual(tradeCount).Trades;
        }
        
        private GeneticIndividual CreateIdenticalReturnsIndividual(int tradeCount)
        {
            var individual = new GeneticIndividual();
            individual.StartingBalance = 100000;
            individual.Trades = new List<TradeResult>();
            
            var balance = individual.StartingBalance;
            var baseDate = DateTime.Today.AddDays(-tradeCount);
            
            for (int i = 0; i < tradeCount; i++)
            {
                var openPrice = 100.0;
                var closePrice = 105.0; // 5% gain for all trades
                
                var dollarGain = balance * 0.05; // 5% gain
                balance += dollarGain;
                
                var openDate = baseDate.AddDays(i * 2);
                var closeDate = openDate.AddDays(1);
                
                individual.Trades.Add(new TradeResult
                {
                    OpenPrice = openPrice,
                    ClosePrice = closePrice,
                    Balance = balance,
                    OpenIndex = i * 2,
                    CloseIndex = i * 2 + 1,
                    Position = 100, // 100 shares
                    PositionInDollars = openPrice * 100,
                    PriceRecordForOpen = CreatePriceRecord(openDate, openPrice),
                    PriceRecordForClose = CreatePriceRecord(closeDate, closePrice),
                    AllowedTradeType = AllowedTradeType.Buy,
                    AllowedSecurityType = AllowedSecurityType.Stock
                });
            }
            
            return individual;
        }
        
        private PriceRecord CreatePriceRecord(DateTime date, double price)
        {
            return new PriceRecord(date, TimeFrame.D1, price, price + 1, price - 1, price, volume: 1000000, wap: price, count: 1);
        }

        #endregion
    }
}