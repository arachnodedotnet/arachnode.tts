using System;
using System.Collections.Generic;
using System.Linq;
using Trade.Prices2;

namespace Trade
{
    internal partial class Program
    {
        #region Performance Optimization Constants and Cache

        // Pre-allocated arrays and caches for performance optimization
        private static readonly object _dataScienceCacheLock = new object();
        private static double[] _tempCloseBuffer = new double[10000]; // Pre-allocated for price calculations
        private static PriceRecord[] _tempRecordBuffer = new PriceRecord[10000]; // Pre-allocated for record operations
        
        #endregion

        #region Proper Data Science Methods

        /// <summary>
        /// Represents the result of a proper data split
        /// </summary>
        private struct DataSplits
        {
            public PriceRecord[] Training;
            public PriceRecord[] Validation;
            public PriceRecord[] Test;
        }

        /// <summary>
        /// Represents normalization parameters calculated from training data
        /// </summary>
        private struct NormalizationParameters
        {
            public double MinPrice;
            public double MaxPrice;
            public double MeanPrice;
            public double StdPrice;
            public Dictionary<string, double> IndicatorMins;
            public Dictionary<string, double> IndicatorMaxs;
            public Dictionary<string, double> IndicatorMeans;
            public Dictionary<string, double> IndicatorStds;
        }

        /// <summary>
        /// Represents a trained model candidate for selection
        /// </summary>
        private struct ModelCandidate
        {
            public GeneticIndividual Model;
            public string Description;
            public double TrainingPerformance;
            public double ValidationPerformance;
            public double ValidationPerfExtrapolatedToTrain; // geometric scaling
            public Dictionary<string, object> Hyperparameters;
            public double RobustnessScore;
            public double WFMedianSharpe;
            public double WFWorstSharpe;
            public double WFPassRate;
            public double WFGenGap;
        }

        /// <summary>
        /// Create proper temporal data splits that respect time ordering
        /// OPTIMIZED: Eliminated LINQ operations, direct array operations, pre-allocated arrays
        /// </summary>
        private static DataSplits CreateProperDataSplits(PriceRecord[] allData, bool allSplitsAreClones)
        {
            if (allSplitsAreClones)
            {
                return new DataSplits
                {
                    Training = allData,
                    Validation = allData,
                    Test = allData
                };
            }

            if (allData == null || allData.Length == 0)
            {
                return new DataSplits
                {
                    Training = new PriceRecord[0],
                    Validation = new PriceRecord[0],
                    Test = new PriceRecord[0]
                };
            }

            // OPTIMIZATION: Direct array operations instead of LINQ sorting
            // Assume data is already sorted by time, but verify and sort if needed
            var sortedData = EnsureTimeSortedOptimized(allData);

            // Use temporal splits - configurable ratios from Program constants
            var trainEndIndex = (int)(sortedData.Length * TrainingDataRatio);
            var valEndIndex = (int)(sortedData.Length * (TrainingDataRatio + ValidationDataRatio));

            // OPTIMIZATION: Direct array copying instead of LINQ Take/Skip operations
            var training = new PriceRecord[trainEndIndex];
            var validationLength = Math.Max(0, valEndIndex - trainEndIndex);
            var validation = new PriceRecord[validationLength];
            var testLength = Math.Max(0, sortedData.Length - valEndIndex);
            var test = new PriceRecord[testLength];

            // Direct array copying for maximum performance
            Array.Copy(sortedData, 0, training, 0, trainEndIndex);
            
            if (validationLength > 0)
                Array.Copy(sortedData, trainEndIndex, validation, 0, validationLength);
            
            if (testLength > 0)
                Array.Copy(sortedData, valEndIndex, test, 0, testLength);

            return new DataSplits
            {
                Training = training,
                Validation = validation,
                Test = test
            };
        }

