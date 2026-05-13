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
        /// Calculate volatility-based indicator values
        /// </summary>
        private double CalculateVolatilityIndicator(IndicatorParams ind, double[] openPrices, double[] highPrices,
            double[] lowPrices, double[] closePrices, double[] volumes, double[] priceBuffer,
            int totalPriceBufferLength, string caller)
        {
            var period = Math.Min(ind.Period, priceBuffer.Length);
            var ohlc = ind.OHLC;
            int fastMAPeriod = FastMAPeriod;
            int slowMAPeriod = SlowMAPeriod;

            switch (ind.Type)
            {
                case 5: // ATR
                {
                    if (ind.DebugCase && period <= 0)
                    {
                        // For ATR in debug mode, calculate a simple range from available data
                        if (highPrices.Length > 0 && lowPrices.Length > 0)
                        {
                            var lastIndex = Math.Min(highPrices.Length, lowPrices.Length) - 1;
                            return Math.Abs(highPrices[lastIndex] - lowPrices[lastIndex]);
                        }

                        return 0.0;
                    }

                    if (period <= 0) return 0.0;
                    period--; //TODO: verify this is OK... period is actually period - 1;...
                    var v = AtrIndicator.Calculate(highPrices, lowPrices, closePrices, period).LastOrDefault();
                    return v;
                }
                case 11: // Bollinger Bands
                {
                    if (ind.DebugCase && period <= 0)
                        return priceBuffer.Length > 0 ? priceBuffer[priceBuffer.Length - 1] : 0.0;
                    if (period <= 0) return 0.0;
                    var v = BollingerBands.Calculate(priceBuffer, period, 2).middle.LastOrDefault();
                    return v;
                }
                case 18: // Chaikin Volatility
                {
                    var len = closePrices.Length;
                    if (len < 3) return 0.0;
                    var fast = Math.Max(2, fastMAPeriod);
                    var slow = Math.Max(fast + 1, slowMAPeriod);

                    // Map Mode 0->EMA (default), 1->SMA for smoothing (constructor param)
                    SmoothMethod smoothType = SmoothMethod.EMA;
                    if (ind.Mode == 1) smoothType = SmoothMethod.SMA;

                    var chv = new global::Trade.Indicators.ChaikinVolatility(slow, fast, smoothType).Calculate(highPrices, lowPrices, fast, slow);
                    var v = chv.Length > 0 ? chv.LastOrDefault() : 0.0;
                    return v;
                }
                case 30: // Mass Index
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral Mass Index value

                    // Use period to scale the standard Mass Index parameters (9, 9, 25)
                    var periodEma = Math.Max(period * 9 / 25, 9);
                    var secondPeriodEma = Math.Max(period * 9 / 25, 9);
                    var sumPeriod = Math.Max(period, 25);

                    // If period is provided, use it as sumPeriod and scale EMAs proportionally
                    if (period > 0)
                    {
                        periodEma = Math.Max(period * 9 / 25, 1);
                        secondPeriodEma = Math.Max(period * 9 / 25, 1);
                        sumPeriod = period;
                    }

                    var result = MassIndex.Calculate(highPrices, lowPrices, periodEma, secondPeriodEma, sumPeriod);

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // Mass Index (MI)
                            return result.MI.Length > 0 ? result.MI.LastOrDefault() : 0.0;

                        case 1: // High-Low Range (HL)
                            return result.HL.Length > 0 ? result.HL.LastOrDefault() : 0.0;

                        case 2: // EMA of HL
                            return result.EMA_HL.Length > 0 ? result.EMA_HL.LastOrDefault() : 0.0;

                        case 3: // EMA of EMA_HL
                            return result.EMA_EMA_HL.Length > 0 ? result.EMA_EMA_HL.LastOrDefault() : 0.0;

                        default:
                            // Default to Mass Index
                            return result.MI.Length > 0 ? result.MI.LastOrDefault() : 0.0;
                    }
                }
                case 40: // Standard Deviation
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral StdDev value
                    if (period <= 0) return 0.0;

                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);

                    // Convert MaMethod from Mode
                    var maMethod = MAMethod.SMA; // Default
                    switch (ind.Mode)
                    {
                        case 0:
                            maMethod = MAMethod.SMA;
                            break;
                        case 1:
                            maMethod = MAMethod.EMA;
                            break;
                        case 2:
                            maMethod = MAMethod.SMMA;
                            break;
                        case 3:
                            maMethod = MAMethod.LWMA;
                            break;
                        case 4: // Return MA instead of StdDev
                        case 5: // Return MA instead of StdDev
                            maMethod = MAMethod.SMA; // Will return MA below
                            break;
                    }

                    var result = StdDev.Calculate(selectedPriceBuffer, period, 0, maMethod);

                    if (result?.MA == null || result?.StdDev == null)
                    {
                        return 0;
                    }

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // StdDev with SMA
                        case 1: // StdDev with EMA
                        case 2: // StdDev with SMMA
                        case 3: // StdDev with LWMA
                            return result.StdDev.Length > 0 ? result.StdDev.LastOrDefault() : 0.0;

                        case 4: // Moving Average (SMA)
                            return result.MA.Length > 0 ? result.MA.LastOrDefault() : 0.0;

                        case 5: // Moving Average (based on mode 0-3 selection)
                            // Recalculate with the MA method from modes 0-3
                            var maModeMethod = MAMethod.SMA;
                            var subMode = ind.Mode / 10; // Allow mode values > 5 to select MA method
                            switch (subMode)
                            {
                                case 0:
                                    maModeMethod = MAMethod.SMA;
                                    break;
                                case 1:
                                    maModeMethod = MAMethod.EMA;
                                    break;
                                case 2:
                                    maModeMethod = MAMethod.SMMA;
                                    break;
                                case 3:
                                    maModeMethod = MAMethod.LWMA;
                                    break;
                            }

                            var maResult = StdDev.Calculate(selectedPriceBuffer, period, 0, maModeMethod);
                            return maResult.MA.Length > 0 ? maResult.MA.LastOrDefault() : 0.0;

                        default:
                            // Default to StdDev with SMA
                            return result.StdDev.Length > 0 ? result.StdDev.LastOrDefault() : 0.0;
                    }
                }

                default:
                    return 0.0; // Not a volatility indicator
            }
        }
    }
}