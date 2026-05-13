using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Trade.Prices2;

namespace Trade
{
    /// <summary>
    ///     Position sizing methods available for dynamic position sizing
    /// </summary>
    public enum PositionSizingMethod
    {
        FixedPercentage = 0, // Fixed percentage of account balance
        KellyOptimal = 1, // Kelly Criterion for optimal position sizing
        VolatilityAdjusted = 2, // Adjust size based on market volatility
        RiskParity = 3, // Risk-adjusted position sizing
        ATRBased = 4, // Based on Average True Range
        DrawdownProtective = 5, // Reduce size during drawdowns
        MomentumAdaptive = 6, // Increase size during strong trends
        CorrelationAware = 7, // Adjust for position correlation
        MarketRegimeAdaptive = 8 // Adapt to bull/bear market conditions
    }

    /// <summary>
    ///     Risk adjustment modes for position sizing
    /// </summary>
    public enum RiskAdjustmentMode
    {
        Conservative = 0, // Lower risk, smaller positions
        Balanced = 1, // Moderate risk adjustment
        Aggressive = 2 // Higher risk, larger positions
    }

    /// <summary>
    ///     Market regime detection for adaptive position sizing
    /// </summary>
    public enum MarketRegime
    {
        Unknown = 0,
        Trending = 1, // Strong directional movement
        Sideways = 2, // Range-bound market
        HighVolatility = 3, // Volatile conditions
        LowVolatility = 4 // Calm market conditions
    }

    /// <summary>
    ///     Comprehensive position sizing configuration
    /// </summary>
    public class PositionSizingConfig
    {
        // Primary method selection
        public PositionSizingMethod Method { get; set; } = PositionSizingMethod.FixedPercentage;

        // Risk management parameters
        public double MaxPositionSize { get; set; } = 0.25; // Maximum 25% of account
        public double MinPositionSize { get; set; } = 0.005; // Minimum 0.5% of account
        public double BaseRiskPerTrade { get; set; } = 0.02; // Base risk 2% per trade
        public RiskAdjustmentMode RiskMode { get; set; } = RiskAdjustmentMode.Balanced;

        // Kelly Criterion parameters
        public double KellyMultiplier { get; set; } = 0.25; // Use 25% of full Kelly (quarter Kelly)
        public int LookbackPeriod { get; set; } = 50; // Periods for Kelly calculation

        // Volatility adjustment parameters
        public int VolatilityLookback { get; set; } = 20; // Periods for volatility calculation
        public double VolatilityTarget { get; set; } = 0.15; // Target annualized volatility (15%)
        public double VolatilityScaling { get; set; } = 1.0; // Volatility scaling factor

        // ATR-based parameters
        public int ATRPeriod { get; set; } = 14; // ATR calculation period
        public double ATRMultiplier { get; set; } = 2.0; // ATR stop loss multiplier

        // Drawdown protection parameters
        public double DrawdownThreshold { get; set; } = 0.10; // 10% drawdown triggers reduction
        public double DrawdownReduction { get; set; } = 0.5; // Reduce position by 50%
        public double RecoveryThreshold { get; set; } = 0.95; // 95% recovery to restore size

        // Momentum parameters
        public int MomentumPeriod { get; set; } = 10; // Momentum calculation period
        public double MomentumThreshold { get; set; } = 0.05; // 5% threshold for momentum scaling
        public double MomentumMultiplier { get; set; } = 1.5; // Scale factor for strong momentum

        // Correlation parameters
        public double MaxCorrelation { get; set; } = 0.7; // Maximum allowed correlation
        public double CorrelationReduction { get; set; } = 0.5; // Reduction for correlated positions

        // Advanced features
        public bool EnableHeatAdjustment { get; set; } = true; // Adjust for recent performance
        public bool EnableLiquidityAdjustment { get; set; } = false; // Adjust for market liquidity
        public bool EnableConcurrentPositionLimit { get; set; } = true; // Limit concurrent positions
        public int MaxConcurrentPositions { get; set; } = 5; // Maximum concurrent positions
    }

