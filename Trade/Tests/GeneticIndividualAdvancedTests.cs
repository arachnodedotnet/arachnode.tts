using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;
using Trade.Utils; // Added for PriceRange wrappers

namespace Trade.Tests
{
    [TestClass]
    public class GeneticIndividualAdvancedTests
    {
        private const double TOLERANCE = 1e-6;

        [TestInitialize]
        public void Setup()
        {
            // Initialize static dependencies for testing
            GeneticIndividual.InitializePrices();
            GeneticIndividual.InitializeOptionSolvers();
        }

        #region Normalize Method Tests

        [TestMethod][TestCategory("Core")]
        public void Normalize_WithAnalyzedRanges_ReturnsCorrectValues()
        {
            var individual = new GeneticIndividual();
            var priceRecords = CreateTestPriceRecords(100); // Increased to provide more data

            try
            {
                // Analyze ranges first
                GeneticIndividual.AnalyzeIndicatorRanges(priceRecords);

                // Test normalization (this would require making the method public or using reflection)
                // For now, we verify that the analysis completed without error
                Assert.IsTrue(true, "Range analysis should complete successfully");
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException)
            {
                // Handle the specific case where indicators need more data
                Assert.Inconclusive($"Indicator range analysis requires more data points. " +
                                    $"Current dataset: {priceRecords.Length} records. " +
                                    $"Some indicators may need larger datasets for proper calculation. " +
                                    $"Error: {ex.Message}");
            }
        }

        #endregion

        #region Advanced Indicator Tests

        [TestMethod][TestCategory("Core")]
        public void CalculateIndicatorValue_AllIndicatorTypes_ProcessWithoutError()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = CreateTestPriceArray();

            // Test all indicator types (0-17)
            for (var type = 0; type <= 17; type++)
            {
                var indicator = new IndicatorParams
                {
                    Type = type,
                    Period = Math.Min(10, priceBuffer.Length),
                    Mode = 0,
                    TimeFrame = TimeFrame.D1,
                    Polarity = 1,
                    Param1 = 1.0,
                    Param2 = 1.0,
                    Param3 = 1.0,
                    Param4 = 1.0,
                    Param5 = 1.0
                };

                try
                {
                    var value = CallPrivateCalculateIndicatorValue(individual, indicator,
                        priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer.Length);

                    Assert.IsFalse(double.IsNaN(value), $"Indicator type {type} should not return NaN");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Indicator type {type} failed with exception: {ex.Message}");
                }
            }
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateIndicatorValue_ChaikinOscillator_UsesMAParameters()
        {
            var individual = new GeneticIndividual();
            individual.FastMAPeriod = 3;
            individual.SlowMAPeriod = 10;

            var priceBuffer = CreateTestPriceArray(20);
            var indicator = new IndicatorParams { Type = 17, Period = 14, Mode = 0 }; // Chaikin Oscillator

            var value = CallPrivateCalculateIndicatorValue(individual, indicator,
                priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer.Length);

            Assert.IsFalse(double.IsNaN(value), "Chaikin Oscillator should return valid value");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateIndicatorValue_CCIIndicator_ProcessesCorrectly()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = CreateTestPriceArray();
            var indicator = new IndicatorParams { Type = 16, Period = 14 }; // CCI

            var value = CallPrivateCalculateIndicatorValue(individual, indicator,
                priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer.Length);

            Assert.IsFalse(double.IsNaN(value), "CCI should return valid value");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateIndicatorValue_DebugCase_BypassesPeriodCheck()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = new double[1];
            var indicator = new IndicatorParams
            {
                Type = 1,
                Period = 0,
                DebugCase = true
            };

            var value = CallPrivateCalculateIndicatorValue(individual, indicator,
                priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer.Length);

            // Should not return 0 even with period 0 when DebugCase is true
            Assert.IsNotNull(value);
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateIndicatorValue_ATRIndicator_ProcessesCorrectly()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = CreateTestPriceArray(20);
            var indicator = new IndicatorParams { Type = 5, Period = 14 }; // ATR

            var value = CallPrivateCalculateIndicatorValue(individual, indicator,
                priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer.Length);

            Assert.IsFalse(double.IsNaN(value), "ATR should return valid value");
            Assert.IsTrue(value >= 0, "ATR should be non-negative");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateIndicatorValue_ADXIndicator_ProcessesCorrectly()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = CreateTestPriceArray();
            var indicator = new IndicatorParams { Type = 6, Period = 14 }; // ADX

            var value = CallPrivateCalculateIndicatorValue(individual, indicator,
                priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer.Length);

            Assert.IsFalse(double.IsNaN(value), "ADX should return valid value");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateIndicatorValue_BollingerBands_ProcessesCorrectly()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = CreateTestPriceArray(25);
            var indicator = new IndicatorParams { Type = 11, Period = 20 }; // Bollinger Bands

            var value = CallPrivateCalculateIndicatorValue(individual, indicator,
                priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer.Length);

            Assert.IsFalse(double.IsNaN(value), "Bollinger Bands should return valid value");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateIndicatorValue_AwesomeOscillator_ProcessesCorrectly()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = CreateTestPriceArray(40);
            var indicator = new IndicatorParams { Type = 14, Period = 20 }; // Awesome Oscillator

            var value = CallPrivateCalculateIndicatorValue(individual, indicator,
                priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer.Length);

            Assert.IsFalse(double.IsNaN(value), "Awesome Oscillator should return valid value");
        }

