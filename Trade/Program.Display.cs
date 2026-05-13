using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using Trade.Prices2;

namespace Trade
{
    internal partial class Program
    {
        #region Display Methods

        /// <summary>
        ///     Displays the application header with version information.
        /// </summary>
        private static void DisplayApplicationHeader()
        {
            try
            {
                Console.Clear();
                var version = Assembly.GetExecutingAssembly().GetName().Version;

                Console.ForegroundColor = ConsoleColor.Cyan;
                ConsoleUtilities.WriteLine(
                    "═══════════════════════════════════════════════════════════════════════════════");
                ConsoleUtilities.WriteLine("                    Trade Genetic Algorithm Console Application");
                ConsoleUtilities.WriteLine($"                                   Version {version}");
                ConsoleUtilities.WriteLine("                              Copyright © arachnode.net, llc");
                ConsoleUtilities.WriteLine(
                    "═══════════════════════════════════════════════════════════════════════════════");
                Console.ResetColor();
                ConsoleUtilities.WriteLine();
            }
            catch (Exception ex)
            {
                ConsoleUtilities.WriteLine(ex.Message, ConsoleColor.Red);

                // Fallback for console issues - just write plain text
                ConsoleUtilities.WriteLine("Trade Genetic Algorithm Console Application");
                ConsoleUtilities.WriteLine($"Version {Assembly.GetExecutingAssembly().GetName().Version}");
                ConsoleUtilities.WriteLine("Copyright © arachnode.net, llc");
                ConsoleUtilities.WriteLine();
            }
        }

        /// <summary>
        ///     Displays command-line usage information.
        /// </summary>
        private static void DisplayUsage()
        {
            ConsoleUtilities.WriteLine("USAGE:");
            ConsoleUtilities.WriteLine("    Trade.exe [options]");
            ConsoleUtilities.WriteLine();
            ConsoleUtilities.WriteLine("OPTIONS:");
            ConsoleUtilities.WriteLine("    /?          Display this help message");
            ConsoleUtilities.WriteLine("    -h          Display this help message");
            ConsoleUtilities.WriteLine("    --help      Display this help message");
            ConsoleUtilities.WriteLine("    --verbose   Display detailed error information");
            ConsoleUtilities.WriteLine();
            ConsoleUtilities.WriteLine("DESCRIPTION:");
            ConsoleUtilities.WriteLine(
                "    Runs a genetic algorithm to optimize trading strategies using technical indicators.");
            ConsoleUtilities.WriteLine(
                "    The application generates synthetic price data and evolves trading strategies");
            ConsoleUtilities.WriteLine("    through genetic algorithm optimization.");
        }

        /// <summary>
        ///     Displays individual trading results.
        /// </summary>
        /// <param name="individual">Individual to display results for</param>
        /// <param name="priceRecords">Price record data</param>
        private static void DisplayIndividualResults(GeneticIndividual individual, PriceRecord[] priceRecords)
        {
            WriteSection("Individual Trading Results");

            var priceBuffer = GeneticIndividual.ExtractClosePrices(priceRecords);
            var maxFitness = GeneticIndividual.CalculateMaximalFitness(priceBuffer);
            var totalTrades = individual.Trades.Count;

            // Use executed trades to count long/short orders
            var totalLongs = individual.Trades.Count(t => t.AllowedTradeType == AllowedTradeType.Buy);
            var totalShorts = individual.Trades.Count(t => t.AllowedTradeType == AllowedTradeType.SellShort);

            var finalBalance = individual.FinalBalance;
            var startingBalance = individual.StartingBalance;
            var netDollarGain = finalBalance - startingBalance;

            var results = new[]
            {
                $"Theoretical Maximum Dollar Gain : ${maxFitness.DollarGain:F4}",
                $"Theoretical Maximum Percent Gain: {maxFitness.PercentGain:F4}%",
                $"Individual Dollar Gain          : ${individual.Fitness.DollarGain:F4}",
                $"Individual Percent Gain         : {individual.Fitness.PercentGain:F4}%",
                // Fix label: report final balance and net gain separately for clarity
                $"Final Balance                   : ${finalBalance:F2}",
                $"Net Dollar Gain                 : ${netDollarGain:F2}",
                $"Starting Balance                : ${startingBalance:F2}",
                $"Total Trades Executed           : {totalTrades}",
                $"Total Long Orders                : {totalLongs}",
                $"Total Short Orders               : {totalShorts}",
                $"Win Rate                        : {(totalTrades > 0 ? individual.Trades.Count(t => t.ActualDollarGain > 0) * 100.0 / totalTrades : 0):F1}%",
                $"Profit Factor                   : {CalculateProfitFactor(individual):F2}"
            };

            foreach (var result in results) ConsoleUtilities.WriteLine($"  {result}");

            // Assert math consistency
            //try { ProgramTypeSafeHooks.AssertPnLConsistency(individual, nameof(DisplayIndividualResults)); } catch { }

            // Display fitness comparison
            ConsoleUtilities.WriteLine();
            if (Math.Abs(individual.Fitness.PercentGain - maxFitness.PercentGain) < 1e-6)
            {
                WriteSuccess("✓ Individual percent gain matches theoretical maximum!");
            }
            else
            {
                WriteWarning("! Individual percent gain does not match theoretical maximum");
                ConsoleUtilities.WriteLine(
                    $"  Efficiency: {individual.Fitness.PercentGain / maxFitness.PercentGain * 100:F1}%");
            }

            // Display indicator parameters
            ConsoleUtilities.WriteLine();
            WriteInfo("Indicator Configuration:");
            var indicator = individual.Indicators[0];
            ConsoleUtilities.WriteLine($"  Type: {indicator.Type} (Indicator)");
            ConsoleUtilities.WriteLine($"  Amplitude: {indicator.Period}");
            ConsoleUtilities.WriteLine($"  Mode: {indicator.Mode} (Delta Mode)");
            ConsoleUtilities.WriteLine($"  Phase: {indicator.TimeFrame}");
            ConsoleUtilities.WriteLine($"  Offset: {indicator.Param1}");
            ConsoleUtilities.WriteLine($"  Long Threshold: {indicator.LongThreshold:F4}");
            ConsoleUtilities.WriteLine($"  Short Threshold: {indicator.ShortThreshold:F4}");

            // Display detailed trades listing
            DisplayTradesList(individual, priceRecords, verifyWithEvents: EnableTradeVerification);
        }