    /// <summary>
    ///     Dynamic position sizing context for decision making
    /// </summary>
    public class PositionSizingContext
    {
        // Market data
        public PriceRecord[] PriceHistory { get; set; }
        public double CurrentPrice { get; set; }
        public double AverageVolume { get; set; }

        // Account information
        public double AccountBalance { get; set; }
        public double AvailableBalance { get; set; }
        public double UnrealizedPnL { get; set; }
        public double MaxDrawdownFromPeak { get; set; }

        // Position information
        public List<TradeResult> OpenPositions { get; set; } = new List<TradeResult>();
        public List<TradeResult> RecentTrades { get; set; } = new List<TradeResult>();
        public double TotalExposure { get; set; }

        // Strategy performance
        public double WinRate { get; set; }
        public double AverageWin { get; set; }
        public double AverageLoss { get; set; }
        public double ProfitFactor { get; set; }
        public double SharpeRatio { get; set; }

        // Market conditions
        public MarketRegime CurrentRegime { get; set; } = MarketRegime.Unknown;
        public double MarketVolatility { get; set; }
        public double MarketMomentum { get; set; }
        public double ATR { get; set; }

        // Risk metrics
        public double VaR95 { get; set; } // 95% Value at Risk
        public double MaxCorrelationWithExisting { get; set; }
        public bool IsHeatPeriod { get; set; } // Recent poor performance
    }

    /// <summary>
    ///     Position sizing decision result
    /// </summary>
    public class PositionSizingResult
    {
        public double PositionSize { get; set; } // Final position size (% of account)
        public double PositionAmount { get; set; } // Dollar amount
        public double ShareQuantity { get; set; } // Number of shares/contracts
        public double StopLoss { get; set; } // Suggested stop loss price
        public double RiskAmount { get; set; } // Dollar amount at risk
        public double ExpectedReturn { get; set; } // Expected return for position
        public double RiskRewardRatio { get; set; } // Risk/reward ratio

        // Decision factors
        public string PrimarySizingFactor { get; set; } // Main factor driving size
        public List<string> AdjustmentFactors { get; set; } = new List<string>(); // Other adjustments applied
        public double ConfidenceLevel { get; set; } // Confidence in sizing decision
        public string RiskAssessment { get; set; } // Risk level assessment

        // Limits and constraints
        public bool HitMaxPositionLimit { get; set; }
        public bool HitMinPositionLimit { get; set; }
        public bool BlockedByCorrelation { get; set; }
        public bool ReducedByDrawdown { get; set; }

        public override string ToString()
        {
            return $"Size: {PositionSize:P2} (${PositionAmount:F0}) | Risk: ${RiskAmount:F0} | {RiskAssessment}";
        }
    }

    /// <summary>
    ///     Advanced dynamic position sizing engine with performance optimizations
    /// </summary>
    public class DynamicPositionSizer
    {
        private readonly PositionSizingConfig _config;
        private readonly Dictionary<string, double> _correlationMatrix = new Dictionary<string, double>();
        private readonly Random _random;

        // OPTIMIZATION: Caching for expensive calculations
        private readonly List<double> _recentReturns = new List<double>();
        private double _currentDrawdown;
        private double _peakBalance;
        
        // OPTIMIZATION: Cached calculations with validity tracking
        private double _cachedVolatility = -1;
        private int _volatilityCalculationHash = 0;
        private MarketRegime _cachedRegime = MarketRegime.Unknown;
        private int _regimeCalculationHash = 0;
        
        // OPTIMIZATION: Pre-allocated arrays to reduce GC pressure
        private static readonly double[] _tempReturns = new double[1000];
        private static readonly double[] _tempPrices = new double[1000];

        // OPTIMIZATION: Static readonly for commonly used calculations
        private static readonly double Sqrt252 = Math.Sqrt(252);
        private static readonly double InvSqrt252 = 1.0 / Math.Sqrt(252);

