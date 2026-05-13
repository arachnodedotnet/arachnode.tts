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
        /// Calculate complex multi-component indicator values (Alligator, Ichimoku, etc.)
        /// </summary>
        private double CalculateComplexIndicator(IndicatorParams ind, double[] openPrices, double[] highPrices,
            double[] lowPrices, double[] closePrices, double[] volumes, double[] priceBuffer,
            int totalPriceBufferLength, string caller)
        {
            var period = Math.Min(ind.Period, priceBuffer.Length);
            var ohlc = ind.OHLC;
            int fastMAPeriod = FastMAPeriod;
            int slowMAPeriod = SlowMAPeriod;

            switch (ind.Type)
            {
                case 6: // ADX
                {
                    if (ind.DebugCase && period <= 0) return 50.0; // Default neutral ADX value
                    if (period <= 0) return 0.0;
                    var v = AdxIndicator.Calculate(highPrices, lowPrices, closePrices, period).adx.LastOrDefault();
                    return v;
                }
                case 7: // ADX Wilder
                {
                    if (ind.DebugCase && period <= 0) return 50.0; // Default neutral ADX value
                    if (period <= 0) return 0.0;
                    try
                    {
                        var p = Math.Max(1, period - 1); // Wilder uses period-1 in some impls
                        var (adxw, plusDi, minusDi) =
                            AdxWilderIndicator.Calculate(highPrices, lowPrices, closePrices, p);

                        switch (ind.Mode)
                        {
                            case 0: // ADXW
                                return adxw.LastOrDefault();
                            case 1: // +DI
                                return plusDi.LastOrDefault();
                            case 2: // -DI
                                return minusDi.LastOrDefault();
                            default:
                                return adxw.LastOrDefault(); // Fallback to ADXW
                        }
                    }
                    catch
                    {
                        return 50.0; // Graceful neutral fallback
                    }
                }
                case 8: // Alligator
                {
                    if (ind.DebugCase && period <= 0)
                        return priceBuffer.Length > 0 ? priceBuffer[priceBuffer.Length - 1] : 0.0;
                    if (period <= 0) return 0.0;
                    // Graceful fallback implementation using SMMA of Median Price
                    var median = new double[closePrices.Length];
                    for (int i = 0; i < closePrices.Length; i++)
                        median[i] = (highPrices[i] + lowPrices[i]) / 2.0;

                    var jawsPeriod = Math.Max(13, period);
                    var teethPeriod = Math.Max(8, period * 8 / 13);
                    var lipsPeriod = Math.Max(5, period * 5 / 13);

                    var jaws = CalculateSMMA(median, Math.Min(jawsPeriod, median.Length));
                    var teeth = CalculateSMMA(median, Math.Min(teethPeriod, median.Length));
                    var lips = CalculateSMMA(median, Math.Min(lipsPeriod, median.Length));

                    switch (ind.Mode)
                    {
                        case 0: // Jaws
                            return jaws;
                        case 1: // Teeth
                            return teeth;
                        case 2: // Lips
                            return lips;
                        default:
                            return jaws; // Fallback
                    }
                }
                case 12: // Bulls Power
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral power value
                    if (period <= 0) return 0.0;
                    var v = BullsPower.Calculate(highPrices, closePrices, period).LastOrDefault();
                    return v;
                }
                case 13: // Bears Power
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral power value
                    if (period <= 0) return 0.0;
                    var v = BearsPower.Calculate(lowPrices, closePrices, period).LastOrDefault();
                    return v;
                }
                case 14: // Awesome Oscillator
                {
                    // Graceful fallback AO = SMA(median, fast) - SMA(median, slow)
                    var fast = Math.Max(2, fastMAPeriod);
                    var slow = Math.Max(fast + 1, slowMAPeriod);
                    var median = new double[closePrices.Length];
                    for (int i = 0; i < closePrices.Length; i++)
                        median[i] = (highPrices[i] + lowPrices[i]) / 2.0;
                    if (median.Length < slow) return 0.0;
                    var smaFast = median.Skip(median.Length - fast).Take(fast).Average();
                    var smaSlow = median.Skip(median.Length - slow).Take(slow).Average();
                    var v = smaFast - smaSlow;
                    return v;
                }
                case 15: // Accelerator Oscillator
                {
                    // Graceful fallback AC = AO - SMA(AO, 5)
                    var fast = Math.Max(2, fastMAPeriod);
                    var slow = Math.Max(fast + 1, slowMAPeriod);
                    var median = new double[closePrices.Length];
                    for (int i = 0; i < closePrices.Length; i++)
                        median[i] = (highPrices[i] + lowPrices[i]) / 2.0;
                    if (median.Length < slow + 5) return 0.0;
                    var smaFast = median.Skip(median.Length - fast).Take(fast).Average();
                    var smaSlow = median.Skip(median.Length - slow).Take(slow).Average();
                    var ao = smaFast - smaSlow;
                    // Build short AO window for SMA
                    var aoSeries = new double[5];
                    for (int i = 0; i < 5; i++)
                    {
                        var idxEnd = median.Length - i;
                        var smaF = median.Skip(idxEnd - fast).Take(fast).Average();
                        var smaS = median.Skip(idxEnd - slow).Take(slow).Average();
                        aoSeries[4 - i] = smaF - smaS;
                    }
                    var aoSma5 = aoSeries.Average();
                    var v = ao - aoSma5;
                    return v;
                }
                case 22: // Envelopes
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral Detrended value
                    if (period <= 0) return 0.0;
                    var result = Envelopes.Calculate(openPrices, highPrices, lowPrices, closePrices);

                    switch (ind.Mode)
                    {
                        case 0:
                            return result.upperBand.LastOrDefault();
                        case 1:
                            return result.maBuffer.LastOrDefault();
                        case 3:
                            return result.lowerBand.LastOrDefault();
                        default:
                            return result.maBuffer.LastOrDefault();
                    }
                }
                case 26: // Gator Oscillator
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral Gator value

                    // Use standard Alligator parameters or adapt based on period
                    var jawsPeriod = Math.Max(period, 13);
                    var teethPeriod = Math.Max(period * 8 / 13, 8);
                    var lipsPeriod = Math.Max(period * 5 / 13, 5);
                    var jawsShift = Math.Max(period * 8 / 13, 8);
                    var teethShift = Math.Max(period * 5 / 13, 5);
                    var lipsShift = Math.Max(period * 3 / 13, 3);

                    // Convert MaMethod from Mode
                    var maMethod = MaMethod.SMMA; // Default for Gator
                    switch (ind.Mode)
                    {
                        case 0:
                            maMethod = MaMethod.SMMA;
                            break;
                        case 1:
                            maMethod = MaMethod.SMA;
                            break;
                        case 2:
                            maMethod = MaMethod.EMA;
                            break;
                    }

                    // Convert AppliedPrice from OHLC
                    var appliedPrice = AppliedPrice.Median; // Default for Gator
                    switch (ind.OHLC)
                    {
                        case OHLC.Open:
                            appliedPrice = AppliedPrice.Open;
                            break;
                        case OHLC.High:
                            appliedPrice = AppliedPrice.High;
                            break;
                        case OHLC.Low:
                            appliedPrice = AppliedPrice.Low;
                            break;
                        case OHLC.Close:
                            appliedPrice = AppliedPrice.Close;
                            break;
                        default:
                            appliedPrice = AppliedPrice.Median;
                            break;
                    }

                    var result = GatorOscillator.Calculate(
                        openPrices, highPrices, lowPrices, closePrices,
                        jawsPeriod, jawsShift, teethPeriod, teethShift, lipsPeriod, lipsShift,
                        maMethod, appliedPrice);

                    // Return based on Mode selection (using higher bits for output type)
                    var outputMode = ind.Mode / 3; // 0-2 for MA method, higher values for output type
                    switch (outputMode)
                    {
                        case 0: // Upper Buffer (Jaws-Teeth distance)
                            return result.UpperBuffer.Length > 0 ? result.UpperBuffer.LastOrDefault() : 0.0;

                        case 1: // Lower Buffer (Teeth-Lips distance, negative)
                            return result.LowerBuffer.Length > 0 ? result.LowerBuffer.LastOrDefault() : 0.0;

                        case 2: // Upper Color Signal (1=Green/expanding, 0=Red/contracting)
                            return result.UpperColors.Length > 0 ? result.UpperColors.LastOrDefault() : 0.0;

                        case 3: // Lower Color Signal (1=Green/expanding, 0=Red/contracting)
                            return result.LowerColors.Length > 0 ? result.LowerColors.LastOrDefault() : 0.0;

                        case 4: // Jaws (Blue line)
                            return result.Jaws.Length > 0 ? result.Jaws.LastOrDefault() : 0.0;

                        case 5: // Teeth (Red line)
                            return result.Teeth.Length > 0 ? result.Teeth.LastOrDefault() : 0.0;

                        case 6: // Lips (Green line)
                            return result.Lips.Length > 0 ? result.Lips.LastOrDefault() : 0.0;

                        default:
                            // Default to Upper Buffer
                            return result.UpperBuffer.Length > 0 ? result.UpperBuffer.LastOrDefault() : 0.0;
                    }
                }
                case 28: // Ichimoku
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral Ichimoku value

                    // Use period to scale the standard Ichimoku parameters (9, 26, 52)
                    var tenkanPeriod = Math.Max(period * 9 / 26, 9);
                    var kijunPeriod = Math.Max(period, 26);
                    var senkouPeriod = Math.Max(period * 52 / 26, 52);

                    // If period is provided, use it as kijun and scale others proportionally
                    if (period > 0)
                    {
                        tenkanPeriod = Math.Max(period * 9 / 26, 1);
                        kijunPeriod = period;
                        senkouPeriod = period * 52 / 26;
                    }

                    var result = Ichimoku.Calculate(highPrices, lowPrices, closePrices,
                        tenkanPeriod, kijunPeriod, senkouPeriod);

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // Tenkan-sen (Conversion Line)
                            return result.TenkanSen.Length > 0 ? result.TenkanSen.LastOrDefault() : 0.0;

                        case 1: // Kijun-sen (Base Line)
                            return result.KijunSen.Length > 0 ? result.KijunSen.LastOrDefault() : 0.0;

                        case 2: // Senkou Span A (Leading Span A)
                            var spanA = result.SenkouSpanA.Length > 0
                                ? result.SenkouSpanA.LastOrDefault()
                                : 0;
                            return double.IsNaN(spanA) ? 0.0 : spanA;

                        case 3: // Senkou Span B (Leading Span B)
                            var spanB = result.SenkouSpanB.Length > 0
                                ? result.SenkouSpanB.LastOrDefault()
                                : 0;
                            return double.IsNaN(spanB) ? 0.0 : spanB;

                        case 4: // Chikou Span (Lagging Span)
                            var chikou = result.ChikouSpan.Length > 0
                                ? result.ChikouSpan.LastOrDefault()
                                : 0;
                            return double.IsNaN(chikou) ? 0.0 : chikou;

                        default:
                            // Default to Tenkan-sen
                            return result.TenkanSen.Length > 0 ? result.TenkanSen.LastOrDefault() : 0.0;
                    }
                }
                case 35: // Price Channel
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral Price Channel value
                    if (period <= 0) return 0.0;

                    var result = PriceChannel.Calculate(highPrices, lowPrices, period);

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // Upper Channel
                            return result.Upper.Length > 0 ? result.Upper.LastOrDefault() : 0.0;

                        case 1: // Lower Channel
                            return result.Lower.Length > 0 ? result.Lower.LastOrDefault() : 0.0;

                        case 2: // Median Channel (Middle Line)
                            return result.Median.Length > 0 ? result.Median.LastOrDefault() : 0.0;

                        default:
                            // Default to Upper Channel
                            return result.Upper.Length > 0 ? result.Upper.LastOrDefault() : 0.0;
                    }
                }

                default:
                    return 0.0; // Not a complex indicator
            }
        }
    }
}