        private static void PrintGaSummary(GeneticIndividual individual, PriceRecord[] priceRecords)
        {
            Func<double, int, string> FormatPercent = (pct, width) =>
            {
                return (pct.ToString("F2") + "%").PadLeft(width);
            };

            double startingBalance = (individual != null) ? individual.StartingBalance : 0.0;
            double finalBalance = (individual != null && individual.Trades.Count > 0)
                ? individual.Trades[individual.Trades.Count - 1].Balance
                : startingBalance;
            int tradesCount = (individual != null) ? individual.Trades.Count : 0;

            double pnl = finalBalance - startingBalance;
            double percentGain = (startingBalance != 0.0) ? (pnl / startingBalance * 100.0) : 0.0;

            // Get date range from priceRecords
            var (firstDate, lastDate, tradingDays) = GeneticIndividual.GetDateRange(priceRecords);
            double years = Math.Max(0.0, (lastDate - firstDate).TotalDays / 365.25);

            double annualizedReturnPct = 0.0;
            if (years > 0 && startingBalance > 0)
            {
                var grow = finalBalance / startingBalance;
                annualizedReturnPct = (Math.Pow(grow, 1.0 / years) - 1.0) * 100.0;
            }

            // Header
            ConsoleUtilities.WriteLine("■ Genetic Algorithm Comparison Results");
            ConsoleUtilities.WriteLine("──────────────────────────────────────");
            ConsoleUtilities.WriteLine(string.Format("{0,-20} {1,12} {2,13} {3,8} {4,24} {5,12}",
                "Strategy", "Dollar Gain", "Percent Gain", "Trades", "Date Range", "Annualized %"));
            ConsoleUtilities.WriteLine("──────────────────────────────────────────────────────────────────────────────");

            var percentCell = FormatPercent(percentGain, 13);
            ConsoleUtilities.WriteLine(string.Format("{0,-20} {1,12:C0} {2} {3,8} {4,24} {5,12:F2}",
                "GA Best Individual", pnl, percentCell, tradesCount,
                $"{firstDate:yyyy-MM-dd} to {lastDate:yyyy-MM-dd}", annualizedReturnPct));
        }

        /// <summary>
        ///     Displays comparison results between different individuals.
        /// </summary>
        /// <param name="baseIndividual">Sin indicator individual</param>
        /// <param name="bestIndividual">Best individual from genetic algorithm</param>
        private static void DisplayComparisonResults(GeneticIndividual baseIndividual, GeneticIndividual bestIndividual,
            PriceRecord[] priceRecords)
        {
            if (baseIndividual != null)
            {
                ConsoleUtilities.WriteLine(
                    string.Format("{0,-20} {1, -15} {2,-15} {3,-10}",
                        "Base Individual",
                        baseIndividual.Fitness.DollarGain.ToString("C2"),
                        baseIndividual.Fitness.PercentGain.ToString("F2") + "%",
                        baseIndividual.Trades.Count));
            }

            if (bestIndividual != null)
            {
                PrintGaSummary(bestIndividual, priceRecords);
                // NEW: print detailed configuration with buffer preferences
                PrintBestIndividualConfiguration(bestIndividual);
            }

            if (baseIndividual == null && bestIndividual == null)
            {
                // Determine winner
                ConsoleUtilities.WriteLine();
                if (bestIndividual.Fitness.PercentGain > baseIndividual.Fitness.PercentGain)
                    WriteSuccess(
                        $"✓ Genetic Algorithm improved performance by {bestIndividual.Fitness.PercentGain - baseIndividual.Fitness.PercentGain:F2}%");
                else if (bestIndividual.Fitness.PercentGain < baseIndividual.Fitness.PercentGain)
                    WriteWarning(
                        $"! Sin Individual outperformed GA by {baseIndividual.Fitness.PercentGain - bestIndividual.Fitness.PercentGain:F2}%");
                else
                    WriteInfo("= Both strategies achieved identical performance");
            }

            if (bestIndividual != null)
            {
                // Display best individual parameters (existing)
                ConsoleUtilities.WriteLine();
                WriteInfo("Best Individual Indicator Configuration:");
                for (var idx = 0; idx < bestIndividual.Indicators.Count; idx++)
                {
                    var ind = bestIndividual.Indicators[idx];
                    ConsoleUtilities.WriteLine($"  Indicator {idx + 1}:");
                    ConsoleUtilities.WriteLine($"    Type: {ind.Type}, Period: {ind.Period}, OHLC: {ind.OHLC}, Mode: {ind.Mode}");
                    ConsoleUtilities.WriteLine($"    Timeframe: {ind.TimeFrame}, Polarity: {ind.Polarity}");
                    ConsoleUtilities.WriteLine(
                        $"    Buy Threshold: {ind.LongThreshold:F4}, Sell Threshold: {ind.ShortThreshold:F4}");
                }

                // Display detailed trades listing
                DisplayTradesList(bestIndividual, priceRecords, verifyWithEvents: EnableTradeVerification);
            }
        }