        public DynamicPositionSizer(PositionSizingConfig config = null)
        {
            _config = config ?? new PositionSizingConfig();
            _random = new Random(42); // Deterministic for testing
        }

        /// <summary>
        ///     Calculate optimal position size based on current context
        /// </summary>
        public PositionSizingResult CalculatePositionSize(PositionSizingContext context, double targetPrice,
            AllowedTradeType allowedTradeType)
        {
            // OPTIMIZATION: Pre-allocate result object and reuse adjustment factors list
            var result = new PositionSizingResult();

            // OPTIMIZATION: Early exit for invalid inputs
            if (context?.AvailableBalance <= 0 || targetPrice <= 0)
            {
                result.PositionSize = 0;
                result.RiskAssessment = "INVALID INPUT";
                return result;
            }

            // Update internal state efficiently
            UpdateDrawdownStateOptimized(context);
            UpdateMarketRegimeOptimized(context);

            // Calculate base position size using primary method
            var baseSize = CalculateBasePositionSizeOptimized(context, result);
            result.PositionSize = baseSize;
            result.PrimarySizingFactor = _config.Method.ToString();

            // Apply adjustments in order of computational cost (cheapest first)
            ApplyRiskAdjustmentsOptimized(context, result);
            ApplyMarketConditionAdjustmentsOptimized(context, result);
            ApplyPortfolioAdjustmentsOptimized(context, result);

            // Calculate final position details
            CalculateFinalPositionDetailsOptimized(context, targetPrice, allowedTradeType, result);

            // Validate and constrain position
            ValidateAndConstrainPositionOptimized(context, result);

            return result;
        }

        /// <summary>
        ///     Get suggested position sizing method based on current conditions
        /// </summary>
        public PositionSizingMethod GetSuggestedMethod(PositionSizingContext context)
        {
            // OPTIMIZATION: Quick checks in order of likelihood
            if (_currentDrawdown > 0.15)
                return PositionSizingMethod.DrawdownProtective;

            if (context.MarketVolatility > 0.25)
                return PositionSizingMethod.VolatilityAdjusted;

            if (Math.Abs(context.MarketMomentum) > 0.1)
                return PositionSizingMethod.MomentumAdaptive;

            if (context.RecentTrades?.Count >= 30 && context.WinRate > 0.3)
                return PositionSizingMethod.KellyOptimal;

            return PositionSizingMethod.FixedPercentage;
        }

        /// <summary>
        ///     Generate comprehensive position sizing report with StringBuilder optimization
        /// </summary>
        public string GenerateSizingReport(PositionSizingContext context, PositionSizingResult result)
        {
            // OPTIMIZATION: Pre-allocate StringBuilder with estimated capacity
            var report = new StringBuilder(1024);

            report.AppendLine("=== DYNAMIC POSITION SIZING REPORT ===");
            report.Append("Method: ").AppendLine(_config.Method.ToString());
            report.AppendFormat("Final Position Size: {0:P2} (${1:F0})\n", result.PositionSize, result.PositionAmount);
            report.AppendFormat("Share Quantity: {0:F2}\n", result.ShareQuantity);
            report.Append("Risk Assessment: ").AppendLine(result.RiskAssessment);
            report.AppendFormat("Confidence Level: {0:P0}\n", result.ConfidenceLevel);
            report.AppendLine();

            report.AppendLine("Adjustments Applied:");
            if (result.AdjustmentFactors.Count == 0)
            {
                report.AppendLine("  • No adjustments applied");
            }
            else
            {
                foreach (var adjustment in result.AdjustmentFactors)
                {
                    report.Append("  • ").AppendLine(adjustment);
                }
            }

            report.AppendLine();
            report.AppendLine("Market Conditions:");
            report.Append("  • Regime: ").AppendLine(context.CurrentRegime.ToString());
            report.AppendFormat("  • Volatility: {0:P1}\n", context.MarketVolatility);
            report.AppendFormat("  • Momentum: {0:P1}\n", context.MarketMomentum);
            report.AppendFormat("  • Current Drawdown: {0:P1}\n", _currentDrawdown);

            return report.ToString();
        }