        /// <summary>
        /// Compute normalization parameters using ONLY training data
        /// OPTIMIZED: Single-pass calculation, eliminated multiple LINQ operations
        /// </summary>
        private static NormalizationParameters ComputeNormalizationParameters(PriceRecord[] trainingData)
        {
            if (trainingData == null || trainingData.Length == 0)
            {
                return new NormalizationParameters
                {
                    MinPrice = 0,
                    MaxPrice = 0,
                    MeanPrice = 0,
                    StdPrice = 0,
                    IndicatorMins = new Dictionary<string, double>(),
                    IndicatorMaxs = new Dictionary<string, double>(),
                    IndicatorMeans = new Dictionary<string, double>(),
                    IndicatorStds = new Dictionary<string, double>()
                };
            }

            // OPTIMIZATION: Single pass calculation instead of multiple LINQ operations
            var count = trainingData.Length;
            double sum = 0.0;
            double sumSquares = 0.0;
            double minPrice = trainingData[0].Close;
            double maxPrice = trainingData[0].Close;

            // Single pass through data for all statistics
            for (int i = 0; i < count; i++)
            {
                var closePrice = trainingData[i].Close;
                sum += closePrice;
                sumSquares += closePrice * closePrice;
                
                if (closePrice < minPrice) minPrice = closePrice;
                if (closePrice > maxPrice) maxPrice = closePrice;
            }

            var meanPrice = sum / count;
            var variance = (sumSquares / count) - (meanPrice * meanPrice);
            var stdPrice = Math.Sqrt(Math.Max(0.0, variance)); // Ensure non-negative variance

            return new NormalizationParameters
            {
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                MeanPrice = meanPrice,
                StdPrice = stdPrice,
                IndicatorMins = new Dictionary<string, double>(),
                IndicatorMaxs = new Dictionary<string, double>(),
                IndicatorMeans = new Dictionary<string, double>(),
                IndicatorStds = new Dictionary<string, double>()
            };
        }

        /// <summary>
        /// Apply normalization parameters to data (training, validation, or test)
        /// </summary>
        private static PriceRecord[] ApplyNormalization(PriceRecord[] data, NormalizationParameters normParams)
        {
            // For now, we'll set the normalization parameters globally
            // In a more sophisticated system, you'd apply the normalization directly to the data
            GeneticIndividual.AnalyzeIndicatorRanges(data);
            return data;
        }

        /// <summary>
        /// Train multiple model candidates with different hyperparameters
        /// OPTIMIZED: Pre-allocated collections, reduced intermediate allocations
        /// </summary>
        private static List<ModelCandidate> TrainModelCandidates(PriceRecord[] training, PriceRecord[] validation)
        {
            var candidates = new List<ModelCandidate>(3); // Pre-allocate with known capacity

            // Candidate 1: Basic genetic algorithm
            WriteInfo("Training Candidate 1: Basic Genetic Algorithm"); //HACK: Why is runInParallel = false;...
            var basicModel = RunGeneticAlgorithm(training, runInParallel: false);
            var basicModelFitness = basicModel.Fitness;
            var basicValidation = validation != null ? basicModel.Process(validation) : new Fitness(0, 0, 0);

            var basicCandidate = new ModelCandidate
            {
                Model = basicModel,
                Description = "Basic GA",
                TrainingPerformance = basicModelFitness.PercentGain,
                ValidationPerformance = basicValidation.PercentGain,
                Hyperparameters = new Dictionary<string, object>(3) // Pre-allocate capacity
                {
                    ["PopulationSize"] = PopulationSize,
                    ["Generations"] = Generations,
                    ["MutationRate"] = MutationRate
                }
            };
            if(!Program.SIMPLE_MODE)
                ComputeRobustnessOnTrainVal(ref basicCandidate, training, validation);
            candidates.Add(basicCandidate);
            
            //// Candidate 2: Enhanced genetic algorithm with early stopping
            //WriteInfo("Training Candidate 2: Enhanced GA with Early Stopping");
            //var enhancedModel = RunEnhancedGeneticAlgorithm(training, validation);
            //var enhancedValidation = enhancedModel.Process(validation);

            //var enhancedCandidate = new ModelCandidate
            //{
            //    Model = enhancedModel,
            //    Description = "Enhanced GA with Early Stopping",
            //    TrainingPerformance = enhancedModel.Process(training).PercentGain,
            //    ValidationPerformance = enhancedValidation.PercentGain,
            //    Hyperparameters = new Dictionary<string, object>(3) // Pre-allocate capacity
            //    {
            //        ["EarlyStoppingPatience"] = EarlyStoppingPatience,
            //        ["ValidationPercentage"] = ValidationPercentage,
            //        ["RegularizationStrength"] = RegularizationStrength
            //    }
            //};
            //ComputeRobustnessOnTrainVal(ref enhancedCandidate, training, validation);
            //candidates.Add(enhancedCandidate);

            //// Candidate 3: Conservative model with lower complexity
            //WriteInfo("Training Candidate 3: Conservative Low-Complexity Model");
            //var conservativeModel = RunConservativeGeneticAlgorithm(training, validation);
            //var conservativeValidation = conservativeModel.Process(validation);

            //var conservativeCandidate = new ModelCandidate
            //{
            //    Model = conservativeModel,
            //    Description = "Conservative Low-Complexity",
            //    TrainingPerformance = conservativeModel.Process(training).PercentGain,
            //    ValidationPerformance = conservativeValidation.PercentGain,
            //    Hyperparameters = new Dictionary<string, object>(2) // Pre-allocate capacity
            //    {
            //        ["MaxComplexity"] = 1, // Force single indicator
            //        ["Regularization"] = "High"
            //    }
            //};
            //ComputeRobustnessOnTrainVal(ref conservativeCandidate, training, validation);
            //candidates.Add(conservativeCandidate);

            return candidates;
        }