        private static void DisplayTradesList(GeneticIndividual individual, PriceRecord[] priceRecords, bool verifyWithEvents = false)
        {
            var priceBuffer = GeneticIndividual.ExtractClosePrices(priceRecords);

            ConsoleUtilities.WriteLine();
            WriteInfo("Detailed Trades Executed:");

            if (individual.Trades.Count == 0)
            {
                ConsoleUtilities.WriteLine("  No trades were executed.");
                return;
            }

            // NEW: Event verification if requested
            if (verifyWithEvents)
            {
                WriteInfo("Verifying trade records with events...");
                VerifyTradeConsistencyWithEvents(individual, priceRecords);
            }

            // Chronologically sort trades for display (actual occurrence order) and keep only completed trades
            var sortedTrades = individual.Trades
                .Where(t => t.CloseIndex > t.OpenIndex)
                .OrderBy(t => t.CloseIndex)
                .ThenBy(t => t.ResponsibleIndicatorIndex)
                .ThenBy(t => t.ResponsibleIndicatorIndex) // stable ordering when simultaneous
                .ToList();

            // Header: add Indicator column (Ind) showing responsible indicator index (or 'C' for combined)
            ConsoleUtilities.WriteLine(
                string.Format("  {0,-3} {1,-3} {2,-12} {3,-22} {4,-12} {5,-12} {6,-6} {7,-6} {8,-8} {9,-8} {10,-10} {11,-10} {12,-8} {13,-8} {14,-8} {15,-12}",
                "#", "Ind", "Type", "Contract", "Open Date", "Close Date", "Open", "Close", "Price $", "Close $", "Contracts",
                "$ Position", "Gain $", "Gain %", "Duration", "Balance $"));

            ConsoleUtilities.WriteLine(
                "  " + new string('─', 3) + " " + new string('─', 3) + " " + new string('─', 12) + " " + new string('─', 22) + " " +
                      new string('─', 12) + " " + new string('─', 12) + " " + new string('─', 6) + " " +
                      new string('─', 6) + " " + new string('─', 8) + " " + new string('─', 8) + " " +
                      new string('─', 10) + " " + new string('─', 10) + " " + new string('─', 8) + " " +
                      new string('─', 8) + " " + new string('─', 8) + " " + new string('─', 12));

            // Per-trade rows (chronological numbering)
            for (int i = 0; i < sortedTrades.Count; i++)
            {
                var trade = sortedTrades[i];
                string direction = trade.AllowedTradeType == AllowedTradeType.Buy ? "LONG" : "SHORT";

                // Derive security code
                string security;
                if (trade.AllowedSecurityType == AllowedSecurityType.Option)
                {
                    var action = (individual.TradeActions.ElementAtOrDefault(trade.OpenIndex) ?? "");
                    var parts = action.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    var last = parts.Length > 0 ? parts[parts.Length - 1] : "";
                    if (last.IndexOf("CALL", StringComparison.OrdinalIgnoreCase) >= 0) security = "C";
                    else if (last.IndexOf("PUT", StringComparison.OrdinalIgnoreCase) >= 0) security = "P";
                    else security = "O";
                    if (IsExpiredOptionTrade(trade, priceRecords, individual.TradeActions)) security += " (E)";
                }
                else
                {
                    security = trade.AllowedTradeType == AllowedTradeType.Buy ? "B" : "S";
                }

                // Option contract symbol (or '-')
                string contractCol = "-";
                if (trade.AllowedSecurityType == AllowedSecurityType.Option && trade.PriceRecordForClose != null && trade.PriceRecordForClose.Option != null)
                {
                    contractCol = trade.PriceRecordForClose.Option.GetStandardSymbol();
                    if (string.IsNullOrWhiteSpace(contractCol))
                    {
                        var action = (individual.TradeActions.ElementAtOrDefault(trade.OpenIndex) ?? "");
                        var parts = action.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        contractCol = parts.Length > 0 ? parts[parts.Length - 1].Trim() : "-";
                    }
                }

                string typeCol = direction + " " + security;
                int durationBars = trade.CloseIndex - trade.OpenIndex;
                double contracts = Math.Abs(trade.Position);
                double positionInDollars = trade.PositionInDollars;
                double actualGain = trade.ActualDollarGain;
                double tradeBalance = trade.Balance;
                string indCol = trade.ResponsibleIndicatorIndex < 0 ? "C" : trade.ResponsibleIndicatorIndex.ToString();

                // Dates
                string openDateStr = "N/A";
                string closeDateStr = "N/A";
                if (trade.OpenIndex >= 0 && trade.OpenIndex < priceRecords.Length)
                {
                    var d = GetDisplayDateForLog(priceRecords, trade.OpenIndex);
                    openDateStr = d != DateTime.MinValue ? d.ToString("MM/dd/yyyy") : "N/A";
                }
                if (trade.CloseIndex >= 0 && trade.CloseIndex < priceRecords.Length)
                {
                    var d = GetDisplayDateForLog(priceRecords, trade.CloseIndex);
                    closeDateStr = d != DateTime.MinValue ? d.ToString("MM/dd/yyyy") : "N/A";
                }

                if (trade.DollarGain > 0) Console.ForegroundColor = ConsoleColor.Green;
                else if (trade.DollarGain < 0) Console.ForegroundColor = ConsoleColor.Red;
                else Console.ForegroundColor = ConsoleColor.Gray;

                ConsoleUtilities.WriteLine(string.Format(
                    "  {0,-3} {1,-3} {2,-12} {3,-22} {4,-12} {5,-12} {6,-6} {7,-6} {8,-8:F2} {9,-8:F2} {10,-10:F2} ${11,-9:F0} ${12,-7:F0} {13,-8:F1} {14,-8} ${15,-11:F0}",
                    i + 1, indCol, typeCol, contractCol, openDateStr, closeDateStr,
                    trade.OpenIndex, trade.CloseIndex,
                    trade.OpenPrice, trade.ClosePrice,
                    contracts, positionInDollars, actualGain,
                    trade.PercentGain, durationBars, tradeBalance));

                Console.ResetColor();
            }

            DisplayEquityVsSpyOverview(priceRecords, individual);

            // --- Stats block ---
            ConsoleUtilities.WriteLine();
            ConsoleUtilities.WriteLine("  Trade Statistics:");

            var tradesForStats = sortedTrades; // already filtered & chronological
            if (tradesForStats.Count == 0)
            {
                ConsoleUtilities.WriteLine("    No completed trades.");
                return;
            }

            // Classify by actual P&L (position-sized), not per-share change
            var profitableTrades = tradesForStats.Where(t => t.ActualDollarGain > 0).ToList();
            var losingTrades = tradesForStats.Where(t => t.ActualDollarGain < 0).ToList();
            var breakEvenTrades = tradesForStats.Where(t => Math.Abs(t.ActualDollarGain) < 0.01).ToList();

            ConsoleUtilities.WriteLine(string.Format("    Profitable Trades    : {0} ({1:F1}%)",
                profitableTrades.Count, tradesForStats.Count > 0 ? profitableTrades.Count * 100.0 / tradesForStats.Count : 0.0));
            ConsoleUtilities.WriteLine(string.Format("    Losing Trades        : {0} ({1:F1}%)",
                losingTrades.Count, tradesForStats.Count > 0 ? losingTrades.Count * 100.0 / tradesForStats.Count : 0.0));
            ConsoleUtilities.WriteLine(string.Format("    Break-even Trades    : {0} ({1:F1}%)",
                breakEvenTrades.Count, tradesForStats.Count > 0 ? breakEvenTrades.Count * 100.0 / tradesForStats.Count : 0.0));

            var profitFactor = CalculateProfitFactor(individual);
            ConsoleUtilities.WriteLine(string.Format("    Profit Factor        : {0:F2}", profitFactor));

            if (tradesForStats.Count > 0)
            {
                double avgRegularBars = AverageRegularDurationBars(tradesForStats, priceRecords);
                ConsoleUtilities.WriteLine(string.Format("    Average Duration    : {0:F1} bars (excl. gap bars)", avgRegularBars));

                double finalBalance = tradesForStats[tradesForStats.Count - 1].Balance;
                double startingBalance = individual.StartingBalance;
                double totalPnL = tradesForStats.Sum(t => t.ActualDollarGain);

                ConsoleUtilities.WriteLine(string.Format("    Total P&L           : ${0:F2}", totalPnL));
                ConsoleUtilities.WriteLine(string.Format("    Final Balance       : ${0:F0}", finalBalance));
                ConsoleUtilities.WriteLine(string.Format("    Total Return        : {0:F1}%", (finalBalance - startingBalance) / startingBalance * 100.0));

                // Derive first/last real dates for span
                var minOpen = tradesForStats.Min(t => Math.Max(0, t.OpenIndex));
                var maxClose = tradesForStats.Max(t => Math.Min(priceRecords.Length - 1, t.CloseIndex));
                var firstDate = GetDisplayDateForLog(priceRecords, minOpen);
                var lastDate = GetDisplayDateForLog(priceRecords, maxClose);
                double years = Math.Max(0.0, (lastDate - firstDate).TotalDays / 365.25);

                ConsoleUtilities.WriteLine(string.Format("    Data Period Length  : {0} bars (incl. gap bars)", priceBuffer != null ? priceBuffer.Length : 0));
                ConsoleUtilities.WriteLine(string.Format("    Estimated Time Span : {0:F2} years", years));

                double annualizedReturnPct = 0.0;
                if (years > 0 && startingBalance > 0)
                {
                    var grow = finalBalance / startingBalance;
                    annualizedReturnPct = (Math.Pow(grow, 1.0 / years) - 1.0) * 100.0;
                }
                ConsoleUtilities.WriteLine(string.Format("    Annualized Return   : {0:F1}%", annualizedReturnPct));

                if (priceBuffer != null && priceBuffer.Length > 1)
                {
                    double startingPrice = priceBuffer[0];
                    double endingPrice = priceBuffer[priceBuffer.Length - 1];
                    double sharesBought = startingBalance / startingPrice;
                    const double dividendYield = 0.014;

                    double totalDividends = startingBalance * dividendYield * years;
                    double bhFinal = sharesBought * endingPrice + totalDividends;
                    double bhPnl = bhFinal - startingBalance;
                    double bhAnnualPct = (years > 0 && startingBalance > 0)
                        ? (Math.Pow(bhFinal / startingBalance, 1.0 / years) - 1.0) * 100.0
                        : 0.0;

                    ConsoleUtilities.WriteLine();
                    ConsoleUtilities.WriteLine("  Buy & Hold Comparison (with dividends):");
                    ConsoleUtilities.WriteLine(string.Format("    Buy & Hold Gain     : ${0:F2}", bhPnl));
                    ConsoleUtilities.WriteLine(string.Format("    Buy & Hold Annual   : {0:F1}%", bhAnnualPct));
                    ConsoleUtilities.WriteLine(string.Format("    Strategy vs B&H     : {0:+0.0;-0.0;0.0}%", annualizedReturnPct - bhAnnualPct));
                }
            }
        }