        /// <summary>
        ///     Calculate base position size using the primary method with optimizations
        /// </summary>
        private double CalculateBasePositionSizeOptimized(PositionSizingContext context, PositionSizingResult result)
        {
            switch (_config.Method)
            {
                case PositionSizingMethod.FixedPercentage:
                    return _config.BaseRiskPerTrade;

                case PositionSizingMethod.KellyOptimal:
                    return CalculateKellyOptimalSizeOptimized(context, result);

                case PositionSizingMethod.VolatilityAdjusted:
                    return CalculateVolatilityAdjustedSizeOptimized(context, result);

                case PositionSizingMethod.RiskParity:
                    return CalculateRiskParitySizeOptimized(context, result);

                case PositionSizingMethod.ATRBased:
                    return CalculateATRBasedSizeOptimized(context, result);

                case PositionSizingMethod.DrawdownProtective:
                    return CalculateDrawdownProtectiveSizeOptimized(context, result);

                case PositionSizingMethod.MomentumAdaptive:
                    return CalculateMomentumAdaptiveSizeOptimized(context, result);

                case PositionSizingMethod.MarketRegimeAdaptive:
                    return CalculateMarketRegimeAdaptiveSizeOptimized(context, result);

                default:
                    return _config.BaseRiskPerTrade;
            }
        }

        #region Optimized Position Sizing Methods

        private double CalculateKellyOptimalSizeOptimized(PositionSizingContext context, PositionSizingResult result)
        {
            var recentTrades = context.RecentTrades;
            if (recentTrades == null || recentTrades.Count < 10)
            {
                result.AdjustmentFactors.Add("Insufficient trade history for Kelly");
                return _config.BaseRiskPerTrade;
            }

            // OPTIMIZATION: Use array bounds checking to avoid LINQ Skip overhead
            var startIndex = Math.Max(0, recentTrades.Count - _config.LookbackPeriod);
            var tradesCount = recentTrades.Count - startIndex;
            
            var winCount = 0;
            var totalWinAmount = 0.0;
            var totalLossAmount = 0.0;
            var lossCount = 0;

            // OPTIMIZATION: Single pass through trades instead of multiple LINQ operations
            for (var i = startIndex; i < recentTrades.Count; i++)
            {
                var trade = recentTrades[i];
                var dollarGain = GetTradeDollarGain(trade);
                
                if (dollarGain > 0)
                {
                    winCount++;
                    totalWinAmount += Math.Abs(dollarGain);
                }
                else if (dollarGain < 0)
                {
                    lossCount++;
                    totalLossAmount += Math.Abs(dollarGain);
                }
            }

            if (lossCount == 0)
            {
                result.AdjustmentFactors.Add("No losses in history - using conservative sizing");
                return Math.Min(_config.BaseRiskPerTrade * 2, _config.MaxPositionSize);
            }

            var winProbability = (double)winCount / tradesCount;
            var averageWinAmount = winCount > 0 ? totalWinAmount / winCount : 0;
            var averageLossAmount = totalLossAmount / lossCount;

            if (averageLossAmount == 0) return _config.BaseRiskPerTrade;

            var winLossRatio = averageWinAmount / averageLossAmount;
            var kellyFraction = (winProbability * winLossRatio - (1 - winProbability)) / winLossRatio;

            // Apply Kelly multiplier for safety
            var adjustedKelly = Math.Max(0, kellyFraction * _config.KellyMultiplier);

            result.AdjustmentFactors.Add(
                $"Kelly: WinRate={winProbability:P1}, W/L={winLossRatio:F2}, Raw={kellyFraction:P2}");
            return Math.Min(adjustedKelly, _config.MaxPositionSize);
        }

