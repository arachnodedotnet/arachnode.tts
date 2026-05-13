using System;
using System.Collections.Generic;
using System.Linq;
using Trade.Prices2;

namespace Trade
{
    internal partial class Program
    {
        #region Visualization

        /// <summary>
        ///     Creates an ASCII plot of all indicator values as actual graphs.
        /// </summary>
        /// <param name="individual">Individual with indicator values to display</param>
        private static void Plot(GeneticIndividual individual = null)
        {
            if (individual == null || individual.IndicatorValues.Count == 0)
            {
                WriteWarning("No indicator values available for plotting.");
                return;
            }

            // Plot each indicator's values
            for (var indicatorIndex = 0; indicatorIndex < individual.IndicatorValues.Count; indicatorIndex++)
            {
                var values = individual.IndicatorValues[indicatorIndex];

                if (values.Count == 0)
                {
                    WriteWarning($"Indicator {indicatorIndex + 1} has no values to plot.");
                    continue;
                }

                ConsoleUtilities.WriteLine();
                WriteInfo($"Indicator {indicatorIndex + 1} Values Chart:");

                // Display individual configuration properties
                ConsoleUtilities.WriteLine(
                    $"  Strategy Config: AllowMultipleTrades={individual.AllowMultipleTrades}, " +
                    $"CombinationMethod={individual.CombinationMethod}, " +
                    $"EnsembleVotingThreshold={individual.EnsembleVotingThreshold}");

                const int chartHeight = 20;
                var chartWidth = Math.Min(values.Count, 80); // Limit width for console
                var min = values.Min();
                var max = values.Max();
                if (Math.Abs(max - min) < 1e-10)
                {
                    min -= 1.0;
                    max += 1.0;
                }

                // Use string[,] instead of char[,] for plot
                var plot = new string[chartHeight, chartWidth];

                // Initialize plot with empty strings
                for (var row = 0; row < chartHeight; row++)
                for (var col = 0; col < chartWidth; col++)
                    plot[row, col] = "";

                // Plot indicator values as a graph, each value at its own column
                var step = values.Count > chartWidth ? values.Count / chartWidth : 1;
                for (int i = 0, col = 0; i < values.Count && col < chartWidth; i += step, col++)
                {
                    var value = values[i];
                    var row = (int)Math.Round((value - min) / (max - min) * (chartHeight - 1));
                    row = Math.Max(0, Math.Min(chartHeight - 1, row));
                    var plotStr = ".";

                    // Collect all trade markers for this index
                    if (individual.Trades != null)
                    {
                        var markers = new List<string>();
                        foreach (var trade in individual.Trades)
                        {
                            if (trade.OpenIndex == i)
                            {
                                if (trade.AllowedSecurityType == AllowedSecurityType.Option)
                                    markers.Add(trade.AllowedOptionType == AllowedOptionType.Calls ? "C" : "P");
                                else
                                    markers.Add(trade.AllowedTradeType == AllowedTradeType.Buy ? "B" : "S");
                            }

                            if (trade.CloseIndex == i && trade.AllowedSecurityType == AllowedSecurityType.Option)
                                markers.Add(trade.AllowedOptionType == AllowedOptionType.Calls ? "c" : "p");
                            if (trade.CloseIndex == i && trade.AllowedSecurityType != AllowedSecurityType.Option)
                                markers.Add(trade.AllowedTradeType == AllowedTradeType.Buy ? "b" : "s");
                        }

                        if (markers.Count > 0)
                            plotStr = string.Join("", markers);
                    }

                    plot[chartHeight - 1 - row, col] = plotStr;
                }

                // Display the plot
                ConsoleUtilities.WriteLine(
                    $"  Value Range: {min:F4} - {max:F4} | Data Points: {values.Count} | Chart Width: {chartWidth}");
                ConsoleUtilities.WriteLine("  ┌" + new string('-', chartWidth) + "┐");
                for (var row = 0; row < chartHeight; row++)
                {
                    ConsoleUtilities.Write("  |");
                    for (var col = 0; col < chartWidth; col++)
                    {
                        var s = plot[row, col];
                        foreach (var c in s)
                        {
                            switch (c)
                            {
                                case 'B':
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    break;
                                case 'S':
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    break;
                                case 'b':
                                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                                    break;
                                case 's':
                                    Console.ForegroundColor = ConsoleColor.DarkRed;
                                    break;
                                case 'C':
                                    Console.ForegroundColor = ConsoleColor.Cyan;
                                    break;
                                case 'P':
                                    Console.ForegroundColor = ConsoleColor.Magenta;
                                    break;
                                case 'c':
                                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                                    break;
                                case 'p':
                                    Console.ForegroundColor = ConsoleColor.DarkMagenta;
                                    break;
                                default:
                                    Console.ForegroundColor = ConsoleColor.Gray;
                                    break;
                            }

                            ConsoleUtilities.Write(c);
                            Console.ResetColor();
                        }

                        if (s.Length == 0) ConsoleUtilities.Write(' ');
                    }

                    ConsoleUtilities.WriteLine("|");
                }

                ConsoleUtilities.WriteLine("  └" + new string('-', chartWidth) + "┘");

                // Display indicator configuration if available
                if (individual.Indicators != null && indicatorIndex < individual.Indicators.Count)
                {
                    var indicator = individual.Indicators[indicatorIndex];
                    // Print ALL indicator parameters
                    ConsoleUtilities.WriteLine(
                        $"  Config: Type={indicator.Type}, Period={indicator.Period}, Mode={indicator.Mode}, Timeframe={indicator.TimeFrame}, OHLC={indicator.OHLC}, Polarity={indicator.Polarity}, FastMA={indicator.FastMAPeriod}, SlowMA={indicator.SlowMAPeriod}, LongThr={indicator.LongThreshold:F4}, ShortThr={indicator.ShortThreshold:F4}, Param1={indicator.Param1:F4}, Param2={indicator.Param2:F4}, Param3={indicator.Param3:F4}, Param4={indicator.Param4:F4}, Param5={indicator.Param5:F4}, Debug={indicator.DebugCase}, TradeMode={indicator.TradeMode}");
                }
            }

            ConsoleUtilities.WriteLine();
            ConsoleUtilities.WriteLine(
                "  Legend: . = Indicator Value, B = Buy, S = SellShort, b = Sell (close buy), s = Cover (close short), C = Call Option, P = Put Option, c = Close Call, p = Close Put");
        }

