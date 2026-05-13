using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class SignalAggregationTests
    {
        private GeneticIndividual _individual;

        [TestInitialize]
        public void Setup()
        {
            _individual = new GeneticIndividual
            {
                StartingBalance = 10000,
                TradePercentageForStocks = 0.1,
                AllowedSecurityTypes = AllowedSecurityType.Stock,
                AllowedTradeTypes = AllowedTradeType.Any,
                AllowMultipleTrades = true
            };

            // Add test indicators
            _individual.Indicators.Add(new IndicatorParams { Type = 1, Polarity = 1, TimeFrame = TimeFrame.D1});
            _individual.Indicators.Add(new IndicatorParams { Type = 2, Polarity = 1, TimeFrame = TimeFrame.D1 });
            _individual.Indicators.Add(new IndicatorParams { Type = 3, Polarity = -1, TimeFrame = TimeFrame.D1 }); // Inverse polarity
        }

        [TestMethod][TestCategory("Core")]
        public void SignalCombinationMethod_Sum_AddsAllDeltas()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Sum;
            var deltas = new int[] { 1, 1, -1, 0, 1 }; // Sum = 2

            // Act
            var result = CallAggregateDeltas(deltas);

            // Assert
            Assert.AreEqual(2, result, "Sum method should add all deltas");
        }

        [TestMethod][TestCategory("Core")]
        public void SignalCombinationMethod_Majority_ReturnsSignOnly()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Majority;

            // Test various scenarios
            var testCases = new[]
            {
                (new int[] { 1, 1, 1 }, 1, "All positive"),
                (new int[] { -1, -1, -1 }, -1, "All negative"),
                (new int[] { 1, 1, -1 }, 1, "Positive majority"),
                (new int[] { -1, -1, 1 }, -1, "Negative majority"),
                (new int[] { 1, -1, 0 }, 0, "Neutral sum"),
                (new int[] { 0, 0, 0 }, 0, "All neutral")
            };

            foreach (var (deltas, expected, description) in testCases)
            {
                var result = CallAggregateDeltas(deltas);
                Assert.AreEqual(expected, result, $"Majority method failed for: {description}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void SignalCombinationMethod_Consensus_RequiresAllToAgree()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Consensus;

            var testCases = new[]
            {
                (new int[] { 1, 1, 1 }, 1, "All positive consensus"),
                (new int[] { -1, -1, -1 }, -1, "All negative consensus"),
                (new int[] { 1, 1, -1 }, 0, "Mixed signals - no consensus"),
                (new int[] { 1, 0, 1 }, 0, "With neutral - no consensus"),
                (new int[] { 0, 0, 0 }, 0, "All neutral"),
                (new int[] { 1, 1, 0 }, 0, "Partial consensus with neutral")
            };

            foreach (var (deltas, expected, description) in testCases)
            {
                var result = CallAggregateDeltas(deltas);
                Assert.AreEqual(expected, result, $"Consensus method failed for: {description}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void SignalCombinationMethod_Weighted_UsesIndicatorWeights()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Weighted;
            _individual.IndicatorWeights = new double[] { 2.0, 1.5, 0.5, 1.0 };

            var testCases = new[]
            {
                // deltas: [1, 1, 1, 1], weights: [2.0, 1.5, 0.5, 1.0] = 5.0 ? 5
                (new int[] { 1, 1, 1, 1 }, 5, "All positive with weights"),
                
                // deltas: [1, -1, 1, -1], weights: [2.0, -1.5, 0.5, -1.0] = 0.0 ? 0  
                (new int[] { 1, -1, 1, -1 }, 0, "Mixed signs with weights"),
                
                // deltas: [1, 0, 0, 0], weights: [2.0, 0, 0, 0] = 2.0 ? 2
                (new int[] { 1, 0, 0, 0 }, 2, "Single indicator with weight"),
            };

            foreach (var (deltas, expected, description) in testCases)
            {
                var result = CallAggregateDeltas(deltas);
                Assert.AreEqual(expected, result, $"Weighted method failed for: {description}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void SignalCombinationMethod_Weighted_HandlesNoWeights()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Weighted;
            _individual.IndicatorWeights = null; // No weights specified

            var deltas = new int[] { 1, 1, -1 }; // Should use weight 1.0 for all

            // Act
            var result = CallAggregateDeltas(deltas);

            // Assert
            Assert.AreEqual(1, result, "Should default to weight 1.0 when no weights specified");
        }

        [TestMethod][TestCategory("Core")]
        public void SignalCombinationMethod_Weighted_HandlesPartialWeights()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Weighted;
            _individual.IndicatorWeights = new double[] { 2.0, 1.5 }; // Only 2 weights for 3 indicators

            var deltas = new int[] { 1, 1, 1 }; // Third indicator should use default weight 1.0

            // Act
            var result = CallAggregateDeltas(deltas);

            // Assert
            // Expected: (1 * 2.0) + (1 * 1.5) + (1 * 1.0) = 4.5 ? 5
            Assert.AreEqual(5, result, "Should use default weight for indicators without specified weights");
        }

        [TestMethod][TestCategory("Core")]
        public void ThresholdSystem_LongEntryAndExit()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Sum;
            _individual.LongEntryThreshold = 2;   // Need +2 to enter long
            _individual.LongExitThreshold = 1;    // Exit when drops to +1 
            _individual.ShortEntryThreshold = -2;
            _individual.ShortExitThreshold = -1;
            _individual.OptionExitThreshold = 0;

            // Create test data
            var priceRecords = new PriceRecord[]
            {
                CreatePriceRecord(0, 100),
                CreatePriceRecord(1, 101),  // Bar where entry should occur
                CreatePriceRecord(2, 102),  // Hold position
                CreatePriceRecord(3, 101),  // Bar where exit should occur
                CreatePriceRecord(4, 100)
            };

            var indicatorValues = new List<List<double>>
            {
                new List<double> { 100, 101, 102, 101, 100 }, // +1, +1, -1, -1 (Polarity +1)
                new List<double> { 100, 101, 101, 100, 99 },  // +1,  0, -1, -1 (Polarity +1)
                new List<double> { 100, 99, 98, 99, 100 }     // -1, -1, +1, +1 (Polarity -1, so becomes +1, +1, -1, -1)
            };
            // Combined deltas with 3 indicators: [+2, +1, -2, -2]

            InitializeTradeActions(priceRecords.Length);

            // Act
            _individual.ExecuteTradesDeltaMode(priceRecords, indicatorValues);

            // Assert
            var trades = _individual.Trades;
            Assert.IsTrue(trades.Count > 0, "Should generate trades from threshold system");

            // All trades should be portfolio level
            Assert.IsTrue(trades.All(t => t.ResponsibleIndicatorIndex == -1),
                "All trades should be portfolio-level in aggregation mode");

            // Check for portfolio action tags
            var allActions = string.Join("", _individual.TradeActions);
            Assert.IsTrue(allActions.Contains("PORTFOLIO_"), "Should contain portfolio action tags");

            Console.WriteLine($"Trades generated: {trades.Count}");
            Console.WriteLine($"Actions: {allActions}");
        }

        [TestMethod][TestCategory("Core")]
        public void ThresholdSystem_ShortEntryAndExit()
        {
            // Arrange
            _individual.SignalCombination = SignalCombinationMethod.Sum;
            _individual.LongEntryThreshold = 2;
            _individual.LongExitThreshold = 1;
            _individual.ShortEntryThreshold = -2;  // Need -2 to enter short
            _individual.ShortExitThreshold = -1;   // Exit when rises to -1
            _individual.OptionExitThreshold = 0;

            var priceRecords = new PriceRecord[]
            {
                CreatePriceRecord(0, 100),
                CreatePriceRecord(1, 99),   // Bar where short entry should occur
                CreatePriceRecord(2, 98),   // Hold short position  
                CreatePriceRecord(3, 99),   // Bar where exit should occur
                CreatePriceRecord(4, 100)
            };

            var indicatorValues = new List<List<double>>
            {
                new List<double> { 100, 99, 98, 99, 100 },  // -1, -1, +1, +1 (Polarity +1)
                new List<double> { 100, 99, 99, 100, 101 }, // -1,  0, +1, +1 (Polarity +1)
                new List<double> { 100, 101, 102, 101, 99 } // +1, +1, -1, -2 (Polarity -1, so becomes -1, -1, +1, +2)
            };
            // Combined deltas with 3 indicators: [-3, -1, +2, +3]

            InitializeTradeActions(priceRecords.Length);

            // Act  
            _individual.ExecuteTradesDeltaMode(priceRecords, indicatorValues);

            // Assert
            var trades = _individual.Trades;
            Assert.IsTrue(trades.Count > 0, "Should generate short trades from threshold system");

            // Look for short trades
            var shortTrades = trades.Where(t => t.AllowedTradeType == AllowedTradeType.SellShort).ToList();
            Console.WriteLine($"Short trades: {shortTrades.Count}");
            Console.WriteLine($"Total trades: {trades.Count}");
        }

        [TestMethod][TestCategory("Core")]
        public void MixedSignalCombinations_ProduceDifferentResults()
        {
            // Arrange - Same input deltas, different combination methods
            var deltas = new int[] { 1, 1, -1, 1 }; // Sum=2, Majority=1, may not reach consensus

            var testResults = new Dictionary<SignalCombinationMethod, int>();

            foreach (SignalCombinationMethod method in Enum.GetValues(typeof(SignalCombinationMethod)))
            {
                _individual.SignalCombination = method;
                if (method == SignalCombinationMethod.Weighted)
                {
                    _individual.IndicatorWeights = new double[] { 1.0, 1.0, 1.0, 1.0 };
                }
                
                testResults[method] = CallAggregateDeltas(deltas);
            }

            // Assert - Different methods should produce different results
            Assert.AreEqual(2, testResults[SignalCombinationMethod.Sum], "Sum should be 2");
            Assert.AreEqual(1, testResults[SignalCombinationMethod.Majority], "Majority should be 1");
            Assert.AreEqual(0, testResults[SignalCombinationMethod.Consensus], "Consensus should be 0 (mixed signals)");
            Assert.AreEqual(2, testResults[SignalCombinationMethod.Weighted], "Weighted should be 2 (same as sum with equal weights)");

            Console.WriteLine("Signal combination results:");
            foreach (var kvp in testResults)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void PortfolioDecision_RespectsThresholds()
        {
            // This tests the core logic that your +2/-1 system relies on

            // Arrange
            _individual.LongEntryThreshold = 3;   // High threshold
            _individual.LongExitThreshold = 2;
            _individual.ShortEntryThreshold = -3; // High threshold (absolute)
            _individual.ShortExitThreshold = -2;

            var priceRecord = CreatePriceRecord(1, 101);
            var priceRecords = new[] { priceRecord }; // Create array for consistency

            // Initialize TradeActions for the price records
            InitializeTradeActions(priceRecords.Length);

            var state = new PortfolioState();
            var balance = 10000.0;
            var totalUsedBalance = 0.0;

            // Test 1: Signal too weak for entry
            CallProcessPortfolioDecision(priceRecords, priceRecords, 0, 2, ref state, ref balance, ref totalUsedBalance);
            Assert.IsFalse(state.HoldingStock, "Should not enter position with signal=2 when threshold=3");

            // Test 2: Signal strong enough for entry
            CallProcessPortfolioDecision(priceRecords, priceRecords, 0, 3, ref state, ref balance, ref totalUsedBalance);
            Assert.IsTrue(state.HoldingStock, "Should enter position with signal=3 when threshold=3");
            Assert.IsTrue(state.StockPosition > 0, "Should be long");

            // Test 3: Signal weakens but not enough to exit
            CallProcessPortfolioDecision(priceRecords, priceRecords, 0, 2, ref state, ref balance, ref totalUsedBalance);
            Assert.IsTrue(state.HoldingStock, "Should keep position with signal=2 when exit threshold=2");

            // Test 4: Signal weakens enough to exit
            CallProcessPortfolioDecision(priceRecords, priceRecords, 0, 1, ref state, ref balance, ref totalUsedBalance);
            Assert.IsFalse(state.HoldingStock, "Should exit position with signal=1 when exit threshold=2");
        }

        // Helper methods
        private int CallAggregateDeltas(int[] deltas)
        {
            return _individual.AggregateDeltas(deltas);
        }

        private void CallProcessPortfolioDecision(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int barIndex, int combinedDelta,
            ref PortfolioState state, ref double balance, ref double totalUsedBalance)
        {
            _individual.ProcessPortfolioDecision(priceRecords, guideRecords, barIndex, barIndex, combinedDelta, ref state, ref balance, ref totalUsedBalance);
        }

        private PriceRecord CreatePriceRecord(int day, double price)
        {
            return new PriceRecord(
                DateTime.Today.AddDays(day), TimeFrame.D1,
                price - 0.1, price + 0.1, price - 0.1, price, volume: 1000
            );
        }

        private void InitializeTradeActions(int length)
        {
            _individual.TradeActions.Clear();
            for (int i = 0; i < length; i++)
            {
                _individual.TradeActions.Add("");
            }
        }
    }
}