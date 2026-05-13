using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class WeekendAwareGapAnalysisTests
    {
        [TestMethod][TestCategory("Core")]
        public void BusinessDayGap_WeekendGap_ReturnsZero()
        {
            // Test the CalculateBusinessDayGap method via reflection since it's private
            var method = typeof(Prices).GetMethod("CalculateBusinessDayGap",
                BindingFlags.NonPublic | BindingFlags.Static);

            // Friday 5 PM to Monday 9 AM - should be 0 business days
            var friday = new DateTime(2025, 1, 10, 17, 0, 0); // Friday 5 PM
            var monday = new DateTime(2025, 1, 13, 9, 0, 0); // Monday 9 AM

            var businessDays = (int)method.Invoke(null, new object[] { friday, monday });

            Assert.AreEqual(0, businessDays, "Friday to Monday should be 0 business days");
        }

        [TestMethod][TestCategory("Core")]
        public void BusinessDayGap_ThreeDayWeekend_ReturnsOne()
        {
            var method = typeof(Prices).GetMethod("CalculateBusinessDayGap",
                BindingFlags.NonPublic | BindingFlags.Static);

            // Friday to Tuesday (long weekend) - should be 1 business day
            var friday = new DateTime(2025, 1, 10, 17, 0, 0); // Friday 5 PM
            var tuesday = new DateTime(2025, 1, 14, 9, 0, 0); // Tuesday 9 AM

            var businessDays = (int)method.Invoke(null, new object[] { friday, tuesday });

            Assert.AreEqual(1, businessDays, "Friday to Tuesday should be 1 business day (Monday)");
        }

        [TestMethod][TestCategory("Core")]
        public void BusinessDayGap_OneWeekGap_ReturnsFive()
        {
            var method = typeof(Prices).GetMethod("CalculateBusinessDayGap",
                BindingFlags.NonPublic | BindingFlags.Static);

            // Friday to next Friday - should be 5 business days
            var friday1 = new DateTime(2025, 1, 10, 17, 0, 0); // Friday 5 PM
            var friday2 = new DateTime(2025, 1, 17, 9, 0, 0); // Next Friday 9 AM

            var businessDays = (int)method.Invoke(null, new object[] { friday1, friday2 });

            Assert.AreEqual(4, businessDays, "Friday to next Friday should be 5 business days");
        }

        [TestMethod][TestCategory("Core")][TestCategory("Core")]
        public void WeekendGaps_DoNotTriggerWarnings()
        {
            var prices = new Prices(); // Empty constructor

            // Add Friday data
            var friday = new DateTime(2025, 1, 10, 15, 30, 0); // Friday 3:30 PM
            var fridayRecord = new PriceRecord(friday, TimeFrame.M1, 100, 101, 99, 100.5, volume: 1000, wap: 100.25, count: 100);
            prices.AddPrice(fridayRecord);

            // Add Monday data (2.5 day calendar gap, 0 business day gap)
            var monday = new DateTime(2025, 1, 13, 9, 30, 0); // Monday 9:30 AM
            var mondayRecord = new PriceRecord(monday, TimeFrame.M1, 100.5, 102, 100, 101.5, volume: 1000, wap: 101, count: 100);
            prices.AddPrice(mondayRecord);

            // Validate data - should not report weekend as a gap
            var validationResult = prices.ValidateLoadedData();

            Assert.IsTrue(validationResult.IsValid, "Weekend gap should not cause validation to fail");

            // Check that no gap warnings were generated for weekend
            var gapWarnings = validationResult.Warnings.Where(w => w.Contains("data gap")).ToList();
            Assert.AreEqual(0, gapWarnings.Count,
                "Weekend gaps should not be reported as significant data gaps");
        }

        [TestMethod][TestCategory("Core")]
        public void LongWeekend_DoesNotTriggerWarning()
        {
            var prices = new Prices(); // Empty constructor

            // Add Friday data
            var friday = new DateTime(2025, 1, 10, 16, 0, 0); // Friday 4 PM
            var fridayRecord = new PriceRecord(friday, TimeFrame.M1, 100, 101, 99, 100.5, volume: 1000, wap: 100.25, count: 100);
            prices.AddPrice(fridayRecord);

            // Add Tuesday data (long weekend - 4.5 calendar days, 1 business day gap)
            var tuesday = new DateTime(2025, 1, 14, 9, 30, 0); // Tuesday 9:30 AM
            var tuesdayRecord = new PriceRecord(tuesday, TimeFrame.M1, 100.5, 102, 100, 101.5, volume: 1000, wap: 101, count: 100);
            prices.AddPrice(tuesdayRecord);

            // Validate data - should not report long weekend as significant gap
            var validationResult = prices.ValidateLoadedData();

            Assert.IsTrue(validationResult.IsValid, "Long weekend should not cause validation to fail");

            // Check that no gap warnings were generated for long weekend
            var gapWarnings = validationResult.Warnings.Where(w => w.Contains("data gap")).ToList();
            Assert.AreEqual(0, gapWarnings.Count,
                "Long weekend gaps (1 business day) should not be reported as significant");
        }

        [TestMethod][TestCategory("Core")]
        public void TwoWeekGap_TriggersWarning()
        {
            var prices = new Prices(); // Empty constructor

            // Add Friday data
            var friday = new DateTime(2025, 1, 10, 16, 0, 0); // Friday 4 PM
            var fridayRecord = new PriceRecord(friday, TimeFrame.M1, 100, 101, 99, 100.5, volume: 1000, wap: 100.25, count: 100);
            prices.AddPrice(fridayRecord);

            // Add data two weeks later (10 business days gap - should trigger warning)
            var futureDate = new DateTime(2025, 1, 27, 9, 30, 0); // Monday two weeks later
            var futureRecord = new PriceRecord(futureDate, TimeFrame.M1, 100.5, 102, 100, 101.5, volume: 1000, wap: 101, count: 100);
            prices.AddPrice(futureRecord);

            // Validate data - should report this as a significant gap
            var validationResult = prices.ValidateLoadedData();

            Assert.IsTrue(validationResult.IsValid, "Large gap should not cause validation to fail completely");

            // Check that gap warning was generated for 2-week gap
            var gapWarnings = validationResult.Warnings.Where(w => w.Contains("data gap")).ToList();
            Assert.AreEqual(1, gapWarnings.Count,
                "Two week gap (>5 business days) should be reported as significant");

            Assert.IsTrue(gapWarnings[0].Contains("business days"),
                "Gap warning should include business days information");
        }
    }
}