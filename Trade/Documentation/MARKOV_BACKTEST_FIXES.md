# Markov Backtest Fixes - Apply These Changes

## Summary of Required Fixes

This document contains all the fixes needed to make the Markov backtest more accurate and functional.

## 1. Fix Analytics Bugs

### 1.1 Leveraged B&H Calculation (Line ~1015)

**Find:**
```csharp
var leveragedBuyHoldReturn = marketReturn * avgAllocation * 100.0;
```

**Replace with:**
```csharp
var leveragedBuyHoldReturn = marketReturn * avgAllocation * avgLeverage * 100.0;
```

### 1.2 Average Losing Trade Parsing (Line ~862)

**Find:**
```csharp
var avgLosingTrade = losingTrades > 0 ?
    tradeLog.Where(t => t.reason.Contains("PnL: -$"))
           .Select(t => {
               var pnlStart = t.reason.IndexOf("PnL: -$") + 7;
               var pnlEnd = t.reason.IndexOf(",", pnlStart);
               if (double.TryParse(t.reason.Substring(pnlStart, pnlEnd - pnlStart).Replace(",", ""), out var pnl))
                   return pnl;
               return 0.0;
           })
           .Where(p => p > 0)
           .DefaultIfEmpty(0)
           .Average() : 0;
```

**Replace with:**
```csharp
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
```

### 1.3 Confidence Cap in PredictWithContext (Line ~386)

**Find:**
```csharp
return new MarkovPrediction
{
    PredictedState = bestPrediction.Key,
    Confidence = selectedPattern.ConfidenceScore * confidenceBoost,
    ContextScore = contextScore,
    Probability = bestPrediction.Value,
    SampleSize = selectedPattern.TotalOccurrences,
    Message = $"Based on {selectedPattern.TotalOccurrences} occurrences in {(useRegimePattern ? regimeKey + " regime" : "global")}, context match: {contextScore:F2}"
};
```

**Replace with:**
```csharp
return new MarkovPrediction
{
    PredictedState = bestPrediction.Key,
    Confidence = Math.Min(1.0, selectedPattern.ConfidenceScore * confidenceBoost),
    ContextScore = contextScore,
    Probability = bestPrediction.Value,
    SampleSize = selectedPattern.TotalOccurrences,
    Message = $"Based on {selectedPattern.TotalOccurrences} occurrences in {(useRegimePattern ? regimeKey + " regime" : "global")}, context match: {contextScore:F2}"
};
```

### 1.4 Two-Tailed P-Value (Line ~993)

**Find:**
```csharp
var zScore = (directionalAccuracy - randomAccuracy) / standardError;
var pValue = 1.0 - NormalCDF(zScore);
```

**Replace with:**
```csharp
var zScore = (directionalAccuracy - randomAccuracy) / standardError;
var pValue = 2.0 * (1.0 - NormalCDF(Math.Abs(zScore)));
```

### 1.5 Trade Type Counting with REGIME Tag (Lines ~685, ~935)

**Bull entry logging (Line ~685):**

**Find:**
```csharp
tradeLog.Add((currentDate, "BUY (Bull 2x)", currentPrice, currentPosition, investAmount,
    currentCash, currentCash + investAmount, 
    $"Bull 2x leverage (trend: {currentContext.Trend:F2}, vol: {currentContext.Volatility:F2}, mom: {currentContext.MomentumScore:F0})"));
```

**Replace with:**
```csharp
tradeLog.Add((currentDate, "BUY (Bull 2x)", currentPrice, currentPosition, investAmount,
    currentCash, currentCash + investAmount, 
    $"REGIME=BULL | Bull 2x leverage (trend:{currentContext.Trend:F2}, vol:{currentContext.Volatility:F2}, mom:{currentContext.MomentumScore:F0})"));
```

**Trade counting (Line ~935):**

**Find:**
```csharp
var bullTrades = tradeLog.Where(t => t.reason.Contains("Bull market detected") || t.reason.Contains("Bull Hold")).Count();
var markovTrades = tradeLog.Where(t => t.reason.Contains("Markov") || t.reason.Contains("Pred:")).Count();
var defensiveTrades = tradeLog.Where(t => t.reason.Contains("Defensive") || t.reason.Contains("Passive")).Count();
```

