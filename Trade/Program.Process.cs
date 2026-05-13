using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trade.Prices2;

namespace Trade
{
    partial class Program
    {
		private static DataSplits Process(ref PriceRecord[] allPriceRecords, out PriceRecord[] normalizedTraining,
			out PriceRecord[] normalizedValidation, out PriceRecord[] normalizedTest, out ModelCandidate bestModel)
		{
			//HACK: !!!
			/*var priceRecords = GeneticIndividual.Prices.GetRange(allPriceRecords[0].DateTime,
                    allPriceRecords[allPriceRecords.Length - 1].DateTime.AddDays(1), TimeFrame.D1).ToArray(); 
                allPriceRecords = priceRecords;*/

			WriteInfo($"Loaded {allPriceRecords.Length} total price records");
			WriteInfo(
				$"Date range: {allPriceRecords[0].DateTime:yyyy-MM-dd} to {allPriceRecords[allPriceRecords.Length - 1].DateTime:yyyy-MM-dd}");

			// STEP 1: Proper data split with temporal ordering
			WriteSection("Step 1: Proper Data Splitting with Temporal Ordering");
			var dataSplits = CreateProperDataSplits(allPriceRecords, Program.ALL_SPLITS_ARE_CLONES);

			WriteInfo(
				$"Training data: {dataSplits.Training.Length} records ({dataSplits.Training[0].DateTime:yyyy-MM-dd} to {dataSplits.Training[dataSplits.Training.Length - 1].DateTime:yyyy-MM-dd})");
			WriteInfo(
				$"Validation data: {dataSplits.Validation.Length} records ({dataSplits.Validation[0].DateTime:yyyy-MM-dd} to {dataSplits.Validation[dataSplits.Validation.Length - 1].DateTime:yyyy-MM-dd})");
			WriteInfo(
				$"Test data: {dataSplits.Test.Length} records ({dataSplits.Test[0].DateTime:yyyy-MM-dd} to {dataSplits.Test[dataSplits.Test.Length - 1].DateTime:yyyy-MM-dd})");

			// STEP 2: Calculate normalization parameters ONLY on training data
			WriteSection("Step 2: Computing Normalization Parameters on Training Data Only");
			var normalizationParams = ComputeNormalizationParameters(dataSplits.Training);
			WriteSuccess("Normalization parameters computed from training data only");

			// Apply normalization to all splits using training-derived parameters
			normalizedTraining = ApplyNormalization(dataSplits.Training, normalizationParams);
			normalizedValidation = ApplyNormalization(dataSplits.Validation, normalizationParams);
			normalizedTest = ApplyNormalization(dataSplits.Test, normalizationParams);

			WriteInfo("Applied training-derived normalization to all data splits");

			// STEP 3: Model selection and hyperparameter tuning using ONLY training and validation
			WriteSection("Step 3: Model Selection and Hyperparameter Tuning");
			WriteInfo("Training models using ONLY training and validation data...");

			var modelCandidates = TrainModelCandidates(normalizedTraining, normalizedValidation);

			bestModel = SelectBestModel(modelCandidates);
			WriteInfo($"Selected best model: {bestModel.Description}");
			WriteInfo($"Training performance: {bestModel.TrainingPerformance:F2}%");
			WriteInfo($"Validation performance: {bestModel.ValidationPerformance:F2}%");
			if (bestModel.ValidationPerfExtrapolatedToTrain != 0)
				WriteInfo(
					$"Validation performance (theoretical, extrapolated to training horizon): {bestModel.ValidationPerfExtrapolatedToTrain:F2}%");
			WriteInfo($"Performance gap: {Math.Abs(bestModel.TrainingPerformance - bestModel.ValidationPerformance):F2}%");

			var bestBalance = modelCandidates.OrderByDescending(_ => _.Model.Fitness.DollarGain).Take(1);
			DisplayEquityVsSpyOverview(normalizedTraining, bestModel.Model);

			// STEP 4: SINGLE evaluation on test data (no model selection based on test performance)
			WriteSection("Step 4: Final Model Evaluation on Hold-out Test Data");
			WriteWarning("CRITICAL: This is the ONLY evaluation on test data - no model selection based on these results");

            Fitness testFitness = null;
            var finalTestPerformance = EvaluateModelOnTestData(bestModel.Model, normalizedTest, ref testFitness);
			WriteInfo($"Final test performance: {finalTestPerformance:F2}%");

			// Calculate realistic performance metrics
			var performanceGap = Math.Abs(bestModel.ValidationPerformance - finalTestPerformance);
			WriteInfo($"Validation vs Test gap: {performanceGap:F2}%");

			if (performanceGap > 10.0)
			{
				WriteWarning($"Large performance gap ({performanceGap:F2}%) suggests potential overfitting");
			}
			else
			{
				WriteSuccess($"Good generalization - performance gap is acceptable ({performanceGap:F2}%)");
			}

			// Display detailed test results
			DisplayFinalResults(bestModel, finalTestPerformance, normalizedTest);
			return dataSplits;
		}


