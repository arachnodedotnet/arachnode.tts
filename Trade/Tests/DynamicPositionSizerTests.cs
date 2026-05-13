using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class DynamicPositionSizerTests
    {
        private const double TOLERANCE = 1e-6;

        [TestMethod][TestCategory("Core")]
        public void Constructor_WithDefaultConfig_InitializesCorrectly()
        {
            var sizer = new DynamicPositionSizer();

            Assert.IsNotNull(sizer);
            // Sizer should be created with default configuration
        }

        [TestMethod][TestCategory("Core")]
        public void Constructor_WithCustomConfig_UsesProvidedConfig()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.KellyOptimal,
                BaseRiskPerTrade = 0.03,
                MaxPositionSize = 0.15
            };

            var sizer = new DynamicPositionSizer(config);

            Assert.IsNotNull(sizer);
            // Internal config should be used (can't directly test private field)
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_FixedPercentageMethod_ReturnsBaseRisk()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.FixedPercentage,
                BaseRiskPerTrade = 0.05
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();

            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.AreEqual(0.05, result.PositionSize, TOLERANCE);
            Assert.AreEqual("FixedPercentage", result.PrimarySizingFactor);
            Assert.AreEqual("NORMAL RISK", result.RiskAssessment);
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_KellyOptimalWithInsufficientHistory_FallsBackToBaseRisk()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.KellyOptimal,
                BaseRiskPerTrade = 0.02
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();
            // Only add 5 trades (less than required 10)
            context.RecentTrades = CreateTradeHistory(5);

            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.AreEqual(0.02, result.PositionSize, TOLERANCE);
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("Insufficient trade history")));
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_KellyOptimalWithSufficientHistory_CalculatesKelly()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.KellyOptimal,
                BaseRiskPerTrade = 0.02,
                KellyMultiplier = 0.25
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();
            // Create profitable trade history
            context.RecentTrades = CreateProfitableTradeHistory(20);

            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("Kelly:")));
            // Kelly sizing should be different from base risk
            Assert.AreNotEqual(0.02, result.PositionSize, TOLERANCE);
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_ATRBased_UsesATRForSizing()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.ATRBased,
                BaseRiskPerTrade = 0.02,
                ATRMultiplier = 2.0
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();
            context.ATR = 2.5; // Set ATR value
            context.AccountBalance = 10000;
            context.AvailableBalance = 10000;
            context.CurrentPrice = 100.0;

            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("ATR:")));
            Assert.IsTrue(result.StopLoss > 0); // ATR method sets stop loss
            Assert.AreEqual(95.0, result.StopLoss, TOLERANCE); // 100 - (2.5 * 2.0)
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_DrawdownProtective_ReducesSizeDuringDrawdown()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.DrawdownProtective,
                BaseRiskPerTrade = 0.02,
                DrawdownThreshold = 0.10,
                DrawdownReduction = 0.5
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();

            // First call to establish peak balance
            context.AccountBalance = 10000;
            sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            // Second call with lower balance (15% drawdown)
            context.AccountBalance = 8500;
            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.ReducedByDrawdown);
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("Drawdown protection")));
            Assert.IsTrue(result.PositionSize < config.BaseRiskPerTrade);
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_MomentumAdaptive_IncreasesSizeWithMomentum()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.MomentumAdaptive,
                BaseRiskPerTrade = 0.02,
                MomentumThreshold = 0.05,
                MomentumMultiplier = 1.5
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();
            context.MarketMomentum = 0.08; // Strong momentum

            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("Momentum:")));
            Assert.IsTrue(result.PositionSize > config.BaseRiskPerTrade);
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_ConservativeRiskMode_ReducesSize()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.FixedPercentage,
                BaseRiskPerTrade = 0.02,
                RiskMode = RiskAdjustmentMode.Conservative
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();

            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("Conservative risk mode")));
            Assert.AreEqual(0.014, result.PositionSize, TOLERANCE); // 0.02 * 0.7
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_AggressiveRiskMode_IncreasesSize()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.FixedPercentage,
                BaseRiskPerTrade = 0.02,
                RiskMode = RiskAdjustmentMode.Aggressive
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();

            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("Aggressive risk mode")));
            Assert.AreEqual(0.026, result.PositionSize, TOLERANCE); // 0.02 * 1.3
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_HeatPeriod_ReducesSize()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.FixedPercentage,
                BaseRiskPerTrade = 0.02,
                EnableHeatAdjustment = true
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();
            context.IsHeatPeriod = true;

            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("Heat period")));
            Assert.AreEqual(0.012, result.PositionSize, TOLERANCE); // 0.02 * 0.6
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_HighCorrelation_ReducesSize()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.FixedPercentage,
                BaseRiskPerTrade = 0.02,
                MaxCorrelation = 0.7,
                CorrelationReduction = 0.5
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();
            context.MaxCorrelationWithExisting = 0.8; // High correlation

            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.BlockedByCorrelation);
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("High correlation")));
            Assert.AreEqual(0.01, result.PositionSize, TOLERANCE); // 0.02 * 0.5
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_MaxConcurrentPositions_ReducesSize()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.FixedPercentage,
                BaseRiskPerTrade = 0.02,
                EnableConcurrentPositionLimit = true,
                MaxConcurrentPositions = 3
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();
            context.OpenPositions = CreateOpenPositions(4); // Exceed limit

            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("Position limit reached")));
            Assert.AreEqual(0.01, result.PositionSize, TOLERANCE); // 0.02 * 0.5
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_HighExposure_ReducesSize()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.FixedPercentage,
                BaseRiskPerTrade = 0.02
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();
            context.TotalExposure = 0.85; // High exposure

            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("High exposure")));
            Assert.IsTrue(result.PositionSize < config.BaseRiskPerTrade);
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_ExceedsMaxSize_CapsAtMaximum()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.FixedPercentage,
                BaseRiskPerTrade = 0.30, // Very high base risk
                MaxPositionSize = 0.25,
                RiskMode = RiskAdjustmentMode.Aggressive
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();

            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.HitMaxPositionLimit);
            Assert.AreEqual(0.25, result.PositionSize, TOLERANCE);
            Assert.AreEqual("HIGH RISK", result.RiskAssessment);
        }

        [TestMethod][TestCategory("Core")]
        public void GetSuggestedMethod_WithSufficientTradeHistory_ReturnsKellyOptimal()
        {
            var sizer = new DynamicPositionSizer();
            var context = CreateBasicContext();
            context.RecentTrades = CreateProfitableTradeHistory(35);
            context.WinRate = 0.6;

            var suggested = sizer.GetSuggestedMethod(context);

            Assert.AreEqual(PositionSizingMethod.KellyOptimal, suggested);
        }

        [TestMethod][TestCategory("Core")]
        public void GetSuggestedMethod_WithHighVolatility_ReturnsVolatilityAdjusted()
        {
            var sizer = new DynamicPositionSizer();
            var context = CreateBasicContext();
            context.MarketVolatility = 0.30;

            var suggested = sizer.GetSuggestedMethod(context);

            Assert.AreEqual(PositionSizingMethod.VolatilityAdjusted, suggested);
        }

        [TestMethod][TestCategory("Core")]
        public void GetSuggestedMethod_WithHighDrawdown_ReturnsDrawdownProtective()
        {
            var sizer = new DynamicPositionSizer();
            var context = CreateBasicContext();

            // Simulate drawdown by calling CalculatePositionSize twice
            context.AccountBalance = 10000;
            sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);
            context.AccountBalance = 8000; // 20% drawdown
            sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            var suggested = sizer.GetSuggestedMethod(context);

            Assert.AreEqual(PositionSizingMethod.DrawdownProtective, suggested);
        }

        [TestMethod][TestCategory("Core")]
        public void GetSuggestedMethod_WithHighMomentum_ReturnsMomentumAdaptive()
        {
            var sizer = new DynamicPositionSizer();
            var context = CreateBasicContext();
            context.MarketMomentum = 0.12;

            var suggested = sizer.GetSuggestedMethod(context);

            Assert.AreEqual(PositionSizingMethod.MomentumAdaptive, suggested);
        }

        [TestMethod][TestCategory("Core")]
        public void GetSuggestedMethod_DefaultConditions_ReturnsFixedPercentage()
        {
            var sizer = new DynamicPositionSizer();
            var context = CreateBasicContext();

            var suggested = sizer.GetSuggestedMethod(context);

            Assert.AreEqual(PositionSizingMethod.FixedPercentage, suggested);
        }

        [TestMethod][TestCategory("Core")]
        public void CalculatePositionSize_WithComplexScenario_HandlesAllAdjustments()
        {
            var config = new PositionSizingConfig
            {
                Method = PositionSizingMethod.KellyOptimal,
                BaseRiskPerTrade = 0.03,
                RiskMode = RiskAdjustmentMode.Conservative,
                EnableHeatAdjustment = true,
                MaxCorrelation = 0.6,
                CorrelationReduction = 0.7
            };
            var sizer = new DynamicPositionSizer(config);
            var context = CreateBasicContext();
            context.RecentTrades = CreateMixedTradeHistory(25);
            context.IsHeatPeriod = true;
            context.MaxCorrelationWithExisting = 0.8;
            context.MarketVolatility = 0.30;

            var result = sizer.CalculatePositionSize(context, 100.0, AllowedTradeType.Buy);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.AdjustmentFactors.Count > 3); // Multiple adjustments applied
            Assert.IsTrue(result.BlockedByCorrelation);
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("Kelly:")));
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("Conservative")));
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("Heat period")));
            Assert.IsTrue(result.AdjustmentFactors.Any(f => f.Contains("High correlation")));
        }

        #region Helper Methods

        private PositionSizingContext CreateBasicContext()
        {
            return new PositionSizingContext
            {
                AccountBalance = 10000,
                AvailableBalance = 10000,
                CurrentPrice = 100.0,
                PriceHistory = CreatePriceHistory(20, 100.0, 0.15),
                RecentTrades = new List<TradeResult>(),
                OpenPositions = new List<TradeResult>(),
                WinRate = 0.5,
                ProfitFactor = 1.2,
                SharpeRatio = 0.8,
                MarketVolatility = 0.15,
                MarketMomentum = 0.02,
                ATR = 1.5,
                CurrentRegime = MarketRegime.Unknown,
                TotalExposure = 0.3,
                MaxCorrelationWithExisting = 0.2,
                IsHeatPeriod = false
            };
        }

        private PriceRecord[] CreatePriceHistory(int count, double startPrice, double volatility)
        {
            var records = new PriceRecord[count];
            var rng = new Random(42);
            var price = startPrice;

            for (var i = 0; i < count; i++)
            {
                var change = (rng.NextDouble() - 0.5) * volatility * price;
                price = Math.Max(1.0, price + change);

                records[i] = new PriceRecord
                {
                    DateTime = DateTime.Today.AddDays(-count + i),
                    Open = price,
                    High = price * 1.01,
                    Low = price * 0.99,
                    Close = price,
                    Volume = 1000,
                    IsComplete = true
                };
            }

            return records;
        }

        private List<TradeResult> CreateTradeHistory(int count)
        {
            var trades = new List<TradeResult>();
            var rng = new Random(42);

            for (var i = 0; i < count; i++)
                trades.Add(new TradeResult
                {
                    OpenPrice = 100.0,
                    ClosePrice = 100.0 + (rng.NextDouble() - 0.5) * 10,
                    AllowedTradeType = AllowedTradeType.Buy,
                    Position = 100,
                    Balance = 10000 + i * 100
                });

            return trades;
        }

        private List<TradeResult> CreateProfitableTradeHistory(int count)
        {
            var trades = new List<TradeResult>();
            var rng = new Random(42);

            for (var i = 0; i < count; i++)
            {
                // 70% winners, 30% losers
                var isWinner = rng.NextDouble() < 0.7;
                var gainLoss = isWinner ? rng.NextDouble() * 5 + 1 : -(rng.NextDouble() * 3 + 0.5);

                trades.Add(new TradeResult
                {
                    OpenPrice = 100.0,
                    ClosePrice = 100.0 + gainLoss,
                    AllowedTradeType = AllowedTradeType.Buy,
                    Position = 100,
                    Balance = 10000 + i * 50
                });
            }

            return trades;
        }

        private List<TradeResult> CreateMixedTradeHistory(int count)
        {
            var trades = new List<TradeResult>();
            var rng = new Random(42);

            for (var i = 0; i < count; i++)
            {
                // 55% winners, 45% losers - more realistic
                var isWinner = rng.NextDouble() < 0.55;
                var gainLoss = isWinner ? rng.NextDouble() * 3 + 0.5 : -(rng.NextDouble() * 2 + 0.3);

                trades.Add(new TradeResult
                {
                    OpenPrice = 100.0,
                    ClosePrice = 100.0 + gainLoss,
                    AllowedTradeType = AllowedTradeType.Buy,
                    Position = 100,
                    Balance = 10000 + i * 25
                });
            }

            return trades;
        }

        private List<TradeResult> CreateOpenPositions(int count)
        {
            var positions = new List<TradeResult>();

            for (var i = 0; i < count; i++)
                positions.Add(new TradeResult
                {
                    OpenPrice = 95.0 + i,
                    AllowedTradeType = AllowedTradeType.Buy,
                    Position = 50,
                    Balance = 10000
                });

            return positions;
        }

        #endregion
    }
}