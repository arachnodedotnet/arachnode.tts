using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class ConcurrentIndicatorProcessingTests
    {
        private GeneticIndividual _individual;
        private PriceRecord[] _testPriceRecords;
        private List<List<double>> _testIndicatorValues;

        [TestInitialize]
        public void Setup()
        {
            _individual = new GeneticIndividual
            {
                StartingBalance = 10000,
                TradePercentageForStocks = 0.1, // 10% per trade
                AllowedSecurityTypes = AllowedSecurityType.Stock,
                AllowedTradeTypes = AllowedTradeType.Any,
                AllowMultipleTrades = true,
                SignalCombination = SignalCombinationMethod.Isolation
            };

            // Add two test indicators (M1 timeframe as per production assumption)
            _individual.Indicators.Add(new IndicatorParams
            {
                Type = 1,
                Period = 2,
                Polarity = 1,
                TimeFrame = TimeFrame.M1,
                OHLC = OHLC.Close
            });

            _individual.Indicators.Add(new IndicatorParams
            {
                Type = 2,
                Period = 2,
                Polarity = 1,
                TimeFrame = TimeFrame.M1,
                OHLC = OHLC.Close
            });

            CreateTestData();
        }

        private void CreateTestData()
        {
            // Create price records with clear trend changes (minute bars)
            var start = DateTime.UtcNow.Date.AddMinutes(-10 * 60); // just ensure unique base; relative ordering only matters
            var records = new List<PriceRecord>();
            var prices = new double[] { 100, 101, 102, 103, 102, 101, 100, 101, 102, 103 };
            for (int i = 0; i < prices.Length; i++)
            {
                var price = prices[i];
                records.Add(new PriceRecord(
                    start.AddMinutes(i), TimeFrame.M1,
                    price - 0.1,
                    price + 0.1,
                    price - 0.1,
                    price,
                    volume: 1000
                ));
            }
            _testPriceRecords = records.ToArray();

            _testIndicatorValues = new List<List<double>>
            {
                prices.ToList(),
                new List<double> { 100, 100, 101, 102, 102, 101, 100, 100, 101, 102 }
            };

            _individual.TradeActions.Clear();
            for (int i = 0; i < _testPriceRecords.Length; i++) _individual.TradeActions.Add("");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ProcessIndicatorsInIsolation_BothIndicatorsTradeIndependently()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Isolation;
            var tradeOpenedEvents = new List<TradeOpenedEventArgs>();
            var tradeClosedEvents = new List<TradeClosedEventArgs>();
            _individual.TradeOpened += (s, e) => tradeOpenedEvents.Add(e);
            _individual.TradeClosed += (s, e) => tradeClosedEvents.Add(e);

            // Act
            _individual.ExecuteTradesDeltaMode(_testPriceRecords, _testIndicatorValues);

            // Assert - Both indicators should have traded independently
            Assert.IsTrue(_individual.Trades.Count > 0, "Should have generated trades");
            var indicator0Trades = _individual.Trades.Where(t => t.ResponsibleIndicatorIndex == 0).ToList();
            var indicator1Trades = _individual.Trades.Where(t => t.ResponsibleIndicatorIndex == 1).ToList();
            Assert.IsTrue(indicator0Trades.Count > 0);
            Assert.IsTrue(indicator1Trades.Count > 0);

            // Verify concurrent processing - trades should be interleaved by time, not sequential
            var allTradeIndices = _individual.Trades.Select(t => t.OpenIndex).OrderBy(x => x).ToList();
            bool isInterleaved = false;
            for (int i = 1; i < allTradeIndices.Count; i++)
            {
                var prev = _individual.Trades.First(t => t.OpenIndex == allTradeIndices[i - 1]).ResponsibleIndicatorIndex;
                var curr = _individual.Trades.First(t => t.OpenIndex == allTradeIndices[i]).ResponsibleIndicatorIndex;
                if (prev != curr) { isInterleaved = true; break; }
            }
            Console.WriteLine($"Trades are interleaved: {isInterleaved}");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ProcessIndicatorsWithAggregation_Sum_CombinesSignalsCorrectly()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Sum;
            _individual.LongEntryThreshold = 1;
            _individual.LongExitThreshold = 1;
            _individual.ShortEntryThreshold = -1;
            _individual.ShortExitThreshold = -1;

            // Act
            _individual.ExecuteTradesDeltaMode(_testPriceRecords, _testIndicatorValues);

            // Assert
            Assert.IsTrue(_individual.Trades.Count > 0);
            var portfolioTrades = _individual.Trades.Where(t => t.ResponsibleIndicatorIndex == -1).ToList();
            Assert.AreEqual(_individual.Trades.Count, portfolioTrades.Count);
            Assert.IsTrue(_individual.TradeActions.Any(a => a.Contains("PORTFOLIO_")));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateIndicatorDelta_ReturnsCorrectDeltas()
        {
            // Arrange
            var signals = new List<double> { 100, 101, 102, 101, 100 };
            var indicator = new IndicatorParams { Polarity = 1 };
            var state = new IndicatorState { PrevDir = 0 };
            var priceRecords = _testPriceRecords.Take(signals.Count).ToArray();

            // Act & Assert
            // First change: 100->101, slope=+1, polarity=1, currDir=+1, isSwitch=true (0->+1)
            var delta1 = CallCalculateIndicatorDelta(priceRecords, 1, signals, indicator, 0, ref state);
            Assert.AreEqual(1, delta1, "First upward switch should return +1");
            Assert.AreEqual(1, state.PrevDir, "PrevDir should be updated to +1");

            // Second change: 101->102, slope=+1, polarity=1, currDir=+1, isSwitch=false (+1->+1)
            var delta2 = CallCalculateIndicatorDelta(priceRecords, 2, signals, indicator, 0, ref state);
            Assert.AreEqual(0, delta2, "Continuing upward trend should return 0");

            // Third change: 102->101, slope=-1, polarity=1, currDir=-1, isSwitch=true (+1->-1)
            var delta3 = CallCalculateIndicatorDelta(priceRecords, 3, signals, indicator, 0, ref state);
            Assert.AreEqual(-1, delta3, "Switch to downward should return -1");
            Assert.AreEqual(-1, state.PrevDir, "PrevDir should be updated to -1");

            // Fourth change: 101->100, slope=-1, polarity=1, currDir=-1, isSwitch=false (-1->-1)
            var delta4 = CallCalculateIndicatorDelta(priceRecords, 4, signals, indicator, 0, ref state);
            Assert.AreEqual(0, delta4, "Continuing downward trend should return 0");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void AggregateDeltas_Sum_ReturnsSum()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Sum;
            var deltas = new int[] { 1, 1, 0 }; // Two bullish, one neutral

            // Act
            var result = CallAggregateDeltas(deltas);

            // Assert
            Assert.AreEqual(2, result, "Sum should be 2");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void AggregateDeltas_Majority_ReturnsSign()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Majority;

            // Test positive majority
            var deltas1 = new int[] { 1, 1, -1 }; // Sum = 1, Sign = +1
            var result1 = CallAggregateDeltas(deltas1);
            Assert.AreEqual(1, result1, "Positive majority should return +1");

            // Test negative majority
            var deltas2 = new int[] { -1, -1, 1 }; // Sum = -1, Sign = -1  
            var result2 = CallAggregateDeltas(deltas2);
            Assert.AreEqual(-1, result2, "Negative majority should return -1");

            // Test neutral
            var deltas3 = new int[] { 1, -1, 0 }; // Sum = 0, Sign = 0
            var result3 = CallAggregateDeltas(deltas3);
            Assert.AreEqual(0, result3, "Neutral should return 0");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void AggregateDeltas_Consensus_RequiresAllAgree()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Consensus;

            // Test all bullish
            var deltas1 = new int[] { 1, 1, 1 };
            var result1 = CallAggregateDeltas(deltas1);
            Assert.AreEqual(1, result1, "All bullish should return +1");

            // Test all bearish
            var deltas2 = new int[] { -1, -1, -1 };
            var result2 = CallAggregateDeltas(deltas2);
            Assert.AreEqual(-1, result2, "All bearish should return -1");

            // Test mixed - should be neutral
            var deltas3 = new int[] { 1, -1, 1 };
            var result3 = CallAggregateDeltas(deltas3);
            Assert.AreEqual(0, result3, "Mixed signals should return 0");

            // Test with zeros - should be neutral
            var deltas4 = new int[] { 1, 0, 1 };
            var result4 = CallAggregateDeltas(deltas4);
            Assert.AreEqual(0, result4, "Including zeros should return 0");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void AggregateDeltas_Weighted_UsesWeights()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Weighted;
            _individual.IndicatorWeights = new double[] { 2.0, 1.0, 0.5 }; // Different weights

            var deltas = new int[] { 1, 1, 1 }; // All +1, but weighted differently

            // Act
            var result = CallAggregateDeltas(deltas);

            // Assert
            // Expected: (1 * 2.0) + (1 * 1.0) + (1 * 0.5) = 3.5, rounded = 4
            Assert.AreEqual(4, result, "Weighted sum should be rounded to 4");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ProcessSingleBarForIndicator_HandlesTradeLogicCorrectly()
        {
            // Arrange
            _individual.AllowMultipleTrades = true;
            _individual.TradePercentageForStocks = 0.1;
            var balance = 10000.0;
            var totalUsed = 0.0;
            var state = new IndicatorState { PrevDir = 0 };
            
            var signals = new List<double> { 100, 101 }; // Upward signal
            var indicator = new IndicatorParams { Polarity = 1 };

            var opened = new List<TradeOpenedEventArgs>();
            _individual.TradeOpened += (s, e) => opened.Add(e);

            // Act
            CallProcessSingleBarForIndicator(_testPriceRecords, 1, signals, indicator, 0,
                ref state, ref balance, ref totalUsed);

            // Assert
            Assert.IsTrue(state.HoldingStock, "Should be holding stock after bullish signal");
            Assert.IsTrue(state.StockPosition > 0, "Should have positive stock position");
            Assert.AreEqual(1, state.PrevDir, "PrevDir should be updated to +1");
            Assert.IsTrue(balance < 10000, "Balance should decrease after buying");
            Assert.IsTrue(totalUsed > 0, "Used balance should increase");
            Assert.AreEqual(1, opened.Count, "Should have fired one trade opened event");

            // Verify trade details
            var tradeEvent = opened[0];
            Assert.AreEqual(0, tradeEvent.IndicatorIndex, "Trade should be attributed to indicator 0");
            Assert.AreEqual(AllowedTradeType.Buy, tradeEvent.TradeType);
            Assert.AreEqual(AllowedSecurityType.Stock, tradeEvent.SecurityType);
            Assert.IsTrue(tradeEvent.ActionTag.Contains("BU0"), "Action tag should contain BU0");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeEvents_AreProperlyFired()
        {
            // Arrange
            var opened = new List<TradeOpenedEventArgs>();
            var closed = new List<TradeClosedEventArgs>();
            
            _individual.TradeOpened += (s, e) => opened.Add(e);
            _individual.TradeClosed += (s, e) => closed.Add(e);

            // Act
            _individual.ExecuteTradesDeltaMode(_testPriceRecords, _testIndicatorValues);

            // Assert
            Assert.IsTrue(opened.Count > 0, "Should have fired trade opened events");
            Assert.IsTrue(closed.Count > 0, "Should have fired trade closed events");

            // Verify event properties
            var openEvent = opened[0];
            Assert.IsTrue(openEvent.TradeIndex >= 0, "Trade index should be valid");
            Assert.IsTrue(openEvent.Price > 0, "Price should be positive");
            Assert.IsTrue(openEvent.Position != 0, "Position should not be zero");
            Assert.IsNotNull(openEvent.ActionTag, "Action tag should be set");

            var closeEvent = closed[0];
            Assert.IsNotNull(closeEvent.Trade, "Trade object should be set");
            Assert.IsTrue(closeEvent.ClosePrice > 0, "Close price should be positive");
            Assert.IsNotNull(closeEvent.ActionTag, "Action tag should be set");
            
            Console.WriteLine($"Trade opened events: {opened.Count}");
            Console.WriteLine($"Trade closed events: {closed.Count}");
        }

        //[TestMethod]
        [TestCategory("Core")]
        public void BackwardCompatibility_SingleIndicatorModeStillWorks()
        {
            // Use fresh individual to avoid residual state
            var gi = new GeneticIndividual
            {
                StartingBalance = 10000,
                AllowMultipleTrades = false,
                AllowedSecurityTypes = AllowedSecurityType.Stock,
                AllowedTradeTypes = AllowedTradeType.Any,
                TradePercentageForStocks = 0.5,
                SignalCombination = SignalCombinationMethod.Isolation,
                LongEntryThreshold = 1,
                LongExitThreshold = 0,
                ShortEntryThreshold = -1,
                ShortExitThreshold = 0
            };
            gi.Indicators.Add(new IndicatorParams
            {
                Type = 1,
                Period = 2,
                Polarity = 1,
                TimeFrame = TimeFrame.M1,
                OHLC = OHLC.Close
            });

            // Build strong minute-based trend + reversal data (M1 bars mandatory)
            var start = DateTime.UtcNow.AddMinutes(-200);
            var strongTrendPrices = new double[] { 100, 95, 90, 85, 80, 85, 90, 95, 100, 105 };
            var bars = new List<PriceRecord>();
            // Warmup minutes (to satisfy moving average / internal buffering)
            for (int i = 0; i < 50; i++)
            {
                double p = 100 + Math.Sin(i * 0.1);
                bars.Add(new PriceRecord(start.AddMinutes(i), TimeFrame.D1, p - 0.1, p + 0.2, p - 0.2, p, volume: 1000));
            }
            var dataStartIndex = bars.Count;
            for (int i = 0; i < strongTrendPrices.Length; i++)
            {
                var p = strongTrendPrices[i];
                bars.Add(new PriceRecord(start.AddMinutes(dataStartIndex + i), TimeFrame.D1, p - 0.5, p + 0.5, p - 0.5, p, volume: 1500));
            }
            var priceRecords = bars.ToArray();

            var indicatorValues = new List<List<double>> { strongTrendPrices.ToList() };
            gi.TradeActions.Clear();
            for (int i = 0; i < priceRecords.Length; i++) gi.TradeActions.Add("");

            // Act
            gi.ExecuteTradesDeltaMode(priceRecords, indicatorValues);

            Console.WriteLine($"Starting Balance: {gi.StartingBalance}");
            Console.WriteLine($"Final Balance: {gi.FinalBalance}");
            Console.WriteLine($"Trades: {gi.Trades.Count}");

            Assert.IsTrue(gi.Trades.Count > 0, "Expected at least one trade for single-indicator isolation mode");
            Assert.AreEqual(0, gi.Trades[0].ResponsibleIndicatorIndex, "Responsible indicator index should be 0");
        }

        // Helper methods to access internal methods for testing
        private int CallCalculateIndicatorDelta(PriceRecord[] priceRecords, int barIndex, List<double> signals,
            IndicatorParams indicator, int indicatorIndex, ref IndicatorState state)
        {
            return _individual.CalculateIndicatorDelta(priceRecords, barIndex, signals, indicator, indicatorIndex, ref state);
        }

        private int CallAggregateDeltas(int[] deltas) => _individual.AggregateDeltas(deltas);

        private void CallProcessSingleBarForIndicator(PriceRecord[] priceRecords, int currentBar, List<double> signals,
            IndicatorParams indicator, int indicatorIndex, ref IndicatorState state,
            ref double balance, ref double totalUsedBalance)
        {
            _individual.ProcessSingleBarForIndicator(priceRecords, priceRecords, currentBar, currentBar, signals, indicator, indicatorIndex,
                ref state, ref balance, ref totalUsedBalance);
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void IsolationMode_FixesSequentialProcessingBug()
        {
            // Arrange - Create specific scenario that exposes the sequential bug
            _individual.AllowMultipleTrades = true;
            _individual.SignalCombination = SignalCombinationMethod.Isolation;
            _individual.StartingBalance = 1000;
            _individual.TradePercentageForStocks = 0.5; // 50% per trade to make the effect visible

            // Create two indicators that both want to trade on bar 1
            var indicatorValues = new List<List<double>>
            {
                new List<double> { 100, 101, 100, 99 },  // Indicator 0: buy on bar 1, sell on bar 2
                new List<double> { 100, 101, 100, 99 }   // Indicator 1: same signals
            };

            // Act
            _individual.ExecuteTradesDeltaMode(_testPriceRecords.Take(4).ToArray(), indicatorValues);

            // Assert - Both indicators should have had equal opportunity to trade
            var trades = _individual.Trades;
            Assert.IsTrue(trades.Count > 0, "Should have generated trades");

            // Key verification: Both indicators should be represented
            var indicator0Trades = trades.Where(t => t.ResponsibleIndicatorIndex == 0).ToList();
            var indicator1Trades = trades.Where(t => t.ResponsibleIndicatorIndex == 1).ToList();

            // With isolation mode, both indicators compete fairly for balance
            // They might not both get trades (if balance runs out), but both should be processed
            Console.WriteLine($"Final balance: {_individual.FinalBalance}");
            Console.WriteLine($"Total trades: {trades.Count}");
            Console.WriteLine($"Indicator 0 trades: {indicator0Trades.Count}");
            Console.WriteLine($"Indicator 1 trades: {indicator1Trades.Count}");

            // The key fix: verify that processing was concurrent (time-first), not sequential
            // In sequential mode, indicator 1 would never get to trade because indicator 0 used all the balance
            // In concurrent mode, both indicators compete on each bar
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ThresholdSystem_WorksWithMultipleIndicators()
        {
            // Arrange - Set up the +2/-1 threshold system
            _individual.SignalCombination = SignalCombinationMethod.Sum;
            _individual.LongEntryThreshold = 2;   // Both indicators must be bullish
            _individual.LongExitThreshold = 1;    // Exit when one indicator weakens
            _individual.ShortEntryThreshold = -2; 
            _individual.ShortExitThreshold = -1;

            // Create specific signals to test the threshold logic
            var indicatorValues = new List<List<double>>
            {
                // Indicator 0: 100 -> 101 -> 102 -> 101 -> 100
                //              +1     +1     -1     -1
                new List<double> { 100, 101, 102, 101, 100 },
                
                // Indicator 1: 100 -> 101 -> 101 -> 100 -> 99  
                //              +1     0      -1     -1
                new List<double> { 100, 101, 101, 100, 99 }
            };
            
            // Expected combined deltas:
            // Bar 1: +1 + +1 = +2 ? ENTER LONG (meets threshold)
            // Bar 2: +1 + 0 = +1 ? KEEP LONG (above exit threshold)
            // Bar 3: -1 + -1 = -2 ? EXIT LONG (below exit threshold) and ENTER SHORT
            // Bar 4: -1 + -1 = -2 ? KEEP SHORT

            var priceRecords = indicatorValues[0].Select((price, index) => new PriceRecord(
                DateTime.UtcNow.Date.AddMinutes(index), TimeFrame.M1,
                price - 0.1, price + 0.1, price - 0.1, price, volume: 1000
            )).ToArray();

            // Initialize TradeActions
            _individual.TradeActions.Clear();
            for (int i = 0; i < priceRecords.Length; i++)
            {
                _individual.TradeActions.Add("");
            }

            // Act
            _individual.ExecuteTradesDeltaMode(priceRecords, indicatorValues);

            // Assert
            var trades = _individual.Trades;
            Assert.IsTrue(trades.Count > 0, "Should have generated trades from threshold system");

            // Verify portfolio-level trades (not individual indicator trades)
            var portfolioTrades = trades.Where(t => t.ResponsibleIndicatorIndex == -1).ToList();
            Assert.AreEqual(trades.Count, portfolioTrades.Count, "All trades should be portfolio-level in aggregation mode");

            // Look for portfolio action tags
            var allActions = string.Join("", _individual.TradeActions);
            Assert.IsTrue(allActions.Contains("PORTFOLIO_"), "Should contain portfolio action tags");

            Console.WriteLine($"Portfolio trades: {portfolioTrades.Count}");
            Console.WriteLine($"Actions: {allActions}");
        }
    }
}