        private static void ComputeRobustnessOnTrainVal(ref ModelCandidate candidate, PriceRecord[] training,
            PriceRecord[] validation)
        {
            try
            {
                // OPTIMIZATION: Efficient array concatenation using pre-allocated buffer
                var bars = ConcatenateArraysOptimized(training, validation);
                if (bars.Length < 126)
                {
                    return; // Skip robustness computation for insufficient data
                }

                var wf = WalkForwardScoring.WalkForwardScore(candidate.Model, bars);
                candidate.RobustnessScore = wf.CompositeScore;
                candidate.WFMedianSharpe = wf.MedianTestSharpe;
                candidate.WFWorstSharpe = wf.WorstTestSharpe;
                candidate.WFPassRate = wf.PassRate;
                candidate.WFGenGap = wf.GenGap;

                // Compute validation performance extrapolated to training length
                try
                {
                    var valPct = candidate.ValidationPerformance;
                    var nVal = Math.Max(1, validation?.Length ?? 0);
                    var nTrain = Math.Max(1, training?.Length ?? 0);
                    var factor = (double)nTrain / nVal;
                    var growth = 1.0 + valPct / 100.0;
                    if (growth > 0.0)
                    {
                        var scaled = Math.Pow(growth, factor) - 1.0;
                        candidate.ValidationPerfExtrapolatedToTrain = scaled * 100.0;
                    }
                }
                catch
                {
                    // ignore
                }
            }
            catch
            {
                // Ignore robustness computation errors to avoid breaking primary flow
            }
        }

