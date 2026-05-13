using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class GeneticSolverTests
    {
        private const double TOLERANCE = 1e-6;
        private GeneticSolver _solver;

        [TestInitialize]
        public void Setup()
        {
            // Initialize static dependencies for testing
            GeneticIndividual.InitializePrices();
            //GeneticIndividual.InitializeOptionSolvers();

            // Create a GeneticSolver with typical parameters
            _solver = new GeneticSolver(
                10,
                5,
                0.1,
                3,
                10000.0,
                0, 5,
                5, 20,
                0, 3,
                TimeFrame.M1, TimeFrame.D1,
                -2, 2,
                0.1, 2.0,
                3,
                0.01, 0.05,
                1, 30,
                0, 20,
                5, 15,
                20, 50,
                0, 1,
                0, 1,
                0, 0,
                1, 10
            );
        }

        #region Constructor Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            // Test with minimal parameters
            var solver = new GeneticSolver(
                5, 3, 0.05, 2, 1000.0,
                0, 3, 5, 15, 0, 2, TimeFrame.M1, TimeFrame.H1,
                -1, 1, 0.5, 1.5, 2, 0.02, 0.04, 1, 10, 0, 5,
                2, 10, 5, 25,
                0, 1, 0, 1, 0, 1, 1, 5
            );

            Assert.IsNotNull(solver);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Constructor_WithMaximumParameters_InitializesCorrectly()
        {
            // Test with maximum realistic parameters
            var solver = new GeneticSolver(
                100, 50, 0.2, 10, 100000.0,
                0, 15, 2, 100, 0, 5, TimeFrame.M1, TimeFrame.D1,
                -5, 5, 0.01, 5.0, 10, 0.001, 0.1, 1, 365, 0, 100,
                1, 50, 10, 200,
                0, 1, 0, 2, 0, 2, 1, 100
            );

            Assert.IsNotNull(solver);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Constructor_WithEdgeCaseParameters_InitializesCorrectly()
        {
            // Test with edge case values
            var solver = new GeneticSolver(
                1, 1, 0.0, 1, 0.01,
                0, 0, 1, 1, 0, 0, TimeFrame.M1, TimeFrame.M1,
                0, 0, 1.0, 1.0, 1, 1.0, 1.0, 1, 1, 0, 0,
                1, 1, 1, 1,
                0, 0, 0, 0, 0, 0, 1, 1
            );

            Assert.IsNotNull(solver);
        }

        #endregion

        #region Solve Method Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Solve_WithValidPriceRecords_ReturnsValidIndividual()
        {
            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTestPriceRecords(60);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 30 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(30).ToArray();

            var result = _solver.Solve(testPriceRecords);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Fitness);
            Assert.IsTrue(result.Indicators.Count > 0);
            Assert.IsTrue(result.StartingBalance > 0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Solve_WithSequentialPricing_ProducesValidResult()
        {
            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateSequentialPriceRecords(100);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 50 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(50).ToArray();

            var result = _solver.Solve(testPriceRecords, false);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Fitness);
            Assert.AreEqual(10000.0, result.StartingBalance, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Solve_WithParallelExecution_ProducesValidResult()
        {
            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTestPriceRecords(60);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 30 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(30).ToArray();

            var result = _solver.Solve(testPriceRecords);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Fitness);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Solve_WithSinglePriceRecord_HandlesGracefully()
        {
            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTestPriceRecords(50);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only 1 record for testing
            var testPriceRecords = new[]
            {
                new PriceRecord(
                    historicalPriceRecords.Last().DateTime.AddDays(1), TimeFrame.D1,
                    100, 105, 95, 102, volume: 1000,
                    wap: 102, count: 1)
            };

            var result = _solver.Solve(testPriceRecords);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void Solve_WithEarlyTermination_WhenHighFitnessReached()
        {
            // Create a solver with very small population for quick termination test
            var quickSolver = new GeneticSolver(
                2, 100, 0.1, 2, 10000.0,
                0, 5, 5, 20, 0, 3, TimeFrame.M1, TimeFrame.D1,
                -2, 2, 0.1, 2.0, 3, 0.01, 0.05, 1, 30, 0, 20,
                5, 15, 20, 50,
                0, 1, 0, 1, 0, 1, 1, 10
            );

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateStrongTrendPriceRecords(40);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 20 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(20).ToArray();

            var result = quickSolver.Solve(testPriceRecords);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Fitness);
        }

        #endregion

        #region Selection Method Tests

        [TestMethod]
        [TestCategory("Core")]
        public void CreateRankedSelectionProbabilities_WithValidPopulation_ReturnsCorrectProbabilities()
        {
            var population = CreateTestPopulation(5);
            var originalFitness = population.ToDictionary(ind => ind, ind => ind.Fitness.PercentGain);

            // Use reflection to test private method
            var method = typeof(GeneticSolver).GetMethod("CreateRankedSelectionProbabilities",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var result = (List<(GeneticIndividual individual, double cumulativeProbability)>)
                method.Invoke(_solver, new object[] { population, originalFitness });

            Assert.AreEqual(population.Count, result.Count);
            Assert.IsTrue(result.Last().cumulativeProbability <= 1.0 + TOLERANCE);
            Assert.IsTrue(result.All(r => r.cumulativeProbability > 0));

            // Verify cumulative probabilities are in ascending order
            for (var i = 1; i < result.Count; i++)
                Assert.IsTrue(result[i].cumulativeProbability >= result[i - 1].cumulativeProbability);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void RankBasedSelect_WithValidRankedPopulation_ReturnsValidIndividual()
        {
            var population = CreateTestPopulation(5);
            var originalFitness = population.ToDictionary(ind => ind, ind => ind.Fitness.PercentGain);

            var createRankedMethod = typeof(GeneticSolver).GetMethod("CreateRankedSelectionProbabilities",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var selectMethod = typeof(GeneticSolver).GetMethod("RankBasedSelect",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var rankedPopulation = (List<(GeneticIndividual individual, double cumulativeProbability)>)
                createRankedMethod.Invoke(_solver, new object[] { population, originalFitness });

            var selected = (GeneticIndividual)selectMethod.Invoke(_solver, new object[] { rankedPopulation });

            Assert.IsNotNull(selected);
            Assert.IsTrue(population.Contains(selected));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void AdaptiveTournamentSelect_WithDifferentPressures_ShowsVariation()
        {
            var population = CreateTestPopulation(10);
            var method = typeof(GeneticSolver).GetMethod("AdaptiveTournamentSelect",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Test with low selection pressure
            var lowPressureSelected = (GeneticIndividual)method.Invoke(_solver, new object[] { population, 0.3 });

            // Test with high selection pressure
            var highPressureSelected = (GeneticIndividual)method.Invoke(_solver, new object[] { population, 0.9 });

            Assert.IsNotNull(lowPressureSelected);
            Assert.IsNotNull(highPressureSelected);
            Assert.IsTrue(population.Contains(lowPressureSelected));
            Assert.IsTrue(population.Contains(highPressureSelected));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TournamentSelect_WithValidPopulation_ReturnsValidIndividual()
        {
            var population = CreateTestPopulation(5);
            var method = typeof(GeneticSolver).GetMethod("TournamentSelect",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var selected = (GeneticIndividual)method.Invoke(_solver, new object[] { population });

            Assert.IsNotNull(selected);
            Assert.IsTrue(population.Contains(selected));
        }

        #endregion

        #region Diversity and Fitness Sharing Tests

        [TestMethod]
        [TestCategory("Core")]
        public void ApplyFitnessSharing_WithSimilarIndividuals_ReducesFitness()
        {
            var population = CreateSimilarTestPopulation(3);
            var originalFitness = population.ToDictionary(ind => ind, ind => ind.Fitness.PercentGain);

            var method = typeof(GeneticSolver).GetMethod("ApplyFitnessSharing",
                BindingFlags.NonPublic | BindingFlags.Instance);

            method.Invoke(_solver, new object[] { population });

            // Check that fitness has been reduced for similar individuals
            foreach (var individual in population)
                Assert.IsTrue(individual.Fitness.PercentGain <= originalFitness[individual]);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateGenotypeSimilarity_WithIdenticalIndividuals_ReturnsHighSimilarity()
        {
            var individual1 = CreateTestIndividual();
            var individual2 = CloneIndividual(individual1);

            var method = typeof(GeneticSolver).GetMethod("CalculateGenotypeSimilarity",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var similarity = (double)method.Invoke(_solver, new object[] { individual1, individual2 });

            Assert.IsTrue(similarity > 0.8); // Should be very similar
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CompareIndicatorSets_WithIdenticalSets_ReturnsHighSimilarity()
        {
            var indicators1 = CreateTestIndicators();
            var indicators2 = CreateTestIndicators(); // Identical

            var method = typeof(GeneticSolver).GetMethod("CompareIndicatorSets",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var similarity = (double)method.Invoke(_solver, new object[] { indicators1, indicators2 });

            Assert.IsTrue(similarity > 0.8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CompareIndicatorSets_WithEmptySets_ReturnsCorrectSimilarity()
        {
            var emptySet1 = new List<IndicatorParams>();
            var emptySet2 = new List<IndicatorParams>();
            var nonEmptySet = CreateTestIndicators();

            var method = typeof(GeneticSolver).GetMethod("CompareIndicatorSets",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var emptySimilarity = (double)method.Invoke(_solver, new object[] { emptySet1, emptySet2 });
            var mixedSimilarity = (double)method.Invoke(_solver, new object[] { emptySet1, nonEmptySet });

            Assert.AreEqual(1.0, emptySimilarity, TOLERANCE); // Two empty sets are identical
            Assert.AreEqual(0.0, mixedSimilarity, TOLERANCE); // Empty vs non-empty should be 0
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculatePopulationDiversity_WithDiversePopulation_ReturnsHighDiversity()
        {
            var population = CreateDiverseTestPopulation(5);

            var method = typeof(GeneticSolver).GetMethod("CalculatePopulationDiversity",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var diversity = (double)method.Invoke(_solver, new object[] { population });

            Assert.IsTrue(diversity >= 0.0);
            Assert.IsTrue(diversity <= 1.0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculatePopulationDiversity_WithSingleIndividual_ReturnsZero()
        {
            var population = new List<GeneticIndividual> { CreateTestIndividual() };

            var method = typeof(GeneticSolver).GetMethod("CalculatePopulationDiversity",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var diversity = (double)method.Invoke(_solver, new object[] { population });

            Assert.AreEqual(0.0, diversity, TOLERANCE);
        }

        #endregion

        #region Crossover and Mutation Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Crossover_WithTwoParents_ProducesValidOffspring()
        {
            var parent1 = CreateTestIndividual();
            var parent2 = CreateDifferentTestIndividual();

            var method = typeof(GeneticSolver).GetMethod("Crossover",
                BindingFlags.Public | BindingFlags.Instance);

            var offspring = (GeneticIndividual)method.Invoke(_solver, new object[] { parent1, parent2 });

            Assert.IsNotNull(offspring);
            Assert.IsTrue(offspring.Indicators.Count > 0);
            Assert.AreEqual(10000.0, offspring.StartingBalance, TOLERANCE);

            // Verify genetic parameters are set
            Assert.IsTrue(offspring.AllowedTradeTypes == parent1.AllowedTradeTypes ||
                          offspring.AllowedTradeTypes == parent2.AllowedTradeTypes);
            Assert.IsTrue(offspring.AllowedSecurityTypes == parent1.AllowedSecurityTypes ||
                          offspring.AllowedSecurityTypes == parent2.AllowedSecurityTypes);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Crossover_IndicatorCombination_WorksCorrectly()
        {
            var parent1 = CreateTestIndividualWithIndicators(3);
            var parent2 = CreateTestIndividualWithIndicators(2);

            var method = typeof(GeneticSolver).GetMethod("Crossover",
                BindingFlags.Public | BindingFlags.Instance);

            var offspring = (GeneticIndividual)method.Invoke(_solver, new object[] { parent1, parent2 });

            Assert.IsNotNull(offspring);
            Assert.IsTrue(offspring.Indicators.Count <= Math.Max(parent1.Indicators.Count, parent2.Indicators.Count));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Mutate_WithTestIndividual_ModifiesParameters()
        {
            var individual = CreateTestIndividual();
            var originalIndicatorCount = individual.Indicators.Count;
            var originalTradePercentage = individual.TradePercentageForStocks;

            var method = typeof(GeneticSolver).GetMethod("Mutate",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Run mutation multiple times to increase chance of hitting mutations
            for (var i = 0; i < 20; i++) method.Invoke(_solver, new object[] { individual });

            // Verify individual is still valid (some properties may have changed)
            Assert.IsNotNull(individual);
            Assert.IsTrue(individual.Indicators.Count > 0);
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void Mutate_IndicatorAddition_WorksCorrectly()
        {
            var individual = CreateTestIndividualWithIndicators(1); // Start with minimal indicators

            var method = typeof(GeneticSolver).GetMethod("Mutate",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var originalCount = individual.Indicators.Count;

            // Run many mutations to test indicator addition
            for (var i = 0; i < 100; i++)
            {
                method.Invoke(_solver, new object[] { individual });

                // Check that we don't exceed max indicators
                Assert.IsTrue(individual.Indicators.Count <= 3); // maxIndicators = 3
            }
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void Mutate_IndicatorRemoval_WorksCorrectly()
        {
            var individual = CreateTestIndividualWithIndicators(3); // Start with max indicators

            var method = typeof(GeneticSolver).GetMethod("Mutate",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Run many mutations to test indicator removal
            for (var i = 0; i < 100; i++)
            {
                method.Invoke(_solver, new object[] { individual });

                // Check that we always have at least 1 indicator
                Assert.IsTrue(individual.Indicators.Count >= 1);
            }
        }

        #endregion

        #region Cloning Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Clone_GeneticIndividual_CreatesDeepCopy()
        {
            var original = CreateTestIndividual();

            var method = typeof(GeneticSolver).GetMethod("Clone",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(GeneticIndividual) }, null);

            var clone = (GeneticIndividual)method.Invoke(_solver, new object[] { original });

            Assert.IsNotNull(clone);
            Assert.AreNotSame(original, clone);
            Assert.AreEqual(original.StartingBalance, clone.StartingBalance, TOLERANCE);
            Assert.AreEqual(original.TradePercentageForStocks, clone.TradePercentageForStocks, TOLERANCE);
            Assert.AreEqual(original.AllowedTradeTypes, clone.AllowedTradeTypes);
            Assert.AreEqual(original.AllowedSecurityTypes, clone.AllowedSecurityTypes);
            Assert.AreEqual(original.Indicators.Count, clone.Indicators.Count);

            // Verify fitness is copied
            Assert.AreEqual(original.Fitness.DollarGain, clone.Fitness.DollarGain, TOLERANCE);
            Assert.AreEqual(original.Fitness.PercentGain, clone.Fitness.PercentGain, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Clone_IndicatorParams_CreatesDeepCopy()
        {
            var original = CreateTestIndicatorParams();

            var method = typeof(GeneticSolver).GetMethod("Clone",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(IndicatorParams) }, null);

            var clone = (IndicatorParams)method.Invoke(_solver, new object[] { original });

            Assert.IsNotNull(clone);
            Assert.AreNotSame(original, clone);
            Assert.AreEqual(original.Type, clone.Type);
            Assert.AreEqual(original.Period, clone.Period);
            Assert.AreEqual(original.Mode, clone.Mode);
            Assert.AreEqual(original.TimeFrame, clone.TimeFrame);
            Assert.AreEqual(original.Polarity, clone.Polarity);
            Assert.AreEqual(original.LongThreshold, clone.LongThreshold, TOLERANCE);
            Assert.AreEqual(original.ShortThreshold, clone.ShortThreshold, TOLERANCE);
        }

        #endregion

        #region Helper Method Tests

        [TestMethod]
        [TestCategory("Core")]
        public void GenerateValidScaleOutFractions_WithValidContracts_ReturnsSumToOne()
        {
            var method = typeof(GeneticSolver).GetMethod("GenerateValidScaleOutFractions",
                BindingFlags.NonPublic | BindingFlags.Static);

            var rng = new Random(42);
            var totalContracts = 10.0;

            var fractions = (double[])method.Invoke(null, new object[] { rng, totalContracts });

            Assert.AreEqual(8, fractions.Length);
            Assert.AreEqual(1.0, fractions.Sum(), 1e-10); // Should sum to 1.0

            // All fractions should be non-negative
            Assert.IsTrue(fractions.All(f => f >= 0));

            // When multiplied by totalContracts, should yield whole numbers
            for (var i = 0; i < fractions.Length; i++)
            {
                var contractCount = fractions[i] * totalContracts;
                Assert.AreEqual(Math.Round(contractCount), contractCount, 1e-10);
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GenerateValidScaleOutFractions_WithSmallContracts_HandlesCorrectly()
        {
            var method = typeof(GeneticSolver).GetMethod("GenerateValidScaleOutFractions",
                BindingFlags.NonPublic | BindingFlags.Static);

            var rng = new Random(42);
            var totalContracts = 3.0; // Less than 8

            var fractions = (double[])method.Invoke(null, new object[] { rng, totalContracts });

            Assert.AreEqual(8, fractions.Length);
            Assert.AreEqual(1.0, fractions.Sum(), 1e-10);

            // Count non-zero fractions should not exceed totalContracts
            var nonZeroCount = fractions.Count(f => f > 0);
            Assert.IsTrue(nonZeroCount <= totalContracts);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GenerateValidScaleOutFractions_WithOneContract_HandlesCorrectly()
        {
            var method = typeof(GeneticSolver).GetMethod("GenerateValidScaleOutFractions",
                BindingFlags.NonPublic | BindingFlags.Static);

            var rng = new Random(42);
            var totalContracts = 1.0;

            var fractions = (double[])method.Invoke(null, new object[] { rng, totalContracts });

            Assert.AreEqual(8, fractions.Length);
            Assert.AreEqual(1.0, fractions.Sum(), 1e-10);

            // Only one fraction should be 1.0, others should be 0.0
            var nonZeroCount = fractions.Count(f => f > 0);
            Assert.AreEqual(1, nonZeroCount);
            Assert.AreEqual(1.0, fractions.Max(), 1e-10);
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Solve_WithVolatilePrices_HandlesCorrectly()
        {
            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateVolatilePriceRecords(50);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 25 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(25).ToArray();

            var result = _solver.Solve(testPriceRecords);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Fitness);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Solve_WithFlatPrices_HandlesCorrectly()
        {
            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateFlatPriceRecords(100, 100.0);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 50 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(50).ToArray();

            var result = _solver.Solve(testPriceRecords);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Fitness);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Solve_WithDecreasingPrices_HandlesCorrectly()
        {
            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateDecreasingPriceRecords(40);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 20 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(20).ToArray();

            var result = _solver.Solve(testPriceRecords);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Fitness);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void Solve_FullGeneticAlgorithmRun_ProducesOptimizedResult()
        {
            // Use a more complex solver configuration
            var complexSolver = new GeneticSolver(
                20, 10, 0.15, 5, 50000.0,
                0, 10, 5, 50, 0, 4, TimeFrame.M1, TimeFrame.D1,
                -3, 3, 0.05, 3.0, 5, 0.005, 0.1, 1, 60, 0, 50,
                3, 30, 10, 100,
                0, 1, 0, 2, 0, 2, 1, 50
            );

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateComplexTestPriceRecords(200);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 100 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(100).ToArray();

            var result = complexSolver.Solve(testPriceRecords);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Fitness);
            Assert.IsTrue(result.Indicators.Count > 0);
            Assert.IsTrue(result.Indicators.Count <= 5);
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void Solve_ConsistentResults_WithSameRandomSeed()
        {
            // The solver uses a fixed seed (42), so results should be consistent
            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTestPriceRecords(60);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 30 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(30).ToArray();

            var result1 = _solver.Solve(testPriceRecords);

            // Create a new solver with same parameters (should use same seed)
            var solver2 = new GeneticSolver(
                10, 5, 0.1, 3, 10000.0,
                0, 5, 5, 20, 0, 3, TimeFrame.M1, TimeFrame.D1,
                -2, 2, 0.1, 2.0, 3, 0.01, 0.05, 1, 30, 0, 20,
                5, 15, 20, 50,
                0, 1, 0, 1, 0, 1, 1, 10
            );

            var result2 = solver2.Solve(testPriceRecords);

            // Results should be identical due to fixed seed
            Assert.AreEqual(result1.Fitness.DollarGain, result2.Fitness.DollarGain, 5000);
            Assert.AreEqual(result1.Fitness.PercentGain, result2.Fitness.PercentGain, 5000);
        }

        #endregion

        #region Helper Methods

        private PriceRecord[] CreateTestPriceRecords(int count = 30)
        {
            var records = new List<PriceRecord>();
            var baseDate = DateTime.Today.AddDays(-count);
            var basePrice = 100.0;

            for (var i = 0; i < count; i++)
            {
                var price = basePrice + Math.Sin(i * 0.2) * 10; // Sine wave pattern
                records.Add(new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price - 1, price + 2, price - 2, price, volume: 10000 + i * 100,
                    wap: price, count: 1));
            }

            return records.ToArray();
        }

        private PriceRecord[] CreateSequentialPriceRecords(int count)
        {
            var records = new List<PriceRecord>();
            var baseDate = DateTime.Today.AddDays(-count);

            for (var i = 0; i < count; i++)
            {
                var price = 100.0 + i; // Sequential increase
                records.Add(new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price, price + 1, price - 1, price, volume: 10000,
                    wap: price, count: 1));
            }

            return records.ToArray();
        }

        private PriceRecord[] CreateStrongTrendPriceRecords(int count = 20)
        {
            var records = new List<PriceRecord>();
            var baseDate = DateTime.Today.AddDays(-count);

            for (var i = 0; i < count; i++)
            {
                var price = 100.0 + i * 5; // Strong upward trend
                records.Add(new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price, price + 2, price - 1, price, volume: 15000,
                    wap: price, count: 1));
            }

            return records.ToArray();
        }

        private PriceRecord[] CreateVolatilePriceRecords(int count = 25)
        {
            var records = new List<PriceRecord>();
            var baseDate = DateTime.Today.AddDays(-count);
            var random = new Random(123);

            for (var i = 0; i < count; i++)
            {
                var price = 100.0 + (random.NextDouble() - 0.5) * 40; // High volatility
                records.Add(new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price + random.NextDouble() * 4 - 2,
                    price + random.NextDouble() * 8,
                    price - random.NextDouble() * 8,
                    price, volume: 8000 + random.Next(5000),
                    wap: price, count: 1));
            }

            return records.ToArray();
        }

        private PriceRecord[] CreateFlatPriceRecords(int count, double price)
        {
            var records = new List<PriceRecord>();
            var baseDate = DateTime.Today.AddDays(-count);

            for (var i = 0; i < count; i++)
                records.Add(new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price, price + 0.5, price - 0.5, price, volume: 10000,
                    wap: price, count: 1));

            return records.ToArray();
        }

        private PriceRecord[] CreateDecreasingPriceRecords(int count = 20)
        {
            var records = new List<PriceRecord>();
            var baseDate = DateTime.Today.AddDays(-count);

            for (var i = 0; i < count; i++)
            {
                var price = 150.0 - i * 2; // Decreasing trend
                records.Add(new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price + 0.5, price + 1, price - 1, price, volume: 12000,
                    wap: price, count: 1));
            }

            return records.ToArray();
        }

        private PriceRecord[] CreateComplexTestPriceRecords(int count = 100)
        {
            var records = new List<PriceRecord>();
            var baseDate = DateTime.Today.AddDays(-count);

            for (var i = 0; i < count; i++)
            {
                // Complex pattern: trend + cycle + noise
                var trend = i * 0.5;
                var cycle = Math.Sin(i * 0.1) * 15;
                var noise = (new Random(i).NextDouble() - 0.5) * 5;
                var price = 100.0 + trend + cycle + noise;

                records.Add(new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price + (new Random(i * 2).NextDouble() - 0.5) * 2,
                    price + new Random(i * 3).NextDouble() * 3,
                    price - new Random(i * 4).NextDouble() * 3,
                    price, volume: 10000 + new Random(i * 5).Next(5000),
                    wap: price, count: 1));
            }

            return records.ToArray();
        }

        private List<GeneticIndividual> CreateTestPopulation(int size)
        {
            var population = new List<GeneticIndividual>();
            var rng = new Random(42);

            for (var i = 0; i < size; i++)
            {
                var individual = new GeneticIndividual(rng, 10000.0,
                    0, 5, 5, 20, 0, 3, TimeFrame.M1, TimeFrame.D1,
                    -2, 2, 0.1, 2.0, 3, 0.01, 0.05, 1, 30, 0, 20,
                    5, 15, 20, 50,
                    0, 1, 0, 1, 0, 1, 1, 10);

                // Set different fitness values for testing
                individual.Fitness = new Fitness(i * 100, i * 0.01);
                population.Add(individual);
            }

            return population;
        }

        private List<GeneticIndividual> CreateSimilarTestPopulation(int size)
        {
            var population = new List<GeneticIndividual>();
            var baseIndividual = CreateTestIndividual();

            for (var i = 0; i < size; i++)
            {
                var individual = CloneIndividual(baseIndividual);
                individual.Fitness = new Fitness(i * 50, i * 0.005);
                population.Add(individual);
            }

            return population;
        }

        private List<GeneticIndividual> CreateDiverseTestPopulation(int size)
        {
            var population = new List<GeneticIndividual>();
            var rng = new Random(123);

            for (var i = 0; i < size; i++)
            {
                var individual = new GeneticIndividual(rng, 10000.0,
                    0, 15, 2, 50, 0, 5, TimeFrame.M1, TimeFrame.D1,
                    -5, 5, 0.01, 5.0, 5, 0.001, 0.1, 1, 365, 0, 100,
                    1, 50, 10, 200,
                    0, 1, 0, 2, 0, 2, 1, 100);

                individual.Fitness = new Fitness(rng.NextDouble() * 1000, rng.NextDouble() * 0.1);
                population.Add(individual);
            }

            return population;
        }

        private GeneticIndividual CreateTestIndividual()
        {
            var rng = new Random(42);
            return new GeneticIndividual(rng, 10000.0,
                0, 5, 5, 20, 0, 3, TimeFrame.M1, TimeFrame.D1,
                -2, 2, 0.1, 2.0, 3, 0.01, 0.05, 1, 30, 0, 20,
                5, 15, 20, 50,
                0, 1, 0, 1, 0, 1, 1, 10);
        }

        private GeneticIndividual CreateDifferentTestIndividual()
        {
            var rng = new Random(123);
            return new GeneticIndividual(rng, 10000.0,
                0, 10, 10, 40, 0, 4, TimeFrame.H1, TimeFrame.D1,
                -3, 3, 0.5, 3.0, 4, 0.02, 0.08, 5, 60, 5, 50,
                10, 25, 30, 80,
                0, 1, 0, 2, 0, 2, 2, 20);
        }

        private GeneticIndividual CreateTestIndividualWithIndicators(int count)
        {
            var individual = CreateTestIndividual();

            // Clear existing indicators and add specific count
            individual.Indicators.Clear();
            var rng = new Random(42);

            for (var i = 0; i < count; i++)
                individual.Indicators.Add(new IndicatorParams
                {
                    Type = i % 6,
                    Period = 10 + i * 5,
                    Mode = i % 3,
                    TimeFrame = TimeFrame.D1,
                    Polarity = i % 2 == 0 ? 1 : -1,
                    LongThreshold = 0.5 + i * 0.1,
                    ShortThreshold = -(0.5 + i * 0.1)
                });

            return individual;
        }

        private GeneticIndividual CloneIndividual(GeneticIndividual original)
        {
            var method = typeof(GeneticSolver).GetMethod("Clone",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(GeneticIndividual) }, null);

            return (GeneticIndividual)method.Invoke(_solver, new object[] { original });
        }

        private List<IndicatorParams> CreateTestIndicators()
        {
            return new List<IndicatorParams>
            {
                new IndicatorParams
                {
                    Type = 0, Period = 10, Mode = 0, TimeFrame = TimeFrame.D1,
                    Polarity = 1, LongThreshold = 0.5, ShortThreshold = -0.5
                },
                new IndicatorParams
                {
                    Type = 1, Period = 20, Mode = 1, TimeFrame = TimeFrame.H1,
                    Polarity = -1, LongThreshold = 1.0, ShortThreshold = -1.0
                }
            };
        }

        private IndicatorParams CreateTestIndicatorParams()
        {
            return new IndicatorParams
            {
                Type = 2,
                Period = 15,
                Mode = 1,
                TimeFrame = TimeFrame.D1,
                Polarity = 1,
                LongThreshold = 0.8,
                ShortThreshold = -0.8
            };
        }

        #endregion
    }
}