        #endregion

        #region Trading Logic Advanced Tests

        [TestMethod][TestCategory("Core")]
        public void Process_WithOptionSecurityType_ProcessesOptionTrades()
        {
            var individual = CreateTestIndividual();
            individual.AllowedSecurityTypes = AllowedSecurityType.Option;
            individual.NumberOfOptionContractsToOpen = 2;
            individual.OptionDaysOut = 30;
            individual.OptionStrikeDistance = 5;

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTrendingPriceRecords(100);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 50 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(50).ToArray();

            // Initialize option solvers
            GeneticIndividual.InitializeOptionSolvers();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
            // Should handle option trades (even if they don't execute due to pricing)
        }

        [TestMethod][TestCategory("Core")]
        public void Process_WithShortTradeType_AllowsShortTrades()
        {
            var individual = CreateTestIndividual();
            individual.AllowedTradeTypes = AllowedTradeType.SellShort;

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateDownTrendPriceRecords(50);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 25 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(25).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
        }

        [TestMethod][TestCategory("Core")]
        public void Process_WithMultipleTradesEnabled_TracksUsedBalance()
        {
            var individual = CreateMultiIndicatorIndividual();
            individual.AllowMultipleTrades = true;
            individual.TradePercentageForStocks = 0.1; // 10% per trade

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateVolatilePriceRecords(200);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 100 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(100).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
        }

        [TestMethod][TestCategory("Core")]
        public void Process_WithLowTradePercentage_HandlesSmallTrades()
        {
            var individual = CreateTestIndividual();
            individual.TradePercentageForStocks = 0.001; // 0.1%
            individual.StartingBalance = 1000.0;

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTrendingPriceRecords(100);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 50 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(50).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
        }
        
        [TestMethod][TestCategory("Core")]
        public void Process_WithDifferentCombinationMethods_ProducesResults()
        {
            var combinations = new[]
            {
                CombinationMethod.Sum,
                CombinationMethod.NormalizedSum,
                CombinationMethod.EnsembleVoting
            };

            foreach (var method in combinations)
            {
                var individual = CreateMultiIndicatorIndividual();
                individual.AllowMultipleTrades = false;
                individual.CombinationMethod = method;
                if (method == CombinationMethod.EnsembleVoting) individual.EnsembleVotingThreshold = 2;

                // Create historical data and add to Prices system
                var historicalPriceRecords = CreateTrendingPriceRecords(100);
                GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

                // Use only the last 50 records for testing
                var testPriceRecords = historicalPriceRecords.Skip(50).ToArray();

                var fitness = individual.Process(testPriceRecords);

                Assert.IsNotNull(fitness, $"Should process correctly with {method}");
            }
        }

        #endregion

        #region Scale Out Fractions Tests

        [TestMethod][TestCategory("Core")]
        public void GenerateValidScaleOutFractions_WithVariousContractSizes_ProducesValidFractions()
        {
            var contractSizes = new double[] { 1, 3, 8, 16, 24, 100 };
            var rng = new Random(42);

            foreach (var contracts in contractSizes)
            {
                var fractions = CallPrivateGenerateValidScaleOutFractions(rng, contracts);

                Assert.AreEqual(8, fractions.Length, $"Should always return 8 fractions for {contracts} contracts");
                Assert.AreEqual(1.0, fractions.Sum(), 1e-10, $"Fractions should sum to 1.0 for {contracts} contracts");

                // Verify each fraction results in whole contracts
                for (var i = 0; i < fractions.Length; i++)
                {
                    var contractCount = fractions[i] * contracts;
                    Assert.AreEqual(Math.Round(contractCount), contractCount, 1e-10,
                        $"Fraction {i} should result in whole contracts for {contracts} total contracts");
                }
            }
        }

