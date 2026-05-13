# Polygon.cs - Option Request Generator with Minute Data Support

## Overview

The `Polygon.cs` class integrates with the existing `Prices.cs` class to automatically generate distinct lists of call and put option requests for retrieval from the Polygon.io API. It walks forward through your price data and builds option requests for strikes around the current price and expiration dates in the future.

**NEW: Enhanced with comprehensive minute data support for intraday analysis and extended historical backtesting.**

## Key Features

- **Price Data Integration**: Seamlessly works with existing `Prices.cs` price data
- **Multiple Timeframes**: Support for Daily (D1) and Minute (M1) data processing
- **?? NEW: Minute Data Retrieval**: Get minute-level data for specific dates, ranges, and periods
- **?? NEW: Extended Historical Analysis**: Retrieve 100+ days of minute data for backtesting
- **Distinct Request Generation**: Automatically deduplicates option requests
- **Configurable Parameters**: Customize strikes away and days to expiration
- **Multiple Strike Ranges**: Intelligent strike price generation based on underlying price
- **Business Day Calculation**: Properly calculates expiration dates excluding weekends
- **Standard Option Symbols**: Generates industry-standard option symbols
- **Polygon.io API Integration**: Ready-to-use API endpoints for data retrieval
- **Batch Processing**: Efficient parallel API requests with rate limiting
- **File I/O Support**: Save/load option requests to/from JSON files
- **?? NEW: Data Quality Analysis**: Comprehensive minute data coverage and quality reports

## Basic Usage
// 1. Load your price data
var prices = new Prices(Constants.SPX_JSON);

// 2. Create Polygon instance
var polygon = new Polygon(
    prices: prices,
    apiKey: "YOUR_POLYGON_API_KEY",
    baseSymbol: "SPY",
    strikesAway: 10,    // 10 strikes on each side of current price
    daysAway: 10        // 10 business days to expiration
);

// 3. Generate option requests for a date range (DEFAULT: Daily data)
var result = polygon.BuildOptionRequests(
    startDate: DateTime.Now.AddDays(-30),
    endDate: DateTime.Now.AddDays(-1)
);

// 4. Display summary
ConsoleUtilities.WriteLine(polygon.GenerateOptionRequestSummary(result));
ConsoleUtilities.WriteLine($"Generated {result.TotalUniqueRequests} unique option requests");
## ?? NEW: Minute Data Functionality

### Get Minute Data for Specific Periods
// Get minute data for a specific day
var dayData = polygon.GetMinuteDataForDay(DateTime.Now.AddDays(-1));
ConsoleUtilities.WriteLine($"Retrieved {dayData.TotalMinutes} minutes of data");

// Get minute data for a date range
var rangeData = polygon.GetMinuteDataForRange(
    startDate: DateTime.Now.AddDays(-7), 
    endDate: DateTime.Now  // EXCLUSIVE end date
);

// Get minute data for recent trading days
var recentData = polygon.GetMinuteDataForRecentDays(30); // Last 30 trading days

// Get minute data for 101 days back (100 days + current day)
var extendedData = polygon.GetMinuteDataFor101DaysBack();
### Minute Data Analysis
// Get detailed minute data summary with coverage analysis
var summary = polygon.GetMinuteDataSummary(
    startDate: DateTime.Now.AddDays(-7),
    endDate: DateTime.Now
);
ConsoleUtilities.WriteLine(summary);

// Example output:
// Minute Data Summary for SPY
// =====================================
// Date Range: 2025-01-08 to 2025-01-15 (exclusive)
// Total Minutes: 1,950
// Trading Days: 5
// Average Minutes/Day: 390.0
// Price Range: $485.25 - $525.75
// Coverage: 100.0% ? Good coverage - minute data appears complete
### Option Requests from Minute Data
// Generate option requests using minute data instead of daily
var minuteOptions = polygon.BuildOptionRequestsFromMinuteData(
    startDate: DateTime.Now.AddDays(-5),
    endDate: DateTime.Now
);

// Or use recent minute data
var recentMinuteOptions = polygon.BuildOptionRequestsFromRecentMinuteData(10); // 10 trading days

