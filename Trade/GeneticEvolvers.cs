using System;
using System.Collections.Generic;
using System.Linq;

namespace Trade
{
    /// <summary>
    ///     Advanced genetic evolution strategies for preserving diversity and preventing premature convergence.
    ///     Contains static methods for sophisticated genetic algorithm operations including island evolution,
    ///     fitness sharing, speciation, and schema preservation.
    /// </summary>
    public static class GeneticEvolvers
    {
        #region Speciation

        /// <summary>
        ///     Assign individuals to species based on genetic similarity
        /// </summary>
        /// <param name="population">Population to speciate</param>
        /// <param name="species">Existing species list (will be modified)</param>
        /// <param name="compatibilityThreshold">Similarity threshold for same species</param>
        public static void AssignToSpecies(
            List<GeneticIndividual> population,
            List<Species> species,
            double compatibilityThreshold = 0.6)
        {
            // Clear existing assignments
            foreach (var speciesItem in species)
                speciesItem.Members.Clear();

            foreach (var individual in population)
            {
                var foundSpecies = false;

                // Try to assign to existing species
                foreach (var speciesItem in species)
                    if (CalculateGenotypeSimilarity(individual, speciesItem.Representative) > compatibilityThreshold)
                    {
                        speciesItem.Members.Add(individual);
                        foundSpecies = true;
                        break;
                    }

                // Create new species if no match
                if (!foundSpecies)
                    species.Add(new Species
                    {
                        Members = new List<GeneticIndividual> { individual },
                        Representative = CloneIndividual(individual)
                    });
            }

            // Remove empty species
            species.RemoveAll(speciesItem => speciesItem.Members.Count == 0);

            // Apply fitness sharing within each species
            foreach (var speciesItem in species) speciesItem.AdjustFitness();
        }

        #endregion

        #region Strategy Pattern Analysis

        /// <summary>
        ///     Generate a strategy pattern key for tracking similar strategies
        /// </summary>
        public static string GenerateStrategyPatternKey(GeneticIndividual individual)
        {
            var indicators = string.Join(",",
                individual.Indicators.Select(indicator => indicator.Type).OrderBy(type => type));
            var complexity = individual.Indicators.Count;
            var tradeSize = Math.Round(individual.TradePercentageForStocks * 100, 1);
            var security = individual.AllowedSecurityTypes.ToString().Substring(0, 1);
            var tradeType = individual.AllowedTradeTypes.ToString().Substring(0, 1);
            var combination = individual.CombinationMethod.ToString().Substring(0, 1);

            // Enhanced pattern key with more genetic parameters
            var optionParams = individual.AllowedSecurityTypes == AllowedSecurityType.Option
                ? $"O{individual.OptionDaysOut}d{individual.OptionStrikeDistance}s{individual.NumberOfOptionContractsToOpen:F0}c"
                : "NoOpt";

            var movingAverageParams = $"MA{individual.FastMAPeriod}-{individual.SlowMAPeriod}";

            // NEW: Summarize TradeModes (Delta vs Range)
            var deltaCount = individual.Indicators.Count(ind => ind.TradeMode == IndicatorTradeMode.Delta);
            var rangeCount = individual.Indicators.Count(ind => ind.TradeMode == IndicatorTradeMode.Range);
            var tradeModes = $"Modes:D{deltaCount}/R{rangeCount}";

            return
                $"{indicators}|C{complexity}|T{tradeSize}|{security}{tradeType}|{combination}|{optionParams}|{movingAverageParams}|{tradeModes}";
        }

        #endregion

        #region Data Structures

        /// <summary>
        ///     Represents a species in speciation-based genetic algorithms
        /// </summary>
        public class Species
        {
            public double BestFitnessEver = double.MinValue;
            public List<GeneticIndividual> Members = new List<GeneticIndividual>();
            public GeneticIndividual Representative;
            public int StagnantGenerations = 0;

            public void AdjustFitness()
            {
                // Fitness sharing within species
                foreach (var member in Members) member.Fitness.FitnessScore /= Members.Count; // Share fitness
            }
        }

        /// <summary>
        ///     Configuration for island-based genetic algorithm
        /// </summary>
        public class IslandConfiguration
        {
            public double DiversityThreshold = 0.8; // Similarity threshold for diversity
            public int MigrationFrequency = 10; // Every N generations
            public double MigrationRate = 0.05; // 5% of population
            public int NumberOfIslands = 4;
        }

