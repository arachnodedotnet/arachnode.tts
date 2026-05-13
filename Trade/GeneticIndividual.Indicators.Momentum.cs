using System;
using System.Collections.Generic;
using System.Linq;
using Trade.Indicators;
using Trade.Prices2;

namespace Trade
{
    public partial class GeneticIndividual
    {
        /// <summary>
        /// Calculate momentum-based indicator values
        /// </summary>
        private double CalculateMomentumIndicator(IndicatorParams ind, double[] openPrices, double[] highPrices,
            double[] lowPrices, double[] closePrices, double[] volumes, double[] priceBuffer,
            int totalPriceBufferLength, string caller)
        {
            var period = Math.Min(ind.Period, priceBuffer.Length);
            var ohlc = ind.OHLC;
            int fastMAPeriod = FastMAPeriod;
            int slowMAPeriod = SlowMAPeriod;

            switch (ind.Type)
            {
                case 16: // CCI
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral CCI value
                    if (period <= 0) return 0.0;
                    var idx = priceBuffer.Length - 1;
                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);
                    var result = CCIIndicator.Calculate(idx, period, selectedPriceBuffer);
                    return ApplyRangeMode(ind, 16, result);
                }
                case 29: // MACD
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral MACD value

                    // Use period to scale the standard MACD parameters (12, 26, 9)
                    var fastPeriod = fastMAPeriod;
                    var slowPeriod = slowMAPeriod;
                    var signalPeriod = period;
                        
                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);

                    var result = MACD.Calculate(selectedPriceBuffer, fastMAPeriod, slowMAPeriod, signalPeriod);

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // MACD Line (Fast EMA - Slow EMA)
                            return result.MACD.Length > 0 ? result.MACD.LastOrDefault() : 0.0;

                        case 1: // Signal Line (EMA of MACD)
                            return result.Signal.Length > 0 ? result.Signal.LastOrDefault() : 0.0;

                        case 2: // Fast EMA
                            return result.FastEMA.Length > 0 ? result.FastEMA.LastOrDefault() : 0.0;

                        case 3: // Slow EMA
                            return result.SlowEMA.Length > 0 ? result.SlowEMA.LastOrDefault() : 0.0;

