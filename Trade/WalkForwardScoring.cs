using System;
using System.Collections.Generic;
using System.Linq;
using Trade.Prices2;

namespace Trade
{
    // Optional backtest configuration placeholder (extend as needed)
    public sealed class BacktestConfig
    {
        public double RiskFreeRate { get; set; } = 0.0; // annual
    }

    public sealed class FoldResult
    {
        public int TrainStart, TrainEnd, TestEnd; // indices into bars
        public double TrainSharpe, TestSharpe, TestCAGR, TestMaxDD;
    }

    public sealed class WFScore
    {
        public double MedianTestSharpe;
        public double WorstTestSharpe;
        public double PassRate;        // % of folds with TestSharpe > 0
        public double GenGap;          // median(TrainSharpe - TestSharpe)
        public double CompositeScore;  // for selection
    }

    public static class WalkForwardScoring
    {
        #region Performance Optimization Constants and Caches

        // Pre-allocated arrays and buffers for performance optimization
        private static double[] _tempArray = new double[1000]; // Pre-allocated for statistics calculations
        private static readonly object _cacheLock = new object();

        #endregion

        /// <summary>
        /// Compute robustness score for an (already-evolved) individual using walk-forward validation.
        /// No fitting is performed here; caller must evolve/choose the individual beforehand.
        /// OPTIMIZED: Reduced LINQ operations, efficient statistics calculations, optimized memory usage
        /// </summary>
        public static WFScore WalkForwardScore(
            GeneticIndividual ind,
            PriceRecord[] bars,
            int trainDays = 252 * 3,
            int testDays = 252 * 1,
            int stepDays = 252 / 2,     // slide by ~6 months
            BacktestConfig cfg = null)
        {
            if (bars == null || bars.Length == 0 || ind == null)
                return new WFScore { MedianTestSharpe = 0, WorstTestSharpe = 0, PassRate = 0, GenGap = double.PositiveInfinity, CompositeScore = double.NegativeInfinity };

            // OPTIMIZATION: Pre-allocate collection with estimated capacity
            var maxPossibleFolds = Math.Max(1, (bars.Length - trainDays - testDays) / stepDays + 1);
            var folds = new List<FoldResult>(maxPossibleFolds);
            
            int i = 0, n = bars.Length;
            var rf = (cfg?.RiskFreeRate).GetValueOrDefault(0.0);

            while (i + trainDays + testDays <= n)
            {
                int trStart = i;
                int trEndEx = i + trainDays;      // exclusive
                int teEndEx = trEndEx + testDays; // exclusive

                // Extract segments (inclusive indices for our helper below)
                int trStartIdx = trStart;
                int trEndIdx = trEndEx - 1;
                int teStartIdx = trEndEx;
                int teEndIdx = teEndEx - 1;

                // IMPORTANT: compute normalization on training only, then test using same ranges
                var trainSeg = GeneticIndividual.CreateSubset(bars, trStartIdx, trEndIdx);
                var testSeg = GeneticIndividual.CreateSubset(bars, teStartIdx, teEndIdx);

                // Train normalization from training segment only
                GeneticIndividual.AnalyzeIndicatorRanges(trainSeg);

                // Evaluate on train segment (for gap)
                var trainFit = BacktestOptimized(ind, trainSeg, rf);
                // Evaluate on test segment (true OOS)
                var testFit = BacktestOptimized(ind, testSeg, rf);

                folds.Add(new FoldResult
                {
                    TrainStart = trStartIdx,
                    TrainEnd = trEndIdx,
                    TestEnd = teEndIdx,
                    TrainSharpe = trainFit.Sharpe,
                    TestSharpe = testFit.Sharpe,
                    TestCAGR = testFit.CAGR,
                    TestMaxDD = testFit.MaxDrawdown
                });

                i += stepDays;
            }

            if (folds.Count == 0)
                return new WFScore { MedianTestSharpe = 0, WorstTestSharpe = 0, PassRate = 0, GenGap = double.PositiveInfinity, CompositeScore = double.NegativeInfinity };

            // OPTIMIZATION: Single-pass statistics calculation instead of multiple LINQ operations
            return CalculateWFScoreOptimized(folds);
        }