        /// <summary>
        ///     Historical gene pool for preserving interesting genetics
        /// </summary>
        public class HistoricalGenePool
        {
            public List<GeneticIndividual> HallOfFame = new List<GeneticIndividual>();
            public List<GeneticIndividual> InterestingMutants = new List<GeneticIndividual>();
            public int MaxArchiveSize = 100;
            public Dictionary<string, int> SchemaFrequency = new Dictionary<string, int>();
        }

        #endregion

        #region Multi-Population Islands

        /// <summary>
        ///     Run island-based genetic algorithm evolution with periodic migration
        /// </summary>
        /// <param name="populations">List of populations (islands)</param>
        /// <param name="config">Island configuration parameters</param>
        /// <param name="generations">Number of generations to evolve</param>
        /// <param name="randomNumberGenerator">Random number generator</param>
        /// <returns>Best individual across all islands</returns>
        public static GeneticIndividual RunIslandEvolution(
            List<List<GeneticIndividual>> populations,
            IslandConfiguration config,
            int generations,
            Random randomNumberGenerator)
        {
            GeneticIndividual globalBest = null;
            var globalBestFitness = double.MinValue;

            for (var generation = 0; generation < generations; generation++)
            {
                // Evolve each island independently (this would be called from main GA loop)
                // Track best individual across all islands
                foreach (var population in populations)
                {
                    var islandBest = population.OrderByDescending(individual => individual.Fitness.FitnessScore).First();
                    if (islandBest.Fitness.FitnessScore > globalBestFitness)
                    {
                        globalBestFitness = islandBest.Fitness.FitnessScore.GetValueOrDefault(0);
                        globalBest = CloneIndividual(islandBest);
                    }
                }

                // Periodic migration between islands
                if (generation % config.MigrationFrequency == 0 && generation > 0)
                    ExchangeBestIndividuals(populations, config, randomNumberGenerator);
            }

            return globalBest;
        }

        /// <summary>
        ///     Exchange best individuals between islands to share genetic material
        /// </summary>
        /// <param name="populations">List of island populations</param>
        /// <param name="config">Island configuration</param>
        /// <param name="randomNumberGenerator">Random number generator</param>
        public static void ExchangeBestIndividuals(
            List<List<GeneticIndividual>> populations,
            IslandConfiguration config,
            Random randomNumberGenerator)
        {
            var migrationCount = (int)(populations[0].Count * config.MigrationRate);

            for (var islandIndex = 0; islandIndex < populations.Count; islandIndex++)
            {
                var sourceIsland = populations[islandIndex];
                var targetIsland = populations[(islandIndex + 1) % populations.Count]; // Next island

                // Select migrants: mix of best performers and random interesting individuals
                var migrants = new List<GeneticIndividual>();

                // Add top performers
                var topPerformers = sourceIsland.OrderByDescending(individual => individual.Fitness.FitnessScore)
                    .Take(migrationCount / 2);
                migrants.AddRange(topPerformers.Select(CloneIndividual));

                // Add random "interesting" individuals
                var interesting = sourceIsland.Where(IsInterestingGenotype)
                    .OrderBy(individual => randomNumberGenerator.Next())
                    .Take(migrationCount - migrants.Count);
                migrants.AddRange(interesting.Select(CloneIndividual));

                // Replace worst individuals in target island
                var worstIndices = targetIsland
                    .Select((individual, index) => new { Individual = individual, Index = index })
                    .OrderBy(item => item.Individual.Fitness.FitnessScore)
                    .Take(migrants.Count)
                    .Select(item => item.Index)
                    .OrderByDescending(index => index)
                    .ToList();

                for (var migrantIndex = 0;
                     migrantIndex < migrants.Count && migrantIndex < worstIndices.Count;
                     migrantIndex++) targetIsland[worstIndices[migrantIndex]] = migrants[migrantIndex];
            }
        }

        #endregion

        #region Fitness Sharing & Niching