        /// <summary>
        /// Print a horizontal histogram showing profits aggregated by 30-minute time increments
        /// </summary>
        private static void PrintProfitHistogram30Min(List<TradeResult> trades)
        {
            if (trades == null || trades.Count == 0)
            {
                WriteInfo("  No trades available for histogram");
                return;
            }

            WriteInfo("  === Profit Histogram by 30-Minute Time Slots ===");

            // Group trades by 30-minute time slots based on close time
            var profitByTimeSlot = new Dictionary<string, double>();
            var tradeCountByTimeSlot = new Dictionary<string, int>();

            foreach (var trade in trades)
            {
                if (trade.PriceRecordForClose?.DateTime == null) continue;

                var closeTime = trade.PriceRecordForClose.DateTime;

                // Round down to nearest 30-minute interval
                var minutes = closeTime.Minute;
                var roundedMinutes = (minutes / 30) * 30;
                var timeSlot = new DateTime(closeTime.Year, closeTime.Month, closeTime.Day,
                                          closeTime.Hour, roundedMinutes, 0);

                var timeKey = timeSlot.ToString("HH:mm");

                if (!profitByTimeSlot.ContainsKey(timeKey))
                {
                    profitByTimeSlot[timeKey] = 0;
                    tradeCountByTimeSlot[timeKey] = 0;
                }

                profitByTimeSlot[timeKey] += trade.ActualDollarGain;
                tradeCountByTimeSlot[timeKey]++;
            }

            if (profitByTimeSlot.Count == 0)
            {
                WriteInfo("  No valid trade times found for histogram");
                return;
            }

            // Sort by time and prepare for display
            var sortedSlots = profitByTimeSlot.OrderBy(kv => kv.Key).ToList();

            // Find the maximum absolute value for scaling
            var maxAbsValue = sortedSlots.Max(kv => Math.Abs(kv.Value));
            const int maxBarLength = 40; // Maximum length of histogram bars

            WriteInfo("  Time   Trades  Profit     Histogram");
            WriteInfo("  -----  ------  ---------  " + new string('-', maxBarLength + 10));

            foreach (var slot in sortedSlots)
            {
                var timeSlot = slot.Key;
                var profit = slot.Value;
                var tradeCount = tradeCountByTimeSlot[timeSlot];

                // Calculate bar length (scale to maxBarLength)
                var barLength = maxAbsValue > 0 ? (int)Math.Round(Math.Abs(profit) / maxAbsValue * maxBarLength) : 0;
                barLength = Math.Max(0, Math.Min(maxBarLength, barLength));

                // Create the histogram bar
                var barChar = profit >= 0 ? '█' : '▓';
                var bar = new string(barChar, barLength);

                // Format profit with appropriate sign
                var profitSign = profit >= 0 ? "+" : "";
                var profitStr = $"{profitSign}{profit:F0}";

                // Color coding for positive/negative
                var line = $"  {timeSlot}    {tradeCount,2}    {profitStr,8}  ";

                WriteInfo(line + bar);
            }

            // Summary statistics
            var totalProfit = profitByTimeSlot.Values.Sum();
            var totalTrades = tradeCountByTimeSlot.Values.Sum();
            var avgProfitPerSlot = profitByTimeSlot.Count > 0 ? totalProfit / profitByTimeSlot.Count : 0;
            var profitableSlots = profitByTimeSlot.Count(kv => kv.Value > 0);

            WriteInfo("  " + new string('-', maxBarLength + 35));
            WriteInfo($"  Summary: {profitByTimeSlot.Count} time slots, {totalTrades} total trades");
            WriteInfo($"  Total P&L: ${totalProfit:F0} | Avg per slot: ${avgProfitPerSlot:F0}");
            WriteInfo($"  Profitable slots: {profitableSlots}/{profitByTimeSlot.Count} ({(double)profitableSlots / profitByTimeSlot.Count * 100:F1}%)");
            WriteInfo($"  Best slot: {sortedSlots.OrderByDescending(kv => kv.Value).First().Key} (${sortedSlots.Max(kv => kv.Value):F0})");
            WriteInfo($"  Worst slot: {sortedSlots.OrderBy(kv => kv.Value).First().Key} (${sortedSlots.Min(kv => kv.Value):F0})");
        }

