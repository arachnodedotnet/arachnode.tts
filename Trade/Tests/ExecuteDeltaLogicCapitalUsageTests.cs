using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Tests illustrating current capital accounting behavior when executing delta mode trading.
    /// Validates that principal is not debited on entry and only P/L affects realized balance when trades close.
    /// </summary>
    [TestClass]
    public class ExecuteDeltaLogicCapitalUsageTests
    {
        [TestMethod][TestCategory("Core")]
        public void ExecuteTradesDeltaMode_CorrectAccountingBehavior_DebitsAndCredits()
        {
            // Arrange
            var gi = new GeneticIndividual
            {
                StartingBalance = 10_000,
                TradePercentageForStocks = 0.5, // 50% position target
                AllowedTradeTypes = AllowedTradeType.Buy,
                AllowedSecurityTypes = AllowedSecurityType.Stock,
                AllowMultipleTrades = false
            };
            var ind = new IndicatorParams
            {
                Type = 1,
                Period = 1,
                TimeFrame = TimeFrame.D1,
                Polarity = 1
            };
            gi.Indicators.Add(ind);

            // Two bars -> slope appears only at second bar; entry and finalization both at bar index 1
            var priceRecords = new[]
            {
                new PriceRecord(new DateTime(2025,1,1), TimeFrame.D1,100,100,100,100,volume: 1000,wap: 100,count: 1),
                new PriceRecord(new DateTime(2025,1,2), TimeFrame.D1,101,101,101,101,volume: 1000,wap: 101,count: 1)
            };

            var indicatorValues = new List<List<double>>
            {
                new List<double> { 0.0, 1.0 } // Positive slope triggers entry at i=1
            };

            gi.TradeActions.Clear();
            for (int i = 0; i < priceRecords.Length; i++) gi.TradeActions.Add(string.Empty);

            // Act
            gi.ExecuteTradesDeltaMode(priceRecords, indicatorValues);

            // Assert: exactly one (immediately finalized) trade
            Assert.AreEqual(1, gi.Trades.Count, "One closed trade expected.");
            var trade = gi.Trades[0];
            Assert.AreEqual(1, trade.OpenIndex, "Open index should be 1 (entry occurs on second bar).");
            Assert.AreEqual(1, trade.CloseIndex, "Close index is also 1 due to end-of-series finalization.");
            Assert.AreEqual(101.0, trade.OpenPrice, 1e-8, "Open price must match bar 1 close.");
            Assert.AreEqual(101.0, trade.ClosePrice, 1e-8, "Close price equals open (no movement).");

            // Shares actually purchased (cash debited at entry)
            var shares = 5000.0 / 101.0;
            Assert.AreEqual(shares, trade.Position, 1e-8, "Position size should reflect 50% allocation at entry price.");

            // Zero profit because entry & exit are same price
            var expectedProfit = 0.0;
            var expectedFinalBalance = gi.StartingBalance + expectedProfit;

            Assert.AreEqual(expectedFinalBalance, gi.FinalBalance, 1e-6,
                "FinalBalance should equal StartingBalance (no price change between entry and exit).");
        }

        [TestMethod][TestCategory("Core")]
        public void ExecuteTradesDeltaMode_ZeroProfitTrade_CorrectAccounting()
        {
            // Arrange
            var gi = new GeneticIndividual
            {
                StartingBalance = 10_000,
                TradePercentageForStocks = 0.5,
                AllowedTradeTypes = AllowedTradeType.Buy,
                AllowedSecurityTypes = AllowedSecurityType.Stock,
                AllowMultipleTrades = false
            };
            var ind = new IndicatorParams { Type = 1, Period = 1, TimeFrame = TimeFrame.D1, Polarity = 1 };
            gi.Indicators.Add(ind);

            // Four bars: entry at 101, exit at 101 (zero profit)
            var priceRecords = new[]
            {
                new PriceRecord(new DateTime(2025,1,1), TimeFrame.D1,100,100,100,100,volume: 1000,wap: 100,count: 1),
                new PriceRecord(new DateTime(2025,1,2), TimeFrame.D1,101,101,101,101,volume: 1000,wap: 101,count: 1),
                new PriceRecord(new DateTime(2025,1,3), TimeFrame.D1,102,102,102,102,volume: 1000,wap: 102,count: 1),
                new PriceRecord(new DateTime(2025,1,4), TimeFrame.D1,101,101,101,101,volume: 1000,wap: 101,count: 1) // Same price as entry
            };

            var indicatorValues = new List<List<double>>
            {
                new List<double> { 0, 1, 2, 1 } // Up, up, down (triggers exit)
            };

            gi.TradeActions.Clear();
            for (int i = 0; i < priceRecords.Length; i++) gi.TradeActions.Add(string.Empty);

            // Act
            gi.ExecuteTradesDeltaMode(priceRecords, indicatorValues);

            // Assert
            Assert.AreEqual(1, gi.Trades.Count, "One closed trade expected.");
            var trade = gi.Trades[0];
            Assert.AreEqual(1, trade.OpenIndex, "Open at bar 1 (price 101).");
            Assert.AreEqual(3, trade.CloseIndex, "Close at bar 3 (price 101).");

            // ? CORRECT ACCOUNTING: Entry and exit at same price = zero profit
            // Entry: Buy ~49.5 shares @ $101 = $5,000 cost
            // Exit: Sell ~49.5 shares @ $101 = $5,000 proceeds  
            // Net gain: $0, so final balance = starting balance
            Assert.AreEqual(101.0, trade.OpenPrice, 1e-8, "Open price should be 101.");
            Assert.AreEqual(101.0, trade.ClosePrice, 1e-8, "Close price should be 101.");
            Assert.AreEqual(gi.StartingBalance, gi.FinalBalance, 1e-6, 
                "With zero profit, FinalBalance should equal StartingBalance.");
        }

        [TestMethod][TestCategory("Core")]
        public void ExecuteTradesDeltaMode_ProfitableTrade_CorrectAccounting()
        {
            // Arrange
            var gi = new GeneticIndividual
            {
                StartingBalance = 10_000,
                TradePercentageForStocks = 0.5,
                AllowedTradeTypes = AllowedTradeType.Buy,
                AllowedSecurityTypes = AllowedSecurityType.Stock,
                AllowMultipleTrades = false
            };
            var ind = new IndicatorParams { Type = 1, Period = 1, TimeFrame = TimeFrame.D1, Polarity = 1 };
            gi.Indicators.Add(ind);

            // Four bars with significant price move: 200->400 (100% gain)
            var priceRecords = new[]
            {
                new PriceRecord(new DateTime(2025,1,1), TimeFrame.D1,100,100,100,100,volume: 1000,wap: 100,count: 1),
                new PriceRecord(new DateTime(2025,1,2), TimeFrame.D1,200,200,200,200,volume: 1000,wap: 200,count: 1), // Entry
                new PriceRecord(new DateTime(2025,1,3), TimeFrame.D1,400,400,400,400,volume: 1000,wap: 400,count: 1), 
                new PriceRecord(new DateTime(2025,1,4), TimeFrame.D1,300,300,300,300,volume: 1000,wap: 300,count: 1) // Exit at 300
            };

            var indicatorValues = new List<List<double>>
            {
                new List<double> { 0, 1, 2, 1 } // Up, up, down (triggers exit)
            };

            gi.TradeActions.Clear();
            for (int i = 0; i < priceRecords.Length; i++) gi.TradeActions.Add(string.Empty);

            // Act
            gi.ExecuteTradesDeltaMode(priceRecords, indicatorValues);

            // Assert
            Assert.AreEqual(1, gi.Trades.Count, "One closed trade expected.");
            var trade = gi.Trades[0];
            Assert.AreEqual(1, trade.OpenIndex, "Open at bar 1 (price 200).");
            Assert.AreEqual(3, trade.CloseIndex, "Close at bar 3 (price 300).");

            // ? CORRECT ACCOUNTING: Calculate expected profit
            // Entry: Buy 25 shares @ $200 = $5,000 investment
            // Exit: Sell 25 shares @ $300 = $7,500 proceeds
            // Profit: $7,500 - $5,000 = $2,500
            var sharesTraded = 5000.0 / 200.0; // 25 shares
            var expectedProfit = sharesTraded * (300.0 - 200.0); // $2,500
            var expectedFinalBalance = gi.StartingBalance + expectedProfit; // $12,500

            Assert.AreEqual(200.0, trade.OpenPrice, 1e-8, "Open price should be 200.");
            Assert.AreEqual(300.0, trade.ClosePrice, 1e-8, "Close price should be 300.");
            Assert.AreEqual(expectedFinalBalance, gi.FinalBalance, 1e-6, 
                "FinalBalance should reflect starting balance plus $2,500 profit.");

            gi.CalculateFitness();
            Assert.AreEqual(25.0, gi.Fitness.PercentGain, 1e-6, "Should show 25% gain on $10,000 account.");
        }
    }
}