        /// <summary>
        ///     Calculate shared fitness to promote diversity by penalizing similar individuals
        /// </summary>
        /// <param name="individual">Individual to calculate shared fitness for</param>
        /// <param name="population">Current population</param>
        /// <param name="diversityThreshold">Similarity threshold for niching</param>
        /// <returns>Shared fitness value</returns>
        public static double CalculateSharedFitness(
            GeneticIndividual individual,
            List<GeneticIndividual> population,
            double diversityThreshold = 0.8)
        {
            // Prefer FitnessScore when present; fall back to PercentGain for tests/legacy callers
            var rawFitness = individual.Fitness != null && individual.Fitness.FitnessScore.HasValue
                ? individual.Fitness.FitnessScore.Value
                : (individual.Fitness != null ? individual.Fitness.PercentGain : 0.0);

            var nichingPenalty = 0.0;

            // Penalize similar individuals to promote diversity
            foreach (var other in population)
            {
                if (individual == other) continue;

                var similarity = CalculateGenotypeSimilarity(individual, other);
                if (similarity > diversityThreshold) // Very similar
                    nichingPenalty += 0.1 * similarity;
            }

            return rawFitness - nichingPenalty;
        }

        /// <summary>
        ///     Calculate similarity between two genetic individuals based on their genotype
        /// </summary>
        /// <param name="individual1">First individual</param>
        /// <param name="individual2">Second individual</param>
        /// <returns>Similarity score (0.0 = completely different, 1.0 = identical)</returns>
        public static double CalculateGenotypeSimilarity(GeneticIndividual individual1, GeneticIndividual individual2)
        {
            var similarity = 0.0;

            // Compare indicator configurations (40% weight)
            similarity += 0.4 * CompareIndicatorSets(individual1.Indicators, individual2.Indicators);

            // Compare trading parameters (60% weight)
            if (Math.Abs(individual1.TradePercentageForStocks - individual2.TradePercentageForStocks) < 0.005) similarity += 0.15;
            if (individual1.AllowedTradeTypes == individual2.AllowedTradeTypes) similarity += 0.15;
            if (individual1.AllowedSecurityTypes == individual2.AllowedSecurityTypes) similarity += 0.15;
            if (individual1.CombinationMethod == individual2.CombinationMethod) similarity += 0.15;

            return Math.Min(1.0, similarity);
        }

        /// <summary>
        ///     Compare two sets of indicators and return similarity score
        /// </summary>
        public static double CompareIndicatorSets(List<IndicatorParams> indicators1, List<IndicatorParams> indicators2)
        {
            if (indicators1.Count == 0 && indicators2.Count == 0) return 1.0;
            if (indicators1.Count == 0 || indicators2.Count == 0) return 0.0;

            // Compare indicator types
            var types1 = indicators1.Select(indicator => indicator.Type).OrderBy(type => type).ToList();
            var types2 = indicators2.Select(indicator => indicator.Type).OrderBy(type => type).ToList();

            var commonTypes = types1.Intersect(types2).Count();
            var totalUniqueTypes = types1.Union(types2).Count();

            var typeSimilarity = totalUniqueTypes > 0 ? (double)commonTypes / totalUniqueTypes : 0.0;

            // Compare periods and trade modes (if same types)
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

                    // Reward same TradeMode, 1.0 if equal else 0.0
                    modeSimilarity = matchingPairs.Average(pair => pair.TradeMode == pair.OtherMode ? 1.0 : 0.0);
                }
            }