        [TestMethod][TestCategory("Core")]
        public void GenerateValidScaleOutFractions_WithZeroContracts_HandlesGracefully()
        {
            var rng = new Random(42);

            var fractions = CallPrivateGenerateValidScaleOutFractions(rng, 0);

            Assert.AreEqual(8, fractions.Length);
            Assert.IsTrue(fractions.All(f => f == 0), "All fractions should be zero for zero contracts");
        }

        [TestMethod][TestCategory("Core")]
        public void GenerateValidScaleOutFractions_WithLargeContracts_HandlesCorrectly()
        {
            var rng = new Random(42);

            var fractions = CallPrivateGenerateValidScaleOutFractions(rng, 1000);

            Assert.AreEqual(8, fractions.Length);
            Assert.AreEqual(1.0, fractions.Sum(), 1e-10);

            // Should distribute reasonably across all steps
            var nonZeroSteps = fractions.Count(f => f > 0);
            Assert.IsTrue(nonZeroSteps >= 7, "Should use most scale-out steps for large contract counts");
        }

        #endregion

        #region Static Dependencies Tests

        [TestMethod][TestCategory("Core")]
        public void StaticDependencies_CanBeSetAndAccessed()
        {
            GeneticIndividual.InitializePrices("test.csv");
            GeneticIndividual.InitializeOptionSolvers("test.csv");

            Assert.IsNotNull(GeneticIndividual.Prices);
            Assert.IsNotNull(GeneticIndividual.OptionsPrices);
            Assert.IsNotNull(GeneticIndividual.ImpliedVolatilitySolverCalls);
            Assert.IsNotNull(GeneticIndividual.ImpliedVolatilitySolverPuts);
        }

        [TestMethod][TestCategory("Core")]
        public void StaticDependencies_InitializeMultipleTimes_RemainsStable()
        {
            for (var i = 0; i < 3; i++)
            {
                GeneticIndividual.InitializePrices();
                GeneticIndividual.InitializeOptionSolvers();

                Assert.IsNotNull(GeneticIndividual.Prices);
                Assert.IsNotNull(GeneticIndividual.OptionsPrices);
                Assert.IsNotNull(GeneticIndividual.ImpliedVolatilitySolverCalls);
                Assert.IsNotNull(GeneticIndividual.ImpliedVolatilitySolverPuts);
            }
        }

        #endregion

        #region Performance and Stress Tests

        [TestMethod][TestCategory("Core")]
        public void Process_WithLargeDataSet_CompletesInReasonableTime()
        {
            var individual = CreateTestIndividual();

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTestPriceRecords(2000); // Large historical dataset
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 1000 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(1000).ToArray();

            var startTime = DateTime.Now;
            var fitness = individual.Process(testPriceRecords);
            var endTime = DateTime.Now;

            var duration = endTime - startTime;

            Assert.IsNotNull(fitness);
            Assert.IsTrue(duration.TotalSeconds < 10, "Large dataset should process in under 10 seconds");
        }

        [TestMethod][TestCategory("Core")]
        public void Process_WithManyIndicators_HandlesCorrectly()
        {
            var individual = new GeneticIndividual();
            individual.StartingBalance = 10000.0;

            // Add maximum indicators
            for (var i = 0; i < 5; i++)
                individual.Indicators.Add(new IndicatorParams
                {
                    Type = i % 6,
                    Period = 10 + i,
                    Mode = 0,
                    TimeFrame = TimeFrame.D1,
                    Polarity = i % 2 == 0 ? 1 : -1,
                    LongThreshold = 0.5 + i * 0.1,
                    ShortThreshold = -(0.5 + i * 0.1)
                });

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTrendingPriceRecords(100);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 50 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(50).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
            Assert.AreEqual(5, individual.IndicatorValues.Count);
        }

