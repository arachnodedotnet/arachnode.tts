using Trade.Prices2;

namespace Trade
{
    internal partial class Program
    {
        #region Configuration Constants - Overfitting Prevention

        public const bool SIMPLE_MODE = true;
        public const bool ALL_SPLITS_ARE_CLONES = false;

        public const double TrainingDataRatio = 0.60; // 60% for training
        public const double ValidationDataRatio = 0.20; // 20% for validation  
        public const double TestDataRatio = 0.20; // 20% for final test
        public const bool EnableWindowSizeOptimization = true; // Toggle to run window size optimizer
        public const int EarlyStoppingPatience = 5; // Patience for early stopping (per tests)
        public const double ValidationPercentage = 0.15d; // 15% validation per tests
        public const double RegularizationStrength = 0.01d; // Regularization strength per tests
        public const int MaxComplexity = 1; // Severely limit complexity
        public const int PopulationSize = 100; // Reduced population
        public const int Generations = 15; // Reduced generations
        public const double MutationRate = 0.05; // Reduced mutation rate
        public const int TournamentSize = 2; // Smaller tournament
        public const double StartingBalance = 100_000.0;
        public static int IndicatorTypeMin = 17;
        public static int IndicatorTypeMax = 48; // Limit to basic indicators //17 29 32 48
        public const int IndicatorPeriodMin = 1; // Increased minimum
        public const int IndicatorPeriodMax = 200; // Decreased maximum
        public const int IndicatorModeMin = 1;
        public const int IndicatorModeMax = 6; // Limit modes
        public const TimeFrame IndicatorTimeFrameMin = TimeFrame.M1;
        public const TimeFrame IndicatorTimeFrameMax = TimeFrame.D1;
        public const int IndicatorPolarityMin = -1;
        public const int IndicatorPolarityMax = 1;
        public const double IndicatorThresholdMin = -1.0; // More conservative thresholds
        public const double IndicatorThresholdMax = 1.0;
        public const int MaxIndicators = MaxComplexity; // Force single indicator
        public const double TradePercentageForStocksMin = 0.03d; // Higher minimum
        public const double TradePercentageForStocksMax = 0.99d; // Much lower maximum
        public const double TradePercentageForOptionsMin = 0.0005d; // Higher minimum (options)
        public const double TradePercentageForOptionsMax = 0.03d; // Much lower maximum (options)
        public const int OptionDaysOutMin = 2;
        public const int OptionDaysOutMax = 14; // Shorter expirations
        public const int OptionStrikeDistanceMin = 1;
        public const int OptionStrikeDistanceMax = 30; // Closer strikes
        public const int FastMAPeriodMin = 1;
        public const int FastMAPeriodMax = 200;
        public const int SlowMAPeriodMin = 1;
        public const int SlowMAPeriodMax = 200;
        public static int AllowedTradeTypeMin = 1; // Buy = 1
        public static int AllowedTradeTypeMax = 2; // Sell = 2
        public static int AllowedOptionTypeMin = 1; // Call = 1
        public static int AllowedOptionTypeMax = 2; // Put = 2
        public static int AllowedSecurityTypeMin = 1; // Stock = 1
        public static int AllowedSecurityTypeMax = 1; // Options = 2
        public const int NumberOfOptionContractsMin = 1;
        public const int NumberOfOptionContractsMax = 8;
        public const bool EnableIntraTrainingCV = false; // Gate for inner CV during GA evolution
        public const int IntraCVFolds = 4; // Number of time-ordered folds
        public const int IntraCVPurgeBars = 5; // Purge bars between train and val to prevent leakage
        public const int IntraCVEmbargoBars = 0; // Optional embargo after validation
        public const bool UseGAInOptimizer = true; // Use GA instead of placeholder inside optimizer

        #endregion

        #region Walkforward Analysis Configuration - More Conservative

        public const int WalkforwardWindowSize = 126; // 6 months of trading days
        public const int WalkforwardStepSize = 21; // 1 month step forward
        public const int MinimumTrainingPeriods = 63; // Minimum 3 months
        public const double WalkforwardValidationPercentage = 0.25; // 25% for validation
        public const bool EnableWalkforwardEarlyStopping = true;
        public const int WalkforwardMaxWindows = 10; // Limit windows

        #endregion

        #region Enhanced Genetic Algorithm Configuration - Disabled Advanced Features

        public const bool EnableHistoricalTracking = false;
        public const int HistoricalArchiveSize = 50; // Reduced
        public const double DiversityInjectionRate = 0.0; // Disabled
        public const int DiversityInjectionFrequency = 100; // Effectively disabled
        public const bool EnableIslandEvolution = false;
        public const int NumberOfIslands = 1; // Single population
        public const int MigrationFrequency = 100; // Effectively disabled
        public const double MigrationRate = 0.0; // Disabled
        public const bool EnableSchemaPreservation = false;
        public const double SchemaProtectionBonus = 0.0; // Disabled

        #endregion

        #region Verification and Debugging Constants

        // VERIFICATION AND DEBUGGING PARAMETERS
        public const bool EnableTradeVerification = true;     // Enable trade event verification
        public const bool VerboseVerification = false;        // Show detailed verification logs

        #endregion
    }
}