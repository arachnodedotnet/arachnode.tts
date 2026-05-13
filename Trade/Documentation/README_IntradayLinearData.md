# Intraday Linear Price Data Generator

## Overview
This module generates comprehensive intraday price data with linear appreciation and depreciation patterns over 10-day cycles, matching the date ranges from the original SPX daily data file.

## Key Features

### ?? **Data Specifications**
- **Date Range**: 2024-05-01 to 2025-08-01 (matching `^spx_d.csv`)
- **Time Resolution**: Minute-by-minute bars (390 minutes per trading day)
- **Market Hours**: 9:30 AM - 4:15 PM EST
- **Cycle Pattern**: 10 trading days per cycle
  - Days 1-5: Linear price appreciation
  - Days 6-10: Linear price depreciation
- **Price Range**: ~5000 ± 500 points per cycle
- **Format**: `DateTime,Open,High,Low,Close,Volume`

### ?? **Pattern Structure**
```
Day 1-5:  ?? Linear Appreciation (5000 ? 5500)
Day 6-10: ?? Linear Depreciation (5500 ? 5000)
Repeat cycle...
```

### ?? **Intraday Characteristics**
- **Linear Progression**: Each trading day shows smooth linear price movement
- **Realistic OHLC**: Proper High ? max(Open,Close), Low ? min(Open,Close)
- **Volume Patterns**: 
  - Higher volume at market open (first hour)
  - Higher volume at market close (last hour)  
  - Lower volume during lunch (mid-day)
  - Random variations around base volume

### ?? **Generated Data Scale**
- **Total Records**: ~100,000+ minute bars
- **File Size**: ~5-10 MB
- **Granularity**: 390x more detailed than daily data
- **Trading Days**: ~300+ days covered

## Usage Examples

### Basic Generation
```csharp
var generator = new IntradayLinearDataGeneratorTests();
generator.TestGenerateActualIntradayLinearFile();
// Creates: intraday_linear_spx_data.csv
```

### Custom Parameters
```csharp
var startDate = new DateTime(2024, 5, 1);
var endDate = new DateTime(2025, 8, 1);
var cycleLengthDays = 10;
var minutesPerDay = 390;

GenerateIntradayLinearDataFile("custom_intraday.csv", 
    startDate, endDate, cycleLengthDays, minutesPerDay);
```

## Data Format

### Sample Output
```csv
DateTime,Open,High,Low,Close,Volume
2024-05-01 09:30:00,5000.00,5001.25,4999.80,5000.85,2150000
2024-05-01 09:31:00,5000.85,5002.15,5000.45,5001.70,1890000
2024-05-01 09:32:00,5001.70,5003.05,5001.25,5002.55,1750000
...
2024-05-01 15:59:00,5012.30,5013.15,5011.90,5012.82,2200000
```

### Column Descriptions
- **DateTime**: Timestamp in `yyyy-MM-dd HH:mm:ss` format
- **Open**: Opening price for the minute
- **High**: Highest price during the minute
- **Low**: Lowest price during the minute  
- **Close**: Closing price for the minute
- **Volume**: Trading volume for the minute

## Testing & Validation

### Included Test Suite
- **`IntradayLinearDataGeneratorTests`**: Core generation and validation
- **`IntradayVsDailyComparisonTests`**: Scale comparison with daily data
- **Format compatibility verification**
- **Linear progression pattern validation**
- **OHLC relationship verification**

### Quality Checks
- ? DateTime format validation
- ? OHLC mathematical relationships
- ? Volume positivity and realism
- ? Linear trend consistency
- ? Cycle pattern adherence
- ? Trading day calendar accuracy (excludes weekends/holidays)

## Performance Characteristics

### Generation Speed
- **~2-5 seconds** for full dataset generation
- **Memory efficient** streaming file writing
- **Progress indicators** for large datasets

### File Characteristics
- **Compression friendly** due to linear patterns
- **Database compatible** format
- **Standard CSV parsing** compatibility

## Use Cases

### ?? **Ideal For**
- **Intraday Algorithm Testing**: Test strategies on minute-by-minute data
- **Scalping Strategy Backtesting**: High-frequency trading validation
- **Linear Trend Following**: Validate trend-following algorithms
- **Market Microstructure Studies**: Analyze intraday patterns
- **High-Frequency Testing**: Stress test algorithms with detailed data

### ?? **Comparison with Daily Data**
| Aspect | Daily Data | Intraday Linear Data |
|--------|------------|---------------------|
| Records | ~300 bars | ~100,000+ bars |
| Granularity | 1 bar/day | 390 bars/day |
| File Size | ~15 KB | ~5-10 MB |
| Use Case | Swing trading | Intraday/scalping |
| Pattern | Market noise | Predictable linear |

## Integration

### With Existing Trading System
```csharp
// Load intraday data instead of daily data
var intradayData = LoadPriceData("intraday_linear_spx_data.csv");

// Use with existing trading algorithms
var individual = new GeneticIndividual();
individual.Process(intradayData);

// Events will fire for every minute bar
individual.TradeOpened += (sender, e) => Console.WriteLine($"Trade opened at {e.DateTime}");
individual.TradeClosed += (sender, e) => Console.WriteLine($"Trade closed at {e.DateTime}");
```

### Benefits for Algorithm Testing
- **Statistical Significance**: More data points = better statistical validation
- **Intraday Timing**: Test entry/exit timing within trading days
- **Scalping Validation**: Verify high-frequency strategies
- **Linear Patterns**: Predictable trends for algorithm validation
- **Volume Analysis**: Test volume-based indicators

## Files Generated

1. **`intraday_linear_spx_data.csv`** - Main intraday dataset
2. **Test validation files** (temporary, cleaned up automatically)
3. **Demo output and statistics**

## Technical Notes

### Holiday Handling
- Excludes weekends (Saturday/Sunday)
- Excludes major holidays (Christmas, New Year, July 4th, Thanksgiving)
- Can be extended with more sophisticated holiday calendars

### Price Calculation Logic
```csharp
// 5-day appreciation phase
if (dayInCycle < 5) {
    var progress = dayInCycle / 4.0;
    dayPrice = basePrice + (cycleRange * progress);
}
// 5-day depreciation phase  
else {
    var progress = (dayInCycle - 5) / 4.0;
    dayPrice = basePrice + cycleRange - (cycleRange * progress);
}

// Linear intraday progression
targetPrice = dayStartPrice + ((dayEndPrice - dayStartPrice) * minuteProgress);
```

This comprehensive intraday data generator provides the foundation for detailed algorithmic testing while maintaining predictable linear patterns that are ideal for validating trading strategy performance.