        private double CalculateVolatilityAdjustedSizeOptimized(PositionSizingContext context, PositionSizingResult result)
        {
            var priceHistory = context.PriceHistory;
            var lookbackPeriods = (int)_config.VolatilityLookback;
            
            if (priceHistory == null || priceHistory.Length < lookbackPeriods)
            {
                result.AdjustmentFactors.Add("Insufficient price history for volatility");
                return _config.BaseRiskPerTrade;
            }

            // OPTIMIZATION: Use cached volatility if price history hasn't changed
            var historyHash = GetPriceHistoryHash(priceHistory, lookbackPeriods);
            double volatility;
            
            if (_volatilityCalculationHash == historyHash && _cachedVolatility >= 0)
            {
                volatility = _cachedVolatility;
            }
            else
            {
                volatility = CalculateVolatilityOptimized(priceHistory, lookbackPeriods);
                _cachedVolatility = volatility;
                _volatilityCalculationHash = historyHash;
            }

            // Adjust position size inversely to volatility
            var volatilityAdjustment = _config.VolatilityTarget / Math.Max(volatility, 0.01);
            volatilityAdjustment = Math.Min(volatilityAdjustment, 3.0); // Cap at 3x

            var adjustedSize = _config.BaseRiskPerTrade * volatilityAdjustment * _config.VolatilityScaling;

            result.AdjustmentFactors.Add(
                $"Volatility: {volatility:P1} vs target {_config.VolatilityTarget:P1}, adj={volatilityAdjustment:F2}x");
            return adjustedSize;
        }

        private double CalculateRiskParitySizeOptimized(PositionSizingContext context, PositionSizingResult result)
        {
            // OPTIMIZATION: Pre-computed constants
            const double baseVolatility = 0.20;
            var currentVolatility = Math.Max(context.MarketVolatility, 0.01);

            var riskParitySize = _config.BaseRiskPerTrade * (baseVolatility / currentVolatility);

            result.AdjustmentFactors.Add($"Risk Parity: {currentVolatility:P1} volatility");
            return riskParitySize;
        }

        private double CalculateATRBasedSizeOptimized(PositionSizingContext context, PositionSizingResult result)
        {
            if (context.ATR <= 0)
            {
                result.AdjustmentFactors.Add("No ATR data available");
                return _config.BaseRiskPerTrade;
            }

            // OPTIMIZATION: Pre-calculate commonly used values
            var dollarRisk = context.AccountBalance * _config.BaseRiskPerTrade;
            var atrStop = context.ATR * _config.ATRMultiplier;
            var positionSize = dollarRisk / (atrStop * context.CurrentPrice);

            // Convert to percentage of account
            var sizePercentage = positionSize * context.CurrentPrice / context.AccountBalance;

            result.AdjustmentFactors.Add($"ATR: {context.ATR:F2}, Stop: {atrStop:F2}");
            result.StopLoss = context.CurrentPrice - atrStop; // For long positions

            return sizePercentage;
        }

        private double CalculateDrawdownProtectiveSizeOptimized(PositionSizingContext context, PositionSizingResult result)
        {
            var baseSize = _config.BaseRiskPerTrade;

            if (_currentDrawdown > _config.DrawdownThreshold)
            {
                var reductionFactor = 1.0 - _currentDrawdown / _config.DrawdownThreshold * _config.DrawdownReduction;
                reductionFactor = Math.Max(reductionFactor, 0.1); // Don't reduce below 10%

                baseSize *= reductionFactor;
                result.AdjustmentFactors.Add(
                    $"Drawdown protection: {_currentDrawdown:P1} drawdown, {reductionFactor:P1} of normal size");
                result.ReducedByDrawdown = true;
            }

            return baseSize;
        }

        private double CalculateMomentumAdaptiveSizeOptimized(PositionSizingContext context, PositionSizingResult result)
        {
            var baseSize = _config.BaseRiskPerTrade;
            var absMomentum = Math.Abs(context.MarketMomentum);

            if (absMomentum > _config.MomentumThreshold)
            {
                var momentumMultiplier = 1.0 + absMomentum / _config.MomentumThreshold *
                    (_config.MomentumMultiplier - 1.0);
                momentumMultiplier = Math.Min(momentumMultiplier, _config.MomentumMultiplier);

                baseSize *= momentumMultiplier;
                result.AdjustmentFactors.Add(
                    $"Momentum: {context.MarketMomentum:P1}, {momentumMultiplier:F2}x multiplier");
            }

            return baseSize;
        }

