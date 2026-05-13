using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Trade.Tests
{
    [TestClass]
    public class TradeResultValidationTests
    {
        [TestMethod][TestCategory("Core")]
        public void ValidateTradeResultCalculations_BuyTrade()
        {
            // Test the exact scenario we fixed in the code
            var buyTrade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 100.0,
                ClosePrice = 110.0,
                AllowedTradeType = AllowedTradeType.Buy
            };

            // Validate the calculations match our expected formulas
            Assert.AreEqual(10.0, buyTrade.DollarGain, 1e-10, "Buy: DollarGain = ClosePrice - OpenPrice");
            Assert.AreEqual(10.0, buyTrade.PercentGain, 1e-10,
                "Buy: PercentGain = (ClosePrice - OpenPrice) / OpenPrice * 100");
        }

        [TestMethod][TestCategory("Core")]
        public void ValidateTradeResultCalculations_SellShortTrade()
        {
            // Test the exact scenario we fixed in the code  
            var shortTrade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 110.0,
                ClosePrice = 100.0,
                AllowedTradeType = AllowedTradeType.SellShort
            };

            // Validate the calculations match our expected formulas
            Assert.AreEqual(10.0, shortTrade.DollarGain, 1e-10, "Short: DollarGain = OpenPrice - ClosePrice");
            Assert.AreEqual(9.090909090909091, shortTrade.PercentGain, 1e-10,
                "Short: PercentGain = (OpenPrice - ClosePrice) / OpenPrice * 100");
        }

        [TestMethod][TestCategory("Core")]
        public void VerifyPropertyNames()
        {
            var trade = new TradeResult
            {
                OpenIndex = 5,
                CloseIndex = 15,
                OpenPrice = 50.0,
                ClosePrice = 60.0,
                AllowedTradeType = AllowedTradeType.Buy
            };

            // Verify that the property names are correct (not the old BuyIndex/SellIndex)
            Assert.AreEqual(5, trade.OpenIndex, "OpenIndex property should work");
            Assert.AreEqual(15, trade.CloseIndex, "CloseIndex property should work");
            Assert.AreEqual(50.0, trade.OpenPrice, "OpenPrice property should work");
            Assert.AreEqual(60.0, trade.ClosePrice, "ClosePrice property should work");
        }

        [TestMethod][TestCategory("Core")]
        public void CompareOldVsNewCalculations()
        {
            // Simulate the OLD incorrect calculation vs NEW correct calculation
            var openPrice = 100.0;
            var closePrice = 120.0;

            var shortTrade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = openPrice,
                ClosePrice = closePrice,
                AllowedTradeType = AllowedTradeType.SellShort
            };

            // NEW correct calculation (what we implemented)
            var newDollarGain = openPrice - closePrice; // 100 - 120 = -20 (loss)
            var newPercentGain = (openPrice - closePrice) / openPrice * 100.0; // -20/100*100 = -20%

            // OLD incorrect calculation (what was there before - using SellPrice as denominator)
            var oldIncorrectPercent =
                closePrice != 0 ? (openPrice - closePrice) / closePrice * 100.0 : openPrice; // -20/120*100 = -16.67%

            Assert.AreEqual(newDollarGain, shortTrade.DollarGain, 1e-10, "Dollar gain calculation should be correct");
            Assert.AreEqual(newPercentGain, shortTrade.PercentGain, 1e-10,
                "Percent gain should use OpenPrice as denominator");
            Assert.AreNotEqual(oldIncorrectPercent, shortTrade.PercentGain,
                "Should NOT equal the old incorrect calculation");
        }
    }
}