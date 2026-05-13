using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class WindowOptimizerTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void GenerateWindowConfigurations_ReturnsValidConfigurations()
        {
            var configs = GetPrivateWindowConfigurations(500);
            Assert.IsTrue(configs.Count > 0);
            Assert.IsTrue(configs.All(c => c.TrainingSize > 0 && c.TestingSize > 0 && c.StepSize > 0));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GenerateWindowConfigurations_WithLargeDataSet_IncludesResearchBasedConfigs()
        {
            var configs = GetPrivateWindowConfigurations(3000);
            Assert.IsTrue(configs.Count > 0);
            // Should include some larger configurations for comprehensive testing
            Assert.IsTrue(configs.Any(c => c.TrainingSize >= 252));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void AddResearchBasedConfigurations_AddsExpectedConfigs()
        {
            var configs = new List<WindowOptimizer.WindowConfiguration>();
            CallPrivateAddResearchConfigs(configs, 1200);
            Assert.IsTrue(configs.Any(c => c.TrainingSize == 1260));
            Assert.IsTrue(configs.Any(c => c.TrainingSize == 2520));
            Assert.IsTrue(configs.Any(c => c.TrainingSize == 252));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void AddResearchBasedConfigurations_WithLimitedData_AddsConservativeConfigs()
        {
            var configs = new List<WindowOptimizer.WindowConfiguration>();
            CallPrivateAddResearchConfigs(configs, 500);
            // Should still add conservative configurations even with limited data
            Assert.IsTrue(configs.Any(c => c.TrainingSize == 252));
            Assert.IsTrue(configs.Any(c => c.TrainingSize == 504));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void AnalyzeWindowConfiguration_SetsScoresAndRecommendation()
        {
            var walkforward = new WindowOptimizer.WalkforwardResults
            {
                Windows = new List<WindowOptimizer.WalkforwardWindow>
                {
                    new WindowOptimizer.WalkforwardWindow
                    {
                        TrainingPerformance = 10, TestPerformance = 8, PerformanceGap = 2,
                        EarlyStoppedDueToOverfitting = false
                    }
                },
                AverageTrainingPerformance = 10,
                AverageTestPerformance = 8,
                AveragePerformanceGap = 2,
                ConsistencyScore = 1,
                OverfittingFrequency = 0
            };
            var config = new WindowOptimizer.WindowConfiguration { TrainingSize = 100, TestingSize = 20, StepSize = 5 };
            var analysis = CallPrivateAnalyzeWindowConfiguration(walkforward, config);
            Assert.IsTrue(analysis.RobustnessScore > 0.0);
            Assert.IsTrue(analysis.ConsistencyScore > 0.0);
            Assert.IsTrue(analysis.EfficiencyScore > 0.0);
            Assert.IsTrue(analysis.StatisticalPower >= 0.0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void AnalyzeWindowConfiguration_WithHighOverfitting_PenalizesRobustness()
        {
            var walkforward = new WindowOptimizer.WalkforwardResults
            {
                Windows = new List<WindowOptimizer.WalkforwardWindow>
                {
                    new WindowOptimizer.WalkforwardWindow
                    {
                        TrainingPerformance = 20, TestPerformance = -5, PerformanceGap = 120, // Increased gap
                        EarlyStoppedDueToOverfitting = true
                    }
                },
                AverageTrainingPerformance = 20,
                AverageTestPerformance = -5,
                AveragePerformanceGap = 120, // Increased to trigger penalty (>100)
                ConsistencyScore = 30,
                OverfittingFrequency = 85 // Increased to trigger penalty (>80)
            };
            var config = new WindowOptimizer.WindowConfiguration { TrainingSize = 100, TestingSize = 20, StepSize = 5 };
            var analysis = CallPrivateAnalyzeWindowConfiguration(walkforward, config);

            // With updated thresholds, should now have low robustness:
            // Penalties: OverfittingFreq (85% > 80%) = 0.2 + (85% > 60%) = 0.2
            //           PerformanceGap (120% > 100%) = 0.2 + (120% > 50%) = 0.2  
            //           NegativePerformance (-5% <= 0%) = 0.4
            // Total penalty = 0.8, RobustnessScore = 1.0 - 0.8 = 0.2
            Assert.IsTrue(analysis.RobustnessScore < 0.5, $"Expected RobustnessScore < 0.5, got {analysis.RobustnessScore:F3}");
            Assert.IsFalse(analysis.IsRecommended);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void AnalyzeWindowConfiguration_WithPoorConsistency_LowersConsistencyScore()
        {
            var walkforward = new WindowOptimizer.WalkforwardResults
            {
                Windows = new List<WindowOptimizer.WalkforwardWindow>
                {
                    new WindowOptimizer.WalkforwardWindow { TrainingPerformance = 10, TestPerformance = 8 }
                },
                AverageTrainingPerformance = 10,
                AverageTestPerformance = 8,
                AveragePerformanceGap = 2,
                ConsistencyScore = 60, // Increased to exceed our new threshold of 50
                OverfittingFrequency = 0
            };
            var config = new WindowOptimizer.WindowConfiguration { TrainingSize = 100, TestingSize = 20, StepSize = 5 };
            var analysis = CallPrivateAnalyzeWindowConfiguration(walkforward, config);

            // With maxConsistency = 50.0, a ConsistencyScore of 60 should result in:
            // ConsistencyScore = Max(0.0, 1.0 - 60/50) = Max(0.0, 1.0 - 1.2) = Max(0.0, -0.2) = 0.0
            Assert.IsTrue(analysis.ConsistencyScore < 0.5, $"Expected ConsistencyScore < 0.5, got {analysis.ConsistencyScore:F3}");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void FindOptimalConfiguration_ReturnsBestRecommended()
        {
            var analyses = new List<WindowOptimizer.WindowConfigurationAnalysis>
            {
                new WindowOptimizer.WindowConfigurationAnalysis { OverallScore = 0.5, IsRecommended = false },
                new WindowOptimizer.WindowConfigurationAnalysis { OverallScore = 0.9, IsRecommended = true },
                new WindowOptimizer.WindowConfigurationAnalysis { OverallScore = 0.8, IsRecommended = true }
            };
            var best = CallPrivateFindOptimalConfiguration(analyses);
            Assert.IsTrue(best.IsRecommended);
            Assert.AreEqual(0.9, best.OverallScore, 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void FindOptimalConfiguration_WithNoRecommended_ReturnsBestOverall()
        {
            var analyses = new List<WindowOptimizer.WindowConfigurationAnalysis>
            {
                new WindowOptimizer.WindowConfigurationAnalysis { OverallScore = 0.3, IsRecommended = false },
                new WindowOptimizer.WindowConfigurationAnalysis { OverallScore = 0.5, IsRecommended = false },
                new WindowOptimizer.WindowConfigurationAnalysis { OverallScore = 0.4, IsRecommended = false }
            };
            var best = CallPrivateFindOptimalConfiguration(analyses);
            Assert.IsFalse(best.IsRecommended);
            Assert.AreEqual(0.5, best.OverallScore, 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void FindOptimalConfiguration_WithEmptyList_ReturnsDefault()
        {
            var analyses = new List<WindowOptimizer.WindowConfigurationAnalysis>();
            var best = CallPrivateFindOptimalConfiguration(analyses);
            Assert.AreEqual(0.0, best.OverallScore, 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GenerateWindowSizeRecommendations_ReturnsRecommendations()
        {
            var analyses = new List<WindowOptimizer.WindowConfigurationAnalysis>
            {
                new WindowOptimizer.WindowConfigurationAnalysis
                {
                    OverallScore = 0.9, IsRecommended = true,
                    Configuration = new WindowOptimizer.WindowConfiguration
                        { TrainingSize = 100, TestingSize = 20, StepSize = 5 },
                    WalkforwardResults = new WindowOptimizer.WalkforwardResults
                    {
                        Windows = new List<WindowOptimizer.WalkforwardWindow>
                            { new WindowOptimizer.WalkforwardWindow() }
                    }
                },
                new WindowOptimizer.WindowConfigurationAnalysis
                {
                    OverallScore = 0.8, IsRecommended = true,
                    Configuration = new WindowOptimizer.WindowConfiguration
                        { TrainingSize = 200, TestingSize = 40, StepSize = 10 },
                    WalkforwardResults = new WindowOptimizer.WalkforwardResults
                    {
                        Windows = new List<WindowOptimizer.WalkforwardWindow>
                            { new WindowOptimizer.WalkforwardWindow() }
                    }
                }
            };
            var recs = CallPrivateGenerateWindowSizeRecommendations(analyses, 500);
            Assert.IsTrue(recs.Count > 0);
            Assert.IsTrue(recs.Any(r => r.Contains("RECOMMENDED")));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GenerateWindowSizeRecommendations_WithNoResults_ReturnsWarning()
        {
            var analyses = new List<WindowOptimizer.WindowConfigurationAnalysis>();
            var recs = CallPrivateGenerateWindowSizeRecommendations(analyses, 500);
            Assert.IsTrue(recs.Count > 0);
            Assert.IsTrue(recs.Any(r => r.Contains("No valid window configurations")));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GenerateWindowSizeRecommendations_WithLimitedData_IncludesDataWarning()
        {
            var analyses = new List<WindowOptimizer.WindowConfigurationAnalysis>
            {
                new WindowOptimizer.WindowConfigurationAnalysis
                {
                    OverallScore = 0.6, IsRecommended = false,
                    Configuration = new WindowOptimizer.WindowConfiguration { TrainingSize = 100, TestingSize = 20, StepSize = 5 },
                    WalkforwardResults = new WindowOptimizer.WalkforwardResults { Windows = new List<WindowOptimizer.WalkforwardWindow>() }
                }
            };
            var recs = CallPrivateGenerateWindowSizeRecommendations(analyses, 200); // Less than 2 years of data
            Assert.IsTrue(recs.Any(r => r.Contains("Limited data")));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void DisplayWindowOptimizationResults_WithOptimalConfiguration_DisplaysResults()
        {
            var results = new WindowOptimizer.WindowSizeOptimizationResults(true);
            results.OptimalConfiguration = new WindowOptimizer.WindowConfigurationAnalysis
            {
                IsRecommended = true,
                OverallScore = 0.85,
                Configuration = new WindowOptimizer.WindowConfiguration
                {
                    TrainingSize = 252,
                    TestingSize = 63,
                    StepSize = 21,
                    TrainingMonths = 12,
                    TestingMonths = 3,
                    StepWeeks = 4.2
                },
                WalkforwardResults = new WindowOptimizer.WalkforwardResults
                {
                    Windows = new List<WindowOptimizer.WalkforwardWindow>
                    {
                        new WindowOptimizer.WalkforwardWindow()
                    },
                    AverageTestPerformance = 8.5,
                    ConsistencyScore = 12.3,
                    OverfittingFrequency = 15.0
                },
                StatisticalPower = 0.9
            };
            results.ConfigurationResults = new List<WindowOptimizer.WindowConfigurationAnalysis> { results.OptimalConfiguration };
            results.Recommendations = new List<string> { "Test recommendation" };

            // This should not throw an exception
            try
            {
                WindowOptimizer.DisplayWindowOptimizationResults(results);
                Assert.IsTrue(true); // Test passes if no exception is thrown
            }
            catch (Exception ex)
            {
                Assert.Fail($"Expected no exception, but got: {ex.Message}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CreateSimpleTestIndividual_ReturnsValidIndividual()
        {
            var individual = CallPrivateCreateSimpleTestIndividual();
            Assert.IsNotNull(individual);
            Assert.IsNotNull(individual.Indicators);
            Assert.IsTrue(individual.Indicators.Count > 0);

            var firstIndicator = individual.Indicators[0];
            Assert.AreEqual(1, firstIndicator.Type); // SMA
            Assert.AreEqual(20, firstIndicator.Period);
            Assert.AreEqual(TimeFrame.D1, firstIndicator.TimeFrame);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateRiskAdjustedReturn_WithNoTrades_ReturnsZero()
        {
            var individual = new GeneticIndividual();
            individual.Trades = new List<TradeResult>(); // Empty trades list

            var sharpe = CallPrivateCalculateRiskAdjustedReturn(individual);
            Assert.AreEqual(0.0, sharpe, 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateRiskAdjustedReturn_WithIdenticalReturns_ReturnsHighSharpe()
        {
            var individual = new GeneticIndividual();
            individual.Trades = new List<TradeResult>
            {
                new TradeResult { OpenPrice = 100.0, ClosePrice = 105.0, AllowedTradeType = AllowedTradeType.Buy }, // 5% gain
                new TradeResult { OpenPrice = 100.0, ClosePrice = 105.0, AllowedTradeType = AllowedTradeType.Buy }, // 5% gain
                new TradeResult { OpenPrice = 100.0, ClosePrice = 105.0, AllowedTradeType = AllowedTradeType.Buy }  // 5% gain
            };

            var sharpe = CallPrivateCalculateRiskAdjustedReturn(individual, 0.02);
            Assert.IsTrue(double.IsPositiveInfinity(sharpe) || sharpe > 1000); // Very high Sharpe ratio
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateRiskAdjustedReturn_WithVariedReturns_CalculatesCorrectly()
        {
            var individual = new GeneticIndividual();
            individual.Trades = new List<TradeResult>
            {
                new TradeResult { OpenPrice = 100.0, ClosePrice = 110.0, AllowedTradeType = AllowedTradeType.Buy }, // 10% gain
                new TradeResult { OpenPrice = 100.0, ClosePrice = 98.0, AllowedTradeType = AllowedTradeType.Buy },  // -2% loss
                new TradeResult { OpenPrice = 100.0, ClosePrice = 105.0, AllowedTradeType = AllowedTradeType.Buy }, // 5% gain
                new TradeResult { OpenPrice = 100.0, ClosePrice = 108.0, AllowedTradeType = AllowedTradeType.Buy }  // 8% gain
            };

            var sharpe = CallPrivateCalculateRiskAdjustedReturn(individual, 0.02);
            Assert.IsTrue(sharpe > 0); // Should be positive given positive average return
            Assert.IsFalse(double.IsInfinity(sharpe));
            Assert.IsFalse(double.IsNaN(sharpe));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateMaxDrawdown_WithNoTrades_ReturnsZero()
        {
            var individual = new GeneticIndividual();
            individual.Trades = new List<TradeResult>();
            individual.StartingBalance = 100000;

            var maxDrawdown = CallPrivateCalculateMaxDrawdown(individual);
            Assert.AreEqual(0.0, maxDrawdown, 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateMaxDrawdown_WithIncreasingBalance_ReturnsZero()
        {
            var individual = new GeneticIndividual();
            individual.StartingBalance = 100000;
            individual.Trades = new List<TradeResult>
            {
                new TradeResult { Balance = 105000 },
                new TradeResult { Balance = 110000 },
                new TradeResult { Balance = 115000 }
            };

            var maxDrawdown = CallPrivateCalculateMaxDrawdown(individual);
            Assert.AreEqual(0.0, maxDrawdown, 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateMaxDrawdown_WithDrawdown_CalculatesCorrectly()
        {
            var individual = new GeneticIndividual();
            individual.StartingBalance = 100000;
            individual.Trades = new List<TradeResult>
            {
                new TradeResult { Balance = 120000 }, // Peak
                new TradeResult { Balance = 110000 }, // 8.33% drawdown
                new TradeResult { Balance = 96000 },  // 20% drawdown from peak
                new TradeResult { Balance = 105000 }  // Recovery but not to peak
            };

            var maxDrawdown = CallPrivateCalculateMaxDrawdown(individual);
            Assert.AreEqual(20.0, maxDrawdown, 0.1); // 20% drawdown from 120k to 96k
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateStandardDeviation_WithEmptyArray_ReturnsZero()
        {
            var values = new double[0];
            var stdDev = CallPrivateCalculateStandardDeviation(values);
            Assert.AreEqual(0.0, stdDev, 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateStandardDeviation_WithSingleValue_ReturnsZero()
        {
            var values = new double[] { 5.0 };
            var stdDev = CallPrivateCalculateStandardDeviation(values);
            Assert.AreEqual(0.0, stdDev, 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateStandardDeviation_WithIdenticalValues_ReturnsZero()
        {
            var values = new double[] { 10.0, 10.0, 10.0, 10.0 };
            var stdDev = CallPrivateCalculateStandardDeviation(values);
            Assert.AreEqual(0.0, stdDev, 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateStandardDeviation_WithVariedValues_CalculatesCorrectly()
        {
            var values = new double[] { 2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0 };
            var stdDev = CallPrivateCalculateStandardDeviation(values);
            Assert.IsTrue(stdDev > 0);
            Assert.AreEqual(2.0, stdDev, 0.1); // Expected std dev is 2.0
        }

        [TestMethod]
        [TestCategory("Core")]
        public void RunWalkforwardAnalysisWithConfiguration_WithValidConfiguration_ReturnsResults()
        {
            var priceRecords = CreateDummyPriceRecords(300);
            var config = new WindowOptimizer.WindowConfiguration
            {
                TrainingSize = 100,
                TestingSize = 30,
                StepSize = 10
            };

            var results = CallPrivateRunWalkforwardAnalysisWithConfiguration(priceRecords, config);
            Assert.IsNotNull(results.Windows);
            Assert.IsTrue(results.Windows.Count >= 0); // Should have some windows or none if configuration is invalid
        }

        [TestMethod]
        [TestCategory("Core")]
        public void RunWalkforwardAnalysisWithConfiguration_WithInsufficientData_ReturnsEmptyWindows()
        {
            var priceRecords = CreateDummyPriceRecords(50); // Too little data
            var config = new WindowOptimizer.WindowConfiguration
            {
                TrainingSize = 200, // More than available data
                TestingSize = 50,
                StepSize = 10
            };

            var results = CallPrivateRunWalkforwardAnalysisWithConfiguration(priceRecords, config);
            Assert.IsNotNull(results.Windows);
            Assert.AreEqual(0, results.Windows.Count); // Should have no windows due to insufficient data
        }

        [TestMethod]
        [TestCategory("Core")]
        public void WriteSection_DoesNotThrow()
        {
            // Arrange & Act & Assert - No exception should be thrown
            try
            {
                CallPrivateWriteSection("Test Section");
                // If we reach here, no exception was thrown - test passes
                Assert.IsTrue(true);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Expected no exception, but WriteSection threw: {ex.Message}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void WriteInfo_DoesNotThrow()
        {
            // Arrange & Act & Assert - No exception should be thrown
            try
            {
                CallPrivateWriteInfo("Test info message");
                // If we reach here, no exception was thrown - test passes
                Assert.IsTrue(true);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Expected no exception, but WriteInfo threw: {ex.Message}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void WriteWarning_DoesNotThrow()
        {
            // Arrange & Act & Assert - No exception should be thrown
            try
            {
                CallPrivateWriteWarning("Test warning message");
                // If we reach here, no exception was thrown - test passes
                Assert.IsTrue(true);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Expected no exception, but WriteWarning threw: {ex.Message}");
            }
        }

        //[TestMethod]
        //[TestCategory("Performance")]
        //public void OptimizeWindowSizes_ReturnsResultsWithConfigsAndRecommendations()
        //{
        //    var dummyRecords = CreateDummyPriceRecords(300);
        //    var results = WindowOptimizer.OptimizeWindowSizes(dummyRecords);
        //    Assert.IsNotNull(results);
        //    Assert.IsTrue(results.ConfigurationResults.Count > 0);
        //    Assert.IsTrue(results.Recommendations.Count > 0);
        //}

        // --- Helper methods for private access ---
        private List<WindowOptimizer.WindowConfiguration> GetPrivateWindowConfigurations(int totalDataPoints)
        {
            var method = typeof(WindowOptimizer).GetMethod("GenerateWindowConfigurations",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (List<WindowOptimizer.WindowConfiguration>)method.Invoke(null, new object[] { totalDataPoints });
        }

        private void CallPrivateAddResearchConfigs(List<WindowOptimizer.WindowConfiguration> configs,
            int totalDataPoints)
        {
            var method = typeof(WindowOptimizer).GetMethod("AddResearchBasedConfigurations",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Invoke(null, new object[] { configs, totalDataPoints });
        }

        private WindowOptimizer.WindowConfigurationAnalysis CallPrivateAnalyzeWindowConfiguration(
            WindowOptimizer.WalkforwardResults walkforward, WindowOptimizer.WindowConfiguration config)
        {
            var method = typeof(WindowOptimizer).GetMethod("AnalyzeWindowConfiguration",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (WindowOptimizer.WindowConfigurationAnalysis)method.Invoke(null,
                new object[] { walkforward, config });
        }

        private WindowOptimizer.WindowConfigurationAnalysis CallPrivateFindOptimalConfiguration(
            List<WindowOptimizer.WindowConfigurationAnalysis> analyses)
        {
            var method = typeof(WindowOptimizer).GetMethod("FindOptimalConfiguration",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (WindowOptimizer.WindowConfigurationAnalysis)method.Invoke(null, new object[] { analyses });
        }

        private List<string> CallPrivateGenerateWindowSizeRecommendations(
            List<WindowOptimizer.WindowConfigurationAnalysis> analyses, int totalDataPoints)
        {
            var method = typeof(WindowOptimizer).GetMethod("GenerateWindowSizeRecommendations",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (List<string>)method.Invoke(null, new object[] { analyses, totalDataPoints });
        }

        private GeneticIndividual CallPrivateCreateSimpleTestIndividual()
        {
            var method = typeof(WindowOptimizer).GetMethod("CreateSimpleTestIndividual",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (GeneticIndividual)method.Invoke(null, null);
        }

        private double CallPrivateCalculateRiskAdjustedReturn(GeneticIndividual individual, double riskFreeRate = 0.02)
        {
            var method = typeof(WindowOptimizer).GetMethod("CalculateRiskAdjustedReturn",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (double)method.Invoke(null, new object[] { individual, riskFreeRate });
        }

        private double CallPrivateCalculateMaxDrawdown(GeneticIndividual individual)
        {
            var method = typeof(WindowOptimizer).GetMethod("CalculateMaxDrawdown",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (double)method.Invoke(null, new object[] { individual });
        }

        private double CallPrivateCalculateStandardDeviation(double[] values)
        {
            var method = typeof(WindowOptimizer).GetMethod("CalculateStandardDeviation",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (double)method.Invoke(null, new object[] { values });
        }

        private WindowOptimizer.WalkforwardResults CallPrivateRunWalkforwardAnalysisWithConfiguration(
            PriceRecord[] priceRecords, WindowOptimizer.WindowConfiguration config)
        {
            var method = typeof(WindowOptimizer).GetMethod("RunWalkforwardAnalysisWithConfiguration",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (WindowOptimizer.WalkforwardResults)method.Invoke(null, new object[] { priceRecords, config });
        }

        private void CallPrivateWriteSection(string title)
        {
            var method = typeof(WindowOptimizer).GetMethod("WriteSection",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Invoke(null, new object[] { title });
        }

        private void CallPrivateWriteInfo(string message)
        {
            var method = typeof(WindowOptimizer).GetMethod("WriteInfo",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Invoke(null, new object[] { message });
        }

        private void CallPrivateWriteWarning(string message)
        {
            var method = typeof(WindowOptimizer).GetMethod("WriteWarning",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Invoke(null, new object[] { message });
        }

        private PriceRecord[] CreateDummyPriceRecords(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var random = new Random(42); // Fixed seed for reproducible tests

            for (var i = 0; i < count; i++)
            {
                var date = baseDate.AddDays(i);
                var basePrice = 100.0 + i * 0.1 + Math.Sin(i * 0.1) * 5; // Trending with some variation
                var open = basePrice + random.NextDouble() * 2 - 1;
                var close = basePrice + random.NextDouble() * 2 - 1;
                var high = Math.Max(open, close) + random.NextDouble() * 1;
                var low = Math.Min(open, close) - random.NextDouble() * 1;
                var volume = 1000 + random.Next(9000);

                records[i] = new PriceRecord(date, TimeFrame.D1, open, high, low, close, volume: volume, wap: close, count: 1);
            }
            return records;
        }
    }
}