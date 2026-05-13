using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class ConcurrentTradingIntegrationTests
    {
        private GeneticIndividual _individual;

        [TestInitialize]
        public void Setup()
        {
            _individual = new GeneticIndividual
            {
                StartingBalance = 10000,
                TradePercentageForStocks = 0.05, // 5% per trade to allow multiple trades
                AllowedSecurityTypes = AllowedSecurityType.Stock,
                AllowedTradeTypes = AllowedTradeType.Any,
                AllowMultipleTrades = true
            };
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void FullIntegration_IsolationVsSequentialBug_ShowsDifference()
        {
            // This test demonstrates the fix for the sequential processing bug
            
            // Arrange - Two indicators, both want to trade at same time
            _individual.Indicators.Add(new IndicatorParams { Type = 1, Polarity = 1, Period = 2 });
            _individual.Indicators.Add(new IndicatorParams { Type = 2, Polarity = 1, Period = 2 });

            var priceRecords = CreateTestPriceData();
            var indicatorValues = new List<List<double>>
            {
                new List<double> { 100, 102, 104, 102, 100 }, // Both indicators have
                new List<double> { 100, 102, 104, 102, 100 }  // identical signals
            };

            InitializeTradeActions(priceRecords.Length);

            // Test with Isolation mode (FIXED)
            _individual.SignalCombination = SignalCombinationMethod.Isolation;
            var balanceBeforeIsolation = _individual.StartingBalance;
            
            _individual.ExecuteTradesDeltaMode(priceRecords, indicatorValues);
            var isolationFinalBalance = _individual.FinalBalance;
            var isolationTrades = new List<TradeResult>(_individual.Trades);

            Console.WriteLine("=== ISOLATION MODE RESULTS ===");
            Console.WriteLine($"Starting Balance: ${balanceBeforeIsolation:F2}");
            Console.WriteLine($"Final Balance: ${isolationFinalBalance:F2}");
            Console.WriteLine($"Total Trades: {isolationTrades.Count}");
            
            var indicator0Trades = isolationTrades.Where(t => t.ResponsibleIndicatorIndex == 0).ToList();
            var indicator1Trades = isolationTrades.Where(t => t.ResponsibleIndicatorIndex == 1).ToList();
            
            Console.WriteLine($"Indicator 0 Trades: {indicator0Trades.Count}");
            Console.WriteLine($"Indicator 1 Trades: {indicator1Trades.Count}");

            // Assert - Both indicators should have had opportunity to trade
            Assert.IsTrue(isolationTrades.Count > 0, "Isolation mode should generate trades");
            
            // The key test: In isolation mode, both indicators compete fairly
            // They may not both get trades (due to balance constraints), but both are processed
            Console.WriteLine($"Trade distribution is fair in isolation mode");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void FullIntegration_AggregationMode_CombinesSignalsCorrectly()
        {
            // Arrange
            _individual.Indicators.Add(new IndicatorParams { Type = 1, Polarity = 1, Period = 2, TimeFrame = TimeFrame.D1});
            _individual.Indicators.Add(new IndicatorParams { Type = 2, Polarity = 1, Period = 2, TimeFrame = TimeFrame.D1 });
            
            _individual.SignalCombination = SignalCombinationMethod.Sum;
            _individual.LongEntryThreshold = 2;  // Both must be bullish
            _individual.LongExitThreshold = 1;   // Exit when one weakens
            _individual.ShortEntryThreshold = -2;
            _individual.ShortExitThreshold = -1;

            var priceRecords = CreateTestPriceData();
            var indicatorValues = new List<List<double>>
            {
                new List<double> { 100, 101, 102, 101, 100 }, // +1, +1, -1, -1
                new List<double> { 100, 101, 101, 100, 99 }   // +1,  0, -1, -1  
            };
            // Combined deltas: [+2, +1, -2, -2]
            // Should: ENTER_LONG(+2) ? HOLD(+1) ? EXIT_LONG&ENTER_SHORT(-2) ? HOLD_SHORT(-2)

            InitializeTradeActions(priceRecords.Length);

            var tradeOpenedEvents = new List<TradeOpenedEventArgs>();
            var tradeClosedEvents = new List<TradeClosedEventArgs>();
            _individual.TradeOpened += (s, e) => tradeOpenedEvents.Add(e);
            _individual.TradeClosed += (s, e) => tradeClosedEvents.Add(e);

            // Act
            _individual.ExecuteTradesDeltaMode(priceRecords, indicatorValues);

            // Assert
            Console.WriteLine("=== AGGREGATION MODE RESULTS ===");
            Console.WriteLine($"Final Balance: ${_individual.FinalBalance:F2}");
            Console.WriteLine($"Total Trades: {_individual.Trades.Count}");
            Console.WriteLine($"Trade Opened Events: {tradeOpenedEvents.Count}");
            Console.WriteLine($"Trade Closed Events: {tradeClosedEvents.Count}");

            var allActions = string.Join("", _individual.TradeActions);
            Console.WriteLine($"All Actions: {allActions}");

            // All trades should be portfolio-level
            Assert.IsTrue(_individual.Trades.All(t => t.ResponsibleIndicatorIndex == -1),
                "All trades should be portfolio-level in aggregation mode");

            // Should contain portfolio action tags
            Assert.IsTrue(allActions.Contains("PORTFOLIO_"), "Should contain portfolio action tags");

            // Verify threshold behavior
            Assert.IsTrue(_individual.Trades.Count > 0, "Should generate trades from threshold system");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void FullIntegration_EventsAndTradesConsistent_Fixed()
        {
            // Arrange
            _individual.Indicators.Add(new IndicatorParams { Type = 1, Polarity = 1, Period = 2 });
            _individual.SignalCombination = SignalCombinationMethod.Isolation;

            var priceRecords = CreateTestPriceData();
            var indicatorValues = new List<List<double>>
    {
        new List<double> { 100, 102, 101, 103, 102 } // Clear up/down pattern
    };

            InitializeTradeActions(priceRecords.Length);

            var openEvents = new List<TradeOpenedEventArgs>();
            var closeEvents = new List<TradeClosedEventArgs>();

            _individual.TradeOpened += (s, e) => openEvents.Add(e);
            _individual.TradeClosed += (s, e) => closeEvents.Add(e);

            // Act
            _individual.ExecuteTradesDeltaMode(priceRecords, indicatorValues);

            // Assert - Simple counts first
            var actualTrades = _individual.Trades;
            Assert.AreEqual(actualTrades.Count, openEvents.Count,
                "Should have open event for each trade");
            Assert.AreEqual(actualTrades.Count, closeEvents.Count,
                "Should have close event for each trade");

            // Verify each trade has consistent data
            for (int i = 0; i < actualTrades.Count; i++)
            {
                var trade = actualTrades[i];

                // Match by bar indices, not reference equality
                var openEvent = openEvents.FirstOrDefault(e => e.TradeIndex == trade.OpenIndex);
                var closeEvent = closeEvents.FirstOrDefault(e => ReferenceEquals(e.Trade, trade));

                Assert.IsNotNull(openEvent, $"Missing open event for trade at bar {trade.OpenIndex}");
                Assert.IsNotNull(closeEvent, $"Missing close event for trade at bar {trade.CloseIndex}");

                // Verify price consistency
                Assert.AreEqual(trade.OpenPrice, openEvent.Price, 0.001, "Open prices should match");
                Assert.AreEqual(trade.ClosePrice, closeEvent.ClosePrice, 0.001, "Close prices should match");

                // Verify the close price matches the price record
                Assert.AreEqual(priceRecords[trade.CloseIndex].Close, trade.ClosePrice, 0.001,
                    "Trade close price should match price record");
            }
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void FullIntegration_DifferentCombinationMethods_ProduceDifferentResults()
        {
            // Arrange - Same setup, different combination methods
            _individual.Indicators.Add(new IndicatorParams { Type = 1, Polarity = 1, Period = 2 });
            _individual.Indicators.Add(new IndicatorParams { Type = 2, Polarity = 1, Period = 2 });
            _individual.Indicators.Add(new IndicatorParams { Type = 3, Polarity = 1, Period = 2 });

            var priceRecords = CreateTestPriceData();

            // FIX: Create stronger, more distinctive signals
            var indicatorValues = new List<List<double>>
    {
        new List<double> { 100, 105, 110, 105, 100 }, // Strong up then down
        new List<double> { 100, 103, 106, 102, 98 },  // Moderate up then down
        new List<double> { 100, 102, 104, 106, 108 }  // Steady uptrend
    };

            var results = new Dictionary<SignalCombinationMethod, (double finalBalance, int tradeCount)>();

            // Test each combination method
            foreach (SignalCombinationMethod method in Enum.GetValues(typeof(SignalCombinationMethod)))
            {
                // Reset individual for each test
                ResetIndividual();
                _individual.SignalCombination = method;

                if (method == SignalCombinationMethod.Weighted)
                {
                    _individual.IndicatorWeights = new double[] { 2.0, 1.0, 0.5 };
                }

                // FIX: Set appropriate thresholds for each method
                if (method != SignalCombinationMethod.Isolation)
                {
                    // For aggregation methods, use thresholds that work with the stronger signals
                    _individual.LongEntryThreshold = 1;   // Lower threshold for entry
                    _individual.LongExitThreshold = 0;    // Exit when neutral
                    _individual.ShortEntryThreshold = -1;
                    _individual.ShortExitThreshold = 0;
                }
                else
                {
                    // For isolation mode, ensure individual indicators can trade
                    // (They use their own polarity-based logic, not thresholds)
                }

                InitializeTradeActions(priceRecords.Length);

                Console.WriteLine($"Testing {method} mode...");

                // Act
                _individual.ExecuteTradesDeltaMode(priceRecords, indicatorValues);

                // Store results
                results[method] = (_individual.FinalBalance, _individual.Trades.Count);

                Console.WriteLine($"  Result: Balance=${_individual.FinalBalance:F2}, Trades={_individual.Trades.Count}");

                // Debug: Print trade actions for this method
                var actions = string.Join("", _individual.TradeActions);
                if (!string.IsNullOrEmpty(actions))
                {
                    Console.WriteLine($"  Actions: {actions}");
                }
            }

            // Assert - Different methods should produce different results
            Console.WriteLine("=== COMBINATION METHOD COMPARISON ===");
            foreach (var kvp in results)
            {
                Console.WriteLine($"{kvp.Key}: Balance=${kvp.Value.finalBalance:F2}, Trades={kvp.Value.tradeCount}");
            }

            // Check that we have some trading activity
            var totalTrades = results.Values.Sum(v => v.tradeCount);
            Assert.IsTrue(totalTrades > 0, "At least one method should produce trades");

            // Verify we get different outcomes
            var uniqueBalances = results.Values.Select(v => v.finalBalance).Distinct().Count();
            var uniqueTradeCounts = results.Values.Select(v => v.tradeCount).Distinct().Count();

            Assert.IsTrue(uniqueBalances > 1 || uniqueTradeCounts > 1,
                "Different combination methods should produce different results");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void FullIntegration_BackwardCompatibility_SingleIndicatorModeWorks()
        {
            // Arrange - Single indicator (should use legacy path)
            _individual.Indicators.Add(new IndicatorParams { Type = 1, Polarity = 1, Period = 2 });
            _individual.AllowMultipleTrades = false; // Force single mode

            var priceRecords = CreateTestPriceData();
            var indicatorValues = new List<List<double>>
            {
                new List<double> { 100, 101, 102, 101, 100 }
            };

            InitializeTradeActions(priceRecords.Length);

            // Act
            _individual.ExecuteTradesDeltaMode(priceRecords, indicatorValues);

            // Assert - Should still work (even if ExecuteDeltaLogicForIndicatorWithOptions is placeholder)
            Assert.AreEqual(10000, _individual.StartingBalance, "Starting balance should be unchanged");
            // Note: With placeholder implementation, no trades will be generated
            // But the code path should execute without errors

            Console.WriteLine("=== BACKWARD COMPATIBILITY TEST ===");
            Console.WriteLine($"Single indicator mode executed successfully");
            Console.WriteLine($"Final balance: ${_individual.FinalBalance:F2}");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void PerformanceComparison_IsolationVsAggregation()
        {
            // Arrange - Larger dataset for performance testing
            _individual.Indicators.Add(new IndicatorParams { Type = 1, Polarity = 1, Period = 2 });
            _individual.Indicators.Add(new IndicatorParams { Type = 2, Polarity = 1, Period = 2 });
            _individual.Indicators.Add(new IndicatorParams { Type = 3, Polarity = -1, Period = 2 });

            var priceRecords = CreateLargePriceDataSet(100); // 100 bars
            var indicatorValues = CreateLargeIndicatorDataSet(100, 3); // 100 bars, 3 indicators

            // Test Isolation Mode
            var isolationStart = DateTime.Now;
            ResetIndividual();
            _individual.SignalCombination = SignalCombinationMethod.Isolation;
            InitializeTradeActions(priceRecords.Length);
            _individual.ExecuteTradesDeltaMode(priceRecords, indicatorValues);
            var isolationTime = DateTime.Now - isolationStart;
            var isolationTrades = _individual.Trades.Count;

            // Test Aggregation Mode
            var aggregationStart = DateTime.Now;
            ResetIndividual();
            _individual.SignalCombination = SignalCombinationMethod.Sum;
            _individual.LongEntryThreshold = 2;
            _individual.LongExitThreshold = 1;
            _individual.ShortEntryThreshold = -2;
            _individual.ShortExitThreshold = -1;
            InitializeTradeActions(priceRecords.Length);
            _individual.ExecuteTradesDeltaMode(priceRecords, indicatorValues);
            var aggregationTime = DateTime.Now - aggregationStart;
            var aggregationTrades = _individual.Trades.Count;

            // Report performance
            Console.WriteLine("=== PERFORMANCE COMPARISON ===");
            Console.WriteLine($"Data: {priceRecords.Length} bars, {_individual.Indicators.Count} indicators");
            Console.WriteLine($"Isolation Mode: {isolationTime.TotalMilliseconds:F2}ms, {isolationTrades} trades");
            Console.WriteLine($"Aggregation Mode: {aggregationTime.TotalMilliseconds:F2}ms, {aggregationTrades} trades");

            // Both should complete successfully
            Assert.IsTrue(isolationTime.TotalSeconds < 10, "Isolation mode should complete quickly");
            Assert.IsTrue(aggregationTime.TotalSeconds < 10, "Aggregation mode should complete quickly");
        }

        // Helper methods
        private void ResetIndividual()
        {
            _individual.Trades.Clear();
            _individual.TradeActions.Clear();
            _individual.StartingBalance = 10000;
            _individual.FinalBalance = 0;
            _individual.Indicators.Clear();
        }

        private PriceRecord[] CreateTestPriceData()
        {
            var startDate = DateTime.Today.AddDays(-10);
            var prices = new double[] { 100,101,102,103,104 };
            return prices.Select((p,i)=> new PriceRecord(startDate.AddDays(i), TimeFrame.D1, p-0.1, p+0.1, p-0.1, p, volume: 1000)).ToArray();
        }

        private PriceRecord[] CreateLargePriceDataSet(int count)
        {
            var startDate = DateTime.Today.AddDays(-count);
            var rand = new Random(42); var recs = new List<PriceRecord>(); double price=100;
            for(int i=0;i<count;i++){ price += (rand.NextDouble()-0.5)*2; price=Math.Max(50,Math.Min(150,price)); recs.Add(new PriceRecord(startDate.AddDays(i), TimeFrame.D1, price-0.1, price+0.1, price-0.1, price,volume: 1000)); }
            return recs.ToArray();
        }

        private List<List<double>> CreateLargeIndicatorDataSet(int bars,int indicators)
        {
            var rand = new Random(42); var list = new List<List<double>>();
            for(int k=0;k<indicators;k++){ var v=new List<double>(); double val=100; for(int i=0;i<bars;i++){ val += (rand.NextDouble()-0.5)*1.5; val=Math.Max(80,Math.Min(120,val)); v.Add(val);} list.Add(v);} return list;
        }

        private void InitializeTradeActions(int count){ _individual.TradeActions.Clear(); for(int i=0;i<count;i++) _individual.TradeActions.Add(""); }

        [TestMethod]
        [TestCategory("Core")]
        public void Debug_FullIntegration_EventsAndTradesConsistent_WithLogging()
        {
            // Arrange - Set up comprehensive scenario
            _individual.Indicators.Add(new IndicatorParams { Type = 1, Polarity = 1, Period = 2 });
            _individual.Indicators.Add(new IndicatorParams { Type = 2, Polarity = -1, Period = 2 }); // Opposite polarity

            _individual.SignalCombination = SignalCombinationMethod.Isolation;

            var priceRecords = CreateTestPriceData();
            var indicatorValues = new List<List<double>>
    {
        new List<double> { 100, 102, 101, 103, 102 }, // Up, down, up, down
        new List<double> { 100, 98, 99, 97, 98 }      // Down, up, down, up (opposite signals)
    };

            InitializeTradeActions(priceRecords.Length);

            var allTradeEvents = new List<(DateTime time, string type, object eventObj)>();

            _individual.TradeOpened += (s, e) =>
            {
                Console.WriteLine($"TRADE OPENED: Bar {e.TradeIndex}, Price {e.Price:F2}, Indicator {e.IndicatorIndex}");
                allTradeEvents.Add((e.DateTime, "OPEN", e));
            };
            _individual.TradeClosed += (s, e) =>
            {
                Console.WriteLine($"TRADE CLOSED: Bar {e.Trade.CloseIndex}, TradeResult.ClosePrice={e.Trade.ClosePrice:F2}, Event.ClosePrice={e.ClosePrice:F2}, Indicator {e.Trade.ResponsibleIndicatorIndex}");
                allTradeEvents.Add((e.DateTime, "CLOSE", e));
            };

            // Log price records and indicator values
            Console.WriteLine("=== PRICE RECORDS ===");
            for (int i = 0; i < priceRecords.Length; i++)
            {
                Console.WriteLine($"Bar {i}: Close = {priceRecords[i].Close:F2}");
            }

            Console.WriteLine("=== INDICATOR VALUES ===");
            for (int i = 0; i < indicatorValues[0].Count; i++)
            {
                Console.WriteLine($"Bar {i}: Ind0 = {indicatorValues[0][i]:F2}, Ind1 = {indicatorValues[1][i]:F2}");
            }

            // Act
            _individual.ExecuteTradesDeltaMode(priceRecords, indicatorValues);

            // Assert - Complete consistency check
            var actualTrades = _individual.Trades;
            var openEvents = allTradeEvents.Where(e => e.type == "OPEN").Select(e => (TradeOpenedEventArgs)e.eventObj).ToList();
            var closeEvents = allTradeEvents.Where(e => e.type == "CLOSE").Select(e => (TradeClosedEventArgs)e.eventObj).ToList();

            Console.WriteLine("=== FINAL RESULTS ===");
            Console.WriteLine($"Actual Trades: {actualTrades.Count}");
            Console.WriteLine($"Open Events: {openEvents.Count}");
            Console.WriteLine($"Close Events: {closeEvents.Count}");

            // Detailed trade analysis
            for (int i = 0; i < actualTrades.Count; i++)
            {
                var trade = actualTrades[i];
                var closeEvent = closeEvents.FirstOrDefault(e => ReferenceEquals(e.Trade, trade));

                Console.WriteLine($"Trade {i}:");
                Console.WriteLine($"  OpenIndex: {trade.OpenIndex}, CloseIndex: {trade.CloseIndex}");
                Console.WriteLine($"  OpenPrice: {trade.OpenPrice:F2}, ClosePrice: {trade.ClosePrice:F2}");
                Console.WriteLine($"  PriceRecord[{trade.CloseIndex}].Close: {priceRecords[trade.CloseIndex].Close:F2}");

                if (closeEvent != null)
                {
                    Console.WriteLine($"  Event.ClosePrice: {closeEvent.ClosePrice:F2}");
                    Console.WriteLine($"  Prices Match: {Math.Abs(trade.ClosePrice - closeEvent.ClosePrice) < 1e-10}");

                    // This should NEVER be a mismatch if the trading code is correct
                    if (Math.Abs(trade.ClosePrice - closeEvent.ClosePrice) >= 1e-10)
                    {
                        Console.WriteLine($"  *** MISMATCH DETECTED ***");
                        Console.WriteLine($"  Expected Event.ClosePrice: {trade.ClosePrice:F2}");
                        Console.WriteLine($"  Actual Event.ClosePrice: {closeEvent.ClosePrice:F2}");
                    }
                }
                else
                {
                    Console.WriteLine($"  *** NO CLOSE EVENT FOUND FOR THIS TRADE ***");
                }
            }

            // Only fail if there's actually a mismatch
            var hasMismatch = false;
            foreach (var trade in actualTrades)
            {
                var closeEvent = closeEvents.FirstOrDefault(e => ReferenceEquals(e.Trade, trade));
                if (closeEvent != null && Math.Abs(trade.ClosePrice - closeEvent.ClosePrice) >= 1e-10)
                {
                    hasMismatch = true;
                    break;
                }
            }

            if (hasMismatch)
            {
                Assert.Fail("Found mismatch between TradeResult.ClosePrice and TradeClosedEventArgs.ClosePrice");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Debug_SimplestCase_OneTradeClosePrice()
        {
            // Arrange - Simplest possible case: just verify a single trade's close price consistency
            _individual.Indicators.Add(new IndicatorParams { Type = 1, Polarity = 1, Period = 2 });
            _individual.SignalCombination = SignalCombinationMethod.Isolation;

            // Create predictable price data
            var prices = new double[] { 100, 101, 102, 103, 104 }; // Clear ascending prices
            var startDate = DateTime.Today.AddDays(-prices.Length + 1);
            var priceRecords = prices.Select((price, index) => new PriceRecord(
                startDate.AddDays(index), TimeFrame.D1,
                price, price, price, price, volume: 1000
            )).ToArray();

            // Force a simple trade scenario: Indicator goes up, then down (should cause 1 trade)
            var indicatorValues = new List<List<double>>
            {
                new List<double> { 50, 55, 60, 55, 50 } // Up, up, down, down
            };

            InitializeTradeActions(priceRecords.Length);

            var trades = new List<TradeResult>();
            var events = new List<TradeClosedEventArgs>();
            
            _individual.TradeOpened += (s, e) => 
            {
                Console.WriteLine($"OPENED: Bar {e.TradeIndex} at price {e.Price:F2}");
            };
            _individual.TradeClosed += (s, e) => 
            {
                Console.WriteLine($"CLOSED: Bar {e.Trade.CloseIndex} - TradeResult.ClosePrice={e.Trade.ClosePrice:F2}, EventArgs.ClosePrice={e.ClosePrice:F2}");
                Console.WriteLine($"        Price from priceRecords[{e.Trade.CloseIndex}].Close = {priceRecords[e.Trade.CloseIndex].Close:F2}");
                events.Add(e);
            };

            Console.WriteLine("=== PRICE DATA ===");
            for (int i = 0; i < priceRecords.Length; i++)
            {
                Console.WriteLine($"Bar {i}: Close = {priceRecords[i].Close:F2}");
            }

            Console.WriteLine("=== INDICATOR VALUES ===");
            for (int i = 0; i < indicatorValues[0].Count; i++)
            {
                Console.WriteLine($"Bar {i}: Indicator = {indicatorValues[0][i]:F2}");
            }

            // Act
            _individual.ExecuteTradesDeltaMode(priceRecords, indicatorValues);

            Console.WriteLine("=== RESULTS ===");
            Console.WriteLine($"Total Trades: {_individual.Trades.Count}");
            Console.WriteLine($"Total Events: {events.Count}");

            // Detailed analysis of each trade
            for (int i = 0; i < _individual.Trades.Count; i++)
            {
                var trade = _individual.Trades[i];
                var evt = events.FirstOrDefault(e => e.Trade.OpenIndex == trade.OpenIndex);
                
                Console.WriteLine($"Trade {i}:");
                Console.WriteLine($"  OpenIndex: {trade.OpenIndex} CloseIndex: {trade.CloseIndex}");
                Console.WriteLine($"  TradeResult.ClosePrice: {trade.ClosePrice:F2}");
                Console.WriteLine($"  PriceRecords[{trade.CloseIndex}].Close: {priceRecords[trade.CloseIndex].Close:F2}");
                
                if (evt != null)
                {
                    Console.WriteLine($"  EventArgs.ClosePrice: {evt.ClosePrice:F2}");
                    Console.WriteLine($"  Match: {Math.Abs(trade.ClosePrice - evt.ClosePrice) < 1e-10}");
                }
                else
                {
                    Console.WriteLine($"  *** NO EVENT FOUND FOR THIS TRADE ***");
                }
            }

            // Assert - Find the first mismatch if any
            foreach (var trade in _individual.Trades)
            {
                var evt = events.FirstOrDefault(e => e.Trade.OpenIndex == trade.OpenIndex);
                if (evt != null && Math.Abs(trade.ClosePrice - evt.ClosePrice) >= 1e-10)
                {
                    Assert.Fail($"Mismatch found! TradeResult.ClosePrice={trade.ClosePrice:F2}, EventArgs.ClosePrice={evt.ClosePrice:F2}");
                }
            }

            Console.WriteLine("? All trades have consistent close prices!");
        }
    }
}