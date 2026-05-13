using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Trade.Examples;
using Trade.Prices2;
using Trade.Tests;

namespace Trade
{
    /// <summary>
    ///     Trade Genetic Algorithm Console Application
    ///     A Microsoft-style console application for genetic algorithm-based trading strategy optimization.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal partial class Program
    {
        #region Main Entry Point

        /// <summary>
        ///     Application entry point.
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        /// <returns>Exit code: 0 for success, non-zero for error</returns>
        private static async Task<int> Main(string[] args)
        {
            try
            {
                //fixes the compiler warning...
                await Task.Delay(1);

                // SET UP CONSOLE CLOSE EVENT HANDLERS FIRST
                SetupConsoleCloseHandlers();

                //await new IVPreCalcTests().Prepare();
                //new CyclicalPriceDataGeneratorTests().GenerateAllThree();

                ConsoleUtilities.LogToFile = true;
                ConsoleUtilities.EnableFileLogging();

                //var prices = new Prices();
                //var polygon = new Polygon2.Polygon(prices, "SPY", 10, 10);
                //polygon.CombineBulkDataForStocks("SPY", DateTime.Now.AddYears(-5).Date, DateTime.Now.Date);

                // Display application header
                DisplayApplicationHeader();

                // Parse command line arguments if needed
                if (args.Length > 0)
                {
                    if (args[0] == "/?" || args[0] == "-h" || args[0] == "--help")
                    {
                        DisplayUsage();
                        return 0;
                    }

                    // NEW: Add position sizer performance demo
                    if (args[0] == "--position-sizer-demo" || args[0] == "-psd")
                    {
                        DynamicPositionSizerDemo.RunPerformanceDemo();
                        return 0;
                    }

                    // NEW: Add PriceRange performance test
                    if (args[0] == "--test-pricerange" || args[0] == "-tpr")
                    {
                        TestPriceRangeOptimizations();
                        return 0;
                    }
                }

                // Initialize and run the trading simulation
                return RunTradingSimulation();
            }
            catch (Exception ex)
            {
                DisplayError($"An unexpected error occurred: {ex.Message}");
                if (args.Length > 0 && args.Contains("--verbose"))
                    ConsoleUtilities.WriteLine($"Stack trace: {ex.StackTrace}");

                return 1;
            }
            finally
            {
                // Ensure log is flushed on normal exit
                FlushAndCleanup();
                DisplayProgramFooter();
            }
        }

        /// <summary>
        /// Set up event handlers to catch application close events (X button, Ctrl+C, process kill, etc.)
        /// </summary>
        private static void SetupConsoleCloseHandlers()
        {
            // Handle Ctrl+C, Ctrl+Break, and close console window (X button)
            Console.CancelKeyPress += (sender, e) =>
            {
                ConsoleUtilities.WriteLine("[SYSTEM] Console close event detected - flushing log...");
                FlushAndCleanup();

                // Allow the process to terminate gracefully
                e.Cancel = false;
            };

            // Handle process exit events (covers X button clicks and other termination methods)
            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                ConsoleUtilities.WriteLine("[SYSTEM] Process exit event detected - flushing log...");
                FlushAndCleanup();
            };

            // Handle unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                ConsoleUtilities.WriteLine($"[SYSTEM] Unhandled exception detected - flushing log... Exception: {e.ExceptionObject}");
                FlushAndCleanup();
            };
        }

        /// <summary>
        /// Centralized cleanup method to flush logs and perform shutdown tasks
        /// </summary>
        private static void FlushAndCleanup()
        {
            try
            {
                ConsoleUtilities.WriteLine("[SYSTEM] Performing cleanup and log flush...");
                ConsoleUtilities.FlushLog();

                // Add a small delay to ensure file write completes
                Thread.Sleep(100);

                ConsoleUtilities.WriteLine("[SYSTEM] Cleanup completed.");
                ConsoleUtilities.FlushLog();
            }
            catch (Exception ex)
            {
                // Last resort - try to write to standard console
                Console.WriteLine($"[ERROR] Failed to flush log during cleanup: {ex.Message}");
            }
        }

        /// <summary>
        ///     Displays application footer and cleanup information.
        /// </summary>
        private static void DisplayProgramFooter()
        {
            try
            {
                ConsoleUtilities.WriteLine();
                ConsoleUtilities.WriteLine("Press any key to exit...");

                // Check if console input is available before trying to read
                if (Environment.UserInteractive && !Console.IsInputRedirected)
                    Console.ReadKey(true);
                else
                    // If running without interactive console (e.g., redirected output), just wait briefly
                    Thread.Sleep(1000);
            }
            catch (InvalidOperationException ex)
            {
                // Handle "The handle is invalid" errors gracefully
                ConsoleUtilities.WriteLine($"[WARNING] Console input not available: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Handle any other console-related errors
                ConsoleUtilities.WriteLine($"[WARNING] Console error: {ex.Message}");
            }
            // Note: FlushAndCleanup() is called in Main's finally block, so no need to duplicate here
        }

        #endregion

        // DATA SPLIT RATIOS - Strict temporal splitting

        // OVERFITTING PREVENTION PARAMETERS - More conservative

        // GENETIC ALGORITHM PARAMETERS - More conservative

        // INDICATOR CONSTRAINTS - More restrictive

        // TRADING PARAMETERS - More conservative
        // Options have their own GA-evolved trade percentage bounds

        // INNER TIME-SERIES CV (fitness) - disabled by default due to runtime cost

        // WINDOW OPTIMIZER INTEGRATION

        // WALKFORWARD ANALYSIS PARAMETERS

        // HISTORICAL TRACKING PARAMETERS - Disabled for simplicity

        // ISLAND EVOLUTION PARAMETERS - Disabled

        // SCHEMA PRESERVATION PARAMETERS - Disabled

        #region Global Tracking Variables

        // Global historical gene pool for cross-window learning
        private static GeneticEvolvers.HistoricalGenePool _globalGenePool;
        private static readonly List<GeneticIndividual> _allTimeChampions = new List<GeneticIndividual>();

        private static readonly Dictionary<string, List<GeneticIndividual>> _strategyPatterns =
            new Dictionary<string, List<GeneticIndividual>>();

        private static int _totalGenerationsRun;
        private static int _totalWindowsCompleted;

        #endregion

        #region Data Science Validation Constants

        // STATISTICAL SIGNIFICANCE THRESHOLDS
        public const double MinimumPerformanceGap = 5.0;        // Minimum gap to consider significant
        public const double MaximumAcceptableGap = 10.0;       // Maximum gap before overfitting warning
        public const double CrossValidationVarianceThreshold = 15.0; // Max CV standard deviation
        public const double MinimumSampleSize = 50;            // Minimum samples for statistical validity

        // VALIDATION METRICS
        public const double MinimumSharpeRatio = 0.5;          // Minimum acceptable Sharpe ratio
        public const double MaximumDrawdown = 20.0;            // Maximum acceptable drawdown
        public const double MinimumWinRate = 0.4;              // Minimum win rate (40%)
        public const int MinimumTrades = 10;                   // Minimum trades for validity

        #endregion

        #region Walkforward Analysis Data Structures

        /// <summary>
        ///     Represents results from a single walkforward window.
        /// </summary>
        public struct WalkforwardWindow
        {
            public int WindowIndex;
            public int TrainingStartIndex;
            public int TrainingEndIndex;
            public int TestStartIndex;
            public int TestEndIndex;
            public DateTime? TrainingStartDate;
            public DateTime? TrainingEndDate;
            public DateTime? TestStartDate;
            public DateTime? TestEndDate;
            public GeneticIndividual BestIndividual;
            public double TrainingPerformance;
            public double TestPerformance;
            public double PerformanceGap;
            public int TradesExecuted;
            public double MaxDrawdown;
            public double SharpeRatio;
            public bool EarlyStoppedDueToOverfitting;
            public int GenerationsUsed;
            public bool IsStatisticallySignificant;
        }

        /// <summary>
        ///     Comprehensive walkforward analysis results.
        /// </summary>
        public struct WalkforwardResults
        {
            public List<WalkforwardWindow> Windows;
            public double AverageTrainingPerformance;
            public double AverageTestPerformance;
            public double AveragePerformanceGap;
            public double ConsistencyScore; // Standard deviation of test performances
            public double CumulativeReturn;
            public double MaxSystemDrawdown;
            public double AverageSharpeRatio;
            public int TotalTradesExecuted;
            public double OverfittingFrequency; // Percentage of windows with large gaps
            public bool IsStrategyRobust; // Overall assessment
            public double StatisticalSignificance; // Percentage of statistically significant windows
        }

        #endregion

        #region Temporary Storage for Walkforward Metadata

        // Temporary storage for per-window metadata
        private static int _tempGenerationsUsed;
        private static bool _tempEarlyStopped;

        private static void StoreTempGenerationsUsed(int generations)
        {
            _tempGenerationsUsed = generations;
        }

        private static void StoreTempEarlyStopped(bool earlyStopped)
        {
            _tempEarlyStopped = earlyStopped;
        }

        private static int GetTempGenerationsUsed()
        {
            return _tempGenerationsUsed;
        }

        private static bool GetTempEarlyStopped()
        {
            return _tempEarlyStopped;
        }

        #endregion

        #region Prediction Support Data Structures

        /// <summary>
        ///     Represents a market signal with confidence
        /// </summary>
        private struct MarketSignal
        {
            public double Signal;
            public string RecommendedAction;
            public double Confidence;
            public bool IsStatisticallySignificant;
        }

        /// <summary>
        ///     Represents risk metrics for a prediction model
        /// </summary>
        private struct ModelRiskMetrics
        {
            public double SharpeRatio;
            public double MaxDrawdown;
            public double ProfitFactor;
            public double WinRate;
            public bool IsRobust;
            public bool PassesStatisticalTests;
            public int SampleSize;
        }

        #endregion

        #region Helper Method Name Fix
        
        private static void PrintBestIndividualConfiguration(GeneticIndividual best)
        {
            if (best == null) return;

            ConsoleUtilities.WriteLine();
            WriteSection("Best Individual Configuration");
            ConsoleUtilities.WriteLine($"  AllowedSecurity: {best.AllowedSecurityTypes}, AllowedTrade: {best.AllowedTradeTypes}");
            ConsoleUtilities.WriteLine($"  Combination: {best.CombinationMethod}, EnsembleThreshold: {best.EnsembleVotingThreshold}");
            ConsoleUtilities.WriteLine($"  Trade %: {best.TradePercentageForStocks:P2}, OHLC: {best.OHLC}");
            // NEW: show global buffer preference
            ConsoleUtilities.WriteLine($"  BufferSource: {best.BufferSource}");
            if (best.AllowedSecurityTypes == AllowedSecurityType.Option)
            {
                ConsoleUtilities.WriteLine($"  Options: {best.NumberOfOptionContractsToOpen:F0}c, {best.OptionDaysOut}d, ±{best.OptionStrikeDistance} strikes");
            }

            ConsoleUtilities.WriteLine("  Indicators:");
            for (int i = 0; i < best.Indicators.Count; i++)
            {
                var ind = best.Indicators[i];
                ConsoleUtilities.WriteLine(
                    $"    #{i + 1}: Type={ind.Type}, Period={ind.Period}, Mode={ind.Mode}, TF={ind.TimeFrame}, OHLC={ind.OHLC}, Polarity={ind.Polarity}");
                ConsoleUtilities.WriteLine(
                    $"        Thresholds: Buy>={ind.LongThreshold:F4}, Sell<={ind.ShortThreshold:F4}; FastMA={ind.FastMAPeriod}, SlowMA={ind.SlowMAPeriod}");
                // NEW: per-indicator buffer source
                ConsoleUtilities.WriteLine($"        BufferSource={ind.BufferSource}, TradeMode={ind.TradeMode}");
            }
        }

        #endregion

        /// <summary>
        /// Run performance tests to verify PriceRange optimizations
        /// </summary>
        public static void TestPriceRangeOptimizations()
        {
            Console.WriteLine("=== TESTING PRICERANGE OPTIMIZATIONS ===");
            
            try 
            {
                Tests.ManualPerformanceTest.RunPerformanceComparison();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running performance test: {ex.Message}");
            }
        }
    }
}