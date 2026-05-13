//public class TechnicalAnalysisGeneticSolver : GeneticSolver
//{
//    private GeneticEvolvers.HistoricalGenePool _genePool = new GeneticEvolvers.HistoricalGenePool();

//    protected override void EvolveGeneration(int generation)
//    {
//        // 1. SCHEMA PRESERVATION (Tier 1)
//        GeneticEvolvers.AnalyzeAndPreserveSchemas(population, _genePool);

//        // 2. ADAPTIVE SELECTION (Tier 1) 
//        var pressure = GeneticEvolvers.CalculateGradualSelectionPressure(generation, totalGenerations);

//        // 3. FITNESS SHARING (Tier 2)
//        foreach (var individual in population)
//        {
//            individual.Fitness.PercentGain = GeneticEvolvers.CalculateSharedFitness(
//                individual, population, 0.75);
//        }

//        // 4. HISTORICAL PRESERVATION (Tier 1)
//        GeneticEvolvers.ArchiveInterestingIndividuals(population, _genePool, generation);

//        // 5. DIVERSITY INJECTION (Tier 1)
//        if (generation % 15 == 0) // Every 15 generations
//        {
//            GeneticEvolvers.InjectArchivedGenetics(population, _genePool, 0.1, rng);
//        }

//        // Standard evolution with adaptive pressure
//        var newPopulation = new List<GeneticIndividual>();
//        while (newPopulation.Count < populationSize)
//        {
//            var parent1 = GeneticEvolvers.AdaptiveTournamentSelect(population, pressure, tournamentSize, rng);
//            var parent2 = GeneticEvolvers.AdaptiveTournamentSelect(population, pressure, tournamentSize, rng);
//            var offspring = Crossover(parent1, parent2);
//            Mutate(offspring);
//            newPopulation.Add(offspring);
//        }
//        population = newPopulation;
//    }
//}

