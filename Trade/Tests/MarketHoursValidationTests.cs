using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class MarketHoursValidationTests
    {
        [TestMethod][TestCategory("Core")]
        public void MarketHours_DailyBarCompletion_OnlyAfterClose()
        {
            var prices = new Prices();
            var tradingDay = new DateTime(2025, 1, 8); // Wednesday
            var marketOpen = tradingDay.AddHours(9).AddMinutes(30); // 9:30 AM
            var marketClose = tradingDay.AddHours(16).AddMinutes(15); // 4:15 PM

            // Add full trading day data
            for (var i = 0; i < 390; i++) // 6.5 hours of trading
            {
                var time = marketOpen.AddMinutes(i);
                var price = 100.0 + Math.Sin(i * 0.01) * 5;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // Test daily bar
            var dailyBars = prices.GetTimeFrame(TimeFrame.D1);
            Assert.AreEqual(1, dailyBars.Count, "Should have exactly one daily bar");

            var dailyBar = dailyBars[0];
            Assert.AreEqual(tradingDay.Date, dailyBar.DateTime.Date, "Daily bar should be for correct date");

            // Verify OHLC spans entire trading session
            var m1Data = prices.GetTimeFrame(TimeFrame.M1);
            var firstMinute = m1Data[0];
            var lastMinute = m1Data[m1Data.Count - 1];

            Assert.AreEqual(firstMinute.Open, dailyBar.Open, 0.001, "Daily open should match first minute");
            Assert.AreEqual(lastMinute.Close, dailyBar.Close, 0.001, "Daily close should match last minute");

            // High and low should be extremes from all minutes
            var allHighs = new List<double>();
            var allLows = new List<double>();
            for (var i = 0; i < m1Data.Count; i++)
            {
                allHighs.Add(m1Data[i].High);
                allLows.Add(m1Data[i].Low);
            }

            Assert.AreEqual(allHighs.Max(), dailyBar.High, 0.001, "Daily high should be max of all minutes");
            Assert.AreEqual(allLows.Min(), dailyBar.Low, 0.001, "Daily low should be min of all minutes");
        }

        [TestMethod][TestCategory("Core")]
        public void MarketHours_IntradayBars_CompleteAfterPeriodEnd()
        {
            var prices = new Prices();
            var marketOpen = new DateTime(2025, 1, 8, 9, 30, 0);

            // Add exactly 30 minutes of data for M30 bar testing
            for (var i = 0; i < 30; i++)
            {
                var time = marketOpen.AddMinutes(i);
                var price = 100.0 + i * 0.1;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // Test various timeframes
            var m5Data = prices.GetTimeFrame(TimeFrame.M5);
            var m15Data = prices.GetTimeFrame(TimeFrame.M15);
            var m30Data = prices.GetTimeFrame(TimeFrame.M30);

            Assert.AreEqual(6, m5Data.Count, "Should have 6 five-minute bars (30/5)");
            Assert.AreEqual(2, m15Data.Count, "Should have 2 fifteen-minute bars (30/15)");
            Assert.AreEqual(1, m30Data.Count, "Should have 1 thirty-minute bar (30/30)");

            // Verify aggregation correctness for M30 bar
            var m30Bar = m30Data[0];
            var m1Data = prices.GetTimeFrame(TimeFrame.M1);

            Assert.AreEqual(marketOpen, m30Bar.DateTime, "M30 bar should start at market open");
            Assert.AreEqual(m1Data[0].Open, m30Bar.Open, 0.001, "M30 open should match first minute");
            Assert.AreEqual(m1Data[29].Close, m30Bar.Close, 0.001, "M30 close should match last minute");
        }

        [TestMethod][TestCategory("Core")]
        public void MarketHours_PreMarketData_IncludedInDailyBar()
        {
            var prices = new Prices();
            var tradingDay = new DateTime(2025, 1, 8);

            // Pre-market data (7:00 AM - 9:30 AM)
            for (var i = 0; i < 150; i++) // 2.5 hours before market open
            {
                var time = tradingDay.AddHours(7).AddMinutes(i);
                var price = 99.0 + i * 0.01;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.2, price - 0.2, price + 0.1, volume: 500, wap: price, count: 50);
                prices.AddPrice(record);
            }

            // Regular market hours (9:30 AM - 4:15 PM)
            var marketOpen = tradingDay.AddHours(9).AddMinutes(30);
            for (var i = 0; i < 390; i++)
            {
                var time = marketOpen.AddMinutes(i);
                var price = 100.0 + i * 0.01;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            var dailyBar = prices.GetTimeFrame(TimeFrame.D1)[0];
            var allMinutes = prices.GetTimeFrame(TimeFrame.M1);

            // Daily bar should start with pre-market data
            Assert.AreEqual(allMinutes[0].Open, dailyBar.Open, 0.001, "Daily should start with pre-market open");
            Assert.AreEqual(allMinutes[allMinutes.Count - 1].Close, dailyBar.Close, 0.001,
                "Daily should end with regular hours close");

            // Volume should include all sessions
            var expectedVolume = 150 * 500 + 390 * 1000; // Pre-market + regular hours
            Assert.AreEqual(expectedVolume, dailyBar.Volume, 0.001, "Daily volume should include all sessions");
        }

        [TestMethod][TestCategory("Core")]
        public void MarketHours_AfterHoursData_IncludedInDailyBar()
        {
            var prices = new Prices();
            var tradingDay = new DateTime(2025, 1, 8);
            var marketOpen = tradingDay.AddHours(9).AddMinutes(30);
            var marketClose = tradingDay.AddHours(16).AddMinutes(15);

            // Regular market hours
            for (var i = 0; i < 390; i++)
            {
                var time = marketOpen.AddMinutes(i);
                var price = 100.0 + i * 0.01;
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.5, price - 0.5, price + 0.25, volume: 1000, wap: price, count: 100);
                prices.AddPrice(record);
            }

            // After-hours data (4:15 PM - 8:00 PM)
            for (var i = 0; i < 240; i++) // 4 hours after market close
            {
                var time = marketClose.AddMinutes(i);
                var price = 103.9 + i * 0.005; // Continue from where market closed
                var record = new PriceRecord(time, TimeFrame.M1, price, price + 0.3, price - 0.3, price + 0.15, volume: 300, wap: price, count: 30);
                prices.AddPrice(record);
            }

            var dailyBar = prices.GetTimeFrame(TimeFrame.D1)[0];
            var allMinutes = prices.GetTimeFrame(TimeFrame.M1);

            // Daily bar should span entire extended session
            Assert.AreEqual(allMinutes[0].Open, dailyBar.Open, 0.001, "Daily should start with regular hours open");
            Assert.AreEqual(allMinutes[allMinutes.Count - 1].Close, dailyBar.Close, 0.001,
                "Daily should end with after-hours close");

            // Volume should include all sessions
            var expectedVolume = 390 * 1000 + 240 * 300; // Regular + after-hours
            Assert.AreEqual(expectedVolume, dailyBar.Volume, 0.001, "Daily volume should include after-hours");
        }

        [TestMethod][TestCategory("Core")]
        public void MarketHours_WeekendData_CreatesSeparateDailyBars()
        {
            var prices = new Prices();

            // Friday data
            var friday = new DateTime(2025, 1, 10); // Friday
            for (var i = 0; i < 100; i++)
            {
                var time = friday.AddHours(9).AddMinutes(30).AddMinutes(i);
                var record = new PriceRecord(time, TimeFrame.M1, 100 + i * 0.01, 100.5 + i * 0.01, 99.5 + i * 0.01,
                    100.25 + i * 0.01);
                prices.AddPrice(record);
            }

            // Saturday data (weekend)
            var saturday = new DateTime(2025, 1, 11); // Saturday
            for (var i = 0; i < 50; i++)
            {
                var time = saturday.AddHours(10).AddMinutes(i);
                var record = new PriceRecord(time, TimeFrame.M1, 101 + i * 0.01, 101.5 + i * 0.01, 100.5 + i * 0.01,
                    101.25 + i * 0.01);
                prices.AddPrice(record);
            }

            // Monday data
            var monday = new DateTime(2025, 1, 13); // Monday
            for (var i = 0; i < 100; i++)
            {
                var time = monday.AddHours(9).AddMinutes(30).AddMinutes(i);
                var record = new PriceRecord(time, TimeFrame.M1, 102 + i * 0.01, 102.5 + i * 0.01, 101.5 + i * 0.01,
                    102.25 + i * 0.01);
                prices.AddPrice(record);
            }

            var dailyBars = prices.GetTimeFrame(TimeFrame.D1);
            Assert.AreEqual(5, dailyBars.Count, "Should have 5 separate daily bars (Fri, Sat, Mon) + Bridge Bars");

            var dailyBarsNoBridge =
                prices.GetTimeFrame(TimeFrame.D1).PricesByTimestampAscending.Where(_ => !_.Manufactured);
            Assert.AreEqual(3, dailyBarsNoBridge.Count(), "Should have 3 separate daily bars (Fri, Sat, Mon)");

            // Verify each daily bar is for correct date
            PriceRecord fridayBar = null;
            PriceRecord saturdayBar = null;
            PriceRecord mondayBar = null;

            for (var i = 0; i < dailyBars.Count; i++)
            {
                var bar = dailyBars[i];
                if (bar.DateTime.Date == friday.Date)
                    fridayBar = bar;
                else if (bar.DateTime.Date == saturday.Date)
                    saturdayBar = bar;
                else if (bar.DateTime.Date == monday.Date)
                    mondayBar = bar;
            }

            Assert.IsNotNull(fridayBar, "Should have Friday daily bar");
            Assert.IsNotNull(saturdayBar, "Should have Saturday daily bar");
            Assert.IsNotNull(mondayBar, "Should have Monday daily bar");
        }

        [TestMethod][TestCategory("Core")]
        public void MarketHours_TimezoneDuringMarketHours_ConvertCorrectly()
        {
            var prices = new Prices();

            // Test data with various timezones during market hours
            var testCases = new[]
            {
                new { Time = "20250108 06:30:00 US/Pacific", Description = "Pacific 6:30 AM -> Eastern 9:30 AM" },
                new { Time = "20250108 08:30:00 US/Central", Description = "Central 8:30 AM -> Eastern 9:30 AM" },
                new { Time = "20250108 14:30:00 UTC", Description = "UTC 2:30 PM -> Eastern 9:30 AM" },
                new
                {
                    Time = "20250108 03:30:00 Pacific/Honolulu", Description = "Hawaii 3:30 AM -> Eastern 8:30/9:30 AM"
                }
            };

            foreach (var testCase in testCases)
            {
                var record = new PriceRecord();
                record.Time = testCase.Time;
                record.DateTime = Prices.ParseDateTimeFromString(testCase.Time);
                record.Open = 100;
                record.High = 101;
                record.Low = 99;
                record.Close = 100.5;
                record.Volume = 1000;
                record.WAP = 100.25;
                record.Count = 100;
                record.IsComplete = true;

                prices.AddPrice(record);

                Console.WriteLine(
                    $"{testCase.Description}: Converted to {record.DateTime:yyyy-MM-dd HH:mm:ss} Eastern");

                // Verify the record was added and converted
                var retrievedRecord = prices.GetTimeFrame(TimeFrame.M1).GetLatest();
                Assert.IsNotNull(retrievedRecord, $"Should retrieve record for {testCase.Description}");
                Assert.AreEqual(2025, retrievedRecord.DateTime.Year, "Year should be 2025");
                Assert.AreEqual(1, retrievedRecord.DateTime.Month, "Month should be January");
                Assert.AreEqual(8, retrievedRecord.DateTime.Day, "Day should be 8th");
            }

            // All records should be aggregated into single daily bar for Jan 8th
            var dailyBars = prices.GetTimeFrame(TimeFrame.D1);
            Assert.AreEqual(1, dailyBars.Count, "All timezone data should aggregate to single daily bar");

            var dailyBar = dailyBars[0];
            Assert.AreEqual(new DateTime(2025, 1, 8).Date, dailyBar.DateTime.Date, "Daily bar should be for Jan 8th");
        }

        [TestMethod][TestCategory("Core")]
        public void MarketHours_EdgeCases_HandleCorrectly()
        {
            var prices = new Prices();
            var tradingDay = new DateTime(2025, 1, 8);

            // Test edge cases
            var edgeCases = new[]
            {
                new { Time = tradingDay.AddHours(9).AddMinutes(30), Description = "Exactly market open" },
                new { Time = tradingDay.AddHours(16.25).AddMinutes(-1), Description = "One minute before close" },
                new { Time = tradingDay.AddHours(16.25), Description = "Exactly market close" },
                new { Time = tradingDay.AddHours(0), Description = "Midnight" },
                new { Time = tradingDay.AddHours(23).AddMinutes(59), Description = "End of day" }
            };

            foreach (var edgeCase in edgeCases)
            {
                var record = new PriceRecord(edgeCase.Time, TimeFrame.M1, 100, 101, 99, 100.5, volume: 1000, wap: 100.25, count: 100);
                prices.AddPrice(record);

                Console.WriteLine($"{edgeCase.Description}: {edgeCase.Time:HH:mm:ss}");
            }

            // All should be in same daily bar
            var dailyBars = prices.GetTimeFrame(TimeFrame.D1);
            Assert.AreEqual(1, dailyBars.Count, "All edge case times should be in same daily bar");

            var dailyBar = dailyBars[0];
            Assert.AreEqual(tradingDay.Date, dailyBar.DateTime.Date, "Daily bar should be for correct date");

            // Verify all 5 records are captured
            var m1Data = prices.GetTimeFrame(TimeFrame.M1);
            Assert.AreEqual(5, m1Data.Count, "Should have all 5 edge case records");
        }
    }
}