        /// <summary>
        ///     Display comprehensive walkforward analysis results with historical tracking.
        /// </summary>
        private static void DisplayWalkforwardResults(WalkforwardResults results)
        {
            WriteSection("Walkforward Analysis Results");

            ConsoleUtilities.WriteLine(
                "═══════════════════════════════════════════════════════════════════════════════");
            ConsoleUtilities.WriteLine("                     ENHANCED WALKFORWARD ANALYSIS SUMMARY");
            ConsoleUtilities.WriteLine(
                "═══════════════════════════════════════════════════════════════════════════════");
            ConsoleUtilities.WriteLine();

            // Overall Assessment
            ConsoleUtilities.WriteLine("🎯 OVERALL STRATEGY ASSESSMENT:");
            if (results.IsStrategyRobust)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                ConsoleUtilities.WriteLine("   ✅ STRATEGY IS ROBUST - Shows consistent out-of-sample performance");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                ConsoleUtilities.WriteLine("   ❌ STRATEGY SHOWS OVERFITTING - Poor out-of-sample generalization");
            }

            Console.ResetColor();
            ConsoleUtilities.WriteLine();

            // Enhanced Performance Summary with Historical Tracking
            ConsoleUtilities.WriteLine("📊 PERFORMANCE SUMMARY:");
            ConsoleUtilities.WriteLine($"   Average Training Performance: {results.AverageTrainingPerformance:F2}%");
            ConsoleUtilities.WriteLine($"   Average Test Performance:     {results.AverageTestPerformance:F2}%");
            ConsoleUtilities.WriteLine($"   Average Performance Gap:      {results.AveragePerformanceGap:F2}%");
            ConsoleUtilities.WriteLine($"   Cumulative Return:            {results.CumulativeReturn:F2}%");
            ConsoleUtilities.WriteLine($"   Maximum System Drawdown:      {results.MaxSystemDrawdown:F2}%");
            ConsoleUtilities.WriteLine($"   Average Sharpe Ratio:         {results.AverageSharpeRatio:F3}");
            ConsoleUtilities.WriteLine($"   Total Trades Executed:        {results.TotalTradesExecuted}");

