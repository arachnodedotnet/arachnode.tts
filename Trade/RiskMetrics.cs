using System;
using System.Collections.Generic;
using System.Linq;

namespace Trade
{
    /// <summary>
    /// Centralized risk and performance metric calculations to avoid duplication.
    /// OPTIMIZED for performance with reduced algorithmic complexity and eliminated LINQ operations.
    /// </summary>
    internal static class RiskMetrics
    {
        #region Performance Optimization Constants

        // Pre-allocated arrays for statistical calculations to reduce GC pressure
        private static readonly object _cacheLock = new object();
        private static double[] _tempReturnsArray = new double[10000]; // Pre-allocated for return calculations
        
        // Pre-computed constants to avoid repeated calculations
        private static readonly double DefaultTradesPerYear = 252.0; // Assume daily trading
        
        #endregion

        /// <summary>
        /// Calculate Sharpe-like ratio from per-trade returns.
        /// riskFreeRate is annual; internally converted approximately to per-trade basis.
        /// Returns +Infinity if zero variance and positive edge; 0 if no trades or not computable.
        /// OPTIMIZED: Single-pass algorithm eliminates multiple LINQ operations.
        /// </summary>
        public static double CalculateSharpe(GeneticIndividual individual, double riskFreeRate = 0.02)
        {
            if (individual?.Trades == null || individual.Trades.Count == 0)
                return 0.0;

            var tradeCount = individual.Trades.Count;
            
            // OPTIMIZATION: Single pass to calculate mean and variance, eliminates LINQ chains
            double sumReturns = 0.0;
            double sumSquaredReturns = 0.0;
            int validCount = 0;

            // Ensure temp array is large enough
            lock (_cacheLock)
            {
                if (_tempReturnsArray.Length < tradeCount)
                {
                    _tempReturnsArray = new double[Math.Max(tradeCount * 2, 1000)];
                }
            }

            // Single pass: collect valid returns and calculate statistics
            for (int i = 0; i < tradeCount; i++)
            {
                var returnValue = individual.Trades[i].PercentGain / 100.0;
                
                // OPTIMIZATION: Inline validation instead of LINQ Where
                if (!double.IsNaN(returnValue) && !double.IsInfinity(returnValue))
                {
                    _tempReturnsArray[validCount] = returnValue;
                    sumReturns += returnValue;
                    sumSquaredReturns += returnValue * returnValue;
                    validCount++;
                }
            }

            if (validCount == 0) return 0.0;

            var meanReturn = sumReturns / validCount;
            var variance = (sumSquaredReturns / validCount) - (meanReturn * meanReturn);
            var standardDeviation = Math.Sqrt(Math.Max(0.0, variance)); // Ensure non-negative variance

            // Approximate per-trade risk-free rate - OPTIMIZATION: Pre-computed constant
            var rfPerTrade = riskFreeRate / Math.Max(1, validCount);

            if (standardDeviation == 0.0) 
                return meanReturn > rfPerTrade ? double.PositiveInfinity : 0.0;
                
            return (meanReturn - rfPerTrade) / standardDeviation;
        }

        /// <summary>
        /// Calculate maximum drawdown from equity curve inferred from trades.
        /// Prefer Trade.Balance when available; otherwise compound by trade percent.
        /// Returns percentage drawdown.
        /// OPTIMIZED: Single-pass algorithm eliminates O(n log n) sorting operation.
        /// </summary>
        public static double CalculateMaxDrawdown(GeneticIndividual individual)
        {
            if (individual?.Trades == null || individual.Trades.Count == 0)
                return 0.0;

            var tradeCount = individual.Trades.Count;
            double equity = individual.StartingBalance;
            double peak = equity;
            double maxDrawdown = 0.0;

            // OPTIMIZATION: Direct iteration eliminates OrderBy LINQ operation (O(n log n) -> O(n))
            // Process trades in their original order (assumed to be chronological)
            for (int i = 0; i < tradeCount; i++)
            {
                var trade = individual.Trades[i];
                
                // Use Balance if available, otherwise compound by percent gain
                var nextBalance = trade.Balance > 0 ? trade.Balance : equity * (1.0 + trade.PercentGain / 100.0);
                equity = nextBalance;
                
                if (equity > peak)
                {
                    peak = equity;
                }
                else if (peak > 0)
                {
                    var drawdown = (peak - equity) / peak * 100.0;
                    if (drawdown > maxDrawdown) 
                        maxDrawdown = drawdown;
                }
            }

            return maxDrawdown;
        }

