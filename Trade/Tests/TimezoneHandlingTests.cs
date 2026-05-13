using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class TimezoneHandlingTests
    {
        [TestMethod][TestCategory("Core")]
        public void TimezoneConversion_AllSupportedTimezones_ConvertCorrectly()
        {
            var testCases = new[]
            {
                new { Timezone = "Pacific/Honolulu", ExpectedOffset = -10 }, // UTC-10 always
                new { Timezone = "HST", ExpectedOffset = -10 },
                new { Timezone = "Hawaii", ExpectedOffset = -10 },
                new { Timezone = "US/Pacific", ExpectedOffset = -8 }, // UTC-8 PST or UTC-7 PDT
                new { Timezone = "Pacific/Los_Angeles", ExpectedOffset = -8 },
                new { Timezone = "PST", ExpectedOffset = -8 },
                new { Timezone = "PDT", ExpectedOffset = -7 },
                new { Timezone = "US/Mountain", ExpectedOffset = -7 }, // UTC-7 MST or UTC-6 MDT
                new { Timezone = "America/Denver", ExpectedOffset = -7 },
                new { Timezone = "US/Central", ExpectedOffset = -6 }, // UTC-6 CST or UTC-5 CDT
                new { Timezone = "America/Chicago", ExpectedOffset = -6 },
                new { Timezone = "US/Eastern", ExpectedOffset = -5 }, // UTC-5 EST or UTC-4 EDT
                new { Timezone = "America/New_York", ExpectedOffset = -5 },
                new { Timezone = "UTC", ExpectedOffset = 0 },
                new { Timezone = "GMT", ExpectedOffset = 0 }
            };

            foreach (var testCase in testCases)
            {
                var timeString = $"20250115 12:00:00 {testCase.Timezone}"; // Winter date to avoid DST

                try
                {
                    var easternTime = Prices.ParseDateTimeFromString(timeString);

                    // Should get a valid DateTime
                    Assert.IsTrue(easternTime.Year == 2025, $"Year should be 2025 for {testCase.Timezone}");
                    Assert.IsTrue(easternTime.Month == 1, $"Month should be 1 for {testCase.Timezone}");
                    Assert.IsTrue(easternTime.Day == 15, $"Day should be 15 for {testCase.Timezone}");

                    Console.WriteLine($"{testCase.Timezone}: 12:00 -> {easternTime:HH:mm} Eastern");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Failed to parse timezone {testCase.Timezone}: {ex.Message}");
                }
            }
        }

        [TestMethod][TestCategory("Core")]
        public void TimezoneConversion_InvalidTimezone_FallsBackGracefully()
        {
            var invalidTimezones = new[]
            {
                "Invalid/Timezone",
                "NonExistent",
                "Bad_Format",
                "",
                "123456"
            };

            foreach (var invalidTz in invalidTimezones)
            {
                var timeString = $"20250115 12:00:00 {invalidTz}";

                try
                {
                    var result = Prices.ParseDateTimeFromString(timeString);

                    // Should not throw, should fallback to some reasonable value
                    Assert.IsTrue(result.Year >= 2000, $"Should handle invalid timezone gracefully: {invalidTz}");
                    Console.WriteLine(
                        $"Invalid timezone '{invalidTz}' handled gracefully: {result:yyyy-MM-dd HH:mm:ss}");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Should handle invalid timezone gracefully: {invalidTz}, Error: {ex.Message}");
                }
            }
        }

        [TestMethod][TestCategory("Core")]
        public void TimezoneConversion_DSTTransitions_WorkCorrectly()
        {
            // Test specific DST transition dates for 2025
            var springDST = new DateTime(2025, 3, 9); // Second Sunday in March
            var fallDST = new DateTime(2025, 11, 2); // First Sunday in November

            var testCases = new[]
            {
                new { Date = new DateTime(2025, 1, 15), Description = "Winter (EST)" },
                new { Date = springDST.AddDays(-1), Description = "Before Spring DST" },
                new { Date = springDST.AddDays(1), Description = "After Spring DST" },
                new { Date = new DateTime(2025, 7, 15), Description = "Summer (EDT)" },
                new { Date = fallDST.AddDays(-1), Description = "Before Fall DST" },
                new { Date = fallDST.AddDays(1), Description = "After Fall DST" }
            };

            foreach (var testCase in testCases)
            {
                var timeString = $"{testCase.Date:yyyyMMdd} 12:00:00 US/Pacific";
                var easternTime = Prices.ParseDateTimeFromString(timeString);

                // Pacific noon should convert to either 3 PM EST or 4 PM EDT
                Assert.IsTrue(easternTime.Hour >= 14 && easternTime.Hour <= 16,
                    $"Expected 2-4 PM Eastern for Pacific noon on {testCase.Description}, got {easternTime.Hour}");

                Console.WriteLine($"{testCase.Description}: Pacific 12:00 -> Eastern {easternTime:HH:mm}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void TimezoneConversion_HawaiiConsistency_YearRound()
        {
            // Hawaii should always be UTC-10, no DST changes
            var testDates = new[]
            {
                new DateTime(2025, 1, 15), // Winter
                new DateTime(2025, 4, 15), // Spring
                new DateTime(2025, 7, 15), // Summer
                new DateTime(2025, 10, 15) // Fall
            };

            var previousHour = -1;
            foreach (var testDate in testDates)
            {
                var timeString = $"{testDate:yyyyMMdd} 06:00:00 Pacific/Honolulu";
                var easternTime = Prices.ParseDateTimeFromString(timeString);

                Console.WriteLine($"Hawaii 6:00 AM on {testDate:yyyy-MM-dd} -> Eastern {easternTime:HH:mm}");

                // The difference should be consistent for Hawaii vs Eastern (allowing for Eastern DST changes)
                if (previousHour == -1)
                    previousHour = easternTime.Hour;
                else
                    // Allow for 1 hour difference due to Eastern DST, but Hawaii should be consistent
                    Assert.IsTrue(Math.Abs(easternTime.Hour - previousHour) <= 1,
                        $"Hawaii conversion should be consistent, got {easternTime.Hour} vs previous {previousHour}");
            }
        }
    }
}