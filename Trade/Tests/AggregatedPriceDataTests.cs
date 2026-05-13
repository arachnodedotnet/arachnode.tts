using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class AggregatedPriceDataTests
    {
        private static PriceRecord CreateOptionRecord(DateTime dt, DateTime? expiration, double close = 100,
            bool isComplete = true)
        {
            return new PriceRecord(
                dt, TimeFrame.M15,
                close, close, close, close,
                volume: 1,
                wap: close,
                count: 1,
                option: new Ticker
                {
                    Symbol = "O:SPY240814C00390000",
                    IsOption = true,
                    ExpirationDate = expiration,
                    UnderlyingSymbol = "SPY"
                },
                isComplete: isComplete
            );
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetByTimestampForOption_DictionaryHit_ReturnsRecord()
        {
            var agg = new AggregatedPriceData(TimeFrame.M1, true);
            var dt = new DateTime(2025, 1, 3, 11, 0, 0);
            var expiration = dt.AddHours(2);
            var rec = CreateOptionRecord(dt, expiration);

            agg.AddOrUpdate(rec);

            var result = agg.GetByTimestampForOptions(dt);
            Assert.IsNotNull(result);
            Assert.AreEqual(dt, result.DateTime);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetByTimestampForOption_FallbackToSortedList_ReturnsRecord()
        {
            var agg = new AggregatedPriceData(TimeFrame.M1, true);
            var dt1 = new DateTime(2025, 1, 3, 9, 45, 0);
            var dt2 = new DateTime(2025, 1, 3, 12, 45, 0);
            var expiration = dt2.AddHours(1);

            agg.AddOrUpdate(CreateOptionRecord(dt1, expiration));
            agg.AddOrUpdate(CreateOptionRecord(dt2, expiration));

            var queryTime = new DateTime(2025, 1, 3, 11, 0, 0); // No direct record
            var result = agg.GetByTimestampForOptions(queryTime);
            Assert.IsNotNull(result);
            Assert.AreEqual(dt1, result.DateTime);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetByTimestampForOption_ExpiredOption_ReturnsNull()
        {
            var agg = new AggregatedPriceData(TimeFrame.M1, true);
            var dt = new DateTime(2025, 1, 3, 9, 45, 0);
            var expiration = dt.AddMinutes(-1); // Already expired

            agg.AddOrUpdate(CreateOptionRecord(dt, expiration));

            var queryTime = new DateTime(2025, 1, 3, 10, 0, 0);
            var result = agg.GetByTimestampForOptions(queryTime);
            Assert.IsNull(result);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetByTimestampForOption_NoMatchingRecord_ReturnsNull()
        {
            var agg = new AggregatedPriceData(TimeFrame.M1, true);
            var queryTime = new DateTime(2025, 1, 3, 10, 0, 0);
            var result = agg.GetByTimestampForOptions(queryTime);
            Assert.IsNull(result);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetByTimestampForOption_NullExpiration_ReturnsNull()
        {
            var agg = new AggregatedPriceData(TimeFrame.M1, true);
            var dt = new DateTime(2025, 1, 3, 9, 45, 0);

            var rec = CreateOptionRecord(dt, null);
            agg.AddOrUpdate(rec);

            var queryTime = new DateTime(2025, 1, 3, 10, 0, 0);
            var result = agg.GetByTimestampForOptions(queryTime);
            Assert.IsNull(result);
        }
    }
}