        /// <summary>
        /// Simple sliding window walk-forward with percentage-based sizes and explicit window size
        /// Updated to track rolling dollar totals starting with $100,000
        /// </summary>
        /// <param name="allPriceRecords">Complete price record data</param>
        /// <param name="trainingPercentage">Training window size as percentage (0.0 to 1.0)</param>
        /// <param name="testingPercentage">Testing window size as percentage (0.0 to 1.0)</param>
        /// <param name="stepSize">Step size to slide the window forward (in days)</param>
        /// <param name="windowSize">Explicit window size in days (0 = use all data)</param>
        /// <param name="validationPercentage">Optional validation size as percentage (0.0 for train/test only)</param>
        /// <returns>Collection of all trained models and their test performances</returns>
        private static (List<ModelCandidate> allModels, List<double> testPerformances, ModelCandidate averagedModel)
            ProcessSlidingWindow(PriceRecord[] allPriceRecords, double trainingPercentage, double testingPercentage,
                int stepSize, int windowSize = 0, double validationPercentage = 0.0)
        {
            void PrintTrades(ModelCandidate bestModel, int i)
            {
                // NEW: Display trade details for this window
                if (bestModel.Model.Trades != null && bestModel.Model.Trades.Count > 0)
                {
                    WriteInfo($"  === Trade Details for Window {i} ===");
                    WriteInfo($"  Total trades executed: {bestModel.Model.Trades.Count}");

                    foreach (var trade in bestModel.Model.Trades.Take(10)) // Show first 10 trades to avoid too much output
                    {
                        var securityType = trade.AllowedSecurityType == AllowedSecurityType.Option ? "OPT" : "STK";
                        var tradeType = trade.AllowedTradeType == AllowedTradeType.Buy ? "BUY" : "SHORT";
                        var optionInfo = "";

                        // Add option-specific information
                        if (trade.AllowedSecurityType == AllowedSecurityType.Option)
                        {
                            var optionType = trade.AllowedOptionType == AllowedOptionType.Calls ? "CALL" : "PUT";
                            optionInfo = $" {optionType}";

                            // Extract contract details from PriceRecordForOpen if available
                            if (trade.PriceRecordForOpen?.Option != null)
                            {
                                var option = trade.PriceRecordForOpen.Option;
                                var strike = option.StrikePrice?.ToString("F0") ?? "N/A";
                                var expiry = option.ExpirationDate?.ToString("yyyy-MM-dd") ?? "N/A";
                                optionInfo += $" Strike=${strike} Exp={expiry}";
                            }
                        }

                        // Display trade information
                        var openDate = trade.PriceRecordForOpen?.DateTime.ToString("yyyy-MM-dd") ?? "N/A";
                        var closeDate = trade.PriceRecordForClose?.DateTime.ToString("yyyy-MM-dd") ?? "N/A";

                        // Fix the string formatting issues
                        var pnlSign = trade.ActualDollarGain >= 0 ? "+" : "";
                        var percentSign = trade.PercentGain >= 0 ? "+" : "";

                        WriteInfo(
                            $"    {trade.PriceRecordForOpen?.DateTime} {securityType} {tradeType}{optionInfo}: Open=${trade.OpenPrice:F2} ({openDate}) → Close=${trade.ClosePrice:F2} ({trade.PriceRecordForClose?.DateTime}) | " +
                            $"Pos={Math.Abs(trade.Position):F2} | P&L=${pnlSign}{trade.ActualDollarGain:F0} ({percentSign}{trade.PercentGain:F1}%)");
                    }

                    if (bestModel.Model.Trades.Count > 10)
                    {
                        WriteInfo($"    ... and {bestModel.Model.Trades.Count - 10} more trades");
                    }

                    // Summary statistics
                    var totalPnL = bestModel.Model.Trades.Sum(t => t.ActualDollarGain);
                    var winners = bestModel.Model.Trades.Count(t => t.ActualDollarGain > 0);
                    var losers = bestModel.Model.Trades.Count(t => t.ActualDollarGain < 0);
                    var winRate = bestModel.Model.Trades.Count > 0 ? (double)winners / bestModel.Model.Trades.Count * 100 : 0;

                    var totalPnLSign = totalPnL >= 0 ? "+" : "";
                    WriteInfo(
                        $"  Trade Summary: Total P&L=${totalPnLSign}{totalPnL:F0} | Win Rate={winRate:F1}% ({winners}W/{losers}L)");

                    // NEW: Generate horizontal histogram of profits by 30-minute time slots
                    PrintProfitHistogram30Min(bestModel.Model.Trades);
                    
                    WriteInfo($"  =======================================");
                }
                else
                {
                    WriteInfo($"  Window {i}: No trades executed");
                }
            }

            // Validate percentages
            if (trainingPercentage <= 0 || trainingPercentage >= 1.0)
                throw new ArgumentException("Training percentage must be between 0.0 and 1.0");
            if (testingPercentage <= 0 || testingPercentage >= 1.0)
                throw new ArgumentException("Testing percentage must be between 0.0 and 1.0");
            if (validationPercentage < 0 || validationPercentage >= 1.0)
                throw new ArgumentException("Validation percentage must be between 0.0 and 1.0");
            if (trainingPercentage + testingPercentage + validationPercentage > 1.0)
                throw new ArgumentException("Sum of percentages cannot exceed 1.0");

            // Determine effective data size and get data slice starting from the beginning
            var effectiveDataSize = windowSize > 0 ? Math.Min(windowSize, allPriceRecords.Length) : allPriceRecords.Length;
            var dataToUse = allPriceRecords;

            // Calculate actual sizes in days
            var trainingDays = (int)(effectiveDataSize * trainingPercentage);
            var testingDays = (int)(effectiveDataSize * testingPercentage);
            var validationDays = validationPercentage > 0 ? (int)(effectiveDataSize * validationPercentage) : 0;

            WriteSection($"Sliding Window Processing with Rolling Dollar Totals");
            WriteInfo($"Effective data size: {effectiveDataSize} days (from {dataToUse.Length} available)");
            WriteInfo($"Data slice: {(windowSize > 0 ? $"First {windowSize} records" : "All records")}");
            WriteInfo($"Date range: {dataToUse[0].DateTime:yyyy-MM-dd} to {dataToUse[dataToUse.Length - 1].DateTime:yyyy-MM-dd}");
            WriteInfo($"Training: {trainingPercentage:P1} = {trainingDays} days");
            WriteInfo($"Testing: {testingPercentage:P1} = {testingDays} days");
            if (validationDays > 0)
                WriteInfo($"Validation: {validationPercentage:P1} = {validationDays} days");
            WriteInfo($"Step size: {stepSize} days");

            var allModels = new List<ModelCandidate>();
            var testPerformances = new List<double>();

            int windowIndex = 0;
            int startIndex = 0;

            // Calculate total window size
            int totalWindowSize = trainingDays + testingDays + validationDays;

            WriteInfo($"Total window size: {totalWindowSize} days");
            var expectedWindows = Math.Max(0, (dataToUse.Length - totalWindowSize) / stepSize + 1);
            WriteInfo($"Expected number of windows: {expectedWindows}");

            // Running totals for progress tracking (percentages)
            double runningTotalTestPerformance = 0.0;
            double runningTotalTrainingPerformance = 0.0;
            double runningTotalValidationPerformance = 0.0;
            int successfulWindows = 0;

            // Rolling sum tracking for cumulative performance (percentages)
            double rollingTestSum = 0.0;
            double rollingTrainingSum = 0.0;
            double rollingValidationSum = 0.0;

            // NEW: Rolling dollar balance tracking starting with $100,000
            const double STARTING_BALANCE = 100000.0;
            double rollingTestBalance = STARTING_BALANCE;
            double rollingTrainingBalance = STARTING_BALANCE;
            double rollingValidationBalance = STARTING_BALANCE;

            WriteInfo($"Starting rolling balance tracking with ${STARTING_BALANCE:N0}");

            // Slide the window forward
            while (startIndex + totalWindowSize <= dataToUse.Length)
            {
                windowIndex++;
                var progressPercent = (double)windowIndex / expectedWindows * 100.0;
                WriteInfo($"Processing window {windowIndex}/{expectedWindows} ({progressPercent:F1}%) starting at index {startIndex}");

                try
                {
                    // Extract training data
                    var trainingEndIndex = startIndex + trainingDays - 1;
                    var trainingData = GeneticIndividual.CreateSubset(dataToUse, startIndex, trainingEndIndex);

                    // Extract validation data (if specified)
                    PriceRecord[] validationData = null;
                    if (validationDays > 0)
                    {
                        var validationStartIndex = trainingEndIndex + 1;
                        var validationEndIndex = validationStartIndex + validationDays - 1;
                        validationData =
                            GeneticIndividual.CreateSubset(dataToUse, validationStartIndex, validationEndIndex);
                    }

                    // Extract test data
                    var testStartIndex = (validationDays > 0)
                        ? (trainingEndIndex + validationDays + 1)
                        : (trainingEndIndex + 1);
                    var testEndIndex = testStartIndex + testingDays - 1;
                    var testData = GeneticIndividual.CreateSubset(dataToUse, testStartIndex, testEndIndex);

                    WriteInfo(
                        $"  Window {windowIndex}: Train[{startIndex}-{trainingEndIndex}], Test[{testStartIndex}-{testEndIndex}]");
                    WriteInfo(
                        $"  Data sizes: Train={trainingData.Length}, Val={validationData?.Length ?? 0}, Test={testData.Length}");
                    WriteInfo(
                        $"  Date ranges: Train={trainingData[0].DateTime:yyyy-MM-dd} to {trainingData[trainingData.Length - 1].DateTime:yyyy-MM-dd}");
                    WriteInfo(
                        $"               Test={testData[0].DateTime:yyyy-MM-dd} to {testData[testData.Length - 1].DateTime:yyyy-MM-dd}");

                    // Analyze indicator ranges for this window
                    GeneticIndividual.AnalyzeIndicatorRanges(trainingData);

                    // Train model
                    var modelCandidates = TrainModelCandidates(trainingData, validationData);
                    var bestModel = SelectBestModel(modelCandidates);

                    // Add window identifier to model description
                    bestModel.Description = $"{bestModel.Description}_W{windowIndex}";

                    if (true)
                    {
                        PrintTrades(bestModel, windowIndex);
                    }
                    
                    // Test the model and get both percentage and fitness object
                    Fitness testFitness = null;
                    var testPerformance = EvaluateModelOnTestData(bestModel.Model, testData, ref testFitness);

                    if (true)
                    {
                        PrintTrades(bestModel, windowIndex);
                    }
                    
                    // Store results
                    allModels.Add(bestModel);
                    testPerformances.Add(testPerformance);

                    // Update running totals (percentages)
                    successfulWindows++;
                    runningTotalTestPerformance += testPerformance;
                    runningTotalTrainingPerformance += bestModel.TrainingPerformance;
                    runningTotalValidationPerformance += bestModel.ValidationPerformance;

                    // Update rolling sums (cumulative percentage performance)
                    rollingTestSum += testPerformance;
                    rollingTrainingSum += bestModel.TrainingPerformance;
                    rollingValidationSum += bestModel.ValidationPerformance;

                    // NEW: Update rolling dollar balances
                    // Apply this window's percentage gains to the rolling balances
                    rollingTestBalance +=  (STARTING_BALANCE * (testPerformance / 100.0));
                    rollingTrainingBalance += (STARTING_BALANCE * (bestModel.TrainingPerformance / 100.0));
                    rollingValidationBalance += (STARTING_BALANCE * (bestModel.ValidationPerformance / 100.0));

                    // Calculate running averages
                    var avgTestPerf = runningTotalTestPerformance / successfulWindows;
                    var avgTrainPerf = runningTotalTrainingPerformance / successfulWindows;
                    var avgValPerf = runningTotalValidationPerformance / successfulWindows;

                    // Calculate total gains from starting balance
                    var testTotalGain = ((rollingTestBalance - STARTING_BALANCE) / STARTING_BALANCE) * 100.0;
                    var trainingTotalGain = ((rollingTrainingBalance - STARTING_BALANCE) / STARTING_BALANCE) * 100.0;
                    var validationTotalGain = ((rollingValidationBalance - STARTING_BALANCE) / STARTING_BALANCE) * 100.0;

                    WriteInfo($"  Window {windowIndex} completed: Train={bestModel.TrainingPerformance:F2}%, Val={bestModel.ValidationPerformance:F2}%, Test={testPerformance:F2}%");
                    WriteInfo($"  Running averages ({successfulWindows} windows): Train={avgTrainPerf:F2}%, Val={avgValPerf:F2}%, Test={avgTestPerf:F2}%");

                    WriteInfo($"");
                    WriteInfo($"=== WORKING ROLLING DOLLAR TOTALS (from ${STARTING_BALANCE:N0} starting) ===");

                    // Fix: Use manual sign handling like the trade display
                    var trainingSign = trainingTotalGain >= 0 ? "+" : "";
                    var validationSign = validationTotalGain >= 0 ? "+" : "";
                    var testSign = testTotalGain >= 0 ? "+" : "";

                    WriteInfo($"Training:   ${rollingTrainingBalance:N0} (Total gain: {trainingSign}{trainingTotalGain:F2}%)");
                    WriteInfo($"Validation: ${rollingValidationBalance:N0} (Total gain: {validationSign}{validationTotalGain:F2}%)");
                    WriteInfo($"Test:       ${rollingTestBalance:N0} (Total gain: {testSign}{testTotalGain:F2}%)");
                    WriteInfo($"========================================================");

                    WriteInfo($"  Progress: {windowIndex}/{expectedWindows} windows completed ({progressPercent:F1}%)");
                }
                catch (Exception ex)
                {
                    WriteWarning($"Window {windowIndex} failed: {ex.Message}");
                }

                // Slide the window forward
                startIndex += stepSize;
            }

            WriteInfo($"Completed {allModels.Count} windows");

            if (allModels.Count == 0)
            {
                WriteWarning("No valid models generated");
                return (new List<ModelCandidate>(), new List<double>(), default(ModelCandidate));
            }

            // Create averaged model
            var averagedModel = CreateAveragedModel(allModels, testPerformances);

            // Display summary statistics including final dollar balances
            WriteSection("Sliding Window Summary with Rolling Dollar Totals");
            WriteInfo($"Total models trained: {allModels.Count}");
            WriteInfo($"Average test performance: {testPerformances.Average():F2}%");
            WriteInfo($"Test performance std dev: {CalculateStandardDeviation(testPerformances):F2}%");
            WriteInfo($"Best test performance: {testPerformances.Max():F2}%");
            WriteInfo($"Worst test performance: {testPerformances.Min():F2}%");
            WriteInfo($"Models with positive test performance: {testPerformances.Count(p => p > 0)} / {testPerformances.Count}");
            WriteInfo($"Consistency ratio: {(testPerformances.Count(p => p > 0) / (double)testPerformances.Count):P1}");

            // NEW: Final rolling balance summary with nice, easily readable totals
            var finalTestGain = ((rollingTestBalance - STARTING_BALANCE) / STARTING_BALANCE) * 100.0;
            var finalTrainingGain = ((rollingTrainingBalance - STARTING_BALANCE) / STARTING_BALANCE) * 100.0;
            var finalValidationGain = ((rollingValidationBalance - STARTING_BALANCE) / STARTING_BALANCE) * 100.0;

            WriteInfo($"");
            WriteInfo($"=== FINAL ROLLING DOLLAR TOTALS (from ${STARTING_BALANCE:N0} starting) ===");

            // Fix: Use manual sign handling
            var finalTrainingSign = finalTrainingGain >= 0 ? "+" : "";
            var finalValidationSign = finalValidationGain >= 0 ? "+" : "";
            var finalTestSign = finalTestGain >= 0 ? "+" : "";

            WriteInfo($"Training:   ${rollingTrainingBalance:N0} (Total gain: {finalTrainingSign}{finalTrainingGain:F2}%)");
            WriteInfo($"Validation: ${rollingValidationBalance:N0} (Total gain: {finalValidationSign}{finalValidationGain:F2}%)");
            WriteInfo($"Test:       ${rollingTestBalance:N0} (Total gain: {finalTestSign}{finalTestGain:F2}%)");
            WriteInfo($"========================================================");

            return (allModels, testPerformances, averagedModel);
        }

