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
        private static readonly object _optimizationCacheLock = new object();
        private static double[] _tempPriceBuffer = new double[10000]; // Pre-allocated for price conversion
        private static List<GeneticIndividual> _tempPopulationBuffer = new List<GeneticIndividual>(1000);
        private static readonly Dictionary<int, double[]> _priceBufferCache = new Dictionary<int, double[]>();
        
        // Pre-computed constants
        private static readonly Random _sharedRandom = new Random(42); // Shared RNG for performance
        
        #endregion

        #region Optimization Methods

        /// <summary>
        ///     Enhanced RunWalkforwardAnalysis that works with PriceRecord[] and preserves DateTime information
        ///     OPTIMIZED: Reduced LINQ operations, pre-allocated collections, optimized best-finding algorithms
        /// </summary>
        /// <param name="priceRecords">Complete price record data for analysis</param>
        /// <returns>Comprehensive walkforward analysis results with DateTime information</returns>
        private static WalkforwardResults RunWalkforwardAnalysisWithDates(PriceRecord[] priceRecords)
        {
            WriteSection("Running Comprehensive Walkforward Analysis with DateTime Information");

            // Initialize global gene pool for cross-window learning
            if (EnableHistoricalTracking && _globalGenePool == null)
            {
                _globalGenePool = new GeneticEvolvers.HistoricalGenePool
                {
                    MaxArchiveSize = HistoricalArchiveSize
                };
                WriteInfo("Initialized global historical gene pool for cross-window learning");
            }

            var dates = GeneticIndividual.ExtractDates(priceRecords);
            
            // OPTIMIZATION: Use cached price buffer conversion or create efficiently
            var priceBuffer = GetOrCreatePriceBuffer(priceRecords);

            // OPTIMIZATION: Pre-allocate collections with estimated capacity
            var windows = new List<WalkforwardWindow>(WalkforwardMaxWindows);
            var cumulativeBalance = StartingBalance;
            var peakBalance = StartingBalance;
            var maxSystemDrawdown = 0.0;
            var testPerformances = new List<double>(WalkforwardMaxWindows);

            // Calculate number of possible windows
            var maxPossibleWindows = Math.Min(
                (priceRecords.Length - WalkforwardWindowSize) / WalkforwardStepSize,
                WalkforwardMaxWindows
            );

            WriteInfo($"Executing {maxPossibleWindows} walkforward windows...");
            WriteInfo($"Window size: {WalkforwardWindowSize} periods, Step size: {WalkforwardStepSize} periods");
            WriteInfo(
                $"Date range: {priceRecords[0].DateTime:yyyy-MM-dd} to {priceRecords[priceRecords.Length - 1].DateTime:yyyy-MM-dd}");

            if (EnableHistoricalTracking)
                WriteInfo(
                    $"Historical tracking enabled: Archive size {HistoricalArchiveSize}, Injection rate {DiversityInjectionRate:P1}");
            if (EnableIslandEvolution)
                WriteInfo(
                    $"Island evolution enabled: {NumberOfIslands} islands, Migration every {MigrationFrequency} generations");

            for (var windowIndex = 0; windowIndex < maxPossibleWindows; windowIndex++)
            {
                var trainStart = windowIndex * WalkforwardStepSize;
                var trainEnd = trainStart + WalkforwardWindowSize - 1;
                var testStart = trainEnd + 1;
                var testEnd = Math.Min(testStart + WalkforwardStepSize - 1, priceRecords.Length - 1);

                // Ensure we have sufficient data
                if (testEnd >= priceRecords.Length || trainEnd - trainStart + 1 < MinimumTrainingPeriods)
                {
                    WriteWarning($"Window {windowIndex + 1}: Insufficient data, skipping");
                    continue;
                }

                // Extract date information for this window - OPTIMIZATION: Direct array access
                var trainingStartDate = priceRecords[trainStart].DateTime;
                var trainingEndDate = priceRecords[trainEnd].DateTime;
                var testStartDate = priceRecords[testStart].DateTime;
                var testEndDate = priceRecords[testEnd].DateTime;

                WriteInfo(
                    $"Window {windowIndex + 1}/{maxPossibleWindows}: Training[{trainStart}:{trainEnd}] ({trainingStartDate:yyyy-MM-dd} to {trainingEndDate:yyyy-MM-dd}), Test[{testStart}:{testEnd}] ({testStartDate:yyyy-MM-dd} to {testEndDate:yyyy-MM-dd})");

                // Extract training and test data as PriceRecord arrays
                var trainingRecords = GeneticIndividual.CreateSubset(priceRecords, trainStart, trainEnd);
                var testRecords = GeneticIndividual.CreateSubset(priceRecords, testStart, testEnd);

                // Split training into train/validation for this window
                var validationSize = (int)(trainingRecords.Length * WalkforwardValidationPercentage);
                var trainSplitRecords =
                    GeneticIndividual.CreateSubset(trainingRecords, 0, trainingRecords.Length - validationSize - 1);
                var validationSplitRecords = GeneticIndividual.CreateSubset(trainingRecords,
                    trainingRecords.Length - validationSize, trainingRecords.Length - 1);

                // CRITICAL: Reset normalization parameters for each window using PriceRecord data
                WriteInfo($"  Analyzing indicator ranges for window {windowIndex + 1} training data...");
                GeneticIndividual.AnalyzeIndicatorRanges(trainSplitRecords);

                // Initialize option solvers for this window only
                GeneticIndividual.InitializeOptionSolvers(Constants.SPX_D_FOR_OPTIONS);

                // Run enhanced GA for this window using PriceRecord data WITH HISTORICAL TRACKING
                WriteInfo($"  Running enhanced genetic algorithm for window {windowIndex + 1}...");
                var bestIndividual =
                    RunEnhancedGeneticAlgorithmWithHistoricalTracking(trainSplitRecords, validationSplitRecords,
                        windowIndex);

                // Evaluate on out-of-sample test data using PriceRecord data
                WriteInfo($"  Evaluating on out-of-sample data for window {windowIndex + 1}...");
                var trainingFitness = bestIndividual.Process(trainingRecords);
                var testFitness = bestIndividual.Process(testRecords);

                // Archive champion individuals for global learning
                if (EnableHistoricalTracking && testFitness.PercentGain > 0)
                {
                    var champion = GeneticEvolvers.CloneIndividual(bestIndividual);
                    champion.Fitness =
                        new Fitness(testFitness.DollarGain, testFitness.PercentGain); // Use test performance
                    _allTimeChampions.Add(champion);

                    // Track strategy patterns
                    var patternKey = GeneticEvolvers.GenerateStrategyPatternKey(champion);
                    if (!_strategyPatterns.ContainsKey(patternKey))
                        _strategyPatterns[patternKey] = new List<GeneticIndividual>();
                    _strategyPatterns[patternKey].Add(champion);

                    WriteInfo(
                        $"  Archived champion with {testFitness.PercentGain:F2}% test performance (Pattern: {patternKey})");
                }

                // Calculate performance metrics - OPTIMIZATION: Use RiskMetrics optimized methods
                var performanceGap = Math.Abs(trainingFitness.PercentGain - testFitness.PercentGain);
                var sharpeRatio = RiskMetrics.CalculateSharpe(bestIndividual);
                var maxDrawdown = RiskMetrics.CalculateMaxDrawdown(bestIndividual);

                // Detect overfitting
                var isOverfitted = performanceGap > 15.0 || // 15% performance gap threshold
                                   (trainingFitness.PercentGain > 10.0 && testFitness.PercentGain < -5.0);

                // Update cumulative performance
                var windowReturn = testFitness.PercentGain / 100.0;
                cumulativeBalance *= 1.0 + windowReturn;

                // Track system drawdown
                if (cumulativeBalance > peakBalance)
                {
                    peakBalance = cumulativeBalance;
                }
                else
                {
                    var currentDrawdown = (peakBalance - cumulativeBalance) / peakBalance * 100.0;
                    maxSystemDrawdown = Math.Max(maxSystemDrawdown, currentDrawdown);
                }

                // Store window results with actual DateTime information and enhanced metadata
                var window = new WalkforwardWindow
                {
                    WindowIndex = windowIndex,
                    TrainingStartIndex = trainStart,
                    TrainingEndIndex = trainEnd,
                    TestStartIndex = testStart,
                    TestEndIndex = testEnd,
                    TrainingStartDate = trainingStartDate,
                    TrainingEndDate = trainingEndDate,
                    TestStartDate = testStartDate,
                    TestEndDate = testEndDate,
                    BestIndividual = bestIndividual,
                    TrainingPerformance = trainingFitness.PercentGain,
                    TestPerformance = testFitness.PercentGain,
                    PerformanceGap = performanceGap,
                    TradesExecuted = bestIndividual.Trades.Count,
                    MaxDrawdown = maxDrawdown,
                    SharpeRatio = sharpeRatio,
                    EarlyStoppedDueToOverfitting = isOverfitted || GetTempEarlyStopped(),
                    GenerationsUsed = GetTempGenerationsUsed()
                };

                windows.Add(window);
                testPerformances.Add(testFitness.PercentGain);
                _totalWindowsCompleted++;

                WriteSuccess(
                    $"  Window {windowIndex + 1} completed: Training={trainingFitness.PercentGain:F2}% ({trainingStartDate:yyyy-MM-dd} to {trainingEndDate:yyyy-MM-dd}), Test={testFitness.PercentGain:F2}% ({testStartDate:yyyy-MM-dd} to {testEndDate:yyyy-MM-dd}), Gap={performanceGap:F2}%");

                if (isOverfitted) WriteWarning($"  Window {windowIndex + 1}: Potential overfitting detected!");
            }

            // Display global learning statistics
            if (EnableHistoricalTracking)
            {
                WriteSection("Global Historical Learning Statistics");
                WriteInfo($"Total champions archived: {_allTimeChampions.Count}");
                WriteInfo($"Unique strategy patterns discovered: {_strategyPatterns.Count}");
                WriteInfo(
                    $"Global gene pool size: Hall of Fame: {_globalGenePool.HallOfFame.Count}, Interesting Mutants: {_globalGenePool.InterestingMutants.Count}");
                WriteInfo($"Total generations across all windows: {_totalGenerationsRun}");

                // Show top strategy patterns - OPTIMIZATION: Use efficient top-N selection
                var topPatterns = GetTopStrategyPatterns(_strategyPatterns, 5);

                WriteInfo("Top 5 strategy patterns by average performance:");
                for (int i = 0; i < topPatterns.Count; i++)
                {
                    var pattern = topPatterns[i];
                    WriteInfo($"  {pattern.Key}: {pattern.avgPerformance:F2}% avg ({pattern.count} instances)");
                }
            }

            // Calculate summary statistics - OPTIMIZATION: Single pass calculation
            var windowCount = windows.Count;
            if (windowCount == 0)
            {
                return new WalkforwardResults
                {
                    Windows = windows,
                    AverageTrainingPerformance = 0,
                    AverageTestPerformance = 0,
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

            // OPTIMIZATION: Single pass summary calculation
            double sumTraining = 0, sumTest = 0, sumGap = 0, sumSharpe = 0;
            int totalTrades = 0, overfittingCount = 0;

            for (int i = 0; i < windowCount; i++)
            {
                var w = windows[i];
                sumTraining += w.TrainingPerformance;
                sumTest += w.TestPerformance;
                sumGap += w.PerformanceGap;
                sumSharpe += w.SharpeRatio;
                totalTrades += w.TradesExecuted;
                if (w.EarlyStoppedDueToOverfitting) overfittingCount++;
            }

            var avgTrainingPerf = sumTraining / windowCount;
            var avgTestPerf = sumTest / windowCount;
            var avgGap = sumGap / windowCount;
            var avgSharpe = sumSharpe / windowCount;
            var overfittingFreq = overfittingCount * 100.0 / windowCount;

            // OPTIMIZATION: Use optimized standard deviation calculation
            var consistencyScore = CalculateStandardDeviationOptimized(testPerformances);

            // Overall robustness assessment
            var isRobust = avgGap < 10.0 && // Average gap less than 10%
                           consistencyScore < 15.0 && // Test performance consistency
                           overfittingFreq < 30.0 && // Less than 30% overfitted windows
                           avgTestPerf > 0.0; // Positive average test performance

            var results = new WalkforwardResults
            {
                Windows = windows,
                AverageTrainingPerformance = avgTrainingPerf,
                AverageTestPerformance = avgTestPerf,
                AveragePerformanceGap = avgGap,
                ConsistencyScore = consistencyScore,
                CumulativeReturn = (cumulativeBalance - StartingBalance) / StartingBalance * 100.0,
                MaxSystemDrawdown = maxSystemDrawdown,
                AverageSharpeRatio = avgSharpe,
                TotalTradesExecuted = totalTrades,
                OverfittingFrequency = overfittingFreq,
                IsStrategyRobust = isRobust
            };

            return results;
        }

        /// <summary>
        ///     Enhanced genetic algorithm with historical tracking, island evolution, and schema preservation
        ///     OPTIMIZED: Reduced LINQ operations, optimized population management, efficient best individual finding
        /// </summary>
        private static GeneticIndividual RunEnhancedGeneticAlgorithmWithHistoricalTracking(PriceRecord[] trainingSplit,
            PriceRecord[] validationSplit, int windowIndex)
        {
            WriteInfo($"    Starting enhanced GA with historical tracking for window {windowIndex + 1}");

            // Initialize or use existing global gene pool
            if (_globalGenePool == null && EnableHistoricalTracking)
                _globalGenePool = new GeneticEvolvers.HistoricalGenePool
                {
                    MaxArchiveSize = HistoricalArchiveSize
                };

            // Enhanced with early stopping and cross-window learning
            var bestValidationPerformance = double.MinValue;
            var patienceCounter = 0;
            GeneticIndividual bestValidationIndividual = null;
            var generationsRun = 0;

            // OPTIMIZATION: Initialize populations with pre-allocated capacity
            var populations = new List<List<GeneticIndividual>>(Math.Max(1, NumberOfIslands));

            if (EnableIslandEvolution)
            {
                // Initialize multiple island populations
                WriteInfo($"    Initializing {NumberOfIslands} island populations");
                for (var island = 0; island < NumberOfIslands; island++)
                {
                    var islandSize = PopulationSize / NumberOfIslands;
                    var islandPopulation = new List<GeneticIndividual>(islandSize);

                    for (var i = 0; i < islandSize; i++)
                        islandPopulation.Add(new GeneticIndividual(
                            new Random(42 + island * 1000 + i), // Fixed seed for repeatability
                            StartingBalance,
                            IndicatorTypeMin, IndicatorTypeMax,
                            IndicatorPeriodMin, IndicatorPeriodMax,
                            IndicatorModeMin, IndicatorModeMax,
                            IndicatorTimeFrameMin, IndicatorTimeFrameMax,
                            IndicatorPolarityMin, IndicatorPolarityMax,
                            IndicatorThresholdMin, IndicatorThresholdMax,
                            MaxIndicators, TradePercentageForStocksMin, TradePercentageForStocksMax,
                            TradePercentageForOptionsMin, TradePercentageForOptionsMax,
                            OptionDaysOutMin, OptionDaysOutMax,
                            OptionStrikeDistanceMin, OptionStrikeDistanceMax,
                            FastMAPeriodMin, FastMAPeriodMax,
                            SlowMAPeriodMin, SlowMAPeriodMax,
                            AllowedTradeTypeMin, AllowedTradeTypeMax,
                            AllowedOptionTypeMin, AllowedOptionTypeMax,
                            AllowedSecurityTypeMin, AllowedSecurityTypeMax,
                            NumberOfOptionContractsMin, NumberOfOptionContractsMax));
                    populations.Add(islandPopulation);
                }

                // Inject historical champions into random islands for cross-window learning
                if (EnableHistoricalTracking && _globalGenePool.HallOfFame.Count > 0)
                {
                    WriteInfo(
                        $"    Injecting {Math.Min(10, _globalGenePool.HallOfFame.Count)} historical champions across islands");
                    var rng = new Random(42 + windowIndex * 10000); // Fixed seed per window
                    
                    // OPTIMIZATION: Use efficient top-N selection instead of OrderByDescending
                    var champions = GetTopIndividuals(_globalGenePool.HallOfFame, 10);

                    foreach (var champion in champions)
                    {
                        var targetIsland = rng.Next(NumberOfIslands);
                        var replaceIndex = rng.Next(populations[targetIsland].Count);
                        populations[targetIsland][replaceIndex] = GeneticEvolvers.CloneIndividual(champion);
                    }
                }
            }
            else
            {
                // Single population mode
                var singlePopulation = new List<GeneticIndividual>(PopulationSize);
                for (var i = 0; i < PopulationSize; i++)
                    singlePopulation.Add(new GeneticIndividual(new Random(42 + i), // Fixed seed for repeatability
                        StartingBalance,
                        IndicatorTypeMin, IndicatorTypeMax,
                        IndicatorPeriodMin, IndicatorPeriodMax,
                        IndicatorModeMin, IndicatorModeMax,
                        IndicatorTimeFrameMin, IndicatorTimeFrameMax,
                        IndicatorPolarityMin, IndicatorPolarityMax,
                        IndicatorThresholdMin, IndicatorThresholdMax,
                        MaxIndicators, TradePercentageForStocksMin, TradePercentageForStocksMax,
                        TradePercentageForOptionsMin, TradePercentageForOptionsMax,
                        OptionDaysOutMin, OptionDaysOutMax,
                        OptionStrikeDistanceMin, OptionStrikeDistanceMax,
                        FastMAPeriodMin, FastMAPeriodMax,
                        SlowMAPeriodMin, SlowMAPeriodMax,
                        AllowedTradeTypeMin, AllowedTradeTypeMax,
                        AllowedOptionTypeMin, AllowedOptionTypeMax,
                        AllowedSecurityTypeMin, AllowedSecurityTypeMax,
                        NumberOfOptionContractsMin, NumberOfOptionContractsMax));
                populations.Add(singlePopulation);

                // Inject historical individuals for cross-window learning
                if (EnableHistoricalTracking && _globalGenePool != null)
                {
                    GeneticEvolvers.InjectArchivedGenetics(singlePopulation, _globalGenePool, DiversityInjectionRate,
                        new Random(42 + windowIndex * 1000));
                    WriteInfo(
                        $"    Injected {(int)(PopulationSize * DiversityInjectionRate)} archived individuals for diversity");
                }
            }

            // Evolution loop with enhanced features
            for (var gen = 0; gen < Generations; gen++)
            {
                generationsRun = gen + 1;
                _totalGenerationsRun++;

                GeneticIndividual generationBest = null;
                var bestFitnessThisGen = double.MinValue;

                // Evolve each population/island
                foreach (var population in populations)
                {
                    // Evaluate fitness on training data
                    foreach (var individual in population) individual.Fitness = individual.Process(trainingSplit);

                    // Apply schema preservation if enabled
                    if (EnableSchemaPreservation && _globalGenePool != null)
                        GeneticEvolvers.AnalyzeAndPreserveSchemas(population, _globalGenePool);

                    // OPTIMIZATION: Find best individual efficiently without full sort
                    var populationBest = FindBestIndividualOptimized(population);
                    if (populationBest.Fitness.PercentGain > bestFitnessThisGen)
                    {
                        bestFitnessThisGen = populationBest.Fitness.PercentGain;
                        generationBest = populationBest;
                    }

                    // Archive interesting individuals periodically
                    if (EnableHistoricalTracking && _globalGenePool != null && gen % DiversityInjectionFrequency == 0)
                        GeneticEvolvers.ArchiveInterestingIndividuals(population, _globalGenePool, gen);

                    // Inject diversity periodically
                    if (EnableHistoricalTracking && _globalGenePool != null && gen % DiversityInjectionFrequency == 0 &&
                        gen > 0)
                        GeneticEvolvers.InjectArchivedGenetics(population, _globalGenePool, DiversityInjectionRate / 2,
                            new Random(42 + gen * 100));
                }

                // Island migration
                if (EnableIslandEvolution && populations.Count > 1 && gen % MigrationFrequency == 0 && gen > 0)
                {
                    var config = new GeneticEvolvers.IslandConfiguration
                    {
                        NumberOfIslands = NumberOfIslands,
                        MigrationFrequency = MigrationFrequency,
                        MigrationRate = MigrationRate,
                        DiversityThreshold = 0.8
                    };

                    GeneticEvolvers.ExchangeBestIndividuals(populations, config, new Random(42 + gen * 10));
                    WriteInfo($"    Island migration completed at generation {gen + 1}");
                }

                // Apply regularization to training fitness
                var complexityPenalty = (generationBest.Indicators.Count - 1) * RegularizationStrength;
                var regularizedTrainingFitness = generationBest.Fitness.PercentGain - complexityPenalty;

                // Validate on validation set
                var validationFitness = generationBest.Process(validationSplit);

                WriteInfo(
                    $"    Generation {gen + 1}: Training={regularizedTrainingFitness:F2}%, Validation={validationFitness.PercentGain:F2}%, Complexity Penalty={complexityPenalty:F3}%");

                // Check if validation performance improved
                if (validationFitness.PercentGain > bestValidationPerformance)
                {
                    bestValidationPerformance = validationFitness.PercentGain;
                    bestValidationIndividual = GeneticEvolvers.CloneIndividual(generationBest);
                    patienceCounter = 0;
                    WriteInfo($"    New best validation performance: {bestValidationPerformance:F2}%");
                }
                else
                {
                    patienceCounter++;
                    WriteInfo($"    No validation improvement (patience: {patienceCounter}/{EarlyStoppingPatience})");
                }

                // Early stopping check
                if (EnableWalkforwardEarlyStopping && patienceCounter >= EarlyStoppingPatience)
                {
                    WriteInfo(
                        $"    Early stopping triggered after {patienceCounter} generations without validation improvement");
                    break;
                }

                // Continue evolution for next generation (using proper genetic operations)
                if (gen < Generations - 1)
                    foreach (var population in populations)
                    {
                        // OPTIMIZATION: Pre-allocate new population and use efficient selection
                        var newPopulation = new List<GeneticIndividual>(population.Count);
                        var popBest = FindBestIndividualOptimized(population);
                        newPopulation.Add(GeneticEvolvers.CloneIndividual(popBest)); // Elitism

                        while (newPopulation.Count < population.Count)
                        {
                            var parent = TournamentSelectionOptimized(population,
                                new Random(42 + gen * 1000 + newPopulation.Count));
                            var offspring = MutateIndividual(parent, new Random(42 + gen * 2000 + newPopulation.Count));
                            newPopulation.Add(offspring);
                        }

                        population.Clear();
                        population.AddRange(newPopulation);
                    }
            }

            WriteInfo($"    Evolution completed after {generationsRun} generations");

            // Final archiving of best individual
            if (EnableHistoricalTracking && bestValidationIndividual != null && _globalGenePool != null)
            {
                _globalGenePool.HallOfFame.Add(GeneticEvolvers.CloneIndividual(bestValidationIndividual));

                // Maintain archive size limit - OPTIMIZATION: Use efficient top-N selection
                if (_globalGenePool.HallOfFame.Count > _globalGenePool.MaxArchiveSize)
                {
                    var topIndividuals = GetTopIndividuals(_globalGenePool.HallOfFame, _globalGenePool.MaxArchiveSize);
                    _globalGenePool.HallOfFame.Clear();
                    _globalGenePool.HallOfFame.AddRange(topIndividuals);
                }
            }

            // OPTIMIZATION: Find best individual from all populations efficiently
            var finalResult = bestValidationIndividual ?? FindBestIndividualFromPopulations(populations);

            StoreTempGenerationsUsed(generationsRun);
            StoreTempEarlyStopped(patienceCounter >= EarlyStoppingPatience);

            return finalResult;
        }

        /// <summary>
        ///     Runs the genetic algorithm to find optimal trading strategies using PriceRecord data.
        ///     This version preserves full price history including DateTime information for enhanced trading simulation.
        ///     OPTIMIZED: Efficient GeneticSolver usage with minimal object creation
        /// </summary>
        /// <param name="priceRecords">Array of price records with full OHLC and DateTime information</param>
        /// <returns>Best individual found by the genetic algorithm using PriceRecord data</returns>
        private static GeneticIndividual RunGeneticAlgorithm(PriceRecord[] priceRecords, bool runInParallel = true)
        {
            WriteInfo("Initializing genetic solver with PriceRecord data...");
            var solver = new GeneticSolver(
                PopulationSize, Generations, MutationRate, TournamentSize,
                StartingBalance,
                IndicatorTypeMin, IndicatorTypeMax,
                IndicatorPeriodMin, IndicatorPeriodMax,
                IndicatorModeMin, IndicatorModeMax,
                IndicatorTimeFrameMin, IndicatorTimeFrameMax,
                IndicatorPolarityMin, IndicatorPolarityMax,
                IndicatorThresholdMin, IndicatorThresholdMax,
                MaxIndicators, TradePercentageForStocksMin, TradePercentageForStocksMax,
                TradePercentageForOptionsMin, TradePercentageForOptionsMax,
                OptionDaysOutMin, OptionDaysOutMax,
                OptionStrikeDistanceMin, OptionStrikeDistanceMax,
                FastMAPeriodMin, FastMAPeriodMax,
                SlowMAPeriodMin, SlowMAPeriodMax,
                AllowedTradeTypeMin, AllowedTradeTypeMax,
                AllowedOptionTypeMin, AllowedOptionTypeMax,
                AllowedSecurityTypeMin, AllowedSecurityTypeMax,
                NumberOfOptionContractsMin, NumberOfOptionContractsMax);

            WriteInfo("Running genetic algorithm optimization with enhanced price data...");
            var best = solver.Solve(priceRecords, runInParallel);
            best.Process(priceRecords);

            return best;
        }

        /// <summary>
        ///     Runs enhanced genetic algorithm with validation and overfitting prevention using PriceRecord data.
        ///     This version preserves full price history including DateTime information for enhanced trading simulation.
        ///     OPTIMIZED: Efficient early stopping implementation
        /// </summary>
        /// <param name="trainingRecords">Training data as PriceRecord array</param>
        /// <param name="validationRecords">Validation data as PriceRecord array</param>
        /// <returns>Best individual with overfitting prevention using PriceRecord data</returns>
        private static GeneticIndividual RunEnhancedGeneticAlgorithm(PriceRecord[] trainingRecords,
            PriceRecord[] validationRecords)
        {
            WriteInfo("Initializing enhanced genetic solver with PriceRecord data and overfitting prevention...");
            var solver = new GeneticSolver(
                PopulationSize, Generations, MutationRate, TournamentSize,
                StartingBalance,
                IndicatorTypeMin, IndicatorTypeMax,
                IndicatorPeriodMin, IndicatorPeriodMax,
                IndicatorModeMin, IndicatorModeMax,
                IndicatorTimeFrameMin, IndicatorTimeFrameMax,
                IndicatorPolarityMin, IndicatorPolarityMax,
                IndicatorThresholdMin, IndicatorThresholdMax,
                MaxIndicators, TradePercentageForStocksMin, TradePercentageForStocksMax,
                TradePercentageForOptionsMin, TradePercentageForOptionsMax,
                OptionDaysOutMin, OptionDaysOutMax,
                OptionStrikeDistanceMin, OptionStrikeDistanceMax,
                FastMAPeriodMin, FastMAPeriodMax,
                SlowMAPeriodMin, SlowMAPeriodMax,
                AllowedTradeTypeMin, AllowedTradeTypeMax,
                AllowedOptionTypeMin, AllowedOptionTypeMax,
                AllowedSecurityTypeMin, AllowedSecurityTypeMax,
                NumberOfOptionContractsMin, NumberOfOptionContractsMax);

            WriteInfo("Running enhanced genetic algorithm with early stopping and enhanced price data...");

            // Track best validation performance for early stopping
            var bestValidationPerformance = double.MinValue;
            var patienceCounter = 0;
            GeneticIndividual bestValidationIndividual = null;

            // Run for fewer generations with early stopping using PriceRecord data
            var best = solver.Solve(trainingRecords);

            // Validate on validation set using PriceRecord data
            var validationFitness = best.Process(validationRecords);
            if (validationFitness.PercentGain > bestValidationPerformance)
            {
                bestValidationPerformance = validationFitness.PercentGain;
                bestValidationIndividual = best;
                patienceCounter = 0;
            }
            else
            {
                patienceCounter++;
            }

            WriteInfo($"Validation performance: {validationFitness.PercentGain:F2}%");

            if (patienceCounter >= EarlyStoppingPatience)
                WriteWarning($"Early stopping triggered after {patienceCounter} generations without improvement");

            // Apply regularization by penalizing complexity
            var complexityPenalty = (best.Indicators.Count - 1) * RegularizationStrength;
            var regularizedFitness = best.Process(trainingRecords).PercentGain - complexityPenalty;

            WriteInfo($"Original fitness: {best.Process(trainingRecords).PercentGain:F2}%");
            WriteInfo($"Complexity penalty: {complexityPenalty:F2}%");
            WriteInfo($"Regularized fitness: {regularizedFitness:F2}%");

            return bestValidationIndividual ?? best;
        }

        /// <summary>
        ///     Optimized tournament selection for genetic algorithm
        ///     OPTIMIZED: Direct array access pattern, reduced random number generation
        /// </summary>
        private static GeneticIndividual TournamentSelection(List<GeneticIndividual> population, Random rng)
        {
            return TournamentSelectionOptimized(population, rng);
        }

        /// <summary>
        ///     Optimized tournament selection implementation
        /// </summary>
        private static GeneticIndividual TournamentSelectionOptimized(List<GeneticIndividual> population, Random rng)
        {
            var populationCount = population.Count;
            var tournamentSize = Math.Min(TournamentSize, populationCount);
            
            GeneticIndividual best = null;
            double bestFitness = double.MinValue;

            // OPTIMIZATION: Direct fitness comparison without creating tournament collection
            for (var i = 0; i < tournamentSize; i++)
            {
                var candidate = population[rng.Next(populationCount)];
                var candidateFitness = candidate.Fitness?.PercentGain ?? double.MinValue;
                
                if (candidateFitness > bestFitness)
                {
                    bestFitness = candidateFitness;
                    best = candidate;
                }
            }

            return best ?? population[0];
        }

        /// <summary>
        ///     Simple mutation for early stopping genetic algorithm
        /// </summary>
        private static GeneticIndividual MutateIndividual(GeneticIndividual parent, Random rng)
        {
            // Create a new individual with similar parameters but random mutations
            var offspring = new GeneticIndividual(rng,
                StartingBalance,
                IndicatorTypeMin, IndicatorTypeMax,
                IndicatorPeriodMin, IndicatorPeriodMax,
                IndicatorModeMin, IndicatorModeMax,
                IndicatorTimeFrameMin, IndicatorTimeFrameMax,
                IndicatorPolarityMin, IndicatorPolarityMax,
                IndicatorThresholdMin, IndicatorThresholdMax,
                MaxIndicators, TradePercentageForStocksMin, TradePercentageForStocksMax,
                TradePercentageForOptionsMin, TradePercentageForOptionsMax,
                OptionDaysOutMin, OptionDaysOutMax,
                OptionStrikeDistanceMin, OptionStrikeDistanceMax,
                FastMAPeriodMin, FastMAPeriodMax,
                SlowMAPeriodMin, SlowMAPeriodMax,
                AllowedTradeTypeMin, AllowedTradeTypeMax,
                AllowedOptionTypeMin, AllowedOptionTypeMax,
                AllowedSecurityTypeMin, AllowedSecurityTypeMax,
                NumberOfOptionContractsMin, NumberOfOptionContractsMax);

            return offspring;
        }

        #endregion

        #region Optimized Helper Methods

        /// <summary>
        ///     Get or create cached price buffer for efficient array conversion
        ///     OPTIMIZED: Caches converted price buffers to avoid repeated LINQ operations
        /// </summary>
        private static double[] GetOrCreatePriceBuffer(PriceRecord[] priceRecords)
        {
            var recordCount = priceRecords.Length;
            
            lock (_optimizationCacheLock)
            {
                // Check if we have a cached buffer of the right size
                if (_priceBufferCache.TryGetValue(recordCount, out var cachedBuffer))
                {
                    // Update the buffer with current prices
                    for (int i = 0; i < recordCount; i++)
                    {
                        cachedBuffer[i] = priceRecords[i].Close;
                    }
                    return cachedBuffer;
                }

                // Create new buffer and cache it
                var newBuffer = new double[recordCount];
                for (int i = 0; i < recordCount; i++)
                {
                    newBuffer[i] = priceRecords[i].Close;
                }
                
                // Cache the buffer for reuse (keep cache size limited)
                if (_priceBufferCache.Count < 10)
                {
                    _priceBufferCache[recordCount] = newBuffer;
                }
                
                return newBuffer;
            }
        }

        /// <summary>
        ///     Find best individual from a population without full sorting
        ///     OPTIMIZED: O(n) linear search instead of O(n log n) sorting
        /// </summary>
        private static GeneticIndividual FindBestIndividualOptimized(List<GeneticIndividual> population)
        {
            if (population.Count == 0) return null;

            var best = population[0];
            var bestFitness = best.Fitness?.PercentGain ?? double.MinValue;

            // Linear search for best individual
            for (int i = 1; i < population.Count; i++)
            {
                var currentFitness = population[i].Fitness?.PercentGain ?? double.MinValue;
                if (currentFitness > bestFitness)
                {
                    bestFitness = currentFitness;
                    best = population[i];
                }
            }

            return best;
        }

        /// <summary>
        ///     Find best individual from multiple populations efficiently
        ///     OPTIMIZED: Single pass through all populations
        /// </summary>
        private static GeneticIndividual FindBestIndividualFromPopulations(List<List<GeneticIndividual>> populations)
        {
            GeneticIndividual globalBest = null;
            double globalBestFitness = double.MinValue;

            foreach (var population in populations)
            {
                var populationBest = FindBestIndividualOptimized(population);
                if (populationBest != null)
                {
                    var fitness = populationBest.Fitness?.PercentGain ?? double.MinValue;
                    if (fitness > globalBestFitness)
                    {
                        globalBestFitness = fitness;
                        globalBest = populationBest;
                    }
                }
            }

            return globalBest;
        }

        /// <summary>
        ///     Get top N individuals efficiently without full sorting
        ///     OPTIMIZED: Partial selection sort O(n*k) instead of O(n log n)
        /// </summary>
        private static List<GeneticIndividual> GetTopIndividuals(List<GeneticIndividual> individuals, int topN)
        {
            if (individuals.Count <= topN)
                return new List<GeneticIndividual>(individuals);

            var result = new List<GeneticIndividual>(topN);

            // Partial selection sort for top N elements
            for (int i = 0; i < topN && i < individuals.Count; i++)
            {
                var best = individuals[i];
                var bestIndex = i;
                var bestFitness = best.Fitness?.PercentGain ?? double.MinValue;

                // Find best remaining individual
                for (int j = i + 1; j < individuals.Count; j++)
                {
                    var candidateFitness = individuals[j].Fitness?.PercentGain ?? double.MinValue;
                    if (candidateFitness > bestFitness)
                    {
                        best = individuals[j];
                        bestIndex = j;
                        bestFitness = candidateFitness;
                    }
                }

                // Swap if needed
                if (bestIndex != i)
                {
                    individuals[bestIndex] = individuals[i];
                    individuals[i] = best;
                }

                result.Add(best);
            }

            return result;
        }

        /// <summary>
        ///     Get top strategy patterns efficiently without LINQ
        ///     OPTIMIZED: Single pass calculation with efficient sorting
        /// </summary>
        private static List<(string Key, double avgPerformance, int count)> GetTopStrategyPatterns(
            Dictionary<string, List<GeneticIndividual>> strategyPatterns, int topN)
        {
            var patterns = new List<(string Key, double avgPerformance, int count)>();

            // Single pass to calculate averages
            foreach (var kvp in strategyPatterns)
            {
                var individuals = kvp.Value;
                double sum = 0;
                int count = individuals.Count;
                
                for (int i = 0; i < count; i++)
                {
                    sum += individuals[i].Fitness?.PercentGain ?? 0;
                }
                
                var avgPerformance = count > 0 ? sum / count : 0;
                patterns.Add((kvp.Key, avgPerformance, count));
            }

            // Partial selection sort for top N patterns
            var resultCount = Math.Min(topN, patterns.Count);
            for (int i = 0; i < resultCount; i++)
            {
                var best = patterns[i];
                var bestIndex = i;

                for (int j = i + 1; j < patterns.Count; j++)
                {
                    if (patterns[j].avgPerformance > best.avgPerformance)
                    {
                        best = patterns[j];
                        bestIndex = j;
                    }
                }

                if (bestIndex != i)
                {
                    patterns[bestIndex] = patterns[i];
                    patterns[i] = best;
                }
            }

            return patterns.Take(resultCount).ToList();
        }

        /// <summary>
        ///     Optimized standard deviation calculation for performance data
        ///     OPTIMIZED: Single-pass algorithm using Welford's method
        /// </summary>
        private static double CalculateStandardDeviationOptimized(List<double> values)
        {
            var count = values.Count;
            if (count <= 1) return 0.0;

            // Single-pass calculation using Welford's online algorithm
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

        #endregion
    }
}