**Replace with:**
```csharp
var bullTrades = tradeLog.Count(t => t.reason.Contains("REGIME=BULL"));
var markovTrades = tradeLog.Count(t => t.reason.Contains("Pred:"));
var defensiveTrades = tradeLog.Count(t => t.action.Contains("Defensive"));
```

## 2. Loosen Thresholds to Make Regimes Reachable

### 2.1 Bull Market Detection (Line ~676)

**Find:**
```csharp
var isBullMarket = currentContext.Trend > 0.08 && 
                  currentContext.Volatility < 0.30 &&
                  currentContext.MomentumScore > 45;
```

**Replace with:**
```csharp
var isBullMarket = currentContext.Trend > 0.04 &&     // Loosened from 0.08
                  currentContext.Volatility < 0.28 && // Loosened from 0.30
                  currentContext.MomentumScore > 50;   // Tightened from 45
```

### 2.2 Markov Entry Gate (Line ~731)

**Find:**
```csharp
var isHighConfidence = prediction.Confidence > 0.60 && 
                      prediction.ContextScore > 0.70 && 
                      prediction.SampleSize >= 18 &&
                      prediction.Probability > 0.52;
```

**Replace with:**
```csharp
var isHighConfidence = prediction.SampleSize >= 18 &&
                      prediction.Probability > 0.52 &&
                      prediction.Confidence > 0.58 &&   // Loosened from 0.60
                      prediction.ContextScore > 0.68;   // Loosened from 0.70
```

## 3. Add Expected-Move Logic

### 3.1 Add ExpectedMoveBp Helper Method (Add after line 502, inside EnhancedMarkovChain class)

**Add this method:**
```csharp
private static double ExpectedMoveBp(Dictionary<MarketState, double> probs)
{
    // Typical day magnitudes in basis points
    const double muDown = -0.45; // -0.45%
    const double muFlat = 0.00;
    const double muUp = 0.45;   // +0.45%

    probs.TryGetValue(MarketState.Down, out var pD);
    probs.TryGetValue(MarketState.Flat, out var pF);
    probs.TryGetValue(MarketState.Up, out var pU);

    return pD * muDown + pF * muFlat + pU * muUp;
}
```

### 3.2 Add ExpectedMove Property to MarkovPrediction (Line ~508)

**Find:**
```csharp
public class MarkovPrediction
{
    public MarketState PredictedState { get; set; }
    public double Confidence { get; set; }
    public double ContextScore { get; set; }
    public double Probability { get; set; }
    public int SampleSize { get; set; }
    public string Message { get; set; }
```

**Add property:**
```csharp
public class MarkovPrediction
{
    public MarketState PredictedState { get; set; }
    public double Confidence { get; set; }
    public double ContextScore { get; set; }
    public double Probability { get; set; }
    public int SampleSize { get; set; }
    public string Message { get; set; }
    public double ExpectedMoveBp { get; set; }  // ADD THIS
```

### 3.3 Calculate Expected Move in PredictWithContext (Line ~387)

**Find:**
```csharp
return new MarkovPrediction
{
    PredictedState = bestPrediction.Key,
    Confidence = Math.Min(1.0, selectedPattern.ConfidenceScore * confidenceBoost),
    ContextScore = contextScore,
    Probability = bestPrediction.Value,
    SampleSize = selectedPattern.TotalOccurrences,
    Message = $"Based on {selectedPattern.TotalOccurrences} occurrences in {(useRegimePattern ? regimeKey + " regime" : "global")}, context match: {contextScore:F2}"
};
```

**Replace with:**
```csharp
var expMove = ExpectedMoveBp(selectedPattern.NextStateProbabilities);

return new MarkovPrediction
{
    PredictedState = bestPrediction.Key,
    Confidence = Math.Min(1.0, selectedPattern.ConfidenceScore * confidenceBoost),
    ContextScore = contextScore,
    Probability = bestPrediction.Value,
    SampleSize = selectedPattern.TotalOccurrences,
    ExpectedMoveBp = expMove,
    Message = $"Based on {selectedPattern.TotalOccurrences} occurrences in {(useRegimePattern ? regimeKey + " regime" : "global")}, context match: {contextScore:F2}, exp: {expMove:F2}bp"
};
```