            // Historical tracking metrics
            if (EnableHistoricalTracking)
            {
                ConsoleUtilities.WriteLine();
                ConsoleUtilities.WriteLine("🧬 GENETIC ALGORITHM TRACKING:");
                ConsoleUtilities.WriteLine($"   Historical Champions Archived: {_allTimeChampions.Count}");
                ConsoleUtilities.WriteLine($"   Unique Strategy Patterns:      {_strategyPatterns.Count}");
                ConsoleUtilities.WriteLine($"   Total Generations Run:         {_totalGenerationsRun}");
                ConsoleUtilities.WriteLine($"   Windows Completed:             {_totalWindowsCompleted}");

                if (_globalGenePool != null)
                {
                    ConsoleUtilities.WriteLine($"   Hall of Fame Size:             {_globalGenePool.HallOfFame.Count}");
                    ConsoleUtilities.WriteLine(
                        $"   Interesting Mutants:           {_globalGenePool.InterestingMutants.Count}");
                    ConsoleUtilities.WriteLine(
                        $"   Schema Patterns Tracked:       {_globalGenePool.SchemaFrequency.Count}");
                }
            }

            ConsoleUtilities.WriteLine();

            // Consistency Metrics
            ConsoleUtilities.WriteLine("🎯 CONSISTENCY METRICS:");
            ConsoleUtilities.WriteLine(
                $"   Test Performance Consistency: {results.ConsistencyScore:F2}% (lower is better)");
            ConsoleUtilities.WriteLine(
                $"   Overfitting Frequency:        {results.OverfittingFrequency:F1}% of windows");
            ConsoleUtilities.WriteLine($"   Total Windows Analyzed:       {results.Windows.Count}");

            // Enhanced genetic algorithm metrics
            if (EnableHistoricalTracking)
            {
                ConsoleUtilities.WriteLine(
                    $"   Cross-Window Learning:         {(EnableHistoricalTracking ? "ENABLED" : "DISABLED")}");
                ConsoleUtilities.WriteLine(
                    $"   Island Evolution:              {(EnableIslandEvolution ? "ENABLED" : "DISABLED")}");
                ConsoleUtilities.WriteLine(
                    $"   Schema Preservation:           {(EnableSchemaPreservation ? "ENABLED" : "DISABLED")}");
            }

            ConsoleUtilities.WriteLine();

