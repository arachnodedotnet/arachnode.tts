using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;
using System.Text;

namespace Trade.Tests
{
    [TestClass]
    public class MarkovPatternAnalysisTests
    {
        public TestContext TestContext { get; set; }

        // Shared price data loaded once per class to avoid repeated disk I/O
        private static Prices _sharedPrices;
        private static PriceRecord[] _sharedDaily;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            _sharedPrices = new Prices(Constants.SPX_JSON);
            _sharedDaily = _sharedPrices.GetDailyPriceRecords();
            if (_sharedDaily == null || _sharedDaily.Length == 0)
                Assert.Inconclusive("No daily price records loaded from Constants.SPX_JSON (ClassInit)");
        }

        /// <summary>
        /// Represents a discrete price movement state for Markov analysis
        /// IMPROVED: 3-state model to reduce overfitting and increase pattern robustness
        /// </summary>
        public enum MarketState
        {
            Down = 0,      // < -0.3% (more conservative threshold)
            Flat = 1,      // -0.3% to +0.3%
            Up = 2         // > +0.3%
        }

        /// <summary>
        /// Enhanced market context for better predictions
        /// </summary>
        public class MarketContext
        {
            public double Volatility { get; set; }           // 20-day volatility
            public double Trend { get; set; }                // 50-day SMA slope
            public double RelativeVolume { get; set; }       // Volume vs 20-day average
            public string TrendRegime { get; set; }          // "Uptrend", "Downtrend", "Sideways"
            public double MomentumScore { get; set; }        // RSI-like momentum (0-100)
            
            // NEW: Additional predictive features
            public double VolumeProfile { get; set; }        // Cumulative volume distribution
            public double VolatilityRank { get; set; }       // Current vol percentile (0-1)
            public double IntradayMomentum { get; set; }     // Open to close momentum
            public double GapSize { get; set; }              // Overnight gap percentage
            public double ShortTermTrend { get; set; }       // 5-day vs 20-day SMA
            public double RecentMaxDrawdown { get; set; }    // Max drawdown in last 10 days
            public int ConsecutiveDays { get; set; }         // Consecutive up/down days
            public double ATR { get; set; }                  // Average True Range (14-day)
        }

        /// <summary>
        /// Represents a pattern sequence with enhanced statistical properties
        /// </summary>
        public class MarkovPattern
        {
            public string PatternKey { get; set; }
            public List<MarketState> StateSequence { get; set; }
            public Dictionary<MarketState, int> NextStateOccurrences { get; set; }
            public Dictionary<MarketState, double> NextStateProbabilities { get; set; }
            public int TotalOccurrences { get; set; }
            public double ConfidenceScore { get; set; }
            public MarketContext AverageContext { get; set; }  // Average market conditions for this pattern
            
            public MarkovPattern()
            {
                StateSequence = new List<MarketState>();
                NextStateOccurrences = new Dictionary<MarketState, int>();
                NextStateProbabilities = new Dictionary<MarketState, double>();
                AverageContext = new MarketContext();
            }

            public override string ToString()
            {
                var states = string.Join("→", StateSequence.Select(s => s.ToString().Substring(0, 1)));
                var topNext = NextStateProbabilities.OrderByDescending(kvp => kvp.Value).FirstOrDefault();
                return $"{states} → {topNext.Key}({topNext.Value:P1}) [n={TotalOccurrences}, conf={ConfidenceScore:F3}, vol={AverageContext.Volatility:F3}]";
            }
        }

        /// <summary>
        /// Enhanced Markov chain with market context awareness and better statistical properties
        /// </summary>
        public class EnhancedMarkovChain
        {
            private readonly Dictionary<string, MarkovPattern> _patterns;
            private readonly int _order;
            private readonly double _minSampleSize = 15;  // Require at least 15 occurrences for reliability
            
            // NEW: Regime-specific pattern storage for better predictions
            private readonly Dictionary<string, Dictionary<string, MarkovPattern>> _regimePatterns;

            public EnhancedMarkovChain(int order = 2)  // REDUCED order from 3-4 to 2 for better generalization
            {
                _order = order;
                _patterns = new Dictionary<string, MarkovPattern>();
                _regimePatterns = new Dictionary<string, Dictionary<string, MarkovPattern>>
                {
                    ["HighVol"] = new Dictionary<string, MarkovPattern>(),
                    ["LowVol"] = new Dictionary<string, MarkovPattern>(),
                    ["Uptrend"] = new Dictionary<string, MarkovPattern>(),
                    ["Downtrend"] = new Dictionary<string, MarkovPattern>(),
                    ["Sideways"] = new Dictionary<string, MarkovPattern>()
                };
            }

            public MarketState ClassifyMove(double percentChange)
            {
                // IMPROVED: More conservative thresholds to reduce noise
                // Using <= and >= to include boundary values in the directional states
                if (percentChange <= -0.3) return MarketState.Down;
                if (percentChange >= 0.3) return MarketState.Up;
                return MarketState.Flat;
            }

            public void TrainWithContext(PriceRecord[] dailyRecords, DateTime walkForwardCutoff)
            {
                var trainingData = dailyRecords
                    .Where(r => r.DateTime.Date < walkForwardCutoff.Date)
                    .OrderBy(r => r.DateTime.Date)
                    .ToArray();

                if (trainingData.Length < _order + 50)  // Need more data for statistical reliability
                    throw new ArgumentException("Insufficient training data for robust Markov analysis");

                // Calculate price changes and market context
                var dailyChanges = new List<double>();
                var marketContexts = new List<MarketContext>();
                
                for (int i = 1; i < trainingData.Length; i++)
                {
                    var pctChange = (trainingData[i].Close - trainingData[i - 1].Close) / trainingData[i - 1].Close * 100.0;
                    dailyChanges.Add(pctChange);
                    
                    // Calculate market context for this day
                    var context = CalculateMarketContext(trainingData, i);
                    marketContexts.Add(context);
                }

                // Convert to market states
                var marketStates = dailyChanges.Select(ClassifyMove).ToArray();

                // Build Markov patterns with context
                for (int i = 0; i <= marketStates.Length - _order - 1; i++)
                {
                    var pattern = marketStates.Skip(i).Take(_order).ToArray();
                    var nextState = marketStates[i + _order];
                    var currentContext = marketContexts[i + _order - 1];

                    var patternKey = string.Join(",", pattern.Select(s => ((int)s).ToString()));
                    
                    // Determine regime for this pattern occurrence
                    var regimeKey = DetermineRegime(currentContext);

                    if (!_patterns.ContainsKey(patternKey))
                    {
                        _patterns[patternKey] = new MarkovPattern
                        {
                            PatternKey = patternKey,
                            StateSequence = pattern.ToList(),
                            AverageContext = new MarketContext()
                        };
                    }
                    
                    // Also store in regime-specific dictionary
                    if (!_regimePatterns[regimeKey].ContainsKey(patternKey))
                    {
                        _regimePatterns[regimeKey][patternKey] = new MarkovPattern
                        {
                            PatternKey = patternKey,
                            StateSequence = pattern.ToList(),
                            AverageContext = new MarketContext()
                        };
                    }

                    var markovPattern = _patterns[patternKey];
                    var regimePattern = _regimePatterns[regimeKey][patternKey];
                    
                    markovPattern.TotalOccurrences++;
                    regimePattern.TotalOccurrences++;

                    if (!markovPattern.NextStateOccurrences.ContainsKey(nextState))
                        markovPattern.NextStateOccurrences[nextState] = 0;
                    markovPattern.NextStateOccurrences[nextState]++;
                    
                    if (!regimePattern.NextStateOccurrences.ContainsKey(nextState))
                        regimePattern.NextStateOccurrences[nextState] = 0;
                    regimePattern.NextStateOccurrences[nextState]++;
                    
                    // Accumulate context information (ALL features for fair comparison)
                    markovPattern.AverageContext.Volatility += currentContext.Volatility;
                    markovPattern.AverageContext.Trend += currentContext.Trend;
                    markovPattern.AverageContext.RelativeVolume += currentContext.RelativeVolume;
                    markovPattern.AverageContext.MomentumScore += currentContext.MomentumScore;
                    markovPattern.AverageContext.ShortTermTrend += currentContext.ShortTermTrend;
                    markovPattern.AverageContext.VolatilityRank += currentContext.VolatilityRank;
                    markovPattern.AverageContext.ConsecutiveDays += currentContext.ConsecutiveDays;
                    
                    regimePattern.AverageContext.Volatility += currentContext.Volatility;
                    regimePattern.AverageContext.Trend += currentContext.Trend;
                    regimePattern.AverageContext.RelativeVolume += currentContext.RelativeVolume;
                    regimePattern.AverageContext.MomentumScore += currentContext.MomentumScore;
                    regimePattern.AverageContext.ShortTermTrend += currentContext.ShortTermTrend;
                    regimePattern.AverageContext.VolatilityRank += currentContext.VolatilityRank;
                    regimePattern.AverageContext.ConsecutiveDays += currentContext.ConsecutiveDays;

                }

                // Calculate probabilities and finalize context averages
                FinalizePatterns(_patterns.Values);
                foreach (var regimeDict in _regimePatterns.Values)
                {
                    FinalizePatterns(regimeDict.Values);
                }
            }
            