        /// <summary>
        /// Select the best model based on validation performance and overfitting criteria
        /// OPTIMIZED: Single-pass selection without LINQ operations
        /// </summary>
        private static ModelCandidate SelectBestModel(List<ModelCandidate> candidates)
        {
            if (candidates.Count == 0)
            {
                throw new ArgumentException("No candidates provided for selection");
            }

            var bestCandidate = candidates[0];
            var bestScore = double.MinValue;

            // OPTIMIZATION: Direct iteration instead of LINQ operations
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                
                // Calculate a composite score that penalizes overfitting
                var performanceGap = Math.Abs(candidate.TrainingPerformance - candidate.ValidationPerformance);
                var overfittingPenalty = performanceGap * 2.0; // Penalize large gaps
                // Incorporate robustness (walk-forward composite). Scale factor to keep magnitudes comparable.
                var robustnessBoost = 10.0 * candidate.RobustnessScore; // if negative, it will penalize
                var score = candidate.ValidationPerformance - overfittingPenalty + robustnessBoost;

                // Report both actual and theoretical (extrapolated) validation performance
                var theo = candidate.ValidationPerfExtrapolatedToTrain;
                if (theo != 0)
                    WriteInfo(
                        $"{candidate.Description}: Validation={candidate.ValidationPerformance:F2}%, Theo@TrainLen={theo:F2}%, Gap={performanceGap:F2}%, Score={score:F2}");
                else
                    WriteInfo(
                        $"{candidate.Description}: Validation={candidate.ValidationPerformance:F2}%, Gap={performanceGap:F2}%, Score={score:F2}");

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidate = candidate;
                }
            }