        /// <summary>
        /// SPY vs per-indicator equity curves (%). 
        /// SPY shown with '.', each indicator with a distinct symbol/color.
        /// </summary>
        public static void DisplayEquityVsSpyOverview(
            PriceRecord[] allPriceRecords,
            GeneticIndividual mergedWalkforwardIndividual,
            int chartWidth = 120,
            int chartHeight = 20,
            bool includePerIndicator = true,
            string allocationMode = "Independent") // Independent | Split
        {
            try
            {
                if (allPriceRecords == null || allPriceRecords.Length < 2)
                {
                    WriteWarning("Insufficient price data for SPY/equity overview plot.");
                    return;
                }
                if (mergedWalkforwardIndividual == null)
                {
                    WriteWarning("No individual available for equity overview plot.");
                    return;
                }

                // 1. Build SPY cumulative % series
                var closes = allPriceRecords.Select(p => p.Close).ToArray();
                var basePrice = closes[0];
                var spyPct = new double[closes.Length];
                for (int i = 0; i < closes.Length; i++)
                    spyPct[i] = (closes[i] / basePrice - 1.0) * 100.0;

                // 2. Collect trades by indicator
                var trades = mergedWalkforwardIndividual.Trades ?? new List<TradeResult>();
                var byIndicator = new Dictionary<int, List<TradeResult>>();
                foreach (var t in trades)
                {
                    if (t.CloseIndex <= t.OpenIndex) continue;
                    int key = t.ResponsibleIndicatorIndex;
                    if (!byIndicator.ContainsKey(key))
                        byIndicator[key] = new List<TradeResult>();
                    byIndicator[key].Add(t);
                }

                // If no per-indicator breakdown or disabled, fallback to original single equity
                if (!includePerIndicator || byIndicator.Count == 0)
                {
                    // Original single equity logic (unchanged)
                    var startBal = mergedWalkforwardIndividual.StartingBalance > 0
                        ? mergedWalkforwardIndividual.StartingBalance
                        : StartingBalance;

                    var equityPctSingle = new double[closes.Length];
                    double lastBal = startBal;
                    var byCloseIndex = new Dictionary<int, double>();
                    foreach (var t in trades)
                    {
                        var idx = Math.Max(0, Math.Min(t.CloseIndex, equityPctSingle.Length - 1));
                        byCloseIndex[idx] = t.Balance;
                    }
                    for (int i = 0; i < equityPctSingle.Length; i++)
                    {
                        if (byCloseIndex.ContainsKey(i))
                            lastBal = byCloseIndex[i];
                        equityPctSingle[i] = (lastBal / startBal - 1.0) * 100.0;
                    }
                    PlotComposite(new[] { ("SPY", spyPct, '.'), ("Equity", equityPctSingle, 'G') }, chartWidth, chartHeight);
                    return;
                }

                // 3. Determine starting balance allocation
                double masterStart = mergedWalkforwardIndividual.StartingBalance > 0
                    ? mergedWalkforwardIndividual.StartingBalance
                    : StartingBalance;

                int indicatorCurveCount = byIndicator.Keys.Count;
                double perIndicatorStart = masterStart;

                if (string.Equals(allocationMode, "Split", StringComparison.OrdinalIgnoreCase) && indicatorCurveCount > 0)
                    perIndicatorStart = masterStart / indicatorCurveCount;

                // 4. Build per-indicator equity % curves
                var seriesList = new List<(string name, double[] pct, char symbol)>();

                // Always add SPY first
                seriesList.Add(("SPY", spyPct, '.'));

                // Sorted keys for deterministic legend (-1 combined first)
                var sortedKeys = byIndicator.Keys.OrderBy(k => k).ToList();

                int curveIdx = 0;
                foreach (var key in sortedKeys)
                {
                    var list = byIndicator[key].OrderBy(t => t.CloseIndex).ToList();
                    var eq = new double[closes.Length];
                    double startBal = perIndicatorStart;
                    double bal = startBal;

                    // Recompute equity curve for that indicator ONLY from its trades
                    int tradePtr = 0;
                    for (int i = 0; i < eq.Length; i++)
                    {
                        while (tradePtr < list.Count && list[tradePtr].CloseIndex == i)
                        {
                            bal = list[tradePtr].Balance; // Balance stored after close (already includes P&L relative to global run)
                            tradePtr++;
                        }
                        // If stored Balance is global, we can still show relative % vs starting sub-balance (shape ok).
                        eq[i] = (bal / startBal - 1.0) * 100.0;
                    }

                    char sym;
                    if (key < 0) sym = 'C'; // Combined
                    else if (indicatorCurveCount <= 10)
                        sym = (char)('0' + Math.Min(key, 9));
                    else
                    {
                        // After 10 use letters
                        sym = (char)('A' + (curveIdx % 26));
                    }

                    string name = key < 0 ? "Combined" : $"Ind{key}";
                    seriesList.Add((name, eq, sym));
                    curveIdx++;
                }

                // 5. Plot all series
                PlotComposite(seriesList.ToArray(), chartWidth, chartHeight);
            }
            catch (Exception ex)
            {
                WriteWarning($"Failed to plot SPY vs per-indicator equity overview: {ex.Message}");
            }
        }

