using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Demonstration tests showing how to use the optimized DynamicPositionSizer features
    /// </summary>
    [TestClass]
    public class DynamicPositionSizerOptimizationDemoTests
    {
        private DynamicPositionSizer _sizer;
        private PositionSizingContext _context;

        [TestInitialize]
        public void Setup()
        {
            // Configure for optimal performance
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.VolatilityAdjusted,
                BaseRiskPerTrade = 0.02,
                MaxPositionSize = 0.25,
                MinPositionSize = 0.005,
                LookbackPeriod = 30, // Smaller for better performance
                VolatilityLookback = 20,
                EnableHeatAdjustment = true,
                EnableConcurrentPositionLimit = true
            };

            _sizer = new DynamicPositionSizer(config);
            _context = CreateOptimizedContext();
        }

        [TestMethod][TestCategory("Core")]
        public void OptimizedPositionSizing_CachingDemo()
        {
            Console.WriteLine("=== CACHING OPTIMIZATION DEMO ===");

            // First call - will calculate volatility
            PerformanceTimer.TimeAndLog("First volatility calculation", () =>
            {
                var result = _sizer.CalculatePositionSize(_context, 100.0, AllowedTradeType.Any);
                Assert.IsNotNull(result);
                Console.WriteLine($"Position Size: {result.PositionSize:P2}");
            }, ConsoleColor.Cyan);

            // Second call - should use cached volatility
            PerformanceTimer.TimeAndLog("Cached volatility calculation", () =>
            {
                var result = _sizer.CalculatePositionSize(_context, 100.0, AllowedTradeType.Any);
                Assert.IsNotNull(result);
                Console.WriteLine($"Position Size: {result.PositionSize:P2}");
            }, ConsoleColor.Green);

            // Third call with modified context - should recalculate
            _context.PriceHistory = CreatePriceHistory(25); // Different data
            PerformanceTimer.TimeAndLog("Recalculation after data change", () =>
            {
                var result = _sizer.CalculatePositionSize(_context, 100.0, AllowedTradeType.Any);
                Assert.IsNotNull(result);
                Console.WriteLine($"Position Size: {result.PositionSize:P2}");
            }, ConsoleColor.Yellow);
        }

        [TestMethod][TestCategory("Core")]
        public void OptimizedMethodComparison_PerformanceDemo()
        {
            Console.WriteLine("\n=== METHOD PERFORMANCE COMPARISON ===");

            var methods = new[]
            {
                PositionSizingMethod.FixedPercentage,
                PositionSizingMethod.KellyOptimal,
                PositionSizingMethod.VolatilityAdjusted,
                PositionSizingMethod.ATRBased,
                PositionSizingMethod.MomentumAdaptive
            };

            var config = new PositionSizingConfig();
            var results = new Dictionary<PositionSizingMethod, (double avgTime, PositionSizingResult result)>();

            foreach (var method in methods)
            {
                config.Method = method;
                var sizer = new DynamicPositionSizer(config);
                
                // Setup context appropriately for each method
                SetupContextForMethod(_context, method);

                var avgTime = PerformanceTimer.TimeActionAverage(() =>
                {
                    sizer.CalculatePositionSize(_context, 100.0, AllowedTradeType.Any);
                }, 100);

                var result = sizer.CalculatePositionSize(_context, 100.0, AllowedTradeType.Any);
                results[method] = (avgTime, result);

                Console.WriteLine($"{method,-25}: {avgTime,6:F3}ms avg, Size: {result.PositionSize:P2}");
            }

            // Find fastest method
            var fastest = results.OrderBy(r => r.Value.avgTime).First();
            Console.WriteLine($"\nFastest Method: {fastest.Key} ({fastest.Value.avgTime:F3}ms)");
        }

        [TestMethod][TestCategory("Core")]
        public void OptimizedReportGeneration_StringBuilderDemo()
        {
            Console.WriteLine("\n=== REPORT GENERATION OPTIMIZATION ===");

            var result = _sizer.CalculatePositionSize(_context, 100.0, AllowedTradeType.Any);

            // Time the optimized report generation
            string report = null;
            var elapsed = PerformanceTimer.TimeAction(() =>
            {
                report = _sizer.GenerateSizingReport(_context, result);
            });

            Console.WriteLine($"Report generated in {elapsed:F2}ms");
            Console.WriteLine($"Report length: {report.Length} characters");
            
            // Display parts of the report
            var lines = report.Split('\n');
            Console.WriteLine("\nReport Preview:");
            for (int i = 0; i < Math.Min(10, lines.Length); i++)
            {
                Console.WriteLine($"  {lines[i]}");
            }

            Assert.IsFalse(string.IsNullOrEmpty(report));
        }

        [TestMethod][TestCategory("Core")]
        public void OptimizedKellyCalculation_SinglePassDemo()
        {
            Console.WriteLine("\n=== KELLY CALCULATION OPTIMIZATION ===");

            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.KellyOptimal,
                LookbackPeriod = 50
            };

            var sizer = new DynamicPositionSizer(config);
            
            // Create context with extensive trade history
            _context.RecentTrades = CreateTradeHistory(200);

            // Time the optimized Kelly calculation
            PositionSizingResult result = null;
            var elapsed = PerformanceTimer.TimeAction(() =>
            {
                result = sizer.CalculatePositionSize(_context, 100.0, AllowedTradeType.Any);
            });

            Console.WriteLine($"Kelly calculation with 200 trades: {elapsed:F2}ms");
            Console.WriteLine($"Resulting position size: {result.PositionSize:P2}");
            Console.WriteLine($"Adjustments applied: {result.AdjustmentFactors.Count}");

            foreach (var adjustment in result.AdjustmentFactors)
            {
                Console.WriteLine($"  • {adjustment}");
            }

            Assert.IsNotNull(result);
            Assert.IsTrue(result.PositionSize > 0);
        }

        [TestMethod][TestCategory("Core")]
        public void OptimizedVolatilityCalculation_PreAllocatedArraysDemo()
        {
            Console.WriteLine("\n=== VOLATILITY CALCULATION OPTIMIZATION ===");

            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.VolatilityAdjusted,
                VolatilityLookback = 50
            };

            var sizer = new DynamicPositionSizer(config);

            // Create context with large price history
            _context.PriceHistory = CreatePriceHistory(500);

            // Time multiple calculations to show consistency
            var times = new List<double>();
            PositionSizingResult result = null;

            for (int i = 0; i < 10; i++)
            {
                var elapsed = PerformanceTimer.TimeAction(() =>
                {
                    result = sizer.CalculatePositionSize(_context, 100.0, AllowedTradeType.Any);
                });
                times.Add(elapsed);
            }

            var avgTime = times.Average();
            var minTime = times.Min();
            var maxTime = times.Max();

            Console.WriteLine($"Volatility calculations (500 price points):");
            Console.WriteLine($"  Average: {avgTime:F3}ms");
            Console.WriteLine($"  Min:     {minTime:F3}ms");
            Console.WriteLine($"  Max:     {maxTime:F3}ms");
            Console.WriteLine($"  Final position size: {result.PositionSize:P2}");

            Assert.IsNotNull(result);
            Assert.IsTrue(avgTime < 10.0, "Volatility calculation should be fast");
        }

        #region Helper Methods

        private PositionSizingContext CreateOptimizedContext()
        {
            return new PositionSizingContext
            {
                PriceHistory = CreatePriceHistory(50),
                CurrentPrice = 100.0,
                AverageVolume = 1000000,
                AccountBalance = 100000,
                AvailableBalance = 95000,
                UnrealizedPnL = 0,
                MaxDrawdownFromPeak = 0.05,
                OpenPositions = new List<TradeResult>(),
                RecentTrades = CreateTradeHistory(20),
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

        private PriceRecord[] CreatePriceHistory(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var basePrice = 100.0;
            var random = new Random(42);

            for (int i = 0; i < count; i++)
            {
                var change = (random.NextDouble() - 0.5) * 4; // ±2% daily change
                basePrice = Math.Max(50, Math.Min(200, basePrice + change));

                records[i] = new PriceRecord
                {
                    DateTime = baseDate.AddDays(i),
                    Open = basePrice - 0.5,
                    High = basePrice + 1.0,
                    Low = basePrice - 1.0,
                    Close = basePrice,
                    Volume = 1000000 + random.Next(500000)
                };
            }

            return records;
        }

        private List<TradeResult> CreateTradeHistory(int count)
        {
            var trades = new List<TradeResult>();
            var random = new Random(42);

            for (int i = 0; i < count; i++)
            {
                // Simulate realistic win/loss distribution
                var isWin = random.NextDouble() > 0.45; // 55% win rate
                
                var openPrice = 100.0;
                var closePrice = isWin 
                    ? openPrice + (random.NextDouble() * 5 + 0.5)  // Wins: +$0.5 to +$5.5 per share
                    : openPrice - (random.NextDouble() * 3 + 0.3); // Losses: -$0.3 to -$3.3 per share

                var trade = new TradeResult
                {
                    OpenPrice = openPrice,
                    ClosePrice = closePrice,
                    Position = 100, // Number of shares
                    AllowedTradeType = AllowedTradeType.Buy
                };

                // Set the position in dollars (this may affect calculated properties)
                trade.PositionInDollars = Math.Abs(trade.Position * trade.OpenPrice);

                trades.Add(trade);
            }

            return trades;
        }

        private void SetupContextForMethod(PositionSizingContext context, PositionSizingMethod method)
        {
            switch (method)
            {
                case PositionSizingMethod.KellyOptimal:
                    context.RecentTrades = CreateTradeHistory(30);
                    break;
                    
                case PositionSizingMethod.VolatilityAdjusted:
                    context.PriceHistory = CreatePriceHistory(25);
                    break;
                    
                case PositionSizingMethod.ATRBased:
                    context.ATR = 2.5;
                    break;
                    
                case PositionSizingMethod.MomentumAdaptive:
                    context.MarketMomentum = 0.08;
                    break;
            }
        }

        #endregion
    }
}