### 3.4 Use Expected Move in Trading Logic (Line ~738)

**Find:**
```csharp
if (isHighConfidence)
{
    if (prediction.PredictedState == MarketState.Up)
    {
        // Scale position size by confidence with 2x leverage (100-170% of capital)
```

**Replace with:**
```csharp
if (isHighConfidence)
{
    // Use expected move to break ties
    const double longThreshBp = +0.10;
    const double shortThreshBp = -0.10;
    
    var expUpTilt = prediction.ExpectedMoveBp >= longThreshBp;
    var expDownTilt = prediction.ExpectedMoveBp <= shortThreshBp;
    
    var allowLong = prediction.PredictedState == MarketState.Up || expUpTilt;
    var avoidRisk = prediction.PredictedState == MarketState.Down || expDownTilt;
    
    if (allowLong)
    {
        // Scale position size by confidence with 2x leverage (100-170% of capital)
```

**And update the DOWN handling:**

**Find:**
```csharp
        else if (prediction.PredictedState == MarketState.Down)
        {
            // Stay in cash for DOWN predictions
            tradeLog.Add((currentDate, "HOLD CASH", currentPrice, 0, 0,
                currentCash, currentCash, 
                $"Pred: DOWN, Conf: {prediction.Confidence:F2}, Ctx: {prediction.ContextScore:F2}, Protecting capital"));
        }
```

**Replace with:**
```csharp
        else if (avoidRisk)
        {
            // Stay in cash for DOWN predictions or negative expected move
            tradeLog.Add((currentDate, "HOLD CASH", currentPrice, 0, 0,
                currentCash, currentCash, 
                $"Pred/Exp: DOWN/tilt, Conf:{prediction.Confidence:F2}, Ctx:{prediction.ContextScore:F2}, ExpMove:{prediction.ExpectedMoveBp:F2}bp"));
        }
```

## 4. Make Context Similarity Fair

### 4.1 Accumulate All Context Features During Training (Line ~195)

**Find:**
```csharp
// Accumulate context information
markovPattern.AverageContext.Volatility += currentContext.Volatility;
markovPattern.AverageContext.Trend += currentContext.Trend;
markovPattern.AverageContext.RelativeVolume += currentContext.RelativeVolume;
markovPattern.AverageContext.MomentumScore += currentContext.MomentumScore;

regimePattern.AverageContext.Volatility += currentContext.Volatility;
regimePattern.AverageContext.Trend += currentContext.Trend;
regimePattern.AverageContext.RelativeVolume += currentContext.RelativeVolume;
regimePattern.AverageContext.MomentumScore += currentContext.MomentumScore;
```

**Replace with:**
```csharp
// Accumulate context information (ALL features)
markovPattern.AverageContext.Volatility += currentContext.Volatility;
markovPattern.AverageContext.Trend += currentContext.Trend;
markovPattern.AverageContext.RelativeVolume += currentContext.RelativeVolume;
markovPattern.AverageContext.MomentumScore += currentContext.MomentumScore;
markovPattern.AverageContext.ShortTermTrend += currentContext.ShortTermTrend;
markovPattern.AverageContext.VolatilityRank += currentContext.VolatilityRank;
markovPattern.AverageContext.ConsecutiveDays += currentContext.ConsecutiveDays;
markovPattern.AverageContext.IntradayMomentum += currentContext.IntradayMomentum;
markovPattern.AverageContext.GapSize += currentContext.GapSize;
markovPattern.AverageContext.ATR += currentContext.ATR;
markovPattern.AverageContext.RecentMaxDrawdown += currentContext.RecentMaxDrawdown;

regimePattern.AverageContext.Volatility += currentContext.Volatility;
regimePattern.AverageContext.Trend += currentContext.Trend;
regimePattern.AverageContext.RelativeVolume += currentContext.RelativeVolume;
regimePattern.AverageContext.MomentumScore += currentContext.MomentumScore;
regimePattern.AverageContext.ShortTermTrend += currentContext.ShortTermTrend;
regimePattern.AverageContext.VolatilityRank += currentContext.VolatilityRank;
regimePattern.AverageContext.ConsecutiveDays += currentContext.ConsecutiveDays;
regimePattern.AverageContext.IntradayMomentum += currentContext.IntradayMomentum;
regimePattern.AverageContext.GapSize += currentContext.GapSize;
regimePattern.AverageContext.ATR += currentContext.ATR;
regimePattern.AverageContext.RecentMaxDrawdown += currentContext.RecentMaxDrawdown;
```