        // Helper: composite ASCII plot
        private static void PlotComposite((string name, double[] pct, char symbol)[] series,
            int chartWidth, int chartHeight)
        {
            if (series == null || series.Length == 0) return;

            // Determine global min/max
            double globalMin = double.MaxValue;
            double globalMax = double.MinValue;
            int length = series[0].pct.Length;
            foreach (var s in series)
            {
                if (s.pct.Length != length) return; // inconsistent length safeguard
                var min = s.pct.Min();
                var max = s.pct.Max();
                if (min < globalMin) globalMin = min;
                if (max > globalMax) globalMax = max;
            }
            if (Math.Abs(globalMax - globalMin) < 1e-9)
            {
                globalMin -= 1.0;
                globalMax += 1.0;
            }

            chartWidth = Math.Max(40, Math.Min(chartWidth, 160));
            chartHeight = Math.Max(10, Math.Min(chartHeight, 30));

            var plot = new string[chartHeight, chartWidth];
            for (int y = 0; y < chartHeight; y++)
                for (int x = 0; x < chartWidth; x++)
                    plot[y, x] = string.Empty;

            int n = length;
            int step = Math.Max(1, n / chartWidth);

            for (int col = 0, i = 0; i < n && col < chartWidth; i += step, col++)
            {
                foreach (var s in series)
                {
                    var val = s.pct[i];
                    int row = (int)Math.Round((val - globalMin) / (globalMax - globalMin) * (chartHeight - 1));
                    row = Math.Max(0, Math.Min(chartHeight - 1, row));
                    plot[chartHeight - 1 - row, col] += s.symbol;
                }
            }

            ConsoleUtilities.WriteLine();
            WriteSection("SPY vs Equity Curves (Per Indicator)");
            ConsoleUtilities.WriteLine(
                $"  Range: {globalMin:F1}% to {globalMax:F1}% | Points: {n} | Width: {chartWidth} | Height: {chartHeight}");

            ConsoleUtilities.WriteLine("  ┌" + new string('-', chartWidth) + "┐");
            for (int r = 0; r < chartHeight; r++)
            {
                ConsoleUtilities.Write("  |");
                for (int c = 0; c < chartWidth; c++)
                {
                    var cell = plot[r, c];
                    if (string.IsNullOrEmpty(cell))
                    {
                        ConsoleUtilities.Write(' ');
                        continue;
                    }

                    foreach (var ch in cell)
                    {
                        SetSeriesColor(ch);
                        ConsoleUtilities.Write(ch);
                        Console.ResetColor();
                    }
                }
                ConsoleUtilities.WriteLine("|");
            }
            ConsoleUtilities.WriteLine("  └" + new string('-', chartWidth) + "┘");

            // Legend
            ConsoleUtilities.WriteLine("  Legend:");
            foreach (var s in series)
            {
                SetSeriesColor(s.symbol);
                ConsoleUtilities.Write($"    {s.symbol} ");
                Console.ResetColor();
                ConsoleUtilities.WriteLine($"= {s.name}");
            }
        }

