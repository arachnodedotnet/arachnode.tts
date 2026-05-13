using System;
using System.Collections.Generic;
using System.Linq;
using Trade.Prices2;

namespace Trade
{
    public partial class GeneticIndividual
    {
        #region Performance Optimization Constants and Caches

        // Pre-computed enum values and arrays to avoid repeated reflection
        private static readonly TimeFrame[] _cachedTimeFrameValues = (TimeFrame[])Enum.GetValues(typeof(TimeFrame));
        private static readonly OHLC[] _cachedOHLCValues = (OHLC[])Enum.GetValues(typeof(OHLC));
        private static readonly PositionSizingMethod[] _cachedPositionSizingMethods = 
            (PositionSizingMethod[])Enum.GetValues(typeof(PositionSizingMethod));
        private static readonly RiskAdjustmentMode[] _cachedRiskAdjustmentModes = 
            (RiskAdjustmentMode[])Enum.GetValues(typeof(RiskAdjustmentMode));
        
        // Pre-computed TimeFrame indices for range calculations
        private static readonly Dictionary<TimeFrame, int> _timeFrameIndexLookup = new Dictionary<TimeFrame, int>();
        
        static GeneticIndividual()
        {
            // Initialize TimeFrame index lookup for O(1) range calculations
            for (int i = 0; i < _cachedTimeFrameValues.Length; i++)
            {
                _timeFrameIndexLookup[_cachedTimeFrameValues[i]] = i;
            }
        }

        #endregion