### 4.2 Divide All Context Features in FinalizePatterns (Line ~226)

**Find:**
```csharp
// Average the context
if (pattern.TotalOccurrences > 0)
{
    pattern.AverageContext.Volatility /= pattern.TotalOccurrences;
    pattern.AverageContext.Trend /= pattern.TotalOccurrences;
    pattern.AverageContext.RelativeVolume /= pattern.TotalOccurrences;
    pattern.AverageContext.MomentumScore /= pattern.TotalOccurrences;
}
```

**Replace with:**
```csharp
// Average the context (ALL features)
if (pattern.TotalOccurrences > 0)
{
    pattern.AverageContext.Volatility /= pattern.TotalOccurrences;
    pattern.AverageContext.Trend /= pattern.TotalOccurrences;
    pattern.AverageContext.RelativeVolume /= pattern.TotalOccurrences;
    pattern.AverageContext.MomentumScore /= pattern.TotalOccurrences;
    pattern.AverageContext.ShortTermTrend /= pattern.TotalOccurrences;
    pattern.AverageContext.VolatilityRank /= pattern.TotalOccurrences;
    pattern.AverageContext.ConsecutiveDays /= pattern.TotalOccurrences;
    pattern.AverageContext.IntradayMomentum /= pattern.TotalOccurrences;
    pattern.AverageContext.GapSize /= pattern.TotalOccurrences;
    pattern.AverageContext.ATR /= pattern.TotalOccurrences;
    pattern.AverageContext.RecentMaxDrawdown /= pattern.TotalOccurrences;
}
```

## 5. Clean Up Duplications

### 5.1 Remove Duplicate actualChange Declaration (Line ~660)

**Find (around line 660):**
```csharp
// Calculate actual next day move
var actualChange = (testWindow[testIdx + 1].Close - testWindow[testIdx].Close) / testWindow[testIdx].Close * 100.0;
var nextDayPrice = testWindow[testIdx + 1].Close;
var actualChange = (nextDayPrice - currentPrice) / currentPrice * 100.0;
```

**Replace with:**
```csharp
// Calculate actual next day move
var nextDayPrice = testWindow[testIdx + 1].Close;
var actualChange = (nextDayPrice - currentPrice) / currentPrice * 100.0;
```

### 5.2 Remove Duplicate if Conditions in CalculateMarketContextForTest (Line ~1055)

**Find patterns like:**
```csharp
if (i >= 20) // Ensure we have enough history
if (i >= 20)
{
```

**Keep only one:**
```csharp
if (i >= 20) // Ensure we have enough history
{
```

## Acceptance Criteria

After applying all fixes, the test run should show:

1. ? **Leveraged B&H** differs from Fair B&H when avgLeverage ? 1.0
2. ? **Average Losing Trade** shows actual loss amounts (not -0.00)
3. ? **Bull Market Entries** > 0 (bull regime is reachable)
4. ? **Markov Signal Trades** > 0 (signals fire)
5. ? **High Confidence Predictions** still selective but with UP/DOWN counts > 0
6. ? **Days with positions** > 6.4% (increased engagement)
7. ? **Directional accuracy** remains ~38-41%
8. ? **Log shows** "REGIME=BULL" and "Pred: UP 2x" type trades
9. ? **Overall return** moves closer to adjusted B&H

## Implementation Notes

- Apply fixes in order (analytics ? thresholds ? expected move ? context)
- After each section, build to verify syntax
- Run full test after all fixes applied
- Compare before/after metrics

## Quick Build Command

```bash
dotnet build Trade.sln
dotnet test --filter "TestCategory=Markov&FullyQualifiedName~EnhancedMarkovChain_WalkForward_RealPredictivePower"
```