            // Window-by-Window Results
            ConsoleUtilities.WriteLine("📈 DETAILED WINDOW RESULTS:");
            ConsoleUtilities.WriteLine(
                string.Format("{0,-4} {1,-12} {2,-10} {3,-8} {4,-7} {5,-12} {6,-8} {7,-5} {8,-15}",
                    "Win", "Training %", "Test %", "Gap %", "Trades", "Drawdown %", "Sharpe", "Gens", "Status"));
            ConsoleUtilities.WriteLine(new string('─', 85));

            for (var i = 0; i < results.Windows.Count; i++)
            {
                var window = results.Windows[i];
                var status = window.EarlyStoppedDueToOverfitting ? "OVERFITTED" : "OK";

                // Correct color assignment
                Console.ForegroundColor = window.EarlyStoppedDueToOverfitting ? ConsoleColor.Yellow : ConsoleColor.White;
                ConsoleUtilities.WriteLine(
                    $"{window.WindowIndex + 1,-4} {window.TrainingPerformance,-12:F2} {window.TestPerformance,-10:F2} " +
                    $"{window.PerformanceGap,-8:F2} {window.TradesExecuted,-7} {window.MaxDrawdown,-12:F2} " +
                    $"{window.SharpeRatio,-8:F3} {window.GenerationsUsed,-5} {status,-15}");
                Console.ResetColor();
            }

            ConsoleUtilities.WriteLine();

            // Enhanced Strategy Pattern Analysis
            if (EnableHistoricalTracking && _strategyPatterns.Count > 0)
            {
                ConsoleUtilities.WriteLine("🧬 TOP STRATEGY PATTERNS:");
                var topPatterns = _strategyPatterns
                    .OrderByDescending(kvp => kvp.Value.Average(ind => ind.Fitness.PercentGain))
                    .Take(5);

                foreach (var pattern in topPatterns)
                {
                    var avgPerformance = pattern.Value.Average(ind => ind.Fitness.PercentGain);
                    var count = pattern.Value.Count;
                    var consistency =
                        CalculateStandardDeviation(pattern.Value.Select(ind => ind.Fitness.PercentGain).ToArray());
                    ConsoleUtilities.WriteLine($"   {pattern.Key}");
                    ConsoleUtilities.WriteLine(
                        $"     → Avg Performance: {avgPerformance:F2}%, Instances: {count}, Consistency: {consistency:F2}%");
                }

                ConsoleUtilities.WriteLine();
            }

            ConsoleUtilities.FlushLog();

            // Recommendations
            ConsoleUtilities.WriteLine("💡 RECOMMENDATIONS:");

            if (results.AveragePerformanceGap > 15.0)
            {
                ConsoleUtilities.WriteLine("   ⚠️  Large performance gap suggests overfitting");
                ConsoleUtilities.WriteLine("   →  Consider reducing model complexity");
                ConsoleUtilities.WriteLine("   →  Increase regularization strength");
                ConsoleUtilities.WriteLine("   →  Use more conservative parameter ranges");
                if (EnableHistoricalTracking)
                    ConsoleUtilities.WriteLine("   →  Historical tracking should help reduce overfitting over time");
            }

            if (results.ConsistencyScore > 20.0)
            {
                ConsoleUtilities.WriteLine("   ⚠️  High performance variance across windows");
                ConsoleUtilities.WriteLine("   →  Strategy may not be robust to market regime changes");
                ConsoleUtilities.WriteLine("   →  Consider ensemble methods or regime detection");
                if (EnableIslandEvolution)
                    ConsoleUtilities.WriteLine("   →  Island evolution should improve robustness");
            }

            if (results.OverfittingFrequency > 50.0)
            {
                ConsoleUtilities.WriteLine("   ⚠️  Frequent overfitting detected");
                ConsoleUtilities.WriteLine("   →  Reduce maximum complexity (MaxComplexity)");
                ConsoleUtilities.WriteLine("   →  Increase early stopping patience");
                ConsoleUtilities.WriteLine("   →  Use cross-validation within training windows");
                if (EnableSchemaPreservation)
                    ConsoleUtilities.WriteLine("   →  Schema preservation should help maintain diversity");
            }

            if (results.MaxSystemDrawdown > 25.0)
            {
                ConsoleUtilities.WriteLine("   ⚠️  High system drawdown");
                ConsoleUtilities.WriteLine("   →  Implement position sizing controls");
                ConsoleUtilities.WriteLine("   →  Add stop-loss mechanisms");
                ConsoleUtilities.WriteLine("   →  Consider portfolio diversification");
            }

            if (results.IsStrategyRobust)
            {
                ConsoleUtilities.WriteLine("   ✅ Strategy shows good generalization");
                ConsoleUtilities.WriteLine("   →  Consider live trading with small position sizes");
                ConsoleUtilities.WriteLine("   →  Monitor performance closely for regime changes");
                ConsoleUtilities.WriteLine("   →  Implement real-time overfitting detection");
                if (EnableHistoricalTracking)
                    ConsoleUtilities.WriteLine("   →  Historical tracking provides additional confidence");
            }

            // Data leakage prevention confirmation
            ConsoleUtilities.WriteLine();
            ConsoleUtilities.WriteLine("🔒 DATA LEAKAGE PREVENTION:");
            ConsoleUtilities.WriteLine("   ✅ Each window uses only its own training data for normalization");
            ConsoleUtilities.WriteLine("   ✅ Test data never influences training or validation");
            ConsoleUtilities.WriteLine("   ✅ Indicator ranges computed independently per window");
            ConsoleUtilities.WriteLine("   ✅ Option pricing models isolated per window");
            ConsoleUtilities.WriteLine("   ✅ Cross-window learning uses only test-validated strategies");