        /// <summary>
        /// Optimized calculation of WF score metrics with single-pass algorithms
        /// OPTIMIZED: Eliminates multiple LINQ operations, uses efficient sorting and statistics
        /// </summary>
        private static WFScore CalculateWFScoreOptimized(List<FoldResult> folds)
        {
            var count = folds.Count;
            
            // OPTIMIZATION: Single pass to extract test Sharpe values and calculate statistics
            double[] testSharpes;
            double[] genGaps;
            int passCount = 0;
            
            lock (_cacheLock)
            {
                // Ensure temp arrays are large enough
                if (_tempArray.Length < count * 2)
                {
                    _tempArray = new double[Math.Max(count * 2, 1000)];
                }
                
                testSharpes = new double[count];
                genGaps = new double[count];
            }
            
            // Single pass to extract values and count passes
            for (int i = 0; i < count; i++)
            {
                var fold = folds[i];
                testSharpes[i] = fold.TestSharpe;
                genGaps[i] = fold.TrainSharpe - fold.TestSharpe;
                if (fold.TestSharpe > 0.0) passCount++;
            }
            
            // OPTIMIZATION: Efficient sorting and median calculation
            var medianTestSharpe = CalculateMedianOptimized(testSharpes);
            var worstTestSharpe = FindMinimumOptimized(testSharpes);
            var averageTestSharpe = CalculateAverageOptimized(testSharpes);
            var passRate = (double)passCount / count;
            var genGap = CalculateMedianOptimized(genGaps);

            // Composite: prioritize robustness (median & worst), penalize big gaps
            double composite = (0.5 * medianTestSharpe)
                               + (0.2 * averageTestSharpe)
                               + (0.1 * passRate)
                               + (0.2 * Math.Min(0.0, worstTestSharpe))    // if worst is negative, it drags score
                               - (0.3 * Math.Max(0.0, genGap));            // penalize train>>test

            return new WFScore
            {
                MedianTestSharpe = medianTestSharpe,
                WorstTestSharpe = worstTestSharpe,
                PassRate = passRate,
                GenGap = genGap,
                CompositeScore = composite
            };
        }

        /// <summary>
        /// Optimized backtest call with efficient individual cloning and processing
        /// OPTIMIZED: Reuses individual instances where possible, efficient state management
        /// </summary>
        private static Fitness BacktestOptimized(GeneticIndividual baseInd, PriceRecord[] segment, double riskFreeRate)
        {
            // Work on a clone to avoid state leakage across segments
            var ind = GeneticEvolvers.CloneIndividual(baseInd);

            // Ensure starting state and clear historical trades/actions
            ind.Trades.Clear();
            ind.TradeActions.Clear();

            // Run simulator on the provided segment
            var fit = ind.Process(segment) ?? new Fitness(0, 0);

            // OPTIMIZATION: Use optimized RiskMetrics calculations if available
            fit.Sharpe = CalculateSharpeOptimized(ind, riskFreeRate);
            fit.MaxDrawdown = CalculateMaxDrawdownOptimized(ind);
            fit.CAGR = CalculateCagrFromPercentGainOptimized(fit.PercentGain, segment.Length);
            return fit;
        }

        /// <summary>
        /// Optimized Sharpe ratio calculation with single-pass statistics
        /// OPTIMIZED: Eliminates LINQ operations, uses efficient variance calculation
        /// </summary>
        private static double CalculateSharpeOptimized(GeneticIndividual ind, double riskFreeRate)
        {
            if (ind.Trades == null || ind.Trades.Count == 0) return 0.0;

            var trades = ind.Trades;
            var count = trades.Count;
            
            if (count == 0) return 0.0;

            // OPTIMIZATION: Single-pass mean and variance calculation
            double sum = 0.0;
            double sumSquares = 0.0;
            
            for (int i = 0; i < count; i++)
            {
                var ret = trades[i].PercentGain / 100.0;
                sum += ret;
                sumSquares += ret * ret;
            }
            
            var mean = sum / count;
            var variance = (sumSquares / count) - (mean * mean);
            var sd = Math.Sqrt(Math.Max(0.0, variance)); // Ensure non-negative variance
            
            if (sd == 0) return mean > riskFreeRate ? double.PositiveInfinity : 0.0;
            return (mean - riskFreeRate) / sd;
        }