        // Assign distinct colors based on symbol
        private static void SetSeriesColor(char symbol)
        {
            switch (symbol)
            {
                case '.': Console.ForegroundColor = ConsoleColor.White; break; // SPY
                case 'G': Console.ForegroundColor = ConsoleColor.Green; break; // legacy single equity
                case '0': Console.ForegroundColor = ConsoleColor.Green; break;
                case '1': Console.ForegroundColor = ConsoleColor.Cyan; break;
                case '2': Console.ForegroundColor = ConsoleColor.Yellow; break;
                case '3': Console.ForegroundColor = ConsoleColor.Magenta; break;
                case '4': Console.ForegroundColor = ConsoleColor.Blue; break;
                case '5': Console.ForegroundColor = ConsoleColor.DarkGreen; break;
                case '6': Console.ForegroundColor = ConsoleColor.DarkCyan; break;
                case '7': Console.ForegroundColor = ConsoleColor.DarkYellow; break;
                case '8': Console.ForegroundColor = ConsoleColor.DarkMagenta; break;
                case '9': Console.ForegroundColor = ConsoleColor.DarkBlue; break;
                case 'C': Console.ForegroundColor = ConsoleColor.DarkGray; break; // Combined
                default:
                    if (symbol >= 'A' && symbol <= 'Z')
                    {
                        // Cycle through a few colors for extended indicators
                        var colors = new[]
                        {
                            ConsoleColor.Green, ConsoleColor.Cyan, ConsoleColor.Yellow,
                            ConsoleColor.Magenta, ConsoleColor.Blue, ConsoleColor.DarkGreen,
                            ConsoleColor.DarkCyan, ConsoleColor.DarkYellow, ConsoleColor.DarkMagenta,
                            ConsoleColor.DarkBlue
                        };
                        int idx = (symbol - 'A') % colors.Length;
                        Console.ForegroundColor = colors[idx];
                    }
                    else
                        Console.ForegroundColor = ConsoleColor.Gray;
                    break;
            }
        }

        /// <summary>
        ///     Displays ASCII visualizations of trading strategies.
        /// </summary>
        /// <param name="baseIndividual">Base indicator individual</param>
        /// <param name="bestIndividual">Best individual from genetic algorithm</param>
        private static void DisplayVisualizations(GeneticIndividual baseIndividual,
            GeneticIndividual bestIndividual)
        {
            WriteSection("Strategy Visualizations");

            if (baseIndividual != null)
            {
                WriteInfo("Base Individual Trading Strategy:");
                Plot(baseIndividual);
            }

            if (bestIndividual != null)
            {
                ConsoleUtilities.WriteLine();
                WriteInfo("Best Individual Trading Strategy:");
                Plot(bestIndividual);
            }
        }

        #endregion
    }
}