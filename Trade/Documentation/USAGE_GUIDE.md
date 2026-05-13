# Cluster Buying with Price Action Analysis - Usage Guide

## Quick Start

### 1. Run the Analysis

```csharp
// In Visual Studio Test Explorer:
// 1. Navigate to: Trade.Tests ? SECForm4DownloadTests
// 2. Right-click on: Form4_ClusterBuying_WithPriceActionAnalysis
// 3. Select "Run Test"
```

### 2. Customize the Date Range

```csharp
// Edit the test method in SECForm4DownloadTests.cs:
var endDate = DateTime.Today;
var startDate = endDate.AddDays(-30); // Change -30 to desired lookback
```

### 3. Adjust Processing Limits

```csharp
// Limit Form 4 entries per day (for faster testing):
foreach (var entry in entries.Take(50)) // Change 50 to desired limit

// Limit number of days to process stock indexes:
BuildSortedFileIndexingTests.BuildAllSortedFileIndexesForStocks(
    numberOfSortedFilesToProcess: 90  // Change 90 to desired number
);
```

---

## Understanding the Output

### Signal Detection
```
?? SIGNAL DETECTED: AAPL
  Transaction Date: 2024-12-01      ? When insider bought
  Filing Date: 2024-12-03            ? When public learned (signal date)
  Cluster Score: 215 | Tier: A+     ? ClusterBuyAnalyzer score
```

**Key Point**: We use **Filing Date** as the signal date because that's when the information becomes public and actionable.

### Price Action Metrics
```
  ? Price Action Retrieved:
    1-Day Return: +1.23%    ? Next trading day
    7-Day Return: +3.45%    ? One week later
    30-Day Return: +8.12%   ? One month later
    90-Day Return: +15.67%  ? Three months later
    Max Drawdown: -2.34%    ? Worst peak-to-trough decline
```

**Interpretation:**
- **Positive Returns**: Stock went up after signal
- **Max Drawdown**: Largest unrealized loss if held through worst period
- **Missing Values**: `null` if not enough data (e.g., signal too recent for 90-day return)

### Performance Summary by Tier
```
A+ Tier (3 signals):
  Avg 1-Day:  +1.45% (3 samples)   ? All 3 signals had 1-day data
  Avg 7-Day:  +3.89% (3 samples)
  Avg 30-Day: +9.71% (3 samples)
  Avg 90-Day: +17.25% (2 samples)  ? Only 2 had 90-day data
```

**Interpretation:**
- **A+ Tier**: Highest quality signals (Score ?200, 3+ insiders, strong roles)
- **Sample Count**: Number of signals with data for that horizon
- **Average Return**: Mean return across all samples

---

## Real-World Example Walkthrough

### Scenario: Tim Cook Buys AAPL Stock

**1. Form 4 Filed**
- **Transaction Date**: December 1, 2024 (insider bought)
- **Filing Date**: December 3, 2024 (public learns)
- **Amount**: $2,000,000
- **Role**: CEO
- **Shares**: 11,080 @ $180.50

**2. Cluster Analysis**
```csharp
var clusterMetrics = ClusterBuyAnalyzer.UpdateAndScore(
    ticker: "AAPL",
    aggregatePurchaseValue: 2000000m,
    ownerNames: ["Tim Cook"],
    roles: ["Chief Executive Officer"]
);

// Result:
// Score: 215
// Tier: A+ (high confidence signal)
```

**3. Signal Filter**
```csharp
var shouldSignal = aggregatePurchaseValue >= 500000m && isDirectorOrOfficer;
// Result: true (passes filter)
```

**4. Price Action Lookup**
```csharp
// System uses indexes to find AAPL price data efficiently:
// - Loads index for 2024-12-03_Sorted.csv
// - Seeks to AAPL byte offset: 15234
// - Reads 15,162 bytes (all AAPL data for that day)
// - Repeats for 90 days of files
// - Total time: ~5 seconds

var priceAction = AnalyzePriceActionAfterSignal(
    ticker: "AAPL",
    signalDate: new DateTime(2024, 12, 3),
    clusterMetrics: clusterMetrics
);
```

**5. Results**
```
Signal Price: $180.50 (12/3/2024 16:00)

Returns:
  1-Day:  $182.72 = +1.23%  (12/4/2024)
  7-Day:  $186.73 = +3.45%  (12/10/2024)
  30-Day: $195.16 = +8.12%  (1/2/2025)
  90-Day: $208.80 = +15.67% (3/3/2025)

Max Drawdown: -2.34%
  Occurred on 12/18/2024 when price dipped to $176.28
  Peak was $180.50 (signal price)
  Drawdown = ($180.50 - $176.28) / $180.50 = 2.34%
```

**6. Trading Strategy Implications**
- **Entry**: Buy on filing date (12/3) @ $180.50
- **30-Day Exit**: Sell on 1/2 @ $195.16 = **+8.12% gain**
- **Max Risk**: Would have been down -2.34% on 12/18
- **Risk/Reward**: 8.12% gain / 2.34% drawdown = **3.47 ratio** ?

---

## Advanced Usage

### 1. Filter by Minimum Score

```csharp
var shouldSignal = aggregatePurchaseValue >= 500000m && 
                   isDirectorOrOfficer && 
                   clusterMetrics.Score >= 200; // A+ tier only
```

### 2. Custom Return Horizons

```csharp
// In CalculatePriceActionMetrics, add new horizons:
var horizons = new[] { 1, 3, 7, 14, 30, 60, 90 }; // Add 3, 14, 60 days
```

### 3. Export to CSV for Excel Analysis

