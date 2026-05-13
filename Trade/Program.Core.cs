using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Trade.Prices2;

namespace Trade
{
    internal partial class Program
    {
        #region Core Application Logic

        /// <summary>
        ///     Executes the main trading simulation workflow with proper data science practices.
        ///     Implements strict train/validation/test separation to prevent overfitting.
        /// </summary>
        /// <returns>Exit code: 0 for success, non-zero for error</returns>
        private static int RunTradingSimulation()
        {
            try
            {
                WriteSection("Initializing Trading Simulation with Proper Data Science Practices");

                // Implied Volatility Solver Demo
                WriteSection("Implied Volatility Solver Demo");
                var ivSolver = new ImpliedVolatilitySolver();
                var ohlcCsvPath = Constants.SPX_D_FOR_OPTIONS;
                ivSolver.LoadOptionsAndSolveIVs(ohlcCsvPath);
                ivSolver.SimulateOptionPriceGridWithHistoricalVolatility(0.05, 0.12, true);

                // Example: get option price for a specific date, strike, and expiration
                var exampleDate = new DateTime(2024, 5, 1);
                var exampleClose = ivSolver.GetDailyIVSeries(4100, 2 / 365.0, 0.05, 0.015, 10.0, true)
                    .ContainsKey(exampleDate)
                    ? ivSolver.GetClosePrice(exampleDate)
                    : 4100;
                var exampleStrike = 4100;
                var exampleDaysToExpiration = 2;
                var riskFreeRate = 0.05;
                var dividendYield = 0.015;
                var isCall = true;
                var marketPrice = 10.0;

                // Solve for IV for this contract
                var iv = ivSolver.SolveIV(exampleClose, exampleStrike, exampleDaysToExpiration / 365.0, riskFreeRate,
                    dividendYield, marketPrice, isCall);
                WriteInfo(
                    $"Solved IV for {exampleDate:yyyy-MM-dd}, strike {exampleStrike}, {exampleDaysToExpiration} days: {iv:F4}");

                // Get theoretical option price using solved IV
                var optionPrice = ivSolver.BlackScholesPrice(
                    exampleClose, exampleStrike, exampleDaysToExpiration / 365.0, riskFreeRate, dividendYield, iv,
                    isCall);
                WriteInfo(
                    $"Option price for {exampleDate:yyyy-MM-dd}, strike {exampleStrike}, {exampleDaysToExpiration} days, IV={iv:F4}: {optionPrice:F2}");

                WriteSection("Loading and Preparing Data");

                // Quick validation of TradeResult calculations
                WriteInfo("Validating TradeResult calculations...");
                ValidateTradeResultCalculations();

                var sinIndicator = new IndicatorParams
                {
                    Type = 0,
                    Period = 0,
                    Mode = 0,
                    TimeFrame = TimeFrame.D1,
                    Param1 = 1, //multiplier
                    Param2 = 10, //cycles
                    Param3 = 100, //price floor
                    Param4 = 4, //length
                    Param5 = 0, //shift
                    Polarity = 1,
                    LongThreshold = 0.0,
                    ShortThreshold = 0.0,
                    DebugCase = true
                };

                if (false)
                {
                    // Basic triangular wave test case with enhanced minute-level data
                    WriteSection("Enhanced Triangular Wave Test Case with Minute Data");

                    // Create a triangular wave indicator that generates: 50→100→50 five times
                    var triangularIndicator = new IndicatorParams
                    {
                        Type = 0, // Sin indicator (we'll use it to generate triangular pattern)
                        Period = 0,
                        Mode = 0,
                        TimeFrame = TimeFrame.D1,
                        Param1 = 1, // multiplier
                        Param2 = 5, // five complete cycles
                        Param3 = 75, // midpoint (average of 50 and 100)
                        Param4 = 25, // amplitude (half the range: (100-50)/2)
                        Param5 = 0, // shift
                        Polarity = 1,
                        LongThreshold = 0.0,
                        ShortThreshold = 0.0,
                        DebugCase = true
                    };

                    GeneticIndividual.InitializePrices();

                    // Create PriceRecords using the enhanced historical data approach
                    var (priceRecordsTriangle, datesTriangle, closePricesTriangle) =
                        GeneratePriceRecordsWithDatesAndClosePrices(triangularIndicator, false, true);
                    WriteSuccess(
                        $"Generated {closePricesTriangle.Length} triangular wave price points with historical data");
                    WriteInfo(
                        $"Price pattern: Min=${closePricesTriangle.Min():F1}, Max=${closePricesTriangle.Max():F1}");

                    // Analyze indicator ranges for triangular pattern
                    GeneticIndividual.AnalyzeIndicatorRanges(priceRecordsTriangle);

                    // Initialize option solvers and run genetic algorithm
                    GeneticIndividual.InitializeOptionSolvers(Constants.SPX_D_FOR_OPTIONS);
                    var bestTriangularIndividual = RunGeneticAlgorithm(priceRecordsTriangle, false);

                    // Display triangular wave results
                    DisplayIndividualResults(bestTriangularIndividual, priceRecordsTriangle);

                    // Display enhanced triangular wave analysis
                    WriteInfo("Enhanced Triangular Wave Pattern Analysis:");
                    ConsoleUtilities.WriteLine(
                        $"  Daily Pattern: 5 cycles of 50→100→50 over {closePricesTriangle.Length} days");
                    ConsoleUtilities.WriteLine(
                        "  Minute Pattern: Each day has 390 minutes (6.5 hours) of intraday triangle data");
                    ConsoleUtilities.WriteLine("  Peak Time: ~12:30 PM (minute 195 of 390) reaches daily high");
                    ConsoleUtilities.WriteLine($"  Total Data Points: {closePricesTriangle.Length:N0} daily");

                    DisplayVisualizations(bestTriangularIndividual, null);
                }

                /**/
                
                if (false)
                {
                    // Generate sin wave...
                    WriteInfo("Loading market price data...");
                    var (priceRecordsSin, datesSin, closePricesSin) =
                        GeneratePriceRecordsWithDatesAndClosePrices(sinIndicator, true, false, 100);
                    WriteSuccess($"Loaded 100 price points for analysis");

                    // Create and configure Sin indicator individual
                    WriteInfo("Creating Sin indicator individual...");
                    var sinIndividual = CreateBaseIndividual(sinIndicator);
                    WriteSuccess("Sin indicator individual created");

                    // Analyze indicator ranges
                    WriteInfo("Analyzing indicator ranges...");
                    GeneticIndividual.AnalyzeIndicatorRanges(priceRecordsSin);
                    WriteSuccess("Indicator range analysis complete");

                    // Process the individual
                    WriteInfo("Processing individual...");
                    var fitness = sinIndividual.Process(priceRecordsSin);
                    WriteSuccess($"Individual processed with percent gain: {fitness.PercentGain:F4}%");

                    // Validate Sin indicator calculations
                    var buffer = GeneticIndividual.ExtractClosePrices(priceRecordsSin);
                    ValidateSinIndicator(buffer, sinIndividual);

                    sinIndividual.CalculateFitness();

                    // Display individual results
                    DisplayIndividualResults(sinIndividual, priceRecordsSin);

                    // Display visualizations
                    DisplayVisualizations(sinIndividual, null);
                }

                // Initialize option solvers...
                WriteInfo("Initializing option solvers...");
                GeneticIndividual.InitializeOptionSolvers(Constants.SPX_D_FOR_OPTIONS);
                WriteSuccess("Option solvers initialized");
                
                WriteInfo("Initializing price data...");
                GeneticIndividual.InitializePrices(Constants.SPX_JSON);

                // Load all available data
                var (allPriceRecords, dates, closePrices) =
                    GeneratePriceRecordsWithDatesAndClosePrices(sinIndicator, false, false);

                allPriceRecords = allPriceRecords.Where(_ => _.DateTime >= DateTime.Now.AddYears(-5)).ToArray();

                ProcessSlidingWindow(allPriceRecords, 0.5, 0.5, 1, 30, 0.0);
                
                var dataSplits = Process(ref allPriceRecords, out var normalizedTraining, out var normalizedValidation, out var normalizedTest, out var bestModel);

                // STEP 5: Time series cross-validation for additional validation
                WriteSection("Step 5: Time Series Cross-Validation for Robustness Assessment");
                var crossValidationResults = PerformTimeSeriesCrossValidation(allPriceRecords);
                DisplayCrossValidationResults(crossValidationResults);

                // STEP 6: Walk-forward analysis with proper isolation
                WriteSection("Step 6: Walk-forward Analysis with Proper Data Isolation");
                var walkforwardResults = RunProperWalkforwardAnalysis(allPriceRecords);
                DisplayWalkforwardResults(walkforwardResults);

                // STEP 7: Window size optimization (training/testing lengths) and horizon guidance
                bool optimizationAvailable = false;
                WindowOptimizer.WindowSizeOptimizationResults optResults = default(WindowOptimizer.WindowSizeOptimizationResults);
                if (Program.EnableWindowSizeOptimization)
                {
                    WriteSection("Step 7: Window Size Optimization and Horizon Guidance");
                    optResults = WindowOptimizer.OptimizeWindowSizes(allPriceRecords);
                    optimizationAvailable = true;
                    // The optimizer displays optimal training/testing windows and recommendations
                }

                // FINAL: Deployment recommendation - system, horizon, and current trade
                WriteSection("Deployment Recommendation");

                // If we have optimization results, walk forward top configurations, collect best individuals and merge trades
                if (optimizationAvailable && optResults.ConfigurationResults != null &&
                    optResults.ConfigurationResults.Count > 0)
                {
                    WriteInfo("Running ensemble walk-forward on top window configurations...");

                    // Select top-N configurations by OverallScore (default 3)
                    var topConfigs = optResults.ConfigurationResults
                        .OrderByDescending(r => r.OverallScore)
                        .Take(3)
                        .ToList();
                    
                    foreach (var records in new[]
                             {
                                 ("Training", normalizedTraining), ("Validation", normalizedValidation),
                                 ("Test", normalizedTest)
                             })
                    {
                        var ensembleTrades = new List<TradeResult>();
                        double ensembleStartingBalance = StartingBalance;

                        WriteInfo("PriceRecords: " + records.Item1);

                        foreach (var cfg in topConfigs)
                        {
                            var config = cfg.Configuration;
                            var wf = WindowOptimizer.RunWalkforwardForConfiguration(records.Item2, config);

                            // Collect each window's best individual's trades (already processed on that window's data)
                            foreach (var win in wf.Windows)
                            {
                                if (win.BestIndividual != null && win.BestIndividual.Trades != null)
                                {
                                    int globalOffset = win.TestStartIndex; // trades are indexed relative to testRecords

                                    foreach (var t in win.BestIndividual.Trades)
                                    {
                                        // Only append completed trades
                                        if (t.CloseIndex > t.OpenIndex)
                                        {
                                            int globalOpen = globalOffset + t.OpenIndex;
                                            int globalClose = globalOffset + t.CloseIndex;

                                            // Clamp to available bounds
                                            globalOpen = Math.Max(0, Math.Min(globalOpen, records.Item2.Length - 1));
                                            globalClose = Math.Max(0,
                                                Math.Min(globalClose, records.Item2.Length - 1));

                                            var tr = new TradeResult
                                            {
                                                OpenIndex = globalOpen,
                                                CloseIndex = globalClose,
                                                OpenPrice = t.OpenPrice,
                                                ClosePrice = t.ClosePrice,
                                                AllowedActionType = t.AllowedActionType,
                                                AllowedTradeType = t.AllowedTradeType,
                                                AllowedSecurityType = t.AllowedSecurityType,
                                                AllowedOptionType = t.AllowedOptionType,
                                                Position = t.Position,
                                                PositionInDollars = t.PositionInDollars,
                                                ResponsibleIndicatorIndex = t.ResponsibleIndicatorIndex,
                                                PriceRecordForOpen =
                                                    (globalOpen >= 0 && globalOpen < records.Item2.Length)
                                                        ? records.Item2[globalOpen]
                                                        : t.PriceRecordForOpen,
                                                PriceRecordForClose =
                                                    (globalClose >= 0 && globalClose < records.Item2.Length)
                                                        ? records.Item2[globalClose]
                                                        : t.PriceRecordForClose
                                            };

                                            // Balance will be recomputed after we sort chronologically
                                            tr.Balance = 0;

                                            ensembleTrades.Add(tr);
                                        }
                                    }
                                }
                            }
                        }

                        // Sort all trades by close index (chronological) and recompute running balance
                        ensembleTrades = ensembleTrades
                            .OrderBy(tr => tr.CloseIndex)
                            .ThenBy(tr => tr.OpenIndex)
                            .ToList();

                        double runningBalance = ensembleStartingBalance;
                        for (int i = 0; i < ensembleTrades.Count; i++)
                        {
                            runningBalance += ensembleTrades[i].ActualDollarGain;
                            ensembleTrades[i].Balance = runningBalance;
                        }

                        // Build a synthetic individual to display merged trades
                        var ensembleIndividual = new GeneticIndividual
                        {
                            StartingBalance = ensembleStartingBalance
                        };
                        ensembleIndividual.Trades = ensembleTrades;

                        // Show the merged trades using the standard display
                        if (ensembleTrades.Count > 0)
                        {
                            DisplayTradesList(ensembleIndividual, records.Item2);
                        }
                        else
                        {
                            WriteInfo("Ensemble produced no completed trades across selected configurations.");
                        }
                    }
                }
                
                // Determine estimated horizon (months) the system is expected to be valid
                double horizonMonths = 0.0;
                if (optimizationAvailable && optResults.OptimalConfiguration.Configuration.TrainingSize > 0)
                {
                    horizonMonths = optResults.OptimalConfiguration.Configuration.TestingMonths;
                }
                else
                {
                    // Fallback to test split length in months (~21 trading days per month)
                    horizonMonths = normalizedTest != null && normalizedTest.Length > 0
                        ? normalizedTest.Length / 21.0
                        : 3.0; // default 3 months
                }

                WriteInfo($"Estimated system horizon: ~{horizonMonths:F1} months before re-validation");

                // Compute current market signal using most recent data
                int lookback = optimizationAvailable && optResults.OptimalConfiguration.Configuration.TrainingSize > 0
                    ? optResults.OptimalConfiguration.Configuration.TrainingSize
                    : dataSplits.Training.Length;
                lookback = Math.Max(21, Math.Min(lookback, allPriceRecords.Length));
                var recentData = allPriceRecords.Skip(allPriceRecords.Length - lookback).ToArray();
                var currentSignal = GetCurrentMarketSignal(bestModel.Model, recentData);

                // Output trade to open (if any)
                if (string.Equals(currentSignal.RecommendedAction, "BUY", StringComparison.OrdinalIgnoreCase))
                {
                    WriteSuccess($"Trade to open: BUY (Confidence: {currentSignal.Confidence:F1}%)");
                }
                else if (string.Equals(currentSignal.RecommendedAction, "SELL", StringComparison.OrdinalIgnoreCase))
                {
                    WriteSuccess($"Trade to open: SELL (Confidence: {currentSignal.Confidence:F1}%)");
                }
                else
                {
                    WriteInfo($"No trade: HOLD (Confidence: {currentSignal.Confidence:F1}%)");
                }

                // Brief system description (indicator config)
                WriteInfo("System configuration summary (primary indicators):");
                for (int i = 0; i < Math.Min(3, bestModel.Model.Indicators.Count); i++)
                {
                    var ind = bestModel.Model.Indicators[i];
                    WriteInfo($"  Indicator {i + 1}: Type={ind.Type}, Period={ind.Period}, Mode={(int)ind.Mode}, TF={ind.TimeFrame}, Polarity={ind.Polarity}, LongTh={ind.LongThreshold:F4}, ShortTh={ind.ShortThreshold:F4}");
                }

                WriteSuccess("Trading simulation completed with proper data science practices");
                WriteInfo("All evaluations maintain strict temporal ordering and data isolation");
                
                return 0;
            }
            catch (Exception ex)
            {
                DisplayError($"Simulation failed: {ex.Message}");
                return 1;
            }
        }

