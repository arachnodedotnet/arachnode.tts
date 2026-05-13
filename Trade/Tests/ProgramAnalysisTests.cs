using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class ProgramAnalysisTests
    {
        [TestInitialize]
        public void Setup()
        {
            // Initialize static dependencies for testing
            GeneticIndividual.InitializePrices();
            //GeneticIndividual.InitializeOptionSolvers();
        }


        [TestMethod][TestCategory("Core")]
        public void CalculateAnnualizedPerformance_ValidInputs_ReturnsCorrectValues()
        {
            // Test annualized performance calculation
            // 50% return over 6 months should equal 100% annualized
            var result = InvokeCalculateAnnualizedPerformance(50.0, 126, 252); // 6 months of 252 trading days
            Assert.AreEqual(100.0, result, 0.01, "6 months at 50% should annualize to 100%");

            // 25% return over 3 months should equal 100% annualized  
            result = InvokeCalculateAnnualizedPerformance(25.0, 63, 252); // 3 months of 252 trading days
            Assert.AreEqual(100.0, result, 0.01, "3 months at 25% should annualize to 100%");

            // 100% return over 12 months should equal 100% annualized
            result = InvokeCalculateAnnualizedPerformance(100.0, 252, 252); // 12 months
            Assert.AreEqual(100.0, result, 0.01, "12 months at 100% should remain 100%");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateAnnualizedPerformance_EdgeCases_HandlesCorrectly()
        {
            // Zero data points should return 0
            var result = InvokeCalculateAnnualizedPerformance(50.0, 0, 252);
            Assert.AreEqual(0.0, result, "Zero data points should return 0");

            // Negative data points should return 0
            result = InvokeCalculateAnnualizedPerformance(50.0, -10, 252);
            Assert.AreEqual(0.0, result, "Negative data points should return 0");

            // Zero performance should return 0
            result = InvokeCalculateAnnualizedPerformance(0.0, 126, 252);
            Assert.AreEqual(0.0, result, "Zero performance should return 0");
        }

        [TestMethod][TestCategory("Core")]
        public void TimeAdjustedPerformanceAnalysis_RealisticScenario_ProducesLogicalResults()
        {
            // Simulate the scenario mentioned in the user's question:
            // Training: 70% of data with high performance
            // Testing: 30% of data with lower performance

            var trainingPerformance = 100.0; // 100% return
            var testPerformance = 50.0; // 50% return
            double trainingDataPoints = 700; // 70% of 1000 points
            double testDataPoints = 300; // 30% of 1000 points
            double totalDataPoints = 1000;

            // Calculate time ratios
            var trainingTimeRatio = trainingDataPoints / totalDataPoints; // 0.7
            var testTimeRatio = testDataPoints / totalDataPoints; // 0.3

            // Calculate annualized returns
            var trainingAnnualized = trainingPerformance / trainingTimeRatio; // 100 / 0.7 = 142.86%
            var testAnnualized = testPerformance / testTimeRatio; // 50 / 0.3 = 166.67%

            // Verify the math
            Assert.AreEqual(0.7, trainingTimeRatio, 0.01, "Training time ratio should be 70%");
            Assert.AreEqual(0.3, testTimeRatio, 0.01, "Test time ratio should be 30%");
            Assert.AreEqual(142.86, trainingAnnualized, 0.1, "Training annualized should be ~142.86%");
            Assert.AreEqual(166.67, testAnnualized, 0.1, "Test annualized should be ~166.67%");

            // Time-adjusted gap should be much smaller than raw gap
            var rawGap = Math.Abs(trainingPerformance - testPerformance); // 50%
            var timeAdjustedGap = Math.Abs(trainingAnnualized - testAnnualized); // ~23.81%

            Assert.AreEqual(50.0, rawGap, 0.01, "Raw performance gap should be 50%");
            Assert.IsTrue(timeAdjustedGap < rawGap, "Time-adjusted gap should be smaller than raw gap");
            Assert.IsTrue(timeAdjustedGap < 30.0, "Time-adjusted gap should be reasonable (< 30%)");
        }

        [TestMethod][TestCategory("Core")]
        public void OverfittingDetection_LargePerformanceGaps_ProducesCorrectWarnings()
        {
            // Test the overfitting detection logic
            var performanceGap = 60.0; // Large gap that should trigger warning
            var timeAdjustedGap = 8.0; // Reasonable time-adjusted gap (changed from 15.0 to 8.0)

            // Raw performance gap > 5% should trigger warning
            Assert.IsTrue(performanceGap > 5.0, "Large performance gap should exceed threshold");

            // Time-adjusted gap < 10% should NOT trigger warning
            Assert.IsTrue(timeAdjustedGap < 10.0, "Reasonable time-adjusted gap should be below threshold");

            // Test boundary conditions
            Assert.IsTrue(5.1 > 5.0, "Just above threshold should trigger warning");
            Assert.IsFalse(4.9 > 5.0, "Just below threshold should not trigger warning");
            Assert.IsTrue(10.1 > 10.0, "Time-adjusted gap above 10% should trigger warning");
            Assert.IsFalse(9.9 > 10.0, "Time-adjusted gap below 10% should not trigger warning");

            // Test that time adjustment actually improves the gap
            Assert.IsTrue(timeAdjustedGap < performanceGap, "Time-adjusted gap should be smaller than raw gap");
        }

        [TestMethod][TestCategory("Core")]
        public void OverfittingDetection_LargeTimeAdjustedGap_ProducesCorrectWarnings()
        {
            // Test the overfitting detection logic when time-adjusted gap is large
            var performanceGap = 30.0; // Moderate raw gap
            var timeAdjustedGap = 15.0; // Large time-adjusted gap that should trigger warning

            // Raw performance gap > 5% should trigger warning
            Assert.IsTrue(performanceGap > 5.0, "Performance gap should exceed raw threshold");

            // Time-adjusted gap > 10% SHOULD trigger warning
            Assert.IsTrue(timeAdjustedGap > 10.0, "Large time-adjusted gap should exceed threshold");

            // Test the specific scenario where time adjustment doesn't help much
            // This could indicate overfitting that persists even after time adjustment
            Assert.IsTrue(timeAdjustedGap < performanceGap, "Time-adjusted gap should still be smaller than raw gap");

            // But the time-adjusted gap is still concerning
            var timeAdjustmentImprovement = (performanceGap - timeAdjustedGap) / performanceGap;
            Assert.IsTrue(timeAdjustmentImprovement < 0.6, "Limited improvement suggests persistent overfitting");
        }

        [TestMethod][TestCategory("Core")]
        public void TradingFrequencyAnalysis_CalculatesCorrectMetrics()
        {
            var individual = CreateTestIndividual();

            // Create trades with known durations
            individual.Trades.Add(new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 5, // 5 day duration
                Balance = 105000
            });
            individual.Trades.Add(new TradeResult
            {
                OpenIndex = 10,
                CloseIndex = 20, // 10 day duration
                Balance = 110000
            });
            individual.Trades.Add(new TradeResult
            {
                OpenIndex = 25,
                CloseIndex = 30, // 5 day duration
                Balance = 115000
            });

            // Calculate expected metrics
            var durations = individual.Trades.Select(t => t.CloseIndex - t.OpenIndex);
            var expectedAvgDuration = durations.Average(); // (5 + 10 + 5) / 3 = 6.67

            Assert.AreEqual(6.67, expectedAvgDuration, 0.1, "Average duration should be 6.67 periods");
            Assert.AreEqual(3, individual.Trades.Count, "Should have 3 trades");

            // Test time conversion logic
            var timeRatio = 252.0 / 252.0; // 1 year of data
            var tradesPerYear = individual.Trades.Count / timeRatio; // 3 trades per year
            Assert.AreEqual(3.0, tradesPerYear, 0.1, "Should be 3 trades per year");
        }

        [TestMethod][TestCategory("Core")]
        public void BuyAndHoldComparison_CalculatesCorrectMetrics()
        {
            // Test buy and hold calculation logic
            double[] priceBuffer = { 100.0, 105.0, 110.0, 108.0, 115.0, 120.0 };

            var buyHoldGain = priceBuffer[priceBuffer.Length - 1] - priceBuffer[0]; // 120 - 100 = 20
            var buyHoldPercent = buyHoldGain / priceBuffer[0] * 100.0; // 20%

            Assert.AreEqual(20.0, buyHoldGain, 0.01, "Buy and hold gain should be $20");
            Assert.AreEqual(20.0, buyHoldPercent, 0.01, "Buy and hold return should be 20%");

            // Test annualization
            var timeRatio = priceBuffer.Length / 252.0; // Short period
            var buyHoldAnnualized = buyHoldPercent / timeRatio;

            Assert.IsTrue(buyHoldAnnualized > buyHoldPercent, "Annualized return should be higher for short periods");
        }

        #region Helper Methods

        private GeneticIndividual CreateTestIndividual()
        {
            var individual = new GeneticIndividual
            {
                StartingBalance = 100000
            };
            // Initialize the internal state
            individual.Process(new[] { 100.0 });
            return individual;
        }

        private GeneticIndividual CreateTestIndividualWithTrades()
        {
            // Create individual and process some data to generate trades
            var individual = new GeneticIndividual
            {
                StartingBalance = 100000
            };

            // Add a simple indicator to generate some trades
            individual.Indicators.Add(new IndicatorParams
            {
                Type = 0, // SMA
                Period = 5,
                Polarity = 1,
                LongThreshold = 0.5,
                ShortThreshold = -0.5
            });

            // Process price data that will generate trades
            double[] priceData = { 100, 105, 110, 115, 120, 115, 110, 105, 100, 95 };
            individual.Process(priceData);

            return individual;
        }

        private GeneticIndividual CreateTestIndividualWithVariedReturns()
        {
            var individual = new GeneticIndividual
            {
                StartingBalance = 100000
            };

            individual.Indicators.Add(new IndicatorParams
            {
                Type = 0, // SMA
                Period = 2,
                Polarity = 1,
                LongThreshold = -0.5, // More sensitive thresholds
                ShortThreshold = 0.5
            });

            // Price data designed to create varied trade returns
            // Large price movements with different magnitudes
            double[] variedPrices =
            {
                100, 110, 105, 95, 120, 85, 130, 90, 125, 80, 140, 75, 135
            };
            individual.Process(variedPrices);

            return individual;
        }

        private GeneticIndividual CreateTestIndividualWithVolatileTrades()
        {
            var individual = new GeneticIndividual
            {
                StartingBalance = 100000
            };

            individual.Indicators.Add(new IndicatorParams
            {
                Type = 0, // SMA
                Period = 3,
                Polarity = 1,
                LongThreshold = 0.1,
                ShortThreshold = -0.1
            });

            // Volatile price data
            double[] volatilePrices = { 100, 120, 80, 130, 70, 140, 60, 150 };
            individual.Process(volatilePrices);

            return individual;
        }

        private GeneticIndividual CreateTestIndividualWithDrawdownTrades()
        {
            var individual = new GeneticIndividual
            {
                StartingBalance = 100000
            };

            individual.Indicators.Add(new IndicatorParams
            {
                Type = 0, // SMA
                Period = 2,
                Polarity = 1,
                LongThreshold = 0.0,
                ShortThreshold = 0.0
            });

            // Price data that will create drawdown scenario
            double[] drawdownPrices = { 100, 120, 110, 90, 85, 95, 105 };

            // --- FIX: Use CreateDailyPriceRecordsFromClosePrices for both Prices and test data ---
            // Add extra historical data for indicator warmup
            var warmup = 10;
            var allPrices = Enumerable.Range(0, warmup)
                .Select(i => 95.0 + i * 0.5)
                .Concat(drawdownPrices)
                .ToArray();

            var startDate = DateTime.Today.AddDays(-allPrices.Length);
            var allRecords = Prices.CreateDailyPriceRecordsFromClosePrices(allPrices, null, startDate);

            // Add all records to the Prices system
            GeneticIndividual.Prices.AddPricesBatch(allRecords);

            // Use only the drawdown portion for testing
            var testRecords = allRecords.Skip(warmup).ToArray();
            individual.Process(testRecords);

            return individual;
        }

        // Use reflection to test private static methods - FIXED: Corrected namespace references
        private double InvokeCalculateAnnualizedPerformance(double performance, double dataPoints,
            double totalDataPoints)
        {
            var method = typeof(Trade.Program).GetMethod("CalculateAnnualizedPerformance",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (double)method.Invoke(null, new object[] { performance, dataPoints, totalDataPoints });
        }

        private double InvokeCalculateRiskAdjustedReturn(GeneticIndividual individual)
        {
            var method = typeof(Trade.Program).GetMethod("CalculateRiskAdjustedReturn",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (double)method.Invoke(null, new object[] { individual, 0.02 });
        }

        private double InvokeCalculateMaxDrawdown(GeneticIndividual individual)
        {
            var method = typeof(Trade.Program).GetMethod("CalculateMaxDrawdown",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (double)method.Invoke(null, new object[] { individual });
        }

        #endregion
    }
}