        private double CalculateMarketRegimeAdaptiveSizeOptimized(PositionSizingContext context, PositionSizingResult result)
        {
            var baseSize = _config.BaseRiskPerTrade;

            switch (context.CurrentRegime)
            {
                case MarketRegime.Trending:
                    baseSize *= 1.3; // Increase size in trending markets
                    result.AdjustmentFactors.Add("Trending market: +30% size");
                    break;

                case MarketRegime.HighVolatility:
                    baseSize *= 0.7; // Reduce size in volatile markets
                    result.AdjustmentFactors.Add("High volatility: -30% size");
                    break;

                case MarketRegime.Sideways:
                    baseSize *= 0.8; // Slightly reduce size in range-bound markets
                    result.AdjustmentFactors.Add("Sideways market: -20% size");
                    break;

                case MarketRegime.LowVolatility:
                    baseSize *= 1.1; // Slightly increase size in calm markets
                    result.AdjustmentFactors.Add("Low volatility: +10% size");
                    break;
            }

            return baseSize;
        }

        #endregion

        #region Optimized Adjustment Methods

        private void ApplyRiskAdjustmentsOptimized(PositionSizingContext context, PositionSizingResult result)
        {
            // OPTIMIZATION: Use switch expression for risk mode adjustment (C# 7.3 compatible)
            double riskMultiplier;
            string riskMessage;
            
            switch (_config.RiskMode)
            {
                case RiskAdjustmentMode.Conservative:
                    riskMultiplier = 0.7;
                    riskMessage = "Conservative risk mode: -30%";
                    break;
                case RiskAdjustmentMode.Aggressive:
                    riskMultiplier = 1.3;
                    riskMessage = "Aggressive risk mode: +30%";
                    break;
                default:
                    riskMultiplier = 1.0;
                    riskMessage = null;
                    break;
            }
            
            if (riskMultiplier != 1.0)
            {
                result.PositionSize *= riskMultiplier;
                result.AdjustmentFactors.Add(riskMessage);
            }

            // Heat adjustment (recent poor performance)
            if (_config.EnableHeatAdjustment && context.IsHeatPeriod)
            {
                result.PositionSize *= 0.6;
                result.AdjustmentFactors.Add("Heat period: -40% size");
            }
        }

        private void ApplyMarketConditionAdjustmentsOptimized(PositionSizingContext context, PositionSizingResult result)
        {
            // OPTIMIZATION: Single volatility check with range-based logic
            var volatility = context.MarketVolatility;
            if (volatility > 0.25) // Very high volatility
            {
                result.PositionSize *= 0.8;
                result.AdjustmentFactors.Add("High market volatility: -20%");
            }
            else if (volatility < 0.10) // Very low volatility
            {
                result.PositionSize *= 1.1;
                result.AdjustmentFactors.Add("Low market volatility: +10%");
            }
        }

        private void ApplyPortfolioAdjustmentsOptimized(PositionSizingContext context, PositionSizingResult result)
        {
            // Correlation adjustment
            if (context.MaxCorrelationWithExisting > _config.MaxCorrelation)
            {
                result.PositionSize *= _config.CorrelationReduction;
                result.AdjustmentFactors.Add(
                    $"High correlation ({context.MaxCorrelationWithExisting:P0}): -{1 - _config.CorrelationReduction:P0}");
                result.BlockedByCorrelation = true;
            }

            var openPositionsCount = context.OpenPositions?.Count ?? 0;
            
            // Concentration limits
            if (_config.EnableConcurrentPositionLimit && openPositionsCount >= _config.MaxConcurrentPositions)
            {
                result.PositionSize *= 0.5; // Reduce size if at position limit
                result.AdjustmentFactors.Add($"Position limit reached ({openPositionsCount}): -50%");
            }

            // Total exposure limit
            if (context.TotalExposure > 0.8) // 80% of account already exposed
            {
                var exposureReduction = Math.Max(0.2, 1.0 - context.TotalExposure);
                result.PositionSize *= exposureReduction;
                result.AdjustmentFactors.Add(
                    $"High exposure ({context.TotalExposure:P0}): -{1 - exposureReduction:P0}");
            }
        }