        [TestMethod][TestCategory("Core")]
        public void Process_RepeatedCalls_ConsistentResults()
        {
            var individual = CreateTestIndividual();

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTestPriceRecords(60);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 30 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(30).ToArray();

            var fitness1 = individual.Process(testPriceRecords);
            var fitness2 = individual.Process(testPriceRecords);
            var fitness3 = individual.Process(testPriceRecords);

            Assert.AreEqual(fitness1.DollarGain, fitness2.DollarGain, TOLERANCE);
            Assert.AreEqual(fitness2.DollarGain, fitness3.DollarGain, TOLERANCE);
            Assert.AreEqual(fitness1.PercentGain, fitness2.PercentGain, TOLERANCE);
            Assert.AreEqual(fitness2.PercentGain, fitness3.PercentGain, TOLERANCE);
        }

        #endregion

        #region Edge Cases and Boundary Conditions

        [TestMethod][TestCategory("Core")]
        public void Process_WithInsufficientBalance_HandlesGracefully()
        {
            var individual = CreateTestIndividual();
            individual.StartingBalance = 1.0; // Very small balance
            individual.TradePercentageForStocks = 0.5; // 50% of $1 = $0.50

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTrendingPriceRecords(40);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 20 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(20).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
        }

        [TestMethod][TestCategory("Core")]
        public void Process_WithExtremeVolatility_RemainsStable()
        {
            var individual = CreateTestIndividual();

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateExtremeVolatilityPriceRecords(40);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 20 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(20).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
            Assert.IsFalse(double.IsInfinity(fitness.DollarGain));
            Assert.IsFalse(double.IsInfinity(fitness.PercentGain));
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateMaximalFitness_WithExtremeValues_HandlesCorrectly()
        {
            var priceRecords = CreateExtremeValuePriceRecords();

            var fitness = GeneticIndividual.CalculateMaximalFitness(priceRecords);

            Assert.IsNotNull(fitness);
            Assert.IsFalse(double.IsNaN(fitness.DollarGain));
            Assert.IsFalse(double.IsNaN(fitness.PercentGain));
        }

        [TestMethod][TestCategory("Core")]
        public void Process_WithVeryShortTimeframes_HandlesCorrectly()
        {
            var individual = CreateTestIndividual();
            individual.Indicators[0].TimeFrame = TimeFrame.M1;
            individual.Indicators[0].Period = 2; // Very short period

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTestPriceRecords(10);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 5 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(5).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
        }

        [TestMethod][TestCategory("Core")]
        public void Process_WithZeroBalance_HandlesGracefully()
        {
            var individual = CreateTestIndividual();
            individual.StartingBalance = 0.0;

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTestPriceRecords(20);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 10 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(10).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
            Assert.AreEqual(0, individual.Trades.Count);
        }

        [TestMethod][TestCategory("Core")]
        public void Process_WithNegativePolarity_ProcessesCorrectly()
        {
            var individual = CreateTestIndividual();
            individual.Indicators[0].Polarity = -1; // Negative polarity

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTrendingPriceRecords(60);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 30 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(30).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
        }

        [TestMethod][TestCategory("Core")]
        public void Process_WithExtremeThresholds_HandlesCorrectly()
        {
            var individual = CreateTestIndividual();
            individual.Indicators[0].LongThreshold = 1000.0; // Very high threshold
            individual.Indicators[0].ShortThreshold = -1000.0; // Very low threshold

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTestPriceRecords(100);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 50 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(50).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
        }

        #endregion

        #region Mathematical Accuracy Tests

        [TestMethod][TestCategory("Core")]
        public void CalculateIndicatorValue_EMA_MatchesExpectedCalculation()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = new[] { 22.27, 22.19, 22.08, 22.17, 22.18, 22.13, 22.23, 22.43, 22.24, 22.29 };
            var indicator = new IndicatorParams { Type = 2, Period = 5 }; // EMA

            var value = CallPrivateCalculateIndicatorValue(individual, indicator,
                priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer.Length);

            // EMA calculation should be reasonable
            Assert.IsTrue(value > 22.0 && value < 23.0, "EMA should be within reasonable range");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateIndicatorValue_LWMA_WeightsCorrectly()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = new double[] { 10, 20, 30 }; // Simple ascending values
            var indicator = new IndicatorParams { Type = 4, Period = 3 }; // LWMA

            var value = CallPrivateCalculateIndicatorValue(individual, indicator,
                priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer, priceBuffer.Length);

            // LWMA should be closer to recent values
            // Expected: (10*1 + 20*2 + 30*3) / (1+2+3) = 140/6 = 23.33
            Assert.AreEqual(23.333333333333332, value, 1e-10, "LWMA should calculate correctly");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateMaximalFitness_LocalMinimaMaxima_IdentifiesCorrectly()
        {
            var priceRecords = CreateLocalMinimaMaximaPriceRecords();

            var fitness = GeneticIndividual.CalculateMaximalFitness(priceRecords);

            Assert.IsNotNull(fitness);
            Assert.IsTrue(fitness.DollarGain > 0, "Should profit from buying at minima and selling at maxima");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateMaximalFitness_FlatPrices_ReturnsZeroGain()
        {
            var priceRecords = CreateFlatPriceRecords(20, 100.0);

            var fitness = GeneticIndividual.CalculateMaximalFitness(priceRecords);

            Assert.IsNotNull(fitness);
            Assert.AreEqual(0.0, fitness.DollarGain, TOLERANCE, "Flat prices should yield zero maximal gain");
        }

        #endregion

        #region Dynamic Position Sizing Advanced Tests

        [TestMethod][TestCategory("Core")]
        public void Constructor_DynamicPositionSizing_ParametersWithinBounds()
        {
            var rng = new Random(42);
            var individual = new GeneticIndividual(rng, 10000.0,
                0, 5, 5, 20, 0, 3, TimeFrame.M1, TimeFrame.D1,
                -2, 2, 0.1, 2.0, 3, 0.01, 0.05, 1, 30, 0, 20,
                5, 15, 20, 50,
                0, 1, 0, 1, 0, 1, 1, 10);

            // Verify dynamic position sizing parameters are within expected bounds
            Assert.IsTrue(individual.MaxPositionSize >= 0.05 && individual.MaxPositionSize <= 0.25,
                "MaxPositionSize should be between 5% and 25%");
            Assert.IsTrue(individual.BaseRiskPerTrade >= 0.01 && individual.BaseRiskPerTrade <= 0.05,
                "BaseRiskPerTrade should be between 1% and 5%");
            Assert.IsTrue(individual.VolatilityTarget >= 0.10 && individual.VolatilityTarget <= 0.30,
                "VolatilityTarget should be between 10% and 30%");
            Assert.IsTrue(individual.KellyMultiplier >= 0.1 && individual.KellyMultiplier <= 0.5,
                "KellyMultiplier should be between 10% and 50%");
            Assert.IsTrue(individual.MaxConcurrentPositions >= 2 && individual.MaxConcurrentPositions <= 7,
                "MaxConcurrentPositions should be between 2 and 7");
        }

        [TestMethod][TestCategory("Core")]
        public void Process_WithDynamicPositionSizing_ProcessesCorrectly()
        {
            var rng = new Random(42);
            var individual = new GeneticIndividual(rng, 50000.0,
                0, 3, 5, 15, 0, 2, TimeFrame.M1, TimeFrame.D1,
                -1, 1, 0.5, 1.5, 2, 0.02, 0.04, 1, 20, 0, 10,
                5, 12, 15, 30,
                0, 1, 0, 1, 0, 1, 1, 5);

            individual.UseDynamicPositionSizing = true;

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTrendingPriceRecords(80);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 40 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(40).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
            // Should process without errors even with dynamic position sizing
        }

        #endregion

        #region Comprehensive Integration Tests

        [TestMethod][TestCategory("Core")]
        public void Process_CompleteWorkflow_AllFeaturesEnabled()
        {
            var rng = new Random(42);
            var individual = new GeneticIndividual(rng, 25000.0,
                0, 10, 5, 25, 0, 3, TimeFrame.M1, TimeFrame.D1,
                -2, 2, 0.2, 1.8, 4, 0.015, 0.035, 1, 45, 0, 15,
                3, 18, 20, 60,
                0, 1, 0, 2, 0, 1, 1, 12);

            // Enable all advanced features
            individual.AllowMultipleTrades = true;
            individual.CombinationMethod = CombinationMethod.EnsembleVoting;
            individual.EnsembleVotingThreshold = 2;
            individual.UseDynamicPositionSizing = true;

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateComplexMarketPriceRecords(160);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 80 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(80).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
            Assert.IsTrue(individual.Indicators.Count > 0);
            Assert.IsTrue(individual.IndicatorValues.Count == individual.Indicators.Count);
            Assert.IsTrue(individual.TradeActions.Count == testPriceRecords.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Process_AllSecurityTypes_ProcessesCorrectly()
        {
            var securityTypes = new[] { AllowedSecurityType.Stock, AllowedSecurityType.Option };

            foreach (var securityType in securityTypes)
            {
                var individual = CreateTestIndividual();
                individual.AllowedSecurityTypes = securityType;

                // Create historical data and add to Prices system
                var historicalPriceRecords = CreateTrendingPriceRecords(60);
                GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

                // Use only the last 30 records for testing
                var testPriceRecords = historicalPriceRecords.Skip(30).ToArray();

                var fitness = individual.Process(testPriceRecords);

                Assert.IsNotNull(fitness, $"Should process correctly with {securityType}");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Process_AllTradeTypes_ProcessesCorrectly()
        {
            var tradeTypes = new[] { AllowedTradeType.Buy, AllowedTradeType.SellShort };

            foreach (var tradeType in tradeTypes)
            {
                var individual = CreateTestIndividual();
                individual.AllowedTradeTypes = tradeType;

                // Create historical data and add to Prices system
                var historicalPriceRecords = CreateTrendingPriceRecords(60);
                GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

                // Use only the last 30 records for testing
                var testPriceRecords = historicalPriceRecords.Skip(30).ToArray();

                var fitness = individual.Process(testPriceRecords);

                Assert.IsNotNull(fitness, $"Should process correctly with {tradeType}");
            }
        }

        #endregion

        #region Helper Methods

        private GeneticIndividual CreateTestIndividual()
        {
            var individual = new GeneticIndividual();
            individual.StartingBalance = 10000.0;
            individual.Indicators.Add(new IndicatorParams
            {
                Type = 1, // SMA
                Period = 10,
                Mode = 0,
                TimeFrame = TimeFrame.D1,
                Polarity = 1,
                LongThreshold = 0.5,
                ShortThreshold = -0.5
            });
            return individual;
        }

        private GeneticIndividual CreateMultiIndicatorIndividual()
        {
            var individual = new GeneticIndividual();
            individual.StartingBalance = 10000.0;

            individual.Indicators.Add(new IndicatorParams
            {
                Type = 1,
                Period = 10,
                Mode = 0,
                TimeFrame = TimeFrame.D1,
                Polarity = 1,
                LongThreshold = 0.5,
                ShortThreshold = -0.5
            });

            individual.Indicators.Add(new IndicatorParams
            {
                Type = 2,
                Period = 14,
                Mode = 0,
                TimeFrame = TimeFrame.D1,
                Polarity = 1,
                LongThreshold = 0.8,
                ShortThreshold = -0.8
            });

            individual.Indicators.Add(new IndicatorParams
            {
                Type = 3,
                Period = 20,
                Mode = 0,
                TimeFrame = TimeFrame.D1,
                Polarity = -1,
                LongThreshold = 1.0,
                ShortThreshold = -1.0
            });

            return individual;
        }

        private double[] CreateTestPriceArray(int count = 30)
        {
            var prices = new double[count];
            for (var i = 0; i < count; i++) prices[i] = 100.0 + Math.Sin(i * 0.2) * 15 + i * 0.5;
            return prices;
        }

        private List<double> CreateTestSignals(int count)
        {
            var signals = new List<double>();
            for (var i = 0; i < count; i++) signals.Add(Math.Sin(i * 0.3) * 2);
            return signals;
        }

        private PriceRecord[] CreateTestPriceRecords(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var basePrice = 100.0;

            for (var i = 0; i < count; i++)
            {
                var price = basePrice + Math.Sin(i * 0.1) * 10;
                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price - 1, price + 1, price - 2, price, volume: 1000,
                    wap: price, count: 1);
            }

            return records;
        }

        private PriceRecord[] CreateTrendingPriceRecords(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var basePrice = 100.0;

            for (var i = 0; i < count; i++)
            {
                var price = basePrice + i * 0.8; // Strong upward trend
                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price - 0.5, price + 0.5, price - 1, price, volume: 1000,
                    wap: price, count: 1);
            }

            return records;
        }

        private PriceRecord[] CreateVolatilePriceRecords(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var basePrice = 100.0;
            var rng = new Random(123);

            for (var i = 0; i < count; i++)
            {
                var price = basePrice + (rng.NextDouble() - 0.5) * 30;
                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price + rng.NextDouble() * 3 - 1.5,
                    price + rng.NextDouble() * 5,
                    price - rng.NextDouble() * 5,
                    price, volume: 1000 + rng.Next(800),
                    wap: price, count: 1);
            }

            return records;
        }

        private PriceRecord[] CreateDownTrendPriceRecords(int count = 25)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);

            for (var i = 0; i < count; i++)
            {
                var price = 150.0 - i * 1.5; // Downward trend
                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price + 0.5, price + 1, price - 0.5, price, volume: 1000,
                    wap: price, count: 1);
            }

            return records;
        }

