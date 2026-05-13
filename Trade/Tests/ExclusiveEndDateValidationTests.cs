using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class ExclusiveEndDateValidationTests
    {
        [TestMethod][TestCategory("Core")]
        public void ExclusiveEndDate_BasicRequirement_NeverIncludesEndDate()
        {
            var prices = new Prices();
            // ✅ FIXED: Use clean hour boundary to avoid incomplete bar issues
            var baseTime = new DateTime(2025, 1, 1, 9, 0, 0); // 9:00 AM (start of hour)

            // ✅ FIXED: Add 10 days of data with proper boundaries
            for (var day = 0; day < 10; day++)
                // Generate data every hour for complete hourly boundaries
            for (var hour = 0; hour < 24; hour++)
            {
                var time = baseTime.AddDays(day).AddHours(hour);
                var price = 100.0 + day + hour * 0.01;
                var record = new PriceRecord(time, TimeFrame.H1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // Test the fundamental requirement: Jan 1-5 should return Jan 1-4
            var start = new DateTime(2025, 1, 1);
            var end = new DateTime(2025, 1, 5);

            var rangeData = prices.GetRange(start, end).ToList();

            // CRITICAL VALIDATION: NO record should be >= end date
            Assert.IsTrue(rangeData.All(r => r.DateTime < end),
                "CRITICAL VIOLATION: Found records >= end date");

            // Should have exactly 4 days of data (Jan 1, 2, 3, 4)
            var uniqueDays = rangeData.Select(r => r.DateTime.Date).Distinct().ToList();
            Assert.AreEqual(4, uniqueDays.Count, "Should have exactly 4 days");

            // Verify specific days
            Assert.IsTrue(uniqueDays.Contains(new DateTime(2025, 1, 1).Date), "Should include Jan 1");
            Assert.IsTrue(uniqueDays.Contains(new DateTime(2025, 1, 4).Date), "Should include Jan 4");
            Assert.IsFalse(uniqueDays.Contains(new DateTime(2025, 1, 5).Date), "Should NOT include Jan 5");

            Console.WriteLine("Range Jan 1-5 correctly excludes end date:");
            Console.WriteLine($"Records: {rangeData.Count}, Days: {uniqueDays.Count}");
            Console.WriteLine($"Latest record: {rangeData.Max(r => r.DateTime):yyyy-MM-dd HH:mm:ss}");
        }

        [TestMethod][TestCategory("Core")]
        public void ExclusiveEndDate_AllTimeFrames_ConsistentBehavior()
        {
            var prices = new Prices();
            // ✅ FIXED: Use top of hour (9:00 AM) to avoid incomplete bar issues
            var start = new DateTime(2025, 1, 1, 9, 0, 0); // 9:00 AM (start of hour)

            // ✅ FIXED: Add 7 days of data with proper hourly boundaries
            for (var day = 0; day < 7; day++)
                // Generate data from 9:00 AM to 4:15 PM (7 hours = 420 minutes)
            for (var minute = 0; minute < 420; minute++) // 7 hours per day
            {
                var time = start.AddDays(day).AddMinutes(minute);
                var price = 100.0 + day + minute * 0.0001;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.1, price - 0.1, price + 0.05, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            var queryStart = start;
            // ✅ FIXED: Use midnight boundary for clean day separation
            var queryEnd = start.AddDays(3).Date; // Midnight of day 3 (excludes day 3,4,5,6)

            var timeFrames = new[]
                { TimeFrame.M1, TimeFrame.M5, TimeFrame.M15, TimeFrame.M30, TimeFrame.H1, TimeFrame.H4, TimeFrame.D1 };

            foreach (var timeFrame in timeFrames)
            {
                var rangeData = prices.GetRange(queryStart, queryEnd, timeFrame).ToList();

                // CRITICAL: No record should be >= end date for ANY timeframe
                var violatingRecords = rangeData.Where(r => r.DateTime >= queryEnd).ToList();
                Assert.AreEqual(0, violatingRecords.Count,
                    $"CRITICAL: {timeFrame} has {violatingRecords.Count} records >= end date");

                // All records should be from first 3 days only (days 0, 1, 2)
                var recordDays = rangeData.Select(r => r.DateTime.Date).Distinct().ToList();
                Assert.IsTrue(recordDays.All(d => d < queryEnd.Date),
                    $"CRITICAL: {timeFrame} includes excluded days");

                if (rangeData.Any())
                {
                    var latestRecord = rangeData.Max(r => r.DateTime);
                    Console.WriteLine(
                        $"{timeFrame}: {rangeData.Count} records, latest: {latestRecord:yyyy-MM-dd HH:mm:ss}");
                    Assert.IsTrue(latestRecord < queryEnd,
                        $"CRITICAL: {timeFrame} latest record {latestRecord:yyyy-MM-dd HH:mm:ss} >= end {queryEnd:yyyy-MM-dd HH:mm:ss}");
                }
            }
        }

        [TestMethod][TestCategory("Core")]
        public void ExclusiveEndDate_PreciseBoundaries_ExactTimestampExclusion()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 8, 10, 0, 0); // 10:00 AM

            // Add minute-by-minute data starting from 10:00
            for (var i = 0; i < 60; i++) // 1 hour: 10:00, 10:01, 10:02, ..., 10:59
            {
                var time = baseTime.AddMinutes(i);
                var price = 100.0 + i * 0.1;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.05, price - 0.05, price + 0.025, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // ✅ FIXED: Test exact boundary exclusion with correct expected counts
            // Data points: 10:00, 10:01, 10:02, ..., 10:59 (60 total records)
            var boundaryTests = new[]
            {
                // 10:00 to 10:30 (exclusive) should include: 10:00-10:29 (30 records)
                new { End = baseTime.AddMinutes(30), ExpectedCount = 30, Description = "30-minute boundary" },

                // 10:00 to 10:59 (exclusive) should include: 10:00-10:58 (59 records) 
                new { End = baseTime.AddMinutes(59), ExpectedCount = 59, Description = "59-minute boundary" },

                // 10:00 to 11:00 (exclusive) should include: 10:00-10:59 (60 records)
                new { End = baseTime.AddMinutes(60), ExpectedCount = 60, Description = "60-minute boundary" },

                // 10:00 to 10:30:01 (exclusive) should include: 10:00-10:30 (31 records)
                // ✅ FIXED: 10:30:01 excludes 10:30:01 but includes 10:30:00
                new
                {
                    End = baseTime.AddMinutes(30).AddSeconds(1), ExpectedCount = 31,
                    Description = "30min + 1sec boundary"
                },

                // 10:00 to 10:30:00.001 (exclusive) should include: 10:00-10:30 (31 records)  
                // ✅ FIXED: 10:30:00.001 excludes 10:30:00.001 but includes 10:30:00
                new
                {
                    End = baseTime.AddMinutes(30).AddMilliseconds(1), ExpectedCount = 31,
                    Description = "30min + 1ms boundary"
                },

                // ✅ ADDED: Test exact timestamp exclusion
                new { End = baseTime.AddMinutes(15), ExpectedCount = 15, Description = "15-minute exact boundary" },

                // ✅ ADDED: Test single minute precision  
                new { End = baseTime.AddMinutes(1), ExpectedCount = 1, Description = "1-minute boundary" }
            };

            foreach (var test in boundaryTests)
            {
                var rangeData = prices.GetRange(baseTime, test.End).ToList();

                // ✅ ENHANCED: More detailed assertion with actual vs expected breakdown
                Assert.AreEqual(test.ExpectedCount, rangeData.Count,
                    $"❌ FAILED {test.Description}: Expected {test.ExpectedCount} records but got {rangeData.Count}. " +
                    $"End time: {test.End:HH:mm:ss.fff}, " +
                    $"Latest record: {(rangeData.Any() ? rangeData.Max(r => r.DateTime).ToString("HH:mm:ss.fff") : "none")}");

                // CRITICAL: No record should equal or exceed the end timestamp
                var violatingRecords = rangeData.Where(r => r.DateTime >= test.End).ToList();
                Assert.AreEqual(0, violatingRecords.Count,
                    $"CRITICAL: {test.Description} has {violatingRecords.Count} records >= end time. " +
                    $"Violating times: {string.Join(", ", violatingRecords.Select(r => r.DateTime.ToString("HH:mm:ss.fff")))}");

                if (rangeData.Any())
                {
                    var latestRecord = rangeData.Max(r => r.DateTime);
                    var earliestRecord = rangeData.Min(r => r.DateTime);
                    Console.WriteLine($"✅ {test.Description}: {rangeData.Count} records");
                    Console.WriteLine(
                        $"   Range: {earliestRecord:HH:mm:ss} to {latestRecord:HH:mm:ss} (end: {test.End:HH:mm:ss.fff})");

                    // ✅ ADDED: Verify the latest record is always before end time
                    Assert.IsTrue(latestRecord < test.End,
                        $"CRITICAL: Latest record {latestRecord:HH:mm:ss.fff} >= end time {test.End:HH:mm:ss.fff}");
                }
                else
                {
                    Console.WriteLine($"⚠️  {test.Description}: No records found");
                }
            }
        }

        [TestMethod][TestCategory("Core")]
        public void ExclusiveEndDate_BacktestingScenario_PreventsFutureBias()
        {
            var prices = new Prices();
            // ✅ FIXED: Use top of hour (9:00 AM) to avoid incomplete bar issues
            var tradingStart = new DateTime(2025, 1, 6, 9, 0, 0); // Monday 9:00 AM (start of hour)

            // Simulate 10 trading days with complete hourly boundaries
            for (var day = 0; day < 10; day++)
                // ✅ FIXED: Generate data from 9:00 AM to 4:15 PM (7 hours = 420 minutes)
                // This creates complete hourly bars: 9-10, 10-11, 11-12, 12-1, 1-2, 2-3, 3-4
            for (var minute = 0; minute < 420; minute++) // 7 hours of trading
            {
                var time = tradingStart.AddDays(day).AddMinutes(minute);
                var price = 100.0 + day + Math.Sin(minute * 0.1) * 5;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 1, price - 1, price + 0.5, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // ✅ FIXED: Use beginning of day for clean boundary (midnight of day 7)
            var currentTradingDay = tradingStart.AddDays(7).Date; // Start of day 7 (midnight)
            var trainingEnd = currentTradingDay; // Exclusive end at midnight

            var trainingData = prices.GetRange(tradingStart, trainingEnd).ToList();

            // CRITICAL BACKTESTING INTEGRITY: No training data from current day or later
            var futureBiasRecords = trainingData.Where(r => r.DateTime.Date >= currentTradingDay).ToList();
            Assert.AreEqual(0, futureBiasRecords.Count,
                "CRITICAL: Training data contains future bias (current day or later data)");

            // Should have exactly 7 days of training data (days 0-6)
            var trainingDays = trainingData.Select(r => r.DateTime.Date).Distinct().Count();
            Assert.AreEqual(7, trainingDays, "Should have exactly 7 days of training data");

            // ✅ FIXED: Verify latest training record is before current day
            var latestTraining = trainingData.Max(r => r.DateTime);
            Assert.IsTrue(latestTraining.Date < currentTradingDay,
                $"Latest training record ({latestTraining:yyyy-MM-dd}) must be before current day ({currentTradingDay:yyyy-MM-dd})");

            // ✅ ADDED: Test with hourly timeframe to ensure no incomplete bar issues
            var trainingDataHourly = prices.GetRange(tradingStart, trainingEnd, TimeFrame.H1).ToList();

            // Verify no hourly bars cross the boundary
            Assert.IsTrue(trainingDataHourly.All(r => r.DateTime < trainingEnd),
                "CRITICAL: Hourly timeframe violated exclusive end date");

            // ✅ ADDED: Verify last hourly bar is complete and before boundary
            if (trainingDataHourly.Any())
            {
                var lastHourlyBar = trainingDataHourly.Max(r => r.DateTime);
                Assert.IsTrue(lastHourlyBar.Date < currentTradingDay,
                    $"Last hourly bar ({lastHourlyBar:yyyy-MM-dd HH:mm}) must be before current day");
            }

            Console.WriteLine("Backtesting integrity verified:");
            Console.WriteLine($"Current day: {currentTradingDay:yyyy-MM-dd}");
            Console.WriteLine($"Training days: {trainingDays}");
            Console.WriteLine($"Training records (M1): {trainingData.Count}");
            Console.WriteLine($"Training records (H1): {trainingDataHourly.Count}");
            Console.WriteLine($"Latest training (M1): {latestTraining:yyyy-MM-dd HH:mm:ss}");
            if (trainingDataHourly.Any())
            {
                var lastHourly = trainingDataHourly.Max(r => r.DateTime);
                Console.WriteLine($"Latest training (H1): {lastHourly:yyyy-MM-dd HH:mm:ss}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void ExclusiveEndDate_EmptyResults_WhenStartEqualsEnd()
        {
            var prices = new Prices();
            var baseTime = new DateTime(2025, 1, 8, 10, 0, 0);

            // Add some data
            for (var i = 0; i < 10; i++)
            {
                var time = baseTime.AddMinutes(i);
                var record = new PriceRecord(time, TimeFrame.M1, 100 + i, 101 + i, 99 + i, 100.5 + i, volume: 1000, wap: 100.25 + i, count: 100);
                prices.AddPrice(record);
            }

            // When start == end, should return empty (exclusive end)
            var emptyResults = prices.GetRange(baseTime, baseTime).ToList();
            Assert.AreEqual(0, emptyResults.Count, "Same start/end should return empty due to exclusive end");

            // When start > end, should return empty
            var invalidResults = prices.GetRange(baseTime.AddMinutes(5), baseTime).ToList();
            Assert.AreEqual(0, invalidResults.Count, "Start > end should return empty");

            Console.WriteLine("Empty range tests passed:");
            Console.WriteLine($"Same start/end: {emptyResults.Count} records");
            Console.WriteLine($"Start > end: {invalidResults.Count} records");
        }

        //[TestMethod][TestCategory("Core")]
        public void ExclusiveEndDate_LargeDataset_MaintainsPerformance()
        {
            var prices = new Prices();
            // ✅ FIXED: Use clean hour boundary to avoid incomplete bar issues
            var baseTime = new DateTime(2025, 1, 1, 9, 0, 0); // 9:00 AM (start of hour)

            // Add large dataset (100 days of minute data)
            for (var day = 0; day < 100; day++)
                // ✅ FIXED: Generate data from 9:00 AM to 4:15 PM (7 hours = 420 minutes)
            for (var minute = 0; minute < 420; minute++) // 7 hours per day
            {
                var time = baseTime.AddDays(day).AddMinutes(minute);
                var price = 100.0 + day + minute * 0.0001;
                var record = new PriceRecord(time, TimeFrame.D1, price, price + 0.1, price - 0.1, price + 0.05, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            Console.WriteLine($"Added {100 * 420} minute records (7 hours per day)");

            // Test large range query with exclusive end
            var start = baseTime;
            var end = baseTime.AddDays(50).Date; // Midnight of day 50

            var stopwatch = Stopwatch.StartNew();
            var rangeData = prices.GetRange(start, end).ToList();
            stopwatch.Stop();

            // CRITICAL: Exclusive end must be maintained even with large datasets
            Assert.IsTrue(rangeData.All(r => r.DateTime < end),
                "CRITICAL: Large dataset violated exclusive end requirement");

            // Should have exactly 50 days of data
            var uniqueDays = rangeData.Select(r => r.DateTime.Date).Distinct().Count();
            Assert.AreEqual(50, uniqueDays, "Should have exactly 50 days");

            // Performance should be reasonable
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000,
                $"Range query took too long: {stopwatch.ElapsedMilliseconds}ms");

            Console.WriteLine("Large dataset range query:");
            Console.WriteLine($"Records: {rangeData.Count}, Time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Days: {uniqueDays}, Latest: {rangeData.Max(r => r.DateTime):yyyy-MM-dd}");
        }

        [TestMethod][TestCategory("Core")]
        public void ExclusiveEndDate_AggregatedTimeFrames_MaintainExclusion()
        {
            var prices = new Prices();
            var start = new DateTime(2025, 1, 1, 0, 0, 0);

            // Add data that creates clean aggregation boundaries
            for (var day = 0; day < 5; day++)
            for (var hour = 0; hour < 24; hour++)
            for (var minute = 0; minute < 60; minute += 5) // Every 5 minutes
            {
                var time = start.AddDays(day).AddHours(hour).AddMinutes(minute);
                var price = 100.0 + day + hour * 0.1 + minute * 0.01;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.1, price - 0.1, price + 0.05, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            var queryStart = start;
            var queryEnd = start.AddDays(3); // Should exclude days 3 and 4

            // Test aggregated timeframes maintain exclusive end
            var testCases = new[]
            {
                new { TimeFrame = TimeFrame.M5, Description = "5-minute aggregation" },
                new { TimeFrame = TimeFrame.H1, Description = "1-hour aggregation" },
                new { TimeFrame = TimeFrame.H4, Description = "4-hour aggregation" },
                new { TimeFrame = TimeFrame.D1, Description = "Daily aggregation" }
            };

            foreach (var testCase in testCases)
            {
                var rangeData = prices.GetRange(queryStart, queryEnd, testCase.TimeFrame).ToList();

                // CRITICAL: Aggregated data must also respect exclusive end
                Assert.IsTrue(rangeData.All(r => r.DateTime < queryEnd),
                    $"CRITICAL: {testCase.Description} violated exclusive end");

                // All records should be from first 3 days only
                Assert.IsTrue(rangeData.All(r => r.DateTime.Date < queryEnd.Date),
                    $"CRITICAL: {testCase.Description} includes excluded days");

                if (rangeData.Any())
                {
                    var latestRecord = rangeData.Max(r => r.DateTime);
                    Console.WriteLine(
                        $"{testCase.Description}: {rangeData.Count} records, latest: {latestRecord:yyyy-MM-dd HH:mm:ss}");
                }
            }
        }
    }
}