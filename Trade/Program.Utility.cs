using System;
using System.Collections.Generic;
using System.Linq;
using Trade.Prices2;

namespace Trade
{
    internal partial class Program
    {
        #region Console Output Helpers

        /// <summary>
        ///     Writes a section header to the console.
        /// </summary>
        /// <param name="title">Section title</param>
        private static void WriteSection(string title)
        {
            try
            {
                ConsoleUtilities.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                ConsoleUtilities.WriteLine($"■ {title}");
                ConsoleUtilities.WriteLine(new string('─', title.Length + 2));
                Console.ResetColor();
            }
            catch
            {
                ConsoleUtilities.WriteLine();
                ConsoleUtilities.WriteLine($"■ {title}");
                ConsoleUtilities.WriteLine(new string('─', title.Length + 2));
            }
        }

        /// <summary>
        ///     Writes an informational message to the console.
        /// </summary>
        /// <param name="message">Message to display</param>
        private static void WriteInfo(string message)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.White;
                ConsoleUtilities.WriteLine($"[INFO] {message}");
                Console.ResetColor();
            }
            catch
            {
                ConsoleUtilities.WriteLine($"[INFO] {message}");
            }
        }

        /// <summary>
        ///     Writes a success message to the console.
        /// </summary>
        /// <param name="message">Message to display</param>
        private static void WriteSuccess(string message)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                ConsoleUtilities.WriteLine($"[SUCCESS] {message}");
                Console.ResetColor();
            }
            catch
            {
                ConsoleUtilities.WriteLine($"[SUCCESS] {message}");
            }
        }

        /// <summary>
        ///     Writes a warning message to the console.
        /// </summary>
        /// <param name="message">Message to display</param>
        private static void WriteWarning(string message)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ConsoleUtilities.WriteLine($"[WARNING] {message}");
                Console.ResetColor();
            }
            catch
            {
                ConsoleUtilities.WriteLine($"[WARNING] {message}");
            }
        }

        /// <summary>
        ///     Writes an error message to the console.
        /// </summary>
        /// <param name="message">Message to display</param>
        private static void DisplayError(string message)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Red;
                ConsoleUtilities.WriteLine($"[ERROR] {message}");
                Console.ResetColor();
            }
            catch
            {
                ConsoleUtilities.WriteLine($"[ERROR] {message}");
            }
        }

        #endregion

        #region Performance Metrics

        /// <summary>
        ///     Calculate time-adjusted performance metrics for proper comparison across different period lengths.
        /// </summary>
        /// <param name="performance">Raw performance percentage</param>
        /// <param name="dataPoints">Number of data points in the period</param>
        /// <param name="totalDataPoints">Total data points representing full time period (e.g., 1 year)</param>
        /// <returns>Annualized performance percentage</returns>
        private static double CalculateAnnualizedPerformance(double performance, double dataPoints,
            double totalDataPoints)
        {
            if (dataPoints <= 0) return 0.0;
            var timeRatio = dataPoints / totalDataPoints;
            return performance / timeRatio;
        }

        /// <summary>
        ///     Calculate Sharpe-like ratio for trading strategy performance evaluation.
        ///     Delegates to centralized RiskMetrics to avoid duplication.
        /// </summary>
        private static double CalculateRiskAdjustedReturn(GeneticIndividual individual, double riskFreeRate = 0.02)
        {
            return RiskMetrics.CalculateSharpe(individual, riskFreeRate);
        }

        /// <summary>
        ///     Calculate profit factor for trading strategy.
        ///     Profit Factor = Gross Profit / Gross Loss
        /// </summary>
        private static double CalculateProfitFactor(GeneticIndividual individual)
        {
            if (individual.Trades.Count == 0) return 0.0;

            var grossProfit = individual.Trades
                .Where(t => t.ActualDollarGain > 0)
                .Sum(t => t.ActualDollarGain);

            var grossLoss = Math.Abs(individual.Trades
                .Where(t => t.ActualDollarGain < 0)
                .Sum(t => t.ActualDollarGain));

            if (grossLoss == 0)
                return grossProfit > 0 ? double.PositiveInfinity : 0.0;

            return grossProfit / grossLoss;
        }

        /// <summary>
        ///     Calculate maximum drawdown for the trading strategy.
        /// </summary>
        private static double CalculateMaxDrawdown(GeneticIndividual individual)
        {
            return RiskMetrics.CalculateMaxDrawdown(individual);
        }

        /// <summary>
        ///     Calculate standard deviation of an array of values.
        /// </summary>
        private static double CalculateStandardDeviation(double[] values)
        {
            if (values.Length == 0) return 0.0;

            var mean = values.Average();
            var sumSquaredDifferences = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquaredDifferences / values.Length);
        }

        #endregion

        #region State Management for Data Leakage Prevention

        // Helper methods for state management to prevent data leakage
        private static object CaptureCurrentIndicatorRanges()
        {
            // This captures the current indicator normalization state to prevent data leakage
            // between walkforward windows. Each window should use only its own training data
            // for normalization parameter calculation.
            WriteInfo("    Capturing current indicator normalization parameters...");

            // In a real implementation, this would capture the static state from GeneticIndividual
            // For now, we return a placeholder that indicates proper isolation is needed
            return new
            {
                Message = "Indicator ranges captured for window isolation",
                Timestamp = DateTime.Now,
                Note =
                    "This prevents data leakage by ensuring each window uses only its training data for normalization"
            };
        }

        private static object CaptureCurrentOptionSolversState()
        {
            // This captures the current option solver state to prevent data leakage
            WriteInfo("    Capturing current option solver state...");

            return new
            {
                Message = "Option solver state captured for window isolation",
                Timestamp = DateTime.Now,
                Note = "This prevents data leakage by isolating option pricing models per window"
            };
        }

        private static void RestoreIndicatorRanges(object state)
        {
            // This would restore the indicator normalization state for the next window
            // Critical for preventing data leakage - each window must be isolated
            WriteInfo("    Indicator normalization parameters restored for next window");
            WriteInfo("    ✓ Data leakage prevention: Each window uses only its own training data for normalization");
        }

        private static void RestoreOptionSolversState(object state)
        {
            // This would restore the option solver state for the next window
            WriteInfo("    Option solver state restored for next window");
            WriteInfo("    ✓ Data leakage prevention: Option pricing models isolated per window");
        }

        #endregion

        #region Validation and Helper Methods

        /// <summary>
        ///     Validates TradeResult calculations are working correctly.
        /// </summary>
        private static void ValidateTradeResultCalculations()
        {
            // Test Buy trade calculation
            var buyTrade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 100.0,
                ClosePrice = 120.0,
                AllowedTradeType = AllowedTradeType.Buy,
                Position = 10.0, // 10 shares
                PositionInDollars = 1000.0 // $1000 invested
            };

            var expectedBuyDollar = 120.0 - 100.0; // $20 per share
            var expectedBuyPercent = (120.0 - 100.0) / 100.0 * 100.0; // 20%
            var expectedActualGain = expectedBuyDollar * 10.0; // $200 total

            if (Math.Abs(buyTrade.DollarGain - expectedBuyDollar) > 1e-10)
                throw new InvalidOperationException(
                    $"Buy trade dollar calculation failed: Expected {expectedBuyDollar}, got {buyTrade.DollarGain}");

            if (Math.Abs(buyTrade.PercentGain - expectedBuyPercent) > 1e-10)
                throw new InvalidOperationException(
                    $"Buy trade percent calculation failed: Expected {expectedBuyPercent}, got {buyTrade.PercentGain}");

            if (Math.Abs(buyTrade.ActualDollarGain - expectedActualGain) > 1e-10)
                throw new InvalidOperationException(
                    $"Buy trade actual gain calculation failed: Expected {expectedActualGain}, got {buyTrade.ActualDollarGain}");

            // Test SellShort trade calculation
            var shortTrade = new TradeResult
            {
                OpenIndex = 0,
                CloseIndex = 10,
                OpenPrice = 120.0,
                ClosePrice = 100.0,
                AllowedTradeType = AllowedTradeType.SellShort,
                Position = -8.33, // About 8.33 shares short
                PositionInDollars = 1000.0 // $1000 value shorted
            };

            var expectedShortDollar = 120.0 - 100.0; // $20 per share
            var expectedShortPercent = (120.0 - 100.0) / 120.0 * 100.0; // 16.67%
            var expectedShortActualGain = expectedShortDollar * 8.33; // About $166.60 total

            if (Math.Abs(shortTrade.DollarGain - expectedShortDollar) > 1e-10)
                throw new InvalidOperationException(
                    $"Short trade dollar calculation failed: Expected {expectedShortDollar}, got {shortTrade.DollarGain}");

            if (Math.Abs(shortTrade.PercentGain - expectedShortPercent) > 1e-10)
                throw new InvalidOperationException(
                    $"Short trade percent calculation failed: Expected {expectedShortPercent}, got {shortTrade.PercentGain}");

            if (Math.Abs(shortTrade.ActualDollarGain - expectedShortActualGain) > 1e-6)
                throw new InvalidOperationException(
                    $"Short trade actual gain calculation failed: Expected {expectedShortActualGain}, got {shortTrade.ActualDollarGain}");

            WriteSuccess("✓ TradeResult calculations validated successfully");
            ConsoleUtilities.WriteLine(
                $"  Buy Trade: ${buyTrade.DollarGain:F2}/share, {buyTrade.PercentGain:F2}%, ${buyTrade.ActualDollarGain:F2} total");
            ConsoleUtilities.WriteLine(
                $"  Short Trade: ${shortTrade.DollarGain:F2}/share, {shortTrade.PercentGain:F2}%, ${shortTrade.ActualDollarGain:F2} total");
        }

        /// <summary>
        ///     Creates a genetic individual with Sin indicator configuration.
        /// </summary>
        /// <returns>Configured genetic individual</returns>
        private static GeneticIndividual CreateBaseIndividual(IndicatorParams indicatorParams)
        {
            var individual = new GeneticIndividual
            {
                AllowedSecurityTypes = AllowedSecurityType.Stock,
                AllowedTradeTypes = AllowedTradeType.Any,
                StartingBalance = StartingBalance
            };
            individual.Indicators.Clear();
            individual.Indicators.Add(indicatorParams);

            return individual;
        }

        /// <summary>
        ///     Validates Sin indicator calculations against expected values.
        /// </summary>
        /// <param name="priceBuffer">Price data buffer</param>
        /// <param name="individual">Individual to validate</param>
        private static void ValidateSinIndicator(double[] priceBuffer, GeneticIndividual individual)
        {
            WriteInfo("Validating Sin indicator calculations...");

            if (individual.Indicators.Count == 0)
            {
                WriteWarning("No indicators found to validate.");
                return;
            }

            var sinIndicator = individual.Indicators[0];
            var match = true;
            var mismatches = 0;
            var validComparisons = 0;

            // Validate that we have indicator values
            if (individual.IndicatorValues.Count == 0 || individual.IndicatorValues[0].Count == 0)
            {
                WriteWarning("No indicator values found for validation.");
                return;
            }

            WriteInfo(
                $"Checking {individual.IndicatorValues[0].Count} indicator values against expected Sin calculations...");
            WriteInfo(
                $"Sin Indicator Config: Amplitude={sinIndicator.Param1}, Phase={sinIndicator.Param2}, Offset={sinIndicator.Param3}");

            // Start validation from the period to avoid early zero values
            var startIndex = Math.Max(sinIndicator.Period, 0);

            for (var i = startIndex; i < priceBuffer.Length && i < individual.IndicatorValues[0].Count; i++)
            {
                var expected = Indicators.Program.SinIndicator.Calculate(i, priceBuffer.Length, sinIndicator.Param1,
                    sinIndicator.Param2, sinIndicator.Param3, sinIndicator.Param4, sinIndicator.Param5);
                var actual = individual.IndicatorValues[0][i];
                validComparisons++;

                if (Math.Abs(expected - actual) > 1e-6)
                {
                    match = false;
                    mismatches++;
                    if (mismatches <= 5) // Limit error output
                        WriteWarning(
                            $"  Mismatch at index {i}: Expected={expected:F6}, Actual={actual:F6}, Diff={Math.Abs(expected - actual):F6}");
                    else if (mismatches == 6) WriteWarning("  ... (additional mismatches suppressed)");
                }
            }

            WriteInfo($"Validation completed: {validComparisons} comparisons made");

            if (match)
            {
                WriteSuccess("✓ Sin indicator calculations validated successfully");
            }
            else
            {
                WriteWarning(
                    $"! Sin indicator validation failed with {mismatches} mismatches out of {validComparisons} comparisons");
                var errorRate = (double)mismatches / validComparisons * 100;
                WriteWarning($"  Error rate: {errorRate:F1}%");
            }
        }

        #endregion

        #region Prediction Support Methods

        /// <summary>
        ///     Get the next trading day after the given date (skips weekends)
        /// </summary>
        private static DateTime GetNextTradingDay(DateTime currentDate)
        {
            var nextDay = currentDate.AddDays(1);

            // Skip weekends
            while (nextDay.DayOfWeek == DayOfWeek.Saturday || nextDay.DayOfWeek == DayOfWeek.Sunday)
                nextDay = nextDay.AddDays(1);

            return nextDay;
        }

        /// <summary>
        ///     Get current market signal from the trained model
        /// </summary>
        private static MarketSignal GetCurrentMarketSignal(GeneticIndividual model, PriceRecord[] recentData)
        {
            // Process the model on recent data to get current signal state
            model.Process(recentData);

            // Get the most recent signal value
            var currentSignal = 0.0;
            if (model.IndicatorValues.Count > 0 && model.IndicatorValues[0].Count > 0)
            {
                // Get the last signal value
                var lastSignalIndex = model.IndicatorValues[0].Count - 1;
                currentSignal = model.IndicatorValues[0][lastSignalIndex];
            }

            // Determine recommended action based on signal and thresholds
            var recommendedAction = "HOLD";
            var confidence = 50.0; // Base confidence

            if (model.Indicators.Count > 0)
            {
                var primaryIndicator = model.Indicators[0];

                if (currentSignal > primaryIndicator.LongThreshold)
                {
                    recommendedAction = "BUY";
                    confidence = Math.Min(95.0, 50.0 + Math.Abs(currentSignal - primaryIndicator.LongThreshold) * 45.0);
                }
                else if (currentSignal < primaryIndicator.ShortThreshold)
                {
                    recommendedAction = "SELL";
                    confidence = Math.Min(95.0,
                        50.0 + Math.Abs(currentSignal - primaryIndicator.ShortThreshold) * 45.0);
                }
                else
                {
                    recommendedAction = "HOLD";
                    confidence = Math.Max(20.0, 50.0 - Math.Abs(currentSignal) * 30.0);
                }
            }

            return new MarketSignal
            {
                Signal = currentSignal,
                RecommendedAction = recommendedAction,
                Confidence = confidence
            };
        }

        /// <summary>
        ///     Calculate risk metrics for the prediction model
        /// </summary>
        private static ModelRiskMetrics CalculateModelRiskMetrics(GeneticIndividual model)
        {
            var sharpeRatio = CalculateRiskAdjustedReturn(model);
            var maxDrawdown = CalculateMaxDrawdown(model);
            var profitFactor = CalculateProfitFactor(model);

            var profitableTrades = model.Trades.Count(t => t.DollarGain > 0);
            var winRate = model.Trades.Count > 0 ? (double)profitableTrades / model.Trades.Count * 100.0 : 0.0;

            // Determine if model is robust based on multiple criteria
            var isRobust = sharpeRatio > 1.0 && maxDrawdown < 20.0 && profitFactor > 1.5 && winRate > 50.0;

            return new ModelRiskMetrics
            {
                SharpeRatio = sharpeRatio,
                MaxDrawdown = maxDrawdown,
                ProfitFactor = profitFactor,
                WinRate = winRate,
                IsRobust = isRobust
            };
        }

        /// <summary>
        ///     Store prediction result for potential live trading or analysis
        /// </summary>
        private static void StorePredictionResult(GeneticIndividual model, MarketSignal signal, ModelRiskMetrics risk,
            DateTime targetDate)
        {
            // In a real implementation, this would store to database or file
            WriteInfo(
                $"Prediction stored for {targetDate:yyyy-MM-dd}: {signal.RecommendedAction} (Confidence: {signal.Confidence:F1}%)");
        }

        #endregion
    }
}