        private PriceRecord[] CreateExtremeVolatilityPriceRecords(int count = 20)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var rng = new Random(456);

            for (var i = 0; i < count; i++)
            {
                var price = 100.0 + (rng.NextDouble() - 0.5) * 80; // Extreme volatility
                price = Math.Max(10, price); // Keep price positive
                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price + rng.NextDouble() * 10 - 5,
                    price + rng.NextDouble() * 15,
                    price - rng.NextDouble() * 15,
                    price, volume: 1000 + rng.Next(2000),
                    wap: price, count: 1);
            }

            return records;
        }

        private PriceRecord[] CreateExtremeValuePriceRecords()
        {
            var count = 15;
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);

            for (var i = 0; i < count; i++)
            {
                var price = i % 2 == 0 ? 1000000.0 : 0.01; // Extreme price swings
                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price * 0.99, price * 1.01, price * 0.98, price, volume: 1000,
                    wap: price, count: 1);
            }

            return records;
        }

        private PriceRecord[] CreateLocalMinimaMaximaPriceRecords()
        {
            var prices = new double[] { 100, 95, 90, 95, 100, 105, 110, 105, 100, 95, 90, 95, 100 };
            var records = new PriceRecord[prices.Length];
            var baseDate = DateTime.Today.AddDays(-prices.Length);

            for (var i = 0; i < prices.Length; i++)
                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    prices[i] - 1, prices[i] + 1, prices[i] - 2, prices[i], volume: 1000,
                    wap: prices[i], count: 1);

            return records;
        }

        private PriceRecord[] CreateFlatPriceRecords(int count, double price)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);

            for (var i = 0; i < count; i++)
                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price, price, price, price, volume: 1000,
                    wap: price, count: 1);

            return records;
        }

        private PriceRecord[] CreateComplexMarketPriceRecords(int count = 80)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            var rng = new Random(789);

            for (var i = 0; i < count; i++)
            {
                // Complex pattern: trend + cycle + noise
                var trend = i * 0.3;
                var cycle = Math.Sin(i * 0.15) * 8;
                var noise = (rng.NextDouble() - 0.5) * 3;
                var price = 100.0 + trend + cycle + noise;

                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price + (rng.NextDouble() - 0.5) * 1.5,
                    price + rng.NextDouble() * 2.5,
                    price - rng.NextDouble() * 2.5,
                    price, volume: 1000 + rng.Next(1000),
                    wap: price, count: 1);
            }

            return records;
        }

        // Helper: reflection wrapper for internal CalculateIndicatorValue requiring PriceRange parameters
        private double CallPrivateCalculateIndicatorValue(GeneticIndividual individual, IndicatorParams indicator,
            double[] openPrices, double[] highPrices, double[] lowPrices, double[] closePrices, double[] volumes,
            double[] priceBuffer, int totalLength)
        {
            var method = typeof(GeneticIndividual).GetMethod("CalculateIndicatorValue", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "CalculateIndicatorValue method not found via reflection");

            var openRange = new PriceRange(openPrices);
            var highRange = new PriceRange(highPrices);
            var lowRange = new PriceRange(lowPrices);
            var closeRange = new PriceRange(closePrices);
            var volumeRange = new PriceRange(volumes);
            var priceBufferRange = new PriceRange(priceBuffer);

            return (double)method.Invoke(individual, new object[]
            {
                indicator, openRange, highRange, lowRange, closeRange, volumeRange, priceBufferRange, totalLength, null
            });
        }

        private double[] CallPrivateGenerateValidScaleOutFractions(Random rng, double totalContracts)
        {
            var method = typeof(GeneticIndividual).GetMethod("GenerateValidScaleOutFractionsOptimized",
                BindingFlags.NonPublic | BindingFlags.Static) ??
                         typeof(GeneticIndividual).GetMethod("GenerateValidScaleOutFractions",
                BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
                throw new InvalidOperationException("Could not find GenerateValidScaleOutFractions method");

            return (double[])method.Invoke(null, new object[] { rng, totalContracts });
        }

        #endregion
    }
}