            private string DetermineRegime(MarketContext context)
            {
                // Determine primary regime based on volatility and trend
                var isHighVol = context.Volatility > 0.20; // 20% annualized
                
                if (isHighVol)
                    return "HighVol";
                
                if (context.Trend > 0.05)
                    return "Uptrend";
                else if (context.Trend < -0.05)
                    return "Downtrend";
                else
                    return "Sideways";
            }
            
            private void FinalizePatterns(IEnumerable<MarkovPattern> patterns)
            {
                foreach (var pattern in patterns)
                {
                    foreach (var nextState in pattern.NextStateOccurrences.Keys)
                    {
                        var probability = (double)pattern.NextStateOccurrences[nextState] / pattern.TotalOccurrences;
                        pattern.NextStateProbabilities[nextState] = probability;
                    }

                    // Average the context
                    if (pattern.TotalOccurrences > 0)
                    {
                        pattern.AverageContext.Volatility /= pattern.TotalOccurrences;
                        pattern.AverageContext.Trend /= pattern.TotalOccurrences;
                        pattern.AverageContext.RelativeVolume /= pattern.TotalOccurrences;
                        pattern.AverageContext.MomentumScore /= pattern.TotalOccurrences;
                        pattern.AverageContext.ShortTermTrend /= pattern.TotalOccurrences;
                        pattern.AverageContext.VolatilityRank /= pattern.TotalOccurrences;
                        pattern.AverageContext.ConsecutiveDays /= pattern.TotalOccurrences;
                    }
                    
                    // Determine trend regime
                    pattern.AverageContext.TrendRegime = 
                        pattern.AverageContext.Trend > 0.05 ? "Uptrend" :
                        pattern.AverageContext.Trend < -0.05 ? "Downtrend" : "Sideways";

                    pattern.ConfidenceScore = CalculateEnhancedConfidenceScore(pattern);
                }
            }

            private MarketContext CalculateMarketContext(PriceRecord[] data, int currentIndex)
            {
                var context = new MarketContext();
                
                // Calculate 20-day volatility
                if (currentIndex >= 20)
                {
                    var returns = new List<double>();
                    for (int i = currentIndex - 19; i <= currentIndex; i++)
                    {
                        var ret = (data[i].Close - data[i - 1].Close) / data[i - 1].Close;
                        returns.Add(ret);
                    }
                    var avgReturn = returns.Average();
                    var variance = returns.Select(r => Math.Pow(r - avgReturn, 2)).Average();
                    context.Volatility = Math.Sqrt(variance * 252); // Annualized
                    
                    // Calculate volatility rank (percentile)
                    if (currentIndex >= 100)
                    {
                        var volatilities = new List<double>();
                        for (int i = currentIndex - 99; i <= currentIndex - 20; i++)
                        {
                            if (i >= 20) // Ensure we have enough history for volatility calculation
                            {
                                var histReturns = new List<double>();
                                for (int j = i - 19; j <= i; j++)
                                {
                                    if (j > 0 && j < data.Length) // Bounds check
                                    {
                                        var ret = (data[j].Close - data[j - 1].Close) / data[j - 1].Close;
                                        histReturns.Add(ret);
                                    }
                                }
                                if (histReturns.Count >= 19) // Need at least 19 returns for valid volatility
                                {
                                    var histAvg = histReturns.Average();
                                    var histVar = histReturns.Select(r => Math.Pow(r - histAvg, 2)).Average();
                                    volatilities.Add(Math.Sqrt(histVar * 252));
                                }
                            }
                        }
                        if (volatilities.Count > 0)
                        {
                            context.VolatilityRank = volatilities.Count(v => v < context.Volatility) / (double)volatilities.Count;
                        }
                    }
                }
                
                // Calculate 50-day trend (SMA slope)
                if (currentIndex >= 50)
                {
                    var sma50Old = data.Skip(currentIndex - 50).Take(25).Average(r => r.Close);
                    var sma50New = data.Skip(currentIndex - 25).Take(25).Average(r => r.Close);
                    context.Trend = (sma50New - sma50Old) / sma50Old;
                    
                    // NEW: Short-term vs medium-term trend
                    if (currentIndex >= 20)
                    {
                        var sma5 = data.Skip(currentIndex - 4).Take(5).Average(r => r.Close);
                        var sma20 = data.Skip(currentIndex - 19).Take(20).Average(r => r.Close);
                        context.ShortTermTrend = (sma5 - sma20) / sma20;
                    }
                }
                
                // Calculate relative volume
                if (currentIndex >= 20 && data[currentIndex].Volume > 0)
                {
                    var avgVolume = data.Skip(currentIndex - 20).Take(20).Average(r => r.Volume);
                    context.RelativeVolume = avgVolume > 0 ? data[currentIndex].Volume / avgVolume : 1.0;
                }
                
                // Calculate momentum score (RSI-like)
                if (currentIndex >= 14)
                {
                    var gains = 0.0;
                    var losses = 0.0;
                    for (int i = currentIndex - 13; i <= currentIndex; i++)
                    {
                        var change = data[i].Close - data[i - 1].Close;
                        if (change > 0) gains += change;
                        else losses += Math.Abs(change);
                    }
                    var rs = losses > 0 ? gains / losses : 100.0;
                    context.MomentumScore = 100.0 - (100.0 / (1.0 + rs));
                }
                
                // NEW: Calculate gap size
                if (currentIndex >= 1)
                {
                    context.GapSize = (data[currentIndex].Open - data[currentIndex - 1].Close) / data[currentIndex - 1].Close * 100.0;
                }
                
                // NEW: Intraday momentum
                context.IntradayMomentum = (data[currentIndex].Close - data[currentIndex].Open) / data[currentIndex].Open * 100.0;
                
                // NEW: Consecutive days count
                if (currentIndex >= 5)
                {
                    var consecutive = 1;
                    var currentDirection = data[currentIndex].Close > data[currentIndex - 1].Close;
                    for (int i = currentIndex - 1; i >= Math.Max(1, currentIndex - 9); i--)
                    {
                        var direction = data[i].Close > data[i - 1].Close;
                        if (direction == currentDirection)
                        {
                            consecutive++;
                        }
                        else
                        {
                            break;
                        }
                    }
                    context.ConsecutiveDays = currentDirection ? consecutive : -consecutive;
                }
                
                // NEW: Average True Range (14-day)
                if (currentIndex >= 14)
                {
                    var trValues = new List<double>();
                    for (int i = currentIndex - 13; i <= currentIndex; i++)
                    {
                        var highLow = data[i].High - data[i].Low;
                        var highClose = Math.Abs(data[i].High - data[i - 1].Close);
                        var lowClose = Math.Abs(data[i].Low - data[i - 1].Close);
                        trValues.Add(Math.Max(highLow, Math.Max(highClose, lowClose)));
                    }
                    context.ATR = trValues.Average();
                }
                
                // NEW: Recent max drawdown
                if (currentIndex >= 10)
                {
                    var maxPrice = data.Skip(currentIndex - 9).Take(10).Max(r => r.High);
                    var minAfterMax = double.MaxValue;
                    var foundMax = false;
                    for (int i = currentIndex - 9; i <= currentIndex; i++)
                    {
                        if (!foundMax && data[i].High >= maxPrice)
                            foundMax = true;
                        if (foundMax && data[i].Low < minAfterMax)
                            minAfterMax = data[i].Low;
                    }
                    if (foundMax && minAfterMax != double.MaxValue)
                    {
                        context.RecentMaxDrawdown = (minAfterMax - maxPrice) / maxPrice * 100.0;
                    }
                }
                
                return context;
            }

