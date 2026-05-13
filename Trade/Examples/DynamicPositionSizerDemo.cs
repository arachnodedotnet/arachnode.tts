using System;
using System.Collections.Generic;
using Trade.Prices2;

namespace Trade.Examples
{
    /// <summary>
    /// Simple demonstration program for optimized DynamicPositionSizer features
    /// </summary>
    public static class DynamicPositionSizerDemo
    {
        public static void RunPerformanceDemo()
        {
            ConsoleUtilities.WriteLine("=== DYNAMIC POSITION SIZER PERFORMANCE DEMO ===");
            
            try
            {
                // Configure for optimal performance
                var config = new PositionSizingConfig
                {
                    Method = PositionSizingMethod.VolatilityAdjusted,
                    BaseRiskPerTrade = 0.02,
                    MaxPositionSize = 0.25,
                    MinPositionSize = 0.005,
                    LookbackPeriod = 30,
                    VolatilityLookback = 20,
                    EnableHeatAdjustment = true,
                    EnableConcurrentPositionLimit = true
                };

                var sizer = new DynamicPositionSizer(config);
                var context = CreateDemoContext();

                // Demo 1: Basic position sizing
                ConsoleUtilities.WriteLine("\n1. Basic Position Sizing Demo:");
                PerformanceTimer.TimeAndLog("Basic position sizing", () =>
                {
                    var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);
                    ConsoleUtilities.WriteLine($"   Position Size: {result.PositionSize:P2}");
                    ConsoleUtilities.WriteLine($"   Risk Assessment: {result.RiskAssessment}");
                    ConsoleUtilities.WriteLine($"   Adjustments: {result.AdjustmentFactors.Count}");
                }, ConsoleColor.Cyan);

                // Demo 2: Kelly Optimal with trade history
                ConsoleUtilities.WriteLine("\n2. Kelly Optimal Sizing Demo:");
                config.Method = PositionSizingMethod.KellyOptimal;
                context.RecentTrades = CreateDemoTrades(50);
                var kellySizer = new DynamicPositionSizer(config);

                PerformanceTimer.TimeAndLog("Kelly optimal sizing", () =>
                {
                    var result = kellySizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);
                    ConsoleUtilities.WriteLine($"   Kelly Position Size: {result.PositionSize:P2}");
                    foreach (var adjustment in result.AdjustmentFactors)
                    {
                        ConsoleUtilities.WriteLine($"   • {adjustment}");
                    }
                }, ConsoleColor.Green);

                // Demo 3: Performance comparison across methods
                ConsoleUtilities.WriteLine("\n3. Performance Comparison Across Methods:");
                var methods = new[]
                {
                    PositionSizingMethod.FixedPercentage,
                    PositionSizingMethod.KellyOptimal,
                    PositionSizingMethod.VolatilityAdjusted,
                    PositionSizingMethod.ATRBased,
                    PositionSizingMethod.MomentumAdaptive
                };

                foreach (var method in methods)
                {
                    config.Method = method;
                    SetupContextForMethod(context, method);
                    var methodSizer = new DynamicPositionSizer(config);

                    var avgTime = PerformanceTimer.TimeActionAverage(() =>
                    {
                        methodSizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);
                    }, 50);

                    var result = methodSizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);
                    ConsoleUtilities.WriteLine($"   {method,-25}: {avgTime,6:F3}ms avg, Size: {result.PositionSize:P2}");
                }

                // Demo 4: Report generation
                ConsoleUtilities.WriteLine("\n4. Report Generation Demo:");
                var finalResult = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);
                
                string report = null;
                PerformanceTimer.TimeAndLog("Report generation", () =>
                {
                    report = sizer.GenerateSizingReport(context, finalResult);
                }, ConsoleColor.Yellow);

                ConsoleUtilities.WriteLine($"   Report length: {report?.Length ?? 0} characters");
                
                // Display first few lines of report
                if (!string.IsNullOrEmpty(report))
                {
                    var lines = report.Split('\n');
                    ConsoleUtilities.WriteLine("   Report preview:");
                    for (int i = 0; i < Math.Min(5, lines.Length); i++)
                    {
                        ConsoleUtilities.WriteLine($"     {lines[i]}");
                    }
                }

                ConsoleUtilities.WriteLine("\n? Performance demo completed successfully!");
            }
            catch (Exception ex)
            {
                ConsoleUtilities.WriteLine($"\n? Demo failed: {ex.Message}", ConsoleColor.Red);
                ConsoleUtilities.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static PositionSizingContext CreateDemoContext()
        {
            return new PositionSizingContext
            {
                PriceHistory = CreateDemoPriceHistory(50),
                CurrentPrice = 100.0,
                AverageVolume = 1000000,
                AccountBalance = 100000,
                AvailableBalance = 95000,
                UnrealizedPnL = 0,
                MaxDrawdownFromPeak = 0.05,
                OpenPositions = new List<TradeResult>(),
                RecentTrades = new List<TradeResult>(),
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

        private static PriceRecord[] CreateDemoPriceHistory(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var basePrice = 100.0;
            var random = new Random(42);

            for (int i = 0; i < count; i++)
            {
                var change = (random.NextDouble() - 0.5) * 4; // ±2% daily change
                basePrice = Math.Max(50, Math.Min(200, basePrice + change));

                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    basePrice - 0.5,
                    basePrice + 1.0,
                    basePrice - 1.0,
                    basePrice,
                    volume: 1000000 + random.Next(500000));
            }

            return records;
        }

        private static List<TradeResult> CreateDemoTrades(int count)
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

                // Set the position in dollars
                trade.PositionInDollars = Math.Abs(trade.Position * trade.OpenPrice);

                trades.Add(trade);
            }

            return trades;
        }

        private static void SetupContextForMethod(PositionSizingContext context, PositionSizingMethod method)
        {
            switch (method)
            {
                case PositionSizingMethod.KellyOptimal:
                    context.RecentTrades = CreateDemoTrades(30);
                    break;
                    
                case PositionSizingMethod.VolatilityAdjusted:
                    context.PriceHistory = CreateDemoPriceHistory(25);
                    break;
                    
                case PositionSizingMethod.ATRBased:
                    context.ATR = 2.5;
                    break;
                    
                case PositionSizingMethod.MomentumAdaptive:
                    context.MarketMomentum = 0.08;
                    break;
            }
        }
    }
}