                        default:
                            // Default to MACD Line
                            return result.MACD.Length > 0 ? result.MACD.LastOrDefault() : 0.0;
                    }
                }
                case 33: // OsMA (MACD Histogram)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral OsMA value
                    
                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);

                    var result = OsMA.Calculate(selectedPriceBuffer, fastMAPeriod, slowMAPeriod, period);

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // OsMA (MACD Histogram = MACD - Signal)
                            return result.OsMA.Length > 0 ? result.OsMA.LastOrDefault() : 0.0;

                        case 1: // MACD Line (Fast EMA - Slow EMA)
                            return result.MACD.Length > 0 ? result.MACD.LastOrDefault() : 0.0;

                        case 2: // Signal Line (EMA of MACD)
                            return result.Signal.Length > 0 ? result.Signal.LastOrDefault() : 0.0;

                        case 3: // Fast EMA
                            return result.FastEMA.Length > 0 ? result.FastEMA.LastOrDefault() : 0.0;

                        case 4: // Slow EMA
                            return result.SlowEMA.Length > 0 ? result.SlowEMA.LastOrDefault() : 0.0;

                        default:
                            // Default to OsMA (the main histogram)
                            return result.OsMA.Length > 0 ? result.OsMA.LastOrDefault() : 0.0;
                    }
                }
                case 37: // Rate of Change (ROC)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral ROC value
                    if (period <= 0) return 0.0;

                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);

                    var result = ROC.Calculate(selectedPriceBuffer, period);

                    return result.ROC.Length > 0 ? result.ROC.LastOrDefault() : 0.0;
                }
                case 38: // Relative Strength Index (RSI)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral RSI value
                    if (period <= 0) return 0.0;

                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);

                    var result = RSI.Calculate(selectedPriceBuffer, period);

                    double v;
                    switch (ind.Mode)
                    {
                        case 0: // RSI values (main output)
                            v = result.RSI.Length > 0 ? result.RSI.LastOrDefault() : 0.0;
                            return ApplyRangeMode(ind, 38, v);

                        case 1: // Positive Buffer (upward price movements)
                            v = result.PosBuffer.Length > 0 ? result.PosBuffer.LastOrDefault() : 0.0;
                            return v;

                        case 2: // Negative Buffer (downward price movements)
                            v = result.NegBuffer.Length > 0 ? result.NegBuffer.LastOrDefault() : 0.0;
                            return v;

                        default:
                            v = result.RSI.Length > 0 ? result.RSI.LastOrDefault() : 0.0;
                            return ApplyRangeMode(ind, 38, v);
                    }
                }
                case 39: // Relative Vigor Index (RVI)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral RVI value
                    if (period <= 0) return 0.0;

                    var result = RVI.Calculate(openPrices, highPrices, lowPrices, closePrices, period);

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // RVI values (main oscillator)
                            return result.RVI.Length > 0 ? result.RVI.LastOrDefault() : 0.0;

                        case 1: // Signal line (smoothed RVI)
                            return result.Signal.Length > 0 ? result.Signal.LastOrDefault() : 0.0;

                        default:
                            // Default to RVI values
                            return result.RVI.Length > 0 ? result.RVI.LastOrDefault() : 0.0;
                    }
                }
                case 41: // Stochastic Oscillator
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral Stochastic value
                    if (period <= 0) return 0.0;

                    // Use period as kPeriod and scale dPeriod and slowing proportionally
                    var kPeriod = period;
                    var dPeriod = Math.Max(period * 3 / 5, 3); // Scale from default 5:3 ratio
                    var slowing = Math.Max(period * 3 / 5, 3); // Scale from default 5:3 ratio

                    // If period is very small, use minimum reasonable values
                    if (period < 5)
                    {
                        kPeriod = Math.Max(period, 2);
                        dPeriod = Math.Max(period / 2, 1);
                        slowing = Math.Max(period / 2, 1);
                    }

                    var result = Stochastic.Calculate(highPrices, lowPrices, closePrices, kPeriod, dPeriod,
                        slowing);

                    double v;
                    switch (ind.Mode)
                    {
                        case 0: // %K line (Main)
                            v = result.Main.Length > 0 ? result.Main.LastOrDefault() : 0.0;
                            return ApplyRangeMode(ind, 41, v);

                        case 1: // %D line (Signal)
                            v = result.Signal.Length > 0 ? result.Signal.LastOrDefault() : 0.0;
                            return ApplyRangeMode(ind, 41, v);

                        case 2: // Highest Highs buffer
                            v = result.Highes.Length > 0 ? result.Highes.LastOrDefault() : 0.0;
                            return v;

                        case 3: // Lowest Lows buffer
                            v = result.Lowes.Length > 0 ? result.Lowes.LastOrDefault() : 0.0;
                            return v;

                        default:
                            v = result.Main.Length > 0 ? result.Main.LastOrDefault() : 0.0;
                            return ApplyRangeMode(ind, 41, v);
                    }
                }
                case 43: // TRIX (Triple Exponential Average)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral TRIX value
                    if (period <= 0) return 0.0;

                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);

                    var result = TRIX.Calculate(selectedPriceBuffer, period);

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // TRIX (main momentum oscillator)
                            var trixValue = result.TRIX.Length > 0 ? result.TRIX.LastOrDefault() : 0.0;
                            return double.IsNaN(trixValue) ? 0.0 : trixValue;

                        case 1: // First EMA
                            return result.EMA.Length > 0 ? result.EMA.LastOrDefault() : 0.0;

                        case 2: // Second EMA (EMA of EMA)
                            return result.SecondEMA.Length > 0 ? result.SecondEMA.LastOrDefault() : 0.0;

                        case 3: // Third EMA (EMA of EMA of EMA)
                            return result.ThirdEMA.Length > 0 ? result.ThirdEMA.LastOrDefault() : 0.0;

                        default:
                            // Default to TRIX
                            var defaultTrix = result.TRIX.Length > 0 ? result.TRIX.LastOrDefault() : 0.0;
                            return double.IsNaN(defaultTrix) ? 0.0 : defaultTrix;
                    }
                }
                case 44: // Ultimate Oscillator
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral Ultimate Oscillator value
                    if (period <= 0) return 0.0;

                    // Use period to scale the standard Ultimate Oscillator parameters (7, 14, 28)
                    // Maintain the 1:2:4 ratio for fast:middle:slow periods
                    var fastPeriod = fastMAPeriod;
                    var middlePeriod = period;
                    var slowPeriod = slowMAPeriod;
                        
                    // Use default K factors (4, 2, 1) - these could be made genetic parameters in the future
                    var fastK = 4;
                    var middleK = 2;
                    var slowK = 1;

                    var result = UltimateOscillator.Calculate(highPrices, lowPrices, closePrices,
                        fastPeriod, middlePeriod, slowPeriod, fastK, middleK, slowK);

                    double v;
                    switch (ind.Mode)
                    {
                        case 0: // Ultimate Oscillator (main output)
                            v = result.UO.Length > 0 ? result.UO.LastOrDefault() : 0.0;
                            return ApplyRangeMode(ind, 44, v);

                        case 1: // Buying Pressure (BP)
                            v = result.BP.Length > 0 ? result.BP.LastOrDefault() : 0.0;
                            return v;

                        case 2: // Fast ATR
                            v = result.FastATR.Length > 0 ? result.FastATR.LastOrDefault() : 0.0;
                            return v;

                        case 3: // Middle ATR
                            v = result.MiddleATR.Length > 0 ? result.MiddleATR.LastOrDefault() : 0.0;
                            return v;

                        case 4: // Slow ATR
                            v = result.SlowATR.Length > 0 ? result.SlowATR.LastOrDefault() : 0.0;
                            return v;

                        default:
                            v = result.UO.Length > 0 ? result.UO.LastOrDefault() : 0.0;
                            return ApplyRangeMode(ind, 44, v);
                    }
                }
                case 49: // Williams' Percent Range (WPR)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral WPR value
                    if (period <= 0) return 0.0;

                    var result = WPR.Calculate(highPrices, lowPrices, closePrices, period);

                    var v = result.WPR.Length > 0 ? result.WPR.LastOrDefault() : 0.0;
                    return ApplyRangeMode(ind, 49, v);
                }

                default:
                    return 0.0; // Not a momentum indicator
            }
        }
    }
}