            public MarkovPrediction PredictWithContext(MarketState[] recentStates, MarketContext currentContext)
            {
                if (recentStates.Length != _order)
                    throw new ArgumentException($"Recent states must have exactly {_order} elements");

                var patternKey = string.Join(",", recentStates.Select(s => ((int)s).ToString()));
                
                // NEW: Try regime-specific pattern first for better accuracy
                var regimeKey = DetermineRegime(currentContext);
                var regimePatterns = _regimePatterns[regimeKey];
                
                MarkovPattern selectedPattern = null;
                var useRegimePattern = false;
                
                // Prefer regime-specific pattern if it has enough samples
                if (regimePatterns.ContainsKey(patternKey) && 
                    regimePatterns[patternKey].TotalOccurrences >= _minSampleSize)
                {
                    selectedPattern = regimePatterns[patternKey];
                    useRegimePattern = true;
                }
                // Fallback to global pattern
                else if (_patterns.ContainsKey(patternKey) && 
                         _patterns[patternKey].TotalOccurrences >= _minSampleSize)
                {
                    selectedPattern = _patterns[patternKey];
                }

                if (selectedPattern == null)
                {
                    return new MarkovPrediction
                    {
                        PredictedState = MarketState.Flat,
                        Confidence = 0.0,
                        ContextScore = 0.0,
                        Message = regimePatterns.ContainsKey(patternKey) 
                            ? $"Pattern found in {regimeKey} regime but insufficient samples ({regimePatterns[patternKey].TotalOccurrences} < {_minSampleSize})"
                            : $"Pattern not found in {regimeKey} regime or global patterns"
                    };
                }

                var bestPrediction = selectedPattern.NextStateProbabilities.OrderByDescending(kvp => kvp.Value).First();
                
                // Calculate context similarity score
                var contextScore = CalculateContextSimilarity(selectedPattern.AverageContext, currentContext);
                
                // Boost confidence if using regime-specific pattern
                var confidenceBoost = useRegimePattern ? 1.1 : 1.0;

                return new MarkovPrediction
                {
                    PredictedState = bestPrediction.Key,
                    Confidence = Math.Min(1.0, selectedPattern.ConfidenceScore * confidenceBoost), // Cap at 1.0
                    ContextScore = contextScore,
                    Probability = bestPrediction.Value,
                    SampleSize = selectedPattern.TotalOccurrences,
                    Message = $"Based on {selectedPattern.TotalOccurrences} occurrences in {(useRegimePattern ? regimeKey + " regime" : "global")}, context match: {contextScore:F2}"
                };
            }

            private double CalculateContextSimilarity(MarketContext patternContext, MarketContext currentContext)
            {
                // Calculate how similar current context is to pattern's average context
                var volSimilarity = 1.0 - Math.Min(1.0, Math.Abs(patternContext.Volatility - currentContext.Volatility) / 0.5);
                var trendSimilarity = 1.0 - Math.Min(1.0, Math.Abs(patternContext.Trend - currentContext.Trend) / 0.2);
                var momentumSimilarity = 1.0 - Math.Min(1.0, Math.Abs(patternContext.MomentumScore - currentContext.MomentumScore) / 50.0);
                
                // NEW: Add more similarity metrics
                var shortTrendSimilarity = 1.0 - Math.Min(1.0, Math.Abs(patternContext.ShortTermTrend - currentContext.ShortTermTrend) / 0.1);
                var volRankSimilarity = 1.0 - Math.Min(1.0, Math.Abs(patternContext.VolatilityRank - currentContext.VolatilityRank));
                var consecutiveSimilarity = 1.0 - Math.Min(1.0, Math.Abs(patternContext.ConsecutiveDays - currentContext.ConsecutiveDays) / 5.0);
                
                // Weighted combination emphasizing volatility regime and trend
                return (volSimilarity * 0.25 + 
                        trendSimilarity * 0.20 + 
                        momentumSimilarity * 0.15 +
                        shortTrendSimilarity * 0.15 +
                        volRankSimilarity * 0.15 +
                        consecutiveSimilarity * 0.10);
            }

            private double CalculateEnhancedConfidenceScore(MarkovPattern pattern)
            {
                // Improved confidence calculation with higher sample size requirements
                var sampleSizeScore = Math.Min(1.0, pattern.TotalOccurrences / 30.0); // Need 30 samples for full confidence
                
                // Calculate Shannon entropy (lower entropy = more predictable)
                var entropy = 0.0;
                foreach (var prob in pattern.NextStateProbabilities.Values)
                {
                    if (prob > 0)
                        entropy -= prob * Math.Log(prob) / Math.Log(2);
                }
                var maxEntropy = Math.Log(3) / Math.Log(2); // 3 states
                var entropyScore = 1.0 - (entropy / maxEntropy);
                
                // Bonus for strong directional bias
                var maxProb = pattern.NextStateProbabilities.Values.Max();
                var biasScore = (maxProb - 0.33) / 0.67; // Normalize: 33% baseline (random), 100% = perfect

                return (sampleSizeScore * 0.5) + (entropyScore * 0.3) + (biasScore * 0.2);
            }

            public Dictionary<string, MarkovPattern> GetTopPatterns(int count = 20)
            {
                return _patterns.Values
                    .Where(p => p.TotalOccurrences >= _minSampleSize)  // Only show statistically significant patterns
                    .OrderByDescending(p => p.ConfidenceScore)
                    .ThenByDescending(p => p.TotalOccurrences)
                    .Take(count)
                    .ToDictionary(p => p.PatternKey, p => p);
            }

