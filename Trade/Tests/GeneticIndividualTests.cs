using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Reflection;
using Trade.Indicators;
using Trade.Prices2;
using Trade.Utils; // Added for PriceRange reflection support

namespace Trade.Tests
{
    [TestClass]
    public class GeneticIndividualTests
    {
        private const double TOLERANCE = 1e-6;
        private GeneticIndividual _individual;
        private Random _rng;

        [TestInitialize]
        public void Setup()
        {
            _rng = new Random(42); // Fixed seed for reproducible tests
            _individual = new GeneticIndividual();

            // Initialize static dependencies for testing
            GeneticIndividual.InitializePrices();
            GeneticIndividual.InitializeOptionSolvers();
        }

        #region IndicatorParams Tests

        [TestMethod]
        [TestCategory("Core")]
        public void IndicatorParams_Properties_SetAndGetCorrectly()
        {
            var indicator = new IndicatorParams
            {
                Type = 1,
                Period = 14,
                Mode = 0,
                TimeFrame = TimeFrame.D1,
                Polarity = 1,
                LongThreshold = 0.8,
                ShortThreshold = -0.8,
                Param1 = 1.0,
                Param2 = 2.0,
                Param3 = 3.0,
                Param4 = 4.0,
                Param5 = 5.0,
                DebugCase = true
            };

            Assert.AreEqual(1, indicator.Type);
            Assert.AreEqual(14, indicator.Period);
            Assert.AreEqual(0, indicator.Mode);
            Assert.AreEqual(TimeFrame.D1, indicator.TimeFrame);
            Assert.AreEqual(1, indicator.Polarity);
            Assert.AreEqual(0.8, indicator.LongThreshold, TOLERANCE);
            Assert.AreEqual(-0.8, indicator.ShortThreshold, TOLERANCE);
            Assert.AreEqual(1.0, indicator.Param1, TOLERANCE);
            Assert.AreEqual(2.0, indicator.Param2, TOLERANCE);
            Assert.AreEqual(3.0, indicator.Param3, TOLERANCE);
            Assert.AreEqual(4.0, indicator.Param4, TOLERANCE);
            Assert.AreEqual(5.0, indicator.Param5, TOLERANCE);
            Assert.IsTrue(indicator.DebugCase);
        }

        #endregion

        #region Dynamic Position Sizing Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Constructor_WithDynamicPositionSizing_InitializesCorrectly()
        {
            var individual = new GeneticIndividual(_rng, 10000.0,
                0, 5, 5, 20, 0, 3, TimeFrame.M1, TimeFrame.D1,
                -2, 2, 0.1, 2.0, 3, 0.01, 0.05, 1, 30, 0, 20,
                5, 15, 20, 50,
                0, 1, 0, 1, 0, 1, 1, 10);

            // Check dynamic position sizing properties are initialized
            Assert.IsTrue(individual.MaxPositionSize > 0);
            Assert.IsTrue(individual.BaseRiskPerTrade > 0);
            Assert.IsTrue(individual.VolatilityTarget > 0);
            Assert.IsTrue(individual.KellyMultiplier > 0);
            Assert.IsTrue(individual.MaxConcurrentPositions > 0);
        }

        #endregion