```csharp
// After collecting clusterSignalsWithPriceAction:
var csv = new StringBuilder();
csv.AppendLine("Ticker,SignalDate,Tier,Score,Return1D,Return7D,Return30D,Return90D,MaxDD");

foreach (var signal in clusterSignalsWithPriceAction)
{
    csv.AppendLine($"{signal.Ticker}," +
                   $"{signal.SignalDate:yyyy-MM-dd}," +
                   $"{signal.ClusterMetrics.Tier}," +
                   $"{signal.ClusterMetrics.Score}," +
                   $"{signal.PriceAction.Return1Day:P2}," +
                   $"{signal.PriceAction.Return7Day:P2}," +
                   $"{signal.PriceAction.Return30Day:P2}," +
                   $"{signal.PriceAction.Return90Day:P2}," +
                   $"{signal.PriceAction.MaxDrawdown:P2}");
}

File.WriteAllText("ClusterBuyingResults.csv", csv.ToString());
```

### 4. Statistical Analysis

```csharp
// Calculate Sharpe Ratio for A+ tier signals
var aPlusTier = clusterSignalsWithPriceAction
    .Where(s => s.ClusterMetrics.Tier == "A+")
    .ToList();

var returns30d = aPlusTier
    .Where(s => s.PriceAction.Return30Day.HasValue)
    .Select(s => s.PriceAction.Return30Day.Value)
    .ToList();

var avgReturn = returns30d.Average();
var stdDev = Math.Sqrt(returns30d.Average(r => Math.Pow(r - avgReturn, 2)));
var sharpeRatio = avgReturn / stdDev; // Simplified Sharpe (assumes 0% risk-free rate)

ConsoleUtilities.WriteLine($"A+ Tier Sharpe Ratio: {sharpeRatio:F2}");
```

---

## Troubleshooting

### Issue: "No sorted files found for {ticker}"

**Cause**: Missing stock price data in `PolygonBulkData/`

**Solution**:
```csharp
// Download stock data using Polygon integration:
var polygon = new Polygon(...);
await polygon.DownloadBulkDataFromS3Async(
    symbol: "SPY",  // Or your ticker
    startDate: signalDate.AddDays(-1),
    endDate: signalDate.AddDays(90),
    dataType: "us_stocks_sip/minute_aggs"
);
```

### Issue: "No index found for {file}"

**Cause**: Index files not built

**Solution**:
```csharp
// Force rebuild indexes:
BuildSortedFileIndexingTests.BuildAllSortedFileIndexesForStocks(
    numberOfSortedFilesToProcess: 90
);
```

### Issue: "No price data on/after signal date"

**Cause**: Signal date is in the future or a weekend/holiday

**Solution**:
- Check that `signalDate` is a valid trading day
- Ensure price data exists for that date
- The system automatically skips to next available date

### Issue: Sample count too low

**Cause**: Signals too recent (e.g., only 5 days old, can't calculate 30-day return)

**Solution**:
- Use older date range: `startDate = endDate.AddDays(-90)`
- Accept partial data for recent signals

---

## Performance Optimization Tips

### 1. Build Indexes Once
```csharp
// First run: Builds indexes (takes ~12 seconds for 90 files)
BuildAllSortedFileIndexesForStocks(90);

// Subsequent runs: Uses cached indexes (instant)
BuildAllSortedFileIndexesForStocks(90);
```

### 2. Limit Date Range for Testing
```csharp
// Fast test (7 days):
var startDate = endDate.AddDays(-7);

// Full analysis (90 days):
var startDate = endDate.AddDays(-90);
```

### 3. Parallel Processing (Future Enhancement)
```csharp
// Process multiple tickers in parallel:
Parallel.ForEach(byTicker, tickerGroup => {
    var priceAction = AnalyzePriceActionAfterSignal(...);
});
```

---

## Expected Performance

### Typical Test Run (30 days, 50 entries/day)
```
Form 4 Downloads:     ~5 minutes  (SEC rate limiting)
Index Building:       ~12 seconds (one-time)
Price Lookups:        ~1 minute   (12 signals × 5 sec each)
Total:                ~6-7 minutes
```

### Production Run (90 days, all entries)
```
Form 4 Downloads:     ~30 minutes (rate limiting)
Index Building:       ~30 seconds (90 files)
Price Lookups:        ~5 minutes  (60 signals × 5 sec each)
Total:                ~35-40 minutes
```

Compare to **without indexes**: ~45 hours for same workload! ??

---

## Next Steps

### 1. Validate Results
- Compare to manual stock chart review
- Verify return calculations against broker statements
- Test with known historical signals

### 2. Strategy Development
- Determine optimal entry/exit rules
- Test different score thresholds
- Analyze tier performance differences

### 3. Live Trading Integration
- Connect to real-time Form 4 feed (TypeScript pingers-sec.spec.ts)
- Implement automated trade execution
- Set up position sizing based on cluster score

### 4. Research Questions to Answer
- Do A+ tier signals outperform A/B tier?
- What's the optimal holding period?
- Does signal strength correlate with returns?
- Which insider roles have best predictive power?

---

## Support

### Documentation
- `CLUSTER_ANALYZER_IMPLEMENTATION.md` - Scoring algorithm details
- `PRICE_ACTION_IMPLEMENTATION.md` - Technical implementation
- This file - Usage guide

### Code References
- `Trade/Tests/SECForm4DownloadTests.cs` - Main test file
- `Trade/Tests/ClusterBuyAnalyzer.cs` - Scoring engine
- `Trade/Tests/BuildSortedFileIndexingTests.cs` - Index system

### Questions?
Review the inline code comments and console output for detailed execution flow.

---

**Ready to analyze cluster buying patterns with institutional-grade efficiency!** ??