        private void CalculateFinalPositionDetailsOptimized(PositionSizingContext context, double targetPrice,
            AllowedTradeType allowedTradeType, PositionSizingResult result)
        {
            // Calculate dollar amount
            result.PositionAmount = context.AvailableBalance * result.PositionSize;

            // Calculate share quantity
            result.ShareQuantity = result.PositionAmount / targetPrice;

            // Calculate risk amount (if we have stop loss)
            if (result.StopLoss > 0)
            {
                var stopDistance = Math.Abs(targetPrice - result.StopLoss);
                result.RiskAmount = result.ShareQuantity * stopDistance;
            }
            else
            {
                // Default risk as percentage of position
                result.RiskAmount = result.PositionAmount * 0.1; // 10% default risk
            }

            // OPTIMIZATION: Pre-computed expected return
            result.ExpectedReturn = result.PositionAmount * 0.05; // 5% expected return

            // Risk/reward ratio
            result.RiskRewardRatio = result.RiskAmount > 0 ? result.ExpectedReturn / result.RiskAmount : 0;

            // Confidence level (optimized calculation)
            result.ConfidenceLevel = CalculateConfidenceLevelOptimized(context, result);
        }

        private void ValidateAndConstrainPositionOptimized(PositionSizingContext context, PositionSizingResult result)
        {
            // OPTIMIZATION: Combined constraint checking
            var originalSize = result.PositionSize;
            
            if (result.PositionSize > _config.MaxPositionSize)
            {
                result.PositionSize = _config.MaxPositionSize;
                result.HitMaxPositionLimit = true;
                result.AdjustmentFactors.Add($"Capped at max size: {_config.MaxPositionSize:P1}");
            }
            else if (result.PositionSize < _config.MinPositionSize)
            {
                result.PositionSize = _config.MinPositionSize;
                result.HitMinPositionLimit = true;
                result.AdjustmentFactors.Add($"Raised to min size: {_config.MinPositionSize:P1}");
            }

            // Only recalculate if size changed
            if (Math.Abs(result.PositionSize - originalSize) > 1e-10)
            {
                result.PositionAmount = context.AvailableBalance * result.PositionSize;
                result.ShareQuantity = result.PositionAmount / context.CurrentPrice;
            }

            // OPTIMIZATION: Efficient risk assessment using thresholds
            var maxSizeThreshold = _config.MaxPositionSize * 0.8;
            var elevatedRiskThreshold = _config.BaseRiskPerTrade * 1.5;
            var lowRiskThreshold = _config.BaseRiskPerTrade * 0.5;
            
            if (result.PositionSize >= maxSizeThreshold)
                result.RiskAssessment = "HIGH RISK";
            else if (result.PositionSize >= elevatedRiskThreshold)
                result.RiskAssessment = "ELEVATED RISK";
            else if (result.PositionSize <= lowRiskThreshold)
                result.RiskAssessment = "LOW RISK";
            else
                result.RiskAssessment = "NORMAL RISK";
        }

        #endregion

        #region Optimized Helper Methods

        private void UpdateDrawdownStateOptimized(PositionSizingContext context)
        {
            if (context.AccountBalance > _peakBalance) 
                _peakBalance = context.AccountBalance;

            _currentDrawdown = (_peakBalance - context.AccountBalance) / _peakBalance;
        }