            public int GetTotalPatternsCount() => _patterns.Count;
            public int GetSignificantPatternsCount() => _patterns.Values.Count(p => p.TotalOccurrences >= _minSampleSize);
        }

        /// <summary>
        /// Enhanced prediction result with context awareness
        /// </summary>
        public class MarkovPrediction
        {
            public MarketState PredictedState { get; set; }
            public double Confidence { get; set; }
            public double ContextScore { get; set; }  // NEW: How well current context matches pattern context
            public double Probability { get; set; }
            public int SampleSize { get; set; }
            public string Message { get; set; }

            public override string ToString()
            {
                return $"{PredictedState} (prob={Probability:P1}, conf={Confidence:F3}, ctx={ContextScore:F2}, n={SampleSize})";
            }
        }

        /// <summary>
        /// Trading signal based on Markov prediction
        /// </summary>
        public class MarkovTradingSignal
        {
            public DateTime SignalDate { get; set; }
            public MarkovPrediction Prediction { get; set; }
            public Polygon2.OptionType? RecommendedOptionType { get; set; }
            public int RecommendedStrikeDistance { get; set; }
            public int RecommendedExpirationDays { get; set; }
            public double SignalStrength { get; set; }
            public string Reasoning { get; set; }

            public override string ToString()
            {
                return $"{SignalDate:yyyy-MM-dd}: {RecommendedOptionType} {RecommendedStrikeDistance}OTM {RecommendedExpirationDays}DTE (strength={SignalStrength:F3}) - {Reasoning}";
            }
        }

        /// <summary>
        /// Enhanced pattern with volatility and trend information
        /// </summary>
        public class EnhancedMarkovPattern : MarkovPattern
        {
            public double AverageVolatility { get; set; }
            public double TrendStrength { get; set; }
            public Dictionary<DayOfWeek, int> DayOfWeekDistribution { get; set; }
            public int ConsecutiveDays { get; set; }

            public EnhancedMarkovPattern() : base()
            {
                DayOfWeekDistribution = new Dictionary<DayOfWeek, int>();
            }
        }

