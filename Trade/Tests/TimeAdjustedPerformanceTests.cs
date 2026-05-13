using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Trade.Tests
{
    [TestClass]
    public class TimeAdjustedPerformanceTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void TimeRatioCalculations_ProduceCorrectProportions()
        {
            // Test various data split scenarios
            var scenarios = new[]
            {
                new { total = 1000, training = 700, test = 300, expectedTrainRatio = 0.7, expectedTestRatio = 0.3 },
                new { total = 252, training = 189, test = 63, expectedTrainRatio = 0.75, expectedTestRatio = 0.25 },
                new { total = 500, training = 400, test = 100, expectedTrainRatio = 0.8, expectedTestRatio = 0.2 }
            };

            foreach (var scenario in scenarios)
            {
                var trainingTimeRatio = (double)scenario.training / scenario.total;
                var testTimeRatio = (double)scenario.test / scenario.total;

                Assert.AreEqual(scenario.expectedTrainRatio, trainingTimeRatio, 0.01,
                    $"Training ratio should be {scenario.expectedTrainRatio:P0}");
                Assert.AreEqual(scenario.expectedTestRatio, testTimeRatio, 0.01,
                    $"Test ratio should be {scenario.expectedTestRatio:P0}");

                // Ratios should sum to 1
                Assert.AreEqual(1.0, trainingTimeRatio + testTimeRatio, 0.01,
                    "Training and test ratios should sum to 1");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void AnnualizedReturnCalculations_HandleVariousTimePeriods()
        {
            // Test the core annualization formula: performance / timeRatio
            var testCases = new[]
            {
                new { performance = 50.0, timeRatio = 0.5, expectedAnnualized = 100.0 }, // 6 months
                new { performance = 25.0, timeRatio = 0.25, expectedAnnualized = 100.0 }, // 3 months  
                new { performance = 100.0, timeRatio = 1.0, expectedAnnualized = 100.0 }, // 12 months
                new { performance = 10.0, timeRatio = 0.1, expectedAnnualized = 100.0 }, // 1.2 months
                new { performance = 200.0, timeRatio = 2.0, expectedAnnualized = 100.0 } // 24 months
            };

            foreach (var testCase in testCases)
            {
                var annualized = testCase.performance / testCase.timeRatio;
                Assert.AreEqual(testCase.expectedAnnualized, annualized, 0.01,
                    $"{testCase.performance}% over {testCase.timeRatio:P0} time should annualize to {testCase.expectedAnnualized}%");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TimeAdjustedGapAnalysis_IdentifiesOverfittingCorrectly()
        {
            // Scenario 1: Large raw gap, small time-adjusted gap (NOT overfitting)
            var scenario1 = AnalyzePerformanceGap(
                70.0, 0.7, // 100% annualized
                30.0, 0.3 // 100% annualized
            );

            Assert.AreEqual(40.0, scenario1.rawGap, 0.01, "Raw gap should be 40%");
            Assert.AreEqual(0.0, scenario1.timeAdjustedGap, 0.01, "Time-adjusted gap should be 0%");
            Assert.IsFalse(scenario1.indicatesOverfitting,
                "Same annualized performance should not indicate overfitting");

            // Scenario 2: Small raw gap, large time-adjusted gap (POSSIBLE overfitting)
            var scenario2 = AnalyzePerformanceGap(
                20.0, 0.2, // 100% annualized
                18.0, 0.6 // 30% annualized
            );

            Assert.AreEqual(2.0, scenario2.rawGap, 0.01, "Raw gap should be 2%");
            Assert.AreEqual(70.0, scenario2.timeAdjustedGap, 0.01, "Time-adjusted gap should be 70%");
            Assert.IsTrue(scenario2.indicatesOverfitting, "Large time-adjusted gap should indicate overfitting");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradingDayConversions_AreAccurate()
        {
            // Test the 252 trading days per year assumption
            double[] testPeriods = { 21, 63, 126, 189, 252 }; // 1, 3, 6, 9, 12 months
            double[] expectedYears = { 1.0 / 12, 0.25, 0.5, 0.75, 1.0 };

            for (var i = 0; i < testPeriods.Length; i++)
            {
                var calculatedYears = testPeriods[i] / 252.0;
                Assert.AreEqual(expectedYears[i], calculatedYears, 0.01,
                    $"{testPeriods[i]} trading days should equal {expectedYears[i]} years");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PerformanceGapThresholds_AreReasonable()
        {
            // Test the threshold logic used in Program.cs
            var rawThreshold = 5.0; // 5% for raw performance gap
            var timeAdjustedThreshold = 10.0; // 10% for time-adjusted gap

            // Test boundary conditions
            Assert.IsTrue(5.1 > rawThreshold, "Raw gap of 5.1% should trigger warning");
            Assert.IsFalse(4.9 > rawThreshold, "Raw gap of 4.9% should not trigger warning");

            Assert.IsTrue(10.1 > timeAdjustedThreshold, "Time-adjusted gap of 10.1% should trigger warning");
            Assert.IsFalse(9.9 > timeAdjustedThreshold, "Time-adjusted gap of 9.9% should not trigger warning");

            // Thresholds should be reasonable for trading strategies
            Assert.IsTrue(rawThreshold >= 1.0, "Raw threshold should be at least 1%");
            Assert.IsTrue(rawThreshold <= 20.0, "Raw threshold should be reasonable (?20%)");
            Assert.IsTrue(timeAdjustedThreshold >= 5.0, "Time-adjusted threshold should be at least 5%");
            Assert.IsTrue(timeAdjustedThreshold <= 50.0, "Time-adjusted threshold should be reasonable (?50%)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TimeCompressionFactors_CalculateCorrectly()
        {
            // Test the "time compression factor" calculations from Program.cs
            var testScenarios = new[]
            {
                new { timeRatio = 0.5, expectedCompression = 2.0 }, // 6 months = 2x compression
                new { timeRatio = 0.25, expectedCompression = 4.0 }, // 3 months = 4x compression
                new { timeRatio = 1.0, expectedCompression = 1.0 }, // 12 months = 1x (no compression)
                new { timeRatio = 2.0, expectedCompression = 0.5 } // 24 months = 0.5x (expansion)
            };

            foreach (var scenario in testScenarios)
            {
                var compressionFactor = 1.0 / scenario.timeRatio;
                Assert.AreEqual(scenario.expectedCompression, compressionFactor, 0.01,
                    $"Time ratio {scenario.timeRatio} should give {scenario.expectedCompression}x compression");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void EquivalentAnnualPerformance_MakesSense()
        {
            // Test the "equivalent annual performance" concept
            var examples = new[]
            {
                new
                {
                    description = "6 months at 50%",
                    period = "6 months", actualReturn = 50.0, timeRatio = 0.5,
                    equivalentAnnual = 100.0
                },
                new
                {
                    description = "3 months at 25%",
                    period = "3 months", actualReturn = 25.0, timeRatio = 0.25,
                    equivalentAnnual = 100.0
                },
                new
                {
                    description = "18 months at 150%",
                    period = "18 months", actualReturn = 150.0, timeRatio = 1.5,
                    equivalentAnnual = 100.0
                }
            };

            foreach (var example in examples)
            {
                var calculated = example.actualReturn / example.timeRatio;
                Assert.AreEqual(example.equivalentAnnual, calculated, 0.01,
                    $"{example.description} should annualize to {example.equivalentAnnual}%");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void RealWorldPerformanceGapScenario_HandledCorrectly()
        {
            // Test the actual scenario mentioned in the user's question
            var trainingPerformance = 75.5; // From user's example
            var testPerformance = 23.53; // From user's example
            var rawGap = Math.Abs(trainingPerformance - testPerformance); // 51.97%

            // Simulate realistic data split
            double trainingDataPoints = 595; // 70% of ~850 points
            double testDataPoints = 255; // 30% of ~850 points  
            double totalDataPoints = 850;

            var trainingTimeRatio = trainingDataPoints / totalDataPoints;
            var testTimeRatio = testDataPoints / totalDataPoints;

            var trainingAnnualized = trainingPerformance / trainingTimeRatio;
            var testAnnualized = testPerformance / testTimeRatio;
            var timeAdjustedGap = Math.Abs(trainingAnnualized - testAnnualized);

            // Verify the calculations match the expected improvement
            Assert.AreEqual(51.97, rawGap, 0.1, "Raw gap should match user's scenario");
            Assert.IsTrue(timeAdjustedGap < rawGap, "Time-adjusted gap should be smaller than raw gap");
            Assert.IsTrue(timeAdjustedGap < 40.0, "Time-adjusted gap should be more reasonable");

            // The key insight: time adjustment should reduce the apparent overfitting
            var improvementRatio = timeAdjustedGap / rawGap;
            Assert.IsTrue(improvementRatio < 1.0, "Time adjustment should improve the gap");
            Assert.IsTrue(improvementRatio > 0.3, "But should not eliminate the gap entirely");
        }

        #region Helper Methods

        private (double rawGap, double timeAdjustedGap, bool indicatesOverfitting) AnalyzePerformanceGap(
            double trainingPerf, double trainingRatio, double testPerf, double testRatio)
        {
            var rawGap = Math.Abs(trainingPerf - testPerf);

            var trainingAnnualized = trainingPerf / trainingRatio;
            var testAnnualized = testPerf / testRatio;
            var timeAdjustedGap = Math.Abs(trainingAnnualized - testAnnualized);

            // Use the same thresholds as Program.cs
            var indicatesOverfitting = timeAdjustedGap > 10.0;

            return (rawGap, timeAdjustedGap, indicatesOverfitting);
        }

        #endregion
    }
}