        /// <summary>
        /// Calculate CAGR using trade dates when available, else fall back to indices (252 trading days/year).
        /// Returns CAGR in percent.
        /// OPTIMIZED: Single-pass date extraction eliminates multiple LINQ operations.
        /// </summary>
        public static double CalculateCagr(double startingBalance, double finalBalance, IList<TradeResult> trades)
        {
            if (startingBalance <= 0 || finalBalance <= 0 || trades == null || trades.Count == 0)
                return 0.0;

            double years = 0.0;

            try
            {
                // OPTIMIZATION: Single pass to extract date range, eliminates multiple LINQ chains
                DateTime firstDate = DateTime.MinValue;
                DateTime lastDate = DateTime.MinValue;
                int minOpenIndex = int.MaxValue;
                int maxCloseIndex = int.MinValue;
                bool hasValidDates = false;

                var tradeCount = trades.Count;
                for (int i = 0; i < tradeCount; i++)
                {
                    var trade = trades[i];
                    
                    // Check for valid dates
                    if (trade.PriceRecordForOpen?.DateTime != null && trade.PriceRecordForOpen.DateTime != default(DateTime))
                    {
                        var openDate = trade.PriceRecordForOpen.DateTime;
                        if (firstDate == DateTime.MinValue || openDate < firstDate)
                            firstDate = openDate;
                        hasValidDates = true;
                    }
                    
                    if (trade.PriceRecordForClose?.DateTime != null && trade.PriceRecordForClose.DateTime != default(DateTime))
                    {
                        var closeDate = trade.PriceRecordForClose.DateTime;
                        if (lastDate == DateTime.MinValue || closeDate > lastDate)
                            lastDate = closeDate;
                        hasValidDates = true;
                    }
                    
                    // Track indices for fallback calculation
                    if (trade.OpenIndex >= 0)
                    {
                        if (minOpenIndex == int.MaxValue || trade.OpenIndex < minOpenIndex)
                            minOpenIndex = trade.OpenIndex;
                    }
                    
                    if (trade.CloseIndex >= 0)
                    {
                        if (maxCloseIndex == int.MinValue || trade.CloseIndex > maxCloseIndex)
                            maxCloseIndex = trade.CloseIndex;
                    }
                }

                // Calculate years from dates if available
                if (hasValidDates && firstDate != DateTime.MinValue && lastDate != DateTime.MinValue && lastDate > firstDate)
                {
                    years = (lastDate - firstDate).TotalDays / 365.25;
                }

                // Fallback to index-based calculation
                if (years <= 0.0)
                {
                    if (minOpenIndex != int.MaxValue && maxCloseIndex != int.MinValue)
                    {
                        var bars = Math.Max(0, maxCloseIndex - minOpenIndex + 1);
                        if (bars > 0) 
                            years = bars / DefaultTradesPerYear; // Use pre-computed constant
                    }
                }

                if (years <= 0.0) return 0.0;

                var growth = finalBalance / startingBalance;
                if (growth <= 0.0 || double.IsNaN(growth) || double.IsInfinity(growth)) 
                    return 0.0;

                // CAGR calculation with safeguard for minimum time period
                var minYears = 1.0 / DefaultTradesPerYear; // Minimum one trading day
                return (Math.Pow(growth, 1.0 / Math.Max(years, minYears)) - 1.0) * 100.0;
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Calculate Sortino ratio - similar to Sharpe but only considers downside deviation.
        /// OPTIMIZED: Single-pass algorithm for performance.
        /// </summary>
        public static double CalculateSortino(GeneticIndividual individual, double riskFreeRate = 0.02, double targetReturn = 0.0)
        {
            if (individual?.Trades == null || individual.Trades.Count == 0)
                return 0.0;

            var tradeCount = individual.Trades.Count;
            double sumReturns = 0.0;
            double sumDownsideSquaredDiffs = 0.0;
            int validCount = 0;
            int downsideCount = 0;

            // Single pass calculation
            for (int i = 0; i < tradeCount; i++)
            {
                var returnValue = individual.Trades[i].PercentGain / 100.0;
                
                if (!double.IsNaN(returnValue) && !double.IsInfinity(returnValue))
                {
                    sumReturns += returnValue;
                    validCount++;
                    
                    // Only consider returns below target for downside deviation
                    if (returnValue < targetReturn)
                    {
                        var downsideDiff = returnValue - targetReturn;
                        sumDownsideSquaredDiffs += downsideDiff * downsideDiff;
                        downsideCount++;
                    }
                }
            }

            if (validCount == 0) return 0.0;

            var meanReturn = sumReturns / validCount;
            var downsideDeviation = downsideCount > 0 ? Math.Sqrt(sumDownsideSquaredDiffs / downsideCount) : 0.0;
            var rfPerTrade = riskFreeRate / Math.Max(1, validCount);

            if (downsideDeviation == 0.0)
                return meanReturn > rfPerTrade ? double.PositiveInfinity : 0.0;

            return (meanReturn - rfPerTrade) / downsideDeviation;
        }

        /// <summary>
        /// Calculate Calmar ratio (CAGR / Max Drawdown).
        /// OPTIMIZED: Uses optimized CAGR and drawdown calculations.
        /// </summary>
        public static double CalculateCalmar(GeneticIndividual individual, double riskFreeRate = 0.02)
        {
            if (individual?.Trades == null || individual.Trades.Count == 0)
                return 0.0;

            var maxDrawdown = CalculateMaxDrawdown(individual);
            if (maxDrawdown == 0.0) return double.PositiveInfinity; // No drawdown

            var finalBalance = individual.Trades.Count > 0 ? individual.Trades[individual.Trades.Count - 1].Balance : individual.StartingBalance;
            if (finalBalance <= 0) finalBalance = individual.StartingBalance; // Fallback

            var cagr = CalculateCagr(individual.StartingBalance, finalBalance, individual.Trades);
            
            return cagr / maxDrawdown;
        }

        /// <summary>
        /// Calculate win rate (percentage of profitable trades).
        /// OPTIMIZED: Single-pass calculation.
        /// </summary>
        public static double CalculateWinRate(GeneticIndividual individual)
        {
            if (individual?.Trades == null || individual.Trades.Count == 0)
                return 0.0;

            var tradeCount = individual.Trades.Count;
            int winningTrades = 0;

            // Single pass count of winning trades
            for (int i = 0; i < tradeCount; i++)
            {
                if (individual.Trades[i].PercentGain > 0)
                    winningTrades++;
            }

            return (double)winningTrades / tradeCount * 100.0;
        }

        /// <summary>
        /// Calculate average win vs average loss ratio.
        /// OPTIMIZED: Single-pass calculation with early collection.
        /// </summary>
        public static double CalculateWinLossRatio(GeneticIndividual individual)
        {
            if (individual?.Trades == null || individual.Trades.Count == 0)
                return 0.0;

            var tradeCount = individual.Trades.Count;
            double sumWins = 0.0;
            double sumLosses = 0.0;
            int winCount = 0;
            int lossCount = 0;

            // Single pass to calculate win/loss statistics
            for (int i = 0; i < tradeCount; i++)
            {
                var gain = individual.Trades[i].PercentGain;
                if (gain > 0)
                {
                    sumWins += gain;
                    winCount++;
                }
                else if (gain < 0)
                {
                    sumLosses += Math.Abs(gain); // Convert to positive for ratio calculation
                    lossCount++;
                }
            }

            if (winCount == 0 || lossCount == 0) return 0.0;

            var avgWin = sumWins / winCount;
            var avgLoss = sumLosses / lossCount;

            return avgLoss > 0 ? avgWin / avgLoss : double.PositiveInfinity;
        }

        /// <summary>
        /// Calculate profit factor (gross profit / gross loss).
        /// OPTIMIZED: Single-pass calculation.
        /// </summary>
        public static double CalculateProfitFactor(GeneticIndividual individual)
        {
            if (individual?.Trades == null || individual.Trades.Count == 0)
                return 0.0;

            var tradeCount = individual.Trades.Count;
            double grossProfit = 0.0;
            double grossLoss = 0.0;

            // Single pass to calculate gross profit and loss
            for (int i = 0; i < tradeCount; i++)
            {
                var gain = individual.Trades[i].PercentGain;
                if (gain > 0)
                    grossProfit += gain;
                else if (gain < 0)
                    grossLoss += Math.Abs(gain);
            }

            return grossLoss > 0 ? grossProfit / grossLoss : (grossProfit > 0 ? double.PositiveInfinity : 0.0);
        }

        /// <summary>
        /// Calculate comprehensive risk metrics in a single pass for efficiency.
        /// Returns a structure with all common risk metrics calculated together.
        /// OPTIMIZED: Ultimate efficiency by calculating all metrics in a single iteration.
        /// </summary>
        public static RiskMetricsResult CalculateAllMetrics(GeneticIndividual individual, double riskFreeRate = 0.02)
        {
            var result = new RiskMetricsResult();
            
            if (individual?.Trades == null || individual.Trades.Count == 0)
                return result;

            var tradeCount = individual.Trades.Count;
            
            // Ensure temp array is large enough
            lock (_cacheLock)
            {
                if (_tempReturnsArray.Length < tradeCount)
                {
                    _tempReturnsArray = new double[Math.Max(tradeCount * 2, 1000)];
                }
            }

            // Single-pass calculation of ALL metrics for maximum efficiency
            double equity = individual.StartingBalance;
            double peak = equity;
            double maxDrawdown = 0.0;
            double sumReturns = 0.0;
            double sumSquaredReturns = 0.0;
            double sumDownsideSquaredDiffs = 0.0;
            double grossProfit = 0.0;
            double grossLoss = 0.0;
            int validCount = 0;
            int winCount = 0;
            int lossCount = 0;
            int downsideCount = 0;
            DateTime firstDate = DateTime.MinValue;
            DateTime lastDate = DateTime.MinValue;
            int minOpenIndex = int.MaxValue;
            int maxCloseIndex = int.MinValue;
            bool hasValidDates = false;

            for (int i = 0; i < tradeCount; i++)
            {
                var trade = individual.Trades[i];
                var returnValue = trade.PercentGain / 100.0;
                
                // Equity curve and drawdown calculation
                var nextBalance = trade.Balance > 0 ? trade.Balance : equity * (1.0 + returnValue);
                equity = nextBalance;
                
                if (equity > peak)
                {
                    peak = equity;
                }
                else if (peak > 0)
                {
                    var drawdown = (peak - equity) / peak * 100.0;
                    if (drawdown > maxDrawdown) maxDrawdown = drawdown;
                }

                // Return statistics (for Sharpe, Sortino)
                if (!double.IsNaN(returnValue) && !double.IsInfinity(returnValue))
                {
                    _tempReturnsArray[validCount] = returnValue;
                    sumReturns += returnValue;
                    sumSquaredReturns += returnValue * returnValue;
                    validCount++;
                    
                    // Downside deviation (for Sortino)
                    if (returnValue < 0)
                    {
                        sumDownsideSquaredDiffs += returnValue * returnValue;
                        downsideCount++;
                    }
                }

                // Win/Loss statistics
                var percentGain = trade.PercentGain;
                if (percentGain > 0)
                {
                    grossProfit += percentGain;
                    winCount++;
                }
                else if (percentGain < 0)
                {
                    grossLoss += Math.Abs(percentGain);
                    lossCount++;
                }

                // Date range for CAGR
                if (trade.PriceRecordForOpen?.DateTime != null && trade.PriceRecordForOpen.DateTime != default(DateTime))
                {
                    var openDate = trade.PriceRecordForOpen.DateTime;
                    if (firstDate == DateTime.MinValue || openDate < firstDate)
                        firstDate = openDate;
                    hasValidDates = true;
                }
                
                if (trade.PriceRecordForClose?.DateTime != null && trade.PriceRecordForClose.DateTime != default(DateTime))
                {
                    var closeDate = trade.PriceRecordForClose.DateTime;
                    if (lastDate == DateTime.MinValue || closeDate > lastDate)
                        lastDate = closeDate;
                    hasValidDates = true;
                }
                
                // Index range for fallback CAGR
                if (trade.OpenIndex >= 0 && (minOpenIndex == int.MaxValue || trade.OpenIndex < minOpenIndex))
                    minOpenIndex = trade.OpenIndex;
                if (trade.CloseIndex >= 0 && (maxCloseIndex == int.MinValue || trade.CloseIndex > maxCloseIndex))
                    maxCloseIndex = trade.CloseIndex;
            }

            // Calculate final metrics
            result.MaxDrawdown = maxDrawdown;
            result.WinRate = tradeCount > 0 ? (double)winCount / tradeCount * 100.0 : 0.0;
            result.ProfitFactor = grossLoss > 0 ? grossProfit / grossLoss : (grossProfit > 0 ? double.PositiveInfinity : 0.0);

            if (validCount > 0)
            {
                var meanReturn = sumReturns / validCount;
                var variance = (sumSquaredReturns / validCount) - (meanReturn * meanReturn);
                var standardDeviation = Math.Sqrt(Math.Max(0.0, variance));
                var rfPerTrade = riskFreeRate / validCount;

                // Sharpe Ratio
                result.SharpeRatio = standardDeviation == 0.0 ? 
                    (meanReturn > rfPerTrade ? double.PositiveInfinity : 0.0) : 
                    (meanReturn - rfPerTrade) / standardDeviation;

                // Sortino Ratio
                var downsideDeviation = downsideCount > 0 ? Math.Sqrt(sumDownsideSquaredDiffs / downsideCount) : 0.0;
                result.SortinoRatio = downsideDeviation == 0.0 ? 
                    (meanReturn > rfPerTrade ? double.PositiveInfinity : 0.0) : 
                    (meanReturn - rfPerTrade) / downsideDeviation;
            }

            // CAGR calculation
            try
            {
                double years = 0.0;
                if (hasValidDates && firstDate != DateTime.MinValue && lastDate != DateTime.MinValue && lastDate > firstDate)
                {
                    years = (lastDate - firstDate).TotalDays / 365.25;
                }
                else if (minOpenIndex != int.MaxValue && maxCloseIndex != int.MinValue)
                {
                    var bars = Math.Max(0, maxCloseIndex - minOpenIndex + 1);
                    if (bars > 0) years = bars / DefaultTradesPerYear;
                }

                if (years > 0.0)
                {
                    var growth = equity / individual.StartingBalance;
                    if (growth > 0.0 && !double.IsNaN(growth) && !double.IsInfinity(growth))
                    {
                        var minYears = 1.0 / DefaultTradesPerYear;
                        result.CAGR = (Math.Pow(growth, 1.0 / Math.Max(years, minYears)) - 1.0) * 100.0;
                    }
                }
            }
            catch
            {
                result.CAGR = 0.0;
            }

            // Calmar Ratio
            result.CalmarRatio = maxDrawdown > 0 ? result.CAGR / maxDrawdown : double.PositiveInfinity;

            // Win/Loss Ratio
            result.WinLossRatio = winCount > 0 && lossCount > 0 ? 
                (grossProfit / winCount) / (grossLoss / lossCount) : 0.0;

            return result;
        }

        /// <summary>
        /// Structure to hold comprehensive risk metrics calculated in a single pass.
        /// </summary>
        public struct RiskMetricsResult
        {
            public double SharpeRatio;
            public double SortinoRatio;
            public double MaxDrawdown;
            public double CAGR;
            public double CalmarRatio;
            public double WinRate;
            public double WinLossRatio;
            public double ProfitFactor;
        }
    }
}
