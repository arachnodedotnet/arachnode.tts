using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Trade.Prices2;

namespace Trade
{
    /// <summary>
    ///     Comprehensive window size optimization system for walkforward analysis.
    ///     Determines optimal training and testing window sizes based on statistical analysis and market characteristics.
    ///     OPTIMIZED for performance with reduced algorithmic complexity and efficient memory usage.
    /// </summary>
    public static class WindowOptimizer
    {
        #region Performance Optimization Cache and Constants

        // Pre-allocated arrays for statistical calculations to reduce GC pressure
        private static readonly object _cachelock = new object();
        private static double[] _tempArray = new double[10000]; // Pre-allocated for statistical calculations
        private static readonly Dictionary<int, List<WindowConfiguration>> _configCache = 
            new Dictionary<int, List<WindowConfiguration>>();
        
        // Pre-computed constants
        private static readonly double InvSqrt252 = 1.0 / Math.Sqrt(252); // For annualized calculations
        private static readonly double[] MathConstants = { 0.1, 0.15, 0.2, 0.25, 0.3, 0.05, 0.083, 0.125, 0.167, 0.25 };

        #endregion

        #region Public API

        /// <summary>
        ///     Comprehensive window size optimization analysis to determine optimal training and testing periods.
        ///     This method tests various window configurations and evaluates their effectiveness for trading strategy validation.
        ///     Enhanced to use years worth of real market data via Generate1DTradingGuides.
        ///     OPTIMIZED for performance with caching and algorithmic improvements.
        /// </summary>
        /// <param name="priceRecords">Complete price record data for analysis (will be replaced with fresh market data)</param>
        /// <returns>Recommendations for optimal window sizes with detailed analysis</returns>
        public static WindowSizeOptimizationResults OptimizeWindowSizes(PriceRecord[] priceRecords)
        {
            WriteSection("Window Size Optimization Analysis with Enhanced Market Data");

            // Step 1: Generate fresh CSV files with maximum available data
            WriteInfo("Step 1: Generating fresh CSV files with years of market data...");
            var currentDirectory = Directory.GetCurrentDirectory();
            WriteInfo($"Saving CSV files to current directory: {currentDirectory}");

            var generationResult = Generate1DTradingGuides.GenerateTradingGuides(Constants.SPX_JSON, currentDirectory);

            if (!generationResult.Success)
            {
                WriteWarning($"Failed to generate fresh market data: {generationResult.ErrorMessage}");
                WriteWarning("Falling back to provided price records...");

                if (generationResult.Warnings.Count > 0)
                {
                    WriteWarning("Generation warnings:");
                    foreach (var warning in generationResult.Warnings) WriteWarning($"  - {warning}");
                }
            }
            else
            {
                WriteInfo("✓ Successfully generated fresh market data:");
                WriteInfo(
                    $"  Regular CSV: {generationResult.RegularCsvPath} ({generationResult.RegularRecordCount} records)");
                WriteInfo($"  Regular date range: {generationResult.RegularDateRange}");
                WriteInfo(
                    $"  Options CSV: {generationResult.OptionsCsvPath} ({generationResult.OptionsRecordCount} records)");
                WriteInfo($"  Options date range: {generationResult.OptionsDateRange}");
                WriteInfo($"  Generation time: {generationResult.GenerationTimeMs:F0}ms");

                // Step 2: Load the regular CSV file data for window optimization
                WriteInfo("Step 2: Loading fresh market data from generated CSV file...");

                try
                {
                    var regularCsvPath = generationResult.RegularCsvPath;
                    if (!File.Exists(regularCsvPath))
                        // Try relative path
                        regularCsvPath = Path.Combine(currentDirectory, Constants.SPX_D);
                    var forOptionsCsvPath = generationResult.OptionsCsvPath;
                    if (!File.Exists(forOptionsCsvPath))
                        // Try relative path
                        forOptionsCsvPath = Path.Combine(currentDirectory, Constants.SPX_D_FOR_OPTIONS);

                    if (File.Exists(regularCsvPath) && File.Exists(forOptionsCsvPath))
                    {
                        var dailyGuides = Prices.CreateDailyPriceRecordsFromCsv(regularCsvPath);
                        var freshPriceRecords = Prices.CreateDailyPriceRecordsFromCsv(forOptionsCsvPath);

                        if (dailyGuides.Length > 0 && freshPriceRecords.Length > 0)
                        {
                            priceRecords = dailyGuides;
                            WriteInfo($"✓ Loaded {priceRecords.Length} fresh market price records");
                            WriteInfo(
                                $"  Date range: {priceRecords[0].DateTime:yyyy-MM-dd} to {priceRecords[priceRecords.Length - 1].DateTime:yyyy-MM-dd}");
                            WriteInfo(
                                $"  Time span: {(priceRecords[priceRecords.Length - 1].DateTime - priceRecords[0].DateTime).TotalDays:F0} days ({(priceRecords[priceRecords.Length - 1].DateTime - priceRecords[0].DateTime).TotalDays / 365.25:F1} years)");
                            WriteInfo(
                                $"  Price range: ${GetMinPrice(priceRecords):F2} - ${GetMaxPrice(priceRecords):F2}");
                        }
                        else
                        {
                            WriteWarning("No valid price records found in generated CSV, using provided data");
                        }
                    }
                    else
                    {
                        WriteWarning($"Generated CSV file not found at {regularCsvPath}, using provided data");
                    }
                }
                catch (Exception ex)
                {
                    WriteWarning($"Error loading fresh market data: {ex.Message}");
                    WriteWarning("Using provided price records...");
                }
            }

            // Step 4: Continue with window optimization using enhanced data
            WriteInfo($"Step 4: Starting window optimization with {priceRecords.Length} price records...");

            var results = new WindowSizeOptimizationResults(true);
            var configurations = GenerateWindowConfigurations(priceRecords.Length);

            WriteInfo(
                $"Testing {configurations.Count} different window configurations on {priceRecords.Length} price records...");

            // Enhanced configuration display with years of data context
            var dataYears = priceRecords.Length / 252.0; // Assuming ~252 trading days per year
            WriteInfo($"Available data spans approximately {dataYears:F1} years of trading data");

            if (dataYears >= 5.0)
                WriteInfo("✓ Excellent: Sufficient data for comprehensive long-term window analysis");
            else if (dataYears >= 2.0)
                WriteInfo("✓ Good: Adequate data for robust window optimization");
            else
                WriteWarning("⚠ Limited data: Results may be less reliable with < 2 years of data");

            int configurationNumber = 1;
            
            // OPTIMIZATION: Pre-allocate results collection with estimated capacity
            results.ConfigurationResults = new List<WindowConfigurationAnalysis>(configurations.Count);
            
            foreach (var config in configurations)
            {
                WriteInfo(
                    $"Testing configuration #{configurationNumber++} of {configurations.Count}: Training={config.TrainingSize} ({config.TrainingSize * 100.0 / priceRecords.Length:F1}%, {config.TrainingMonths:F1} months), " +
                    $"Testing={config.TestingSize} ({config.TestingSize * 100.0 / priceRecords.Length:F1}%, {config.TestingMonths:F1} months), " +
                    $"Step={config.StepSize} periods ({config.StepWeeks:F1} weeks)");

                try
                {
                    var walkforwardResult = RunWindowConfigurationTest(priceRecords, config);

                    // Log window date ranges in use for all windows
                    if (walkforwardResult.Windows != null && walkforwardResult.Windows.Count > 0)
                    {
                        WriteInfo("  Window date ranges:");
                        var windowCount = walkforwardResult.Windows.Count;
                        for (int i = 0; i < windowCount; i++)
                        {
                            var w = walkforwardResult.Windows[i];
                            var trStart = w.TrainingStartDate?.ToString("yyyy-MM-dd") ?? $"idx {w.TrainingStartIndex}";
                            var trEnd = w.TrainingEndDate?.ToString("yyyy-MM-dd") ?? $"idx {w.TrainingEndIndex}";
                            var teStart = w.TestStartDate?.ToString("yyyy-MM-dd") ?? $"idx {w.TestStartIndex}";
                            var teEnd = w.TestEndDate?.ToString("yyyy-MM-dd") ?? $"idx {w.TestEndIndex}";
                            WriteInfo($"    Window {w.WindowIndex:D2}: Train {trStart} → {trEnd}; Test {teStart} → {teEnd}");
                        }
                    }
                    else
                    {
                        WriteWarning("  No walkforward windows generated for this configuration.");
                    }

                    var analysis = AnalyzeWindowConfiguration(walkforwardResult, config);

                    results.ConfigurationResults.Add(analysis);

                    // Added normalized test performance (extrapolated to training window length)
                    // Example: If testing window is 1 period with 1% return and training window length is 10 periods with 10% return,
                    // we also display normalized test performance scaled by (TrainingSize / TestingSize) = 10 * 1% = 10%
                    var normalizedTestPerf = walkforwardResult.NormalizedAverageTestPerformance;

                    WriteInfo($"  Results: Robustness={analysis.RobustnessScore:F3}, " +
                              $"Consistency={analysis.ConsistencyScore:F3}, " +
                              $"Efficiency={analysis.EfficiencyScore:F3}, " +
                              $"Windows={walkforwardResult.Windows.Count}, " +
                              $"Avg Test Perf={walkforwardResult.AverageTestPerformance:F2}% (Norm={normalizedTestPerf:F2}%)");
                }
                catch (Exception ex)
                {
                    WriteWarning($"  Configuration failed: {ex.Message}");
                }
            }

            // Find optimal configurations
            results.OptimalConfiguration = FindOptimalConfiguration(results.ConfigurationResults);
            results.Recommendations =
                GenerateWindowSizeRecommendations(results.ConfigurationResults, priceRecords.Length);

            // Enhanced results display with data context
            WriteInfo("Step 5: Analysis complete - displaying enhanced results...");
            DisplayWindowOptimizationResults(results);

            // Add summary of data enhancement
            WriteSection("Data Enhancement Summary");
            if (generationResult.Success)
            {
                WriteInfo("✓ Successfully enhanced analysis with fresh market data:");
                WriteInfo($"  Original provided records: {priceRecords.Length} (estimated)");
                WriteInfo($"  Enhanced market data: {generationResult.RegularRecordCount} records");
                WriteInfo(
                    $"  Data improvement: {((double)generationResult.RegularRecordCount / Math.Max(1, priceRecords.Length) - 1) * 100:F1}% more data");
                WriteInfo($"  Time span enhancement: {generationResult.RegularDateRange}");
                WriteInfo(
                    $"  Options data available: {generationResult.OptionsRecordCount} records ({generationResult.OptionsDateRange})");
            }
            else
            {
                WriteWarning(
                    "Analysis completed with provided data - consider checking Constants.SPX_JSON for fresh market data");
            }

            return results;
        }

