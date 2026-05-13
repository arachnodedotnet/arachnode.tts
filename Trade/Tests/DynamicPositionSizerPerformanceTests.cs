using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Comprehensive performance tests for DynamicPositionSizer class
    /// </summary>
    [TestClass]
    public class DynamicPositionSizerPerformanceTests
    {
        private const int WARMUP_ITERATIONS = 25;
        private const int TEST_ITERATIONS = 500;
        private const double PERFORMANCE_THRESHOLD_MS = 50.0; // Max acceptable time for 500 operations
        private const double COMPLEX_THRESHOLD_MS = 100.0; // Higher threshold for complex calculations

        private static readonly Dictionary<string, PerformanceMetrics> _performanceResults = 
            new Dictionary<string, PerformanceMetrics>();

        private DynamicPositionSizer _sizer;
        private PositionSizingContext _testContext;
        private PositionSizingConfig _testConfig;

        #region Test Setup and Cleanup

        [ClassInitialize]
        public static void ClassSetup(TestContext context)
        {
            // Disable console output during performance tests to avoid I/O overhead
            ConsoleUtilities.Enabled = false;
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            ConsoleUtilities.Enabled = true;
            
            // Output performance summary
            Console.WriteLine("\n=== DYNAMIC POSITION SIZER PERFORMANCE SUMMARY ===");
            Console.WriteLine("Test Name                           | Avg (ms) | Max (ms) | Ops/sec | Status");
            Console.WriteLine("------------------------------------|----------|----------|---------|--------");
            
            foreach (var result in _performanceResults.OrderBy(r => r.Value.AverageMs))
            {
                var metrics = result.Value;
                var threshold = result.Key.Contains("Complex") || result.Key.Contains("Kelly") ? 
                    COMPLEX_THRESHOLD_MS : PERFORMANCE_THRESHOLD_MS;
                var status = metrics.AverageMs <= threshold ? "PASS" : "FAIL";
                var opsPerSec = metrics.AverageMs > 0 ? 1000.0 / metrics.AverageMs : 0;
                
                Console.WriteLine($"{result.Key,-35} | {metrics.AverageMs,8:F2} | {metrics.MaxMs,8:F2} | {opsPerSec,7:F0} | {status}");
            }
            Console.WriteLine("================================================================");
        }

        [TestInitialize]
        public void TestSetup()
        {
            // Create test configuration optimized for performance testing
            _testConfig = new PositionSizingConfig
            {
                Method = PositionSizingMethod.FixedPercentage,
                MaxPositionSize = 0.25,
                MinPositionSize = 0.005,
                BaseRiskPerTrade = 0.02,
                KellyMultiplier = 0.25,
                LookbackPeriod = 30, // Smaller for faster testing
                VolatilityLookback = 10,
                ATRPeriod = 10,
                EnableHeatAdjustment = true,
                EnableConcurrentPositionLimit = true,
                MaxConcurrentPositions = 5
            };

            _sizer = new DynamicPositionSizer(_testConfig);
            _testContext = CreateTestContext();
        }

        #endregion

        #region Core Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculatePositionSize_FixedPercentage_PerformanceTest()
        {
            _testConfig.Method = PositionSizingMethod.FixedPercentage;
            
            var testName = "Fixed Percentage Sizing";
            var metrics = MeasurePerformance(testName, () =>
            {
                var result = _sizer.CalculatePositionSize(_testContext, 100.0, AllowedTradeType.Any);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.PositionSize > 0);
            });
            
            Assert.IsTrue(metrics.AverageMs <= PERFORMANCE_THRESHOLD_MS,
                $"Fixed percentage sizing too slow: {metrics.AverageMs:F2}ms avg");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculatePositionSize_Kelly_PerformanceTest()
        {
            _testConfig.Method = PositionSizingMethod.KellyOptimal;
            _testContext.RecentTrades = CreateTestTrades(50); // Sufficient history for Kelly
            
            var testName = "Kelly Optimal Sizing";
            var metrics = MeasurePerformance(testName, () =>
            {
                var result = _sizer.CalculatePositionSize(_testContext, 100.0, AllowedTradeType.Any);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.PositionSize > 0);
            });
            
            Assert.IsTrue(metrics.AverageMs <= COMPLEX_THRESHOLD_MS,
                $"Kelly optimal sizing too slow: {metrics.AverageMs:F2}ms avg");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculatePositionSize_VolatilityAdjusted_PerformanceTest()
        {
            _testConfig.Method = PositionSizingMethod.VolatilityAdjusted;
            _testContext.PriceHistory = CreateTestPriceHistory(50);
            
            var testName = "Volatility Adjusted Sizing";
            var metrics = MeasurePerformance(testName, () =>
            {
                var result = _sizer.CalculatePositionSize(_testContext, 100.0, AllowedTradeType.Any);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.PositionSize > 0);
            });
            
            Assert.IsTrue(metrics.AverageMs <= COMPLEX_THRESHOLD_MS,
                $"Volatility adjusted sizing too slow: {metrics.AverageMs:F2}ms avg");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculatePositionSize_ATRBased_PerformanceTest()
        {
            _testConfig.Method = PositionSizingMethod.ATRBased;
            _testContext.ATR = 2.5;
            
            var testName = "ATR Based Sizing";
            var metrics = MeasurePerformance(testName, () =>
            {
                var result = _sizer.CalculatePositionSize(_testContext, 100.0, AllowedTradeType.Any);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.PositionSize > 0);
            });
            
            Assert.IsTrue(metrics.AverageMs <= PERFORMANCE_THRESHOLD_MS,
                $"ATR based sizing too slow: {metrics.AverageMs:F2}ms avg");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculatePositionSize_DrawdownProtective_PerformanceTest()
        {
            _testConfig.Method = PositionSizingMethod.DrawdownProtective;
            _testContext.MaxDrawdownFromPeak = 0.15; // High drawdown to trigger protection
            
            var testName = "Drawdown Protective Sizing";
            var metrics = MeasurePerformance(testName, () =>
            {
                var result = _sizer.CalculatePositionSize(_testContext, 100.0, AllowedTradeType.Any);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.PositionSize > 0);
            });
            
            Assert.IsTrue(metrics.AverageMs <= PERFORMANCE_THRESHOLD_MS,
                $"Drawdown protective sizing too slow: {metrics.AverageMs:F2}ms avg");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculatePositionSize_MomentumAdaptive_PerformanceTest()
        {
            _testConfig.Method = PositionSizingMethod.MomentumAdaptive;
            _testContext.MarketMomentum = 0.08; // Strong momentum
            
            var testName = "Momentum Adaptive Sizing";
            var metrics = MeasurePerformance(testName, () =>
            {
                var result = _sizer.CalculatePositionSize(_testContext, 100.0, AllowedTradeType.Any);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.PositionSize > 0);
            });
            
            Assert.IsTrue(metrics.AverageMs <= PERFORMANCE_THRESHOLD_MS,
                $"Momentum adaptive sizing too slow: {metrics.AverageMs:F2}ms avg");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculatePositionSize_MarketRegimeAdaptive_PerformanceTest()
        {
            _testConfig.Method = PositionSizingMethod.MarketRegimeAdaptive;
            _testContext.CurrentRegime = MarketRegime.Trending;
            
            var testName = "Market Regime Adaptive Sizing";
            var metrics = MeasurePerformance(testName, () =>
            {
                var result = _sizer.CalculatePositionSize(_testContext, 100.0, AllowedTradeType.Any);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.PositionSize > 0);
            });
            
            Assert.IsTrue(metrics.AverageMs <= PERFORMANCE_THRESHOLD_MS,
                $"Market regime adaptive sizing too slow: {metrics.AverageMs:F2}ms avg");
        }

        #endregion

        #region Stress Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculatePositionSize_LargeTradeHistory_StressTest()
        {
            _testConfig.Method = PositionSizingMethod.KellyOptimal;
            _testContext.RecentTrades = CreateTestTrades(500); // Large history
            
            var testName = "Large Trade History (500 trades)";
            var metrics = MeasurePerformance(testName, () =>
            {
                var result = _sizer.CalculatePositionSize(_testContext, 100.0, AllowedTradeType.Any);
                Assert.IsNotNull(result);
            }, 100); // Fewer iterations for stress test
            
            Assert.IsTrue(metrics.AverageMs <= COMPLEX_THRESHOLD_MS * 2,
                $"Large trade history processing too slow: {metrics.AverageMs:F2}ms avg");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculatePositionSize_LargePriceHistory_StressTest()
        {
            _testConfig.Method = PositionSizingMethod.VolatilityAdjusted;
            _testContext.PriceHistory = CreateTestPriceHistory(1000); // Large price history
            
            var testName = "Large Price History (1000 bars)";
            var metrics = MeasurePerformance(testName, () =>
            {
                var result = _sizer.CalculatePositionSize(_testContext, 100.0, AllowedTradeType.Any);
                Assert.IsNotNull(result);
            }, 100); // Fewer iterations for stress test
            
            Assert.IsTrue(metrics.AverageMs <= COMPLEX_THRESHOLD_MS * 2,
                $"Large price history processing too slow: {metrics.AverageMs:F2}ms avg");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CalculatePositionSize_MultipleOpenPositions_StressTest()
        {
            _testContext.OpenPositions = CreateTestTrades(20); // Many open positions
            _testContext.TotalExposure = 0.85; // High exposure
            
            var testName = "Multiple Open Positions (20)";
            var metrics = MeasurePerformance(testName, () =>
            {
                var result = _sizer.CalculatePositionSize(_testContext, 100.0, AllowedTradeType.Any);
                Assert.IsNotNull(result);
            });
            
            Assert.IsTrue(metrics.AverageMs <= PERFORMANCE_THRESHOLD_MS,
                $"Multiple positions processing too slow: {metrics.AverageMs:F2}ms avg");
        }

        #endregion

        #region Feature Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void GetSuggestedMethod_PerformanceTest()
        {
            var testName = "Get Suggested Method";
            var metrics = MeasurePerformance(testName, () =>
            {
                var method = _sizer.GetSuggestedMethod(_testContext);
                Assert.IsTrue(Enum.IsDefined(typeof(PositionSizingMethod), method));
            });
            
            Assert.IsTrue(metrics.AverageMs <= 5.0, // Should be very fast
                $"Get suggested method too slow: {metrics.AverageMs:F2}ms avg");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void GenerateSizingReport_PerformanceTest()
        {
            var result = _sizer.CalculatePositionSize(_testContext, 100.0, AllowedTradeType.Any);
            
            var testName = "Generate Sizing Report";
            var metrics = MeasurePerformance(testName, () =>
            {
                var report = _sizer.GenerateSizingReport(_testContext, result);
                Assert.IsFalse(string.IsNullOrEmpty(report));
            });
            
            Assert.IsTrue(metrics.AverageMs <= 10.0, // Report generation should be fast
                $"Generate sizing report too slow: {metrics.AverageMs:F2}ms avg");
        }

        #endregion

        #region Benchmark Comparisons

        [TestMethod]
        [TestCategory("Performance")]
        public void AllMethods_ComparativePerformanceBenchmark()
        {
            var methods = new[]
            {
                PositionSizingMethod.FixedPercentage,
                PositionSizingMethod.KellyOptimal,
                PositionSizingMethod.VolatilityAdjusted,
                PositionSizingMethod.ATRBased,
                PositionSizingMethod.DrawdownProtective,
                PositionSizingMethod.MomentumAdaptive,
                PositionSizingMethod.MarketRegimeAdaptive
            };

            Console.WriteLine("\n=== METHOD PERFORMANCE COMPARISON ===");
            Console.WriteLine("Method                    | Avg (ms) | Ops/sec");
            Console.WriteLine("--------------------------|----------|--------");

            foreach (var method in methods)
            {
                _testConfig.Method = method;
                
                // Ensure we have appropriate test data for each method
                SetupContextForMethod(method);
                
                var elapsed = PerformanceTimer.TimeActionAverage(() =>
                {
                    var result = _sizer.CalculatePositionSize(_testContext, 100.0, AllowedTradeType.Any);
                    Assert.IsNotNull(result);
                }, 100);
                
                var opsPerSec = elapsed > 0 ? 1000.0 / elapsed : 0;
                Console.WriteLine($"{method,-25} | {elapsed,8:F3} | {opsPerSec,7:F0}");
            }
            Console.WriteLine("==========================================");
        }

        #endregion

        #region Helper Methods

        private class PerformanceMetrics
        {
            public double AverageMs { get; set; }
            public double MinMs { get; set; }
            public double MaxMs { get; set; }
            public double TotalMs { get; set; }
        }

        private PerformanceMetrics MeasurePerformance(string testName, Action operation, int iterations = TEST_ITERATIONS)
        {
            // Warmup
            for (int i = 0; i < WARMUP_ITERATIONS; i++)
            {
                operation();
            }
            
            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Measurement
            var times = new double[iterations];
            var totalStopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                operation();
                sw.Stop();
                times[i] = sw.Elapsed.TotalMilliseconds;
            }
            
            totalStopwatch.Stop();
            
            var metrics = new PerformanceMetrics
            {
                AverageMs = times.Average(),
                MinMs = times.Min(),
                MaxMs = times.Max(),
                TotalMs = totalStopwatch.Elapsed.TotalMilliseconds
            };
            
            _performanceResults[testName] = metrics;
            return metrics;
        }

        private PositionSizingContext CreateTestContext()
        {
            return new PositionSizingContext
            {
                PriceHistory = CreateTestPriceHistory(20),
                CurrentPrice = 100.0,
                AverageVolume = 1000000,
                AccountBalance = 100000,
                AvailableBalance = 95000,
                UnrealizedPnL = 0,
                MaxDrawdownFromPeak = 0.05,
                OpenPositions = new List<TradeResult>(),
                RecentTrades = CreateTestTrades(10),
                TotalExposure = 0.3,
                WinRate = 0.55,
                AverageWin = 150,
                AverageLoss = -100,
                ProfitFactor = 1.5,
                SharpeRatio = 1.2,
                CurrentRegime = MarketRegime.Trending,
                MarketVolatility = 0.15,
                MarketMomentum = 0.05,
                ATR = 2.0,
                VaR95 = 500,
                MaxCorrelationWithExisting = 0.3,
                IsHeatPeriod = false
            };
        }

        private PriceRecord[] CreateTestPriceHistory(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var basePrice = 100.0;
            var random = new Random(42); // Fixed seed for reproducibility

            for (int i = 0; i < count; i++)
            {
                var price = basePrice + (random.NextDouble() - 0.5) * 10;
                records[i] = new PriceRecord
                {
                    DateTime = baseDate.AddDays(i),
                    Open = price - 0.5,
                    High = price + 1.0,
                    Low = price - 1.0,
                    Close = price,
                    Volume = 1000000
                };
                basePrice = price; // Random walk
            }

            return records;
        }

        private List<TradeResult> CreateTestTrades(int count)
        {
            var trades = new List<TradeResult>();
            var random = new Random(42);

            for (int i = 0; i < count; i++)
            {
                // Simulate some wins and losses
                var isWin = random.NextDouble() > 0.45; // 55% win rate
                
                var openPrice = 100.0;
                var closePrice = isWin 
                    ? openPrice + (random.NextDouble() * 4 + 0.5)  // Wins: +$0.5 to +$4.5 per share
                    : openPrice - (random.NextDouble() * 2.5 + 0.3); // Losses: -$0.3 to -$2.8 per share

                var trade = new TradeResult
                {
                    OpenPrice = openPrice,
                    ClosePrice = closePrice,
                    Position = 100, // Number of shares
                    AllowedTradeType = AllowedTradeType.Buy
                };

                // Set the position in dollars
                trade.PositionInDollars = Math.Abs(trade.Position * trade.OpenPrice);

                trades.Add(trade);
            }

            return trades;
        }

        private void SetupContextForMethod(PositionSizingMethod method)
        {
            switch (method)
            {
                case PositionSizingMethod.KellyOptimal:
                    _testContext.RecentTrades = CreateTestTrades(30);
                    break;
                    
                case PositionSizingMethod.VolatilityAdjusted:
                    _testContext.PriceHistory = CreateTestPriceHistory(25);
                    break;
                    
                case PositionSizingMethod.ATRBased:
                    _testContext.ATR = 2.5;
                    break;
                    
                case PositionSizingMethod.DrawdownProtective:
                    _testContext.MaxDrawdownFromPeak = 0.12;
                    break;
                    
                case PositionSizingMethod.MomentumAdaptive:
                    _testContext.MarketMomentum = 0.08;
                    break;
                    
                case PositionSizingMethod.MarketRegimeAdaptive:
                    _testContext.CurrentRegime = MarketRegime.HighVolatility;
                    break;
            }
        }

        #endregion
    }
}