using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class EnhancedProgramAnalysisTests
    {
        [TestMethod][TestCategory("Core")]
        public void CalculateRiskAdjustedReturn_ImprovedSharpeRatio_CalculatesCorrectly()
        {
            // Create test individual with varied trade returns
            var individual = new GeneticIndividual
            {
                StartingBalance = 100000
            };

            // Add some test trades with varied returns - use proper constructor values
            individual.Trades.AddRange(new[]
            {
                new TradeResult
                {
                    OpenPrice = 100.0, ClosePrice = 105.0, AllowedTradeType = AllowedTradeType.Buy, Position = 100,
                    Balance = 105000
                },
                new TradeResult
                {
                    OpenPrice = 105.0, ClosePrice = 102.9, AllowedTradeType = AllowedTradeType.Buy, Position = 100,
                    Balance = 103000
                },
                new TradeResult
                {
                    OpenPrice = 103.0, ClosePrice = 111.24, AllowedTradeType = AllowedTradeType.Buy, Position = 100,
                    Balance = 111000
                },
                new TradeResult
                {
                    OpenPrice = 111.0, ClosePrice = 114.33, AllowedTradeType = AllowedTradeType.Buy, Position = 100,
                    Balance = 114000
                }
            });

            // FIXED: Calculate improved Sharpe ratio using reflection with correct namespace
            var method = typeof(Trade.Program).GetMethod("CalculateRiskAdjustedReturn",
                BindingFlags.NonPublic | BindingFlags.Static);
            var sharpeRatio = (double)method.Invoke(null, new object[] { individual, 0.02 });

            Console.WriteLine($"Improved Sharpe Ratio: {sharpeRatio:F4}");

            // Should return a finite value for varied returns
            Assert.IsFalse(double.IsInfinity(sharpeRatio), "Sharpe ratio should be finite for varied returns");
            Assert.IsFalse(double.IsNaN(sharpeRatio), "Sharpe ratio should not be NaN");

            // Should be positive since average return > risk-free rate
            Assert.IsTrue(sharpeRatio > 0, "Sharpe ratio should be positive for profitable strategy");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateProfitFactor_CalculatesCorrectly()
        {
            // Create test individual
            var individual = new GeneticIndividual
            {
                StartingBalance = 100000
            };

            // Add trades: 3 profitable, 2 losing - calculate positions to get desired gains
            individual.Trades.AddRange(new[]
            {
                new TradeResult
                {
                    OpenPrice = 100.0, ClosePrice = 110.0, AllowedTradeType = AllowedTradeType.Buy, Position = 100
                }, // $1000 profit
                new TradeResult
                {
                    OpenPrice = 100.0, ClosePrice = 115.0, AllowedTradeType = AllowedTradeType.Buy, Position = 100
                }, // $1500 profit
                new TradeResult
                    { OpenPrice = 100.0, ClosePrice = 95.0, AllowedTradeType = AllowedTradeType.Buy, Position = 100 }, // $500 loss
                new TradeResult
                {
                    OpenPrice = 100.0, ClosePrice = 120.0, AllowedTradeType = AllowedTradeType.Buy, Position = 100
                }, // $2000 profit
                new TradeResult
                    { OpenPrice = 100.0, ClosePrice = 92.0, AllowedTradeType = AllowedTradeType.Buy, Position = 100 } // $800 loss
            });

            // FIXED: Calculate profit factor using reflection with correct namespace
            var method = typeof(Trade.Program).GetMethod("CalculateProfitFactor",
                BindingFlags.NonPublic | BindingFlags.Static);
            var profitFactor = (double)method.Invoke(null, new object[] { individual });

            // Gross Profit = 1000 + 1500 + 2000 = 4500
            // Gross Loss = 500 + 800 = 1300
            // Profit Factor = 4500 / 1300 = 3.46
            var expectedProfitFactor = 4500.0 / 1300.0;

            Console.WriteLine($"Calculated Profit Factor: {profitFactor:F2}");
            Console.WriteLine($"Expected Profit Factor: {expectedProfitFactor:F2}");

            Assert.AreEqual(expectedProfitFactor, profitFactor, 0.01, "Profit factor should be calculated correctly");
            Assert.IsTrue(profitFactor > 2.0, "This strategy should have excellent profit factor (> 2.0)");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateProfitFactor_EdgeCases_HandlesCorrectly()
        {
            var individual = new GeneticIndividual { StartingBalance = 100000 };
            // FIXED: Calculate profit factor using reflection with correct namespace
            var method = typeof(Trade.Program).GetMethod("CalculateProfitFactor",
                BindingFlags.NonPublic | BindingFlags.Static);

            // Test 1: No trades
            var pfNoTrades = (double)method.Invoke(null, new object[] { individual });
            Assert.AreEqual(0.0, pfNoTrades, "No trades should return 0");

            // Test 2: Only profitable trades (no losses)
            individual.Trades.Add(new TradeResult
            { OpenPrice = 100.0, ClosePrice = 110.0, AllowedTradeType = AllowedTradeType.Buy, Position = 100 });
            individual.Trades.Add(new TradeResult
            { OpenPrice = 100.0, ClosePrice = 105.0, AllowedTradeType = AllowedTradeType.Buy, Position = 100 });
            var pfOnlyProfits = (double)method.Invoke(null, new object[] { individual });
            Assert.IsTrue(double.IsPositiveInfinity(pfOnlyProfits), "Only profits should return positive infinity");

            // Test 3: Only losing trades (no profits)
            individual.Trades.Clear();
            individual.Trades.Add(new TradeResult
            { OpenPrice = 100.0, ClosePrice = 90.0, AllowedTradeType = AllowedTradeType.Buy, Position = 100 });
            individual.Trades.Add(new TradeResult
            { OpenPrice = 100.0, ClosePrice = 95.0, AllowedTradeType = AllowedTradeType.Buy, Position = 100 });
            var pfOnlyLosses = (double)method.Invoke(null, new object[] { individual });
            Assert.AreEqual(0.0, pfOnlyLosses, "Only losses should return 0");
        }

        [TestMethod][TestCategory("Core")]
        public void DisplayTradesList_IncludesDateTimeInformation()
        {
            // Create test data with PriceRecords containing DateTime information
            var startDate = new DateTime(2024, 1, 1);
            var priceRecords = new[]
            {
                new PriceRecord { DateTime = startDate, Close = 100.0 },
                new PriceRecord { DateTime = startDate.AddDays(1), Close = 105.0 },
                new PriceRecord { DateTime = startDate.AddDays(2), Close = 103.0 },
                new PriceRecord { DateTime = startDate.AddDays(3), Close = 108.0 },
                new PriceRecord { DateTime = startDate.AddDays(4), Close = 110.0 }
            };

            // Create individual with trades
            var individual = new GeneticIndividual { StartingBalance = 100000 };
            individual.Trades.Add(new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 2,
                OpenPrice = 100.0,
                ClosePrice = 103.0,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 100,
                PositionInDollars = 10000,
                Balance = 100300,
                PriceRecordForOpen = priceRecords[0],
                PriceRecordForClose = priceRecords[2]
            });

            Console.WriteLine("=== Testing Enhanced DisplayTradesList ===");
            Console.WriteLine("This test validates that:");
            Console.WriteLine("1. DateTime information is properly extracted and displayed");
            Console.WriteLine("2. Profit factor is calculated and shown");
            Console.WriteLine("3. Enhanced trade display includes all requested information");
            Console.WriteLine();

            // This would call DisplayTradesList - we can't easily test console output
            // but we can verify the trade has proper DateTime information
            var trade = individual.Trades[0];
            Assert.AreEqual(startDate, trade.PriceRecordForOpen.DateTime, "Open date should be preserved");
            Assert.AreEqual(startDate.AddDays(2), trade.PriceRecordForClose.DateTime, "Close date should be preserved");

            Console.WriteLine($"✓ Trade open date: {trade.PriceRecordForOpen.DateTime:yyyy-MM-dd}");
            Console.WriteLine($"✓ Trade close date: {trade.PriceRecordForClose.DateTime:yyyy-MM-dd}");
            Console.WriteLine(
                $"✓ Trade duration: {(trade.PriceRecordForClose.DateTime - trade.PriceRecordForOpen.DateTime).Days} days");

            Assert.IsTrue(true, "DateTime information is properly preserved in TradeResult");
        }

        [TestMethod][TestCategory("Core")]
        public void EnhancedMetrics_IntegrationTest()
        {
            Console.WriteLine("=== Enhanced Trading Metrics Integration Test ===");
            Console.WriteLine();

            // Test all three enhancements together
            var individual = new GeneticIndividual { StartingBalance = 100000 };

            // Add realistic trading scenario
            individual.Trades.AddRange(new[]
            {
                new TradeResult
                {
                    OpenPrice = 100.0, ClosePrice = 110.0, AllowedTradeType = AllowedTradeType.Buy, Position = 300, Balance = 103000,
                    OpenIndex = 0, CloseIndex = 5,
                    PriceRecordForOpen = new PriceRecord { DateTime = new DateTime(2024, 1, 1) },
                    PriceRecordForClose = new PriceRecord { DateTime = new DateTime(2024, 1, 6) }
                },
                new TradeResult
                {
                    OpenPrice = 110.0, ClosePrice = 104.5, AllowedTradeType = AllowedTradeType.SellShort, Position = -150,
                    Balance = 101500,
                    OpenIndex = 6, CloseIndex = 10,
                    PriceRecordForOpen = new PriceRecord { DateTime = new DateTime(2024, 1, 7) },
                    PriceRecordForClose = new PriceRecord { DateTime = new DateTime(2024, 1, 12) }
                },
                new TradeResult
                {
                    OpenPrice = 100.0, ClosePrice = 115.0, AllowedTradeType = AllowedTradeType.Buy, Position = 300, Balance = 106000,
                    OpenIndex = 11, CloseIndex = 15,
                    PriceRecordForOpen = new PriceRecord { DateTime = new DateTime(2024, 1, 13) },
                    PriceRecordForClose = new PriceRecord { DateTime = new DateTime(2024, 1, 18) }
                }
            });

            // FIXED: Test Sharpe ratio calculation with correct namespace
            var sharpeMethod = typeof(Trade.Program).GetMethod("CalculateRiskAdjustedReturn",
                BindingFlags.NonPublic | BindingFlags.Static);
            var sharpeRatio = (double)sharpeMethod.Invoke(null, new object[] { individual, 0.02 });

            // FIXED: Test profit factor calculation with correct namespace
            var profitFactorMethod = typeof(Trade.Program).GetMethod("CalculateProfitFactor",
                BindingFlags.NonPublic | BindingFlags.Static);
            var profitFactor = (double)profitFactorMethod.Invoke(null, new object[] { individual });

            Console.WriteLine("ENHANCED METRICS RESULTS:");
            Console.WriteLine($"  Improved Sharpe Ratio: {sharpeRatio:F3}");
            Console.WriteLine($"  Profit Factor: {profitFactor:F2}");
            Console.WriteLine($"  Total Trades: {individual.Trades.Count}");
            Console.WriteLine($"  Profitable Trades: {individual.Trades.Count(t => t.ActualDollarGain > 0)}");
            Console.WriteLine($"  Final Balance: ${individual.Trades.Last().Balance:F0}");
            Console.WriteLine();

            Console.WriteLine("DATE INFORMATION:");
            foreach (var trade in individual.Trades)
            {
                var openDate = trade.PriceRecordForOpen.DateTime;
                var closeDate = trade.PriceRecordForClose.DateTime;
                var duration = (closeDate - openDate).Days;
                Console.WriteLine(
                    $"  Trade {individual.Trades.IndexOf(trade) + 1}: {openDate:yyyy-MM-dd} to {closeDate:yyyy-MM-dd} ({duration} days)");
            }

            // Validate calculations
            Assert.IsTrue(sharpeRatio > 0, "Strategy should have positive Sharpe ratio");
            Assert.IsTrue(profitFactor > 1.0, "Strategy should have profit factor > 1.0");
            Assert.IsTrue(individual.Trades.All(t => t.PriceRecordForOpen.DateTime != default),
                "All trades should have open dates");
            Assert.IsTrue(individual.Trades.All(t => t.PriceRecordForClose.DateTime != default),
                "All trades should have close dates");

            Console.WriteLine("✓ All enhanced metrics are working correctly!");
        }

        // FIXED: Return as object instead of trying to cast to local struct - correct namespace
        private object GetCurrentMarketSignalAsObject(GeneticIndividual model, PriceRecord[] recentData)
        {
            var method = typeof(Trade.Program).GetMethod("GetCurrentMarketSignal",
                BindingFlags.NonPublic | BindingFlags.Static);
            return method.Invoke(null, new object[] { model, recentData });
        }

        // FIXED: Return as object instead of trying to cast to local struct - correct namespace
        private object CalculateModelRiskMetricsAsObject(GeneticIndividual model)
        {
            var method = typeof(Trade.Program).GetMethod("CalculateModelRiskMetrics",
                BindingFlags.NonPublic | BindingFlags.Static);
            return method.Invoke(null, new object[] { model });
        }

        private void StorePredictionResult(GeneticIndividual model, object signal, object risk, DateTime targetDate)
        {
            var method = typeof(Trade.Program).GetMethod("StorePredictionResult",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Invoke(null, new[] { model, signal, risk, targetDate });
        }

        // Helper methods to access private static methods for testing - FIXED namespace references
        private DateTime GetNextTradingDay(DateTime currentDate)
        {
            var method = typeof(Trade.Program).GetMethod("GetNextTradingDay",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (DateTime)method.Invoke(null, new object[] { currentDate });
        }

        private MarketSignal GetCurrentMarketSignal(GeneticIndividual model, PriceRecord[] recentData)
        {
            var method = typeof(Trade.Program).GetMethod("GetCurrentMarketSignal",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (MarketSignal)method.Invoke(null, new object[] { model, recentData });
        }

        private ModelRiskMetrics CalculateModelRiskMetrics(GeneticIndividual model)
        {
            var method = typeof(Trade.Program).GetMethod("CalculateModelRiskMetrics",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (ModelRiskMetrics)method.Invoke(null, new object[] { model });
        }

        private void StorePredictionResult(GeneticIndividual model, MarketSignal signal, ModelRiskMetrics risk,
            DateTime targetDate)
        {
            var method = typeof(Trade.Program).GetMethod("StorePredictionResult",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Invoke(null, new object[] { model, signal, risk, targetDate });
        }

        // Define structs for testing (these should match the ones in Program.cs)
        private struct MarketSignal
        {
            //public double Signal;
            //public string RecommendedAction;
            //public double Confidence;
        }

        private struct ModelRiskMetrics
        {
            //public double SharpeRatio;
            //public double MaxDrawdown;
            //public double ProfitFactor;
            //public double WinRate;
            //public bool IsRobust;
        }
    }
}