        [TestMethod]
        [TestCategory("Markov")]
        public void EnhancedMarkovChain_WalkForward_RealPredictivePower()
        {
            var daily = _sharedDaily;
            Assert.IsTrue(daily.Length > 0, "No daily price records loaded.");

            var orderedDaily = daily.OrderBy(r => r.DateTime.Date).ToArray();
            var lastDate = orderedDaily.Max(r => r.DateTime.Date);
            var minWindowDate = lastDate.AddYears(-10); // More data for better patterns

            var window = orderedDaily
                .Where(r => r.DateTime.Date >= minWindowDate && r.DateTime.Date <= lastDate)
                .OrderBy(r => r.DateTime.Date)
                .ToArray();

            Assert.IsTrue(window.Length > 250, "Insufficient daily records for robust Markov analysis.");

            ConsoleUtilities.WriteLine($"=== ENHANCED MARKOV PATTERN ANALYSIS WITH CASH TRACKING ===");
            ConsoleUtilities.WriteLine($"Analyzing {window.Length} days from {window.First().DateTime:yyyy-MM-dd} to {window.Last().DateTime:yyyy-MM-dd}");

            // Walk-forward analysis with larger windows for statistical significance
            var trainingWindowDays = 252 * 3; // 3 years of training
            var testPeriodDays = 60; // Test 60 days at a time
            var markovOrder = 2; // REDUCED from 4 to 2 for better generalization

            // Cash accounting variables
            var startingCash = 100000.0; // Start with $100k
            var currentCash = startingCash;
            var tradesExecuted = 0;
            var profitableTrades = 0;
            var losingTrades = 0;
            var totalGainLoss = 0.0;
            var maxDrawdown = 0.0;
            var peakCash = startingCash;
            var tradeLog = new List<(DateTime date, string action, double price, double shares, double cost, double cash, double portfolioValue, string reason)>();

            var allPredictions = new List<(DateTime date, MarkovPrediction prediction, MarketState actual, double actualReturn, MarketContext context)>();
            var correctDirectional = 0; // Correct direction (up vs down vs flat)
            var totalPredictions = 0;
            var profitableIfTraded = 0; // If we traded on prediction, would it be profitable?
            
            // Position tracking
            double currentPosition = 0; // Number of shares held
            double positionEntryPrice = 0;
            DateTime? positionEntryDate = null;

            // Walk forward through the data
            for (int startIdx = trainingWindowDays; startIdx < window.Length - testPeriodDays; startIdx += testPeriodDays)
            {
                var trainEndIdx = startIdx;
                var testStartIdx = startIdx;
                var testEndIdx = Math.Min(startIdx + testPeriodDays, window.Length - 1);

                var trainingWindow = window.Take(trainEndIdx).ToArray();
                var testWindow = window.Skip(testStartIdx).Take(testEndIdx - testStartIdx).ToArray();

                ConsoleUtilities.WriteLine($"\n=== Walk-Forward Window ===");
                ConsoleUtilities.WriteLine($"Training: {trainingWindow.Length} days ending {trainingWindow.Last().DateTime:yyyy-MM-dd}");
                ConsoleUtilities.WriteLine($"Testing: {testWindow.Length} days starting {testWindow.First().DateTime:yyyy-MM-dd}");
                ConsoleUtilities.WriteLine($"Current Cash: ${currentCash:N2}, Portfolio Value: ${(currentCash + currentPosition * (testWindow.First().Close)):N2}");

                // Train enhanced Markov chain
                var markovChain = new EnhancedMarkovChain(markovOrder);
                markovChain.TrainWithContext(trainingWindow, testWindow.First().DateTime);

                var topPatterns = markovChain.GetTopPatterns(10);
                ConsoleUtilities.WriteLine($"Significant patterns identified: {markovChain.GetSignificantPatternsCount()} of {markovChain.GetTotalPatternsCount()} total");
                
                foreach (var pattern in topPatterns.Values.Take(3))
                {
                    ConsoleUtilities.WriteLine($"  {pattern}");
                }

                // Generate predictions for test period
                for (int testIdx = markovOrder; testIdx < testWindow.Length - 1; testIdx++)
                {
                    var currentDate = testWindow[testIdx].DateTime.Date;
                    var currentPrice = testWindow[testIdx].Close;
                    
                    // Get recent price moves
                    var recentChanges = new List<double>();
                    for (int i = Math.Max(1, testIdx - markovOrder + 1); i <= testIdx; i++)
                    {
                        var pctChange = (testWindow[i].Close - testWindow[i - 1].Close) / testWindow[i - 1].Close * 100.0;
                        recentChanges.Add(pctChange);
                    }

                    if (recentChanges.Count < markovOrder) continue;

                    var recentStates = recentChanges.Select(markovChain.ClassifyMove).ToArray();
                    
                    // Calculate current market context
                    var currentContext = CalculateMarketContextForTest(testWindow, testIdx);
                    
                    // Get prediction with context
                    var prediction = markovChain.PredictWithContext(recentStates, currentContext);

                    // Calculate actual next day move
                    var nextDayPrice = testWindow[testIdx + 1].Close;
                    var actualChange = (nextDayPrice - currentPrice) / currentPrice * 100.0;
                    var actualState = markovChain.ClassifyMove(actualChange);

                    totalPredictions++;
                    
                    // Track directional accuracy
                    if (prediction.PredictedState == actualState)
                        correctDirectional++;
                    
                    // Track if trading on this signal would be profitable
                    if ((prediction.PredictedState == MarketState.Up && actualChange > 0) ||
                        (prediction.PredictedState == MarketState.Down && actualChange < 0))
                    {
                        profitableIfTraded++;
                    }

                    // TRADING LOGIC - Simplified: Always hold 90% SPX + trade 10% tactically with Markov
                    
                    // Step 1: Calculate base 90% SPX position (unleveraged buy-and-hold)
                    var baseSpxAllocation = 0.90; // 90% of capital in SPX always
                    var tacticalAllocation = 0.10; // 10% for tactical Markov trading
                    
                    // Step 2: Determine tactical position based on Markov signals
                    double tacticalMultiplier = 0.0; // Default: 0% tactical (just hold SPX)
                    string tacticalReason = "No signal - 100% cash on tactical portion";
                    
                    // Check for high-confidence Markov signals (relaxed thresholds)
                    var isHighConfidence = prediction.SampleSize >= 15 &&
                                          prediction.Probability > 0.45 &&
                                          prediction.Confidence > 0.50 &&
                                          prediction.ContextScore > 0.60;
                    
                    if (isHighConfidence)
                    {
                        if (prediction.PredictedState == MarketState.Up)
                        {
                            // Long with leverage on tactical portion: 2x to 4x
                            var confidenceBonus = (prediction.Confidence - 0.50) / 0.50; // 0 to 1
                            var contextBonus = (prediction.ContextScore - 0.60) / 0.40; // 0 to 1
                            tacticalMultiplier = Math.Min(4.0, 2.0 + confidenceBonus + contextBonus); // 2x to 4x
                            tacticalReason = $"Markov UP (Conf:{prediction.Confidence:F2}, Ctx:{prediction.ContextScore:F2}) - {tacticalMultiplier:F1}x leverage";
                        }
                        else if (prediction.PredictedState == MarketState.Down)
                        {
                            // Go to cash or short on tactical portion
                            tacticalMultiplier = 0.0; // Cash (or could go -1x for short)
                            tacticalReason = $"Markov DOWN (Conf:{prediction.Confidence:F2}, Ctx:{prediction.ContextScore:F2}) - 0% tactical";
                        }
                        else
                        {
                            // Flat prediction - hold 1x tactical
                            tacticalMultiplier = 1.0;
                            tacticalReason = $"Markov FLAT (Conf:{prediction.Confidence:F2}, Ctx:{prediction.ContextScore:F2}) - 1x tactical";
                        }
                    }
                    else
                    {
                        // No high-confidence signal - default to 1x on tactical (match SPX)
                        tacticalMultiplier = 1.0;
                        tacticalReason = "No high-conf signal - 1x tactical (match SPX)";
                    }
                    
                    // Step 3: Calculate total position size
                    // Total = 90% SPX (always) + 10% tactical (0x to 4x based on signals)
                    // Example: 90% + 10% * 4x = 90% + 40% = 130% total (30% leverage)
                    var totalAllocation = baseSpxAllocation + (tacticalAllocation * tacticalMultiplier);
                    
                    // Step 4: Close old position if allocation changed
                    if (currentPosition != 0)
                    {
                        var positionValue = currentPosition * currentPrice;
                        var positionPnL = positionValue - (currentPosition * positionEntryPrice);
                        currentCash += positionValue;
                        
                        tradesExecuted++;
                        if (positionPnL > 0)
                            profitableTrades++;
                        else
                            losingTrades++;
                        
                        totalGainLoss += positionPnL;
                        
                        tradeLog.Add((currentDate, "CLOSE", currentPrice, currentPosition, positionValue, 
                            currentCash, currentCash, $"PnL: ${positionPnL:N2}, Rebalance daily"));
                        
                        currentPosition = 0;
                        positionEntryPrice = 0;
                        positionEntryDate = null;
                    }
                    
                    // Step 5: Enter new position with calculated allocation
                    var investAmount = currentCash * totalAllocation;
                    if (investAmount >= currentPrice)
                    {
                        currentPosition = investAmount / currentPrice;
                        positionEntryPrice = currentPrice;
                        positionEntryDate = currentDate;
                        currentCash -= investAmount;
                        
                        var actionLabel = tacticalMultiplier >= 2.0 ? "BUY (90% SPX + Tactical LONG)" :
                                         tacticalMultiplier == 0.0 ? "BUY (90% SPX + Cash)" :
                                         "BUY (90% SPX + Tactical 1x)";
                        
                        tradeLog.Add((currentDate, actionLabel, currentPrice, currentPosition, investAmount,
                            currentCash, currentCash + investAmount, 
                            $"Total:{totalAllocation:P0} (90% SPX + {tacticalMultiplier:F1}x tactical) | {tacticalReason}"));
                    }
                    

                    // Update peak and drawdown tracking
                    var currentPortfolioValue = currentCash + (currentPosition * currentPrice);
                    if (currentPortfolioValue > peakCash)
                        peakCash = currentPortfolioValue;
                    
                    var currentDrawdown = (peakCash - currentPortfolioValue) / peakCash;
                    if (currentDrawdown > maxDrawdown)
                        maxDrawdown = currentDrawdown;

                    allPredictions.Add((currentDate, prediction, actualState, actualChange, currentContext));
                }
            }
            
            // Close any remaining position at the end
            if (currentPosition != 0)
            {
                var finalPrice = window[window.Length - 1].Close;
                var finalDate = window[window.Length - 1].DateTime.Date;
                var positionValue = currentPosition * finalPrice;
                var positionPnL = positionValue - (currentPosition * positionEntryPrice);
                currentCash += positionValue;
                
                tradesExecuted++;
                if (positionPnL > 0)
                    profitableTrades++;
                else
                    losingTrades++;
                
                totalGainLoss += positionPnL;
                
                tradeLog.Add((finalDate, "FINAL CLOSE", finalPrice, currentPosition, positionValue,
                    currentCash, currentCash, $"PnL: ${positionPnL:N2}, Final liquidation"));
                
                currentPosition = 0;
            }

            // Comprehensive analysis
            var directionalAccuracy = totalPredictions > 0 ? (double)correctDirectional / totalPredictions : 0.0;
            var tradingAccuracy = totalPredictions > 0 ? (double)profitableIfTraded / totalPredictions : 0.0;
            var finalPortfolioValue = currentCash;
            var totalReturn = ((finalPortfolioValue - startingCash) / startingCash) * 100.0;
            var winRate = tradesExecuted > 0 ? (double)profitableTrades / tradesExecuted : 0.0;
            
            ConsoleUtilities.WriteLine($"\n=== ENHANCED MARKOV RESULTS ===");
            ConsoleUtilities.WriteLine($"Total Predictions: {totalPredictions}");
            ConsoleUtilities.WriteLine($"Directional Accuracy: {directionalAccuracy:P2}");
            ConsoleUtilities.WriteLine($"Trading Accuracy: {tradingAccuracy:P2}");
            
            ConsoleUtilities.WriteLine($"\n=== CASH ACCOUNTING RESULTS ===");
            ConsoleUtilities.WriteLine($"Starting Capital: ${startingCash:N2}");
            ConsoleUtilities.WriteLine($"Final Portfolio Value: ${finalPortfolioValue:N2}");
            ConsoleUtilities.WriteLine($"Total Return: {totalReturn:F2}%");
            ConsoleUtilities.WriteLine($"Total Gain/Loss: ${totalGainLoss:N2}");
            ConsoleUtilities.WriteLine($"Trades Executed: {tradesExecuted}");
            ConsoleUtilities.WriteLine($"Profitable Trades: {profitableTrades} ({winRate:P2})");
            ConsoleUtilities.WriteLine($"Losing Trades: {losingTrades}");
            ConsoleUtilities.WriteLine($"Max Drawdown: {maxDrawdown:P2}");
            ConsoleUtilities.WriteLine($"Peak Portfolio Value: ${peakCash:N2}");
            
            // Calculate additional metrics
            var avgTradeReturn = tradesExecuted > 0 ? totalGainLoss / tradesExecuted : 0;
            var avgWinningTrade = profitableTrades > 0 ? 
                tradeLog.Where(t => t.reason.Contains("PnL: $") && !t.reason.Contains("-$"))
                       .Select(t => {
                           var pnlStart = t.reason.IndexOf("PnL: $") + 6;
                           var pnlEnd = t.reason.IndexOf(",", pnlStart);
                           if (double.TryParse(t.reason.Substring(pnlStart, pnlEnd - pnlStart).Replace(",", ""), out var pnl))
                               return pnl;
                           return 0.0;
                       })
                       .Where(p => p > 0)
                       .DefaultIfEmpty(0)
                       .Average() : 0;
            
            var avgLosingTrade = losingTrades > 0 ? 
                tradeLog.Where(t => t.reason.Contains("PnL: $-"))
                       .Select(t => {
                           var i = t.reason.IndexOf("PnL: $") + 6;
                           var j = t.reason.IndexOf(",", i);
                           var s = (j > i ? t.reason.Substring(i, j - i) : t.reason.Substring(i)).Replace(",", "");
                           return double.TryParse(s, out var pnl) ? Math.Abs(pnl) : 0.0;
                       })
                       .Where(p => p > 0)
                       .DefaultIfEmpty(0)
                       .Average() : 0;
            
            ConsoleUtilities.WriteLine($"\n=== TRADE ANALYTICS ===");
            ConsoleUtilities.WriteLine($"Average Trade P/L: ${avgTradeReturn:N2}");
            ConsoleUtilities.WriteLine($"Average Winning Trade: ${avgWinningTrade:N2}");
            ConsoleUtilities.WriteLine($"Average Losing Trade: -${avgLosingTrade:N2}");
            if (avgLosingTrade > 0)
            {
                var profitFactor = (profitableTrades * avgWinningTrade) / (losingTrades * avgLosingTrade);
                ConsoleUtilities.WriteLine($"Profit Factor: {profitFactor:F2}");
            }
            
            // Analyze signal usage
            var highConfSignals = allPredictions.Count(p => p.prediction.Confidence > 0.65 && 
                                                             p.prediction.ContextScore > 0.75 && 
                                                             p.prediction.SampleSize >= 20 &&
                                                             p.prediction.Probability > 0.55);
            var highConfUpSignals = allPredictions.Count(p => p.prediction.PredictedState == MarketState.Up &&
                                                               p.prediction.Confidence > 0.65 && 
                                                               p.prediction.ContextScore > 0.75 && 
                                                               p.prediction.SampleSize >= 20 &&
                                                               p.prediction.Probability > 0.55);
            var highConfDownSignals = allPredictions.Count(p => p.prediction.PredictedState == MarketState.Down &&
                                                                 p.prediction.Confidence > 0.65 && 
                                                                 p.prediction.ContextScore > 0.75 && 
                                                                 p.prediction.SampleSize >= 20 &&
                                                                 p.prediction.Probability > 0.55);
            
            // NEW: Regime analysis
            var bullMarketDays = allPredictions.Count(p => p.context.Trend > 0.08 && 
                                                            p.context.Volatility < 0.30 &&
                                                            p.context.MomentumScore > 45);
            var nonBullMarketDays = totalPredictions - bullMarketDays;
            
            ConsoleUtilities.WriteLine($"\n=== SIGNAL QUALITY ===");
            ConsoleUtilities.WriteLine($"High Confidence Signals: {highConfSignals} of {totalPredictions} ({(double)highConfSignals/totalPredictions:P1})");
            ConsoleUtilities.WriteLine($"  UP Signals: {highConfUpSignals}");
            ConsoleUtilities.WriteLine($"  DOWN Signals: {highConfDownSignals}");
            ConsoleUtilities.WriteLine($"Signal Selectivity: {(highConfSignals > 0 ? (totalPredictions / (double)highConfSignals).ToString("F1") + "x filter" : "No signals")}");
            
            ConsoleUtilities.WriteLine($"\n=== REGIME ANALYSIS ===");
            ConsoleUtilities.WriteLine($"Bull Market Days: {bullMarketDays} ({(double)bullMarketDays/totalPredictions:P1})");
            ConsoleUtilities.WriteLine($"Non-Bull Market Days: {nonBullMarketDays} ({(double)nonBullMarketDays/totalPredictions:P1})");
            ConsoleUtilities.WriteLine($"Strategy: Hold 90% during bull, use Markov signals during uncertainty");
            
            // Calculate regime-specific performance from trade log
            var bullTrades = tradeLog.Count(t => t.reason.Contains("REGIME=BULL"));
            var markovTrades = tradeLog.Count(t => t.reason.Contains("Pred:"));
            var defensiveTrades = tradeLog.Count(t => t.action.Contains("Defensive"));

            ConsoleUtilities.WriteLine($"\n=== TRADE TYPE BREAKDOWN ===");
            ConsoleUtilities.WriteLine($"Bull Market Entries: {bullTrades}");
            ConsoleUtilities.WriteLine($"Markov Signal Trades: {markovTrades}");
            ConsoleUtilities.WriteLine($"Defensive/Passive Trades: {defensiveTrades}");



            // Show recent trades
            ConsoleUtilities.WriteLine($"\n=== RECENT TRADE LOG (Last 10) ===");
            var recentTrades = tradeLog.Skip(Math.Max(0, tradeLog.Count - 10)).ToList();
            foreach (var trade in recentTrades)
            {
                ConsoleUtilities.WriteLine($"{trade.date:yyyy-MM-dd} | {trade.action,-10} | Price: ${trade.price:F2} | Shares: {trade.shares:F2} | Cash: ${trade.cash:N2} | Value: ${trade.portfolioValue:N2} | {trade.reason}");
            }

            // Analyze predictions by confidence level
            var highConfidencePredictions = allPredictions.Where(p => p.prediction.Confidence > 0.6 && p.prediction.ContextScore > 0.7).ToList();
            if (highConfidencePredictions.Count > 0)
            {
                var highConfAccuracy = highConfidencePredictions.Count(p => p.prediction.PredictedState == p.actual) / (double)highConfidencePredictions.Count;
                ConsoleUtilities.WriteLine($"\nHigh Confidence Predictions (conf>0.6, ctx>0.7): {highConfidencePredictions.Count}");
                ConsoleUtilities.WriteLine($"High Confidence Accuracy: {highConfAccuracy:P2}");
            }
            
            // NEW: Analyze by volatility regime
            var highVolPredictions = allPredictions.Where(p => p.context.Volatility > 0.20).ToList();
            var lowVolPredictions = allPredictions.Where(p => p.context.Volatility <= 0.20).ToList();
            
            if (highVolPredictions.Count > 10)
            {
                var highVolAccuracy = highVolPredictions.Count(p => p.prediction.PredictedState == p.actual) / (double)highVolPredictions.Count;
                ConsoleUtilities.WriteLine($"\nHigh Volatility Regime (>20%): {highVolPredictions.Count} predictions");
                ConsoleUtilities.WriteLine($"  Accuracy: {highVolAccuracy:P2}");
            }
            
            if (lowVolPredictions.Count > 10)
            {
                var lowVolAccuracy = lowVolPredictions.Count(p => p.prediction.PredictedState == p.actual) / (double)lowVolPredictions.Count;
                ConsoleUtilities.WriteLine($"Low Volatility Regime (<=20%): {lowVolPredictions.Count} predictions");
                ConsoleUtilities.WriteLine($"  Accuracy: {lowVolAccuracy:P2}");
            }

            // Analyze by market regime
            var uptrendPredictions = allPredictions.Where(p => p.context.Trend > 0.05).ToList();
            var downtrendPredictions = allPredictions.Where(p => p.context.Trend < -0.05).ToList();
            var sidewaysPredictions = allPredictions.Where(p => Math.Abs(p.context.Trend) <= 0.05).ToList();
            
            if (uptrendPredictions.Count > 10)
            {
                var uptrendAccuracy = uptrendPredictions.Count(p => p.prediction.PredictedState == p.actual) / (double)uptrendPredictions.Count;
                ConsoleUtilities.WriteLine($"\nUptrend Market Accuracy: {uptrendAccuracy:P2} (n={uptrendPredictions.Count})");
            }
            
            if (downtrendPredictions.Count > 10)
            {
                var downtrendAccuracy = downtrendPredictions.Count(p => p.prediction.PredictedState == p.actual) / (double)downtrendPredictions.Count;
                ConsoleUtilities.WriteLine($"Downtrend Market Accuracy: {downtrendAccuracy:P2} (n={downtrendPredictions.Count})");
            }
            
            if (sidewaysPredictions.Count > 10)
            {
                var sidewaysAccuracy = sidewaysPredictions.Count(p => p.prediction.PredictedState == p.actual) / (double)sidewaysPredictions.Count;
                ConsoleUtilities.WriteLine($"Sideways Market Accuracy: {sidewaysAccuracy:P2} (n={sidewaysPredictions.Count})");
            }
            
            // NEW: Analyze by consecutive day streaks
            var afterStreakPredictions = allPredictions.Where(p => Math.Abs(p.context.ConsecutiveDays) >= 3).ToList();
            if (afterStreakPredictions.Count > 10)
            {
                var afterStreakAccuracy = afterStreakPredictions.Count(p => p.prediction.PredictedState == p.actual) / (double)afterStreakPredictions.Count;
                ConsoleUtilities.WriteLine($"\nAfter 3+ Day Streak Accuracy: {afterStreakAccuracy:P2} (n={afterStreakPredictions.Count})");
            }

            // Statistical significance test
            var randomAccuracy = 1.0 / 3.0; // 33% for 3-state model
            var standardError = Math.Sqrt(randomAccuracy * (1 - randomAccuracy) / totalPredictions);
            var zScore = (directionalAccuracy - randomAccuracy) / standardError;
            var pValue = 2.0 * (1.0 - NormalCDF(Math.Abs(zScore))); // Two-tailed test
            
            ConsoleUtilities.WriteLine($"\n=== STATISTICAL SIGNIFICANCE ===");
            ConsoleUtilities.WriteLine($"Random Baseline: {randomAccuracy:P2}");
            ConsoleUtilities.WriteLine($"Z-Score: {zScore:F2}");
            ConsoleUtilities.WriteLine($"P-Value: {pValue:F4}");
            ConsoleUtilities.WriteLine($"Statistically Significant: {(pValue < 0.05 ? "YES" : "NO")}");
            
            // Calculate FAIR buy-and-hold comparison using actual average allocation
            var startPrice = window[trainingWindowDays].Close;
            var endPrice = window[window.Length - 1].Close;
            var marketReturn = (endPrice - startPrice) / startPrice; // Raw market return
            
            // Calculate average allocation and leverage actually used by strategy
            var totalCapitalDeployed = 0.0;
            var totalLeverageUsed = 0.0;
            var deploymentDays = 0;
            foreach (var trade in tradeLog)
            {
                if (trade.action.Contains("BUY"))
                {
                    var allocation = trade.cost / (trade.cash + trade.cost); // % deployed (can be >100% with leverage)
                    totalCapitalDeployed += allocation;
                    deploymentDays++;
                    
                    // Track leverage multiplier
                    if (allocation > 1.0)
                        totalLeverageUsed += allocation;
                }
            }
            var avgAllocation = deploymentDays > 0 ? totalCapitalDeployed / deploymentDays : 0.50;
            var avgLeverage = totalLeverageUsed > 0 ? totalLeverageUsed / deploymentDays : 1.0;
            
            // Fair Buy & Hold: Apply market return to average allocation actually used
            var fairBuyHoldReturn = marketReturn * avgAllocation * 100.0;
            var naiveBuyHoldReturn = marketReturn * 100.0; // 100% allocation unleveraged
            var leveragedBuyHoldReturn = marketReturn * avgAllocation * avgLeverage * 100.0; // FIX: use avgLeverage
            
            ConsoleUtilities.WriteLine($"\n=== PERFORMANCE COMPARISON (LEVERAGE ADJUSTED) ===");
            ConsoleUtilities.WriteLine($"Market Return (100% unleveraged):    {naiveBuyHoldReturn:F2}%");
            ConsoleUtilities.WriteLine($"Strategy Average Allocation:         {avgAllocation:P1} (avg leverage: {avgLeverage:F2}x)");
            ConsoleUtilities.WriteLine($"Fair B&H (same allocation):          {fairBuyHoldReturn:F2}%");
            ConsoleUtilities.WriteLine($"Leveraged B&H (same leverage):       {leveragedBuyHoldReturn:F2}%");
            ConsoleUtilities.WriteLine($"Markov Strategy Return:              {totalReturn:F2}%");
            ConsoleUtilities.WriteLine($"Alpha vs Fair B&H:                   {(totalReturn - fairBuyHoldReturn):F2}%");
            ConsoleUtilities.WriteLine($"Alpha vs Leveraged B&H:              {(totalReturn - leveragedBuyHoldReturn):F2}%");
            ConsoleUtilities.WriteLine($"Alpha vs 100% Unleveraged B&H:       {(totalReturn - naiveBuyHoldReturn):F2}%");
            
            // Show leverage efficiency
            ConsoleUtilities.WriteLine($"\nLeverage Efficiency:");
            ConsoleUtilities.WriteLine($"  Days with positions: {deploymentDays} of {totalPredictions} ({(double)deploymentDays/totalPredictions:P1})");
            ConsoleUtilities.WriteLine($"  Average position size: {avgAllocation:P1}");
            ConsoleUtilities.WriteLine($"  Average leverage multiplier: {avgLeverage:F2}x");
            ConsoleUtilities.WriteLine($"  Theoretical max with 2x leverage: {(naiveBuyHoldReturn * 2):F2}%");
            ConsoleUtilities.WriteLine($"  Strategy capture rate: {(totalReturn / (naiveBuyHoldReturn * avgLeverage)):P1}");

            // Assertions for real predictive power and trading performance
            Assert.IsTrue(directionalAccuracy > randomAccuracy + 0.05, 
                $"Directional accuracy ({directionalAccuracy:P2}) should be at least 5% better than random ({randomAccuracy:P2})");
            Assert.IsTrue(pValue < 0.05, 
                $"Results should be statistically significant (p={pValue:F4} should be < 0.05)");
            Assert.IsTrue(totalPredictions > 200, 
                "Should have substantial number of predictions for statistical validity");
            
            // Report results
            ConsoleUtilities.WriteLine($"\nStrategy Profitability: {(totalReturn > 0 ? "PROFITABLE" : "LOSS")}");
            ConsoleUtilities.WriteLine($"Fair Comparison (same allocation): {(totalReturn > fairBuyHoldReturn ? "BEATS Fair B&H" : "TRAILS Fair B&H")}");
            ConsoleUtilities.WriteLine($"Leverage Comparison: {(totalReturn > leveragedBuyHoldReturn ? "BEATS Leveraged B&H" : "TRAILS Leveraged B&H")}");
        }