        /// <summary>
        /// Constructor for random initialization with centralized parameters and injected IV solvers
        /// OPTIMIZED: Eliminated LINQ operations, cached enum values, optimized TimeFrame range calculations
        /// </summary>
        public GeneticIndividual(Random rng,
            double startingBalance,
            int indicatorTypeMin, int indicatorTypeMax,
            int indicatorPeriodMin, int indicatorPeriodMax,
            int indicatorModeMin, int indicatorModeMax,
            TimeFrame indicatorTimeFrameMin, TimeFrame indicatorTimeFrameMax,
            int indicatorPolarityMin, int indicatorPolarityMax,
            double indicatorThresholdMin, double indicatorThresholdMax,
            int maxIndicators,
            // Split min/max for stocks vs options
            double tradePercentageForStocksMin, double tradePercentageForStocksMax,
            double tradePercentageForOptionsMin, double tradePercentageForOptionsMax,
            int optionDaysOutMin, int optionDaysOutMax,
            int optionStrikeDistanceMin, int optionStrikeDistanceMax,
            int fastMAPeriodMin, int fastMAPeriodMax,
            int slowMAPeriodMin, int slowMAPeriodMax,
            // NEW: Genetic parameter constraints
            int allowedTradeTypeMin, int allowedTradeTypeMax,
            int allowedOptionTypeMin, int allowedOptionTypeMax,
            int allowedSecurityTypeMin, int allowedSecurityTypeMax,
            int numberOfOptionContractsMin, int numberOfOptionContractsMax)
        {
            RandomNumberGenerator = rng;

            StartingBalance = startingBalance;
            _peakBalance = startingBalance;

            // Initialize genetic parameters for multiple indicator support
            AllowMultipleTrades = rng.NextDouble() > 0.5;
            CombinationMethod = (CombinationMethod)rng.Next(0, 3);
            EnsembleVotingThreshold = rng.Next(1, Math.Min(5, maxIndicators) + 1);

            // Initialize trading and option genetic parameters using constraints
            AllowedTradeTypes = (AllowedTradeType)rng.Next(allowedTradeTypeMin, allowedTradeTypeMax + 1);
            AllowedOptionTypes = (AllowedOptionType)rng.Next(allowedOptionTypeMin, allowedOptionTypeMax + 1);
            AllowedSecurityTypes = (AllowedSecurityType)rng.Next(allowedSecurityTypeMin, allowedSecurityTypeMax + 1);
            NumberOfOptionContractsToOpen = rng.Next(numberOfOptionContractsMin, numberOfOptionContractsMax + 1);
            OptionDaysOut = rng.Next(optionDaysOutMin, optionDaysOutMax + 1);
            OptionStrikeDistance = rng.Next(optionStrikeDistanceMin, optionStrikeDistanceMax + 1);

            // OPTIMIZATION: Use optimized scale-out fraction generation
            OptionContractsToScaleOut = GenerateValidScaleOutFractionsOptimized(rng, NumberOfOptionContractsToOpen);

            // Initialize trade percentage as GA parameters (separate for stocks vs options)
            // OPTIMIZATION: Pre-calculate integer ranges to avoid repeated floating-point operations
            var tpSMin = (int)(tradePercentageForStocksMin * 100);
            var tpSMax = (int)((tradePercentageForStocksMax + 0.01) * 100);
            TradePercentageForStocks = (double)rng.Next(tpSMin, tpSMax) / 100.0;

            var tpOMin = (int)(tradePercentageForOptionsMin * 100);
            var tpOMax = (int)((tradePercentageForOptionsMax + 0.01) * 100);
            TradePercentageForOptions = (double)rng.Next(tpOMin, tpOMax) / 100.0;

            // Initialize fast/slow MA periods for indicators that require them
            FastMAPeriod = rng.Next(fastMAPeriodMin, fastMAPeriodMax + 1);
            SlowMAPeriod = rng.Next(slowMAPeriodMin, slowMAPeriodMax + 1);

            // Initialize Dynamic Position Sizing Parameters
            InitializeDynamicPositionSizingOptimized(rng);

            // OPTIMIZATION: Use cached OHLC values instead of Enum.GetValues
            OHLC = _cachedOHLCValues[rng.Next(_cachedOHLCValues.Length)];

            // NEW: Initialize top-level price buffer source
            BufferSource = rng.NextDouble() < 0.5 ? PriceBufferSource.UseStockPriceBuffer : PriceBufferSource.UseOptionPriceBuffer;

            // OPTIMIZATION: Pre-calculate TimeFrame range indices for efficient random selection
            int minTimeFrameIndex, maxTimeFrameIndex;
            if (!_timeFrameIndexLookup.TryGetValue(indicatorTimeFrameMin, out minTimeFrameIndex))
                minTimeFrameIndex = 0;
            if (!_timeFrameIndexLookup.TryGetValue(indicatorTimeFrameMax, out maxTimeFrameIndex))
                maxTimeFrameIndex = _cachedTimeFrameValues.Length - 1;

            var numIndicators = rng.Next(1, maxIndicators + 1);
            
            // OPTIMIZATION: Pre-allocate Indicators list with known capacity
            Indicators = new List<IndicatorParams>(numIndicators);
            
            for (var i = 0; i < numIndicators; i++)
            {
                var type = rng.Next(indicatorTypeMin, indicatorTypeMax + 1);
                int polarity;
                do
                {
                    polarity = rng.Next(indicatorPolarityMin, indicatorPolarityMax + 1);
                } while (polarity == 0);

                // OPTIMIZATION: Use cached OHLC values and optimized TimeFrame selection
                var randomOhlc = _cachedOHLCValues[rng.Next(_cachedOHLCValues.Length)];
                var randomTimeFrame = _cachedTimeFrameValues[rng.Next(minTimeFrameIndex, maxTimeFrameIndex + 1)];

                // Initialize fast/slow MA periods for indicators that require them
                var fastMAPeriod = rng.Next(fastMAPeriodMin, fastMAPeriodMax + 1);
                var slowMAPeriod = rng.Next(slowMAPeriodMin, slowMAPeriodMax + 1);
                
                var ind = new IndicatorParams
                {
                    Type = type,
                    Period = rng.Next(indicatorPeriodMin, indicatorPeriodMax + 1),
                    Mode = rng.Next(indicatorModeMin, indicatorModeMax + 1),
                    OHLC = randomOhlc,
                    TimeFrame = randomTimeFrame,
                    Polarity = polarity,
                    FastMAPeriod = fastMAPeriod,
                    SlowMAPeriod = slowMAPeriod,
                    LongThreshold = indicatorThresholdMin +
                                    rng.NextDouble() * (indicatorThresholdMax - indicatorThresholdMin),
                    ShortThreshold = -(indicatorThresholdMin +
                                       rng.NextDouble() * (indicatorThresholdMax - indicatorThresholdMin)),
                    // NEW: random buffer source per indicator
                    BufferSource = rng.NextDouble() < 0.75
                        ? PriceBufferSource.UseStockPriceBuffer
                        : PriceBufferSource.UseOptionPriceBuffer
                };
                Indicators.Add(ind);
            }
        }