// Compare daily vs minute data generation
var dailyOptions = polygon.BuildOptionRequests(start, end, TimeFrame.D1);   // ~100 requests
var minuteOptions = polygon.BuildOptionRequests(start, end, TimeFrame.M1);  // ~50,000 requests
## Parameters

### Constructor Parameters

- **`prices`**: Your loaded `Prices` instance containing historical market data
- **`apiKey`**: Your Polygon.io API key for data retrieval
- **`baseSymbol`**: Base stock symbol (e.g., "SPY", "AAPL", "QQQ")
- **`strikesAway`** (default: 10): Number of strike prices on each side of current price
- **`daysAway`** (default: 10): Number of business days to expiration

### BuildOptionRequests Parameters

- **`startDate`** (optional): Start date for processing (defaults to first price record)
- **`endDate`** (optional): End date for processing (defaults to last price record)  
- **`timeFrame`** (default: D1): Time frame for price data
  - `TimeFrame.D1` - Daily (default, ~1 request per trading day)
  - `TimeFrame.M1` - Minute (390 requests per trading day)
  - `TimeFrame.H1` - Hourly (6.5 requests per trading day)
  - Other timeframes also supported

## ?? NEW: MinuteDataResult Class
public class MinuteDataResult
{
    public List<PriceRecord> MinuteRecords { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalMinutes { get; }               // Total minute records
    public int TradingDays { get; set; }           // Number of trading days
    public double AverageMinutesPerDay { get; }    // Coverage per day
}
## Generated Option Requests

Each option request contains:
public class OptionRequest
{
    public string Symbol { get; set; }           // Base symbol (e.g., "SPY")
    public string OptionSymbol { get; set; }     // Full option symbol (e.g., "SPY250131C00500000")
    public DateTime ExpirationDate { get; set; } // Option expiration date
    public double StrikePrice { get; set; }      // Strike price
    public OptionType OptionType { get; set; }   // Call or Put
    public DateTime RequestDate { get; set; }    // Date when request was generated
    public double UnderlyingPrice { get; set; }  // Underlying price at request time
    public string ApiEndpoint { get; set; }      // Ready-to-use Polygon.io API endpoint
}
## Strike Price Generation

The class intelligently generates strike prices based on the underlying price:

- **Below $25**: $0.50 increments
- **$25-$50**: $1.00 increments  
- **$50-$100**: $1.00 increments
- **$100-$200**: $2.50 increments
- **$200-$500**: $5.00 increments
- **Above $500**: $10.00 increments

## Option Symbol Format

Generated option symbols follow the standard OCC format:
- **SYMBOL** + **YYMMDD** + **C/P** + **8-digit strike**
- Example: `SPY250131C00500000` = SPY Jan 31, 2025 $500 Call

## ?? NEW: Data Quality and Coverage Analysis

### Coverage Metrics// Automatic coverage analysis in minute data summary
var summary = polygon.GetMinuteDataSummary(start, end);

// Coverage indicators:
// ? 95-105%: Good coverage (normal market hours)
// ??  <95%: Low coverage (missing data)
// ??  >105%: High coverage (extended hours data)
### Expected vs Actual Data
- **Expected**: 390 minutes per trading day (9:30 AM - 4:15 PM ET)
- **Actual**: Measured from your data
- **Coverage %**: (Actual / Expected) × 100

## ?? Real-World Use Cases

### 1. Intraday Option Pricing Models// Get minute data for model validation
var todayMinutes = polygon.GetMinuteDataForDay(DateTime.Now.Date);
// Use todayMinutes.MinuteRecords for pricing model calibration
### 2. Historical Volatility Calculation// 30 days of minute data for volatility models
var volData = polygon.GetMinuteDataForRecentDays(30);
// Calculate realized volatility from minute returns
### 3. Option Flow Analysis// Market opening hour analysis
var marketOpen = DateTime.Today.AddHours(9).AddMinutes(30);
var firstHour = marketOpen.AddHours(1);
var openingFlow = polygon.BuildOptionRequestsFromMinuteData(marketOpen, firstHour);
### 4. Extended Backtesting// 101 days of minute data for comprehensive backtesting
var backtest = polygon.GetMinuteDataFor101DaysBack();
// Provides ~39,000 minute observations for robust analysis
## API Integration

### Single Requestvar optionData = await polygon.GetOptionDataAsync(optionRequest);
### Batch Requests (with rate limiting)var batchResults = await polygon.GetOptionDataBatchAsync(
    optionRequests: result.AllRequests.Take(50),
    maxConcurrent: 5  // Respect API rate limits
);
## File Operations