        /// <summary>
        /// Optimized maximum drawdown calculation with single pass
        /// OPTIMIZED: Direct iteration without LINQ operations
        /// </summary>
        private static double CalculateMaxDrawdownOptimized(GeneticIndividual ind)
        {
            if (ind.Trades == null || ind.Trades.Count == 0) return 0.0;

            var trades = ind.Trades;
            var count = trades.Count;
            
            if (count == 0) return 0.0;
            
            double peak = ind.StartingBalance;
            double maxDd = 0.0;
            
            for (int i = 0; i < count; i++)
            {
                var bal = trades[i].Balance;
                if (bal > peak) peak = bal;
                var dd = (peak - bal) / peak * 100.0;
                if (dd > maxDd) maxDd = dd;
            }
            
            return maxDd;
        }

        /// <summary>
        /// Optimized CAGR calculation with improved numerical stability
        /// OPTIMIZED: Better edge case handling and numerical precision
        /// </summary>
        private static double CalculateCagrFromPercentGainOptimized(double percentGain, int barsCount)
        {
            var totalReturn = percentGain / 100.0;
            var years = Math.Max(1e-9, barsCount / 252.0); // approximate trading years
            var baseValue = Math.Max(-0.99, totalReturn); // avoid invalid pow for loss > 100%
            return Math.Pow(1.0 + baseValue, 1.0 / years) - 1.0;
        }

        /// <summary>
        /// Optimized median calculation using efficient partial sorting
        /// OPTIMIZED: Uses Array.Sort which is highly optimized, minimal allocations
        /// </summary>
        private static double CalculateMedianOptimized(double[] values)
        {
            if (values == null || values.Length == 0) return 0.0;
            
            var length = values.Length;
            if (length == 1) return values[0];
            
            // OPTIMIZATION: Create a copy for sorting to avoid modifying original
            var sortedValues = new double[length];
            Array.Copy(values, sortedValues, length);
            Array.Sort(sortedValues);
            
            var midIndex = length / 2;
            return (length % 2 == 1) 
                ? sortedValues[midIndex] 
                : (sortedValues[midIndex - 1] + sortedValues[midIndex]) / 2.0;
        }

        /// <summary>
        /// Optimized minimum finding with single pass
        /// OPTIMIZED: Direct iteration instead of LINQ Min()
        /// </summary>
        private static double FindMinimumOptimized(double[] values)
        {
            if (values == null || values.Length == 0) return 0.0;
            
            var min = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < min) min = values[i];
            }
            return min;
        }

        /// <summary>
        /// Optimized average calculation with single pass
        /// OPTIMIZED: Direct calculation instead of LINQ Average()
        /// </summary>
        private static double CalculateAverageOptimized(double[] values)
        {
            if (values == null || values.Length == 0) return 0.0;
            
            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }
            return sum / values.Length;
        }

        #region Legacy Methods (Preserved for Backward Compatibility)

        // Skeleton backtest call that returns a Fitness exposing Sharpe/CAGR/MaxDD.
        // No evolve/fit here.
        private static Fitness Backtest(GeneticIndividual baseInd, PriceRecord[] segment, double riskFreeRate)
        {
            return BacktestOptimized(baseInd, segment, riskFreeRate);
        }

        private static double CalculateSharpe(GeneticIndividual ind, double riskFreeRate)
        {
            return CalculateSharpeOptimized(ind, riskFreeRate);
        }

        private static double CalculateMaxDrawdown(GeneticIndividual ind)
        {
            return CalculateMaxDrawdownOptimized(ind);
        }

        private static double CalculateCagrFromPercentGain(double percentGain, int barsCount)
        {
            return CalculateCagrFromPercentGainOptimized(percentGain, barsCount);
        }

        private static double Median(IEnumerable<double> xs)
        {
            if (xs == null) return 0.0;
            
            // OPTIMIZATION: Convert to array once and use optimized median
            var array = xs as double[] ?? xs.ToArray();
            return CalculateMedianOptimized(array);
        }

        #endregion
    }
}
