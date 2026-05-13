using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Trade.Prices2;

namespace Trade
{
    public class GeneticSolver
    {
        #region Constructor

        // Back-compat constructor: single trade% bounds used for both stocks and options
        public GeneticSolver(int populationSize, int generations, double mutationRate, int tournamentSize,
            double startingBalance,
            int indicatorTypeMin, int indicatorTypeMax,
            int indicatorPeriodMin, int indicatorPeriodMax,
            int indicatorModeMin, int indicatorModeMax,
            TimeFrame indicatorTimeFrameMin, TimeFrame indicatorTimeFrameMax,
            int indicatorPolarityMin, int indicatorPolarityMax,
            double indicatorThresholdMin, double indicatorThresholdMax,
            int maxIndicators,
            double tradePercentageMin, double tradePercentageMax,
            int optionDaysOutMin, int optionDaysOutMax,
            int optionStrikeDistanceMin, int optionStrikeDistanceMax,
            int fastMAPeriodMin, int fastMAPeriodMax,
            int slowMAPeriodMin, int slowMAPeriodMax,
            int allowedTradeTypeMin, int allowedTradeTypeMax,
            int allowedOptionTypeMin, int allowedOptionTypeMax,
            int allowedSecurityTypeMin, int allowedSecurityTypeMax,
            int numberOfOptionContractsMin, int numberOfOptionContractsMax)
            : this(populationSize, generations, mutationRate, tournamentSize, startingBalance,
                indicatorTypeMin, indicatorTypeMax,
                indicatorPeriodMin, indicatorPeriodMax,
                indicatorModeMin, indicatorModeMax,
                indicatorTimeFrameMin, indicatorTimeFrameMax,
                indicatorPolarityMin, indicatorPolarityMax,
                indicatorThresholdMin, indicatorThresholdMax,
                maxIndicators,
                // Forward same bounds for both stocks and options for compatibility
                tradePercentageMin, tradePercentageMax,
                tradePercentageMin, tradePercentageMax,
                optionDaysOutMin, optionDaysOutMax,
                optionStrikeDistanceMin, optionStrikeDistanceMax,
                fastMAPeriodMin, fastMAPeriodMax,
                slowMAPeriodMin, slowMAPeriodMax,
                allowedTradeTypeMin, allowedTradeTypeMax,
                allowedOptionTypeMin, allowedOptionTypeMax,
                allowedSecurityTypeMin, allowedSecurityTypeMax,
                numberOfOptionContractsMin, numberOfOptionContractsMax)
        {
        }

        public GeneticSolver(int populationSize, int generations, double mutationRate, int tournamentSize,
            double startingBalance,
            int indicatorTypeMin, int indicatorTypeMax,
            int indicatorPeriodMin, int indicatorPeriodMax,
            int indicatorModeMin, int indicatorModeMax,
            TimeFrame indicatorTimeFrameMin, TimeFrame indicatorTimeFrameMax,
            int indicatorPolarityMin, int indicatorPolarityMax,
            double indicatorThresholdMin, double indicatorThresholdMax,
            int maxIndicators,
            // Split trade percentage bounds per security type
            double tradePercentageStocksMin, double tradePercentageStocksMax,
            double tradePercentageOptionsMin, double tradePercentageOptionsMax,
            int optionDaysOutMin, int optionDaysOutMax,
            int optionStrikeDistanceMin, int optionStrikeDistanceMax,
            int fastMAPeriodMin, int fastMAPeriodMax,
            int slowMAPeriodMin, int slowMAPeriodMax,
            int allowedTradeTypeMin, int allowedTradeTypeMax,
            int allowedOptionTypeMin, int allowedOptionTypeMax,
            int allowedSecurityTypeMin, int allowedSecurityTypeMax,
            int numberOfOptionContractsMin, int numberOfOptionContractsMax)
        {
            _populationSize = populationSize;
            _generations = generations;
            _mutationRate = mutationRate;
            _tournamentSize = tournamentSize;
            _randomNumberGenerator = new Random(42); // FIXED SEED FOR REPEATABILITY
            _startingBalance = startingBalance;
            _indicatorTypeMin = indicatorTypeMin;
            _indicatorTypeMax = indicatorTypeMax;
            _indicatorPeriodMin = indicatorPeriodMin;
            _indicatorPeriodMax = indicatorPeriodMax;
            _indicatorModeMin = indicatorModeMin;
            _indicatorModeMax = indicatorModeMax;
            _indicatorTimeFrameMin = indicatorTimeFrameMin;
            _indicatorTimeFrameMax = indicatorTimeFrameMax;
            _indicatorPolarityMin = indicatorPolarityMin;
            _indicatorPolarityMax = indicatorPolarityMax;
            _indicatorThresholdMin = indicatorThresholdMin;
            _indicatorThresholdMax = indicatorThresholdMax;
            _maxIndicators = maxIndicators;
            _tradePercentageStocksMin = tradePercentageStocksMin;
            _tradePercentageStocksMax = tradePercentageStocksMax;
            _tradePercentageOptionsMin = tradePercentageOptionsMin;
            _tradePercentageOptionsMax = tradePercentageOptionsMax;
            _optionDaysOutMin = optionDaysOutMin;
            _optionDaysOutMax = optionDaysOutMax;
            _optionStrikeDistanceMin = optionStrikeDistanceMin;
            _optionStrikeDistanceMax = optionStrikeDistanceMax;
            _fastMAPeriodMin = fastMAPeriodMin;
            _fastMAPeriodMax = fastMAPeriodMax;
            _slowMAPeriodMin = slowMAPeriodMin;
            _slowMAPeriodMax = slowMAPeriodMax;
            // NEW: Genetic parameter constraints
            _allowedTradeTypeMin = allowedTradeTypeMin;
            _allowedTradeTypeMax = allowedTradeTypeMax;
            _allowedOptionTypeMin = allowedOptionTypeMin;
            _allowedOptionTypeMax = allowedOptionTypeMax;
            _allowedSecurityTypeMin = allowedSecurityTypeMin;
            _allowedSecurityTypeMax = allowedSecurityTypeMax;
            _numberOfOptionContractsMin = numberOfOptionContractsMin;
            _numberOfOptionContractsMax = numberOfOptionContractsMax;
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///     Overloaded Solve method that accepts PriceRecord[] for enhanced DateTime-aware trading simulation.
        ///     This method preserves all historical price information including dates for more accurate option pricing.
        /// </summary>
        public GeneticIndividual Solve(PriceRecord[] priceRecords, bool runInParallel = true)
        {
            _validationRecords = null; // disable validation-aware selection
            if (Program.EnableIntraTrainingCV)
                return SolveWithInnerTimeSeriesCv(priceRecords, runInParallel);
            return SolveInternal(priceRecords, runInParallel);
        }

        /// <summary>
        ///     Validation-aware Solve overload: selection score leverages validation performance and regularization.
        /// </summary>
        /// <param name="trainingRecords">Training data</param>
        /// <param name="validationRecords">Validation data (used only for selection scoring)</param>
        /// <param name="runInParallel">Run evaluation in parallel</param>
        public GeneticIndividual Solve(PriceRecord[] trainingRecords, PriceRecord[] validationRecords, bool runInParallel)
        {
            _validationRecords = validationRecords;
            if (Program.EnableIntraTrainingCV)
                return SolveWithInnerTimeSeriesCv(trainingRecords, runInParallel);
            return SolveInternal(trainingRecords, runInParallel);
        }

        /// <summary>
        /// Simplified genetic algorithm for easy mode - uses basic tournament selection and PercentGain only
        /// </summary>
        private GeneticIndividual SolveInternalEasyMode(PriceRecord[] priceRecords, bool runInParallel)
        {
            var stopwatch = Stopwatch.StartNew();

            // Configure parallel options
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, runInParallel ? Environment.ProcessorCount - 1 : 1)
            };
            //parallelOptions.MaxDegreeOfParallelism = 1;

