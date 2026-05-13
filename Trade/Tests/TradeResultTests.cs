using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Trade.Tests
{
    [TestClass]
    public class TradeResultTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_BuyTrade_CalculatesDollarGainCorrectly()
        {
            // Arrange: Buy at $100, sell at $120
            var trade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 100.0,
                ClosePrice = 120.0,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 10.0, // 10 shares
                TotalDollarAmount = 1000.0, // $1000 invested
                ResponsibleIndicatorIndex = 0 // First indicator
            };

            // Act & Assert
            var expectedDollarGain = 120.0 - 100.0; // $20 profit
            Assert.AreEqual(expectedDollarGain, trade.DollarGain, 1e-6,
                "Buy trade dollar gain should be ClosePrice - OpenPrice");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_BuyTrade_CalculatesPercentGainCorrectly()
        {
            // Arrange: Buy at $100, sell at $120
            var trade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 100.0,
                ClosePrice = 120.0,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 10.0, // 10 shares
                TotalDollarAmount = 1000.0, // $1000 invested
                ResponsibleIndicatorIndex = 0 // First indicator
            };

            // Act & Assert
            var expectedPercentGain = (120.0 - 100.0) / 100.0 * 100.0; // 20%
            Assert.AreEqual(expectedPercentGain, trade.PercentGain, 1e-6,
                "Buy trade percent gain should be (ClosePrice - OpenPrice) / OpenPrice * 100");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_BuyTrade_LossScenario()
        {
            // Arrange: Buy at $100, sell at $80 (loss)
            var trade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 100.0,
                ClosePrice = 80.0,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 10.0, // 10 shares
                TotalDollarAmount = 1000.0, // $1000 invested
                ResponsibleIndicatorIndex = 0 // First indicator
            };

            // Act & Assert
            var expectedDollarGain = 80.0 - 100.0; // -$20 loss
            var expectedPercentGain = (80.0 - 100.0) / 100.0 * 100.0; // -20%

            Assert.AreEqual(expectedDollarGain, trade.DollarGain, 1e-6,
                "Buy trade dollar loss should be negative");
            Assert.AreEqual(expectedPercentGain, trade.PercentGain, 1e-6,
                "Buy trade percent loss should be negative");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_SellShortTrade_CalculatesDollarGainCorrectly()
        {
            // Arrange: Sell short at $120, cover at $100
            var trade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 120.0,
                ClosePrice = 100.0,
                AllowedTradeType = AllowedTradeType.SellShort,
                Position = -8.33, // 8.33 shares short
                TotalDollarAmount = 1000.0, // $1000 value shorted
                ResponsibleIndicatorIndex = 1 // Second indicator
            };

            // Act & Assert
            var expectedDollarGain = 120.0 - 100.0; // $20 profit
            Assert.AreEqual(expectedDollarGain, trade.DollarGain, 1e-6,
                "Short trade dollar gain should be OpenPrice - ClosePrice");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_SellShortTrade_CalculatesPercentGainCorrectly()
        {
            // Arrange: Sell short at $120, cover at $100
            var trade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 120.0,
                ClosePrice = 100.0,
                AllowedTradeType = AllowedTradeType.SellShort,
                Position = -8.33, // 8.33 shares short
                TotalDollarAmount = 1000.0, // $1000 value shorted
                ResponsibleIndicatorIndex = 1 // Second indicator
            };

            // Act & Assert
            var expectedPercentGain = (120.0 - 100.0) / 120.0 * 100.0; // 16.67%
            Assert.AreEqual(expectedPercentGain, trade.PercentGain, 1e-6,
                "Short trade percent gain should be (OpenPrice - ClosePrice) / OpenPrice * 100");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_SellShortTrade_LossScenario()
        {
            // Arrange: Sell short at $100, cover at $120 (loss)
            var trade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 100.0,
                ClosePrice = 120.0,
                AllowedTradeType = AllowedTradeType.SellShort,
                Position = -8.33, // 8.33 shares short
                TotalDollarAmount = 1000.0, // $1000 value shorted
                ResponsibleIndicatorIndex = 1 // Second indicator
            };

            // Act & Assert
            var expectedDollarGain = 100.0 - 120.0; // -$20 loss
            var expectedPercentGain = (100.0 - 120.0) / 100.0 * 100.0; // -20%

            Assert.AreEqual(expectedDollarGain, trade.DollarGain, 1e-6,
                "Short trade dollar loss should be negative");
            Assert.AreEqual(expectedPercentGain, trade.PercentGain, 1e-6,
                "Short trade percent loss should be negative");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_BuyTrade_ZeroOpenPriceHandling()
        {
            // Arrange: Edge case with zero open price
            var trade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 0.0,
                ClosePrice = 100.0,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 10.0, // 10 shares
                TotalDollarAmount = 1000.0, // $1000 invested
                ResponsibleIndicatorIndex = -1 // Combined signal
            };

            // Act & Assert
            var expectedDollarGain = 100.0 - 0.0; // $100
            var expectedPercentGain = 0.0; // Should handle division by zero

            Assert.AreEqual(expectedDollarGain, trade.DollarGain, 1e-6,
                "Dollar gain should still calculate correctly with zero open price");
            Assert.AreEqual(expectedPercentGain, trade.PercentGain, 1e-6,
                "Percent gain should return 0 when open price is 0 to avoid division by zero");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_SellShortTrade_ZeroOpenPriceHandling()
        {
            // Arrange: Edge case with zero open price for short trade
            var trade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 0.0,
                ClosePrice = 100.0,
                AllowedTradeType = AllowedTradeType.SellShort,
                Position = -8.33, // 8.33 shares short
                TotalDollarAmount = 1000.0, // $1000 value shorted
                ResponsibleIndicatorIndex = -1 // Combined signal
            };

            // Act & Assert
            var expectedDollarGain = 0.0 - 100.0; // -$100
            var expectedPercentGain = 0.0; // Should handle division by zero

            Assert.AreEqual(expectedDollarGain, trade.DollarGain, 1e-6,
                "Short trade dollar gain should still calculate correctly with zero open price");
            Assert.AreEqual(expectedPercentGain, trade.PercentGain, 1e-6,
                "Short trade percent gain should return 0 when open price is 0 to avoid division by zero");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_CompareTradeTypes_SamePrices()
        {
            // Arrange: Same prices for both trade types to verify opposite gains
            var openPrice = 100.0;
            var closePrice = 120.0;

            var buyTrade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = openPrice,
                ClosePrice = closePrice,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 10.0, // 10 shares
                TotalDollarAmount = 1000.0, // $1000 invested
                ResponsibleIndicatorIndex = 0 // First indicator
            };

            var shortTrade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = closePrice, // Short starts at higher price
                ClosePrice = openPrice, // Covers at lower price
                AllowedTradeType = AllowedTradeType.SellShort,
                Position = -8.33, // 8.33 shares short
                TotalDollarAmount = 1000.0, // $1000 value shorted
                ResponsibleIndicatorIndex = 1 // Second indicator
            };

            // Act & Assert
            // Both should be profitable with same dollar amount
            Assert.AreEqual(20.0, buyTrade.DollarGain, 1e-6, "Buy trade should profit $20");
            Assert.AreEqual(20.0, shortTrade.DollarGain, 1e-6, "Short trade should profit $20");

            // Percent gains will differ due to different denominators
            Assert.AreEqual(20.0, buyTrade.PercentGain, 1e-6, "Buy trade should have 20% gain");
            Assert.AreEqual(100.0 / 6.0, shortTrade.PercentGain, 1e-3, "Short trade should have ~16.67% gain");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_RealWorldStockScenarios()
        {
            // Test case 1: Apple stock buy scenario
            var appleBuy = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 30,
                OpenPrice = 150.00, // Buy Apple at $150
                ClosePrice = 165.00, // Sell at $165
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 6.67, // About 6.67 shares with $1000
                TotalDollarAmount = 1000.0,
                ResponsibleIndicatorIndex = 2 // Third indicator
            };

            Assert.AreEqual(15.00, appleBuy.DollarGain, 1e-6, "Apple buy should gain $15 per share");
            Assert.AreEqual(10.0, appleBuy.PercentGain, 1e-6, "Apple buy should gain 10%");
            Assert.AreEqual(100.05, appleBuy.ActualDollarGain, 1e-2, "Apple buy should gain about $100 total");

            // Test case 2: Tesla short scenario
            var teslaShort = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 15,
                OpenPrice = 800.00, // Short Tesla at $800
                ClosePrice = 720.00, // Cover at $720
                AllowedTradeType = AllowedTradeType.SellShort,
                Position = -1.25, // 1.25 shares short with $1000
                TotalDollarAmount = 1000.0,
                ResponsibleIndicatorIndex = 3 // Fourth indicator
            };

            Assert.AreEqual(80.00, teslaShort.DollarGain, 1e-6, "Tesla short should gain $80 per share");
            Assert.AreEqual(10.0, teslaShort.PercentGain, 1e-6, "Tesla short should gain 10%");
            Assert.AreEqual(100.0, teslaShort.ActualDollarGain, 1e-2, "Tesla short should gain $100 total");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_ActualDollarGain_CalculatesCorrectly()
        {
            // Arrange: Buy 10 shares at $100, sell at $120
            var buyTrade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 100.0,
                ClosePrice = 120.0,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 10.0, // 10 shares
                TotalDollarAmount = 1000.0, // $1000 invested
                ResponsibleIndicatorIndex = 0 // First indicator
            };

            // Act & Assert
            var expectedActualGain = 20.0 * 10.0; // $20 per share * 10 shares = $200
            Assert.AreEqual(expectedActualGain, buyTrade.ActualDollarGain, 1e-6,
                "Actual dollar gain should be per-share gain times number of shares");

            // Arrange: Short 8.33 shares at $120, cover at $100
            var shortTrade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 120.0,
                ClosePrice = 100.0,
                AllowedTradeType = AllowedTradeType.SellShort,
                Position = -8.33, // 8.33 shares short
                TotalDollarAmount = 1000.0, // $1000 value shorted
                ResponsibleIndicatorIndex = 1 // Second indicator
            };

            var expectedShortGain = 20.0 * 8.33; // $20 per share * 8.33 shares = $166.60
            Assert.AreEqual(expectedShortGain, shortTrade.ActualDollarGain, 1e-3,
                "Short trade actual dollar gain should be per-share gain times absolute position");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_ResponsibleIndicatorIndex_TracksCorrectly()
        {
            // Arrange: Test that ResponsibleIndicatorIndex is properly tracked
            var combinedTrade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 100.0,
                ClosePrice = 120.0,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 10.0,
                TotalDollarAmount = 1000.0,
                ResponsibleIndicatorIndex = -1 // Combined signal
            };

            var indicatorTrade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 100.0,
                ClosePrice = 120.0,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 10.0,
                TotalDollarAmount = 1000.0,
                ResponsibleIndicatorIndex = 2 // Third indicator
            };

            // Act & Assert
            Assert.AreEqual(-1, combinedTrade.ResponsibleIndicatorIndex,
                "Combined signal trades should have ResponsibleIndicatorIndex of -1");
            Assert.AreEqual(2, indicatorTrade.ResponsibleIndicatorIndex,
                "Individual indicator trades should track their specific indicator index");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_NegativeOpenPrice_ThrowsException()
        {
            // Act & Assert: Attempting to set negative OpenPrice should throw ArgumentException
            Assert.ThrowsException<ArgumentException>(() =>
            {
                var trade = new TradeResult
                {
                    OpenIndex = 0,
                    CloseIndex = 10,
                    OpenPrice = -100.0, // Negative price should throw exception
                    ClosePrice = 120.0,
                    AllowedTradeType = AllowedTradeType.Buy
                };
            }, "Setting negative OpenPrice should throw ArgumentException");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_NegativeClosePrice_ThrowsException()
        {
            // Act & Assert: Attempting to set negative ClosePrice should throw ArgumentException
            Assert.ThrowsException<ArgumentException>(() =>
            {
                var trade = new TradeResult
                {
                    OpenIndex = 0,
                    CloseIndex = 10,
                    OpenPrice = 100.0,
                    ClosePrice = -50.0, // Negative price should throw exception
                    AllowedTradeType = AllowedTradeType.Buy
                };
            }, "Setting negative ClosePrice should throw ArgumentException");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_ValidPositivePrices_DoesNotThrow()
        {
            // Act & Assert: Valid positive prices should work without throwing
            var trade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 100.0, // Valid positive price
                ClosePrice = 120.0, // Valid positive price
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 10.0,
                TotalDollarAmount = 1000.0,
                ResponsibleIndicatorIndex = 0
            };

            // Should be able to access properties without exception
            Assert.AreEqual(100.0, trade.OpenPrice, "OpenPrice should be set correctly");
            Assert.AreEqual(120.0, trade.ClosePrice, "ClosePrice should be set correctly");
            Assert.AreEqual(20.0, trade.DollarGain, "DollarGain should calculate correctly");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_ZeroPrices_AllowedButHandledCorrectly()
        {
            // Zero prices are allowed (might represent certain edge cases)
            // but negative prices are not
            var trade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 0.0, // Zero price is allowed
                ClosePrice = 0.0, // Zero price is allowed
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 10.0,
                TotalDollarAmount = 1000.0,
                ResponsibleIndicatorIndex = 0
            };

            // Should work without exception
            Assert.AreEqual(0.0, trade.OpenPrice, "Zero OpenPrice should be allowed");
            Assert.AreEqual(0.0, trade.ClosePrice, "Zero ClosePrice should be allowed");
            Assert.AreEqual(0.0, trade.DollarGain, "DollarGain should be zero when both prices are zero");
            Assert.AreEqual(0.0, trade.PercentGain, "PercentGain should be zero to avoid division by zero");
        }
    }
}