        /// <summary>
        /// Overload for backward compatibility with fixed day sizes
        /// </summary>
        private static (List<ModelCandidate> allModels, List<double> testPerformances, ModelCandidate averagedModel) 
            ProcessSlidingWindow(PriceRecord[] allPriceRecords, int trainingDays, int testingDays, 
                int stepSize, int validationDays = 0)
        {
            // Convert to percentages based on total data
            var totalData = allPriceRecords.Length;
            var trainingPercentage = (double)trainingDays / totalData;
            var testingPercentage = (double)testingDays / totalData;
            var validationPercentage = validationDays > 0 ? (double)validationDays / totalData : 0.0;
            
            return ProcessSlidingWindow(allPriceRecords, trainingPercentage, testingPercentage, 
                stepSize, 0, validationPercentage);
        }

        /// <summary>
        /// Create an averaged model from all individual models
        /// </summary>
        private static ModelCandidate CreateAveragedModel(List<ModelCandidate> allModels, List<double> testPerformances)
        {
            if (allModels.Count == 0)
                return default(ModelCandidate);
            
            // Use the best performing model as the base
            var bestIndex = testPerformances.IndexOf(testPerformances.Max());
            var baseModel = allModels[bestIndex];
            
            // Create averaged model
            var averagedModel = new ModelCandidate
            {
                Model = CloneModel(baseModel.Model),
                Description = $"Averaged_from_{allModels.Count}_models",
                TrainingPerformance = allModels.Average(m => m.TrainingPerformance),
                ValidationPerformance = allModels.Average(m => m.ValidationPerformance),
                ValidationPerfExtrapolatedToTrain = allModels.Average(m => m.ValidationPerfExtrapolatedToTrain),
                Hyperparameters = new Dictionary<string, object>
                {
                    ["EnsembleSize"] = allModels.Count,
                    ["BaseModelIndex"] = bestIndex,
                    ["AverageTestPerformance"] = testPerformances.Average(),
                    ["TestPerformanceStdDev"] = CalculateStandardDeviation(testPerformances),
                    ["BestTestPerformance"] = testPerformances.Max(),
                    ["WorstTestPerformance"] = testPerformances.Min(),
                    ["ConsistencyRatio"] = testPerformances.Count(p => p > 0) / (double)testPerformances.Count
                }
            };
            
            // Average the thresholds across all models
            AverageModelThresholds(averagedModel.Model, allModels);
            
            WriteInfo($"Created averaged model from {allModels.Count} individual models");
            WriteInfo($"Base model (best performer): Window index {bestIndex + 1}");
            
            return averagedModel;
        }