            // Initialize population
            var population = new List<GeneticIndividual>();
            for (var individualIndex = 0; individualIndex < _populationSize; individualIndex++)
                population.Add(new GeneticIndividual(_randomNumberGenerator,
                    _startingBalance,
                    _indicatorTypeMin, _indicatorTypeMax,
                    _indicatorPeriodMin, _indicatorPeriodMax,
                    _indicatorModeMin, _indicatorModeMax,
                    _indicatorTimeFrameMin, _indicatorTimeFrameMax,
                    _indicatorPolarityMin, _indicatorPolarityMax,
                    _indicatorThresholdMin, _indicatorThresholdMax,
                    _maxIndicators,
                    _tradePercentageStocksMin, _tradePercentageStocksMax,
                    _tradePercentageOptionsMin, _tradePercentageOptionsMax,
                    _optionDaysOutMin, _optionDaysOutMax,
                    _optionStrikeDistanceMin, _optionStrikeDistanceMax,
                    _fastMAPeriodMin, _fastMAPeriodMax,
                    _slowMAPeriodMin, _slowMAPeriodMax,
                    _allowedTradeTypeMin, _allowedTradeTypeMax,
                    _allowedOptionTypeMin, _allowedOptionTypeMax,
                    _allowedSecurityTypeMin, _allowedSecurityTypeMax,
                    _numberOfOptionContractsMin, _numberOfOptionContractsMax));

            GeneticIndividual best = null;
            GeneticIndividual bestUncloned = null;
            var bestPercentGain = double.NegativeInfinity;

            for (var generation = 0; generation < _generations; generation++)
            {
                // Simple fitness evaluation - just use PercentGain
                Parallel.ForEach(population, parallelOptions, individual =>
                {
                    individual.Fitness = individual.Process(priceRecords, individual);
                });

                // Find best individual by PercentGain only
                var generationBest = population.OrderByDescending(ind => ind.Fitness.PercentGain).First();

                if (best == null || generationBest.Fitness.PercentGain > bestPercentGain)
                {
                    bestPercentGain = generationBest.Fitness.PercentGain;
                    bestUncloned = generationBest;
                    best = Clone(generationBest);
                }

                // Simple progress reporting
                var avgPercentGain = population.Average(ind => ind.Fitness.PercentGain);
                ConsoleUtilities.WriteLine(
                    $"Generation {generation + 1}/{_generations}: Best={bestPercentGain:F2}%, Avg={avgPercentGain:F2}%, Trades={generationBest.Trades.Count}");

                // Early exit for very good performance
                if (bestPercentGain >= 1000.0) // 1000% gain
                    break;

                // Create new generation using simple tournament selection and crossover
                var newPopulation = new List<GeneticIndividual>();

                // Elitism: Keep best individual
                newPopulation.Add(Clone(generationBest));

                // Fill rest of population with tournament selection + crossover
                while (newPopulation.Count < _populationSize)
                {
                    var parent1 = EasyTournamentSelect(population);
                    var parent2 = EasyTournamentSelect(population);

                    var offspring = Crossover(parent1, parent2);
                    Mutate(offspring);
                    newPopulation.Add(offspring);
                }

                population = newPopulation;
            }

            stopwatch.Stop();
            ConsoleUtilities.WriteLine(
                $"Easy Mode GeneticSolver completed in {stopwatch.ElapsedMilliseconds:N0} ms. " +
                $"Best PercentGain: {bestPercentGain:F2}%, Trades: {(best != null ? best.Trades.Count : 0)}"
            );

            return bestUncloned;
        }

        /// <summary>
        /// Simple tournament selection using only PercentGain for fitness
        /// </summary>
        private GeneticIndividual EasyTournamentSelect(List<GeneticIndividual> population)
        {
            var tournamentSize = Math.Min(_tournamentSize, population.Count);
            var tournament = new List<GeneticIndividual>();

            // Select random individuals for tournament
            for (var i = 0; i < tournamentSize; i++)
            {
                tournament.Add(population[_randomNumberGenerator.Next(population.Count)]);
            }

            // Return individual with highest PercentGain
            return tournament.OrderByDescending(ind => ind.Fitness.PercentGain).First();
        }