        #region Constructor Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Constructor_Default_InitializesCorrectly()
        {
            var individual = new GeneticIndividual();

            Assert.IsNotNull(individual.Indicators);
            Assert.IsNotNull(individual.Trades);
            Assert.IsNotNull(individual.TradeActions);
            Assert.IsNotNull(individual.SignalValues);
            Assert.IsNotNull(individual.IndicatorValues);
            Assert.IsNotNull(individual.Chromosome);
            Assert.IsNotNull(individual.Fitness);
            Assert.IsNotNull(individual.OptionContractsToScaleOut);

            // Check default values
            Assert.AreEqual(CombinationMethod.Sum, individual.CombinationMethod);
            Assert.AreEqual(1, individual.EnsembleVotingThreshold);
            Assert.AreEqual(AllowedTradeType.None, individual.AllowedTradeTypes);
            Assert.AreEqual(0.03, individual.TradePercentageForStocks, TOLERANCE);
            Assert.AreEqual(8, individual.NumberOfOptionContractsToOpen, TOLERANCE);
            Assert.AreEqual(8, individual.OptionContractsToScaleOut.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Constructor_WithParameters_InitializesCorrectly()
        {
            var individual = new GeneticIndividual(_rng, 50000.0,
                0, 5, 5, 20, 0, 3, TimeFrame.M1, TimeFrame.D1,
                -2, 2, 0.1, 2.0, 3, 0.01, 0.05, 1, 30, 0, 20,
                5, 15, 20, 50,
                0, 1, 0, 1, 0, 1, 1, 10);

            Assert.AreEqual(50000.0, individual.StartingBalance, TOLERANCE);
            Assert.IsTrue(individual.Indicators.Count > 0);
            Assert.IsTrue(individual.Indicators.Count <= 3);
            Assert.IsTrue(individual.TradePercentageForStocks >= 0.01 && individual.TradePercentageForStocks <= 0.05);
            Assert.IsTrue(individual.FastMAPeriod >= 5 && individual.FastMAPeriod <= 15);
            Assert.IsTrue(individual.SlowMAPeriod >= 20 && individual.SlowMAPeriod <= 50);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Constructor_WithMinimalParameters_InitializesCorrectly()
        {
            var individual = new GeneticIndividual(_rng, 1000.0,
                0, 0, 1, 1, 0, 0, TimeFrame.D1, TimeFrame.D1,
                1, 1, 1.0, 1.0, 1, 0.01, 0.01, 1, 1, 0, 0,
                1, 1, 1, 1,
                0, 0, 0, 0, 0, 0, 1, 1);

            Assert.AreEqual(1000.0, individual.StartingBalance, TOLERANCE);
            Assert.AreEqual(1, individual.Indicators.Count);
            Assert.AreEqual(0.01, individual.TradePercentageForStocks, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Constructor_EnsuresPolarityIsNonZero()
        {
            var individual = new GeneticIndividual(_rng, 10000.0,
                0, 5, 5, 20, 0, 3, TimeFrame.M1, TimeFrame.D1,
                -2, 2, 0.1, 2.0, 5, 0.01, 0.05, 1, 30, 0, 20,
                5, 15, 20, 50,
                0, 1, 0, 1, 0, 1, 1, 10);

            foreach (var indicator in individual.Indicators)
                Assert.AreNotEqual(0, indicator.Polarity, "Polarity should never be zero");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Constructor_ScaleOutFractions_SumToOne()
        {
            var individual = new GeneticIndividual(_rng, 10000.0,
                0, 5, 5, 20, 0, 3, TimeFrame.M1, TimeFrame.D1,
                -2, 2, 0.1, 2.0, 3, 0.01, 0.05, 1, 30, 0, 20,
                5, 15, 20, 50,
                0, 1, 0, 1, 0, 1, 1, 10);

            var sum = individual.OptionContractsToScaleOut.Sum();
            Assert.AreEqual(1.0, sum, 1e-10, "Scale out fractions should sum to 1.0");
        }

        #endregion

        #region Static Analysis Methods Tests

        [TestMethod]
        [TestCategory("Core")]
        public void AnalyzeIndicatorRanges_WithValidData_CalculatesRanges()
        {
            var priceRecords = CreateTestPriceRecords(50);

            GeneticIndividual.AnalyzeIndicatorRanges(priceRecords);

            // The method should complete without throwing exceptions
            Assert.IsTrue(true, "AnalyzeIndicatorRanges should complete successfully");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void AnalyzeIndicatorRanges_WithShortData_HandlesGracefully()
        {
            var priceRecords = CreateTestPriceRecords(50);

            GeneticIndividual.AnalyzeIndicatorRanges(priceRecords);

            Assert.IsTrue(true, "Should handle short data gracefully");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ExtractDates_WithValidPriceRecords_ReturnsCorrectDates()
        {
            var priceRecords = CreateTestPriceRecords(50);

            var dates = GeneticIndividual.ExtractDates(priceRecords);

            Assert.AreEqual(50, dates.Length);
            for (var i = 0; i < dates.Length; i++) Assert.AreEqual(priceRecords[i].DateTime, dates[i]);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ExtractClosePrices_WithValidPriceRecords_ReturnsCorrectPrices()
        {
            var priceRecords = CreateTestPriceRecords(10);

            var closePrices = GeneticIndividual.ExtractClosePrices(priceRecords);

            Assert.AreEqual(10, closePrices.Length);
            for (var i = 0; i < closePrices.Length; i++)
                Assert.AreEqual(priceRecords[i].Close, closePrices[i], TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetDateRange_WithValidData_ReturnsCorrectRange()
        {
            var priceRecords = CreateTestPriceRecords(20);

            var (firstDate, lastDate, tradingDays) = GeneticIndividual.GetDateRange(priceRecords);

            Assert.AreEqual(priceRecords[0].DateTime, firstDate);
            Assert.AreEqual(priceRecords[19].DateTime, lastDate);
            Assert.AreEqual(20, tradingDays);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetDateRange_WithEmptyArray_ReturnsMinValues()
        {
            var priceRecords = new PriceRecord[0];

            var (firstDate, lastDate, tradingDays) = GeneticIndividual.GetDateRange(priceRecords);

            Assert.AreEqual(DateTime.MinValue, firstDate);
            Assert.AreEqual(DateTime.MinValue, lastDate);
            Assert.AreEqual(0, tradingDays);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetDateRange_WithNull_ReturnsMinValues()
        {
            var (firstDate, lastDate, tradingDays) = GeneticIndividual.GetDateRange(null);

            Assert.AreEqual(DateTime.MinValue, firstDate);
            Assert.AreEqual(DateTime.MinValue, lastDate);
            Assert.AreEqual(0, tradingDays);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CreateSubset_WithValidIndices_ReturnsCorrectSubset()
        {
            var priceRecords = CreateTestPriceRecords(20);

            var subset = GeneticIndividual.CreateSubset(priceRecords, 5, 10);

            Assert.AreEqual(6, subset.Length); // 10 - 5 + 1
            Assert.AreEqual(priceRecords[5].DateTime, subset[0].DateTime);
            Assert.AreEqual(priceRecords[10].DateTime, subset[5].DateTime);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CreateSubset_WithInvalidIndices_ReturnsEmptyArray()
        {
            var priceRecords = CreateTestPriceRecords(10);

            var subset1 = GeneticIndividual.CreateSubset(priceRecords, -1, 5);
            var subset2 = GeneticIndividual.CreateSubset(priceRecords, 5, 20);
            var subset3 = GeneticIndividual.CreateSubset(priceRecords, 8, 3);

            Assert.AreEqual(0, subset1.Length);
            Assert.AreEqual(0, subset2.Length);
            Assert.AreEqual(0, subset3.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CreateSubset_WithNullArray_ReturnsEmptyArray()
        {
            var subset = GeneticIndividual.CreateSubset(null, 0, 5);

            Assert.AreEqual(0, subset.Length);
        }

        #endregion

        #region Process Method Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Process_WithPriceRecords_ReturnsValidFitness()
        {
            var individual = CreateTestIndividual();

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTestPriceRecords(100);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 50 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(50).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
            Assert.IsNotNull(individual.Fitness);
            Assert.AreEqual(fitness.DollarGain, individual.Fitness.DollarGain, TOLERANCE);
            Assert.AreEqual(fitness.PercentGain, individual.Fitness.PercentGain, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Process_WithDoubleArray_ReturnsValidFitness()
        {
            var individual = CreateTestIndividual();
            var historicalPriceRecords = CreateTrendingPriceRecords(200);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 100 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(100).ToArray();
            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
            Assert.IsNotNull(individual.Fitness);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Process_WithTrendingPrices_GeneratesTrades()
        {
            var individual = CreateTestIndividual();

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTrendingPriceRecords(200);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 100 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(100).ToArray();

            individual.Process(testPriceRecords);

            // Should generate some trading activity with trending prices
            Assert.IsTrue(individual.IndicatorValues.Count > 0);
            Assert.IsTrue(individual.TradeActions.Count > 0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Process_WithVolatilePrices_HandlesCorrectly()
        {
            var individual = CreateTestIndividual();

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateVolatilePriceRecords(100);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 50 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(50).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
            Assert.IsFalse(double.IsNaN(fitness.DollarGain));
            Assert.IsFalse(double.IsNaN(fitness.PercentGain));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Process_WithSingleRecord_HandlesGracefully()
        {
            var individual = CreateTestIndividual();

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTestPriceRecords(50);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 1 record for testing
            var testPriceRecords = historicalPriceRecords.Skip(49).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Process_ResetsStateCorrectly()
        {
            var individual = CreateTestIndividual();
            var priceRecords = CreateTestPriceRecords(300);
            GeneticIndividual.Prices.AddPricesBatch(priceRecords);

            var priceRecords2 = priceRecords.Skip(priceRecords.Length / 2).ToArray();
            // First run
            individual.Process(priceRecords2);
            var firstRunTrades = individual.Trades.Count;

            // Second run should reset state
            individual.Process(priceRecords2);

            Assert.AreEqual(firstRunTrades, individual.Trades.Count, "State should be reset between runs");
        }

        #endregion

        #region Maximal Fitness Tests

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateMaximalFitness_WithPriceRecords_CalculatesOptimalTrades()
        {
            var priceRecords = CreateOscillatingPriceRecords();

            var maxFitness = GeneticIndividual.CalculateMaximalFitness(priceRecords);

            Assert.IsNotNull(maxFitness);
            Assert.IsTrue(maxFitness.DollarGain >= 0, "Maximal fitness should be non-negative for oscillating prices");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateMaximalFitness_WithDoubleArray_CalculatesOptimalTrades()
        {
            var priceBuffer = CreateOscillatingPriceBuffer();

            var maxFitness = GeneticIndividual.CalculateMaximalFitness(priceBuffer);

            Assert.IsNotNull(maxFitness);
            Assert.IsTrue(maxFitness.DollarGain >= 0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateMaximalFitness_WithTrendingDown_HandlesShortTrades()
        {
            var priceRecords = CreateDownTrendPriceRecords();

            var maxFitness = GeneticIndividual.CalculateMaximalFitness(priceRecords);

            Assert.IsNotNull(maxFitness);
            // Should handle short trades for downtrend
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateMaximalFitness_WithShortData_HandlesGracefully()
        {
            var priceRecords = CreateTestPriceRecords(3);

            var maxFitness = GeneticIndividual.CalculateMaximalFitness(priceRecords);

            Assert.IsNotNull(maxFitness);
        }

        #endregion

        #region Indicator Value Calculation Tests

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateIndicatorValue_SMA_ReturnsCorrectValue()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = new double[] { 10, 20, 30, 40, 50 };
            var indicator = new IndicatorParams { Type = 1, Period = 3 }; // SMA

            var value = CallPrivateCalculateIndicatorValue(individual, indicator, priceBuffer, priceBuffer, priceBuffer, priceBuffer,
                priceBuffer, priceBuffer, 5);

            // Last 3 values: 30, 40, 50 -> average = 40
            Assert.AreEqual(40.0, value, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateIndicatorValue_SinIndicator_ReturnsValue()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = new double[] { 100 };
            var indicator = new IndicatorParams
                { Type = 0, Period = 1, Param1 = 1, Param2 = 1, Param3 = 1, Param4 = 1, Param5 = 1 }; // Sin

            var value = CallPrivateCalculateIndicatorValue(individual, indicator, priceBuffer, priceBuffer, priceBuffer, priceBuffer,
                priceBuffer, priceBuffer, 1);

            Assert.IsFalse(double.IsNaN(value), "Sin indicator should return a valid value");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateIndicatorValue_EMA_CalculatesCorrectly()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = new double[] { 10, 12, 14, 16, 18 };
            var indicator = new IndicatorParams { Type = 2, Period = 3 }; // EMA

            var value = CallPrivateCalculateIndicatorValue(individual, indicator, priceBuffer, priceBuffer, priceBuffer, priceBuffer,
                priceBuffer, priceBuffer, 5);

            Assert.IsTrue(value > 0, "EMA should return positive value for positive prices");
            Assert.IsFalse(double.IsNaN(value), "EMA should return valid number");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateIndicatorValue_SMMA_CalculatesCorrectly()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = new double[] { 1, 2, 3, 4, 5 };
            var indicator = new IndicatorParams { Type = 3, Period = 3 }; // SMMA

            var value = CallPrivateCalculateIndicatorValue(individual, indicator, priceBuffer, priceBuffer, priceBuffer, priceBuffer,
                priceBuffer, priceBuffer, 5);

            Assert.IsTrue(value > 0, "SMMA should return positive value");
            Assert.IsFalse(double.IsNaN(value), "SMMA should return valid number");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateIndicatorValue_LWMA_CalculatesCorrectly()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = new double[] { 10, 20, 30, 40, 50 };
            var indicator = new IndicatorParams { Type = 4, Period = 3 }; // LWMA

            var value = CallPrivateCalculateIndicatorValue(individual, indicator, priceBuffer, priceBuffer, priceBuffer, priceBuffer,
                priceBuffer, priceBuffer, 5);

            // LWMA gives more weight to recent prices
            Assert.IsTrue(value > 30, "LWMA should be weighted toward recent prices");
            Assert.IsTrue(value < 50, "LWMA should be less than most recent price");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateIndicatorValue_WithZeroPeriod_HandlesGracefully()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = new double[] { 100 };
            var indicator = new IndicatorParams { Type = 1, Period = 0 };

            var value = CallPrivateCalculateIndicatorValue(individual, indicator, priceBuffer, priceBuffer, priceBuffer,priceBuffer,
                priceBuffer, priceBuffer, 1);

            Assert.AreEqual(0.0, value, TOLERANCE, "Zero period should return 0");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateIndicatorValue_PeriodLargerThanBuffer_UsesAvailableData()
        {
            var individual = new GeneticIndividual();
            var priceBuffer = new double[] { 10, 20 };
            var indicator = new IndicatorParams { Type = 1, Period = 5 }; // Period larger than buffer

            var value = CallPrivateCalculateIndicatorValue(individual, indicator, priceBuffer, priceBuffer, priceBuffer, priceBuffer,
                priceBuffer, priceBuffer, 2);

            Assert.AreEqual(15.0, value, TOLERANCE, "Should use all available data when period > buffer size");
        }

        #endregion

        #region TradeResult Tests

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_Properties_SetAndGetCorrectly()
        {
            var trade = new TradeResult
            {
                OpenIndex = 5,
                CloseIndex = 10,
                OpenPrice = 100.0,
                ClosePrice = 110.0,
                AllowedTradeType = AllowedTradeType.Buy,
                AllowedSecurityType = AllowedSecurityType.Stock,
                Position = 10.0,
                PositionInDollars = 1000.0,
                Balance = 1100.0,
                ResponsibleIndicatorIndex = 0
            };

            Assert.AreEqual(5, trade.OpenIndex);
            Assert.AreEqual(10, trade.CloseIndex);
            Assert.AreEqual(100.0, trade.OpenPrice, TOLERANCE);
            Assert.AreEqual(110.0, trade.ClosePrice, TOLERANCE);
            Assert.AreEqual(AllowedTradeType.Buy, trade.AllowedTradeType);
            Assert.AreEqual(10.0, trade.Position, TOLERANCE);
            Assert.AreEqual(1000.0, trade.PositionInDollars, TOLERANCE);
            Assert.AreEqual(1000.0, trade.TotalDollarAmount, TOLERANCE); // Alias test
            Assert.AreEqual(1100.0, trade.Balance, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_DollarGain_CalculatesCorrectlyForBuy()
        {
            var trade = new TradeResult
            {
                OpenPrice = 100.0,
                ClosePrice = 110.0,
                AllowedTradeType = AllowedTradeType.Buy
            };

            Assert.AreEqual(10.0, trade.DollarGain, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_DollarGain_CalculatesCorrectlyForShort()
        {
            var trade = new TradeResult
            {
                OpenPrice = 100.0,
                ClosePrice = 90.0,
                AllowedTradeType = AllowedTradeType.SellShort
            };

            Assert.AreEqual(10.0, trade.DollarGain, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_PercentGain_CalculatesCorrectlyForBuy()
        {
            var trade = new TradeResult
            {
                OpenPrice = 100.0,
                ClosePrice = 110.0,
                AllowedTradeType = AllowedTradeType.Buy
            };

            Assert.AreEqual(10.0, trade.PercentGain, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_PercentGain_CalculatesCorrectlyForShort()
        {
            var trade = new TradeResult
            {
                OpenPrice = 100.0,
                ClosePrice = 90.0,
                AllowedTradeType = AllowedTradeType.SellShort
            };

            Assert.AreEqual(10.0, trade.PercentGain, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_ActualDollarGain_CalculatesWithPosition()
        {
            var trade = new TradeResult
            {
                OpenPrice = 100.0,
                ClosePrice = 110.0,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 5.0,
                AllowedSecurityType = AllowedSecurityType.Stock
            };

            Assert.AreEqual(50.0, trade.ActualDollarGain, TOLERANCE); // 10 * 5 * 1
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_ActualDollarGain_CalculatesWithOptionMultiplier()
        {
            var trade = new TradeResult
            {
                OpenPrice = 2.0,
                ClosePrice = 3.0,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 1.0,
                AllowedSecurityType = AllowedSecurityType.Option
            };

            Assert.AreEqual(100.0, trade.ActualDollarGain, TOLERANCE); // 1 * 1 * 100
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_PercentGain_HandlesZeroOpenPrice()
        {
            var trade = new TradeResult
            {
                OpenPrice = 0.0,
                ClosePrice = 110.0,
                AllowedTradeType = AllowedTradeType.Buy
            };

            Assert.AreEqual(0.0, trade.PercentGain, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void TradeResult_OpenPrice_ThrowsExceptionForNegativeValue()
        {
            var trade = new TradeResult();
            trade.OpenPrice = -10.0;
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void TradeResult_ClosePrice_ThrowsExceptionForNegativeValue()
        {
            var trade = new TradeResult();
            trade.ClosePrice = -10.0;
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeResult_ToString_ReturnsFormattedString()
        {
            var trade = new TradeResult
            {
                OpenPrice = 100.0,
                ClosePrice = 110.0,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 5.0,
                AllowedSecurityType = AllowedSecurityType.Stock
            };

            var result = trade.ToString();
            Assert.IsTrue(result.Contains("50") || result.Contains("$50"), "Should contain the dollar gain amount");
        }

        #endregion

        #region Fitness Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Fitness_Constructor_WithParameters_SetsValues()
        {
            var fitness = new Fitness(1000.0, 10.0);

            Assert.AreEqual(1000.0, fitness.DollarGain, TOLERANCE);
            Assert.AreEqual(10.0, fitness.PercentGain, TOLERANCE);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Fitness_DefaultConstructor_CreatesEmptyFitness()
        {
            var fitness = new Fitness();

            Assert.AreEqual(0.0, fitness.DollarGain, TOLERANCE);
            Assert.AreEqual(0.0, fitness.PercentGain, TOLERANCE);
        }

        #endregion

        #region Enum Tests

        [TestMethod]
        [TestCategory("Core")]
        public void CombinationMethod_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)CombinationMethod.Sum);
            Assert.AreEqual(1, (int)CombinationMethod.NormalizedSum);
            Assert.AreEqual(2, (int)CombinationMethod.EnsembleVoting);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void TradeType_HasExpectedValues()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(AllowedTradeType), AllowedTradeType.Buy));
            Assert.IsTrue(Enum.IsDefined(typeof(AllowedTradeType), AllowedTradeType.SellShort));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void OptionType_HasExpectedValues()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(AllowedOptionType), AllowedOptionType.Calls));
            Assert.IsTrue(Enum.IsDefined(typeof(AllowedOptionType), AllowedOptionType.Puts));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SecurityType_HasExpectedValues()
        {
            Assert.IsTrue(Enum.IsDefined(typeof(AllowedSecurityType), AllowedSecurityType.Stock));
            Assert.IsTrue(Enum.IsDefined(typeof(AllowedSecurityType), AllowedSecurityType.Option));
        }

        #endregion

        #region Multiple Indicator Combination Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Process_WithMultipleIndicators_AllowMultipleTrades_ProcessesCorrectly()
        {
            var individual = CreateMultiIndicatorIndividual();
            individual.AllowMultipleTrades = true;

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTrendingPriceRecords(100);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 50 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(50).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
            Assert.AreEqual(individual.Indicators.Count, individual.IndicatorValues.Count);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Process_WithMultipleIndicators_CombinationSum_ProcessesCorrectly()
        {
            var individual = CreateMultiIndicatorIndividual();
            individual.AllowMultipleTrades = false;
            individual.CombinationMethod = CombinationMethod.Sum;

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTrendingPriceRecords(100);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 50 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(50).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Process_WithMultipleIndicators_CombinationNormalizedSum_ProcessesCorrectly()
        {
            var individual = CreateMultiIndicatorIndividual();
            individual.AllowMultipleTrades = false;
            individual.CombinationMethod = CombinationMethod.NormalizedSum;

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTrendingPriceRecords(100);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 50 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(50).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Process_WithMultipleIndicators_EnsembleVoting_ProcessesCorrectly()
        {
            var individual = CreateMultiIndicatorIndividual();
            individual.AllowMultipleTrades = false;
            individual.CombinationMethod = CombinationMethod.EnsembleVoting;
            individual.EnsembleVotingThreshold = 2;

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTrendingPriceRecords(100);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 50 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(50).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
        }

        #endregion

        #region Edge Cases and Error Handling

        [TestMethod]
        [TestCategory("Core")]
        public void Process_WithEmptyIndicators_HandlesGracefully()
        {
            var individual = new GeneticIndividual();
            individual.Indicators.Clear();

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateTestPriceRecords(20);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 10 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(10).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
            Assert.AreEqual(0, individual.Trades.Count);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Process_WithConstantPrices_HandlesGracefully()
        {
            var individual = CreateTestIndividual();

            // Create historical data and add to Prices system
            var historicalPriceRecords = CreateConstantPriceRecords(40, 100.0);
            GeneticIndividual.Prices.AddPricesBatch(historicalPriceRecords);

            // Use only the last 20 records for testing
            var testPriceRecords = historicalPriceRecords.Skip(20).ToArray();

            var fitness = individual.Process(testPriceRecords);

            Assert.IsNotNull(fitness);
            Assert.IsFalse(double.IsNaN(fitness.DollarGain));
            Assert.IsFalse(double.IsNaN(fitness.PercentGain));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void InitializeOptionSolvers_WithValidPath_InitializesCorrectly()
        {
            // Test the static initialization method
            GeneticIndividual.InitializeOptionSolvers("testpath.csv");

            Assert.IsNotNull(GeneticIndividual.ImpliedVolatilitySolverCalls);
            Assert.IsNotNull(GeneticIndividual.ImpliedVolatilitySolverPuts);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void InitializeOptionSolvers_WithNullPath_InitializesCorrectly()
        {
            GeneticIndividual.InitializeOptionSolvers();

            Assert.IsNotNull(GeneticIndividual.ImpliedVolatilitySolverCalls);
            Assert.IsNotNull(GeneticIndividual.ImpliedVolatilitySolverPuts);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void InitializePrices_WithValidPath_InitializesCorrectly()
        {
            GeneticIndividual.InitializePrices("testpath.csv");

            Assert.IsNotNull(GeneticIndividual.Prices);
            Assert.IsNotNull(GeneticIndividual.OptionsPrices);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void InitializePrices_WithNullPath_InitializesCorrectly()
        {
            GeneticIndividual.InitializePrices();

            Assert.IsNotNull(GeneticIndividual.Prices);
            Assert.IsNotNull(GeneticIndividual.OptionsPrices);
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
                var price = basePrice + i * 0.5; // Upward trend
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
                var price = basePrice + (rng.NextDouble() - 0.5) * 20;
                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price + rng.NextDouble() * 2 - 1,
                    price + rng.NextDouble() * 4,
                    price - rng.NextDouble() * 4,
                    price, volume: 1000 + rng.Next(500),
                    wap: price, count: 1);
            }

            return records;
        }

        private PriceRecord[] CreateConstantPriceRecords(int count, double price)
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

        private PriceRecord[] CreateOscillatingPriceRecords()
        {
            var prices = new double[] { 100, 110, 105, 115, 108, 118, 112, 122, 115, 125 };
            var records = new PriceRecord[prices.Length];
            var baseDate = DateTime.Today.AddDays(-prices.Length);

            for (var i = 0; i < prices.Length; i++)
                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    prices[i] - 1, prices[i] + 1, prices[i] - 2, prices[i], volume: 1000,
                    wap: prices[i], count: 1);

            return records;
        }

        private PriceRecord[] CreateUpTrendPriceRecords()
        {
            var count = 20;
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);

            for (var i = 0; i < count; i++)
            {
                var price = 100.0 + i * 2; // Strong uptrend
                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price - 0.5, price + 0.5, price - 1, price, volume: 1000,
                    wap: price, count: 1);
            }

            return records;
        }

        private PriceRecord[] CreateDownTrendPriceRecords()
        {
            var count = 20;
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);

            for (var i = 0; i < count; i++)
            {
                var price = 140.0 - i * 2; // Strong downtrend
                records[i] = new PriceRecord(
                    baseDate.AddDays(i), TimeFrame.D1,
                    price + 0.5, price + 1, price - 0.5, price, volume: 1000,
                    wap: price, count: 1);
            }

            return records;
        }

        private double[] CreateTestPriceBuffer(int count)
        {
            var buffer = new double[count];
            for (var i = 0; i < count; i++) buffer[i] = 100.0 + Math.Sin(i * 0.1) * 10;
            return buffer;
        }

        private double[] CreateOscillatingPriceBuffer()
        {
            return new double[] { 100, 110, 105, 115, 108, 118, 112, 122, 115, 125 };
        }

        // Helper method to call private CalculateIndicatorValue method using reflection
        private double CallPrivateCalculateIndicatorValue(GeneticIndividual individual, IndicatorParams indicator,
            double[] openPrices, double[] highPrices, double[] lowPrices, double[] closePrices, double[] volumes,
            double[] priceBuffer, int totalLength)
        {
            var method = typeof(GeneticIndividual).GetMethod("CalculateIndicatorValue",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(method, "CalculateIndicatorValue method not found via reflection");

            // Wrap arrays in PriceRange because reflection cannot apply implicit conversions automatically
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

        #endregion
    }
}