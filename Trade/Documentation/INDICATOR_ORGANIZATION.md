# Indicator Categories and Organization

## Overview
The indicators have been successfully split into logical categories using partial classes to improve code organization and maintainability.

## File Structure

### Main File
- **`GeneticIndividual.Indicators.cs`** - Core infrastructure, range analysis, normalization, and routing logic

### Category-Specific Files

#### 1. **`GeneticIndividual.Indicators.Trend.cs`** - Trend Following Indicators
Moving averages and trend-based indicators:
- **Type 1**: SMA (Simple Moving Average)
- **Type 2**: EMA (Exponential Moving Average)  
- **Type 3**: SMMA (Smoothed Moving Average)
- **Type 4**: LWMA (Linear Weighted Moving Average)
- **Type 9**: AMA (Adaptive Moving Average)
- **Type 19**: DEMA (Double Exponential Moving Average)
- **Type 25**: FrAMA (Fractal Adaptive Moving Average)
- **Type 42**: TEMA (Triple Exponential Moving Average)
- **Type 45**: VIDYA (Variable Index Dynamic Average)

#### 2. **`GeneticIndividual.Indicators.Momentum.cs`** - Momentum Oscillators
Price momentum and oscillator indicators:
- **Type 16**: CCI (Commodity Channel Index)
- **Type 29**: MACD (Moving Average Convergence Divergence)
- **Type 33**: OsMA (MACD Histogram)
- **Type 37**: ROC (Rate of Change)
- **Type 38**: RSI (Relative Strength Index)
- **Type 39**: RVI (Relative Vigor Index)
- **Type 41**: Stochastic Oscillator
- **Type 43**: TRIX (Triple Exponential Average)
- **Type 44**: Ultimate Oscillator
- **Type 49**: WPR (Williams' Percent Range)

#### 3. **`GeneticIndividual.Indicators.Volatility.cs`** - Volatility Indicators
Price volatility and range-based indicators:
- **Type 5**: ATR (Average True Range)
- **Type 11**: Bollinger Bands
- **Type 18**: Chaikin Volatility
- **Type 30**: Mass Index
- **Type 40**: Standard Deviation

#### 4. **`GeneticIndividual.Indicators.Volume.cs`** - Volume-Based Indicators
Indicators that incorporate volume data:
- **Type 17**: Chaikin Oscillator
- **Type 23**: Force Index
- **Type 31**: Market Facilitation Index (MFI)
- **Type 32**: OBV (On Balance Volume)
- **Type 36**: PVT (Price and Volume Trend)
- **Type 46**: Volumes
- **Type 47**: VROC (Volume Rate of Change)

#### 5. **`GeneticIndividual.Indicators.Complex.cs`** - Multi-Component Indicators
Complex indicators with multiple outputs/components:
- **Type 6**: ADX (Average Directional Index)
- **Type 7**: ADX Wilder
- **Type 8**: Alligator
- **Type 12**: Bulls Power
- **Type 13**: Bears Power
- **Type 14**: Awesome Oscillator
- **Type 15**: Accelerator Oscillator
- **Type 22**: Envelopes
- **Type 26**: Gator Oscillator
- **Type 28**: Ichimoku
- **Type 35**: Price Channel

#### 6. **`GeneticIndividual.Indicators.Specialized.cs`** - Pattern & Specialized Indicators
Special purpose and pattern-recognition indicators:
- **Type 0**: Sin Indicator (Test/Debug)
- **Type 10**: ASI (Accumulation Swing Index)
- **Type 20**: DeMarker
- **Type 21**: Detrended Price Oscillator
- **Type 24**: Fractals
- **Type 27**: Heiken Ashi
- **Type 34**: Parabolic SAR
- **Type 48**: WAD (Williams Accumulation/Distribution)
- **Type 50**: ZigZag
- **Type 99**: Random Number Generator (Test)

## Architecture Benefits

### 1. **Improved Organization**
- Related indicators grouped logically by function
- Easier to find and maintain specific indicator types
- Clear separation of concerns

### 2. **Maintainability**
- Smaller, focused files instead of one massive switch statement
- Each category can be worked on independently
- Easier code reviews and debugging

### 3. **Extensibility**
- New indicators can be added to appropriate categories
- Easy to add new indicator categories if needed
- Method delegation makes the main switch statement clean

### 4. **Performance**
- Main `CalculateIndicatorValue` now delegates to specialized methods
- Each category method only handles its specific indicator types
- Returns immediately when correct category is found

## Usage Pattern

The main `CalculateIndicatorValue` method now:
1. Tries each category method in sequence
2. Returns the first non-zero result found
3. Falls back to default behavior for unknown indicators
4. Maintains full backward compatibility

## Future Enhancements

Consider further refactoring to individual methods:
- `EvaluateRSI()`, `EvaluateMACD()`, etc.
- Replace switch statements with direct method calls
- Add indicator-specific optimization and caching

This organization provides a solid foundation for continued development and maintenance of the indicator calculation system.