using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Performance tests for Program.DataScience class methods to measure and validate optimization improvements
    /// </summary>
    [TestClass]
    public class ProgramDataSciencePerformanceTests
    {
        // Performance thresholds in milliseconds for different [TestMethod][TestCategory("Core")]
        private const double DATA_SPLITS_THRESHOLD_MS = 100;           // Data splitting [TestMethod][TestCategory("Core")]
        private const double NORMALIZATION_THRESHOLD_MS = 200;          // Normalization parameter calculation
        private const double MODEL_TRAINING_THRESHOLD_MS = 60000;      // 30 seconds for model training
        private const double CROSS_VALIDATION_THRESHOLD_MS = 180000;    // 60 seconds for CV
        private const double ROBUSTNESS_COMPUTATION_THRESHOLD_MS = 5000; // 5 seconds for robustness

        // Test iteration counts
        private const int WARMUP_ITERATIONS = 3;
        private const int PERFORMANCE_ITERATIONS = 10;
        
        private static readonly Dictionary<string, double> _performanceResults = new Dictionary<string, double>();

        #region Test Setup and Cleanup

        [ClassCleanup]
        public static void ClassCleanup()
        {
            ConsoleUtilities.WriteLine("\n=== PROGRAM DATA SCIENCE PERFORMANCE SUMMARY ===");
            ConsoleUtilities.WriteLine("Method                                    | Avg Time (ms) | Threshold  | Status");
            ConsoleUtilities.WriteLine("------------------------------------------|---------------|------------|--------");
            
            foreach (var result in _performanceResults.OrderBy(r => r.Value))
            {
                var threshold = GetThresholdForMethod(result.Key);
                var status = result.Value <= threshold ? "PASS" : "FAIL";
                ConsoleUtilities.WriteLine($"{result.Key,-41} | {result.Value,13:F2} | {threshold,10:F0} | {status}");
            }
            ConsoleUtilities.WriteLine("================================================================================");
        }

        private static double GetThresholdForMethod(string methodName)
        {
            if (methodName.Contains("DataSplits")) return DATA_SPLITS_THRESHOLD_MS;
            if (methodName.Contains("Normalization")) return NORMALIZATION_THRESHOLD_MS;
            if (methodName.Contains("ModelTraining")) return MODEL_TRAINING_THRESHOLD_MS;
            if (methodName.Contains("CrossValidation")) return CROSS_VALIDATION_THRESHOLD_MS;
            if (methodName.Contains("Robustness")) return ROBUSTNESS_COMPUTATION_THRESHOLD_MS;
            return 1000; // Default threshold
        }

        #endregion

        #region Data Splitting Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void CreateProperDataSplits_SmallDataset_Performance()
        {
            // Arrange
            var priceRecords = CreateTestPriceRecords(252); // 1 year of data
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivateCreateProperDataSplits(priceRecords);
                // Use reflection to safely access the struct fields
                var resultType = result.GetType();
                var training = (PriceRecord[])resultType.GetField("Training").GetValue(result);
                var validation = (PriceRecord[])resultType.GetField("Validation").GetValue(result);
                var test = (PriceRecord[])resultType.GetField("Test").GetValue(result);
                
                Assert.IsNotNull(training);
                Assert.IsNotNull(validation);
                Assert.IsNotNull(test);
                Assert.IsTrue(training.Length > 0);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["DataSplits_Small_252"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Data Splits (252 records): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < DATA_SPLITS_THRESHOLD_MS / 10, 
                $"Small data splits took {avgTime:F4}ms, expected < {DATA_SPLITS_THRESHOLD_MS / 10}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CreateProperDataSplits_MediumDataset_Performance()
        {
            // Arrange
            var priceRecords = CreateTestPriceRecords(1260); // 5 years of data
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivateCreateProperDataSplits(priceRecords);
                // Use reflection to safely access the struct fields
                var resultType = result.GetType();
                var training = (PriceRecord[])resultType.GetField("Training").GetValue(result);
                var validation = (PriceRecord[])resultType.GetField("Validation").GetValue(result);
                var test = (PriceRecord[])resultType.GetField("Test").GetValue(result);
                
                Assert.IsNotNull(training);
                Assert.IsNotNull(validation);
                Assert.IsNotNull(test);
                Assert.IsTrue(training.Length > 0);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["DataSplits_Medium_1260"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Data Splits (1260 records): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < DATA_SPLITS_THRESHOLD_MS / 2, 
                $"Medium data splits took {avgTime:F4}ms, expected < {DATA_SPLITS_THRESHOLD_MS / 2}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CreateProperDataSplits_LargeDataset_Performance()
        {
            // Arrange
            var priceRecords = CreateTestPriceRecords(2520); // 10 years of data
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivateCreateProperDataSplits(priceRecords);
                // Use reflection to safely access the struct fields
                var resultType = result.GetType();
                var training = (PriceRecord[])resultType.GetField("Training").GetValue(result);
                var validation = (PriceRecord[])resultType.GetField("Validation").GetValue(result);
                var test = (PriceRecord[])resultType.GetField("Test").GetValue(result);
                
                Assert.IsNotNull(training);
                Assert.IsNotNull(validation);
                Assert.IsNotNull(test);
                Assert.IsTrue(training.Length > 0);
            }, PERFORMANCE_ITERATIONS / 2); // Fewer iterations for large dataset
            
            _performanceResults["DataSplits_Large_2520"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Data Splits (2520 records): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < DATA_SPLITS_THRESHOLD_MS, 
                $"Large data splits took {avgTime:F4}ms, expected < {DATA_SPLITS_THRESHOLD_MS}ms");
        }

        #endregion

        #region Normalization Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void ComputeNormalizationParameters_SmallDataset_Performance()
        {
            // Arrange
            var priceRecords = CreateTestPriceRecords(252); // 1 year of data
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivateComputeNormalizationParameters(priceRecords);
                // Use reflection to safely access the struct fields
                var resultType = result.GetType();
                var minPrice = (double)resultType.GetField("MinPrice").GetValue(result);
                var maxPrice = (double)resultType.GetField("MaxPrice").GetValue(result);
                var meanPrice = (double)resultType.GetField("MeanPrice").GetValue(result);
                var stdPrice = (double)resultType.GetField("StdPrice").GetValue(result);
                
                Assert.IsTrue(minPrice > 0);
                Assert.IsTrue(maxPrice > minPrice);
                Assert.IsTrue(meanPrice > 0);
                Assert.IsTrue(stdPrice >= 0);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["Normalization_Small_252"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Normalization Parameters (252 records): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < NORMALIZATION_THRESHOLD_MS / 5, 
                $"Small normalization took {avgTime:F4}ms, expected < {NORMALIZATION_THRESHOLD_MS / 5}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void ComputeNormalizationParameters_LargeDataset_Performance()
        {
            // Arrange
            var priceRecords = CreateTestPriceRecords(2520); // 10 years of data
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivateComputeNormalizationParameters(priceRecords);
                // Use reflection to safely access the struct fields
                var resultType = result.GetType();
                var minPrice = (double)resultType.GetField("MinPrice").GetValue(result);
                var maxPrice = (double)resultType.GetField("MaxPrice").GetValue(result);
                var meanPrice = (double)resultType.GetField("MeanPrice").GetValue(result);
                var stdPrice = (double)resultType.GetField("StdPrice").GetValue(result);
                
                Assert.IsTrue(minPrice > 0);
                Assert.IsTrue(maxPrice > minPrice);
                Assert.IsTrue(meanPrice > 0);
                Assert.IsTrue(stdPrice >= 0);
            }, PERFORMANCE_ITERATIONS / 2);
            
            _performanceResults["Normalization_Large_2520"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Normalization Parameters (2520 records): {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < NORMALIZATION_THRESHOLD_MS, 
                $"Large normalization took {avgTime:F4}ms, expected < {NORMALIZATION_THRESHOLD_MS}ms");
        }

        #endregion

        #region Model Training Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void TrainModelCandidates_Performance()
        {
            // Arrange
            var trainingRecords = CreateTestPriceRecords(150); // 6 months training (reduced for testing)
            var validationRecords = CreateTestPriceRecords(50); // 2 months validation
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivateTrainModelCandidates(trainingRecords, validationRecords);
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Count > 0);
                
                // Verify all candidates have valid data using reflection
                foreach (var candidate in result)
                {
                    var candidateType = candidate.GetType();
                    var model = candidateType.GetField("Model").GetValue(candidate);
                    var description = (string)candidateType.GetField("Description").GetValue(candidate);
                    var hyperparameters = candidateType.GetField("Hyperparameters").GetValue(candidate);
                    
                    Assert.IsNotNull(model);
                    Assert.IsNotNull(description);
                    Assert.IsNotNull(hyperparameters);
                }
            }, 3); // Very few iterations due to expensive GA [TestMethod][TestCategory("Core")]
            
            _performanceResults["ModelTraining_Candidates"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Model Candidates Training: {avgTime:F2}ms avg");
            Assert.IsTrue(avgTime < MODEL_TRAINING_THRESHOLD_MS, 
                $"Model training took {avgTime:F2}ms, expected < {MODEL_TRAINING_THRESHOLD_MS}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void SelectBestModel_Performance()
        {
            // Arrange
            var candidates = CreateTestModelCandidates(5);
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivateSelectBestModel(candidates);
                // Use reflection to safely access the struct fields
                var resultType = result.GetType();
                var model = resultType.GetField("Model").GetValue(result);
                var description = (string)resultType.GetField("Description").GetValue(result);
                
                Assert.IsNotNull(model);
                Assert.IsNotNull(description);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["ModelSelection_Best"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Best Model Selection: {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < 300, // Should be very fast
                $"Model selection took {avgTime:F4}ms, expected < 300ms");
        }

        #endregion

        #region Cross-Validation Performance Tests

        //[TestMethod]
        public void PerformTimeSeriesCrossValidation_SmallDataset_Performance()
        {
            // Arrange
            var allData = CreateTestPriceRecords(500); // Reduced for testing performance
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                var result = CallPrivatePerformTimeSeriesCrossValidation(allData);
                // Use reflection to safely access the struct fields
                var resultType = result.GetType();
                var numFolds = (int)resultType.GetField("NumFolds").GetValue(result);
                var scores = (double[])resultType.GetField("Scores").GetValue(result);
                
                Assert.IsTrue(numFolds > 0);
                Assert.IsNotNull(scores);
                Assert.IsTrue(scores.Length > 0);
            }, 2); // Very few iterations due to expensive [TestMethod][TestCategory("Core")]
            
            _performanceResults["CrossValidation_TimeSeries_500"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Time Series CV (500 records): {avgTime:F2}ms avg");
            Assert.IsTrue(avgTime < CROSS_VALIDATION_THRESHOLD_MS, 
                $"Time series CV took {avgTime:F2}ms, expected < {CROSS_VALIDATION_THRESHOLD_MS}ms");
        }

        #endregion

        #region Robustness Computation Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void ComputeRobustnessOnTrainVal_Performance()
        {
            // Arrange
            var training = CreateTestPriceRecords(100);
            var validation = CreateTestPriceRecords(30);
            var candidate = CreateTestModelCandidate();
            
            // Act & Assert
            var avgTime = PerformanceTimer.TimeActionAverage(() =>
            {
                CallPrivateComputeRobustnessOnTrainVal(ref candidate, training, validation);
                // Use reflection to verify robustness computation occurred
                var candidateType = candidate.GetType();
                var robustnessScore = (double)candidateType.GetField("RobustnessScore").GetValue(candidate);
                var wfPassRate = (double)candidateType.GetField("WFPassRate").GetValue(candidate);
                
                Assert.IsTrue(robustnessScore != 0 || wfPassRate != 0);
            }, PERFORMANCE_ITERATIONS);
            
            _performanceResults["Robustness_Computation"] = avgTime;
            
            ConsoleUtilities.WriteLine($"[PERF] Robustness Computation: {avgTime:F4}ms avg");
            Assert.IsTrue(avgTime < ROBUSTNESS_COMPUTATION_THRESHOLD_MS, 
                $"Robustness computation took {avgTime:F4}ms, expected < {ROBUSTNESS_COMPUTATION_THRESHOLD_MS}ms");
        }

        #endregion

        #region Memory and Scalability Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void DataScience_MemoryUsage_StressTest()
        {
            // Test memory efficiency with multiple [TestMethod][TestCategory("Core")]
            var initialMemory = GC.GetTotalMemory(true);
            
            var priceRecords = CreateTestPriceRecords(1000);
            
            // Run multiple data science [TestMethod][TestCategory("Core")] to stress test memory
            for (int i = 0; i < 10; i++)
            {
                var splits = CallPrivateCreateProperDataSplits(priceRecords);
                // Use reflection to safely access the training data
                var splitsType = splits.GetType();
                var training = (PriceRecord[])splitsType.GetField("Training").GetValue(splits);
                var normParams = CallPrivateComputeNormalizationParameters(training);
                // Don't run expensive [TestMethod][TestCategory("Core")] in memory test
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = (finalMemory - initialMemory) / (1024 * 1024); // MB

            ConsoleUtilities.WriteLine($"[PERF] Memory increase after 10 data science [TestMethod][TestCategory(\"Core\")]: {memoryIncrease:F2} MB");

            // Should not consume excessive memory (threshold: 50MB)
            Assert.IsTrue(memoryIncrease < 50, 
                $"Memory usage increased by {memoryIncrease:F2}MB, expected < 50MB");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void DataSplitting_Scalability_Performance()
        {
            // Test performance scaling with different data sizes
            var dataSizes = new[] { 252, 756, 1260, 2520 }; // 1, 3, 5, 10 years
            
            foreach (var dataSize in dataSizes)
            {
                var priceRecords = CreateTestPriceRecords(dataSize);
                
                var elapsedMs = PerformanceTimer.TimeAction(() =>
                {
                    var result = CallPrivateCreateProperDataSplits(priceRecords);
                    // Use reflection to verify the result
                    var resultType = result.GetType();
                    var training = (PriceRecord[])resultType.GetField("Training").GetValue(result);
                    Assert.IsNotNull(training);
                });
                
                ConsoleUtilities.WriteLine($"[PERF] Data Splitting {dataSize} records: {elapsedMs:F4}ms");
                _performanceResults[$"DataSplits_Scaling_{dataSize}"] = elapsedMs;
                
                // Performance should scale roughly linearly
                var expectedMaxMs = dataSize * 0.5; // Very generous linear scaling expectation
                Assert.IsTrue(elapsedMs < expectedMaxMs, 
                    $"Data splitting {dataSize} records took {elapsedMs:F4}ms, expected < {expectedMaxMs}ms");
            }
        }

        #endregion

        #region Helper Methods and Private Method Invocation

        private PriceRecord[] CreateTestPriceRecords(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var random = new Random(42); // Fixed seed for reproducible tests

            for (var i = 0; i < count; i++)
            {
                var date = baseDate.AddDays(i);
                var basePrice = 100.0 + i * 0.05 + Math.Sin(i * 0.02) * 10; // Trending with volatility
                var open = basePrice + (random.NextDouble() - 0.5) * 2;
                var close = basePrice + (random.NextDouble() - 0.5) * 2;
                var high = Math.Max(open, close) + random.NextDouble() * 1;
                var low = Math.Min(open, close) - random.NextDouble() * 1;
                var volume = 1000000 + random.Next(5000000); // Realistic volume

                records[i] = new PriceRecord(date, TimeFrame.D1, open, high, low, close, volume: volume, wap: close, count: 1);
            }
            return records;
        }

        private List<dynamic> CreateTestModelCandidates(int count)
        {
            var candidates = new List<dynamic>();
            var random = new Random(42);
            
            for (int i = 0; i < count; i++)
            {
                var candidate = CreateTestModelCandidate();
                // Vary the performance for realistic selection testing
                SetModelCandidatePerformance(ref candidate, 
                    random.NextDouble() * 20 - 5, // -5% to 15% training
                    random.NextDouble() * 15 - 2); // -2% to 13% validation
                candidates.Add(candidate);
            }
            
            return candidates;
        }

        private dynamic CreateTestModelCandidate()
        {
            // Create a test ModelCandidate using reflection since it's private
            var candidateType = typeof(Program).GetNestedType("ModelCandidate", 
                System.Reflection.BindingFlags.NonPublic);
            var candidate = Activator.CreateInstance(candidateType);
            
            // Set basic properties
            var modelField = candidateType.GetField("Model");
            modelField.SetValue(candidate, CreateTestIndividual());
            
            var descriptionField = candidateType.GetField("Description");
            descriptionField.SetValue(candidate, "Test Model");
            
            var hyperparametersField = candidateType.GetField("Hyperparameters");
            hyperparametersField.SetValue(candidate, new Dictionary<string, object> { ["Test"] = "Value" });
            
            return candidate;
        }

        private void SetModelCandidatePerformance(ref dynamic candidate, double trainingPerf, double validationPerf)
        {
            var candidateType = candidate.GetType();
            
            var trainingPerfField = candidateType.GetField("TrainingPerformance");
            trainingPerfField.SetValue(candidate, trainingPerf);
            
            var validationPerfField = candidateType.GetField("ValidationPerformance");
            validationPerfField.SetValue(candidate, validationPerf);
            
            var robustnessField = candidateType.GetField("RobustnessScore");
            robustnessField.SetValue(candidate, 0.5); // Neutral robustness
        }

        private GeneticIndividual CreateTestIndividual()
        {
            return new GeneticIndividual(new Random(42),
                Program.StartingBalance,
                Program.IndicatorTypeMin, Program.IndicatorTypeMax,
                Program.IndicatorPeriodMin, Program.IndicatorPeriodMax,
                Program.IndicatorModeMin, Program.IndicatorModeMax,
                Program.IndicatorTimeFrameMin, Program.IndicatorTimeFrameMax,
                Program.IndicatorPolarityMin, Program.IndicatorPolarityMax,
                Program.IndicatorThresholdMin, Program.IndicatorThresholdMax,
                Program.MaxIndicators, Program.TradePercentageForStocksMin, Program.TradePercentageForStocksMax,
                Program.TradePercentageForOptionsMin, Program.TradePercentageForOptionsMax,
                Program.OptionDaysOutMin, Program.OptionDaysOutMax,
                Program.OptionStrikeDistanceMin, Program.OptionStrikeDistanceMax,
                Program.FastMAPeriodMin, Program.FastMAPeriodMax,
                Program.SlowMAPeriodMin, Program.SlowMAPeriodMax,
                Program.AllowedTradeTypeMin, Program.AllowedTradeTypeMax,
                Program.AllowedOptionTypeMin, Program.AllowedOptionTypeMax,
                Program.AllowedSecurityTypeMin, Program.AllowedSecurityTypeMax,
                Program.NumberOfOptionContractsMin, Program.NumberOfOptionContractsMax);
        }

        // Private method invocation using reflection for performance testing
        private dynamic CallPrivateCreateProperDataSplits(PriceRecord[] priceRecords)
        {
            var method = typeof(Program).GetMethod("CreateProperDataSplits",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return method.Invoke(null, new object[] { priceRecords, false });
        }

        private dynamic CallPrivateComputeNormalizationParameters(PriceRecord[] trainingData)
        {
            var method = typeof(Program).GetMethod("ComputeNormalizationParameters",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return method.Invoke(null, new object[] { trainingData });
        }

        private List<dynamic> CallPrivateTrainModelCandidates(PriceRecord[] training, PriceRecord[] validation)
        {
            var method = typeof(Program).GetMethod("TrainModelCandidates",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = method.Invoke(null, new object[] { training, validation });
            
            // Convert to dynamic list for easier testing
            var dynamicList = new List<dynamic>();
            if (result is System.Collections.IList list)
            {
                foreach (var item in list)
                {
                    dynamicList.Add(item);
                }
            }
            return dynamicList;
        }

        private dynamic CallPrivateSelectBestModel(List<dynamic> candidates)
        {
            // Convert dynamic list back to original type for method call
            var candidateType = typeof(Program).GetNestedType("ModelCandidate", 
                System.Reflection.BindingFlags.NonPublic);
            var listType = typeof(List<>).MakeGenericType(candidateType);
            var originalList = Activator.CreateInstance(listType);
            var addMethod = listType.GetMethod("Add");
            
            foreach (var candidate in candidates)
            {
                addMethod.Invoke(originalList, new[] { candidate });
            }
            
            var method = typeof(Program).GetMethod("SelectBestModel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return method.Invoke(null, new object[] { originalList });
        }

        private dynamic CallPrivatePerformTimeSeriesCrossValidation(PriceRecord[] allData)
        {
            var method = typeof(Program).GetMethod("PerformTimeSeriesCrossValidation",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            return method.Invoke(null, new object[] { allData });
        }

        private void CallPrivateComputeRobustnessOnTrainVal(ref dynamic candidate, PriceRecord[] training, PriceRecord[] validation)
        {
            var method = typeof(Program).GetMethod("ComputeRobustnessOnTrainVal",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var parameters = new object[] { candidate, training, validation };
            method.Invoke(null, parameters);
            candidate = parameters[0]; // Update with ref result
        }

        #endregion
    }
}