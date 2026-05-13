using System;
using System.Collections.Generic;
using System.Linq;
using Trade.Indicators;
using Trade.Prices2;
using Trade.Utils;

namespace Trade
{
    public partial class GeneticIndividual
    {
        /// <summary>
        /// Analyze the input buffer for all indicator types and store min/max
        /// OPTIMIZED: Uses PriceRange for zero-copy operations, eliminating O(n²) array allocations
        /// </summary>
        public static void AnalyzeIndicatorRanges(PriceRecord[] priceRecords)
        {
            IndicatorRanges.Clear();
            
            if (priceRecords == null || priceRecords.Length == 0) return;
            
            var recordCount = priceRecords.Length;
            
            // OPTIMIZATION: Pre-allocate arrays once at maximum size needed
            var fullOpenPrices = new double[recordCount];
            var fullHighPrices = new double[recordCount];
            var fullLowPrices = new double[recordCount];
            var fullClosePrices = new double[recordCount];
            var fullVolumes = new double[recordCount];
            var fullPriceBuffer = new double[recordCount];
            
            // OPTIMIZATION: Single pass to populate all price arrays at maximum size
            for (var j = 0; j < recordCount; j++)
            {
                var record = priceRecords[j];
                fullOpenPrices[j] = record.Open;
                fullHighPrices[j] = record.High;
                fullLowPrices[j] = record.Low;
                fullClosePrices[j] = record.Close;
                fullVolumes[j] = record.Volume;
                fullPriceBuffer[j] = record.Close;
            }
            
            var individual = new GeneticIndividual();
            
            // For each indicator type, calculate min/max over the buffer
            //HACK: this musts be kept in sync. with the indicator eval code...
            for (var type = 0; type <= 50; type++)
            {
                var values = new List<double>(recordCount); // Pre-size for performance
                
                // Use default params for scanning
                var ind = new IndicatorParams
                {
                    Type = type, Period = 14, Mode = 0, TimeFrame = TimeFrame.D1, Polarity = 1, LongThreshold = 0.5,
                    ShortThreshold = -0.5, Param1 = 0
                };
                
                // OPTIMIZATION: Process each time point using zero-copy PriceRange operations
                for (var i = 0; i < recordCount; i++)
                {
                    var currentLength = i + 1;
                    
                    // Create zero-copy ranges - no array allocation or copying!
                    var openRange = new PriceRange(fullOpenPrices, 0, currentLength);
                    var highRange = new PriceRange(fullHighPrices, 0, currentLength);
                    var lowRange = new PriceRange(fullLowPrices, 0, currentLength);
                    var closeRange = new PriceRange(fullClosePrices, 0, currentLength);
                    var volumeRange = new PriceRange(fullVolumes, 0, currentLength);
                    var priceBufferRange = new PriceRange(fullPriceBuffer, 0, currentLength);
                    
                    //this is for testing...
                    if (ind.Type == 0) 
                    {
                        // Create a separate test buffer for type 0
                        var testBuffer = new double[currentLength];
                        Array.Clear(testBuffer, 0, currentLength);
                        var testBufferRange = new PriceRange(testBuffer);
                        
                        var value = individual.CalculateIndicatorValue(ind, openRange, highRange, lowRange,
                            closeRange, volumeRange, testBufferRange, currentLength, nameof(AnalyzeIndicatorRanges));
                        values.Add(value);
                    }
                    else
                    {
                        // OPTIMIZATION: Use zero-copy PriceRange instead of creating new arrays
                        var value = individual.CalculateIndicatorValue(ind, openRange, highRange, lowRange,
                            closeRange, volumeRange, priceBufferRange, currentLength, nameof(AnalyzeIndicatorRanges));
                        values.Add(value);
                    }
                }

                if (values.Count > 0)
                {
                    // OPTIMIZATION: Single-pass min/max calculation instead of LINQ
                    var min = double.MaxValue;
                    var max = double.MinValue;
                    for (int i = 0; i < values.Count; i++)
                    {
                        var val = values[i];
                        if (val < min) min = val;
                        if (val > max) max = val;
                    }
                    IndicatorRanges[type] = (min, max);
                }
            }
        }