        private MarketContext CalculateMarketContextForTest(PriceRecord[] data, int currentIndex)
        {
            var context = new MarketContext();
            
            if (currentIndex >= 20)
            {
                var returns = new List<double>();
                for (int i = currentIndex - 19; i <= currentIndex; i++)
                {
                    var ret = (data[i].Close - data[i - 1].Close) / data[i - 1].Close;
                    returns.Add(ret);
                }
                var avgReturn = returns.Average();
                var variance = returns.Select(r => Math.Pow(r - avgReturn, 2)).Average();
                context.Volatility = Math.Sqrt(variance * 252);
                
                // Calculate volatility rank
                if (currentIndex >= 100)
                {
                    var volatilities = new List<double>();
                    for (int i = currentIndex - 99; i <= currentIndex - 20; i++)
                    {
                        if (i >= 20)
                        {
                            var histReturns = new List<double>();
                            for (int j = i - 19; j <= i; j++)
                            {
                                if (j > 0 && j < data.Length)
                                {
                                    var ret = (data[j].Close - data[j - 1].Close) / data[j - 1].Close;
                                    histReturns.Add(ret);
                                }
                            }
                            if (histReturns.Count >= 19)
                            {
                                var histAvg = histReturns.Average();
                                var histVar = histReturns.Select(r => Math.Pow(r - histAvg, 2)).Average();
                                volatilities.Add(Math.Sqrt(histVar * 252));
                            }
                        }
                    }
                    if (volatilities.Count > 0)
                    {
                        context.VolatilityRank = volatilities.Count(v => v < context.Volatility) / (double)volatilities.Count;
                    }
                }
            }
            
            if (currentIndex >= 50)
            {
                var sma50Old = data.Skip(currentIndex - 50).Take(25).Average(r => r.Close);
                var sma50New = data.Skip(currentIndex - 25).Take(25).Average(r => r.Close);
                context.Trend = (sma50New - sma50Old) / sma50Old;
                
                // Short-term vs medium-term trend
                if (currentIndex >= 20)
                {
                    var sma5 = data.Skip(currentIndex - 4).Take(5).Average(r => r.Close);
                    var sma20 = data.Skip(currentIndex - 19).Take(20).Average(r => r.Close);
                    context.ShortTermTrend = (sma5 - sma20) / sma20;
                }
            }
            
            if (currentIndex >= 20 && data[currentIndex].Volume > 0)
            {
                var avgVolume = data.Skip(currentIndex - 20).Take(20).Average(r => r.Volume);
                context.RelativeVolume = avgVolume > 0 ? data[currentIndex].Volume / avgVolume : 1.0;
            }
            
            if (currentIndex >= 14)
            {
                var gains = 0.0;
                var losses = 0.0;
                for (int i = currentIndex - 13; i <= currentIndex; i++)
                {
                    var change = data[i].Close - data[i - 1].Close;
                    if (change > 0) gains += change;
                    else losses += Math.Abs(change);
                }
                var rs = losses > 0 ? gains / losses : 100.0;
                context.MomentumScore = 100.0 - (100.0 / (1.0 + rs));
            }
            
            // Gap size
            if (currentIndex >= 1)
            {
                context.GapSize = (data[currentIndex].Open - data[currentIndex - 1].Close) / data[currentIndex - 1].Close * 100.0;
            }
            
            // Intraday momentum
            context.IntradayMomentum = (data[currentIndex].Close - data[currentIndex].Open) / data[currentIndex].Open * 100.0;
            
            // Consecutive days count
            if (currentIndex >= 5)
            {
                var consecutive = 1;
                var currentDirection = data[currentIndex].Close > data[currentIndex - 1].Close;
                for (int i = currentIndex - 1; i >= Math.Max(1, currentIndex - 9); i--)
                {
                    var direction = data[i].Close > data[i - 1].Close;
                    if (direction == currentDirection)
                        consecutive++;
                    else
                        break;
                }
                context.ConsecutiveDays = currentDirection ? consecutive : -consecutive;
            }
            
            // Average True Range
            if (currentIndex >= 14)
            {
                var trValues = new List<double>();
                for (int i = currentIndex - 13; i <= currentIndex; i++)
                {
                    var highLow = data[i].High - data[i].Low;
                    var highClose = Math.Abs(data[i].High - data[i - 1].Close);
                    var lowClose = Math.Abs(data[i].Low - data[i - 1].Close);
                    trValues.Add(Math.Max(highLow, Math.Max(highClose, lowClose)));
                }
                context.ATR = trValues.Average();
            }
            
            // Recent max drawdown
            if (currentIndex >= 10)
            {
                var maxPrice = data.Skip(currentIndex - 9).Take(10).Max(r => r.High);
                var minAfterMax = double.MaxValue;
                var foundMax = false;
                for (int i = currentIndex - 9; i <= currentIndex; i++)
                {
                    if (!foundMax && data[i].High >= maxPrice)
                        foundMax = true;
                    if (foundMax && data[i].Low < minAfterMax)
                        minAfterMax = data[i].Low;
                }
                if (foundMax && minAfterMax != double.MaxValue)
                {
                    context.RecentMaxDrawdown = (minAfterMax - maxPrice) / maxPrice * 100.0;
                }
            }
            
            return context;
        }