        /// <summary>
        /// Average the indicator thresholds across all models
        /// </summary>
        private static void AverageModelThresholds(GeneticIndividual targetModel, List<ModelCandidate> allModels)
        {
            if (targetModel.Indicators == null || targetModel.Indicators.Count == 0)
                return;
            
            for (int i = 0; i < targetModel.Indicators.Count; i++)
            {
                var longThresholds = new List<double>();
                var shortThresholds = new List<double>();
                
                foreach (var model in allModels)
                {
                    if (model.Model.Indicators != null && i < model.Model.Indicators.Count)
                    {
                        longThresholds.Add(model.Model.Indicators[i].LongThreshold);
                        shortThresholds.Add(model.Model.Indicators[i].ShortThreshold);
                    }
                }
                
                if (longThresholds.Count > 0)
                {
                    targetModel.Indicators[i].LongThreshold = longThresholds.Average();
                    targetModel.Indicators[i].ShortThreshold = shortThresholds.Average();
                    
                    WriteInfo($"  Indicator {i}: Long threshold averaged from {longThresholds.Min():F4} to {longThresholds.Max():F4} = {targetModel.Indicators[i].LongThreshold:F4}");
                    WriteInfo($"  Indicator {i}: Short threshold averaged from {shortThresholds.Min():F4} to {shortThresholds.Max():F4} = {targetModel.Indicators[i].ShortThreshold:F4}");
                }
            }
            
            WriteInfo($"Averaged thresholds for {targetModel.Indicators.Count} indicators");
        }