        /// <summary>
        /// LEGACY: Original inefficient implementation for performance comparison
        /// Uses O(n²) array allocation pattern - kept for benchmarking purposes only
        /// </summary>
        public static void AnalyzeIndicatorRangesSlow(PriceRecord[] priceRecords)
        {
            IndicatorRanges.Clear();
            // For each indicator type, calculate min/max over the buffer
            //HACK: this musts be kept in sync. with the indicator eval code...
            for (var type = 0; type <= 50; type++)
            {
                var values = new List<double>();
                // Use default params for scanning
                var ind = new IndicatorParams
                {
                    Type = type, Period = 14, Mode = 0, TimeFrame = TimeFrame.D1, Polarity = 1, LongThreshold = 0.5,
                    ShortThreshold = -0.5, Param1 = 0
                };
                for (var i = 0; i < priceRecords.Length; i++)
                {
                    // Extract close prices for indicator calculation - INEFFICIENT: O(n²) allocation pattern
                    var openPrices = new double[i + 1];
                    var highPrices = new double[i + 1];
                    var lowPrices = new double[i + 1];
                    var closePrices = new double[i + 1];
                    var volumes = new double[i + 1];
                    var priceBuffer = new double[i + 1];
                    for (var j = 0; j <= i; j++)
                    {
                        openPrices[j] = priceRecords[j].Open;
                        highPrices[j] = priceRecords[j].High;
                        lowPrices[j] = priceRecords[j].Low;
                        closePrices[j] = priceRecords[j].Close;
                        volumes[j] = priceRecords[j].Volume;
                        priceBuffer[j] = priceRecords[j].Close;
                    }

                    //this is for testing...
                    if (ind.Type == 0) priceBuffer = new double[i + 1];

                    var value = new GeneticIndividual().CalculateIndicatorValueArray(ind, openPrices, highPrices, lowPrices,
                        closePrices, volumes, priceBuffer, priceBuffer.Length, nameof(AnalyzeIndicatorRangesSlow));
                    values.Add(value);
                }

                var min = values.Min();
                var max = values.Max();
                IndicatorRanges[type] = (min, max);
            }
        }

        /// <summary>
        /// Normalize value using analyzed min/max for indicator type, scaled to [-1, 1]
        /// </summary>
        private double Normalize(double value, int type)
        {
            if (IndicatorRanges.ContainsKey(type))
            {
                var range = IndicatorRanges[type];
                if (range.max == range.min) return 0.0;
                return 2 * (value - range.min) / (range.max - range.min) - 1;
            }

            // Fallback normalization
            return Math.Max(-1, Math.Min(1, value / 500.0 - 1));
        }
        
        /// <summary>
        /// Calculate indicator value for all supported indicator types
        /// </summary>
        internal double CalculateIndicatorValue(IndicatorParams ind, PriceRange openPrices, PriceRange highPrices,
            PriceRange lowPrices, PriceRange closePrices, PriceRange volumes, PriceRange priceBuffer,
            int totalPriceBufferLength,
            string caller = null)
        {
            try
            {
                var period = Math.Min(ind.Period, priceBuffer.Length);
                if (period <= 0 && !ind.DebugCase) return 0;
                
                // Delegate to specialized indicator calculation methods
                var result = 0.0;

                // Try each category of indicators
                result = CalculateTrendIndicator(ind, openPrices, highPrices, lowPrices, closePrices, volumes, priceBuffer, totalPriceBufferLength, caller);
                if (result != 0.0) return result;

                result = CalculateMomentumIndicator(ind, openPrices, highPrices, lowPrices, closePrices, volumes, priceBuffer, totalPriceBufferLength, caller);
                if (result != 0.0) return result;

                result = CalculateVolatilityIndicator(ind, openPrices, highPrices, lowPrices, closePrices, volumes, priceBuffer, totalPriceBufferLength, caller);
                if (result != 0.0) return result;

                result = CalculateVolumeIndicator(ind, openPrices, highPrices, lowPrices, closePrices, volumes, priceBuffer, totalPriceBufferLength, caller);
                if (result != 0.0) return result;

                result = CalculateComplexIndicator(ind, openPrices, highPrices, lowPrices, closePrices, volumes, priceBuffer, totalPriceBufferLength, caller);
                if (result != 0.0) return result;

                result = CalculateSpecializedIndicator(ind, openPrices, highPrices, lowPrices, closePrices, volumes, priceBuffer, totalPriceBufferLength, caller);
                if (result != 0.0) return result;

                // Default fallback for unknown indicators
                if (ind.DebugCase && period <= 0)
                    return priceBuffer.Length > 0 ? priceBuffer[priceBuffer.Length - 1] : 0.0;
                if (period <= 0) return 0.0;
                var v = priceBuffer.Skip(priceBuffer.Length - period).Take(period).Average();
                return v;
            }
            catch (Exception exception)
            {
                //TODO: this shouldn't throw...
                ConsoleUtilities.WriteLine(exception.Message);
            }

            return 0;
        }