        private GeneticIndividual SolveInternal(PriceRecord[] priceRecords, bool runInParallel)
        {
            // Use Easy Mode if SIMPLE_MODE is enabled
            if (Program.SIMPLE_MODE)
            {
                return SolveInternalEasyMode(priceRecords, runInParallel);
            }

            var stopwatch = Stopwatch.StartNew();

            // Configure parallel options for optimal performance
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, runInParallel ? Environment.ProcessorCount - 1 : 1) // Leave one core free
            };
            //parallelOptions.MaxDegreeOfParallelism = 1; // FOR DEBUGGING, FORCE SINGLE THREAD. 

            // Initialize population
            var population = new List<GeneticIndividual>();
            for (var individualIndex = 0; individualIndex < _populationSize; individualIndex++)
                population.Add(new GeneticIndividual(_randomNumberGenerator,
                    _startingBalance,
                    _indicatorTypeMin, _indicatorTypeMax,
                    _indicatorPeriodMin, _indicatorPeriodMax,
                    _indicatorModeMin, _indicatorModeMax,
                    _indicatorTimeFrameMin, _indicatorTimeFrameMax,
                    _indicatorPolarityMin, _indicatorPolarityMax,
                    _indicatorThresholdMin, _indicatorThresholdMax,
                    _maxIndicators,
                    // pass both stock/option trade% bounds
                    _tradePercentageStocksMin, _tradePercentageStocksMax,
                    _tradePercentageOptionsMin, _tradePercentageOptionsMax,
                    _optionDaysOutMin, _optionDaysOutMax,
                    _optionStrikeDistanceMin, _optionStrikeDistanceMax,
                    _fastMAPeriodMin, _fastMAPeriodMax,
                    _slowMAPeriodMin, _slowMAPeriodMax,
                    // NEW: Genetic parameter constraints
                    _allowedTradeTypeMin, _allowedTradeTypeMax,
                    _allowedOptionTypeMin, _allowedOptionTypeMax,
                    _allowedSecurityTypeMin, _allowedSecurityTypeMax,
                    _numberOfOptionContractsMin, _numberOfOptionContractsMax));

            GeneticIndividual best = null;
            var bestFitness = double.NegativeInfinity;

            // Early stopping controls
            var noImprovement = 0;
            const int patience = 10;

            for (var generation = 0; generation < _generations; generation++)
            {
                // Calculate adaptive selection pressure (starts low, increases over time)
                var selectionPressure = CalculateAdaptiveSelectionPressure(generation, _generations);

                var evaluationStopwatch = Stopwatch.StartNew();

                // Evaluate on training
                Parallel.ForEach(population, parallelOptions, individual =>
                {
                    individual.Fitness = individual.Process(priceRecords, individual);
                });

                ConsoleUtilities.WriteLine("ForEach > Process > Fitness: " + evaluationStopwatch.Elapsed);
                evaluationStopwatch.Restart();

                // Compute selection scores using robust criteria
                // raw training fitness
                var trainingFitness = population.ToDictionary(ind => ind,
                ind => ind.Fitness.FitnessScore.GetValueOrDefault(0.0));

                // Optional validation fitness (do not mutate individual state for training results)
                Dictionary<GeneticIndividual, double> validationFitness = null;
                if (_validationRecords != null)
                {
                    validationFitness = new Dictionary<GeneticIndividual, double>();
                    foreach (var ind in population)
                    {
                        var v = ind.Process(_validationRecords);
                        validationFitness[ind] = v.FitnessScore.GetValueOrDefault(0.0);
                    }
                    // After validation pass, we leave individual's Fitness as last Process result (validation). Restore training score below when needed.
                }

                // Build penalized originalFitness for robust selection
                var originalFitness = new Dictionary<GeneticIndividual, double>();
                foreach (var ind in population)
                {
                    // Restore training FitnessScore for tracking/log consistency if validation was computed
                    if (_validationRecords != null)
                    {
                        // We won't re-run training here; use cached trainingFitness as base and keep Percent/Dollar from last run acceptable for logs
                    }

                    var train = trainingFitness[ind];
                    var valid = validationFitness != null && validationFitness.ContainsKey(ind)
                        ? validationFitness[ind]
                        : (double?)null;

                    var selectionScore = ComputeSelectionScore(ind, train, valid, priceRecords.Length);
                    originalFitness[ind] = selectionScore;
                }

                // Apply fitness sharing to promote diversity (but preserve original fitness for ranking)
                ApplyFitnessSharing(population);

                // Track best (using robust score)
                var eligible = population.Where(ind => !double.IsNegativeInfinity(originalFitness[ind]) && !double.IsNaN(originalFitness[ind])).ToList();
                GeneticIndividual generationBest;
                double generationBestScore;
                if (eligible.Count > 0)
                {
                    generationBest = eligible.OrderByDescending(ind => originalFitness[ind]).First();
                    generationBestScore = originalFitness[generationBest];
                }
                else
                {
                    // Fallback: pick the least-bad individual by raw training PercentGain to keep algorithm progressing
                    generationBest = population.OrderByDescending(ind => ind.Fitness.PercentGain).First();
                    generationBestScore = double.NegativeInfinity;
                }

                if (best == null || generationBestScore >= bestFitness)
                {
                    bestFitness = generationBestScore;
                    best = Clone(generationBest);
                    // Use generationBest's real performance metrics
                    best.Fitness = new Fitness(
                        generationBest.Fitness.DollarGain,
                        generationBest.Fitness.PercentGain,
                        bestFitness);
                    noImprovement = 0;
                }
                else
                {
                    noImprovement++;
                    if (noImprovement >= patience)
                    {
                        ConsoleUtilities.WriteLine($"Early stopping: no improvement for {patience} generations");
                        break;
                    }
                }

                // Enhanced progress reporting with diversity metrics
                var avgFitness = originalFitness.Values.Where(v => !double.IsNegativeInfinity(v)).DefaultIfEmpty(0.0)
                    .Average();
                var diversityScore = CalculatePopulationDiversity(population);
                ConsoleUtilities.WriteLine(
                    $"Generation {generation + 1}/{_generations}: Best={bestFitness:F4}, Avg={avgFitness:F4}, Percentage={generationBest.Fitness.PercentGain}, Diversity={diversityScore:F3}, Pressure={selectionPressure:F2}, Trades={generationBest.Trades.Count}");

                if (best != null && best.Fitness.DollarGain >= 100_000_000)
                    break;

                // STABLE RANK-BASED SELECTION AND CROSSOVER
                var newPopulation = new List<GeneticIndividual>();

                // Elitism: Keep best individual unchanged (restore original fitness)
                var eliteIndividual = Clone(generationBest);
                eliteIndividual.Fitness = new Fitness(
                    generationBest.Fitness.DollarGain,
                    generationBest.Fitness.PercentGain,
                    generationBestScore);
                newPopulation.Add(eliteIndividual);

                // Create rank-based selection probabilities for stable selection
                var rankedPopulation = CreateRankedSelectionProbabilities(population, originalFitness);

                while (newPopulation.Count < _populationSize)
                {
                    var parent1 = _useRankBasedSelection
                        ? RankBasedSelect(rankedPopulation)
                        : AdaptiveTournamentSelect(population, selectionPressure);
                    var parent2 = _useRankBasedSelection
                        ? RankBasedSelect(rankedPopulation)
                        : AdaptiveTournamentSelect(population, selectionPressure);

                    var offspring = Crossover(parent1, parent2);
                    Mutate(offspring);
                    newPopulation.Add(offspring);
                }

                population = newPopulation;

                ConsoleUtilities.WriteLine("PopulationControl: " + evaluationStopwatch.Elapsed);
            }

            stopwatch.Stop();
            ConsoleUtilities.WriteLine(
                $"GeneticSolver completed in {stopwatch.ElapsedMilliseconds:N0} ms. " +
                $"Best fitness: {bestFitness:F4}, Trades: {(best != null ? best.Trades.Count : 0)}"
            );

            return best;
        }

        // Wrapper used by CV path to fallback to existing pipeline
        private GeneticIndividual SolveWithoutInnerCv(PriceRecord[] priceRecords, bool runInParallel)
        {
            return SolveInternal(priceRecords, runInParallel);
        }

        #endregion

        #region Private Fields

        private readonly int _populationSize;
        private readonly int _generations;
        private readonly double _mutationRate;
        private readonly int _tournamentSize;
        private readonly Random _randomNumberGenerator;

        // Centralized parameters
        private readonly double _startingBalance;
        private readonly int _indicatorTypeMin, _indicatorTypeMax;
        private readonly int _indicatorPeriodMin, _indicatorPeriodMax;
        private readonly int _indicatorModeMin, _indicatorModeMax;
        private readonly TimeFrame _indicatorTimeFrameMin, _indicatorTimeFrameMax;
        private readonly int _indicatorPolarityMin, _indicatorPolarityMax;
        private readonly double _indicatorThresholdMin, _indicatorThresholdMax;
        private readonly int _maxIndicators;
        // Split trade percentage bounds
        private readonly double _tradePercentageStocksMin, _tradePercentageStocksMax;
        private readonly double _tradePercentageOptionsMin, _tradePercentageOptionsMax;
        private readonly int _optionDaysOutMin, _optionDaysOutMax;
        private readonly int _optionStrikeDistanceMin, _optionStrikeDistanceMax;
        private readonly int _fastMAPeriodMin, _fastMAPeriodMax;
        private readonly int _slowMAPeriodMin, _slowMAPeriodMax;

        // NEW: Genetic parameter constraints
        private readonly int _allowedTradeTypeMin, _allowedTradeTypeMax;
        private readonly int _allowedOptionTypeMin, _allowedOptionTypeMax;
        private readonly int _allowedSecurityTypeMin, _allowedSecurityTypeMax;
        private readonly int _numberOfOptionContractsMin, _numberOfOptionContractsMax;

        // Enhanced GA parameters for diversity management
        private readonly double _diversityThreshold = 0.7; // Similarity threshold for fitness sharing
        private readonly double _minSelectionPressure = 0.3; // Start with low pressure
        private readonly double _maxSelectionPressure = 0.9; // End with high pressure

        // STABLE SELECTION PARAMETERS
        private readonly bool _useRankBasedSelection = true; // Enable rank-based selection for stability

        private readonly double _selectionPressureBase = 1.5; // Linear ranking parameter (1.1-2.0, higher = more pressure)

        // Validation records if provided
        private PriceRecord[] _validationRecords;

        // Anti-overfitting thresholds
        private const int MinTradesGate = 10;
        private const double ComplexityPenaltyPerIndicator = 0.75; // percent points per indicator beyond 1
        private const double TurnoverPenaltyPerTradePct = 0.05; // subtract 0.05% per trade
        private const double MinAvgDurationBars = 2.0; // require average duration >= 2 bars

        #endregion

        #region Selection Methods

        /// <summary>
        ///     Create ranked selection probabilities for stable, repeatable selection
        ///     Uses linear ranking where best individual gets highest probability
        /// </summary>
        private List<(GeneticIndividual individual, double cumulativeProbability)> CreateRankedSelectionProbabilities(
            List<GeneticIndividual> population, Dictionary<GeneticIndividual, double> originalFitness)
        {
            // Sort population by fitness (best first) - this is deterministic for same fitness values
            var sortedPopulation = population
                .OrderByDescending(individual => originalFitness[individual])
                .ThenBy(individual => individual.GetHashCode()) // Tie-breaker for deterministic ordering
                .ToList();

            var rankedSelection = new List<(GeneticIndividual individual, double cumulativeProbability)>();
            var totalWeight = 0.0;

            // Linear ranking: P(i) = (2-SP + 2*(SP-1)*(N-i)/(N-1)) / N
            // where SP = selection pressure, N = population size, i = rank (0-based)
            var populationSize = population.Count;
            var selectionPressure = _selectionPressureBase;

            for (var rankIndex = 0; rankIndex < populationSize; rankIndex++)
            {
                // Calculate selection probability for rank i (higher rank = higher probability)
                var probability = (2.0 - selectionPressure + 2.0 * (selectionPressure - 1.0) *
                    (populationSize - 1 - rankIndex) / (populationSize - 1)) / populationSize;
                totalWeight += probability;
                rankedSelection.Add((sortedPopulation[rankIndex], totalWeight));
            }

            return rankedSelection;
        }

        /// <summary>
        ///     Stable rank-based selection using cumulative probabilities
        ///     Always returns the same individual for the same random value
        /// </summary>
        private GeneticIndividual RankBasedSelect(
            List<(GeneticIndividual individual, double cumulativeProbability)> rankedPopulation)
        {
            var randomValue = _randomNumberGenerator.NextDouble();

            // Find first individual whose cumulative probability >= random value
            foreach (var (individual, cumulativeProbability) in rankedPopulation)
                if (randomValue <= cumulativeProbability)
                    return individual;

            // Fallback to last individual (should never happen with proper probabilities)
            return rankedPopulation.Last().individual;
        }

        /// <summary>
        ///     Adaptive tournament selection with variable selection pressure
        /// </summary>
        private GeneticIndividual AdaptiveTournamentSelect(List<GeneticIndividual> population, double selectionPressure)
        {
            // Adjust tournament size based on selection pressure
            var adaptiveTournamentSize = (int)Math.Ceiling(_tournamentSize * selectionPressure);
            adaptiveTournamentSize = Math.Max(2, Math.Min(adaptiveTournamentSize, population.Count));

            // Sometimes select randomly regardless of fitness (preserve diversity)
            if (_randomNumberGenerator.NextDouble() < 1.0 - selectionPressure)
                return population[_randomNumberGenerator.Next(population.Count)];

            // Standard tournament selection with adaptive size
            var selected = new List<GeneticIndividual>();
            for (var tournamentIndex = 0; tournamentIndex < adaptiveTournamentSize; tournamentIndex++)
                selected.Add(population[_randomNumberGenerator.Next(population.Count)]);

            return selected.OrderByDescending(individual => individual.Fitness.FitnessScore).First();
        }

        /// <summary>
        ///     Legacy tournament selection method (kept for backward compatibility)
        /// </summary>
        private GeneticIndividual TournamentSelect(List<GeneticIndividual> population)
        {
            var selected = new List<GeneticIndividual>();
            for (var tournamentIndex = 0; tournamentIndex < _tournamentSize; tournamentIndex++)
                selected.Add(population[_randomNumberGenerator.Next(population.Count)]);
            return selected.OrderByDescending(individual => individual.Fitness.FitnessScore).First();
        }

        #endregion

        #region Diversity and Fitness Methods

        /// <summary>
        ///     Calculate adaptive selection pressure that increases over generations
        ///     Early generations use low pressure to explore, later generations use high pressure to exploit
        /// </summary>
        private double CalculateAdaptiveSelectionPressure(int currentGeneration, int totalGenerations)
        {
            var progress = (double)currentGeneration / Math.Max(1, totalGenerations - 1);
            return _minSelectionPressure + (_maxSelectionPressure - _minSelectionPressure) * progress;
        }

        /// <summary>
        ///     Apply fitness sharing to penalize similar individuals and maintain diversity
        /// </summary>
        private void ApplyFitnessSharing(List<GeneticIndividual> population)
        {
            // Store original fitness values
            var originalFitness =
                population.ToDictionary(individual => individual, individual => individual.Fitness.FitnessScore);

            foreach (var individual in population)
            {
                var rawFitness = originalFitness[individual].GetValueOrDefault(0);
                var nichingPenalty = 0.0;

                // Calculate penalty based on similarity to other individuals
                foreach (var other in population)
                {
                    if (individual == other) continue;

                    var similarity = CalculateGenotypeSimilarity(individual, other);
                    if (similarity > _diversityThreshold)
                        nichingPenalty += 0.1 * similarity; // Penalty for being similar
                }

                // Apply shared fitness (temporary for selection only)
                individual.Fitness = new Fitness(individual.Fitness.DollarGain, individual.Fitness.PercentGain, rawFitness - nichingPenalty);
            }
        }

        /// <summary>
        ///     Compute robust selection score with validation (if provided), complexity and turnover penalties, and min holding time gate.
        /// </summary>
        private double ComputeSelectionScore(GeneticIndividual individual, double trainingScore, double? validationScore, int periodLength)
        {
            // Gate: too few trades
            var trades = individual.Trades?.Count ?? 0;
            if (trades < MinTradesGate) return double.NegativeInfinity;

            // Average duration in bars
            double avgDuration = 0.0;
            if (individual.Trades != null && individual.Trades.Count > 0)
            {
                var durations = individual.Trades.Where(t => t.CloseIndex > t.OpenIndex).Select(t => (double)(t.CloseIndex - t.OpenIndex)).ToList();
                if (durations.Count > 0) avgDuration = durations.Average();
            }
            if (avgDuration > 0 && avgDuration < MinAvgDurationBars) return double.NegativeInfinity;

            // Complexity penalty (beyond 1 indicator)
            var complexity = Math.Max(0, (individual.Indicators?.Count ?? 0) - 1);
            var penalized = trainingScore - ComplexityPenaltyPerIndicator * complexity;

            // Turnover penalty ~ trades count * cost per trade
            penalized -= TurnoverPenaltyPerTradePct * trades;

            // Validation adjustment if available: prefer validation, penalize generalization gap
            if (validationScore.HasValue)
            {
                var gap = Math.Max(0.0, trainingScore - validationScore.Value);
                penalized = validationScore.Value - 0.5 * gap - ComplexityPenaltyPerIndicator * complexity - TurnoverPenaltyPerTradePct * trades;
            }

            if (double.IsNaN(penalized) || double.IsInfinity(penalized)) return double.NegativeInfinity;
            return penalized;
        }

        /// <summary>
        ///     Calculate similarity between two genetic individuals based on their genotype
        /// </summary>
        private double CalculateGenotypeSimilarity(GeneticIndividual individual1, GeneticIndividual individual2)
        {
            var similarity = 0.0;

            // Compare indicator configurations (40% weight)
            similarity += 0.4 * CompareIndicatorSets(individual1.Indicators, individual2.Indicators);

            // Compare trading parameters (60% weight)
            if (Math.Abs(individual1.TradePercentageForStocks - individual2.TradePercentageForStocks) < 0.005) similarity += 0.10;
            if (Math.Abs(individual1.TradePercentageForOptions - individual2.TradePercentageForOptions) < 0.005) similarity += 0.05;
            if (individual1.AllowedTradeTypes == individual2.AllowedTradeTypes) similarity += 0.15;
            if (individual1.AllowedSecurityTypes == individual2.AllowedSecurityTypes) similarity += 0.15;
            if (individual1.CombinationMethod == individual2.CombinationMethod) similarity += 0.15;

            return Math.Min(1.0, similarity);
        }

        /// <summary>
        ///     Compare two sets of indicators and return similarity score
        /// </summary>
        private double CompareIndicatorSets(List<IndicatorParams> indicators1, List<IndicatorParams> indicators2)
        {
            if (indicators1.Count == 0 && indicators2.Count == 0) return 1.0;
            if (indicators1.Count == 0 || indicators2.Count == 0) return 0.0;

            // Compare indicator types
            var types1 = indicators1.Select(indicator => indicator.Type).OrderBy(type => type).ToList();
            var types2 = indicators2.Select(indicator => indicator.Type).OrderBy(type => type).ToList();

            var commonTypes = types1.Intersect(types2).Count();
            var totalUniqueTypes = types1.Union(types2).Count();

            var typeSimilarity = totalUniqueTypes > 0 ? (double)commonTypes / totalUniqueTypes : 0.0;

            // Compare periods and trade modes for matched types
            var periodSimilarity = 0.0;
            var modeSimilarity = 0.0;
            if (commonTypes > 0)
            {
                var matchingPairs = indicators1
                    .Join(indicators2, i1 => i1.Type, i2 => i2.Type,
                        (i1, i2) => new { i1.Period, OtherPeriod = i2.Period, i1.TradeMode, OtherMode = i2.TradeMode })
                    .ToList();

                if (matchingPairs.Count > 0)
                {
                    periodSimilarity = matchingPairs.Average(pair =>
                        1.0 - Math.Min(1.0, Math.Abs(pair.Period - pair.OtherPeriod) / 50.0));
                    modeSimilarity = matchingPairs.Average(pair => pair.TradeMode == pair.OtherMode ? 1.0 : 0.0);
                }
            }

            return (typeSimilarity + periodSimilarity + modeSimilarity) / 3.0;
        }

        /// <summary>
        ///     Calculate population diversity score (higher = more diverse)
        /// </summary>
        private double CalculatePopulationDiversity(List<GeneticIndividual> population)
        {
            if (population.Count < 2) return 0.0;

            var totalSimilarity = 0.0;
            var comparisons = 0;

            for (var individualIndex = 0; individualIndex < population.Count; individualIndex++)
            for (var otherIndex = individualIndex + 1; otherIndex < population.Count; otherIndex++)
            {
                totalSimilarity += CalculateGenotypeSimilarity(population[individualIndex], population[otherIndex]);
                comparisons++;
            }

            var avgSimilarity = totalSimilarity / comparisons;
            return 1.0 - avgSimilarity; // Convert similarity to diversity
        }

        #endregion

        #region Genetic Operators

        public GeneticIndividual Crossover(GeneticIndividual parent1, GeneticIndividual parent2)
        {
            var child = new GeneticIndividual();
            child.RandomNumberGenerator = _randomNumberGenerator;

            // Ensure the child gets a proper starting balance (bug fix: previously 0)
            var p1Balance = parent1.StartingBalance;
            var p2Balance = parent2.StartingBalance;
            child.StartingBalance = p1Balance > 0 ? p1Balance : (p2Balance > 0 ? p2Balance : _startingBalance);
            child.FinalBalance = child.StartingBalance;

            var minIndicators = Math.Min(parent1.Indicators.Count, parent2.Indicators.Count);
            var split = _randomNumberGenerator.Next(minIndicators + 1);
            for (var indicatorIndex = 0; indicatorIndex < split; indicatorIndex++)
                child.Indicators.Add(Clone(parent1.Indicators[indicatorIndex]));
            for (var indicatorIndex = split; indicatorIndex < parent2.Indicators.Count; indicatorIndex++)
                child.Indicators.Add(Clone(parent2.Indicators[indicatorIndex]));

            child.SignalCombination = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.SignalCombination : parent2.SignalCombination;
            child.LongEntryThreshold = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.LongEntryThreshold : parent2.LongEntryThreshold;
            child.ShortEntryThreshold = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.ShortEntryThreshold : parent2.ShortEntryThreshold;
            child.LongExitThreshold = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.LongExitThreshold : parent2.LongExitThreshold;
            child.ShortExitThreshold = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.ShortExitThreshold : parent2.ShortExitThreshold;

            // Add these after the existing crossover logic:
            child.LongCallEntryThreshold = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.LongCallEntryThreshold : parent2.LongCallEntryThreshold;
            child.ShortCallEntryThreshold = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.ShortCallEntryThreshold : parent2.ShortCallEntryThreshold;
            child.LongCallExitThreshold = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.LongCallExitThreshold : parent2.LongCallExitThreshold;
            child.ShortCallExitThreshold = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.ShortCallExitThreshold : parent2.ShortCallExitThreshold;
            child.LongPutEntryThreshold = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.LongPutEntryThreshold : parent2.LongPutEntryThreshold;
            child.ShortPutEntryThreshold = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.ShortPutEntryThreshold : parent2.ShortPutEntryThreshold;
            child.LongPutExitThreshold = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.LongPutExitThreshold : parent2.LongPutExitThreshold;
            child.ShortPutExitThreshold = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.ShortPutExitThreshold : parent2.ShortPutExitThreshold;

            // Crossover genetic parameters for multiple indicator support
            child.AllowMultipleTrades = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.AllowMultipleTrades
                : parent2.AllowMultipleTrades;
            child.CombinationMethod = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.CombinationMethod
                : parent2.CombinationMethod;
            child.EnsembleVotingThreshold = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.EnsembleVotingThreshold
                : parent2.EnsembleVotingThreshold;

            // Crossover trading and option genetic parameters
            child.AllowedTradeTypes = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.AllowedTradeTypes
                : parent2.AllowedTradeTypes;
            child.AllowedOptionTypes = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.AllowedOptionTypes
                : parent2.AllowedOptionTypes; // (missing before)
            child.AllowedSecurityTypes = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.AllowedSecurityTypes
                : parent2.AllowedSecurityTypes;
            child.NumberOfOptionContractsToOpen = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.NumberOfOptionContractsToOpen
                : parent2.NumberOfOptionContractsToOpen;
            child.OptionDaysOut = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.OptionDaysOut
                : parent2.OptionDaysOut;
            child.OptionStrikeDistance = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.OptionStrikeDistance
                : parent2.OptionStrikeDistance;

            // Crossover FastMAPeriod and SlowMAPeriod
            child.FastMAPeriod =
                _randomNumberGenerator.NextDouble() > 0.5 ? parent1.FastMAPeriod : parent2.FastMAPeriod;
            child.SlowMAPeriod =
                _randomNumberGenerator.NextDouble() > 0.5 ? parent1.SlowMAPeriod : parent2.SlowMAPeriod;

            // NEW: crossover top-level OHLC selection
            child.OHLC = _randomNumberGenerator.NextDouble() > 0.5 ? parent1.OHLC : parent2.OHLC;

            // NEW: crossover top-level BufferSource
            child.BufferSource = _randomNumberGenerator.NextDouble() > 0.5 ? parent1.BufferSource : parent2.BufferSource;

            // Generate compatible scale out percentages for the chosen number of contracts
            child.OptionContractsToScaleOut =
                GenerateValidScaleOutFractions(_randomNumberGenerator, child.NumberOfOptionContractsToOpen);

            // Crossover trade percentages
            child.TradePercentageForStocks = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.TradePercentageForStocks
                : parent2.TradePercentageForStocks;
            child.TradePercentageForOptions = _randomNumberGenerator.NextDouble() > 0.5
                ? parent1.TradePercentageForOptions
                : parent2.TradePercentageForOptions;

            return child;
        }

        private void Mutate(GeneticIndividual individual)
        {
            // Mutate indicator parameters - ENHANCED to cover all properties
            for (var indicatorIndex = 0; indicatorIndex < individual.Indicators.Count; indicatorIndex++)
                if (_randomNumberGenerator.NextDouble() < _mutationRate)
                {
                    // Mutate one field randomly - now includes BufferSource
                    var indicator = individual.Indicators[indicatorIndex];
                    var field = _randomNumberGenerator.Next(13); // 0..12
                    switch (field)
                    {
                        case 0: // Type
                            indicator.Type = _randomNumberGenerator.Next(_indicatorTypeMin, _indicatorTypeMax + 1);
                            break;
                        case 1: // Period
                            indicator.Period = _randomNumberGenerator.Next(_indicatorPeriodMin, _indicatorPeriodMax + 1);
                            break;
                        case 2: // Mode
                            indicator.Mode = _randomNumberGenerator.Next(_indicatorModeMin, _indicatorModeMax + 1);
                            break;
                        case 3: // TimeFrame
                            var values = Enum.GetValues(typeof(TimeFrame));
                            var timeFrameValues = values.Cast<TimeFrame>().ToArray();
                            var minIndex = Array.IndexOf(timeFrameValues, _indicatorTimeFrameMin);
                            var maxIndex = Array.IndexOf(timeFrameValues, _indicatorTimeFrameMax);
                            indicator.TimeFrame = timeFrameValues[_randomNumberGenerator.Next(minIndex, maxIndex + 1)];
                            break;
                        case 4: // Polarity
                            int polarity;
                            do
                            {
                                polarity = _randomNumberGenerator.Next(_indicatorPolarityMin, _indicatorPolarityMax + 1);
                            } while (polarity == 0);
                            indicator.Polarity = polarity;
                            break;
                        case 5: // Thresholds
                            indicator.LongThreshold = _indicatorThresholdMin + _randomNumberGenerator.NextDouble() *
                                (_indicatorThresholdMax - _indicatorThresholdMin);
                            indicator.ShortThreshold = -(_indicatorThresholdMin + _randomNumberGenerator.NextDouble() *
                                (_indicatorThresholdMax - _indicatorThresholdMin));
                            break;
                        case 6: // OHLC
                            var ohlcValues = Enum.GetValues(typeof(OHLC));
                            indicator.OHLC = (OHLC)ohlcValues.GetValue(_randomNumberGenerator.Next(ohlcValues.Length));
                            break;
                        case 7: // FastMAPeriod
                            indicator.FastMAPeriod = _randomNumberGenerator.Next(_fastMAPeriodMin, _fastMAPeriodMax + 1);
                            break;
                        case 8: // SlowMAPeriod (ensure Slow > Fast)
                            indicator.SlowMAPeriod = _randomNumberGenerator.Next(Math.Max(_slowMAPeriodMin, indicator.FastMAPeriod + 1), _slowMAPeriodMax + 1);
                            break;
                        case 9: // Param1..3
                            indicator.Param1 = _randomNumberGenerator.NextDouble() * 2.0 - 1.0;
                            indicator.Param2 = _randomNumberGenerator.NextDouble() * 100.0;
                            indicator.Param3 = _randomNumberGenerator.NextDouble() * 10.0;
                            break;
                        case 10: // Param4..5 + Debug
                            indicator.Param4 = _randomNumberGenerator.NextDouble() * 1000.0;
                            indicator.Param5 = _randomNumberGenerator.NextDouble() * 50.0;
                            indicator.DebugCase = _randomNumberGenerator.NextDouble() > 0.9;
                            break;
                        case 11: // TradeMode
                            indicator.TradeMode = indicator.TradeMode == IndicatorTradeMode.Delta ? IndicatorTradeMode.Range : IndicatorTradeMode.Delta;
                            break;
                        case 12: // BufferSource
                            indicator.BufferSource = indicator.BufferSource == PriceBufferSource.UseStockPriceBuffer
                                ? PriceBufferSource.UseOptionPriceBuffer
                                : PriceBufferSource.UseStockPriceBuffer;
                            break;
                    }
                }

            // Mutate genetic parameters for multiple indicator support
            if (_randomNumberGenerator.NextDouble() < _mutationRate)
            {
                individual.AllowMultipleTrades = !individual.AllowMultipleTrades;
                individual.AllowMultipleTrades = false;
            }

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
            {
                individual.CombinationMethod = (CombinationMethod)_randomNumberGenerator.Next(0, 3);
                individual.CombinationMethod = CombinationMethod.Sum;
            }

            // Add these to the Mutate method:
            if (_randomNumberGenerator.NextDouble() < _mutationRate)
            {
                var methods = Enum.GetValues(typeof(SignalCombinationMethod));
                individual.SignalCombination = (SignalCombinationMethod)methods.GetValue(
                    _randomNumberGenerator.Next(methods.Length));
            }

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.LongEntryThreshold = _randomNumberGenerator.Next(-_maxIndicators, _maxIndicators + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.ShortEntryThreshold = _randomNumberGenerator.Next(-_maxIndicators, _maxIndicators + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.LongExitThreshold = _randomNumberGenerator.Next(-_maxIndicators, _maxIndicators + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.ShortExitThreshold = _randomNumberGenerator.Next(-_maxIndicators, _maxIndicators + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.OptionExitThreshold = _randomNumberGenerator.Next(-_maxIndicators, _maxIndicators + 1);

            // Add these after the existing mutation logic:
            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.LongCallEntryThreshold = _randomNumberGenerator.Next(-_maxIndicators, _maxIndicators + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.ShortCallEntryThreshold = _randomNumberGenerator.Next(-_maxIndicators, _maxIndicators + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.LongCallExitThreshold = _randomNumberGenerator.Next(-_maxIndicators, _maxIndicators + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.ShortCallExitThreshold = _randomNumberGenerator.Next(-_maxIndicators, _maxIndicators + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.LongPutEntryThreshold = _randomNumberGenerator.Next(-_maxIndicators, _maxIndicators + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.ShortPutEntryThreshold = _randomNumberGenerator.Next(-_maxIndicators, _maxIndicators + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.LongPutExitThreshold = _randomNumberGenerator.Next(-_maxIndicators, _maxIndicators + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.ShortPutExitThreshold = _randomNumberGenerator.Next(-_maxIndicators, _maxIndicators + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.EnsembleVotingThreshold =
                    _randomNumberGenerator.Next(1, Math.Min(5, individual.Indicators.Count) + 1);

            // Mutate trading and option genetic parameters
            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.AllowedTradeTypes =
                    (AllowedTradeType)_randomNumberGenerator.Next(_allowedTradeTypeMin, _allowedTradeTypeMax + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.AllowedOptionTypes =
                    (AllowedOptionType)_randomNumberGenerator.Next(_allowedOptionTypeMin, _allowedOptionTypeMax + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.AllowedSecurityTypes =
                    (AllowedSecurityType)_randomNumberGenerator.Next(_allowedSecurityTypeMin, _allowedSecurityTypeMax + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
            {
                individual.NumberOfOptionContractsToOpen =
                    _randomNumberGenerator.Next(_numberOfOptionContractsMin, _numberOfOptionContractsMax + 1);
                individual.OptionContractsToScaleOut = GenerateValidScaleOutFractions(_randomNumberGenerator,
                    individual.NumberOfOptionContractsToOpen);
            }

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.OptionDaysOut =
                    _randomNumberGenerator.Next(_optionDaysOutMin, _optionDaysOutMax + 1);

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.OptionStrikeDistance =
                    _randomNumberGenerator.Next(_optionStrikeDistanceMin, _optionStrikeDistanceMax + 1);

            // NEW: mutate top-level OHLC selection occasionally
            if (_randomNumberGenerator.NextDouble() < _mutationRate)
            {
                var ohlcValuesTop = Enum.GetValues(typeof(OHLC));
                individual.OHLC = (OHLC)ohlcValuesTop.GetValue(_randomNumberGenerator.Next(ohlcValuesTop.Length));
            }

            // NEW: mutate top-level buffer preference
            if (_randomNumberGenerator.NextDouble() < _mutationRate)
            {
                individual.BufferSource = individual.BufferSource == PriceBufferSource.UseStockPriceBuffer
                    ? PriceBufferSource.UseOptionPriceBuffer
                    : PriceBufferSource.UseStockPriceBuffer;
            }

            // Mutate trade percentages using genetic parameters - choose bounds by security type
            if (_randomNumberGenerator.NextDouble() < _mutationRate)
            {
                var minS = (int)(_tradePercentageStocksMin * 100);
                var maxS = (int)((_tradePercentageStocksMax + 0.01) * 100);
                individual.TradePercentageForStocks =
                    (double)_randomNumberGenerator.Next(minS, maxS) / 100.0;
            }

            if (_randomNumberGenerator.NextDouble() < _mutationRate)
            {
                var minO = (int)(_tradePercentageOptionsMin * 100);
                var maxO = (int)((_tradePercentageOptionsMax + 0.01) * 100);
                individual.TradePercentageForOptions =
                    (double)_randomNumberGenerator.Next(minO, maxO) / 100.0;
            }

            // Mutate FastMAPeriod and SlowMAPeriod
            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.FastMAPeriod = _randomNumberGenerator.Next(_fastMAPeriodMin, _fastMAPeriodMax + 1);
            if (_randomNumberGenerator.NextDouble() < _mutationRate)
                individual.SlowMAPeriod =
                    _randomNumberGenerator.Next(individual.FastMAPeriod + 1, _slowMAPeriodMax + 1);

            // Possibly add or remove indicators - ENHANCED with complete property initialization
            if (_randomNumberGenerator.NextDouble() < _mutationRate && individual.Indicators.Count < _maxIndicators)
            {
                var type = _randomNumberGenerator.Next(_indicatorTypeMin, _indicatorTypeMax + 1);
                int polarity;
                do
                {
                    polarity = _randomNumberGenerator.Next(_indicatorPolarityMin, _indicatorPolarityMax + 1);
                } while (polarity == 0);

                var values = Enum.GetValues(typeof(TimeFrame));
                var timeFrameValues = values.Cast<TimeFrame>().ToArray();
                var minIndex2 = Array.IndexOf(timeFrameValues, _indicatorTimeFrameMin);
                var maxIndex2 = Array.IndexOf(timeFrameValues, _indicatorTimeFrameMax);
                
                var ohlcValues = Enum.GetValues(typeof(OHLC));
                var randomOHLC = (OHLC)ohlcValues.GetValue(_randomNumberGenerator.Next(ohlcValues.Length));

                // Choose valid fast/slow MA periods with Slow > Fast and within configured bounds
                var fastMax = Math.Min(_fastMAPeriodMax, _slowMAPeriodMax - 1);
                var fast = _randomNumberGenerator.Next(_fastMAPeriodMin, fastMax + 1);
                var slowMin = Math.Max(_slowMAPeriodMin, fast + 1);
                var slow = _randomNumberGenerator.Next(slowMin, _slowMAPeriodMax + 1);

                var newIndicator = new IndicatorParams
                {
                    Type = type,
                    Period = _randomNumberGenerator.Next(_indicatorPeriodMin, _indicatorPeriodMax + 1),
                    Mode = _randomNumberGenerator.Next(_indicatorModeMin, _indicatorModeMax + 1),
                    TimeFrame = timeFrameValues[_randomNumberGenerator.Next(minIndex2, maxIndex2 + 1)],
                    OHLC = randomOHLC,
                    Polarity = polarity,
                    FastMAPeriod = fast,
                    SlowMAPeriod = slow,
                    LongThreshold = _indicatorThresholdMin + _randomNumberGenerator.NextDouble() *
                        (_indicatorThresholdMax - _indicatorThresholdMin),
                    ShortThreshold = -(_indicatorThresholdMin + _randomNumberGenerator.NextDouble() *
                        (_indicatorThresholdMax - _indicatorThresholdMin)),
                    Param1 = _randomNumberGenerator.NextDouble() * 2.0 - 1.0,
                    Param2 = _randomNumberGenerator.NextDouble() * 100.0,
                    Param3 = _randomNumberGenerator.NextDouble() * 10.0,
                    Param4 = _randomNumberGenerator.NextDouble() * 1000.0,
                    Param5 = _randomNumberGenerator.NextDouble() * 50.0,
                    DebugCase = _randomNumberGenerator.NextDouble() > 0.9,
                    TradeMode = _randomNumberGenerator.NextDouble() < 0.5 ? IndicatorTradeMode.Delta : IndicatorTradeMode.Range,
                    BufferSource = _randomNumberGenerator.NextDouble() < 0.75 ? PriceBufferSource.UseStockPriceBuffer : PriceBufferSource.UseOptionPriceBuffer
                };
                individual.Indicators.Add(newIndicator);
            }

            if (_randomNumberGenerator.NextDouble() < _mutationRate && individual.Indicators.Count > 1)
                individual.Indicators.RemoveAt(_randomNumberGenerator.Next(individual.Indicators.Count));
        }

        private GeneticIndividual Clone(GeneticIndividual original)
        {
            var clone = new GeneticIndividual();
            clone.RandomNumberGenerator = _randomNumberGenerator;
            clone.StartingBalance = original.StartingBalance;

            // Clone genetic parameters for multiple indicator support
            clone.AllowMultipleTrades = original.AllowMultipleTrades;
            clone.CombinationMethod = original.CombinationMethod;
            clone.EnsembleVotingThreshold = original.EnsembleVotingThreshold;

            // Add these to the Clone method:
            clone.SignalCombination = original.SignalCombination;
            clone.LongEntryThreshold = original.LongEntryThreshold;
            clone.ShortEntryThreshold = original.ShortEntryThreshold;
            clone.LongExitThreshold = original.LongExitThreshold;
            clone.ShortExitThreshold = original.ShortExitThreshold;
            clone.OptionExitThreshold = original.OptionExitThreshold;

            // Add these after the existing clone logic:
            clone.LongCallEntryThreshold = original.LongCallEntryThreshold;
            clone.ShortCallEntryThreshold = original.ShortCallEntryThreshold;
            clone.LongCallExitThreshold = original.LongCallExitThreshold;
            clone.ShortCallExitThreshold = original.ShortCallExitThreshold;
            clone.LongPutEntryThreshold = original.LongPutEntryThreshold;
            clone.ShortPutEntryThreshold = original.ShortPutEntryThreshold;
            clone.LongPutExitThreshold = original.LongPutExitThreshold;
            clone.ShortPutExitThreshold = original.ShortPutExitThreshold;

            // Clone trading and option genetic parameters
            clone.AllowedTradeTypes = original.AllowedTradeTypes;
            clone.AllowedOptionTypes = original.AllowedOptionTypes;
            clone.AllowedSecurityTypes = original.AllowedSecurityTypes;
            clone.NumberOfOptionContractsToOpen = original.NumberOfOptionContractsToOpen;
            clone.OptionDaysOut = original.OptionDaysOut;
            clone.OptionStrikeDistance = original.OptionStrikeDistance;
            clone.OptionContractsToScaleOut = (double[])original.OptionContractsToScaleOut.Clone();

            // Clone trade percentages
            clone.TradePercentageForStocks = original.TradePercentageForStocks;
            clone.TradePercentageForOptions = original.TradePercentageForOptions;

            // Clone FastMAPeriod and SlowMAPeriod
            clone.FastMAPeriod = original.FastMAPeriod;
            clone.SlowMAPeriod = original.SlowMAPeriod;

            // NEW: clone top-level OHLC and BufferSource
            clone.OHLC = original.OHLC;
            clone.BufferSource = original.BufferSource;

            foreach (var indicator in original.Indicators)
                clone.Indicators.Add(Clone(indicator));
            return clone;
        }

        private IndicatorParams Clone(IndicatorParams indicator)
        {
            return new IndicatorParams
            {
                Type = indicator.Type,
                Period = indicator.Period,
                Mode = indicator.Mode,
                TimeFrame = indicator.TimeFrame,
                OHLC = indicator.OHLC,
                Polarity = indicator.Polarity,
                FastMAPeriod = indicator.FastMAPeriod,
                SlowMAPeriod = indicator.SlowMAPeriod,
                LongThreshold = indicator.LongThreshold,
                ShortThreshold = indicator.ShortThreshold,
                Param1 = indicator.Param1,
                Param2 = indicator.Param2,
                Param3 = indicator.Param3,
                Param4 = indicator.Param4,
                Param5 = indicator.Param5,
                DebugCase = indicator.DebugCase,
                // NEW: clone TradeMode and BufferSource
                TradeMode = indicator.TradeMode,
                BufferSource = indicator.BufferSource
            };
        }

        #endregion

        #region Cross-Validation

        private GeneticIndividual SolveWithInnerTimeSeriesCv(PriceRecord[] priceRecords, bool runInParallel)
        {
            // Time-ordered k-fold (rolling). For each evaluation of an individual, compute mean validation perf.
            int k = Math.Max(2, Program.IntraCVFolds);
            int foldSize = priceRecords.Length / (k + 1); // leave tail for robustness
            if (foldSize < 21)
                return SolveWithoutInnerCv(priceRecords, runInParallel);

            var population = new List<GeneticIndividual>();
            for (int i = 0; i < Program.PopulationSize; i++)
            {
                population.Add(new GeneticIndividual(new Random(84 + i),
                    Program.StartingBalance,
                    Program.IndicatorTypeMin, Program.IndicatorTypeMax,
                    Program.IndicatorPeriodMin, Program.IndicatorPeriodMax,
                    Program.IndicatorModeMin, Program.IndicatorModeMax,
                    Program.IndicatorTimeFrameMin, Program.IndicatorTimeFrameMax,
                    Program.IndicatorPolarityMin, Program.IndicatorPolarityMax,
                    Program.IndicatorThresholdMin, Program.IndicatorThresholdMax,
                    Program.MaxIndicators, 
                    // pass both stock/option trade% bounds from Program
                    Program.TradePercentageForStocksMin, Program.TradePercentageForStocksMax,
                    Program.TradePercentageForOptionsMin, Program.TradePercentageForOptionsMax,
                    Program.OptionDaysOutMin, Program.OptionDaysOutMax,
                    Program.OptionStrikeDistanceMin, Program.OptionStrikeDistanceMax,
                    Program.FastMAPeriodMin, Program.FastMAPeriodMax,
                    Program.SlowMAPeriodMin, Program.SlowMAPeriodMax,
                    Program.AllowedTradeTypeMin, Program.AllowedTradeTypeMax,
                    Program.AllowedOptionTypeMin, Program.AllowedOptionTypeMax,
                    Program.AllowedSecurityTypeMin, Program.AllowedSecurityTypeMax,
                    Program.NumberOfOptionContractsMin, Program.NumberOfOptionContractsMax));
            }

            GeneticIndividual best = null;
            double bestMeanVal = double.MinValue;
            int patience = 0;

            for (int gen = 0; gen < Program.Generations; gen++)
            {
                // Evaluate each individual via rolling CV
                var scores = new Dictionary<GeneticIndividual, double>();

                foreach (var ind in population)
                {
                    double meanVal = TimeSeriesCrossValidatedScore(ind, priceRecords, k, foldSize,
                        Program.IntraCVPurgeBars, Program.IntraCVEmbargoBars);
                    scores[ind] = meanVal;
                }

                // Select best
                var champion = scores.OrderByDescending(kvp => kvp.Value).First().Key;
                if (scores[champion] > bestMeanVal)
                {
                    bestMeanVal = scores[champion];
                    best = GeneticEvolvers.CloneIndividual(champion);
                    patience = 0;
                }
                else
                {
                    patience++;
                }
                if (Program.EnableWalkforwardEarlyStopping && patience >= Program.EarlyStoppingPatience) break;

                // Breed next gen (elitism + clones of top quartile)
                var ranked = scores.OrderByDescending(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
                int eliteCount = Math.Max(1, ranked.Count / 4);
                var next = new List<GeneticIndividual>();
                for (int i = 0; i < eliteCount; i++) next.Add(GeneticEvolvers.CloneIndividual(ranked[i]));
                var rng = new Random(200 + gen);
                while (next.Count < population.Count)
                {
                    var parent = ranked[rng.Next(eliteCount)];
                    next.Add(GeneticEvolvers.CloneIndividual(parent));
                }
                population = next;
            }

            return best ?? population.First();
        }

        private static double TimeSeriesCrossValidatedScore(GeneticIndividual ind, PriceRecord[] all, int k, int foldSize, int purge, int embargo)
        {
            var vals = new List<double>(k);
            for (int fold = 0; fold < k; fold++)
            {
                int trainEnd = (fold + 1) * foldSize;
                int purgeStart = Math.Min(trainEnd + purge, all.Length - 1);
                int valStart = purgeStart;
                int valEnd = Math.Min(valStart + foldSize, all.Length);
                if (valEnd - valStart < 21) break;

                var train = all.Take(trainEnd).ToArray();
                var val = all.Skip(valStart).Take(valEnd - valStart).ToArray();

                GeneticIndividual.AnalyzeIndicatorRanges(train);
                var fit = ind.Process(val);
                vals.Add(fit.PercentGain);
            }
            if (vals.Count == 0) return double.NegativeInfinity;
            var mean = vals.Average();
            // Optionally penalize variance
            var std = vals.Count > 1 ? Math.Sqrt(vals.Sum(v => Math.Pow(v - mean, 2)) / vals.Count) : 0.0;
            return mean - Math.Max(0.0, std - Program.CrossValidationVarianceThreshold);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        ///     Generate scale-out fractions that result in whole contract numbers.
        ///     The fractions must sum to 1.0 and when multiplied with totalContracts, yield whole numbers.
        /// </summary>
        /// <param name="randomNumberGenerator">Random number generator</param>
        /// <param name="totalContracts">Total number of contracts to scale out</param>
        /// <returns>Array of 8 fractions that sum to 1.0 and work with totalContracts</returns>
        private static double[] GenerateValidScaleOutFractions(Random randomNumberGenerator, double totalContracts)
        {
            var fractions = new double[8];
            var contractsToScaleOut = new int[8];
            var remainingContracts = (int)totalContracts;

            if (remainingContracts <= 0)
            {
                for (int i = 0; i < 8; i++) fractions[i] = 0.0;
                return fractions;
            }

            if (remainingContracts < 8)
            {
                for (var i = 0; i < 8; i++)
                {
                    if (remainingContracts > 0)
                    {
                        contractsToScaleOut[i] = 1;
                        remainingContracts--;
                    }
                    else
                    {
                        contractsToScaleOut[i] = 0;
                    }
                }
            }
            else
            {
                for (var i = 0; i < 7 && remainingContracts > 0; i++)
                {
                    var maxForThisStep = Math.Max(1, remainingContracts / (8 - i));
                    contractsToScaleOut[i] = randomNumberGenerator.Next(1, Math.Min(maxForThisStep + 1, remainingContracts + 1));
                    remainingContracts -= contractsToScaleOut[i];
                }
                contractsToScaleOut[7] = remainingContracts;
            }

            for (var i = 0; i < 8; i++) fractions[i] = contractsToScaleOut[i] / totalContracts;
            return fractions;
        }

        #endregion
    }
}