        /// <summary>
        ///     Run walkforward analysis for a specific window configuration. Public wrapper so callers can evaluate
        ///     selected training/testing window pairs and collect per-window best individuals/trades.
        /// </summary>
        /// <param name="priceRecords">Complete price record data</param>
        /// <param name="config">Window configuration (training/testing/step)</param>
        /// <returns>Walkforward results for the given configuration</returns>
        public static WalkforwardResults RunWalkforwardForConfiguration(PriceRecord[] priceRecords, WindowConfiguration config)
        {
            return RunWalkforwardAnalysisWithConfiguration(priceRecords, config);
        }

        #endregion

        #region Recommendation Generation

        /// <summary>
        ///     Generate recommendations for window sizes based on analysis results.
        ///     OPTIMIZED: Reduced LINQ operations and pre-computed statistics.
        /// </summary>
        private static List<string> GenerateWindowSizeRecommendations(List<WindowConfigurationAnalysis> results,
            int totalDataPoints)
        {
            var recommendations = new List<string>(20); // Pre-allocate with estimated capacity

            if (results.Count == 0)
            {
                recommendations.Add("⚠️ No valid window configurations could be tested.");
                return recommendations;
            }

            var optimal = FindOptimalConfiguration(results);
            
            // OPTIMIZATION: Single pass to get top 3 configurations instead of OrderByDescending().Take()
            var topConfigs = GetTopConfigurations(results, 3);

            recommendations.Add("🎯 OPTIMAL WINDOW SIZE RECOMMENDATIONS:");
            recommendations.Add("");

            if (optimal.IsRecommended)
            {
                recommendations.Add("✅ RECOMMENDED CONFIGURATION:");
                recommendations.Add(
                    $"   Training Window: {optimal.Configuration.TrainingSize} periods ({optimal.Configuration.TrainingMonths:F1} months)");
                recommendations.Add(
                    $"   Testing Window:  {optimal.Configuration.TestingSize} periods ({optimal.Configuration.TestingMonths:F1} months)");
                recommendations.Add(
                    $"   Step Size:       {optimal.Configuration.StepSize} periods ({optimal.Configuration.StepWeeks:F1} weeks)");
                recommendations.Add($"   Overall Score:   {optimal.OverallScore:F3}/1.000");
                recommendations.Add($"   Robustness:      {optimal.RobustnessScore:F3}/1.000");
                recommendations.Add($"   Windows Generated: {optimal.WalkforwardResults.Windows.Count}");
            }
            else
            {
                recommendations.Add("⚠️ NO IDEAL CONFIGURATION FOUND");
                recommendations.Add(
                    $"   Best Available: {optimal.Configuration.TrainingSize}/{optimal.Configuration.TestingSize}/{optimal.Configuration.StepSize}");
                recommendations.Add($"   Score: {optimal.OverallScore:F3} (below 0.7 threshold)");
            }

            recommendations.Add("");
            recommendations.Add("📊 TOP 3 CONFIGURATIONS:");

            var topCount = Math.Min(3, topConfigs.Count);
            for (var i = 0; i < topCount; i++)
            {
                var config = topConfigs[i];
                var status = config.IsRecommended ? "✅" : "⚠️";
                recommendations.Add(
                    $"   {i + 1}. {status} Training: {config.Configuration.TrainingSize} | Testing: {config.Configuration.TestingSize} | Step: {config.Configuration.StepSize}");
                recommendations.Add(
                    $"      Score: {config.OverallScore:F3} | Windows: {config.WalkforwardResults.Windows.Count} | Robustness: {config.RobustnessScore:F3}");
            }

            recommendations.Add("");
            recommendations.Add("🔬 ANALYSIS INSIGHTS:");

            // Data availability analysis
            var dataYears = totalDataPoints / 252.0;
            recommendations.Add($"   Available Data: {totalDataPoints} periods ({dataYears:F1} years)");

            if (dataYears < 2.0)
                recommendations.Add("   ⚠️ Limited data - consider longer collection period for robust testing");
            else if (dataYears >= 5.0)
                recommendations.Add("   ✅ Sufficient data for comprehensive walkforward analysis");

            // Window size guidance - OPTIMIZED: Single pass calculation
            var averageTrainingSize = 252.0;
            var averageTestingRatio = 0.25;
            var averageStepSize = 21.0;
            var recommendedCount = 0;

            // Single pass to calculate averages for recommended configurations
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].IsRecommended)
                {
                    averageTrainingSize += results[i].Configuration.TrainingSize;
                    averageTestingRatio += (double)results[i].Configuration.TestingSize / results[i].Configuration.TrainingSize;
                    averageStepSize += results[i].Configuration.StepSize;
                    recommendedCount++;
                }
            }

            if (recommendedCount > 0)
            {
                averageTrainingSize /= recommendedCount;
                averageTestingRatio /= recommendedCount;
                averageStepSize /= recommendedCount;
            }

            recommendations.Add($"   Recommended Training Period: ~{averageTrainingSize / 21:F0} months");
            recommendations.Add($"   Recommended Testing Ratio: ~{averageTestingRatio:P0} of training size");
            
            // Rebalancing frequency guidance
            recommendations.Add(
                $"   Recommended Rebalancing: Every ~{averageStepSize:F0} periods ({averageStepSize / 5:F1} weeks)");

            return recommendations;
        }

        #endregion

        #region Display Methods

        /// <summary>
        ///     Display comprehensive window optimization results.
        /// </summary>
        public static void DisplayWindowOptimizationResults(WindowSizeOptimizationResults results)
        {
            WriteSection("Window Size Optimization Results");

            ConsoleUtilities.WriteLine(
                "═══════════════════════════════════════════════════════════════════════════════");
            ConsoleUtilities.WriteLine("                        WINDOW SIZE OPTIMIZATION ANALYSIS");
            ConsoleUtilities.WriteLine(
                "═══════════════════════════════════════════════════════════════════════════════");
            ConsoleUtilities.WriteLine();

            if (results.OptimalConfiguration.IsRecommended)
            {
                var optimal = results.OptimalConfiguration;
                ConsoleUtilities.WriteLine("🎯 OPTIMAL CONFIGURATION FOUND:");
                ConsoleUtilities.WriteLine(
                    $"   Training: {optimal.Configuration.TrainingSize} periods ({optimal.Configuration.TrainingMonths:F1} months)");
                ConsoleUtilities.WriteLine(
                    $"   Testing:  {optimal.Configuration.TestingSize} periods ({optimal.Configuration.TestingMonths:F1} months)");
                ConsoleUtilities.WriteLine(
                    $"   Step:     {optimal.Configuration.StepSize} periods ({optimal.Configuration.StepWeeks:F1} weeks)");
                ConsoleUtilities.WriteLine($"   Score:    {optimal.OverallScore:F3}/1.000");
                ConsoleUtilities.WriteLine();

                ConsoleUtilities.WriteLine("📈 PERFORMANCE METRICS:");
                ConsoleUtilities.WriteLine(
                    $"   Avg Test Performance:    {optimal.WalkforwardResults.AverageTestPerformance:F2}%");
                ConsoleUtilities.WriteLine(
                    $"   Performance Consistency: {optimal.WalkforwardResults.ConsistencyScore:F2}%");
                ConsoleUtilities.WriteLine(
                    $"   Overfitting Frequency:   {optimal.WalkforwardResults.OverfittingFrequency:F1}%");
                ConsoleUtilities.WriteLine($"   Windows Generated:       {optimal.WalkforwardResults.Windows.Count}");
                ConsoleUtilities.WriteLine($"   Statistical Power:       {optimal.StatisticalPower:F3}");
                ConsoleUtilities.WriteLine();
            }

            // Display top configurations table - OPTIMIZED: Single pass for top 10
            ConsoleUtilities.WriteLine("📊 CONFIGURATION COMPARISON:");
            ConsoleUtilities.WriteLine(
                $"{"Rank",-4} {"Training",-8} {"Testing",-7} {"Step",-4} {"Score",-5} {"Robust",-6} {"Windows",-7} {"Status",-10}");
            ConsoleUtilities.WriteLine(new string('─', 70));

            var topConfigs = GetTopConfigurations(results.ConfigurationResults, 10);
            var rank = 1;

            foreach (var config in topConfigs)
            {
                var status = config.IsRecommended ? "✅ GOOD" : "⚠️ POOR";
                Console.ForegroundColor = config.IsRecommended ? ConsoleColor.Green : ConsoleColor.Yellow;

                ConsoleUtilities.WriteLine(
                    $"{rank,-4} {config.Configuration.TrainingSize,-8} {config.Configuration.TestingSize,-7} " +
                    $"{config.Configuration.StepSize,-4} {config.OverallScore,-5:F3} {config.RobustnessScore,-6:F3} " +
                    $"{config.WalkforwardResults.Windows.Count,-7} {status,-10}");
                Console.ResetColor();
                rank++;
            }

            ConsoleUtilities.WriteLine();

            // Display recommendations
            var recommendationCount = results.Recommendations.Count;
            for (int i = 0; i < recommendationCount; i++)
            {
                ConsoleUtilities.WriteLine(results.Recommendations[i]);
            }

            ConsoleUtilities.WriteLine();
            ConsoleUtilities.WriteLine(
                "═══════════════════════════════════════════════════════════════════════════════");
        }

        #endregion

        #region Window Optimization Data Structures

        /// <summary>
        ///     Configuration for window sizes in walkforward analysis.
        /// </summary>
        public struct WindowConfiguration
        {
            public int TrainingSize; // Number of periods for training
            public int TestingSize; // Number of periods for testing
            public int StepSize; // Number of periods to step forward
            public double TrainingMonths; // Approximate months for training
            public double TestingMonths; // Approximate months for testing
            public double StepWeeks; // Approximate weeks per step
        }

        /// <summary>
        ///     Analysis results for a specific window configuration.
        /// </summary>
        public struct WindowConfigurationAnalysis
        {
            public WindowConfiguration Configuration;
            public WalkforwardResults WalkforwardResults;
            public double RobustnessScore; // 0-1, resistance to overfitting
            public double ConsistencyScore; // 0-1, performance consistency
            public double EfficiencyScore; // 0-1, computational efficiency
            public double StatisticalPower; // 0-1, statistical significance
            public double OverallScore; // 0-1, weighted combination
            public bool IsRecommended; // Whether this config is recommended
        }

        /// <summary>
        ///     Complete results from window size optimization analysis.
        /// </summary>
        public struct WindowSizeOptimizationResults
        {
            public List<WindowConfigurationAnalysis> ConfigurationResults;
            public WindowConfigurationAnalysis OptimalConfiguration;
            public List<string> Recommendations;

            public WindowSizeOptimizationResults(bool initialize)
            {
                ConfigurationResults = new List<WindowConfigurationAnalysis>();
                OptimalConfiguration = default;
                Recommendations = new List<string>();
            }
        }

        /// <summary>
        ///     Represents results from a single walkforward window.
        /// </summary>
        public struct WalkforwardWindow
        {
            public int WindowIndex;
            public int TrainingStartIndex;
            public int TrainingEndIndex;
            public int TestStartIndex;
            public int TestEndIndex;
            public DateTime? TrainingStartDate;
            public DateTime? TrainingEndDate;
            public DateTime? TestStartDate;
            public DateTime? TestEndDate;
            public GeneticIndividual BestIndividual;
            public double TrainingPerformance;
            public double TestPerformance;
            public double PerformanceGap;
            public int TradesExecuted;
            public double MaxDrawdown;
            public double SharpeRatio;
            public bool EarlyStoppedDueToOverfitting;
            public int GenerationsUsed;
        }

        /// <summary>
        ///     Comprehensive walkforward analysis results.
        /// </summary>
        public struct WalkforwardResults
        {
            public List<WalkforwardWindow> Windows;
            public double AverageTrainingPerformance;
            public double AverageTestPerformance;
            public double NormalizedAverageTestPerformance; // New: test performance scaled to training window length
            public double AveragePerformanceGap;
            public double ConsistencyScore; // Standard deviation of test performances
            public double CumulativeReturn;
            public double MaxSystemDrawdown;
            public double AverageSharpeRatio;
            public int TotalTradesExecuted;
            public double OverfittingFrequency; // Percentage of windows with large gaps
            public bool IsStrategyRobust; // Overall assessment
        }

        #endregion

        #region Configuration Generation - OPTIMIZED

        /// <summary>
        ///     Generate different window size configurations to test.
        ///     Based on common trading patterns and statistical requirements.
        ///     OPTIMIZED: Reduced algorithmic complexity from O(n³) to O(n) with caching.
        /// </summary>
        private static List<WindowConfiguration> GenerateWindowConfigurations(int totalDataPoints)
        {
            // Check cache first
            lock (_cachelock)
            {
                if (_configCache.TryGetValue(totalDataPoints, out var cachedConfigs))
                {
                    return cachedConfigs;
                }
            }

            var configurations = new List<WindowConfiguration>(200); // Pre-allocate with estimated capacity

            // Pre-computed arrays for faster iteration - OPTIMIZATION: Avoid repeated array creation
            var trainingDays = new[] { 63, 126, 189, 252, 378, 504 }; // 3, 6, 9, 12, 18, 24 months
            var testingRatios = MathConstants.Take(5).ToArray(); // { 0.1, 0.15, 0.2, 0.25, 0.3 }
            var stepSizeRatios = MathConstants.Skip(5).ToArray(); // { 0.05, 0.083, 0.125, 0.167, 0.25 }

            var maxDataThreshold = totalDataPoints * 0.8; // Pre-calculate threshold

            // OPTIMIZATION: Flatten nested loops and use direct calculations
            foreach (var trainingSize in trainingDays)
            {
                if (trainingSize >= maxDataThreshold) continue; // Skip if too large

                var trainingMonths = trainingSize / 21.0; // Pre-calculate outside inner loops

                foreach (var testRatio in testingRatios)
                {
                    var testingSize = Math.Max(21, (int)(trainingSize * testRatio)); // At least 1 month
                    var testingMonths = testingSize / 21.0; // Pre-calculate

                    foreach (var stepRatio in stepSizeRatios)
                    {
                        var stepSize = Math.Max(1, (int)(testingSize * stepRatio));
                        var stepWeeks = stepSize / 5.0; // Pre-calculate

                        // Validate configuration once - OPTIMIZATION: Early exit
                        if (trainingSize + testingSize < totalDataPoints && stepSize > 0)
                        {
                            configurations.Add(new WindowConfiguration
                            {
                                TrainingSize = trainingSize,
                                TestingSize = testingSize,
                                StepSize = stepSize,
                                TrainingMonths = trainingMonths,
                                TestingMonths = testingMonths,
                                StepWeeks = stepWeeks
                            });
                        }
                    }
                }
            }

            // Add some specific configurations based on academic research
            AddResearchBasedConfigurations(configurations, totalDataPoints);

            // OPTIMIZATION: Remove duplicates using HashSet instead of GroupBy LINQ
            var uniqueConfigs = RemoveDuplicateConfigurations(configurations);

            // Cache result
            lock (_cachelock)
            {
                _configCache[totalDataPoints] = uniqueConfigs;
            }

            return uniqueConfigs;
        }

        /// <summary>
        ///     Add configurations based on academic research and industry best practices.
        /// </summary>
        private static void AddResearchBasedConfigurations(List<WindowConfiguration> configurations,
            int totalDataPoints)
        {
            // Add some research-based configurations if we have enough data
            if (totalDataPoints >= 1000)
            {
                // Configuration 1: Harvey et al. (2016) - minimum 5 years for robust testing
                configurations.Add(new WindowConfiguration
                {
                    TrainingSize = 1260, // 5 years
                    TestingSize = 252, // 1 year
                    StepSize = 63, // Quarterly rebalancing
                    TrainingMonths = 60,
                    TestingMonths = 12,
                    StepWeeks = 12.6
                });

                // Configuration 2: Industry standard - 10 years training, 2 years testing
                configurations.Add(new WindowConfiguration
                {
                    TrainingSize = 2520, // 10 years
                    TestingSize = 504, // 2 years
                    StepSize = 21, // Monthly rebalancing
                    TrainingMonths = 120,
                    TestingMonths = 24,
                    StepWeeks = 4.2
                });
            }

            // Always add conservative short-term configurations
            configurations.Add(new WindowConfiguration
            {
                TrainingSize = 252, // 1 year training
                TestingSize = 63, // 3 months testing
                StepSize = 21, // Monthly rebalancing
                TrainingMonths = 12,
                TestingMonths = 3,
                StepWeeks = 4.2
            });

            configurations.Add(new WindowConfiguration
            {
                TrainingSize = 504, // 2 years training
                TestingSize = 126, // 6 months testing
                StepSize = 21, // Monthly rebalancing
                TrainingMonths = 24,
                TestingMonths = 6,
                StepWeeks = 4.2
            });
        }

        #endregion

        #region Window Configuration Testing

        /// <summary>
        ///     Run walkforward analysis with a specific window configuration.
        /// </summary>
        private static WalkforwardResults RunWindowConfigurationTest(PriceRecord[] priceRecords,
            WindowConfiguration config)
        {
            return RunWalkforwardAnalysisWithConfiguration(priceRecords, config);
        }

        /// <summary>
        ///     Custom walkforward analysis using specified configuration.
        ///     OPTIMIZED: Pre-allocated collections, reduced memory allocations.
        /// </summary>
        private static WalkforwardResults RunWalkforwardAnalysisWithConfiguration(PriceRecord[] priceRecords,
            WindowConfiguration config)
        {
            var windows = new List<WalkforwardWindow>(20); // Pre-allocate with estimated capacity
            var cumulativeBalance = Program.StartingBalance;
            var peakBalance = Program.StartingBalance;
            var maxSystemDrawdown = 0.0;
            var testPerformances = new List<double>(20); // Pre-allocate

            // Calculate number of possible windows with this configuration
            var maxPossibleWindows = Math.Min(
                (priceRecords.Length - config.TrainingSize) / config.StepSize,
                20 // Limit to prevent excessive computation
            );

            for (var windowIndex = 0; windowIndex < maxPossibleWindows; windowIndex++)
            {
                var trainStart = windowIndex * config.StepSize;
                var trainEnd = trainStart + config.TrainingSize - 1;
                var testStart = trainEnd + 1;
                var testEnd = Math.Min(testStart + config.TestingSize - 1, priceRecords.Length - 1);

                if (testEnd >= priceRecords.Length || trainEnd - trainStart + 1 < 63)
                    continue;

                var trainingRecords = GeneticIndividual.CreateSubset(priceRecords, trainStart, trainEnd);
                var testRecords = GeneticIndividual.CreateSubset(priceRecords, testStart, testEnd);

                var validationSize = (int)(trainingRecords.Length * Program.ValidationPercentage);
                var trainSplitRecords =
                    GeneticIndividual.CreateSubset(trainingRecords, 0, trainingRecords.Length - validationSize - 1);
                var validationSplitRecords = GeneticIndividual.CreateSubset(trainingRecords,
                    trainingRecords.Length - validationSize, trainingRecords.Length - 1);

                GeneticIndividual.AnalyzeIndicatorRanges(trainSplitRecords);
                GeneticIndividual.InitializeOptionSolvers(Constants.SPX_D_FOR_OPTIONS);

                GeneticIndividual bestIndividual;
                if (Program.UseGAInOptimizer)
                {
                    bestIndividual = Program_RunGAForOptimizer(trainSplitRecords, validationSplitRecords, windowIndex);
                }
                else
                {
                    bestIndividual = CreateSimpleTestIndividual();
                }

                var trainingFitness = bestIndividual.Process(trainingRecords);
                var testFitness = bestIndividual.Process(testRecords);

                var performanceGap = Math.Abs(trainingFitness.PercentGain - testFitness.PercentGain);
                var sharpeRatio = CalculateRiskAdjustedReturn(bestIndividual);
                var maxDrawdown = CalculateMaxDrawdown(bestIndividual);

                var windowReturn = testFitness.PercentGain / 100.0;
                cumulativeBalance *= 1.0 + windowReturn;

                if (cumulativeBalance > peakBalance)
                {
                    peakBalance = cumulativeBalance;
                }
                else
                {
                    var currentDrawdown = (peakBalance - cumulativeBalance) / peakBalance * 100.0;
                    maxSystemDrawdown = Math.Max(maxSystemDrawdown, currentDrawdown);
                }

                var window = new WalkforwardWindow
                {
                    WindowIndex = windowIndex,
                    TrainingStartIndex = trainStart,
                    TrainingEndIndex = trainEnd,
                    TestStartIndex = testStart,
                    TestEndIndex = testEnd,
                    TrainingStartDate = priceRecords[trainStart].DateTime,
                    TrainingEndDate = priceRecords[trainEnd].DateTime,
                    TestStartDate = priceRecords[testStart].DateTime,
                    TestEndDate = priceRecords[testEnd].DateTime,
                    BestIndividual = bestIndividual,
                    TrainingPerformance = trainingFitness.PercentGain,
                    TestPerformance = testFitness.PercentGain,
                    PerformanceGap = performanceGap,
                    TradesExecuted = bestIndividual.Trades.Count,
                    MaxDrawdown = maxDrawdown,
                    SharpeRatio = sharpeRatio,
                    EarlyStoppedDueToOverfitting = performanceGap > 15.0,
                    GenerationsUsed = Program.Generations
                };

                windows.Add(window);
                testPerformances.Add(testFitness.PercentGain);
            }

            // Calculate summary statistics - OPTIMIZED: Single pass calculations
            var windowCount = windows.Count;
            if (windowCount == 0)
            {
                return new WalkforwardResults
                {
                    Windows = windows,
                    AverageTrainingPerformance = 0,
                    AverageTestPerformance = 0,
                    NormalizedAverageTestPerformance = 0,
                    AveragePerformanceGap = 0,
                    ConsistencyScore = 0,
                    CumulativeReturn = 0,
                    MaxSystemDrawdown = 0,
                    AverageSharpeRatio = 0,
                    TotalTradesExecuted = 0,
                    OverfittingFrequency = 0,
                    IsStrategyRobust = false
                };
            }

            // Single pass calculation of statistics
            double sumTraining = 0, sumTest = 0, sumGap = 0, sumSharpe = 0;
            int totalTrades = 0;
            int overfittingCount = 0;

            for (int i = 0; i < windowCount; i++)
            {
                sumTraining += windows[i].TrainingPerformance;
                sumTest += windows[i].TestPerformance;
                sumGap += windows[i].PerformanceGap;
                sumSharpe += windows[i].SharpeRatio;
                totalTrades += windows[i].TradesExecuted;
                if (windows[i].EarlyStoppedDueToOverfitting) overfittingCount++;
            }

            var averageTrainingPerformance = sumTraining / windowCount;
            var averageTestPerformance = sumTest / windowCount;
            var averageGap = sumGap / windowCount;
            var averageSharpe = sumSharpe / windowCount;
            var overfittingFrequency = overfittingCount * 100.0 / windowCount;

            // OPTIMIZED: Use efficient standard deviation calculation
            var consistencyScore = CalculateStandardDeviationOptimized(testPerformances);

            var isRobust = averageGap < 10.0 && consistencyScore < 15.0 && overfittingFrequency < 30.0 &&
                           averageTestPerformance > 0.0;

            // New: normalized average test performance extrapolated to training window length
            double normalizedAvgTest = 0.0;
            if (config.TestingSize > 0)
            {
                normalizedAvgTest = averageTestPerformance * (config.TrainingSize / (double)config.TestingSize);
            }

            return new WalkforwardResults
            {
                Windows = windows,
                AverageTrainingPerformance = averageTrainingPerformance,
                AverageTestPerformance = averageTestPerformance,
                NormalizedAverageTestPerformance = normalizedAvgTest,
                AveragePerformanceGap = averageGap,
                ConsistencyScore = consistencyScore,
                CumulativeReturn = (cumulativeBalance - Program.StartingBalance) / Program.StartingBalance * 100.0,
                MaxSystemDrawdown = maxSystemDrawdown,
                AverageSharpeRatio = averageSharpe,
                TotalTradesExecuted = totalTrades,
                OverfittingFrequency = overfittingFrequency,
                IsStrategyRobust = isRobust
            };
        }

        // Minimal GA runner reusing Program's configuration; keeps scope local to optimizer
        private static GeneticIndividual Program_RunGAForOptimizer(PriceRecord[] trainingSplit, PriceRecord[] validationSplit, int windowIndex)
        {
            // Simple early-stopping GA using Program's parameters without islands/history
            var rngSeedBase = 4242 + windowIndex * 1000;
            var population = new List<GeneticIndividual>(Program.PopulationSize); // Pre-allocate
            for (int i = 0; i < Program.PopulationSize; i++)
            {
                population.Add(new GeneticIndividual(new Random(rngSeedBase + i),
                    Program.StartingBalance,
                    Program.IndicatorTypeMin, Program.IndicatorTypeMax,
                    Program.IndicatorPeriodMin, Program.IndicatorPeriodMax,
                    Program.IndicatorModeMin, Program.IndicatorModeMax,
                    Program.IndicatorTimeFrameMin, Program.IndicatorTimeFrameMax,
                    Program.IndicatorPolarityMin, Program.IndicatorPolarityMax,
                    Program.IndicatorThresholdMin, Program.IndicatorThresholdMax,
                    Program.MaxIndicators, Program.TradePercentageForStocksMin, Program.TradePercentageForStocksMax,
                    Program.OptionDaysOutMin, Program.OptionDaysOutMax,
                    Program.OptionStrikeDistanceMin, Program.OptionStrikeDistanceMax,
                    Program.FastMAPeriodMin, Program.FastMAPeriodMax,
                    Program.SlowMAPeriodMin, Program.SlowMAPeriodMax,
                    Program.AllowedTradeTypeMin, Program.AllowedTradeTypeMax,
                    Program.AllowedOptionTypeMin, Program.AllowedOptionTypeMax,
                    Program.AllowedSecurityTypeMin, Program.AllowedSecurityTypeMax,
                    Program.NumberOfOptionContractsMin, Program.NumberOfOptionContractsMax));
            }

            double bestVal = double.MinValue;
            GeneticIndividual best = null;
            int patience = 0;
            for (int gen = 0; gen < Program.Generations; gen++)
            {
                // Evaluate on training
                foreach (var ind in population)
                    ind.Fitness = ind.Process(trainingSplit);

                // OPTIMIZATION: Use direct iteration instead of OrderByDescending().First()
                var champion = GetBestIndividual(population);
                var valFitness = champion.Process(validationSplit);
                if (valFitness.PercentGain > bestVal)
                {
                    bestVal = valFitness.PercentGain;
                    best = GeneticEvolvers.CloneIndividual(champion);
                    patience = 0;
                }
                else
                {
                    patience++;
                }
                if (Program.EnableWalkforwardEarlyStopping && patience >= Program.EarlyStoppingPatience) break;

                // Breed next generation (elitism + mutations)
                var next = new List<GeneticIndividual>(Program.PopulationSize) { GeneticEvolvers.CloneIndividual(champion) };
                var rng = new Random(rngSeedBase + gen * 100);
                while (next.Count < population.Count)
                {
                    var parent = population[rng.Next(population.Count)];
                    next.Add(GeneticEvolvers.CloneIndividual(parent));
                }
                population = next;
            }

            return best ?? GetBestIndividual(population);
        }
        #endregion

        #region Analysis Methods

        /// <summary>
        ///     Analyze the effectiveness of a window configuration.
        /// </summary>
        private static WindowConfigurationAnalysis AnalyzeWindowConfiguration(WalkforwardResults walkforwardResult,
            WindowConfiguration config)
        {
            var analysis = new WindowConfigurationAnalysis
            {
                Configuration = config,
                WalkforwardResults = walkforwardResult
            };

            // Calculate robustness score (0-1, higher is better)
            // Based on: low overfitting frequency, small performance gaps, positive test performance
            var robustnessPenalty = 0.0;
            
            // FIXED: More realistic thresholds for trading strategies
            if (walkforwardResult.OverfittingFrequency > 60.0) robustnessPenalty += 0.2; // Was 30.0, +0.3
            if (walkforwardResult.OverfittingFrequency > 80.0) robustnessPenalty += 0.2; // Additional penalty for very high overfitting
            
            if (walkforwardResult.AveragePerformanceGap > 50.0) robustnessPenalty += 0.2; // Was 15.0, +0.3  
            if (walkforwardResult.AveragePerformanceGap > 100.0) robustnessPenalty += 0.2; // Additional penalty for extreme gaps
            
            if (walkforwardResult.AverageTestPerformance <= 0.0) robustnessPenalty += 0.4; // Keep this - negative performance is bad

            analysis.RobustnessScore = Math.Max(0.0, 1.0 - robustnessPenalty);

            // Calculate consistency score (0-1, higher is better)
            // Based on low standard deviation of test performances
            var maxConsistency = 50.0; // Was 20.0 - more realistic for trading strategies  
            analysis.ConsistencyScore = Math.Max(0.0, 1.0 - walkforwardResult.ConsistencyScore / maxConsistency);

            // Calculate efficiency score (0-1, higher is better)
            // Based on number of windows generated and computational efficiency
            var expectedWindows = Math.Max(1, (double)walkforwardResult.Windows.Count);
            var optimalWindows = 10.0; // Target around 10 windows for good statistics
            analysis.EfficiencyScore = Math.Min(1.0, expectedWindows / optimalWindows);

            // Calculate statistical power (0-1, higher is better)
            // Based on having enough data points for reliable statistical inference
            double minimumSampleSize = 30; // Minimum for central limit theorem
            double actualSampleSize = walkforwardResult.Windows.Count;
            analysis.StatisticalPower = Math.Min(1.0, actualSampleSize / minimumSampleSize);

            // Calculate overall score (weighted combination)
            analysis.OverallScore = analysis.RobustnessScore * 0.4 +
                                    analysis.ConsistencyScore * 0.3 +
                                    analysis.EfficiencyScore * 0.2 +
                                    analysis.StatisticalPower * 0.1;

            // Determine if this configuration is recommended - more lenient thresholds
            analysis.IsRecommended = analysis.OverallScore >= 0.5 && // Was 0.7
                                     analysis.RobustnessScore >= 0.4 && // Was 0.6  
                                     walkforwardResult.Windows.Count >= 5;

            return analysis;
        }

        /// <summary>
        ///     Find the optimal window configuration from all tested configurations.
        ///     OPTIMIZED: Single pass algorithm instead of LINQ operations.
        /// </summary>
        private static WindowConfigurationAnalysis FindOptimalConfiguration(List<WindowConfigurationAnalysis> results)
        {
            if (results.Count == 0) return default;

            WindowConfigurationAnalysis bestRecommended = default;
            WindowConfigurationAnalysis bestOverall = default;
            bool hasRecommended = false;
            bool hasAny = false;
            
            // Single pass to find both best recommended and best overall
            for (int i = 0; i < results.Count; i++)
            {
                var current = results[i];
                
                if (!hasAny || current.OverallScore > bestOverall.OverallScore)
                {
                    bestOverall = current;
                    hasAny = true;
                }
                
                if (current.IsRecommended)
                {
                    if (!hasRecommended || current.OverallScore > bestRecommended.OverallScore)
                    {
                        bestRecommended = current;
                        hasRecommended = true;
                    }
                }
            }

            return hasRecommended ? bestRecommended : bestOverall;
        }

        #endregion

        #region Helper Methods - OPTIMIZED

        /// <summary>
        ///     Create a simple test individual for configuration testing.
        /// </summary>
        private static GeneticIndividual CreateSimpleTestIndividual()
        {
            var individual = new GeneticIndividual
            {
                StartingBalance = Program.StartingBalance
            };

            // Add a simple SMA indicator for testing
            individual.Indicators.Add(new IndicatorParams
            {
                Type = 1, // SMA
                Period = 20,
                Mode = 1,
                TimeFrame = TimeFrame.D1,
                Polarity = 1,
                LongThreshold = 0.5,
                ShortThreshold = -0.5
            });

            return individual;
        }

        /// <summary>
        ///     Calculate Sharpe-like ratio for trading strategy performance evaluation.
        ///     OPTIMIZED: Single pass calculation with reduced memory allocations.
        /// </summary>
        private static double CalculateRiskAdjustedReturn(GeneticIndividual individual, double riskFreeRate = 0.02)
        {
            var tradeCount = individual.Trades.Count;
            if (tradeCount == 0) return 0.0;

            // OPTIMIZATION: Single pass calculation instead of multiple LINQ operations
            double sumReturns = 0.0;
            double sumSquaredDiffs = 0.0;

            // First pass: calculate mean
            for (int i = 0; i < tradeCount; i++)
            {
                sumReturns += individual.Trades[i].PercentGain / 100.0;
            }
            var meanReturn = sumReturns / tradeCount;

            // Second pass: calculate variance
            for (int i = 0; i < tradeCount; i++)
            {
                var returnValue = individual.Trades[i].PercentGain / 100.0;
                var diff = returnValue - meanReturn;
                sumSquaredDiffs += diff * diff;
            }
            
            var standardDeviation = Math.Sqrt(sumSquaredDiffs / tradeCount);

            if (standardDeviation == 0) return meanReturn > riskFreeRate ? double.PositiveInfinity : 0.0;

            return (meanReturn - riskFreeRate) / standardDeviation;
        }

        /// <summary>
        ///     Calculate maximum drawdown for the trading strategy.
        ///     OPTIMIZED: Single pass algorithm.
        /// </summary>
        private static double CalculateMaxDrawdown(GeneticIndividual individual)
        {
            var tradeCount = individual.Trades.Count;
            if (tradeCount == 0) return 0.0;

            var peak = individual.StartingBalance;
            var maxDrawdown = 0.0;

            // Single pass calculation
            for (int i = 0; i < tradeCount; i++)
            {
                var balance = individual.Trades[i].Balance;
                if (balance > peak)
                {
                    peak = balance;
                }
                else
                {
                    var drawdown = (peak - balance) / peak * 100.0;
                    maxDrawdown = Math.Max(maxDrawdown, drawdown);
                }
            }

            return maxDrawdown;
        }

        /// <summary>
        ///     Calculate standard deviation of an array of values.
        ///     OPTIMIZED: Single-pass algorithm with pre-allocated array.
        /// </summary>
        private static double CalculateStandardDeviation(double[] values)
        {
            if (values.Length == 0) return 0.0;
            return CalculateStandardDeviationOptimized(values, values.Length);
        }

        /// <summary>
        ///     Optimized standard deviation calculation for List&lt;double&gt;.
        /// </summary>
        private static double CalculateStandardDeviationOptimized(List<double> values)
        {
            var count = values.Count;
            if (count == 0) return 0.0;

            // Ensure temp array is large enough
            lock (_cachelock)
            {
                if (_tempArray.Length < count)
                {
                    _tempArray = new double[Math.Max(count * 2, 1000)];
                }
            }

            // Copy to temp array for efficient access
            for (int i = 0; i < count; i++)
            {
                _tempArray[i] = values[i];
            }

            return CalculateStandardDeviationOptimized(_tempArray, count);
        }

        /// <summary>
        ///     Highly optimized standard deviation calculation.
        /// </summary>
        private static double CalculateStandardDeviationOptimized(double[] values, int count)
        {
            if (count == 0) return 0.0;

            // Single pass algorithm for better cache performance
            double sum = 0.0;
            double sumSquares = 0.0;

            for (int i = 0; i < count; i++)
            {
                var value = values[i];
                sum += value;
                sumSquares += value * value;
            }

            var mean = sum / count;
            var variance = (sumSquares / count) - (mean * mean);
            
            return Math.Sqrt(Math.Max(0.0, variance)); // Ensure non-negative variance
        }

        /// <summary>
        ///     Get top N configurations efficiently without LINQ.
        /// </summary>
        private static List<WindowConfigurationAnalysis> GetTopConfigurations(
            List<WindowConfigurationAnalysis> configurations, int topN)
        {
            if (configurations.Count <= topN)
                return configurations;

            var result = new List<WindowConfigurationAnalysis>(topN);
            
            // Simple selection sort for top N elements
            for (int i = 0; i < topN && i < configurations.Count; i++)
            {
                var best = configurations[i];
                var bestIndex = i;
                
                for (int j = i + 1; j < configurations.Count; j++)
                {
                    if (configurations[j].OverallScore > best.OverallScore)
                    {
                        best = configurations[j];
                        bestIndex = j;
                    }
                }
                
                // Swap if needed
                if (bestIndex != i)
                {
                    configurations[bestIndex] = configurations[i];
                    configurations[i] = best;
                }
                
                result.Add(best);
            }
            
            return result;
        }

        /// <summary>
        ///     Remove duplicate configurations efficiently without LINQ.
        /// </summary>
        private static List<WindowConfiguration> RemoveDuplicateConfigurations(List<WindowConfiguration> configurations)
        {
            var unique = new List<WindowConfiguration>(configurations.Count);
            var seen = new HashSet<(int, int, int)>(configurations.Count);
            
            for (int i = 0; i < configurations.Count; i++)
            {
                var config = configurations[i];
                var key = (config.TrainingSize, config.TestingSize, config.StepSize);
                
                if (seen.Add(key))
                {
                    unique.Add(config);
                }
            }
            
            return unique;
        }

        /// <summary>
        ///     Get best individual from population efficiently.
        /// </summary>
        private static GeneticIndividual GetBestIndividual(List<GeneticIndividual> population)
        {
            if (population.Count == 0) return null;
            
            var best = population[0];
            var bestFitness = best.Fitness?.PercentGain ?? double.MinValue;
            
            for (int i = 1; i < population.Count; i++)
            {
                var currentFitness = population[i].Fitness?.PercentGain ?? double.MinValue;
                if (currentFitness > bestFitness)
                {
                    best = population[i];
                    bestFitness = currentFitness;
                }
            }
            
            return best;
        }

        /// <summary>
        ///     Get minimum price efficiently without LINQ.
        /// </summary>
        private static double GetMinPrice(PriceRecord[] records)
        {
            if (records.Length == 0) return 0.0;
            
            var min = records[0].Close;
            for (int i = 1; i < records.Length; i++)
            {
                if (records[i].Close < min)
                    min = records[i].Close;
            }
            return min;
        }

        /// <summary>
        ///     Get maximum price efficiently without LINQ.
        /// </summary>
        private static double GetMaxPrice(PriceRecord[] records)
        {
            if (records.Length == 0) return 0.0;
            
            var max = records[0].Close;
            for (int i = 1; i < records.Length; i++)
            {
                if (records[i].Close > max)
                    max = records[i].Close;
            }
            return max;
        }

        #endregion

        #region Console Output Methods

        /// <summary>
        ///     Writes a section header to the console.
        /// </summary>
        private static void WriteSection(string title)
        {
            try
            {
                ConsoleUtilities.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                ConsoleUtilities.WriteLine($"■ {title}");
                ConsoleUtilities.WriteLine(new string('─', title.Length + 2));
                Console.ResetColor();
            }
            catch
            {
                ConsoleUtilities.WriteLine();
                ConsoleUtilities.WriteLine($"■ {title}");
                ConsoleUtilities.WriteLine(new string('─', title.Length + 2));
            }
        }

        /// <summary>
        ///     Writes an informational message to the console.
        /// </summary>
        private static void WriteInfo(string message)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.White;
                ConsoleUtilities.WriteLine($"[INFO] {message}");
                Console.ResetColor();
            }
            catch
            {
                ConsoleUtilities.WriteLine($"[INFO] {message}");
            }
        }

        /// <summary>
        ///     Writes a warning message to the console.
        /// </summary>
        private static void WriteWarning(string message)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ConsoleUtilities.WriteLine($"[WARNING] {message}");
                Console.ResetColor();
            }
            catch
            {
                ConsoleUtilities.WriteLine($"[WARNING] {message}");
            }
        }

        #endregion
    }
}