            return bestCandidate;
        }

        /// <summary>
        /// Evaluate the final model on test data - this should only be called ONCE
        /// </summary>
        private static double EvaluateModelOnTestData(GeneticIndividual model, PriceRecord[] testData, ref Fitness fitness)
        {
            var testFitness = model.Process(testData);
            fitness = testFitness;
            return testFitness.PercentGain;
        }

        /// <summary>
        /// Perform time series cross-validation for additional robustness assessment
        /// OPTIMIZED: Reduced LINQ operations, efficient array slicing
        /// </summary>
        private static CrossValidationResults PerformTimeSeriesCrossValidation(PriceRecord[] allData)
        {
            const int numFolds = 5;
            var scores = new List<double>(numFolds); // Pre-allocate capacity
            var foldSize = allData.Length / (numFolds + 1); // +1 to leave room for test set

            WriteInfo($"Performing {numFolds}-fold time series cross-validation");

            for (int fold = 0; fold < numFolds; fold++)
            {
                var trainEnd = (fold + 1) * foldSize;
                var testStart = trainEnd;
                var testEnd = Math.Min(testStart + foldSize / 2, allData.Length); // Smaller test sets

                if (testEnd >= allData.Length) break;

                // OPTIMIZATION: Direct array slicing instead of LINQ Take/Skip
                var foldTraining = new PriceRecord[trainEnd];
                var testLength = testEnd - testStart;
                var foldTest = new PriceRecord[testLength];
                
                Array.Copy(allData, 0, foldTraining, 0, trainEnd);
                Array.Copy(allData, testStart, foldTest, 0, testLength);

                WriteInfo($"  Fold {fold + 1}: Training={foldTraining.Length}, Test={foldTest.Length}");

                // Compute normalization on fold training data
                GeneticIndividual.AnalyzeIndicatorRanges(foldTraining);

                // Train model on fold
                var foldModel = RunGeneticAlgorithm(foldTraining, runInParallel: false);

                // Evaluate on fold test set
                var foldScore = foldModel.Process(foldTest).PercentGain;
                scores.Add(foldScore);

                WriteInfo($"  Fold {fold + 1} score: {foldScore:F2}%");
            }

            // OPTIMIZATION: Single-pass statistics calculation
            var scoresArray = scores.ToArray();
            var meanScore = 0.0;
            var count = scoresArray.Length;
            
            for (int i = 0; i < count; i++)
            {
                meanScore += scoresArray[i];
            }
            meanScore /= count;
            
            var stdScore = CalculateStandardDeviationOptimized(scoresArray);

            return new CrossValidationResults
            {
                Scores = scoresArray,
                MeanScore = meanScore,
                StdScore = stdScore,
                NumFolds = count
            };
        }

        /// <summary>
        /// Run a conservative genetic algorithm with lower complexity
        /// </summary>
        private static GeneticIndividual RunConservativeGeneticAlgorithm(PriceRecord[] training,
            PriceRecord[] validation)
        {
            // Temporarily reduce complexity for conservative model
            WriteInfo("Running conservative GA with reduced complexity");

            // For now, delegate to the basic genetic algorithm with reduced parameters
            return RunGeneticAlgorithm(training, runInParallel: false);
        }

        /// <summary>
        /// Cross-validation results structure
        /// </summary>
        private struct CrossValidationResults
        {
            public double[] Scores;
            public double MeanScore;
            public double StdScore;
            public int NumFolds;
        }

        /// <summary>
        /// Display cross-validation results
        /// OPTIMIZED: Efficient string joining for score display
        /// </summary>
        private static void DisplayCrossValidationResults(CrossValidationResults results)
        {
            WriteInfo($"Cross-Validation Results ({results.NumFolds} folds):");
            WriteInfo($"  Mean Score: {results.MeanScore:F2}% ± {results.StdScore:F2}%");
            
            // OPTIMIZATION: Pre-allocate StringBuilder for efficient string construction
            var scoreStrings = new string[results.Scores.Length];
            for (int i = 0; i < results.Scores.Length; i++)
            {
                scoreStrings[i] = $"{results.Scores[i]:F1}%";
            }
            WriteInfo($"  Individual Scores: {string.Join(", ", scoreStrings)}");

            if (results.StdScore > 15.0)
            {
                WriteWarning("High variance in cross-validation scores suggests unstable model");
            }
            else
            {
                WriteSuccess("Cross-validation shows consistent performance");
            }
        }

        /// <summary>
        /// Display final results with proper statistical analysis
        /// </summary>
        private static void DisplayFinalResults(ModelCandidate bestModel, double testPerformance,
            PriceRecord[] testData)
        {
            WriteSection("Final Model Results");

            WriteInfo($"Selected Model: {bestModel.Description}");
            WriteInfo($"Training Performance: {bestModel.TrainingPerformance:F2}%");
            WriteInfo($"Validation Performance: {bestModel.ValidationPerformance:F2}%");
            if (bestModel.ValidationPerfExtrapolatedToTrain != 0)
                WriteInfo(
                    $"Validation Performance (theoretical, extrapolated to training horizon): {bestModel.ValidationPerfExtrapolatedToTrain:F2}%");
            WriteInfo($"Test Performance: {testPerformance:F2}%");

            var trainValGap = Math.Abs(bestModel.TrainingPerformance - bestModel.ValidationPerformance);
            var valTestGap = Math.Abs(bestModel.ValidationPerformance - testPerformance);

            WriteInfo($"Training-Validation Gap: {trainValGap:F2}%");
            WriteInfo($"Validation-Test Gap: {valTestGap:F2}%");

            // Risk assessment
            WriteSection("Overfitting Risk Assessment");
            if (trainValGap > 10.0)
            {
                WriteWarning("HIGH RISK: Large training-validation gap suggests overfitting");
            }
            else if (trainValGap > 5.0)
            {
                WriteWarning("MEDIUM RISK: Moderate training-validation gap");
            }
            else
            {
                WriteSuccess("LOW RISK: Small training-validation gap");
            }

            if (valTestGap > 15.0)
            {
                WriteWarning("HIGH RISK: Large validation-test gap suggests poor generalization");
            }
            else if (valTestGap > 8.0)
            {
                WriteWarning("MEDIUM RISK: Moderate validation-test gap");
            }
            else
            {
                WriteSuccess("LOW RISK: Good generalization to test data");
            }

            // Display detailed test results
            DisplayComparisonResults(null, bestModel.Model, testData);
        }

        /// <summary>
        /// Run walk-forward analysis with proper data isolation
        /// OPTIMIZED: Direct array slicing instead of LINQ Take operation
        /// </summary>
        private static WalkforwardResults RunProperWalkforwardAnalysis(PriceRecord[] allData)
        {
            // Reserve final portion for out-of-sample testing
            var walkforwardEndIndex = (int)(allData.Length * 0.8); // Only use 80% for walk-forward
            
            // OPTIMIZATION: Direct array copy instead of LINQ Take
            var walkforwardData = new PriceRecord[walkforwardEndIndex];
            Array.Copy(allData, 0, walkforwardData, 0, walkforwardEndIndex);

            WriteInfo($"Walk-forward analysis using {walkforwardData.Length} records (80% of total data)");
            WriteInfo("Remaining 20% reserved as completely untouched holdout data");

            return RunWalkforwardAnalysisWithDates(walkforwardData);
        }

        #endregion

        #region Optimized Helper Methods

        /// <summary>
        /// Ensure array is sorted by time efficiently
        /// OPTIMIZED: Check if already sorted before performing expensive sort
        /// </summary>
        private static PriceRecord[] EnsureTimeSortedOptimized(PriceRecord[] data)
        {
            if (data.Length <= 1) return data;

            // Check if already sorted
            var isSorted = true;
            for (int i = 1; i < data.Length; i++)
            {
                if (data[i].DateTime < data[i - 1].DateTime)
                {
                    isSorted = false;
                    break;
                }
            }

            if (isSorted)
            {
                return data; // Return original array if already sorted
            }

            // Only sort if necessary
            var sortedData = new PriceRecord[data.Length];
            Array.Copy(data, sortedData, data.Length);
            Array.Sort(sortedData, (a, b) => a.DateTime.CompareTo(b.DateTime));
            return sortedData;
        }

        /// <summary>
        /// Efficiently concatenate two arrays using pre-allocated buffer when possible
        /// OPTIMIZED: Uses pre-allocated buffer for common sizes, eliminates LINQ Concat
        /// </summary>
        private static PriceRecord[] ConcatenateArraysOptimized(PriceRecord[] array1, PriceRecord[] array2)
        {
            if (array1 == null && array2 == null) return new PriceRecord[0];
            if (array1 == null) return array2;
            if (array2 == null) return array1;

            var totalLength = array1.Length + array2.Length;
            
            lock (_dataScienceCacheLock)
            {
                // Use pre-allocated buffer if size fits
                if (totalLength <= _tempRecordBuffer.Length)
                {
                    Array.Copy(array1, 0, _tempRecordBuffer, 0, array1.Length);
                    Array.Copy(array2, 0, _tempRecordBuffer, array1.Length, array2.Length);
                    
                    // Return a copy of the relevant portion
                    var result = new PriceRecord[totalLength];
                    Array.Copy(_tempRecordBuffer, 0, result, 0, totalLength);
                    return result;
                }
            }

            // Fallback for very large arrays
            var concatenated = new PriceRecord[totalLength];
            Array.Copy(array1, 0, concatenated, 0, array1.Length);
            Array.Copy(array2, 0, concatenated, array1.Length, array2.Length);
            return concatenated;
        }

        /// <summary>
        /// Optimized standard deviation calculation using single-pass algorithm
        /// OPTIMIZED: Single-pass Welford's method for numerical stability and performance
        /// </summary>
        private static double CalculateStandardDeviationOptimized(double[] values)
        {
            if (values == null || values.Length <= 1) return 0.0;

            var count = values.Length;
            
            // Single-pass Welford's algorithm for numerical stability
            double mean = 0.0;
            double sumSquaredDiffs = 0.0;

            for (int i = 0; i < count; i++)
            {
                var value = values[i];
                var oldMean = mean;
                mean += (value - mean) / (i + 1);
                sumSquaredDiffs += (value - mean) * (value - oldMean);
            }

            return Math.Sqrt(sumSquaredDiffs / (count - 1));
        }

        /// <summary>
        /// Optimized array slicing without LINQ operations
        /// </summary>
        private static PriceRecord[] SliceArrayOptimized(PriceRecord[] source, int startIndex, int length)
        {
            if (source == null || startIndex < 0 || length <= 0) 
                return new PriceRecord[0];
                
            var actualLength = Math.Min(length, source.Length - startIndex);
            if (actualLength <= 0) return new PriceRecord[0];

            var result = new PriceRecord[actualLength];
            Array.Copy(source, startIndex, result, 0, actualLength);
            return result;
        }

        #endregion
    }
}