        #endregion

        // Compute walk-forward robustness on training+validation only to avoid test leakage

        #region Data Generation Methods

        /// <summary>
        ///     Update GeneratePriceRecords to return both PriceRecord[] and corresponding DateTime[] for Program.cs
        /// </summary>
        /// <param name="indicatorParams">Indicator parameters for fallback sine wave generation</param>
        /// <param name="generateSinBuffer">Whether to generate synthetic sine wave data</param>
        /// <param name="generateTriangleBuffer">Whether to generate synthetic triangular wave data</param>
        /// <param name="bufferLengthOverride">Override buffer length for synthetic data</param>
        /// <returns>Tuple of PriceRecord array and corresponding DateTime array</returns>
        private static (PriceRecord[] priceRecords, DateTime[] dates, double[] closePrices)
            GeneratePriceRecordsWithDatesAndClosePrices(IndicatorParams indicatorParams, bool generateSinBuffer,
                bool generateTriangleBuffer, int bufferLengthOverride = 0)
        {
            var priceRecords = GeneratePriceRecords(indicatorParams, generateSinBuffer, generateTriangleBuffer,
                bufferLengthOverride);
            var dates = GeneticIndividual.ExtractDates(priceRecords);
            var closePrices = GeneticIndividual.ExtractClosePrices(priceRecords);
            return (priceRecords, dates, closePrices);
        }