        // Cumulative distribution function for standard normal distribution
        private double NormalCDF(double x)
        {
            return 0.5 * (1.0 + Erf(x / Math.Sqrt(2.0)));
        }

        // Error function approximation
        private double Erf(double x)
        {
            var sign = x >= 0 ? 1 : -1;
            x = Math.Abs(x);
            
            var a1 =  0.254829592;
            var a2 = -0.284496736;
            var a3 =  1.421413741;
            var a4 = -1.453152027;
            var a5 =  1.061405429;
            var p  =  0.3275911;
            
            var t = 1.0 / (1.0 + p * x);
            var y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);
            
            return sign * y;
        }

        [TestMethod]
        [TestCategory("Markov")]
        public void MarkovChain_StateClassification_BoundaryValidation()
        {
            var markovChain = new EnhancedMarkovChain(2);

            // Test new 3-state classification with more conservative thresholds
            Assert.AreEqual(MarketState.Down, markovChain.ClassifyMove(-0.5));
            Assert.AreEqual(MarketState.Flat, markovChain.ClassifyMove(0.0));
            Assert.AreEqual(MarketState.Up, markovChain.ClassifyMove(0.5));

            // Test boundaries
            Assert.AreEqual(MarketState.Down, markovChain.ClassifyMove(-0.3));
            Assert.AreEqual(MarketState.Flat, markovChain.ClassifyMove(-0.29));
            Assert.AreEqual(MarketState.Flat, markovChain.ClassifyMove(0.29));
            Assert.AreEqual(MarketState.Up, markovChain.ClassifyMove(0.3));

            ConsoleUtilities.WriteLine("Enhanced state classification boundaries validated successfully");
            ConsoleUtilities.WriteLine("Using 3-state model with ±0.3% thresholds for better noise reduction");
        }
    }
}