        /// <summary>
        /// Simple model cloning for averaging
        /// </summary>
        private static GeneticIndividual CloneModel(GeneticIndividual original)
        {
            var clone = new GeneticIndividual
            {
                StartingBalance = original.StartingBalance,
                TradePercentageForStocks = original.TradePercentageForStocks,
                TradePercentageForOptions = original.TradePercentageForOptions,
                AllowedTradeTypes = original.AllowedTradeTypes,
                AllowedSecurityTypes = original.AllowedSecurityTypes,
                AllowedOptionTypes = original.AllowedOptionTypes,
                CombinationMethod = original.CombinationMethod,
                EnsembleVotingThreshold = original.EnsembleVotingThreshold,
                AllowMultipleTrades = original.AllowMultipleTrades
            };

            // Copy indicators
            clone.Indicators = new List<IndicatorParams>();
            if (original.Indicators != null)
            {
                foreach (var indicator in original.Indicators)
                {
                    clone.Indicators.Add(new IndicatorParams
                    {
                        Type = indicator.Type,
                        Period = indicator.Period,
                        Mode = indicator.Mode,
                        TimeFrame = indicator.TimeFrame,
                        Polarity = indicator.Polarity,
                        LongThreshold = indicator.LongThreshold,
                        ShortThreshold = indicator.ShortThreshold,
                        OHLC = indicator.OHLC,
                        BufferSource = indicator.BufferSource,
                        Param1 = indicator.Param1,
                        Param2 = indicator.Param2,
                        Param3 = indicator.Param3,
                        Param4 = indicator.Param4,
                        Param5 = indicator.Param5,
                        FastMAPeriod = indicator.FastMAPeriod,
                        SlowMAPeriod = indicator.SlowMAPeriod,
                        DebugCase = indicator.DebugCase
                    });
                }
            }

            return clone;
        }

        /// <summary>
        /// Calculate standard deviation of a list of values
        /// </summary>
        private static double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count <= 1) return 0.0;

            var mean = values.Average();
            var sumSquaredDiffs = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquaredDiffs / (values.Count - 1));
        }
    }
}
