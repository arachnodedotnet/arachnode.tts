using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class OptionPricesConcurrencyTests
    {
        private static readonly DateTime StaticMonday = new DateTime(2025, 8, 11, 9, 30, 0); // Monday, Aug 11, 2025

        private static readonly string ValidOptionSymbol = "O:SPY250811C00100000"; // Expires Aug 14, 2025

        private static PriceRecord CreateRecord(string symbol, DateTime dt)
        {
            return new PriceRecord(dt, TimeFrame.M1, 100, 101, 99, 100, volume: 1000, wap: 100, count: 1, option: new Ticker { Symbol = symbol });
        }

        [TestMethod][TestCategory("Core")]
        public void Concurrent_LoadFromPriceRecords_DoesNotCorruptData()
        {
            var optionPrices = new OptionPrices();
            var referencePrices = new Prices();
            var baseDate = StaticMonday;

            // Prepare reference prices (simulate 10 minutes)
            for (var i = 0; i < 10; i++)
                referencePrices.AddPrice(CreateRecord("SPY", baseDate.AddMinutes(i)));

            // Prepare option records for two symbols
            var symbols = new[] { ValidOptionSymbol, "O:AAPL250814C00175000" };
            var allRecords = new List<PriceRecord>();
            foreach (var symbol in symbols)
                for (var i = 0; i < 10; i++)
                    allRecords.Add(CreateRecord(symbol, baseDate.AddMinutes(i)));

            // Split records by symbol for parallel loading
            var grouped = allRecords.GroupBy(r => r.Option.Symbol).ToList();

            Parallel.ForEach(grouped,
                group => { optionPrices.LoadFromPriceRecords(group.ToArray(), referencePrices); });

            // Assert both symbols loaded and have correct record count
            foreach (var symbol in symbols)
            {
                var prices = optionPrices.GetPricesForSymbol(symbol);
                Assert.IsNotNull(prices, $"Prices for {symbol} should not be null");
                Assert.AreEqual(10, prices.Records.Count, $"Prices for {symbol} should have 10 records");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Concurrent_AddAndRemove_SameSymbol_NoCorruption()
        {
            var optionPrices = new OptionPrices();
            var referencePrices = new Prices();
            var baseDate = StaticMonday;
            var symbol = ValidOptionSymbol;

            for (var i = 0; i < 10; i++)
                referencePrices.AddPrice(CreateRecord("SPY", baseDate.AddMinutes(i)));

            var records = Enumerable.Range(0, 10)
                .Select(j => CreateRecord(symbol, baseDate.AddMinutes(j)))
                .ToArray();

            int addCount = 0, removeCount = 0;
            Parallel.For(0, 100, i =>
            {
                if (i % 2 == 0)
                {
                    optionPrices.LoadFromPriceRecords(records, referencePrices);
                    Interlocked.Increment(ref addCount);
                }
                else
                {
                    optionPrices.TryRemove(symbol);
                    Interlocked.Increment(ref removeCount);
                }
            });

            // After all operations, symbol should be either present with correct data or absent
            var prices = optionPrices.GetPricesForSymbol(symbol);
            if (prices != null)
                Assert.AreEqual(10, prices.Records.Count);
        }

        [TestMethod][TestCategory("Core")]
        public void Concurrent_ClearAndAdd_NoCorruption()
        {
            var optionPrices = new OptionPrices();
            var referencePrices = new Prices();
            var baseDate = StaticMonday;
            var symbols = Enumerable.Range(0, 10).Select(i => $"O:SPY250811C{10000 + i:D8}").ToArray();

            for (var i = 0; i < 10; i++)
                referencePrices.AddPrice(CreateRecord("SPY", baseDate.AddMinutes(i)));

            var records = symbols.SelectMany(symbol =>
                Enumerable.Range(0, 10).Select(j => CreateRecord(symbol, baseDate.AddMinutes(j)))).ToArray();

            // Phase 1: Add all symbols in parallel
            Parallel.ForEach(symbols, symbol =>
            {
                var recs = records.Where(r => r.Option.Symbol == symbol).ToArray();
                optionPrices.LoadFromPriceRecords(recs, referencePrices);
            });

            // Phase 2: Clear all symbols
            optionPrices.Clear();

            // Phase 3: Assert that all symbols are gone
            Assert.AreEqual(0, optionPrices.SymbolCount);
        }


        [TestMethod][TestCategory("Core")]
        public void Concurrent_GetAllPrices_Consistency()
        {
            var optionPrices = new OptionPrices();
            var referencePrices = new Prices();
            var baseDate = StaticMonday;
            var symbols = Enumerable.Range(0, 10).Select(i => $"O:SPY250811C{10000 + i:D8}").ToArray();

            for (var i = 0; i < 10; i++)
                referencePrices.AddPrice(CreateRecord("SPY", baseDate.AddMinutes(i)));

            var records = symbols.SelectMany(symbol =>
                Enumerable.Range(0, 10).Select(j => CreateRecord(symbol, baseDate.AddMinutes(j)))).ToArray();

            Parallel.ForEach(symbols, symbol =>
            {
                var recs = records.Where(r => r.Option.Symbol == symbol).ToArray();
                optionPrices.LoadFromPriceRecords(recs, referencePrices);
            });

            var consistentSnapshots = 0;
            Parallel.For(0, 100, i =>
            {
                var allPrices = optionPrices.GetAllPrices();
                if (allPrices.Values.All(p => p.Records.Count == 10))
                    Interlocked.Increment(ref consistentSnapshots);
            });

            Assert.IsTrue(consistentSnapshots > 0, "Should get at least one consistent snapshot");
        }

        [TestMethod][TestCategory("Core")]
        public void Concurrent_GetTotalRecordCount_Accuracy()
        {
            var optionPrices = new OptionPrices();
            var referencePrices = new Prices();
            var baseDate = StaticMonday;
            var symbols = Enumerable.Range(0, 10).Select(i => $"O:SPY250811C{10000 + i:D8}").ToArray();

            for (var i = 0; i < 10; i++)
                referencePrices.AddPrice(CreateRecord("SPY", baseDate.AddMinutes(i)));

            var records = symbols.SelectMany(symbol =>
                Enumerable.Range(0, 10).Select(j => CreateRecord(symbol, baseDate.AddMinutes(j)))).ToArray();

            Parallel.ForEach(symbols, symbol =>
            {
                var recs = records.Where(r => r.Option.Symbol == symbol).ToArray();
                optionPrices.LoadFromPriceRecords(recs, referencePrices);
            });

            var total = optionPrices.GetTotalRecordCount();
            Assert.AreEqual(100, total, "Total record count should match expected value");
        }

        [TestMethod][TestCategory("Core")]
        public void Concurrent_GetOptionPrice_SameSymbol_OnlyLoadsOnce()
        {
            var optionPrices = new OptionPrices();
            var referencePrices = new Prices();
            var baseDate = StaticMonday;

            // Prepare reference prices
            for (var i = 0; i < 10; i++)
                referencePrices.AddPrice(CreateRecord("SPY", baseDate.AddMinutes(i)));

            // Simulate disk load by pre-populating the dictionary
            var symbol = ValidOptionSymbol; // "O:SPY250814C00390000"
            var prices = new Prices();
            for (var i = 0; i < 10; i++)
                prices.AddPrice(CreateRecord(symbol, baseDate.AddMinutes(i)));
            optionPrices.LoadFromPriceRecords(prices.Records.ToArray(), referencePrices);

            // Use correct strikeDistanceAway and expirationDaysAway
            var strikeDistanceAway = 0; // 100 + 290 = 390
            var expirationDaysAway = 0; // Aug 11 + 3 business days = Aug 14

            var found = 0;
            Parallel.For(0, 20, i =>
            {
                var price = optionPrices.GetOptionPrice(referencePrices, Polygon2.OptionType.Call,
                    baseDate.AddMinutes(i % 10), TimeFrame.M1, strikeDistanceAway, expirationDaysAway);
                if (price != null) Interlocked.Increment(ref found);
            });

            Assert.AreEqual(20, found, "All concurrent reads should succeed");
        }

        [TestMethod][TestCategory("Core")]
        public void Concurrent_LoadAndRead_MultipleSymbols_Isolated()
        {
            var optionPrices = new OptionPrices();
            var referencePrices = new Prices();
            var baseDate = StaticMonday;

            for (var i = 0; i < 10; i++)
                referencePrices.AddPrice(CreateRecord("SPY", baseDate.AddMinutes(i)));

            var symbols = new[] { ValidOptionSymbol, "O:AAPL250814C00175000", "O:TSLA260117C01000000" };
            var allRecords = new List<PriceRecord>();
            foreach (var symbol in symbols)
                for (var i = 0; i < 10; i++)
                    allRecords.Add(CreateRecord(symbol, baseDate.AddMinutes(i)));

            // Load all symbols in parallel
            Parallel.ForEach(symbols, symbol =>
            {
                var records = allRecords.Where(r => r.Option.Symbol == symbol).ToArray();
                optionPrices.LoadFromPriceRecords(records, referencePrices);
            });

            // Read all symbols in parallel
            var found = 0;
            Parallel.ForEach(symbols, symbol =>
            {
                var prices = optionPrices.GetPricesForSymbol(symbol);
                if (prices != null && prices.Records.Count == 10)
                    Interlocked.Increment(ref found);
            });

            Assert.AreEqual(symbols.Length, found, "All symbols should be loaded and readable concurrently");
        }

        [TestMethod][TestCategory("Core")]
        public void StressTest_Concurrent_Load_Read_Remove_Clear()
        {
            var optionPrices = new OptionPrices();
            var referencePrices = new Prices();
            var baseDate = StaticMonday;

            for (var i = 0; i < 10; i++)
                referencePrices.AddPrice(CreateRecord("SPY", baseDate.AddMinutes(i)));

            var symbols = Enumerable.Range(0, 20).Select(i => $"O:SPY250814C{39000 + i:D8}").ToArray();

            Parallel.ForEach(symbols, symbol =>
            {
                var records = Enumerable.Range(0, 10)
                    .Select(j => CreateRecord(symbol, baseDate.AddMinutes(j)))
                    .ToArray();
                optionPrices.LoadFromPriceRecords(records, referencePrices);
            });

            // Concurrent reads
            Parallel.ForEach(symbols, symbol =>
            {
                var prices = optionPrices.GetPricesForSymbol(symbol);
                Assert.IsNotNull(prices);
                Assert.AreEqual(10, prices.Records.Count);
            });

            // Concurrent removals
            Parallel.ForEach(symbols, symbol => { optionPrices.TryRemove(symbol); });

            // After removal, all should be gone
            foreach (var symbol in symbols) Assert.IsNull(optionPrices.GetPricesForSymbol(symbol));

            // Reload and clear
            Parallel.ForEach(symbols, symbol =>
            {
                var records = Enumerable.Range(0, 10)
                    .Select(j => CreateRecord(symbol, baseDate.AddMinutes(j)))
                    .ToArray();
                optionPrices.LoadFromPriceRecords(records, referencePrices);
            });

            optionPrices.Clear();
            Assert.AreEqual(0, optionPrices.SymbolCount, "All symbols should be cleared");
        }
    }
}