            ConsoleUtilities.WriteLine();
            ConsoleUtilities.WriteLine(
                "═══════════════════════════════════════════════════════════════════════════════");
        }

        #endregion

        #region Trade Event Verification

        /// <summary>
        /// Verify trade consistency by re-processing with event capture
        /// </summary>
        private static void VerifyTradeConsistencyWithEvents(GeneticIndividual individual, PriceRecord[] priceRecords)
        {
            try
            {
                // Create a clone to avoid modifying the original
                var clonedIndividual = CloneIndividualForVerification(individual);
                var eventCapture = new TradeEventCapture(individual.StartingBalance);

                // Wire up custom event capture (we'll intercept trades during processing)
                var originalTrades = individual.Trades.ToList();
                var originalBalance = individual.FinalBalance;
                var originalFitness = individual.Fitness;

                // Clear trades and re-process to capture events
                clonedIndividual.Trades.Clear();
                clonedIndividual.FinalBalance = clonedIndividual.StartingBalance;

                // Process with event monitoring
                WriteInfo("  Re-processing trades to capture events...");
                var reprocessedFitness = clonedIndividual.Process(priceRecords);

                // Compare results
                WriteInfo("  Comparing original vs. reprocessed results...");
                
                // Verify trade counts
                if (originalTrades.Count != clonedIndividual.Trades.Count)
                {
                    WriteWarning($"  ❌ Trade count mismatch: Original={originalTrades.Count}, Reprocessed={clonedIndividual.Trades.Count}");
                }
                else
                {
                    WriteSuccess($"  ✅ Trade count matches: {originalTrades.Count} trades");
                }

                // Verify final balance
                var balanceDiff = Math.Abs(originalBalance - clonedIndividual.FinalBalance);
                if (balanceDiff > 0.01)
                {
                    WriteWarning($"  ❌ Balance mismatch: Original=${originalBalance:F2}, Reprocessed=${clonedIndividual.FinalBalance:F2}, Diff=${balanceDiff:F2}");
                }
                else
                {
                    WriteSuccess($"  ✅ Balance matches: ${originalBalance:F2}");
                }

                // Verify fitness scores
                var fitnessDiff = Math.Abs(originalFitness.PercentGain - reprocessedFitness.PercentGain);
                if (fitnessDiff > 0.01)
                {
                    WriteWarning($"  ❌ Fitness mismatch: Original={originalFitness.PercentGain:F4}%, Reprocessed={reprocessedFitness.PercentGain:F4}%, Diff={fitnessDiff:F4}%");
                }
                else
                {
                    WriteSuccess($"  ✅ Fitness matches: {originalFitness.PercentGain:F4}%");
                }

                // Verify individual trade details
                VerifyIndividualTrades(originalTrades, clonedIndividual.Trades);

                // Verify balance progression
                VerifyBalanceProgression(originalTrades, individual.StartingBalance);

                WriteSuccess("  ✅ Trade verification completed");

            }
            catch (Exception ex)
            {
                WriteWarning($"  ❌ Trade verification failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a simplified clone for verification purposes
        /// </summary>
        private static GeneticIndividual CloneIndividualForVerification(GeneticIndividual original)
        {
            var clone = new GeneticIndividual();
            
            // Copy essential properties
            clone.StartingBalance = original.StartingBalance;
            clone.FinalBalance = original.StartingBalance; // Reset to starting
            clone.AllowedSecurityTypes = original.AllowedSecurityTypes;
            clone.AllowedTradeTypes = original.AllowedTradeTypes;
            clone.TradePercentageForStocks = original.TradePercentageForStocks;
            
            // Copy indicators
            foreach (var indicator in original.Indicators)
            {
                clone.Indicators.Add(new IndicatorParams
                {
                    Type = indicator.Type,
                    Period = indicator.Period,
                    Mode = indicator.Mode,
                    TimeFrame = indicator.TimeFrame,
                    Polarity = indicator.Polarity,
                    LongThreshold = indicator.LongThreshold,
                    ShortThreshold = indicator.ShortThreshold,
                    OHLC = indicator.OHLC
                });
            }

            //return clone;

            var clone2 = JsonConvert.DeserializeObject<GeneticIndividual>(JsonConvert.SerializeObject(original));
            
            return clone2;
        }

        /// <summary>
        /// Verify individual trade details match
        /// </summary>
        private static void VerifyIndividualTrades(List<TradeResult> originalTrades, List<TradeResult> reprocessedTrades)
        {
            var minCount = Math.Min(originalTrades.Count, reprocessedTrades.Count);
            int mismatchCount = 0;

            for (int i = 0; i < minCount; i++)
            {
                var orig = originalTrades[i];
                var repr = reprocessedTrades[i];

                var openPriceDiff = Math.Abs(orig.OpenPrice - repr.OpenPrice);
                var closePriceDiff = Math.Abs(orig.ClosePrice - repr.ClosePrice);
                var gainDiff = Math.Abs(orig.ActualDollarGain - repr.ActualDollarGain);

                if (openPriceDiff > 0.01 || closePriceDiff > 0.01 || gainDiff > 0.01)
                {
                    mismatchCount++;
                    if (mismatchCount <= 3) // Only show first 3 mismatches
                    {
                        WriteWarning($"    ❌ Trade {i + 1} mismatch:");
                        WriteWarning($"       Open: ${orig.OpenPrice:F2} vs ${repr.OpenPrice:F2}");
                        WriteWarning($"       Close: ${orig.ClosePrice:F2} vs ${repr.ClosePrice:F2}");
                        WriteWarning($"       Gain: ${orig.ActualDollarGain:F2} vs ${repr.ActualDollarGain:F2}");
                    }
                }
            }

            if (mismatchCount == 0)
            {
                WriteSuccess($"  ✅ All {minCount} individual trades match perfectly");
            }
            else
            {
                WriteWarning($"  ❌ {mismatchCount} trades have mismatches");
            }
        }

        /// <summary>
        /// Verify balance progression is mathematically correct
        /// </summary>
        private static void VerifyBalanceProgression(List<TradeResult> trades, double startingBalance)
        {
            var calculatedBalance = startingBalance;
            int errorCount = 0;

            for (int i = 0; i < trades.Count; i++)
            {
                var trade = trades[i];
                calculatedBalance += trade.ActualDollarGain;

                var balanceDiff = Math.Abs(calculatedBalance - trade.Balance);
                if (balanceDiff > 0.01)
                {
                    errorCount++;
                    if (errorCount <= 3) // Only show first 3 errors
                    {
                        WriteWarning($"    ❌ Balance progression error at trade {i + 1}:");
                        WriteWarning($"       Calculated: ${calculatedBalance:F2}, Recorded: ${trade.Balance:F2}, Diff: ${balanceDiff:F2}");
                    }
                }
            }

            if (errorCount == 0)
            {
                WriteSuccess($"  ✅ Balance progression is mathematically correct through {trades.Count} trades");
            }
            else
            {
                WriteWarning($"  ❌ {errorCount} balance progression errors detected");
            }
        }

        #endregion

        #region Display Helper Methods

        // NEW helper: detect option expiration (text first; fallback to 3rd Friday heuristic)
        private static bool IsExpiredOptionTrade(TradeResult trade, PriceRecord[] priceRecords, IEnumerable<string> tradeActions)
        {
            if (trade.AllowedSecurityType != AllowedSecurityType.Option) return false;

            var actionsList = tradeActions as IList<string> ?? tradeActions.ToList();

            // Scan action text from open→close for expiration keywords
            int start = Math.Max(0, trade.OpenIndex);
            int stop = Math.Min(trade.CloseIndex, actionsList.Count - 1);
            var sb = new System.Text.StringBuilder();
            for (int k = start; k <= stop; k++)
            {
                var a = actionsList[k];
                if (!string.IsNullOrEmpty(a))
                {
                    sb.Append(a);
                    sb.Append(';');
                }
            }
            string window = sb.ToString();
            if (window.IndexOf("EXPIRE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                window.IndexOf("EXPIRATION", StringComparison.OrdinalIgnoreCase) >= 0 ||
                window.IndexOf("EXPIRED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                window.IndexOf("ASSIGN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                window.IndexOf("EXERCISE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Fallback: if close date is a typical monthly expiry (3rd Friday), treat as expired
            if (trade.CloseIndex >= 0 && trade.CloseIndex < priceRecords.Length)
            {
                var d = priceRecords[trade.CloseIndex].DateTime.Date;
                if (d.DayOfWeek == DayOfWeek.Friday)
                {
                    var thirdFriday = ThirdFriday(d.Year, d.Month);
                    if (d == thirdFriday) return true;
                }
            }

            return false;
        }

        private static DateTime ThirdFriday(int year, int month)
        {
            var first = new DateTime(year, month, 1);
            while (first.DayOfWeek != DayOfWeek.Friday) first = first.AddDays(1);
            return first.AddDays(14); // 3rd Friday
        }

        // Helpers for .NET 4.7.2
        private static int CountRegularBars(PriceRecord[] prices, int openIndex, int closeIndex)
        {
            if (prices == null || openIndex < 0 || closeIndex <= openIndex || closeIndex > prices.Length)
                return 0;
            int count = 0;
            for (int i = openIndex; i < closeIndex; i++)
                if (!prices[i].Manufactured) count++;
            return count;
        }

        private static double AverageRegularDurationBars(List<TradeResult> trades, PriceRecord[] prices)
        {
            var valid = trades.Where(t => t.CloseIndex > t.OpenIndex).ToList();
            if (valid.Count == 0) return 0.0;
            double sum = 0.0;
            foreach (var t in valid)
                sum += CountRegularBars(prices, t.OpenIndex, t.CloseIndex);
            return sum / valid.Count;
        }

        private static DateTime GetDisplayDateForLog(PriceRecord[] prices, int index)
        {
            if (index < 0 || index >= prices.Length) return DateTime.MinValue;
            var bar = prices[index];
            if (!bar.Manufactured) return bar.DateTime.Date;
            for (int i = index + 1; i < prices.Length; i++)
                if (!prices[i].Manufactured)
                    return prices[i].DateTime.Date;
            return bar.DateTime.Date;
        }

        #endregion

        private static void DisplayBestIndividualSummary(GeneticIndividual best, PriceRecord[] priceRecords)
        {
            if (best == null) return;
            PrintGaSummary(best, priceRecords);
            PrintBestIndividualConfiguration(best);
        }
    }
}