            return (typeSimilarity + periodSimilarity + modeSimilarity) / 3.0;
        }

        #endregion

        #region Adaptive Selection Pressure

        /// <summary>
        ///     Perform tournament selection with adaptive pressure
        /// </summary>
        /// <param name="population">Population to select from</param>
        /// <param name="selectionPressure">Selection pressure (0.0 = random, 1.0 = always best)</param>
        /// <param name="baseTournamentSize">Base tournament size</param>
        /// <param name="randomNumberGenerator">Random number generator</param>
        /// <returns>Selected individual</returns>
        public static GeneticIndividual AdaptiveTournamentSelect(
            List<GeneticIndividual> population,
            double selectionPressure,
            int baseTournamentSize,
            Random randomNumberGenerator)
        {
            var tournamentSize = (int)Math.Ceiling(baseTournamentSize * selectionPressure);
            tournamentSize = Math.Max(2, Math.Min(tournamentSize, population.Count));

            // Sometimes select randomly regardless of fitness (preserve diversity)
            if (randomNumberGenerator.NextDouble() < 1.0 - selectionPressure)
                return population[randomNumberGenerator.Next(population.Count)];

            return TournamentSelect(population, tournamentSize, randomNumberGenerator);
        }

        /// <summary>
        ///     Standard tournament selection
        /// </summary>
        /// <param name="population">Population to select from</param>
        /// <param name="tournamentSize">Tournament size</param>
        /// <param name="randomNumberGenerator">Random number generator</param>
        /// <returns>Selected individual</returns>
        public static GeneticIndividual TournamentSelect(List<GeneticIndividual> population, int tournamentSize,
            Random randomNumberGenerator)
        {
            var selected = new List<GeneticIndividual>();
            for (var tournamentIndex = 0; tournamentIndex < tournamentSize; tournamentIndex++)
                selected.Add(population[randomNumberGenerator.Next(population.Count)]);
            return selected.OrderByDescending(individual => individual.Fitness.FitnessScore).First();
        }

        #endregion

        #region Schema Preservation

        /// <summary>
        ///     Analyze and preserve genetic schemas (building blocks) in the population
        /// </summary>
        /// <param name="population">Current population</param>
        /// <param name="genePool">Historical gene pool to update</param>
        public static void AnalyzeAndPreserveSchemas(List<GeneticIndividual> population, HistoricalGenePool genePool)
        {
            genePool.SchemaFrequency.Clear();

            foreach (var individual in population)
            {
                // Extract "building blocks" - patterns that might be important
                var schemas = ExtractSchemas(individual);

                foreach (var schema in schemas)
                    // Fixed: Use proper dictionary access for .NET Framework 4.7.2
                    if (genePool.SchemaFrequency.ContainsKey(schema))
                        genePool.SchemaFrequency[schema]++;
                    else
                        genePool.SchemaFrequency[schema] = 1;
            }

            // Identify rare but potentially valuable schemas
            var rareSchemas = genePool.SchemaFrequency.Where(keyValuePair => keyValuePair.Value <= 2)
                .Select(keyValuePair => keyValuePair.Key).ToList();

            // Protect individuals carrying rare schemas from elimination
            foreach (var individual in population)
                if (CarriesRareSchema(individual, rareSchemas))
                    individual.Fitness = new Fitness(
                        individual.Fitness.DollarGain,
                        individual.Fitness.PercentGain + 1.0, individual.Fitness.FitnessScore.GetValueOrDefault(0)); // Small bonus to prevent elimination
        }

        /// <summary>
        ///     Extract genetic schemas (patterns) from an individual
        /// </summary>
        /// <param name="individual">Individual to extract schemas from</param>
        /// <returns>List of schema strings</returns>
        public static List<string> ExtractSchemas(GeneticIndividual individual)
        {
            var schemas = new List<string>();

            // Extract indicator type patterns
            var indicatorPattern = string.Join(",",
                individual.Indicators.Select(indicator => indicator.Type).OrderBy(type => type));
            schemas.Add($"TYPES:{indicatorPattern}");

            // Extract timeframe patterns  
            var timeframePattern = string.Join(",",
                individual.Indicators.Select(indicator => (int)indicator.TimeFrame).Distinct()
                    .OrderBy(timeFrame => timeFrame));
            schemas.Add($"TIMEFRAMES:{timeframePattern}");

            // Extract trading style patterns
            schemas.Add(
                $"STYLE:{individual.AllowedTradeTypes}_{individual.AllowedSecurityTypes}_{individual.TradePercentageForStocks:F2}");

            // Extract complexity patterns
            schemas.Add($"COMPLEXITY:{individual.Indicators.Count}_{individual.CombinationMethod}");

            // NEW: add TradeMode schema summary
            var deltaCount = individual.Indicators.Count(ind => ind.TradeMode == IndicatorTradeMode.Delta);
            var rangeCount = individual.Indicators.Count(ind => ind.TradeMode == IndicatorTradeMode.Range);
            schemas.Add($"TRADEMODE:D{deltaCount}_R{rangeCount}");

            return schemas;
        }

        /// <summary>
        ///     Check if an individual carries any rare schemas
        /// </summary>
        /// <param name="individual">Individual to check</param>
        /// <param name="rareSchemas">List of rare schema patterns</param>
        /// <returns>True if individual carries rare schemas</returns>
        public static bool CarriesRareSchema(GeneticIndividual individual, List<string> rareSchemas)
        {
            var individualSchemas = ExtractSchemas(individual);
            return individualSchemas.Any(schema => rareSchemas.Contains(schema));
        }

        #endregion

        #region Historical Gene Pool Management

        /// <summary>
        ///     Archive interesting individuals for future genetic diversity injection
        /// </summary>
        /// <param name="population">Current population</param>
        /// <param name="genePool">Historical gene pool to update</param>
        /// <param name="generation">Current generation number</param>
        public static void ArchiveInterestingIndividuals(
            List<GeneticIndividual> population,
            HistoricalGenePool genePool,
            int generation)
        {
            // Preserve top 10% performers
            var elite = population.OrderByDescending(individual => individual.Fitness.FitnessScore)
                .Take(population.Count / 10)
                .Select(CloneIndividual);

            // Preserve diverse/unusual individuals
            var diverse = population.Where(IsInterestingGenotype)
                .Take(5)
                .Select(CloneIndividual);

            genePool.HallOfFame.AddRange(elite);
            genePool.InterestingMutants.AddRange(diverse);

            // Limit archive size
            if (genePool.HallOfFame.Count > genePool.MaxArchiveSize)
                genePool.HallOfFame = genePool.HallOfFame
                    .OrderByDescending(individual => individual.Fitness.FitnessScore)
                    .Take(genePool.MaxArchiveSize)
                    .ToList();

            if (genePool.InterestingMutants.Count > genePool.MaxArchiveSize / 2)
                genePool.InterestingMutants = genePool.InterestingMutants
                    .OrderBy(individual => Guid.NewGuid()) // Random selection
                    .Take(genePool.MaxArchiveSize / 2)
                    .ToList();
        }

        /// <summary>
        ///     Inject archived genetics back into population for diversity
        /// </summary>
        /// <param name="population">Current population to inject into</param>
        /// <param name="genePool">Historical gene pool</param>
        /// <param name="injectionRate">Percentage of population to replace (0.0-1.0)</param>
        /// <param name="randomNumberGenerator">Random number generator</param>
        public static void InjectArchivedGenetics(
            List<GeneticIndividual> population,
            HistoricalGenePool genePool,
            double injectionRate,
            Random randomNumberGenerator)
        {
            if (genePool.HallOfFame.Count == 0 && genePool.InterestingMutants.Count == 0) return;

            var injectCount = (int)(population.Count * injectionRate);
            var worstIndices = population
                .Select((individual, index) => new { Individual = individual, Index = index })
                .OrderBy(item => item.Individual.Fitness.FitnessScore)
                .Take(injectCount)
                .Select(item => item.Index)
                .ToList();

            var availableArchive = genePool.HallOfFame.Concat(genePool.InterestingMutants).ToList();

            for (var injectionIndex = 0; injectionIndex < Math.Min(injectCount, worstIndices.Count); injectionIndex++)
                if (availableArchive.Count > 0)
                {
                    var archivedIndividual = availableArchive[randomNumberGenerator.Next(availableArchive.Count)];
                    population[worstIndices[injectionIndex]] = CloneIndividual(archivedIndividual);
                }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        ///     Determine if an individual has an interesting genotype worth preserving
        /// </summary>
        /// <param name="individual">Individual to evaluate</param>
        /// <returns>True if genotype is interesting</returns>
        public static bool IsInterestingGenotype(GeneticIndividual individual)
        {
            // Examples of "interesting" combinations:
            return individual.Indicators.Count > 3 || // Complex strategies
                   individual.TradePercentageForStocks > 0.08 || // Aggressive sizing
                   individual.AllowedSecurityTypes == AllowedSecurityType.Option || // Options traders
                   HasUnusualIndicatorCombination(individual) || // Rare combinations
                   individual.CombinationMethod == CombinationMethod.EnsembleVoting; // Advanced combination
        }

        /// <summary>
        ///     Check if individual has unusual indicator combinations
        /// </summary>
        /// <param name="individual">Individual to check</param>
        /// <returns>True if has unusual combinations</returns>
        public static bool HasUnusualIndicatorCombination(GeneticIndividual individual)
        {
            var types = individual.Indicators.Select(indicator => indicator.Type).Distinct().ToList();

            // Examples of "interesting" combinations:
            return (types.Contains(0) && types.Contains(16)) || // Sin + CCI
                   (types.Contains(5) && types.Contains(14)) || // ATR + Awesome Oscillator  
                   individual.Indicators.Any(indicator => indicator.Period > 100) || // Long period indicators
                   individual.Indicators.Count >= 4; // High complexity
        }

        /// <summary>
        ///     Clone a genetic individual (deep copy)
        /// </summary>
        /// <param name="original">Original individual to clone</param>
        /// <returns>Deep copy of the individual</returns>
        public static GeneticIndividual CloneIndividual(GeneticIndividual original)
        {
            var clone = new GeneticIndividual();
            clone.RandomNumberGenerator = original.RandomNumberGenerator;
            //clone.Fitness = new Fitness(original.Fitness.DollarGain, original.Fitness.PercentGain);
            clone.StartingBalance = original.StartingBalance;

            // Clone genetic parameters for multiple indicator support
            clone.AllowMultipleTrades = original.AllowMultipleTrades;
            clone.CombinationMethod = original.CombinationMethod;
            clone.EnsembleVotingThreshold = original.EnsembleVotingThreshold;

            // Clone trading and option genetic parameters
            clone.AllowedTradeTypes = original.AllowedTradeTypes;
            clone.AllowedOptionTypes = original.AllowedOptionTypes;
            clone.AllowedSecurityTypes = original.AllowedSecurityTypes;
            clone.NumberOfOptionContractsToOpen = original.NumberOfOptionContractsToOpen;
            clone.OptionDaysOut = original.OptionDaysOut;
            clone.OptionStrikeDistance = original.OptionStrikeDistance;
            clone.OptionContractsToScaleOut = (double[])original.OptionContractsToScaleOut.Clone();

            // Clone trade percentage
            clone.TradePercentageForStocks = original.TradePercentageForStocks;

            // Clone FastMAPeriod and SlowMAPeriod
            clone.FastMAPeriod = original.FastMAPeriod;
            clone.SlowMAPeriod = original.SlowMAPeriod;

            // NEW: clone top-level OHLC and BufferSource
            clone.OHLC = original.OHLC;
            clone.BufferSource = original.BufferSource;

            foreach (var indicator in original.Indicators)
                clone.Indicators.Add(CloneIndicatorParams(indicator));

            return clone;
        }

        /// <summary>
        ///     Clone indicator parameters (deep copy)
        /// </summary>
        /// <param name="original">Original indicator parameters</param>
        /// <returns>Deep copy of indicator parameters</returns>
        public static IndicatorParams CloneIndicatorParams(IndicatorParams original)
        {
            return new IndicatorParams
            {
                Type = original.Type,
                Period = original.Period,
                Mode = original.Mode,
                TimeFrame = original.TimeFrame,
                OHLC = original.OHLC,
                Polarity = original.Polarity,
                LongThreshold = original.LongThreshold,
                ShortThreshold = original.ShortThreshold,
                Param1 = original.Param1,
                Param2 = original.Param2,
                Param3 = original.Param3,
                Param4 = original.Param4,
                Param5 = original.Param5,
                DebugCase = original.DebugCase,
                FastMAPeriod = original.FastMAPeriod,
                SlowMAPeriod = original.SlowMAPeriod,
                // NEW: clone trade mode
                TradeMode = original.TradeMode,
                // NEW: clone buffer source
                BufferSource = original.BufferSource
            };
        }

        /// <summary>
        ///     Calculate gradual selection pressure based on generation
        /// </summary>
        /// <param name="currentGeneration">Current generation number</param>
        /// <param name="totalGenerations">Total number of generations</param>
        /// <param name="minPressure">Minimum selection pressure</param>
        /// <param name="maxPressure">Maximum selection pressure</param>
        /// <returns>Selection pressure for current generation</returns>
        public static double CalculateGradualSelectionPressure(
            int currentGeneration,
            int totalGenerations,
            double minPressure = 0.3,
            double maxPressure = 0.8)
        {
            var progress = (double)currentGeneration / Math.Max(1, totalGenerations - 1);
            return minPressure + (maxPressure - minPressure) * progress;
        }

        #endregion
    }
}