### Save Requestspolygon.SaveOptionRequestsToFile(result, "SPY_option_requests.json");
### Load Requestsvar loadedResult = Polygon.LoadOptionRequestsFromFile("SPY_option_requests.json");
## Example Output

### Daily Data (Traditional)Option Request Summary for SPY
================================
Date Range: 2025-01-01 to 2025-01-31
Trading Days Processed: 22
Parameters: 10 strikes away, 10 days out

Total Unique Call Requests: 1,540
Total Unique Put Requests: 1,540
Total Unique Requests: 3,080
### ?? NEW: Minute Data (Enhanced)Option Request Summary for SPY
================================
Date Range: 2025-01-01 to 2025-01-31  
M1 Records Processed: 8,580 (22 trading days)
Parameters: 10 strikes away, 10 days out

Total Unique Call Requests: 42,900
Total Unique Put Requests: 42,900
Total Unique Requests: 85,800

Coverage: 98.5% ? Excellent minute data coverage
## ?? Advanced Minute Data Analysis
// Comprehensive analysis workflow
var polygon = new Polygon(prices, apiKey, "SPY", 10, 10);

// 1. Data quality assessment
var qualitySummary = polygon.GetMinuteDataSummary(startDate, endDate);

// 2. Coverage verification
var minuteData = polygon.GetMinuteDataForRange(startDate, endDate);
if (minuteData.AverageMinutesPerDay < 350) {
    ConsoleUtilities.WriteLine("??  Low minute data coverage detected");
}

// 3. Option request generation
var minuteOptions = polygon.BuildOptionRequestsFromMinuteData(startDate, endDate);

// 4. Density analysis by hour
var hourlyDensity = minuteOptions.AllRequests
    .GroupBy(r => r.RequestDate.Hour)
    .Select(g => new { Hour = g.Key, Count = g.Count() });
## Performance Considerations

### Daily vs Minute Data
- **Daily**: ~1-50 requests per day (fast, traditional)
- **Minute**: ~2,000-20,000 requests per day (comprehensive, detailed)

### Memory Usage
- **Daily**: Minimal memory footprint
- **Minute**: ~390x more data points per day
- **Recommendation**: Use minute data for recent periods, daily for historical

### Processing Speed
- **Daily**: Near-instantaneous for months of data
- **Minute**: Seconds for weeks, minutes for months
- **Built-in Progress Logging**: Track processing for large datasets

## ?? Migration Guide

### From Daily to Minute Data// OLD: Daily data only
var result = polygon.BuildOptionRequests(start, end); // Uses D1 by default

// NEW: Explicit timeframe selection
var dailyResult = polygon.BuildOptionRequests(start, end, TimeFrame.D1);
var minuteResult = polygon.BuildOptionRequests(start, end, TimeFrame.M1);

// NEW: Convenience methods for minute data
var minuteResult = polygon.BuildOptionRequestsFromMinuteData(start, end);
var recentResult = polygon.BuildOptionRequestsFromRecentMinuteData(30);
## Error Handling

The class includes comprehensive error handling:

- **Null parameter validation**
- **Invalid date range handling**  
- **Missing minute data graceful fallback**
- **API request error handling**
- **File I/O error handling**
- **Network timeout handling**
- **?? NEW: Data coverage warnings and recommendations**

## Rate Limiting

When making API calls, the class includes built-in rate limiting:

- **Configurable concurrency**: Default 5 concurrent requests
- **Request delays**: 100ms between requests
- **Error retry logic**: Graceful handling of API errors
- **Progress reporting**: Status updates for large batches

## Integration with Existing Code

The `Polygon.cs` class is designed