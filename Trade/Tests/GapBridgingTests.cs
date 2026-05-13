using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class GapBridgingTests
    {
        [TestMethod][TestCategory("Core")]
        public void TestSanityGap1()
        {
            var prices = new Prices();

            // All DateTime values below are in EST (Eastern Standard Time)
            // If your system expects UTC, convert here, but by default we use EST.

            var record1Est = new DateTime(2025, 1, 3, 15, 59, 0, DateTimeKind.Unspecified); // 3:59 PM EST
            var record1 = new PriceRecord(record1Est, TimeFrame.M1, 100.0, 100.5, 99.5, 100.0, volume: 1000, wap: 100.0, count: 100);
            prices.AddPrice(record1);

            var record2Est = new DateTime(2025, 1, 4, 9, 30, 0, DateTimeKind.Unspecified); // 9:30 AM EST
            var record2 = new PriceRecord(record2Est, TimeFrame.M1, 105.0, 105.5, 104.5, 105.0, volume: 1000, wap: 105.0, count: 100);
            prices.AddPrice(record2);

            // If you need to assert or log, always treat these as EST.
        }

        [TestMethod][TestCategory("Core")]
        public void TestSanityGap2()
        {
            var prices = new Prices();

            // All DateTime values below are in EST (Eastern Standard Time)
            // If your system expects UTC, convert here, but by default we use EST.

            var record2Est = new DateTime(2025, 1, 4, 9, 30, 0, DateTimeKind.Unspecified); // 9:30 AM EST
            var record2 = new PriceRecord(record2Est, TimeFrame.M1, 105.0, 105.5, 104.5, 105.0, volume: 1000, wap: 105.0, count: 100);
            prices.AddPrice(record2);

            var record1Est = new DateTime(2025, 1, 3, 15, 59, 0, DateTimeKind.Unspecified); // 3:59 PM EST
            var record1 = new PriceRecord(record1Est, TimeFrame.M1, 100.0, 100.5, 99.5, 100.0, volume: 1000, wap: 100.0, count: 100);
            prices.AddPrice(record1);

            // If you need to assert or log, always treat these as EST.
        }

        [TestMethod][TestCategory("Core")]
        public void TestAutomaticGapBridgeCreation()
        {
            var prices = new Prices();

            // Add market close data for day 1 (Friday 3:59 PM)
            var friday = new DateTime(2025, 1, 3, 16, 14, 0); // 4:15 PM Friday
            var fridayCloseRecord = new PriceRecord(friday, TimeFrame.M1, 100.0, 100.5, 99.5, 99.75, volume: 1000, wap: 100.0, count: 100);
            prices.AddPrice(fridayCloseRecord);

            // Add market open data for day 2 (Monday 9:30 AM) with a gap
            var monday = new DateTime(2025, 1, 6, 9, 30, 0); // 9:30 AM Monday  
            var mondayOpenRecord = new PriceRecord(monday, TimeFrame.M1, 102.0, 102.5, 101.5, 102.25, volume: 1000, wap: 102.0, count: 100);
            prices.AddPrice(mondayOpenRecord);

            // Get M1 data and check for gap bridge
            var m1Data = prices.GetTimeFrame(TimeFrame.M1);

            // Should have 3 records: Friday close, gap bridge, Monday open
            Assert.AreEqual(3, m1Data.Count, "Should have Friday close + gap bridge + Monday open");

            // Verify the gap bridge bar was created
            var gapBridgeBar = m1Data[1]; // Should be the middle record
            Assert.IsTrue(gapBridgeBar.Manufactured, "Gap bridge bar should be marked as manufactured");
            Assert.AreEqual(99.75, gapBridgeBar.Open, 0.001, "Gap bridge open should be Friday's close");
            Assert.AreEqual(102.0, gapBridgeBar.Close, 0.001, "Gap bridge close should be Monday's open");
            Assert.AreEqual(friday.AddMinutes(1), gapBridgeBar.DateTime,
                "Gap bridge should be 1 minute before market open");
            Assert.IsTrue(gapBridgeBar.Debug.Contains("GapBridge"), "Debug should indicate this is a gap bridge");

            Console.WriteLine("Gap bridge test passed!");
            Console.WriteLine(
                $"Friday close: {fridayCloseRecord.Close:F2} at {fridayCloseRecord.DateTime:yyyy-MM-dd HH:mm}");
            Console.WriteLine(
                $"Gap bridge: {gapBridgeBar.Open:F2} -> {gapBridgeBar.Close:F2} at {gapBridgeBar.DateTime:yyyy-MM-dd HH:mm}");
            Console.WriteLine(
                $"Monday open: {mondayOpenRecord.Open:F2} at {mondayOpenRecord.DateTime:yyyy-MM-dd HH:mm}");
        }

        [TestMethod][TestCategory("Core")]
        public void TestNoGapBridgeForSmallGaps()
        {
            var prices = new Prices();

            // Add market close data with small price difference
            var friday = new DateTime(2025, 1, 3, 16, 0, 0);
            var fridayCloseRecord = new PriceRecord(friday, TimeFrame.M1, 100.0, 100.5, 99.5, 100.0, volume: 1000, wap: 100.0, count: 100);
            prices.AddPrice(fridayCloseRecord);

            // Add market open data with minimal gap (less than 0.1% threshold)
            var monday = new DateTime(2025, 1, 6, 9, 30, 0);
            var mondayOpenRecord = new PriceRecord(monday, TimeFrame.M1, 100.05, 100.55, 99.95, 100.25, volume: 1000, wap: 100.25, count: 100);
            prices.AddPrice(mondayOpenRecord);

            var m1Data = prices.GetTimeFrame(TimeFrame.M1);

            // Should have only 2 records (no gap bridge created for small gaps)
            Assert.AreEqual(2, m1Data.Count, "Should have only Friday close + Monday open (no bridge for small gap)");
            Assert.IsFalse(m1Data[0].Manufactured, "Friday close should not be manufactured");
            Assert.IsFalse(m1Data[1].Manufactured, "Monday open should not be manufactured");
        }

        [TestMethod][TestCategory("Core")]
        public void TestNoGapBridgeForNonMarketHours()
        {
            var prices = new Prices();

            // Add data that's not at market close/open times
            var record1 = new PriceRecord(new DateTime(2025, 1, 3, 14, 30, 0), TimeFrame.M5, 100.0, 100.5, 99.5, 100.0, volume: 1000, wap: 100.0,
                count: 100);
            prices.AddPrice(record1);

            var record2 = new PriceRecord(new DateTime(2025, 1, 3, 14, 35, 0), TimeFrame.M5, 105.0, 105.5, 104.5, 105.0, volume: 1000, wap: 105.0,
                count: 100);
            prices.AddPrice(record2);

            var m1Data = prices.GetTimeFrame(TimeFrame.M1);

            // Should have only 2 records (no gap bridge for non-market hours)
            Assert.AreEqual(2, m1Data.Count, "Should have only the 2 original records");
            Assert.IsFalse(m1Data[0].Manufactured, "First record should not be manufactured");
            Assert.IsFalse(m1Data[1].Manufactured, "Second record should not be manufactured");
        }

        [TestMethod][TestCategory("Core")]
        public void TestGapBridgeOnlyForM1TimeFrame()
        {
            var prices = new Prices();

            // Add market close and open with significant gap
            var fridayClose = new PriceRecord(new DateTime(2025, 1, 3, 16, 14, 0), TimeFrame.M1, 100.0, 100.5, 99.5, 99.5, volume: 1000,
                wap: 100.0, count: 100);
            var mondayOpen = new PriceRecord(new DateTime(2025, 1, 6, 9, 30, 0), TimeFrame.M1, 103.0, 103.5, 102.5, 103.0, volume: 1000,
                wap: 103.0, count: 100);

            prices.AddPrice(fridayClose);
            prices.AddPrice(mondayOpen);

            // Check M1 timeframe (should have gap bridge)
            var m1Data = prices.GetTimeFrame(TimeFrame.M1);
            Assert.AreEqual(3, m1Data.Count, "M1 should have gap bridge bar");

            // Check M5 timeframe (should not have gap bridge, only aggregated data)
            var m5Data = prices.GetTimeFrame(TimeFrame.M5);
            // M5 should aggregate the M1 data but not create additional gap bridges
            Assert.IsTrue(m5Data.Count >= 1, "M5 should have at least one aggregated bar");

            Console.WriteLine($"M1 records: {m1Data.Count} (includes gap bridge)");
            Console.WriteLine($"M5 records: {m5Data.Count} (aggregated from M1)");
        }
    }
}