        /// <summary>
        /// Initialize dynamic position sizing parameters with genetic variation
        /// OPTIMIZED: Use cached enum values instead of LINQ operations
        /// </summary>
        private void InitializeDynamicPositionSizingOptimized(Random rng)
        {
            // Randomly enable/disable dynamic position sizing (80% chance to use it)
            UseDynamicPositionSizing = rng.NextDouble() > 0.2;

            if (UseDynamicPositionSizing)
            {
                // OPTIMIZATION: Use cached enum values instead of LINQ Cast operations
                PositionSizingMethod = _cachedPositionSizingMethods[rng.Next(_cachedPositionSizingMethods.Length)];
                RiskAdjustmentMode = _cachedRiskAdjustmentModes[rng.Next(_cachedRiskAdjustmentModes.Length)];

                // Evolve position sizing parameters
                MaxPositionSize = 0.05 + rng.NextDouble() * 0.20; // 5% to 25%
                BaseRiskPerTrade = 0.01 + rng.NextDouble() * 0.04; // 1% to 5%
                VolatilityTarget = 0.10 + rng.NextDouble() * 0.20; // 10% to 30%
                KellyMultiplier = 0.1 + rng.NextDouble() * 0.4; // 10% to 50% of Kelly

                // Evolve feature flags
                EnableDrawdownProtection = rng.NextDouble() > 0.3; // 70% chance
                EnableCorrelationAdjustment = rng.NextDouble() > 0.4; // 60% chance
                MaxConcurrentPositions = rng.Next(2, 8); // 2 to 7 positions

                // Initialize the position sizer with evolved configuration
                var config = new PositionSizingConfig
                {
                    Method = PositionSizingMethod,
                    MaxPositionSize = MaxPositionSize,
                    MinPositionSize = 0.005, // 0.5% minimum
                    BaseRiskPerTrade = BaseRiskPerTrade,
                    RiskMode = RiskAdjustmentMode,
                    KellyMultiplier = KellyMultiplier,
                    VolatilityTarget = VolatilityTarget,
                    EnableHeatAdjustment = EnableDrawdownProtection,
                    EnableConcurrentPositionLimit = EnableCorrelationAdjustment,
                    MaxConcurrentPositions = MaxConcurrentPositions
                };

                _positionSizer = new DynamicPositionSizer(config);
            }

            // Use traditional fixed percentage approach
            _positionSizer = null;
        }

        /// <summary>
        /// Generate scale-out fractions that result in whole contract numbers.
        /// The fractions must sum to 1.0 and when multiplied by totalContracts, yield whole numbers.
        /// OPTIMIZED: Improved algorithm efficiency, reduced divisions, better edge case handling
        /// </summary>
        private static double[] GenerateValidScaleOutFractionsOptimized(Random rng, double totalContracts)
        {
            var fractions = new double[8];

            // Handle edge case where totalContracts is zero
            if (totalContracts <= 0)
            {
                // All fractions should be zero when there are no contracts
                return fractions; // Already initialized to zeros
            }

            var contractsToScaleOut = new int[8];
            var remainingContracts = (int)totalContracts;

            // OPTIMIZATION: Handle small contract counts more efficiently
            if (remainingContracts <= 8)
            {
                // For small numbers of contracts, distribute 1 per step until exhausted
                for (var i = 0; i < Math.Min(8, remainingContracts); i++)
                {
                    contractsToScaleOut[i] = 1;
                }
            }
            else
            {
                // OPTIMIZATION: Improved distribution algorithm
                // Ensure each step gets at least 1 contract if possible, then distribute remainder
                var guaranteedPerStep = remainingContracts / 8;
                var remainder = remainingContracts % 8;
                
                // Give each step the guaranteed amount
                for (var i = 0; i < 8; i++)
                {
                    contractsToScaleOut[i] = guaranteedPerStep;
                }
                
                // Distribute remainder randomly across steps
                for (var i = 0; i < remainder; i++)
                {
                    var randomStep = rng.Next(8);
                    contractsToScaleOut[randomStep]++;
                }
            }

            // OPTIMIZATION: Single pass to convert contract counts to fractions
            // Avoid repeated division by caching the reciprocal
            var reciprocal = 1.0 / totalContracts;
            for (var i = 0; i < 8; i++)
            {
                fractions[i] = contractsToScaleOut[i] * reciprocal;
            }

            return fractions;
        }

        // Back-compat constructor: single trade percentage min/max applied to both stocks and options
        public GeneticIndividual(Random rng,
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
            : this(rng, startingBalance,
                indicatorTypeMin, indicatorTypeMax,
                indicatorPeriodMin, indicatorPeriodMax,
                indicatorModeMin, indicatorModeMax,
                indicatorTimeFrameMin, indicatorTimeFrameMax,
                indicatorPolarityMin, indicatorPolarityMax,
                indicatorThresholdMin, indicatorThresholdMax,
                maxIndicators,
                // Use same bounds for both stocks and options for back-compat overload
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
    }
}