        /// <summary>
        ///     Generates a price record array from real S&P 500 market data or fallback to synthetic data.
        /// </summary>
        /// <returns>Array of PriceRecord values from S&P 500 CSV data or synthetic sine wave</returns>
        private static PriceRecord[] GeneratePriceRecords(IndicatorParams indicatorParams, bool generateSinBuffer,
            bool generateTriangleBuffer, int bufferLengthOverride = 0)
        {
            try
            {
                if (!generateSinBuffer && !generateTriangleBuffer)
                {
                    WriteInfo("Loading real S&P 500 market data from CSV...");

                    // Try to load the CSV file
                    var csvPath = Constants.SPX_D;
                    if (!File.Exists(csvPath)) csvPath = @"Trade\" + Constants.SPX_D; // Try alternate path

                    if (File.Exists(csvPath))
                    {
                        WriteInfo($"Found CSV file at: {csvPath}");

                        var priceRecords = Prices.CreateDailyPriceRecordsFromCsv(csvPath);

                        if (priceRecords.Length > 0)
                        {
                            WriteSuccess($"Loaded {priceRecords.Length} real market price records from S&P 500 data");
                            WriteInfo(
                                $"Date range: {priceRecords[0].DateTime:yyyy-MM-dd} to {priceRecords[priceRecords.Length - 1].DateTime:yyyy-MM-dd}");
                            WriteInfo(
                                $"Price range: ${priceRecords.Min(p => p.Close):F2} - ${priceRecords.Max(p => p.Close):F2}");

                            return priceRecords;
                        }

                        WriteWarning("No valid price records found in CSV, falling back to sine wave data");
                    }
                    else
                    {
                        WriteWarning($"CSV file not found at '{csvPath}', falling back to sine wave data");
                    }
                }
            }
            catch (Exception ex)
            {
                WriteWarning($"Error loading CSV data: {ex.Message}, falling back to sine wave data");
            }

            if (generateSinBuffer)
            {
                // Fallback to original sine wave generation
                WriteInfo("Generating fallback sine wave price data...");

                var bufferLength = bufferLengthOverride > 0 ? bufferLengthOverride : 1000;
                var sinBuffer = new double[bufferLength];

                for (var i = 0; i < sinBuffer.Length; i++)
                {
                    var x = (double)i / sinBuffer.Length * indicatorParams.Param1 * Math.PI * indicatorParams.Param2;
                    sinBuffer[i] = indicatorParams.Param3 +
                                   indicatorParams.Param4 * Math.Sin(x + indicatorParams.Param5);
                }

                WriteInfo($"Generated {sinBuffer.Length} synthetic sine wave prices");

                // Convert to PriceRecord array
                return Prices.CreateDailyPriceRecordsFromClosePrices(sinBuffer, null, new DateTime(1900, 1, 1));
            }

            if (generateTriangleBuffer)
            {
                WriteInfo("Generating comprehensive triangular wave dataset and returning most recent data...");

                // Determine how many records to return
                var recordsToReturn =
                    bufferLengthOverride > 0 ? bufferLengthOverride : 100; // Default to 100 if not specified

                // Generate a large comprehensive dataset
                var comprehensiveDataSize = recordsToReturn * 2;
                var cyclesToGenerate = 5 * 2;

                var (triangleBuffer, minuteData) =
                    BufferUtilities.GenerateTriangularWaveBufferWithMinuteData(50, 100, cyclesToGenerate,
                        comprehensiveDataSize);

                WriteInfo($"Generated comprehensive triangular wave dataset: {triangleBuffer.Length} total points");

                // Convert to PriceRecord array using the full dataset
                var allPriceRecords =
                    Prices.CreateDailyPriceRecordsFromClosePrices(triangleBuffer, minuteData, new DateTime(1900, 1, 1));

                if (recordsToReturn >= allPriceRecords.Length)
                {
                    // If requesting more records than we have, return all of them
                    WriteInfo(
                        $"Requested {recordsToReturn} records, but only {allPriceRecords.Length} available - returning all records");
                    return allPriceRecords;
                }

                // Return the last N records (most recent data from the comprehensive dataset)
                var recentRecords = allPriceRecords.Skip(allPriceRecords.Length - recordsToReturn).ToArray();

                WriteSuccess(
                    $"Returning most recent {recordsToReturn} records from comprehensive dataset of {allPriceRecords.Length} total records");
                WriteInfo(
                    $"Date range: {recentRecords[0].DateTime:yyyy-MM-dd} to {recentRecords[recentRecords.Length - 1].DateTime:yyyy-MM-dd}");
                WriteInfo(
                    $"Price range: ${recentRecords.Min(p => p.Close):F1} - ${recentRecords.Max(p => p.Close):F1}");

                return recentRecords;
            }

            return default;
        }

        #endregion
    }
}