using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using Trade.Indicators;
using Trade.Interfaces;
using Trade.Prices2;

namespace Trade
{
    // Parameters for a single indicator
    public class IndicatorParams
    {
        public int Type { get; set; }
        public int Period { get; set; }
        public int Mode { get; set; }
        public TimeFrame TimeFrame { get; set; }
        public OHLC OHLC { get; set; }
        public int Polarity { get; set; }
        public int FastMAPeriod { get; set; }
        public int SlowMAPeriod { get; set; }
        public double LongThreshold { get; set; }
        public double ShortThreshold { get; set; }
        public double Param1 { get; set; }
        public double Param2 { get; set; }
        public double Param3 { get; set; }
        public double Param4 { get; set; }
        public double Param5 { get; set; }
        public bool DebugCase { get; set; }
        // NEW: Track indicator trade interpretation mode (Delta vs Range)
        public IndicatorTradeMode TradeMode { get; set; } = IndicatorTradeMode.Delta;
        // NEW: per-indicator buffer source preference
        public PriceBufferSource BufferSource { get; set; } = PriceBufferSource.UseStockPriceBuffer;
    }

    // NEW: Trade mode for indicator signal interpretation
    public enum IndicatorTradeMode
    {
        Delta = 0, // Use slope/delta-based triggers (default)
        Range = 1  // Use range/threshold-based triggers
    }

    // NEW: price buffer selection for indicators/individuals
    public enum PriceBufferSource
    {
        UseStockPriceBuffer = 0,
        UseOptionPriceBuffer = 1
    }

    // Combination methods for multiple indicators
    public enum CombinationMethod
    {
        Sum = 0, // Simple sum of all indicators
        NormalizedSum = 1, // Sum of normalized indicators
        EnsembleVoting = 2 // Voting based on thresholds
    }

    /// <summary>
    /// Signal combination methods for multi-indicator trading
    /// </summary>
    public enum SignalCombinationMethod
    {
        Isolation,  // Indicators trade independently (fixes sequential issue)
        Sum,        // Sum all deltas (+2 example)
        Majority,   // Sign of sum only (-1, 0, +1)
        Consensus,  // All indicators must agree
        Weighted    // Weighted sum using IndicatorWeights
    }

    // Represents a buy/sell trade
    public enum AllowedTradeType
    {
        None = 0,
        Buy,
        SellShort,
        Any
    }
    
    public enum AllowedOptionType
    {
        None = 0,
        Calls,
        Puts,
        Any
    }

    public enum AllowedSecurityType
    {
        None = 0,
        Stock,
        Option,
        Any
    }

    [Flags]
    public enum AllowedActionType
    {
        None = 0,
        OpenBuy = 1 << 0,           // 1
        OpenSell = 1 << 1,          // 2
        OpenSellShort = 1 << 2,     // 4
        OpenBuyToCover = 1 << 3,    // 8
        OpenAny = 1 << 4,           // 16

        // --- Close Actions ---
        CloseBuy = 1 << 5,          // 32
        CloseSell = 1 << 6,         // 64
        CloseSellShort = 1 << 7,    // 128
        CloseBuyToCover = 1 << 8,   // 256
        CloseAny = 1 << 9,          // 512

        // --- ScaleOut Actions ---
        ScaleOutBuy = 1 << 10,          // 1024
        ScaleOutSell = 1 << 11,         // 2048
        ScaleOutShort = 1 << 12,        // 4096
        ScaleOutBuyToCover = 1 << 13,   // 8192
        ScaleOutAny = 1 << 14           // 16384
    }

    // Represents an individual in the genetic algorithm population
    public partial class GeneticIndividual
    {
        // Core properties and fields
        public List<IndicatorParams> Indicators { get; set; } = new List<IndicatorParams>(5);
        public List<TradeResult> Trades { get; set; } = new List<TradeResult>();
        public List<string> TradeActions { get; private set; } = new List<string>();
        public List<double> SignalValues { get; } = new List<double>();
        public List<List<double>> IndicatorValues { get; private set; } = new List<List<double>>();
        public List<double> Chromosome { get; set; } = new List<double>();
        public Fitness Fitness { get; set; } = new Fitness();
        public Random RandomNumberGenerator { get; set; }

