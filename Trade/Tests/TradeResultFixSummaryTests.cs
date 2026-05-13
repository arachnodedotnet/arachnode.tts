using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Trade.Tests
{
    [TestClass]
    public class TradeResultFixSummaryTests
    {
        [TestMethod][TestCategory("Core")]
        public void Summary_AllFixesValidated()
        {
            WriteTestHeader("TRADERESULT FIXES VALIDATION SUMMARY");

            // 1. Test property name changes
            ValidatePropertyNames();

            // 2. Test buy trade calculations 
            ValidateBuyTradeCalculations();

            // 3. Test sell short trade calculations
            ValidateSellShortTradeCalculations();

            // 4. Test edge cases
            ValidateEdgeCases();

            // 5. Test new dollar amount tracking
            ValidateNewDollarAmountFields();

            Console.WriteLine("? ALL TRADERESULT FIXES VALIDATED SUCCESSFULLY!");
        }

        private void ValidatePropertyNames()
        {
            Console.WriteLine("\n1. PROPERTY NAME VALIDATION:");
            var trade = new TradeResult
            {
                OpenIndex = 10,
                CloseIndex = 20,
                OpenPrice = 100.0,
                ClosePrice = 110.0,
                AllowedTradeType = AllowedTradeType.Buy
            };

            Assert.AreEqual(10, trade.OpenIndex, "OpenIndex should work (was BuyIndex)");
            Assert.AreEqual(20, trade.CloseIndex, "CloseIndex should work (was SellIndex)");
            Assert.AreEqual(100.0, trade.OpenPrice, "OpenPrice should work (was BuyPrice)");
            Assert.AreEqual(110.0, trade.ClosePrice, "ClosePrice should work (was SellPrice)");

            Console.WriteLine("   ? Property names correctly changed from BuyIndex/SellIndex to OpenIndex/CloseIndex");
            Console.WriteLine("   ? Property names correctly changed from BuyPrice/SellPrice to OpenPrice/ClosePrice");
        }

        private void ValidateBuyTradeCalculations()
        {
            Console.WriteLine("\n2. BUY TRADE CALCULATIONS:");

            // Profitable buy trade
            var profitableBuy = new TradeResult
            {
                OpenIndex = 0, CloseIndex = 10,
                OpenPrice = 100.0, ClosePrice = 120.0,
                AllowedTradeType = AllowedTradeType.Buy
            };

            Assert.AreEqual(20.0, profitableBuy.DollarGain, 1e-10, "Buy DollarGain = ClosePrice - OpenPrice");
            Assert.AreEqual(20.0, profitableBuy.PercentGain, 1e-10,
                "Buy PercentGain = (ClosePrice - OpenPrice) / OpenPrice * 100");

            Console.WriteLine(
                $"   ? Profitable Buy: Open=$100, Close=$120 ? Gain=${profitableBuy.DollarGain} ({profitableBuy.PercentGain}%)");

            // Losing buy trade
            var losingBuy = new TradeResult
            {
                OpenIndex = 0, CloseIndex = 10,
                OpenPrice = 100.0, ClosePrice = 80.0,
                AllowedTradeType = AllowedTradeType.Buy
            };

            Assert.AreEqual(-20.0, losingBuy.DollarGain, 1e-10, "Buy loss should be negative");
            Assert.AreEqual(-20.0, losingBuy.PercentGain, 1e-10, "Buy loss percentage should be negative");

            Console.WriteLine(
                $"   ? Losing Buy: Open=$100, Close=$80 ? Loss=${losingBuy.DollarGain} ({losingBuy.PercentGain}%)");
        }

        private void ValidateSellShortTradeCalculations()
        {
            Console.WriteLine("\n3. SELL SHORT TRADE CALCULATIONS:");

            // Profitable short trade
            var profitableShort = new TradeResult
            {
                OpenIndex = 0, CloseIndex = 10,
                OpenPrice = 120.0, ClosePrice = 100.0,
                AllowedTradeType = AllowedTradeType.SellShort
            };

            Assert.AreEqual(20.0, profitableShort.DollarGain, 1e-10, "Short DollarGain = OpenPrice - ClosePrice");
            Assert.AreEqual(16.666666666666668, profitableShort.PercentGain, 1e-10,
                "Short PercentGain = (OpenPrice - ClosePrice) / OpenPrice * 100");

            Console.WriteLine(
                $"   ? Profitable Short: Open=$120, Close=$100 ? Gain=${profitableShort.DollarGain} ({profitableShort.PercentGain:F2}%)");

            // Losing short trade  
            var losingShort = new TradeResult
            {
                OpenIndex = 0, CloseIndex = 10,
                OpenPrice = 100.0, ClosePrice = 120.0,
                AllowedTradeType = AllowedTradeType.SellShort
            };

            Assert.AreEqual(-20.0, losingShort.DollarGain, 1e-10, "Short loss should be negative");
            Assert.AreEqual(-20.0, losingShort.PercentGain, 1e-10, "Short loss percentage should be negative");

            Console.WriteLine(
                $"   ? Losing Short: Open=$100, Close=$120 ? Loss=${losingShort.DollarGain} ({losingShort.PercentGain}%)");

            // CRITICAL FIX VALIDATION: Ensure we use OpenPrice as denominator, not ClosePrice
            Console.WriteLine("\n   ?? CRITICAL FIX VERIFICATION:");
            Console.WriteLine("   OLD (INCORRECT): PercentGain used ClosePrice as denominator");
            Console.WriteLine("   NEW (CORRECT): PercentGain uses OpenPrice as denominator");

            var oldIncorrectCalc = (profitableShort.OpenPrice - profitableShort.ClosePrice) /
                profitableShort.ClosePrice * 100.0; // Would be 20%
            var newCorrectCalc = profitableShort.PercentGain; // Should be 16.67%

            Assert.AreNotEqual(oldIncorrectCalc, newCorrectCalc, "Should NOT equal the old incorrect calculation");
            Console.WriteLine($"   ? Old incorrect calculation would give: {oldIncorrectCalc:F2}%");
            Console.WriteLine($"   ? New correct calculation gives: {newCorrectCalc:F2}%");
        }

        private void ValidateEdgeCases()
        {
            Console.WriteLine("\n4. EDGE CASE VALIDATION:");

            // Zero open price
            var zeroOpen = new TradeResult
            {
                OpenIndex = 0, CloseIndex = 10,
                OpenPrice = 0.0, ClosePrice = 100.0,
                AllowedTradeType = AllowedTradeType.Buy
            };

            Assert.AreEqual(100.0, zeroOpen.DollarGain, 1e-10, "Dollar gain should work with zero open price");
            Assert.AreEqual(0.0, zeroOpen.PercentGain, 1e-10, "Percent gain should be 0 to avoid division by zero");

            Console.WriteLine(
                $"   ? Zero open price handled: DollarGain=${zeroOpen.DollarGain}, PercentGain={zeroOpen.PercentGain}%");

            // Same prices (no gain/loss)
            var samePrice = new TradeResult
            {
                OpenIndex = 0, CloseIndex = 10,
                OpenPrice = 100.0, ClosePrice = 100.0,
                AllowedTradeType = AllowedTradeType.Buy
            };

            Assert.AreEqual(0.0, samePrice.DollarGain, 1e-10, "No gain when prices are same");
            Assert.AreEqual(0.0, samePrice.PercentGain, 1e-10, "No percent gain when prices are same");

            Console.WriteLine(
                $"   ? Same price handled: DollarGain=${samePrice.DollarGain}, PercentGain={samePrice.PercentGain}%");
        }

        private void ValidateNewDollarAmountFields()
        {
            Console.WriteLine("\n5. NEW DOLLAR AMOUNT FIELD VALIDATION:");

            // Test buy trade with position and total amount
            var buyTrade = new TradeResult
            {
                OpenIndex = 0, CloseIndex = 10,
                OpenPrice = 100.0, ClosePrice = 120.0,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 10.0,
                TotalDollarAmount = 1000.0
            };

            Assert.AreEqual(10.0, buyTrade.Position, 1e-10, "Position should be set correctly");
            Assert.AreEqual(1000.0, buyTrade.TotalDollarAmount, 1e-10, "TotalDollarAmount should be set correctly");
            Assert.AreEqual(200.0, buyTrade.ActualDollarGain, 1e-10,
                "ActualDollarGain should be per-share gain * position");

            Console.WriteLine(
                $"   ? Buy Trade: {buyTrade.Position} shares @ ${buyTrade.OpenPrice} = ${buyTrade.TotalDollarAmount}, Actual Gain = ${buyTrade.ActualDollarGain}");

            // Test short trade with position and total amount
            var shortTrade = new TradeResult
            {
                OpenIndex = 0, CloseIndex = 10,
                OpenPrice = 120.0, ClosePrice = 100.0,
                AllowedTradeType = AllowedTradeType.SellShort,
                Position = -8.33,
                TotalDollarAmount = 1000.0
            };

            Assert.AreEqual(-8.33, shortTrade.Position, 1e-2, "Short position should be negative");
            Assert.AreEqual(1000.0, shortTrade.TotalDollarAmount, 1e-10, "TotalDollarAmount should be set correctly");
            Assert.AreEqual(166.6, shortTrade.ActualDollarGain, 1e-1,
                "ActualDollarGain should be per-share gain * absolute position");

            Console.WriteLine(
                $"   ? Short Trade: {shortTrade.Position} shares @ ${shortTrade.OpenPrice} = ${shortTrade.TotalDollarAmount}, Actual Gain = ${shortTrade.ActualDollarGain}");
        }

        private void WriteTestHeader(string title)
        {
            Console.WriteLine($"\n{'=',80}");
            Console.WriteLine($"{title}");
            Console.WriteLine($"{'=',80}");
        }
    }
}