        private void UpdateMarketRegimeOptimized(PositionSizingContext context)
        {
            var priceHistory = context.PriceHistory;
            if (priceHistory == null || priceHistory.Length < 20) return;

            // OPTIMIZATION: Use cached regime if price history hasn't changed significantly
            var regimeHash = GetPriceHistoryHash(priceHistory, 20);
            if (_regimeCalculationHash == regimeHash && _cachedRegime != MarketRegime.Unknown)
            {
                context.CurrentRegime = _cachedRegime;
                return;
            }

            // Simple regime detection based on volatility and momentum
            var shortTermVolatility = CalculateVolatilityOptimized(priceHistory, 10);
            var longTermVolatility = CalculateVolatilityOptimized(priceHistory, 20);

            MarketRegime regime;
            if (shortTermVolatility > longTermVolatility * 1.5)
                regime = MarketRegime.HighVolatility;
            else if (shortTermVolatility < longTermVolatility * 0.7)
                regime = MarketRegime.LowVolatility;
            else if (Math.Abs(context.MarketMomentum) > 0.1)
                regime = MarketRegime.Trending;
            else
                regime = MarketRegime.Sideways;

            context.CurrentRegime = regime;
            _cachedRegime = regime;
            _regimeCalculationHash = regimeHash;
        }

        private double CalculateVolatilityOptimized(PriceRecord[] prices, int periods)
        {
            if (prices == null || prices.Length < periods + 1) return 0.0;

            // OPTIMIZATION: Use pre-allocated array and avoid LINQ
            var startIndex = prices.Length - periods;
            var returnCount = 0;
            
            // Use static array if small enough, otherwise allocate
            double[] returns = periods <= _tempReturns.Length ? _tempReturns : new double[periods];

            for (var i = startIndex; i < prices.Length && returnCount < periods; i++)
            {
                if (i > 0 && prices[i - 1].Close > 0 && prices[i].Close > 0)
                {
                    returns[returnCount] = Math.Log(prices[i].Close / prices[i - 1].Close);
                    returnCount++;
                }
            }

            if (returnCount == 0) return 0.0;

            return CalculateStandardDeviationOptimized(returns, returnCount) * Sqrt252;
        }

        private double CalculateStandardDeviationOptimized(double[] values, int count)
        {
            if (count <= 1) return 0.0;

            // OPTIMIZATION: Single-pass calculation
            var sum = 0.0;
            var sumSquares = 0.0;
            
            for (var i = 0; i < count; i++)
            {
                var value = values[i];
                sum += value;
                sumSquares += value * value;
            }

            var mean = sum / count;
            var variance = (sumSquares - sum * mean) / count;
            return Math.Sqrt(Math.Max(0, variance)); // Ensure non-negative
        }

        private double CalculateConfidenceLevelOptimized(PositionSizingContext context, PositionSizingResult result)
        {
            var confidence = 0.5; // Base 50%

            // OPTIMIZATION: Use simple arithmetic instead of multiple if statements
            if (context.WinRate > 0.6) confidence += 0.2;
            if (context.ProfitFactor > 1.5) confidence += 0.2;
            if (context.SharpeRatio > 1.0) confidence += 0.1;

            if (_currentDrawdown > 0.1) confidence -= 0.2;
            if (context.CurrentRegime == MarketRegime.HighVolatility) confidence -= 0.1;
            if (result.BlockedByCorrelation) confidence -= 0.2;

            return Math.Max(0.1, Math.Min(0.9, confidence));
        }

        // OPTIMIZATION: Helper method to get trade dollar gain efficiently
        private double GetTradeDollarGain(TradeResult trade)
        {
            // Use ActualDollarGain directly instead of reflection
            return trade.ActualDollarGain;
        }

        // OPTIMIZATION: Simple hash function for price history change detection
        private int GetPriceHistoryHash(PriceRecord[] prices, int periods)
        {
            if (prices == null) return 0;
            
            var startIndex = Math.Max(0, prices.Length - periods);
            var hash = prices.Length;
            
            for (var i = startIndex; i < prices.Length && i < startIndex + Math.Min(5, periods); i++)
            {
                // Use only a few price points for hash to keep it fast
                hash = hash * 31 + prices[i].Close.GetHashCode();
            }
            
            return hash;
        }

        #endregion
    }
}