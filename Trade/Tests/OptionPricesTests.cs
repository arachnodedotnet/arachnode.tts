using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class OptionPricesTests
    {
        private OptionPrices optionPrices;
        private List<PriceRecord> optionRecords;
        private Prices referencePrices;
        private string validOptionSymbol;

        [TestInitialize]
        public void Setup()
        {
            optionPrices = new OptionPrices();
            referencePrices = new Prices();
            validOptionSymbol = "SPY240814C00390000";
            var ticker = Ticker.ParseToOption(validOptionSymbol);
            var baseDate = new DateTime(2024, 8, 14, 9, 30, 0);
            optionRecords = new List<PriceRecord>();
            for (var i = 0; i < 5; i++)
            {
                var dt = baseDate.AddMinutes(i);
                referencePrices.AddPrice(new PriceRecord(dt, TimeFrame.M1, 100 + i, 101 + i, 99 + i, 100.5 + i));
                optionRecords.Add(new PriceRecord(dt, TimeFrame.M1, 10 + i, 11 + i, 9 + i, 10.5 + i, volume: 1000 + i, wap: 10.5 + i, count: 100 + i,
                    option: ticker, isComplete: false));
            }
        }

        [TestMethod][TestCategory("Core")]
        public void LoadFromPriceRecords_LoadsValidOptions()
        {
            optionPrices.LoadFromPriceRecords(optionRecords.ToArray(), referencePrices);
            Assert.AreEqual(1, optionPrices.SymbolCount);
            Assert.IsTrue(optionPrices.ContainsSymbol(validOptionSymbol));
        }

        [TestMethod][TestCategory("Core")]
        public void LoadFromPriceRecords_ThrowsOnNullArguments()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => optionPrices.LoadFromPriceRecords(null, referencePrices));
            Assert.ThrowsException<ArgumentNullException>(() =>
                optionPrices.LoadFromPriceRecords(optionRecords.ToArray(), null));
        }

        [TestMethod][TestCategory("Core")]
        public void LoadFromPriceRecords_ThrowsOnInvalidOptionSymbol()
        {
            var badRecords = new List<PriceRecord>(optionRecords);
            badRecords[0] = new PriceRecord(optionRecords[0].DateTime, TimeFrame.M1, 1, 1, 1, 1, volume: 1, wap: 1, count: 1, option: null, isComplete: false);
            Assert.ThrowsException<ArgumentException>(() =>
                optionPrices.LoadFromPriceRecords(badRecords.ToArray(), referencePrices));
        }

        [TestMethod][TestCategory("Core")]
        public void GetOptionPriceBuffer_ReturnsCorrectRecords()
        {
            optionPrices.LoadFromPriceRecords(optionRecords.ToArray(), referencePrices);
            var buffer = optionPrices.GetOptionPriceBuffer(validOptionSymbol, 3);
            Assert.AreEqual(3, buffer.Length);
            Assert.AreEqual(optionRecords[4].DateTime, buffer[0].DateTime); // Most recent first
        }

        [TestMethod][TestCategory("Core")]
        public void GetOptionPriceBuffer_ReturnsEmptyForUnknownSymbolOrBadPeriod()
        {
            var empty1 = optionPrices.GetOptionPriceBuffer("UNKNOWN", 3);
            var empty2 = optionPrices.GetOptionPriceBuffer(validOptionSymbol, 0);
            Assert.AreEqual(0, empty1.Length);
            Assert.AreEqual(0, empty2.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void GetPricesForSymbol_ReturnsPricesOrNull()
        {
            optionPrices.LoadFromPriceRecords(optionRecords.ToArray(), referencePrices);
            var prices = optionPrices.GetPricesForSymbol(validOptionSymbol);
            Assert.IsNotNull(prices);
            Assert.IsNull(optionPrices.GetPricesForSymbol("UNKNOWN"));
        }

        [TestMethod][TestCategory("Core")]
        public void GetAllPrices_ReturnsDictionary()
        {
            optionPrices.LoadFromPriceRecords(optionRecords.ToArray(), referencePrices);
            var dict = optionPrices.GetAllPrices();
            Assert.IsTrue(dict.ContainsKey(validOptionSymbol));
        }

        [TestMethod][TestCategory("Core")]
        public void ContainsSymbol_WorksForKnownAndUnknown()
        {
            optionPrices.LoadFromPriceRecords(optionRecords.ToArray(), referencePrices);
            Assert.IsTrue(optionPrices.ContainsSymbol(validOptionSymbol));
            Assert.IsFalse(optionPrices.ContainsSymbol("UNKNOWN"));
        }

        [TestMethod][TestCategory("Core")]
        public void GetTotalRecordCount_ReturnsCorrectCount()
        {
            optionPrices.LoadFromPriceRecords(optionRecords.ToArray(), referencePrices);
            Assert.AreEqual(5, optionPrices.GetTotalRecordCount());
        }

        [TestMethod][TestCategory("Core")]
        public void GetOptionsByUnderlying_ReturnsCorrectOptions()
        {
            optionPrices.LoadFromPriceRecords(optionRecords.ToArray(), referencePrices);
            var dict = optionPrices.GetOptionsByUnderlying("SPY");
            Assert.IsTrue(dict.ContainsKey(validOptionSymbol));
            Assert.AreEqual(1, dict.Count);
        }

        [TestMethod][TestCategory("Core")]
        public void Clear_RemovesAllSymbols()
        {
            optionPrices.LoadFromPriceRecords(optionRecords.ToArray(), referencePrices);
            optionPrices.Clear();
            Assert.AreEqual(0, optionPrices.SymbolCount);
        }

        [TestMethod][TestCategory("Core")]
        public void GetSummary_ReturnsExpectedString()
        {
            Assert.AreEqual("No option data loaded", optionPrices.GetSummary());
            optionPrices.LoadFromPriceRecords(optionRecords.ToArray(), referencePrices);
            var summary = optionPrices.GetSummary();
            Assert.IsTrue(summary.Contains("OptionPrices: 1 symbols"));
            Assert.IsTrue(summary.Contains("SPY: 1 options"));
        }

        // ------------------ NEW GAP-FILLING TESTS ------------------

        private Prices BuildReferenceMinutePrices(DateTime day, int minutes = 390)
        {
            var prices = new Prices();
            var start = day.Date.AddHours(9).AddMinutes(30);
            for (int i = 0; i < minutes; i++)
            {
                var t = start.AddMinutes(i);
                prices.AddPrice(new PriceRecord(t, TimeFrame.M1, 100, 101, 99, 100));
            }
            return prices;
        }

        [TestMethod][TestCategory("Core")]
        public void LoadFromPriceRecords_FillsForwardBetweenSparseTrades()
        {
            var day = new DateTime(2024, 8, 15);
            var refPrices = BuildReferenceMinutePrices(day);
            var firstTrade = day.Date.AddHours(9).AddMinutes(45); // 09:45
            var secondTrade = day.Date.AddHours(10).AddMinutes(0); // 10:00
            var symbol = "O:SPY240815C00400000";
            var ticker = Ticker.ParseToOption(symbol);

            var sparse = new[]
            {
                new PriceRecord(firstTrade, TimeFrame.M15, 5, 5, 5, 5, volume: 10, wap: 5, count: 1, option: ticker),
                new PriceRecord(secondTrade, TimeFrame.M15, 5.5, 5.5, 5.5, 5.5, volume: 20, wap: 5.5, count: 1, option: ticker)
            };

            var op = new OptionPrices();
            op.LoadFromPriceRecords(sparse, refPrices);
            var prices = op.GetPricesForSymbol(symbol);
            Assert.IsNotNull(prices, "Option prices should be loaded");

            // Expected minutes inclusive between first and second trade
            var expectedCount = (int)(secondTrade - firstTrade).TotalMinutes + 1; // inclusive
            Assert.AreEqual(expectedCount, prices.Records.Count, "Should fill forward minute gaps between trades");

            // Verify first & last real trades preserved
            Assert.AreEqual(firstTrade, prices.Records[0].DateTime);
            Assert.AreEqual(secondTrade, prices.Records[prices.Records.Count - 1].DateTime);

            // Verify synthetic (manufactured) records in between
            int manufactured = 0;
            for (int i = 1; i < prices.Records.Count - 1; i++)
            {
                if (prices.Records[i].Manufactured) manufactured++;
                Assert.AreEqual(5, prices.Records[i].Close, 1e-9, "Fill-forward should keep last known close until next real trade");
            }
            Assert.AreEqual(expectedCount - 2, manufactured, "All intermediate minutes should be manufactured");

            // Ensure no bar before first trade was fabricated
            Assert.IsNull(prices.GetPriceAt(firstTrade.AddMinutes(-1)), "Should not fabricate bars before first trade");
        }

        [TestMethod][TestCategory("Core")]
        public void LoadFromPriceRecords_DoesNotCreatePreFirstTradeBars()
        {
            var day = new DateTime(2024, 8, 16);
            var refPrices = BuildReferenceMinutePrices(day);
            var firstTrade = day.Date.AddHours(11); // 11:00 first trade (late listing)
            var lastTrade = day.Date.AddHours(11).AddMinutes(5); // 11:05
            var symbol = "O:SPY240816C00401000";
            var ticker = Ticker.ParseToOption(symbol);

            var sparse = new[]
            {
                new PriceRecord(firstTrade, TimeFrame.M5, 6, 6, 6, 6, volume: 10, wap: 6, count: 1, option: ticker),
                new PriceRecord(lastTrade, TimeFrame.M5, 6.1, 6.1, 6.1, 6.1, volume: 10, wap: 6.1, count: 1, option: ticker)
            };

            var op = new OptionPrices();
            op.LoadFromPriceRecords(sparse, refPrices);
            var prices = op.GetPricesForSymbol(symbol);
            Assert.IsNotNull(prices);

            Assert.IsNull(prices.GetPriceAt(firstTrade.AddMinutes(-1)), "No synthetic bar should precede first real trade");
        }

        [TestMethod][TestCategory("Core")]
        public void LoadFromPriceRecords_FillsEntireDayBetweenOpenAndClose_WhenSparse()
        {
            var day = new DateTime(2024, 8, 19); // Monday
            var refPrices = BuildReferenceMinutePrices(day);
            var openTrade = day.Date.AddHours(9).AddMinutes(30); // 09:30
            var closeTrade = day.Date.AddHours(15).AddMinutes(59); // 15:59
            var symbol = "O:SPY240819C00402000";
            var ticker = Ticker.ParseToOption(symbol);

            var sparse = new[]
            {
                new PriceRecord(openTrade, TimeFrame.M1, 7, 7, 7, 7, volume: 10, wap: 7, count: 1, option: ticker),
                new PriceRecord(closeTrade, TimeFrame.M1, 7.5, 7.5, 7.5, 7.5, volume: 20, wap: 7.5, count: 1, option: ticker)
            };

            var op = new OptionPrices();
            op.LoadFromPriceRecords(sparse, refPrices);
            var prices = op.GetPricesForSymbol(symbol);
            Assert.IsNotNull(prices);

            // Market minutes inclusive 09:30..15:59 = 390
            Assert.AreEqual(390, prices.Records.Count, "Should have full market session filled");

            int manufactured = 0;
            foreach (var r in prices.Records)
            {
                if (r.DateTime != openTrade && r.DateTime != closeTrade)
                {
                    Assert.IsTrue(r.Manufactured, "Intermediate minutes should be manufactured");
                    manufactured++;
                }
            }
            Assert.AreEqual(388, manufactured, "All intermediate minutes should be manufactured");
        }
    }
}