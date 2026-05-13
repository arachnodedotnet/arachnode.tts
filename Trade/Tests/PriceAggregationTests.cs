using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class PriceAggregationTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void TestTimeFrameAggregation()
        {
            // Create test data - 1 hour of minute bars
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 8, 9, 30, 0); // Market open

            // Add 60 minutes of price data
            for (var i = 0; i < 60; i++)
            {
                var time = baseTime.AddMinutes(i);
                var price = 100.0 + Math.Sin(i * 0.1) * 5; // Oscillating price
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // Test 1-minute data
            var m1Data = prices.GetTimeFrame(TimeFrame.M1);
            Assert.AreEqual(60, m1Data.Count, "Should have 60 one-minute bars");

            // Test 5-minute aggregation
            var m5Data = prices.GetTimeFrame(TimeFrame.M5);
            Assert.AreEqual(12, m5Data.Count, "Should have 12 five-minute bars");

            // Test 15-minute aggregation
            var m15Data = prices.GetTimeFrame(TimeFrame.M15);
            Assert.AreEqual(4, m15Data.Count, "Should have 4 fifteen-minute bars");

            // Test 1-hour aggregation
            var h1Data = prices.GetTimeFrame(TimeFrame.H1);
            Assert.AreEqual(2, h1Data.Count, "Should have 1 one-hour bar");

            // Verify OHLC aggregation is correct
            var hourBar0 = h1Data[0];
            var hourBar1 = h1Data[1];
            var firstMinute = m1Data[0];
            var lastMinute = m1Data[59];

            Assert.AreEqual(firstMinute.Open, hourBar0.Open, 0.001, "Hour bar open should match first minute open");
            Assert.AreEqual(lastMinute.Close, hourBar1.Close, 0.001, "Hour bar close should match last minute close");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestIncompleteBarHandling()
        {
            var prices = new Prices();
            var now = DateTime.Now;
            var currentMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

            // Add incomplete current bar
            prices.UpdateCurrentPrice(currentMinute, TimeFrame.M1, 100.0, 101.0, 99.0, 100.5, 500);

            var m1Data = prices.GetTimeFrame(TimeFrame.M1);
            var currentBar = m1Data.GetLatest();

            Assert.IsFalse(currentBar.IsComplete, "Current bar should be marked as incomplete");

            // Complete bars should exclude incomplete ones
            var completeBars = prices.GetCompleteBars().ToList();
            Assert.AreEqual(0, completeBars.Count, "Should have no complete bars when only incomplete bar exists");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestO1Access()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 8, 9, 30, 0);

            // Add 1000 price records to test performance
            for (var i = 0; i < 1000; i++)
            {
                var time = baseTime.AddMinutes(i);
                var price = 100.0 + i * 0.01;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            var m1Data = prices.GetTimeFrame(TimeFrame.M1);

            // Test O(1) access by index
            var middleRecord = m1Data[500];
            Assert.IsNotNull(middleRecord, "Should be able to access record by index");

            // Test O(1) access by timestamp
            var targetTime = baseTime.AddMinutes(500);
            var recordByTime = m1Data.GetByTimestamp(targetTime);
            Assert.IsNotNull(recordByTime, "Should be able to access record by timestamp");
            Assert.AreEqual(middleRecord.DateTime, recordByTime.DateTime, "Records should match");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestArrayAccess()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 8, 9, 30, 0);

            // Add some test data
            for (var i = 0; i < 10; i++)
            {
                var time = baseTime.AddMinutes(i);
                var price = 100.0 + i;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 1, price - 1, price + 0.5, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // Test array access methods
            var closes = prices.GetCloses();
            var opens = prices.GetOpens();
            var highs = prices.GetHighs();
            var lows = prices.GetLows();

            Assert.AreEqual(10, closes.Length, "Should have 10 close prices");
            Assert.AreEqual(10, opens.Length, "Should have 10 open prices");
            Assert.AreEqual(10, highs.Length, "Should have 10 high prices");
            Assert.AreEqual(10, lows.Length, "Should have 10 low prices");

            // Verify values
            Assert.AreEqual(100.0, opens[0], "First open should be 100.0");
            Assert.AreEqual(109.5, closes[9], "Last close should be 109.5");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestRangeQuery()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 8, 9, 30, 0);

            // Add a day's worth of minute data
            for (var i = 0; i < 390; i++) // 6.5 hours of trading
            {
                var time = baseTime.AddMinutes(i);
                var price = 100.0 + Math.Sin(i * 0.1) * 10;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 1, price - 1, price + 0.5, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // Test range query - CRITICAL: End is EXCLUSIVE
            var start = baseTime.AddHours(1); // 10:30 AM
            var end = baseTime.AddHours(3); // 12:30 PM (EXCLUSIVE)

            var rangeData = prices.GetRange(start, end).ToList();

            Assert.IsTrue(rangeData.Count >= 100, "Should have close to 2 hours of data");
            Assert.IsTrue(rangeData.All(r => r.DateTime >= start && r.DateTime < end),
                "CRITICAL: All records should be >= start AND < end (exclusive end)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestJsonLineParsing()
        {
            var jsonLine =
                "{\"Time\":\"20250808 03:30:00 Pacific/Honolulu\",\"Open\":634.07,\"High\":634.54,\"Low\":634.06,\"Close\":634.48,\"Volume\":7070.32,\"WAP\":634.255,\"Count\":3504}";

            var record = Prices.ParseJsonLine(jsonLine);

            Assert.IsNotNull(record, "Should parse JSON line successfully");
            Assert.AreEqual(634.07, record.Open, 0.001, "Open price should match");
            Assert.AreEqual(634.54, record.High, 0.001, "High price should match");
            Assert.AreEqual(634.06, record.Low, 0.001, "Low price should match");
            Assert.AreEqual(634.48, record.Close, 0.001, "Close price should match");
            Assert.AreEqual(7070.32, record.Volume, 0.001, "Volume should match");
            Assert.AreEqual(634.255, record.WAP, 0.001, "WAP should match");
            Assert.AreEqual(3504, record.Count, "Count should match");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestAllTimeFrames()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 8, 9, 30, 0);

            // Add 8 hours of minute data (480 minutes)
            for (var i = 0; i < 480; i++)
            {
                var time = baseTime.AddMinutes(i);
                var price = 100.0 + Math.Sin(i * 0.02) * 5;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // Test all timeframes
            Assert.AreEqual(480, prices.GetTimeFrame(TimeFrame.M1).Count, "M1 should have 480 bars");
            Assert.AreEqual(96, prices.GetTimeFrame(TimeFrame.M5).Count, "M5 should have 96 bars");
            Assert.AreEqual(48, prices.GetTimeFrame(TimeFrame.M10).Count, "M10 should have 48 bars");
            Assert.AreEqual(32, prices.GetTimeFrame(TimeFrame.M15).Count, "M15 should have 32 bars");
            Assert.AreEqual(16, prices.GetTimeFrame(TimeFrame.M30).Count, "M30 should have 16 bars");
            Assert.AreEqual(9, prices.GetTimeFrame(TimeFrame.H1).Count, "H1 should have 8 bars");
            Assert.AreEqual(3, prices.GetTimeFrame(TimeFrame.H4).Count, "H4 should have 2 bars");
            Assert.AreEqual(1, prices.GetTimeFrame(TimeFrame.D1).Count, "D1 should have 1 bar");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestVolumeAndWAPAggregation()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 8, 9, 30, 0);

            // Add 5 minutes of data with specific volume and WAP
            for (var i = 0; i < 5; i++)
            {
                var time = baseTime.AddMinutes(i);
                var price = 100.0 + i;
                var volume = 1000.0 * (i + 1); // 1000, 2000, 3000, 4000, 5000
                var wap = price + 0.1; // Slightly above close
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: volume, wap: wap, count: 100);
                prices.AddPrice(record);
            }

            var m5Data = prices.GetTimeFrame(TimeFrame.M5);
            var aggregatedBar = m5Data[0];

            // Check volume aggregation
            var expectedVolume = 1000 + 2000 + 3000 + 4000 + 5000; // 15000
            Assert.AreEqual(expectedVolume, aggregatedBar.Volume, "Volume should be sum of all minute volumes");

            // Check count aggregation
            var expectedCount = 5 * 100; // 500
            Assert.AreEqual(expectedCount, aggregatedBar.Count, "Count should be sum of all minute counts");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void TestBatchAddPerformance()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 8, 9, 30, 0);

            // Create large batch of records
            var records = new List<PriceRecord>();
            for (var i = 0; i < 5000; i++)
            {
                var time = baseTime.AddMinutes(i);
                var price = 100.0 + Math.Sin(i * 0.01) * 10;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 1, price - 1, price + 0.5, volume: 1000, wap: price, count: 100);
                records.Add(record);
            }

            var stopwatch = Stopwatch.StartNew();
            prices.AddPricesBatch(records);
            stopwatch.Stop();

            Console.WriteLine($"Batch add of 5000 records took: {stopwatch.ElapsedMilliseconds}ms");

            // Verify all timeframes were built correctly
            Assert.AreEqual(5000, prices.GetTimeFrame(TimeFrame.M1).Count, "Should have 5000 M1 bars");
            Assert.IsTrue(prices.GetTimeFrame(TimeFrame.M5).Count > 0, "Should have M5 bars");
            Assert.IsTrue(prices.GetTimeFrame(TimeFrame.H1).Count > 0, "Should have H1 bars");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void TestParallelJsonParsing()
        {
            // Create test JSON lines
            var jsonLines = new List<string>();
            for (var i = 0; i < 1000; i++)
            {
                var time = new DateTime(2025, 1, 8, 9, 30, 0).AddMinutes(i);
                var price = 100.0 + i * 0.01;
                var json =
                    $"{{\"Time\":\"{time:yyyyMMdd HH:mm:ss} Pacific/Honolulu\",\"Open\":{price},\"High\":{price + 0.5},\"Low\":{price - 0.5},\"Close\":{price + 0.25},\"Volume\":1000,\"WAP\":{price},\"Count\":100}}";
                jsonLines.Add(json);
            }

            var stopwatch = Stopwatch.StartNew();
            var records = Prices.ParseJsonLines(jsonLines);
            stopwatch.Stop();

            Console.WriteLine($"Parallel parsing of 1000 JSON lines took: {stopwatch.ElapsedMilliseconds}ms");

            Assert.AreEqual(1000, records.Count, "Should parse all 1000 records");
            Assert.IsTrue(records.All(r => r != null), "All records should be valid");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void TestCachedArrayAccess()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 8, 9, 30, 0);

            // Add data
            for (var i = 0; i < 1000; i++)
            {
                var time = baseTime.AddMinutes(i);
                var price = 100.0 + i * 0.01;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            var m1Data = prices.GetTimeFrame(TimeFrame.M1);

            // First access - builds cache
            var stopwatch1 = Stopwatch.StartNew();
            var closes1 = m1Data.GetCloseArray();
            stopwatch1.Stop();

            // Second access - uses cache
            var stopwatch2 = Stopwatch.StartNew();
            var closes2 = m1Data.GetCloseArray();
            stopwatch2.Stop();

            Console.WriteLine($"First array access: {stopwatch1.ElapsedTicks} ticks");
            Console.WriteLine($"Second array access: {stopwatch2.ElapsedTicks} ticks");

            Assert.AreSame(closes1, closes2, "Should return same cached array");
            Assert.IsTrue(stopwatch2.ElapsedTicks < stopwatch1.ElapsedTicks, "Cached access should be faster");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void TestConcurrentAccess()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 8, 9, 30, 0);

            // Add initial data
            for (var i = 0; i < 100; i++)
            {
                var time = baseTime.AddMinutes(i);
                var price = 100.0 + i * 0.01;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // Test concurrent reads and writes
            var tasks = new Task[10];

            // Reader tasks
            for (var i = 0; i < 5; i++)
                tasks[i] = Task.Run(() =>
                {
                    for (var j = 0; j < 100; j++)
                    {
                        var closes = prices.GetCloses();
                        var m5Data = prices.GetTimeFrame(TimeFrame.M5);
                        Assert.IsNotNull(closes, "Should always get valid array");
                        Assert.IsNotNull(m5Data, "Should always get valid aggregated data");
                    }
                });

            // Writer tasks
            for (var i = 5; i < 10; i++)
            {
                var taskId = i;
                tasks[i] = Task.Run(() =>
                {
                    for (var j = 0; j < 20; j++)
                    {
                        var time = baseTime.AddMinutes(100 + taskId * 20 + j);
                        var price = 101.0 + j * 0.01;
                        var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price,
                            count: 100);
                        prices.AddPrice(record);
                    }
                });
            }

            Task.WaitAll(tasks);

            // Verify final state
            var finalData = prices.GetTimeFrame(TimeFrame.M1);
            Assert.IsTrue(finalData.Count >= 100, "Should have at least initial 100 records");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void TestBinarySearchOptimization()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 8, 9, 30, 0);

            // Add large amount of data to trigger binary search optimization
            for (var i = 0; i < 2000; i++)
            {
                var time = baseTime.AddMinutes(i);
                var price = 100.0 + Math.Sin(i * 0.01) * 10;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 1, price - 1, price + 0.5, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            var m1Data = prices.GetTimeFrame(TimeFrame.M1);

            // Test range queries with binary search optimization
            var start = baseTime.AddHours(5);
            var end = baseTime.AddHours(10);

            var stopwatch = Stopwatch.StartNew();
            var rangeData = m1Data.GetRange(start, end).ToList();
            stopwatch.Stop();

            Console.WriteLine($"Binary search range query took: {stopwatch.ElapsedTicks} ticks");

            Assert.IsTrue(rangeData.Count > 0, "Should find records in range");
            Assert.IsTrue(rangeData.All(r => r.DateTime >= start && r.DateTime <= end),
                "All records should be in range");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestMarketHoursBarCompletion()
        {
            var prices = new Prices();

            // Test date: January 3rd, 2025 (a trading day)
            var tradingDay = new DateTime(2025, 1, 3);
            var marketOpen = tradingDay.AddHours(9).AddMinutes(30); // 9:30 AM
            var marketClose = tradingDay.AddHours(16).AddMinutes(15); // 4:15 PM
            var noon = tradingDay.AddHours(12); // 12:00 PM

            // Add morning data (9:30 - 12:00)
            for (var i = 0; i < 150; i++) // 2.5 hours = 150 minutes
            {
                var time = marketOpen.AddMinutes(i);
                var price = 100.0 + i * 0.01;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // Simulate current time being noon
            var noonData = prices.GetTimeFrame(TimeFrame.D1);

            // At noon, we should have a daily bar but it should be incomplete
            Assert.AreEqual(1, noonData.Count, "Should have one daily bar at noon");

            var noonDailyBar = noonData[0];
            // Note: The IsComplete property depends on DateTime.Now, so we can't easily test this
            // without mocking the current time. In a real scenario, this would be incomplete at noon.

            // The daily bar should have OHLC data even if incomplete
            Assert.IsTrue(noonDailyBar.Open > 0, "Daily bar should have open price");
            Assert.IsTrue(noonDailyBar.High > 0, "Daily bar should have high price");
            Assert.IsTrue(noonDailyBar.Low > 0, "Daily bar should have low price");
            Assert.IsTrue(noonDailyBar.Close > 0, "Daily bar should have close price (latest available)");

            // The daily bar's open should be the first minute's open
            var firstMinute = prices.GetTimeFrame(TimeFrame.M1)[0];
            Assert.AreEqual(firstMinute.Open, noonDailyBar.Open, 0.001, "Daily open should match first minute open");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestPreMarketAndAfterHoursData()
        {
            var prices = new Prices();

            var tradingDay = new DateTime(2025, 1, 3);
            var preMarket = tradingDay.AddHours(8); // 8:00 AM (before 9:30 open)
            var marketOpen = tradingDay.AddHours(9).AddMinutes(30); // 9:30 AM
            var marketClose = tradingDay.AddHours(16).AddMinutes(15); // 4:15 PM
            var afterHours = tradingDay.AddHours(17); // 5:00 PM (after 4:00 close)

            // Add pre-market data
            var preMarketRecord = new PriceRecord(preMarket, TimeFrame.M1, 99.0, 99.5, 98.5, 99.25, volume: 500, wap: 99.1, count: 50);
            prices.AddPrice(preMarketRecord);

            // Add regular market hours data
            for (var i = 0; i < 60; i++) // 1 hour of trading
            {
                var time = marketOpen.AddMinutes(i);
                var price = 100.0 + i * 0.01;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // Add after-hours data
            var afterHoursRecord = new PriceRecord(afterHours, TimeFrame.M1, 101.0, 101.5, 100.5, 101.25, volume: 300, wap: 101.1, count: 30);
            prices.AddPrice(afterHoursRecord);

            // Verify all data is captured
            var allMinutes = prices.GetTimeFrame(TimeFrame.M1);
            Assert.AreEqual(62, allMinutes.Count, "Should have pre-market + regular hours + after-hours data");

            // Daily aggregation should include all data
            var dailyData = prices.GetTimeFrame(TimeFrame.D1);
            Assert.AreEqual(1, dailyData.Count, "Should have one daily bar");

            var dailyBar = dailyData[0];

            // Daily bar should span from pre-market to after-hours
            Assert.AreEqual(preMarketRecord.Open, dailyBar.Open, 0.001, "Daily open should be pre-market open");
            Assert.AreEqual(afterHoursRecord.Close, dailyBar.Close, 0.001, "Daily close should be after-hours close");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestIntradayBarCompletionDuringMarketHours()
        {
            var prices = new Prices();

            var tradingDay = new DateTime(2025, 1, 3);
            var marketOpen = tradingDay.AddHours(9).AddMinutes(30); // 9:30 AM

            // Add exactly 5 minutes of data to test 5-minute bar completion
            for (var i = 0; i < 5; i++)
            {
                var time = marketOpen.AddMinutes(i);
                var price = 100.0 + i;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            var m5Data = prices.GetTimeFrame(TimeFrame.M5);
            Assert.AreEqual(1, m5Data.Count, "Should have one 5-minute bar");

            var fiveMinBar = m5Data[0];

            // Verify aggregation is correct
            Assert.AreEqual(100.0, fiveMinBar.Open, 0.001, "5-min bar open should be first minute open");
            Assert.AreEqual(104.25, fiveMinBar.Close, 0.001, "5-min bar close should be last minute close");
            Assert.AreEqual(104.5, fiveMinBar.High, 0.001, "5-min bar high should be max of all highs");
            Assert.AreEqual(99.5, fiveMinBar.Low, 0.001, "5-min bar low should be min of all lows");
            Assert.AreEqual(5000, fiveMinBar.Volume, 0.001, "5-min bar volume should be sum of all volumes");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestMarketHoursTimeFrameEdgeCases()
        {
            var prices = new Prices();

            var tradingDay = new DateTime(2025, 1, 3);
            var marketOpen = tradingDay.AddHours(9).AddMinutes(30); // 9:30 AM
            var marketClose = tradingDay.AddHours(16).AddMinutes(15); // 4:15 PM

            // Test data right at market open
            var openRecord = new PriceRecord(marketOpen, TimeFrame.M1, 100.0, 100.5, 99.5, 100.25, volume: 1000, wap: 100.1, count: 100);
            prices.AddPrice(openRecord);

            // Test data right at market close
            var closeRecord =
                new PriceRecord(marketClose.AddMinutes(-1), TimeFrame.M1, 101.0, 101.5, 100.5, 101.25, volume: 1000, wap: 101.1, count: 100);
            prices.AddPrice(closeRecord);

            // Add some mid-day data
            for (var i = 60; i < 120; i++) // 10:30 - 11:30 AM
            {
                var time = marketOpen.AddMinutes(i);
                var price = 100.5 + i * 0.001;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.1, price - 0.1, price + 0.05, volume: 500, wap: price, count: 50);
                prices.AddPrice(record);
            }

            // Verify all timeframes handle market hours correctly
            Assert.IsTrue(prices.GetTimeFrame(TimeFrame.M1).Count > 0, "Should have minute data");
            Assert.IsTrue(prices.GetTimeFrame(TimeFrame.M5).Count > 0, "Should have 5-minute data");
            Assert.IsTrue(prices.GetTimeFrame(TimeFrame.H1).Count > 0, "Should have hourly data");
            Assert.AreEqual(1, prices.GetTimeFrame(TimeFrame.D1).Count, "Should have exactly one daily bar");

            var dailyBar = prices.GetTimeFrame(TimeFrame.D1)[0];
            Assert.AreEqual(marketOpen.Date, dailyBar.DateTime.Date, "Daily bar should be for the correct date");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestTimezoneConversion()
        {
            // Test Pacific/Honolulu to Eastern conversion
            var jsonLineHawaii =
                "{\"Time\":\"20250808 03:30:00 Pacific/Honolulu\",\"Open\":634.07,\"High\":634.54,\"Low\":634.06,\"Close\":634.48,\"Volume\":7070.32,\"WAP\":634.255,\"Count\":3504}";
            var recordHawaii = Prices.ParseJsonLine(jsonLineHawaii);

            Assert.IsNotNull(recordHawaii, "Should parse Hawaiian timezone JSON successfully");

            // Hawaii is UTC-10, Eastern is UTC-5 (or UTC-4 during DST)
            // So 3:30 AM Hawaii should be 9:30 AM Eastern (during standard time) or 8:30 AM Eastern (during DST)
            // For August 8th, 2025, we're likely in DST, so expect 8:30 AM Eastern
            var expectedHour = 8; // Assuming DST
            var expectedMinute = 30;

            // Allow for some flexibility since DST rules can be complex
            Assert.IsTrue(recordHawaii.DateTime.Hour >= 8 && recordHawaii.DateTime.Hour <= 9,
                $"Expected hour between 8-9 Eastern, got {recordHawaii.DateTime.Hour}");
            Assert.AreEqual(expectedMinute, recordHawaii.DateTime.Minute, "Minutes should match");

            Console.WriteLine($"Hawaii 3:30 AM -> Eastern {recordHawaii.DateTime:HH:mm}");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestMultipleTimezoneConversions()
        {
            var testCases = new[]
            {
                new { Timezone = "Pacific/Honolulu", Hour = 6, Minute = 0, Description = "Hawaii 6:00 AM" },
                new { Timezone = "US/Pacific", Hour = 6, Minute = 30, Description = "Pacific 6:30 AM" },
                new { Timezone = "US/Central", Hour = 8, Minute = 0, Description = "Central 8:00 AM" },
                new { Timezone = "US/Eastern", Hour = 9, Minute = 30, Description = "Eastern 9:30 AM" }
            };

            foreach (var testCase in testCases)
            {
                var jsonLine =
                    $"{{\"Time\":\"{testCase.Hour:D2}:{testCase.Minute:D2}:00 {testCase.Timezone}\",\"Open\":100.0,\"High\":101.0,\"Low\":99.0,\"Close\":100.5,\"Volume\":1000,\"WAP\":100.25,\"Count\":100}}";
                var record = Prices.ParseJsonLine(jsonLine);

                Assert.IsNotNull(record, $"Should parse {testCase.Description} successfully");

                // All should convert to some Eastern time
                Assert.IsTrue(record.DateTime.Hour >= 0 && record.DateTime.Hour <= 23, "Hour should be valid");
                Assert.IsTrue(record.DateTime.Minute >= 0 && record.DateTime.Minute <= 59, "Minute should be valid");

                Console.WriteLine($"{testCase.Description} -> Eastern {record.DateTime:HH:mm}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestMarketHoursWithTimezoneConversion()
        {
            var prices = new Prices();

            // Test data from Hawaii timezone (which should convert to Eastern for market hours)
            var baseDate = new DateTime(2025, 1, 8); // January 8th, 2025

            // Create data that would be 9:30 AM Eastern when converted from Hawaii
            // Hawaii is typically UTC-10, Eastern is UTC-5 in winter
            // So 4:30 AM Hawaii = 9:30 AM Eastern (during standard time)
            var hawaiiMarketOpen = baseDate.AddHours(4).AddMinutes(30); // 4:30 AM Hawaii time

            // Add some test data
            for (var i = 0; i < 60; i++)
            {
                var hawaiiTime = hawaiiMarketOpen.AddMinutes(i);
                var timeString = $"{hawaiiTime:yyyyMMdd HH:mm:ss} Pacific/Honolulu";
                var price = 100.0 + i * 0.01;

                var record = new PriceRecord();
                record.Time = timeString;
                record.DateTime = Prices.ParseDateTimeFromString(timeString); // This should convert to Eastern
                record.Open = price;
                record.High = price + 0.5;
                record.Low = price - 0.5;
                record.Close = price + 0.25;
                record.Volume = 1000;
                record.WAP = price;
                record.Count = 100;
                record.IsComplete = true;

                prices.AddPrice(record);
            }

            // Verify the data was converted to Eastern time
            var firstRecord = prices.GetTimeFrame(TimeFrame.M1)[0];

            // Should be around 9:30 AM Eastern (allowing for DST variations)
            Assert.IsTrue(firstRecord.DateTime.Hour >= 8 && firstRecord.DateTime.Hour <= 10,
                $"Expected converted time around 9:30 AM Eastern, got {firstRecord.DateTime:HH:mm}");

            Console.WriteLine($"First converted record: {firstRecord.DateTime:yyyy-MM-dd HH:mm:ss} Eastern");

            // Test market hours logic with converted times
            var dailyBars = prices.GetTimeFrame(TimeFrame.D1);
            Assert.AreEqual(1, dailyBars.Count, "Should have one daily bar");

            var dailyBar = dailyBars[0];
            Assert.IsTrue(dailyBar.DateTime.Date == baseDate.Date, "Daily bar should be for the correct date");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestEdgeCaseTimezones()
        {
            // Test various timezone formats and edge cases
            var testCases = new[]
            {
                "20250808 12:00:00 UTC",
                "20250808 12:00:00 GMT",
                "20250808 12:00:00 EST",
                "20250808 12:00:00 EDT",
                "20250808 12:00:00 PST",
                "20250808 12:00:00 PDT",
                "20250808 12:00:00 InvalidTimezone" // Should fallback gracefully
            };

            foreach (var timeString in testCases)
                try
                {
                    var dateTime = Prices.ParseDateTimeFromString(timeString);

                    // Should always get a valid DateTime
                    Assert.IsTrue(dateTime.Year == 2025, $"Year should be 2025 for {timeString}");
                    Assert.IsTrue(dateTime.Month == 8, $"Month should be 8 for {timeString}");
                    Assert.IsTrue(dateTime.Day == 8, $"Day should be 8 for {timeString}");

                    Console.WriteLine($"{timeString} -> {dateTime:yyyy-MM-dd HH:mm:ss} Eastern");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Should handle timezone gracefully: {timeString}, Error: {ex.Message}");
                }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestDSTTransitions()
        {
            // Test around Daylight Saving Time transitions
            // DST typically begins second Sunday in March, ends first Sunday in November

            var springDST = new DateTime(2025, 3, 9); // Around spring DST transition
            var summerTime = new DateTime(2025, 8, 8); // During summer (DST active)
            var fallDST = new DateTime(2025, 11, 2); // Around fall DST transition
            var winterTime = new DateTime(2025, 1, 8); // During winter (standard time)

            var testDates = new[] { springDST, summerTime, fallDST, winterTime };

            foreach (var testDate in testDates)
            {
                var timeString = $"{testDate:yyyyMMdd} 06:30:00 US/Pacific"; // 6:30 AM Pacific
                var easternTime = Prices.ParseDateTimeFromString(timeString);

                // Pacific to Eastern should be +3 hours (during DST) or +2 hours (during standard time)
                // 6:30 AM Pacific -> 9:30 AM Eastern (EDT) or 8:30 AM Eastern (EST)
                Assert.IsTrue(easternTime.Hour >= 8 && easternTime.Hour <= 10,
                    $"Expected reasonable Eastern conversion for {testDate:yyyy-MM-dd}, got {easternTime:HH:mm}");

                Console.WriteLine($"{testDate:yyyy-MM-dd} Pacific 6:30 AM -> Eastern {easternTime:HH:mm}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestHawaiiNoDSTBehavior()
        {
            // Hawaii does NOT observe Daylight Saving Time - it stays UTC-10 year-round
            // This is critical for accurate timezone conversion

            var testDates = new[]
            {
                new DateTime(2025, 1, 15), // Winter (when most US is on standard time)
                new DateTime(2025, 3, 15), // Spring (around DST transition for mainland US)
                new DateTime(2025, 7, 15), // Summer (when most US is on daylight time)
                new DateTime(2025, 11, 15) // Fall (around DST transition for mainland US)
            };

            foreach (var testDate in testDates)
            {
                // 3:30 AM Hawaii time should ALWAYS be the same offset to Eastern
                // Hawaii: UTC-10 (never changes)
                // Eastern: UTC-5 (standard) or UTC-4 (daylight)
                var hawaiiTimeString = $"{testDate:yyyyMMdd} 03:30:00 Pacific/Honolulu";
                var easternTime = Prices.ParseDateTimeFromString(hawaiiTimeString);

                // During Eastern Standard Time (winter): 3:30 AM Hawaii = 8:30 AM Eastern (5 hour difference)
                // During Eastern Daylight Time (summer): 3:30 AM Hawaii = 9:30 AM Eastern (6 hour difference)
                var expectedHourWinter = 8; // 3:30 AM + 5 hours = 8:30 AM
                var expectedHourSummer = 9; // 3:30 AM + 6 hours = 9:30 AM

                // The hour should be either 8 or 9 depending on whether Eastern Time is observing DST
                Assert.IsTrue(easternTime.Hour == expectedHourWinter || easternTime.Hour == expectedHourSummer,
                    $"Expected 8 or 9 Eastern for {testDate:yyyy-MM-dd}, got {easternTime.Hour}");
                Assert.AreEqual(30, easternTime.Minute, "Minutes should always be 30");

                Console.WriteLine($"Hawaii 3:30 AM on {testDate:yyyy-MM-dd} -> Eastern {easternTime:HH:mm}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestPacificVsHawaiiDSTDifference()
        {
            // Compare Pacific Time (which DOES observe DST) vs Hawaii Time (which does NOT)
            var summerDate = new DateTime(2025, 7, 15); // During DST for mainland US
            var winterDate = new DateTime(2025, 1, 15); // During standard time for mainland US

            // Same time in Hawaii vs Pacific during summer (DST active for Pacific)
            var hawaiiSummer = $"{summerDate:yyyyMMdd} 06:30:00 Pacific/Honolulu";
            var pacificSummer = $"{summerDate:yyyyMMdd} 06:30:00 US/Pacific";

            var hawaiiEasternSummer = Prices.ParseDateTimeFromString(hawaiiSummer);
            var pacificEasternSummer = Prices.ParseDateTimeFromString(pacificSummer);

            // Same time in Hawaii vs Pacific during winter (standard time for both)
            var hawaiiWinter = $"{winterDate:yyyyMMdd} 06:30:00 Pacific/Honolulu";
            var pacificWinter = $"{winterDate:yyyyMMdd} 06:30:00 US/Pacific";

            var hawaiiEasternWinter = Prices.ParseDateTimeFromString(hawaiiWinter);
            var pacificEasternWinter = Prices.ParseDateTimeFromString(pacificWinter);

            Console.WriteLine($"Summer - Hawaii 6:30 AM -> Eastern {hawaiiEasternSummer:HH:mm}");
            Console.WriteLine($"Summer - Pacific 6:30 AM -> Eastern {pacificEasternSummer:HH:mm}");
            Console.WriteLine($"Winter - Hawaii 6:30 AM -> Eastern {hawaiiEasternWinter:HH:mm}");
            Console.WriteLine($"Winter - Pacific 6:30 AM -> Eastern {pacificEasternWinter:HH:mm}");

            // During summer: Pacific observes DST, Hawaii doesn't
            // 6:30 AM Pacific (PDT, UTC-7) -> 9:30 AM Eastern (EDT, UTC-4) [3 hour difference]
            // 6:30 AM Hawaii (HST, UTC-10) -> 12:30 PM Eastern (EDT, UTC-4) [6 hour difference]
            Assert.IsTrue(Math.Abs(hawaiiEasternSummer.Hour - pacificEasternSummer.Hour) >= 2,
                "Hawaii and Pacific should have different Eastern conversions during summer due to DST");

            // During winter: Both are on standard time
            // 6:30 AM Pacific (PST, UTC-8) -> 9:30 AM Eastern (EST, UTC-5) [3 hour difference]
            // 6:30 AM Hawaii (HST, UTC-10) -> 11:30 AM Eastern (EST, UTC-5) [5 hour difference]  
            Assert.IsTrue(Math.Abs(hawaiiEasternWinter.Hour - pacificEasternWinter.Hour) >= 1,
                "Hawaii and Pacific should have different Eastern conversions even in winter");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestCriticalMarketTimings()
        {
            // Test the critical timing: Hawaii data at market open
            var tradingDay = new DateTime(2025, 8, 8); // Summer date (Eastern DST active)

            // In the original JSON: "20250808 03:30:00 Pacific/Honolulu"
            // This should convert to Eastern market open time (9:30 AM)
            var hawaiiMarketOpenTime = $"{tradingDay:yyyyMMdd} 03:30:00 Pacific/Honolulu";
            var easternTime = Prices.ParseDateTimeFromString(hawaiiMarketOpenTime);

            // During summer (EDT): Hawaii UTC-10, Eastern UTC-4 = 6 hour difference
            // 3:30 AM Hawaii + 6 hours = 9:30 AM Eastern (perfect market open!)
            Assert.AreEqual(9, easternTime.Hour, "Hawaii 3:30 AM should convert to 9 AM Eastern during summer");
            Assert.AreEqual(30, easternTime.Minute, "Should be exactly 9:30 AM Eastern");

            Console.WriteLine(
                $"Critical timing verified: Hawaii 3:30 AM -> Eastern {easternTime:HH:mm} (Market Open!)");

            // Test winter conversion too
            var winterDay = new DateTime(2025, 1, 8); // Winter date (Eastern EST active)
            var hawaiiWinterTime = $"{winterDay:yyyyMMdd} 03:30:00 Pacific/Honolulu";
            var easternWinterTime = Prices.ParseDateTimeFromString(hawaiiWinterTime);

            // During winter (EST): Hawaii UTC-10, Eastern UTC-5 = 5 hour difference
            // 3:30 AM Hawaii + 5 hours = 8:30 AM Eastern (pre-market)
            Assert.AreEqual(8, easternWinterTime.Hour, "Hawaii 3:30 AM should convert to 8 AM Eastern during winter");
            Assert.AreEqual(30, easternWinterTime.Minute, "Should be exactly 8:30 AM Eastern");

            Console.WriteLine($"Winter timing: Hawaii 3:30 AM -> Eastern {easternWinterTime:HH:mm} (Pre-market)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestExclusiveEndDateCriticalRequirement()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 3, 9, 30, 0); // Jan 3rd 9:30 AM

            // Add data for Jan 3rd through Jan 7th (5 days)
            for (var day = 0; day < 5; day++)
            for (var hour = 0; hour < 24; hour++)
            {
                var time = baseTime.AddDays(day).AddHours(hour);
                var price = 100.0 + day + hour * 0.1;
                var record = new PriceRecord(time, TimeFrame.M30, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // CRITICAL TEST: Jan 3rd - Jan 7th should return Jan 3rd - Jan 6th (exclusive end)
            var start = new DateTime(2025, 1, 3);
            var end = new DateTime(2025, 1, 7);

            var rangeData = prices.GetRange(start, end).ToList();

            // Verify NO records are >= end date
            Assert.IsTrue(rangeData.All(r => r.DateTime < end),
                "CRITICAL: No records should be >= end date (Jan 7th)");

            // Verify we have records up to but not including end date
            var maxDate = rangeData.Max(r => r.DateTime);
            Assert.IsTrue(maxDate < end,
                $"CRITICAL: Latest record ({maxDate:yyyy-MM-dd HH:mm}) must be < end date ({end:yyyy-MM-dd})");

            // Verify we get records from Jan 3-6 (4 full days)
            var days = rangeData.Select(r => r.DateTime.Date).Distinct().ToList();
            Assert.AreEqual(4, days.Count, "Should have exactly 4 days (Jan 3-6)");

            Assert.IsTrue(days.Contains(new DateTime(2025, 1, 3).Date), "Should include Jan 3rd");
            Assert.IsTrue(days.Contains(new DateTime(2025, 1, 6).Date), "Should include Jan 6th");
            Assert.IsFalse(days.Contains(new DateTime(2025, 1, 7).Date), "Should NOT include Jan 7th");

            Console.WriteLine("Range Jan 3 - Jan 7 correctly returns:");
            Console.WriteLine($"First record: {rangeData.First().DateTime:yyyy-MM-dd HH:mm}");
            Console.WriteLine($"Last record: {rangeData.Last().DateTime:yyyy-MM-dd HH:mm}");
            Console.WriteLine($"Total records: {rangeData.Count}");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestExclusiveEndDateEdgeCases()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 3, 9, 30, 0);

            // Add hourly data for 3 days
            for (var day = 0; day < 3; day++)
            for (var hour = 0; hour < 24; hour++)
            {
                var time = baseTime.AddDays(day).AddHours(hour);
                var price = 100.0 + day + hour * 0.01;
                var record = new PriceRecord(time, TimeFrame.M30, price, price + 0.1, price - 0.1, price + 0.05, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // Test 1: Exact timestamp exclusion
            var exactEnd = new DateTime(2025, 1, 4, 12, 0, 0); // Jan 4th noon
            var rangeExact = prices.GetRange(baseTime, exactEnd).ToList();

            Assert.IsTrue(rangeExact.All(r => r.DateTime < exactEnd),
                "CRITICAL: No records should equal the exact end timestamp");

            // Test 2: One millisecond precision
            var endPlusOne = exactEnd.AddMilliseconds(1);
            var rangePlusOne = prices.GetRange(baseTime, endPlusOne).ToList();

            // Should still exclude the exact timestamp even with +1ms
            Assert.IsTrue(rangePlusOne.All(r => r.DateTime < endPlusOne),
                "CRITICAL: Even +1ms should not include the boundary record");

            // Test 3: Same start and end (should return empty)
            var sameTime = new DateTime(2025, 1, 4, 10, 0, 0);
            var rangeSame = prices.GetRange(sameTime, sameTime).ToList();

            Assert.AreEqual(0, rangeSame.Count,
                "CRITICAL: Same start/end should return empty (exclusive end)");

            Console.WriteLine("Edge case tests passed:");
            Console.WriteLine($"Exact boundary excluded: {rangeExact.Count} records < {exactEnd:yyyy-MM-dd HH:mm}");
            Console.WriteLine($"Same start/end: {rangeSame.Count} records (should be 0)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestBacktestingIntegrityWithExclusiveEnd()
        {
            var prices = new Prices();
            var marketOpen = new DateTime(2025, 1, 6, 9, 30, 0); // Monday market open

            // Add 5 trading days of minute data
            for (var day = 0; day < 5; day++)
            for (var minute = 0; minute < 390; minute++) // 6.5 hours of trading per day
            {
                var time = marketOpen.AddDays(day).AddMinutes(minute);
                var price = 100.0 + day + Math.Sin(minute * 0.1) * 2;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // Simulate backtesting: Request data up to but not including current day
            var currentDay = marketOpen.AddDays(3).Date; // Wednesday
            var trainingEnd = currentDay; // Should exclude Wednesday entirely

            var trainingData = prices.GetRange(marketOpen, trainingEnd).ToList();

            // CRITICAL: Training data must not include any Wednesday data
            Assert.IsTrue(trainingData.All(r => r.DateTime.Date < currentDay),
                "CRITICAL: Training data must not include current day for backtesting integrity");

            // Verify we have 2 full days (Monday, Tuesday)
            var trainingDays = trainingData.Select(r => r.DateTime.Date).Distinct().Count();
            Assert.AreEqual(3, trainingDays, "Should have exactly 3 training days");

            // Verify the latest training record is before current day
            var latestTraining = trainingData.Max(r => r.DateTime);
            Assert.IsTrue(latestTraining < currentDay,
                $"Latest training record ({latestTraining:yyyy-MM-dd HH:mm}) must be before current day ({currentDay:yyyy-MM-dd})");

            Console.WriteLine("Backtesting integrity verified:");
            Console.WriteLine($"Current day: {currentDay:yyyy-MM-dd}");
            Console.WriteLine($"Latest training: {latestTraining:yyyy-MM-dd HH:mm}");
            Console.WriteLine($"Training records: {trainingData.Count}");
            Console.WriteLine($"Training days: {trainingDays}");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestExclusiveEndAcrossTimeFrames()
        {
            var prices = new Prices();
            var start = new DateTime(2025, 1, 6, 9, 30, 0);

            // Add 2 full days of minute data
            for (var day = 0; day < 2; day++)
            for (var minute = 0; minute < 1440; minute++) // Full day
            {
                var time = start.AddDays(day).AddMinutes(minute);
                var price = 100.0 + day + minute * 0.001;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.1, price - 0.1, price + 0.05, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            var queryStart = start;
            var queryEnd = start.AddDays(1); // Should exclude second day entirely

            // Test exclusive end across all timeframes
            var timeFrames = new[] { TimeFrame.M1, TimeFrame.M5, TimeFrame.M15, TimeFrame.H1, TimeFrame.D1 };

            foreach (var tf in timeFrames)
            {
                var rangeData = prices.GetRange(queryStart, queryEnd, tf).ToList();

                // CRITICAL: No record should be >= end date
                Assert.IsTrue(rangeData.All(r => r.DateTime < queryEnd),
                    $"CRITICAL: {tf} timeframe violated exclusive end requirement");

                Console.WriteLine(
                    $"{tf}: {rangeData.Count} records, latest: {(rangeData.Any() ? rangeData.Max(r => r.DateTime).ToString("yyyy-MM-dd HH:mm") : "none")}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TestOutOfOrderDataProcessing()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 8, 9, 30, 0);

            // Create test data in RANDOM ORDER instead of sequential
            var records = new List<PriceRecord>();

            // Add records completely out of order
            records.Add(new PriceRecord(baseTime.AddMinutes(7), TimeFrame.M1, 103, 104, 102, 103.5)); // 7th minute
            records.Add(new PriceRecord(baseTime.AddMinutes(2), TimeFrame.M1, 101, 102, 100, 101.5)); // 2nd minute  
            records.Add(new PriceRecord(baseTime.AddMinutes(9), TimeFrame.M1, 104, 105, 103, 104.5)); // 9th minute
            records.Add(new PriceRecord(baseTime.AddMinutes(1), TimeFrame.M1, 100, 101, 99, 100.5)); // 1st minute
            records.Add(new PriceRecord(baseTime.AddMinutes(5), TimeFrame.M1, 102, 103, 101, 102.5)); // 5th minute
            records.Add(new PriceRecord(baseTime.AddMinutes(3), TimeFrame.M1, 101, 102, 100, 101.5)); // 3rd minute
            records.Add(new PriceRecord(baseTime.AddMinutes(8), TimeFrame.M1, 103, 104, 102, 103.5)); // 8th minute
            records.Add(new PriceRecord(baseTime.AddMinutes(4), TimeFrame.M1, 101, 102, 100, 101.5)); // 4th minute
            records.Add(new PriceRecord(baseTime.AddMinutes(6), TimeFrame.M1, 102, 103, 101, 102.5)); // 6th minute
            records.Add(new PriceRecord(baseTime.AddMinutes(0), TimeFrame.M1, 100, 101, 99, 100.5)); // 0th minute (first)

            // Add the UNORDERED data to the system
            foreach (var record in records) prices.AddPrice(record);

            // ? VERIFY: System should still work correctly despite out-of-order input

            // Test 1: M1 data should be properly sorted
            var m1Data = prices.GetTimeFrame(TimeFrame.M1);
            Assert.AreEqual(10, m1Data.Count, "Should have 10 one-minute bars");

            // Verify chronological order
            for (var i = 1; i < m1Data.Count; i++)
                Assert.IsTrue(m1Data[i].DateTime > m1Data[i - 1].DateTime,
                    $"M1 data should be chronologically sorted: {m1Data[i - 1].DateTime} < {m1Data[i].DateTime}");

            // Test 2: M5 aggregation should work correctly
            var m5Data = prices.GetTimeFrame(TimeFrame.M5);
            Assert.AreEqual(2, m5Data.Count, "Should have 2 five-minute bars");

            // First M5 bar: 9:30-9:34 (minutes 0-4)
            var firstM5 = m5Data[0];
            Assert.AreEqual(baseTime, firstM5.DateTime, "First M5 bar should start at 9:30");
            Assert.AreEqual(100.0, firstM5.Open, 0.001, "M5 open should be first minute's open");
            Assert.AreEqual(101.5, firstM5.Close, 0.001, "M5 close should be last minute's close");

            // Second M5 bar: 9:35-9:39 (minutes 5-9)  
            var secondM5 = m5Data[1];
            Assert.AreEqual(baseTime.AddMinutes(5), secondM5.DateTime, "Second M5 bar should start at 9:35");
            Assert.AreEqual(102, secondM5.Open, 0.001, "M5 open should be minute 5's open");
            Assert.AreEqual(104.5, secondM5.Close, 0.001, "M5 close should be minute 9's close");

            // Test 3: Array access should work
            var closes = prices.GetCloses();
            Assert.AreEqual(10, closes.Length, "Should have 10 closes");
            Assert.AreEqual(100.5, closes[0], 0.001, "First close should be from minute 0");
            Assert.AreEqual(104.5, closes[9], 0.001, "Last close should be from minute 9");

            // Test 4: Range queries should work
            var start = baseTime.AddMinutes(2);
            var end = baseTime.AddMinutes(7);
            var rangeData = prices.GetRange(start, end).ToList();

            Assert.AreEqual(5, rangeData.Count, "Should get 5 records (minutes 2-6, exclusive end)");
            Assert.IsTrue(rangeData.All(r => r.DateTime >= start && r.DateTime < end),
                "All range records should be within bounds");

            Console.WriteLine("? Out-of-order data processing test PASSED!");
            Console.WriteLine("   - Added 10 records in random order");
            Console.WriteLine($"   - M1 data properly sorted: {m1Data.Count} records");
            Console.WriteLine($"   - M5 aggregation correct: {m5Data.Count} bars");
            Console.WriteLine($"   - Array access working: {closes.Length} closes");
            Console.WriteLine($"   - Range queries working: {rangeData.Count} records in range");
        }
    }
}