        /// <summary>
        /// LEGACY: Calculate indicator value using traditional array parameters
        /// Provided for backward compatibility with the slow method
        /// </summary>
        private double CalculateIndicatorValueArray(IndicatorParams ind, double[] openPrices, double[] highPrices,
            double[] lowPrices, double[] closePrices, double[] volumes, double[] priceBuffer,
            int totalPriceBufferLength,
            string caller = null)
        {
            // Convert arrays to PriceRange and delegate to optimized method
            var openRange = new PriceRange(openPrices);
            var highRange = new PriceRange(highPrices);
            var lowRange = new PriceRange(lowPrices);
            var closeRange = new PriceRange(closePrices);
            var volumeRange = new PriceRange(volumes);
            var priceBufferRange = new PriceRange(priceBuffer);
            
            return CalculateIndicatorValue(ind, openRange, highRange, lowRange, closeRange, volumeRange, priceBufferRange, totalPriceBufferLength, caller);
        }

        // NEW: Map well-known OB/OS ranges to {-1, 0, 1} when TradeMode == Range
        private static double ApplyRangeMode(IndicatorParams ind, int indicatorType, double value)
        {
            if (ind == null || ind.TradeMode != IndicatorTradeMode.Range)
                return value;

            switch (indicatorType)
            {
                case 16: // CCI: +/-100
                    if (value >= 100.0) return -1.0; // overbought
                    if (value <= -100.0) return 1.0; // oversold
                    return 0.0;
                case 38: // RSI: 70/30
                    if (value >= 70.0) return -1.0;
                    if (value <= 30.0) return 1.0;
                    return 0.0;
                case 41: // Stochastic %K/%D: 80/20 (only when Mode is 0 or 1, enforced by caller)
                    if (value >= 80.0) return -1.0;
                    if (value <= 20.0) return 1.0;
                    return 0.0;
                case 49: // Williams %R: -20/-80 (range -100..0)
                    if (value >= -20.0) return -1.0;
                    if (value <= -80.0) return 1.0;
                    return 0.0;
                case 20: // DeMarker (main): 0.7/0.3
                    if (value >= 0.7) return -1.0;
                    if (value <= 0.3) return 1.0;
                    return 0.0;
                case 44: // Ultimate Oscillator: 70/30
                    if (value >= 70.0) return -1.0;
                    if (value <= 30.0) return 1.0;
                    return 0.0;
                default:
                    return value; // leave others unchanged
            }
        }

        /// <summary>
        /// Get appropriate price buffer based on OHLC setting
        /// </summary>
        private double[] GetPriceBuffer(OHLC ohlc, PriceRange openPrices, PriceRange highPrices,
            PriceRange lowPrices, PriceRange closePrices, PriceRange priceBuffer)
        {
            double[] selectedBuffer;
            switch (ohlc)
            {
                case OHLC.Open:
                    selectedBuffer = openPrices;
                    break;
                case OHLC.High:
                    selectedBuffer = highPrices;
                    break;
                case OHLC.Low:
                    selectedBuffer = lowPrices;
                    break;
                case OHLC.Close:
                default:
                    selectedBuffer = closePrices;
                    break;
            }

            return selectedBuffer;
        }

        /// <summary>
        /// Calculate Exponential Moving Average
        /// </summary>
        private double CalculateEMA(double[] buffer, int period)
        {
            var k = 2.0 / (period + 1);
            var ema = buffer[buffer.Length - period];
            for (var i = buffer.Length - period + 1; i < buffer.Length; i++) ema = buffer[i] * k + ema * (1 - k);
            return ema;
        }

        /// <summary>
        /// Calculate Smoothed Moving Average
        /// </summary>
        private double CalculateSMMA(double[] buffer, int period)
        {
            var smma = buffer.Skip(buffer.Length - period).Take(period).Average();
            for (var i = buffer.Length - period + 1; i < buffer.Length; i++)
                smma = (smma * (period - 1) + buffer[i]) / period;
            return smma;
        }

        /// <summary>
        /// Calculate Linear Weighted Moving Average
        /// </summary>
        private double CalculateLWMA(double[] buffer, int period)
        {
            double sum = 0;
            double weightSum = 0;
            for (var i = 0; i < period; i++)
            {
                var idx = buffer.Length - period + i;
                double weight = i + 1;
                sum += buffer[idx] * weight;
                weightSum += weight;
            }

            return sum / weightSum;
        }
    }
}