        // Genetic parameters for multiple indicator support
        public bool AllowMultipleTrades { get; set; }
        public CombinationMethod CombinationMethod { get; set; } = CombinationMethod.Sum;
        public int EnsembleVotingThreshold { get; set; } = 1; // For ensemble voting
        public AllowedTradeType AllowedTradeTypes { get; set; } = AllowedTradeType.None;
        public AllowedOptionType AllowedOptionTypes { get; set; } = AllowedOptionType.None;
        public AllowedSecurityType AllowedSecurityTypes { get; set; }

        // NEW: Signal aggregation configuration
        public SignalCombinationMethod SignalCombination { get; set; } = SignalCombinationMethod.Sum;
        public double[] IndicatorWeights { get; set; } // For weighted combination

        // Stock thresholds - CLEAR
        public double LongEntryThreshold { get; set; } = 0.5;
        public double ShortEntryThreshold { get; set; } = -0.5;
        public double LongExitThreshold { get; set; } = -0.1;
        public double ShortExitThreshold { get; set; } = 0.1;

        // Option thresholds - CLEAN (no misleading comments)
        public double LongCallEntryThreshold { get; set; } = 0.5;
        public double ShortCallEntryThreshold { get; set; } = -0.5;
        public double LongCallExitThreshold { get; set; } = -0.1;
        public double ShortCallExitThreshold { get; set; } = 0.1;
        public double LongPutEntryThreshold { get; set; } = -0.5;
        public double ShortPutEntryThreshold { get; set; } = 0.5;
        public double LongPutExitThreshold { get; set; } = 0.1;
        public double ShortPutExitThreshold { get; set; } = -0.1;
        public double OptionExitThreshold { get; set; } = 0;



        // Trading configuration
        public double StartingBalance { get; set; }
        public double FinalBalance { get; set; }
        public double TradePercentageForStocks { get; set; } = 0.03; // Default to 3% of balance per trade
        // NEW: Option trade percentage
        public double TradePercentageForOptions { get; set; } = 0.03; // Default to 3% for options as well

        // Option trading parameters
        public double NumberOfOptionContractsToOpen { get; set; } = 8;
        public double[] OptionContractsToScaleOut { get; set; } = { .125, .125, .125, .125, .125, .125, .125, .125 };
        public int OptionDaysOut { get; set; } = 30;
        public int OptionStrikeDistance { get; set; } = 5;

        public OHLC OHLC { get; set; }
        // NEW: default price buffer preference for this individual
        public PriceBufferSource BufferSource { get; set; } = PriceBufferSource.UseStockPriceBuffer;
        // Moving average parameters
        public int FastMAPeriod { get; set; } = 3;
        public int SlowMAPeriod { get; set; } = 10;

        // Dynamic Position Sizing Configuration
        public bool UseDynamicPositionSizing { get; set; } = true;
        public PositionSizingMethod PositionSizingMethod { get; set; } = PositionSizingMethod.VolatilityAdjusted;
        public RiskAdjustmentMode RiskAdjustmentMode { get; set; } = RiskAdjustmentMode.Balanced;
        public double MaxPositionSize { get; set; } = 0.20; // Maximum 20% of account per position
        public double BaseRiskPerTrade { get; set; } = 0.025; // Base 2.5% risk per trade
        public double VolatilityTarget { get; set; } = 0.15; // Target 15% volatility
        public double KellyMultiplier { get; set; } = 0.25; // Quarter Kelly for safety
        public bool EnableDrawdownProtection { get; set; } = true;
        public bool EnableCorrelationAdjustment { get; set; } = true;
        public int MaxConcurrentPositions { get; set; } = 5;

        // Static dependencies
        public static IImpliedVolatilitySolver ImpliedVolatilitySolverCalls { get; set; }
        public static IImpliedVolatilitySolver ImpliedVolatilitySolverPuts { get; set; }
        public static Prices Prices { get; set; }
        public static OptionPrices OptionsPrices { get; set; }

        // Dynamic position sizing
        private DynamicPositionSizer _positionSizer;

        // Performance tracking
        private DateTime _lastRebalanceDate = DateTime.MinValue;
        private double _peakBalance;
        private readonly List<double> _performanceHistory = new List<double>();

        // NEW: current window volume buffers (if available)
        private long[] _tickVolumeBuffer;
        private long[] _realVolumeBuffer;

        // Static dictionary to hold min/max for each indicator type
        internal static readonly Dictionary<int, (double min, double max)> IndicatorRanges =
            new Dictionary<int, (double min, double max)>();

        // Default constructor
        public GeneticIndividual()
        {
            RandomNumberGenerator = new Random(42);
        }
    }
}