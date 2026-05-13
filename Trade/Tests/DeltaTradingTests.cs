using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class DeltaTradingTests
    {
        private static PriceRecord[] MakeDailyPrices(double[] closes, DateTime? start = null)
        {
            var startDate = start ?? new DateTime(2020, 1, 1);
            var records = new PriceRecord[closes.Length];
            for (int i = 0; i < closes.Length; i++)
            {
                var dt = startDate.AddDays(i);
                var c = closes[i];
                records[i] = new PriceRecord(dt, TimeFrame.D1, c, c, c, c, volume: 0);
            }
            return records;
        }

        [TestMethod]
        [TestCategory("Core")]
        public void EntersLong_OnFlatToUpSlope()
        {
            // signals: 500, 500, 500, 500, 501 -> expect long entry at final bar
            var closes = new[] { 500.0, 500.0, 500.0, 500.0, 501.0 };
            var priceRecords = MakeDailyPrices(closes);

            var buffer = new[] { 500.0, 500.0, 500.0, 500.0, 500.0, 501.0 };
            var bufferRecords = MakeDailyPrices(buffer, priceRecords[0].DateTime.AddDays(-1));

            var gi = new GeneticIndividual
            {
                StartingBalance = 10_000,
                TradePercentageForStocks = 0.5,
                AllowedSecurityTypes = AllowedSecurityType.Stock,
                AllowedTradeTypes = AllowedTradeType.Buy,
            };

            // Single SMA(1) indicator so indicator value == close
            gi.Indicators.Add(new IndicatorParams
            {
                Type = 2, // SMA
                Period = 2,
                Mode = 0,
                TimeFrame = TimeFrame.D1,
                Polarity = 1
            });

            GeneticIndividual.InitializePrices();
            GeneticIndividual.Prices.AddPricesBatch(bufferRecords);

            var priceRecords2 = GeneticIndividual.Prices.GetRange(priceRecords.First().DateTime,
                priceRecords.Last().DateTime.AddDays(1), TimeFrame.D1, 0, false, false).ToArray();

            // Run full pipeline
            var fitness = gi.Process(priceRecords2);

            // Assert an entry occurred on the last bar
            Assert.IsTrue(gi.TradeActions[4].Contains("BU"), "Expected long entry (BU) at index 4.");
            Assert.AreEqual(1, gi.Trades.Count, "Exactly one trade should be recorded after finalization.");
            Assert.AreEqual(8, gi.Trades[0].OpenIndex, "Trade should open at index 8.");
            Assert.AreEqual(AllowedTradeType.Buy, gi.Trades[0].AllowedTradeType, "Trade should be a long (Buy).");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void EntersShort_OnFlatToDownSlope()
        {
            // signals: 500, 500, 500, 500, 499 -> expect short entry at final bar
            var closes = new[] { 500.0, 500.0, 500.0, 500.0, 499.0 };
            var priceRecords = MakeDailyPrices(closes);

            var buffer = new[] { 500.0, 500.0, 500.0, 500.0, 500.0, 499.0 };
            var bufferRecords = MakeDailyPrices(buffer, priceRecords[0].DateTime.AddDays(-1));

            var gi = new GeneticIndividual
            {
                StartingBalance = 10_000,
                TradePercentageForStocks = 0.5,
                AllowedSecurityTypes = AllowedSecurityType.Stock,
                AllowedTradeTypes = AllowedTradeType.SellShort,
            };

            // Single SMA(1) indicator so indicator value == close
            gi.Indicators.Add(new IndicatorParams
            {
                Type = 2, // SMA/EMA(1) behaves as price
                Period = 2,
                Mode = 0,
                TimeFrame = TimeFrame.D1,
                Polarity = 1
            });

            GeneticIndividual.InitializePrices();
            GeneticIndividual.Prices.AddPricesBatch(bufferRecords);

            var priceRecords2 = GeneticIndividual.Prices.GetRange(priceRecords.First().DateTime,
                priceRecords.Last().DateTime.AddDays(1), TimeFrame.D1).ToArray();
            
            // Run full pipeline
            var fitness = gi.Process(priceRecords2);

            // Assert a short entry occurred on the last bar
            Assert.IsTrue(gi.TradeActions[4].Contains("SS"), "Expected short entry (SS) at index 4.");
            Assert.AreEqual(1, gi.Trades.Count, "Exactly one trade should be recorded after finalization.");
            Assert.AreEqual(8, gi.Trades[0].OpenIndex, "Trade should open at index 8.");
            Assert.AreEqual(AllowedTradeType.SellShort, gi.Trades[0].AllowedTradeType, "Trade should be a short (SellShort).");
        }
    }
}
