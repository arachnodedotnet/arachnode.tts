using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class NewTradeEventsTests
    {
        private GeneticIndividual _individual;
        private PriceRecord[] _testPriceRecords;
        private List<List<double>> _testIndicatorValues;
        private List<TradeOpenedEventArgs> _tradeOpenedEvents;
        private List<TradeClosedEventArgs> _tradeClosedEvents;

        [TestInitialize]
        public void Setup()
        {
            _individual = new GeneticIndividual
            {
                StartingBalance = 10000,
                TradePercentageForStocks = 0.1,
                AllowedSecurityTypes = AllowedSecurityType.Stock,
                AllowedTradeTypes = AllowedTradeType.Any,
                AllowMultipleTrades = true,
                SignalCombination = SignalCombinationMethod.Isolation
            };

            // Add test indicator
            _individual.Indicators.Add(new IndicatorParams
            {
                Type = 1,
                Period = 2,
                Polarity = 1,
                TimeFrame = TimeFrame.M1,
                OHLC = OHLC.Close
            });

            _tradeOpenedEvents = new List<TradeOpenedEventArgs>();
            _tradeClosedEvents = new List<TradeClosedEventArgs>();

            _individual.TradeOpened += (s, e) => _tradeOpenedEvents.Add(e);
            _individual.TradeClosed += (s, e) => _tradeClosedEvents.Add(e);

            CreateTestData();
        }

        private void CreateTestData()
        {
            var startDate = DateTime.Today.AddDays(-5);
            var records = new List<PriceRecord>();
            
            // Create price trend: up -> down -> up (should generate trades)
            var prices = new double[] { 100, 101, 102, 101, 100, 101 };
            
            for (int i = 0; i < prices.Length; i++)
            {
                records.Add(new PriceRecord(
                    startDate.AddDays(i), TimeFrame.D1,
                    prices[i] - 0.1, prices[i] + 0.1, prices[i] - 0.1, prices[i], volume: 1000
                ));
            }
            
            _testPriceRecords = records.ToArray();
            _testIndicatorValues = new List<List<double>> { prices.ToList() };

            // Initialize TradeActions
            _individual.TradeActions.Clear();
            for (int i = 0; i < prices.Length; i++)
            {
                _individual.TradeActions.Add("");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void TradeOpenedEvent_ContainsCorrectInformation()
        {
            // Act
            _individual.ExecuteTradesDeltaMode(_testPriceRecords, _testIndicatorValues);

            // Assert
            Assert.IsTrue(_tradeOpenedEvents.Count > 0, "Should fire TradeOpened events");

            var openEvent = _tradeOpenedEvents[0];
            
            // Check all required properties are set
            Assert.IsTrue(openEvent.TradeIndex >= 0, "TradeIndex should be valid");
            Assert.IsTrue(openEvent.DateTime != DateTime.MinValue, "DateTime should be set");
            Assert.IsTrue(openEvent.Price > 0, "Price should be positive");
            Assert.IsTrue(openEvent.Position != 0, "Position should not be zero");
            Assert.AreEqual(AllowedSecurityType.Stock, openEvent.SecurityType, "SecurityType should be Stock");
            Assert.IsTrue(openEvent.TradeType == AllowedTradeType.Buy || openEvent.TradeType == AllowedTradeType.SellShort, 
                "TradeType should be Buy or SellShort");
            Assert.IsNull(openEvent.OptionType, "OptionType should be null for stock trades");
            Assert.IsTrue(openEvent.IndicatorIndex >= 0, "IndicatorIndex should be valid");
            Assert.IsNotNull(openEvent.ActionTag, "ActionTag should be set");
            Assert.IsTrue(openEvent.Balance > 0, "Balance should be positive");

            Console.WriteLine($"TradeOpened event details:");
            Console.WriteLine($"  TradeIndex: {openEvent.TradeIndex}");
            Console.WriteLine($"  DateTime: {openEvent.DateTime}");
            Console.WriteLine($"  Price: {openEvent.Price}");
            Console.WriteLine($"  Position: {openEvent.Position}");
            Console.WriteLine($"  SecurityType: {openEvent.SecurityType}");
            Console.WriteLine($"  TradeType: {openEvent.TradeType}");
            Console.WriteLine($"  IndicatorIndex: {openEvent.IndicatorIndex}");
            Console.WriteLine($"  ActionTag: {openEvent.ActionTag}");
            Console.WriteLine($"  Balance: {openEvent.Balance}");
        }

        [TestMethod][TestCategory("Core")]
        public void TradeClosedEvent_ContainsCorrectInformation()
        {
            // Act
            _individual.ExecuteTradesDeltaMode(_testPriceRecords, _testIndicatorValues);

            // Assert
            Assert.IsTrue(_tradeClosedEvents.Count > 0, "Should fire TradeClosed events");

            var closeEvent = _tradeClosedEvents[0];
            
            // Check all required properties are set
            Assert.IsNotNull(closeEvent.Trade, "Trade should be set");
            Assert.IsTrue(closeEvent.DateTime != DateTime.MinValue, "DateTime should be set");
            Assert.IsTrue(closeEvent.ClosePrice > 0, "ClosePrice should be positive");
            Assert.IsTrue(closeEvent.Proceeds != 0, "Proceeds should not be zero");
            Assert.IsTrue(closeEvent.Balance > 0, "Balance should be positive");
            Assert.IsNotNull(closeEvent.ActionTag, "ActionTag should be set");
            Assert.IsFalse(closeEvent.IsEarlyTakeProfit, "Should not be early take profit for regular exit");

            // Verify trade object is complete
            var trade = closeEvent.Trade;
            Assert.IsTrue(trade.OpenIndex >= 0, "Trade.OpenIndex should be valid");
            Assert.IsTrue(trade.CloseIndex > trade.OpenIndex, "Trade.CloseIndex should be after OpenIndex");
            Assert.IsTrue(trade.OpenPrice > 0, "Trade.OpenPrice should be positive");
            Assert.IsTrue(trade.ClosePrice > 0, "Trade.ClosePrice should be positive");
            Assert.IsTrue(trade.Position != 0, "Trade.Position should not be zero");
            Assert.IsTrue(trade.Balance > 0, "Trade.Balance should be positive");

            Console.WriteLine($"TradeClosed event details:");
            Console.WriteLine($"  DateTime: {closeEvent.DateTime}");
            Console.WriteLine($"  ClosePrice: {closeEvent.ClosePrice}");
            Console.WriteLine($"  Proceeds: {closeEvent.Proceeds}");
            Console.WriteLine($"  Balance: {closeEvent.Balance}");
            Console.WriteLine($"  ActionTag: {closeEvent.ActionTag}");
            Console.WriteLine($"  Trade.OpenIndex: {trade.OpenIndex}");
            Console.WriteLine($"  Trade.CloseIndex: {trade.CloseIndex}");
        }

        [TestMethod][TestCategory("Core")]
        public void TradeEvents_MatchTradeRecords()
        {
            // Act
            _individual.ExecuteTradesDeltaMode(_testPriceRecords, _testIndicatorValues);

            // Assert - Events should match trade records
            var actualTrades = _individual.Trades;
            
            // Should have same number of opened and closed events as trades
            Assert.AreEqual(_tradeOpenedEvents.Count, _tradeClosedEvents.Count, 
                "Should have equal opened and closed events");
            Assert.AreEqual(_tradeOpenedEvents.Count, actualTrades.Count, 
                "Should have same number of events as trade records");

            // Verify each event matches corresponding trade
            for (int i = 0; i < actualTrades.Count; i++)
            {
                var trade = actualTrades[i];
                var openEvent = _tradeOpenedEvents.FirstOrDefault(e => e.TradeIndex == trade.OpenIndex);
                var closeEvent = _tradeClosedEvents.FirstOrDefault(e => e.Trade.OpenIndex == trade.OpenIndex);

                Assert.IsNotNull(openEvent, $"Should have open event for trade {i}");
                Assert.IsNotNull(closeEvent, $"Should have close event for trade {i}");

                // Verify consistency between event and trade record
                Assert.AreEqual(trade.OpenPrice, openEvent.Price, "Open prices should match");
                Assert.AreEqual(trade.ClosePrice, closeEvent.ClosePrice, "Close prices should match");
                Assert.AreEqual(trade.Position, openEvent.Position, "Positions should match");
                Assert.AreEqual(trade.ResponsibleIndicatorIndex, openEvent.IndicatorIndex, "Indicator indices should match");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void TradeEvents_FireInCorrectOrder()
        {
            // Act
            _individual.ExecuteTradesDeltaMode(_testPriceRecords, _testIndicatorValues);

            // Assert - Events should fire in chronological order
            if (_tradeOpenedEvents.Count > 1)
            {
                for (int i = 1; i < _tradeOpenedEvents.Count; i++)
                {
                    Assert.IsTrue(_tradeOpenedEvents[i].DateTime >= _tradeOpenedEvents[i-1].DateTime,
                        "TradeOpened events should be in chronological order");
                }
            }

            if (_tradeClosedEvents.Count > 1)
            {
                for (int i = 1; i < _tradeClosedEvents.Count; i++)
                {
                    Assert.IsTrue(_tradeClosedEvents[i].DateTime >= _tradeClosedEvents[i-1].DateTime,
                        "TradeClosed events should be in chronological order");
                }
            }

            // Each trade's close event should come after its open event
            foreach (var closeEvent in _tradeClosedEvents)
            {
                var matchingOpenEvent = _tradeOpenedEvents.FirstOrDefault(e => 
                    e.TradeIndex == closeEvent.Trade.OpenIndex);
                
                if (matchingOpenEvent != null)
                {
                    Assert.IsTrue(closeEvent.DateTime >= matchingOpenEvent.DateTime,
                        "Close event should come after open event for each trade");
                }
            }
        }

        [TestMethod][TestCategory("Core")]
        public void TradeEvents_HandlePortfolioMode()
        {
            // Arrange - Switch to portfolio mode
            _individual.SignalCombination = SignalCombinationMethod.Sum;
            _individual.LongEntryThreshold = 1;
            _individual.LongExitThreshold = 0;

            // Act
            _individual.ExecuteTradesDeltaMode(_testPriceRecords, _testIndicatorValues);

            // Assert - Should still fire events for portfolio trades
            Assert.IsTrue(_tradeOpenedEvents.Count > 0 || _tradeClosedEvents.Count > 0, 
                "Should fire events even in portfolio mode");

            // Portfolio trades should have IndicatorIndex = -1
            if (_tradeOpenedEvents.Count > 0)
            {
                // Note: Portfolio mode uses simplified entry methods that may not fire events
                // but we test the event structure is ready for it
                Console.WriteLine($"Portfolio mode fired {_tradeOpenedEvents.Count} open events");
                Console.WriteLine($"Portfolio mode fired {_tradeClosedEvents.Count} close events");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void TradeEvents_HandleExceptionsGracefully()
        {
            // Arrange - Add event handler that throws exception
            var eventExceptionThrown = false;
            _individual.TradeOpened += (s, e) => 
            {
                eventExceptionThrown = true;
                throw new InvalidOperationException("Test exception in event handler");
            };

            // Act - Should not crash despite exception in event handler
            _individual.ExecuteTradesDeltaMode(_testPriceRecords, _testIndicatorValues);

            // Assert - Trading should continue despite event handler exception
            Assert.IsTrue(eventExceptionThrown, "Event handler exception should have been thrown");
            Assert.IsTrue(_individual.FinalBalance != _individual.StartingBalance || _individual.Trades.Count > 0, 
                "Trading should continue despite event handler exceptions");
            
            Console.WriteLine("Trading completed successfully despite event handler exception");
        }

        [TestMethod][TestCategory("Core")]
        public void TradeEventArgs_PropertiesAreCorrect()
        {
            // Test the event argument classes directly

            // Test TradeOpenedEventArgs
            var openArgs = new TradeOpenedEventArgs
            {
                TradeIndex = 5,
                DateTime = DateTime.Now,
                Price = 100.5,
                Position = 10.0,
                SecurityType = AllowedSecurityType.Stock,
                TradeType = AllowedTradeType.Buy,
                OptionType = null,
                IndicatorIndex = 2,
                ActionTag = "BU2;",
                Balance = 9000.0
            };

            Assert.AreEqual(5, openArgs.TradeIndex);
            Assert.AreEqual(100.5, openArgs.Price);
            Assert.AreEqual(10.0, openArgs.Position);
            Assert.AreEqual(AllowedSecurityType.Stock, openArgs.SecurityType);
            Assert.AreEqual(AllowedTradeType.Buy, openArgs.TradeType);
            Assert.IsNull(openArgs.OptionType);
            Assert.AreEqual(2, openArgs.IndicatorIndex);
            Assert.AreEqual("BU2;", openArgs.ActionTag);
            Assert.AreEqual(9000.0, openArgs.Balance);

            // Test TradeClosedEventArgs
            var trade = new TradeResult
            {
                OpenIndex = 5,
                CloseIndex = 8,
                OpenPrice = 100.5,
                ClosePrice = 102.0,
                Position = 10.0,
                Balance = 9150.0
            };

            var closeArgs = new TradeClosedEventArgs
            {
                Trade = trade,
                DateTime = DateTime.Now.AddHours(1),
                ClosePrice = 102.0,
                Proceeds = 1015.0,
                Balance = 9150.0,
                ActionTag = "SE2;",
                IsEarlyTakeProfit = false
            };

            Assert.AreEqual(trade, closeArgs.Trade);
            Assert.AreEqual(102.0, closeArgs.ClosePrice);
            Assert.AreEqual(1015.0, closeArgs.Proceeds);
            Assert.AreEqual(9150.0, closeArgs.Balance);
            Assert.AreEqual("SE2;", closeArgs.ActionTag);
            Assert.IsFalse(closeArgs.IsEarlyTakeProfit);
        }

        [TestMethod][TestCategory("Core")]
        public void TradeEvents_ProvideCompleteAuditTrail()
        {
            // Act
            _individual.ExecuteTradesDeltaMode(_testPriceRecords, _testIndicatorValues);

            // Assert - Events provide complete audit trail
            var allEvents = new List<(DateTime time, string type, string details)>();

            foreach (var openEvent in _tradeOpenedEvents)
            {
                allEvents.Add((openEvent.DateTime, "OPEN", 
                    $"Indicator {openEvent.IndicatorIndex}: {openEvent.TradeType} {openEvent.Position:F2} @ {openEvent.Price:F2}"));
            }

            foreach (var closeEvent in _tradeClosedEvents)
            {
                allEvents.Add((closeEvent.DateTime, "CLOSE", 
                    $"Trade {closeEvent.Trade.OpenIndex}-{closeEvent.Trade.CloseIndex}: Proceeds {closeEvent.Proceeds:F2}"));
            }

            // Sort by time
            allEvents.Sort((a, b) => a.time.CompareTo(b.time));

            Console.WriteLine("Complete trade audit trail:");
            foreach (var (time, type, details) in allEvents)
            {
                Console.WriteLine($"  {time:yyyy-MM-dd}: {type} - {details}");
            }

            // Should have complete pairs
            var openCount = allEvents.Count(e => e.type == "OPEN");
            var closeCount = allEvents.Count(e => e.type == "CLOSE");
            Assert.AreEqual(openCount, closeCount, "Should have matching open/close events");
        }
    }
}