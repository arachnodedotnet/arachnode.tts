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
        /// Calculate trend-following indicator values (Moving Averages)
        /// </summary>
        private double CalculateTrendIndicator(IndicatorParams ind, double[] openPrices, double[] highPrices,
            double[] lowPrices, double[] closePrices, double[] volumes, double[] priceBuffer,
            int totalPriceBufferLength, string caller)
        {
            var period = Math.Min(ind.Period, priceBuffer.Length);
            var ohlc = ind.OHLC;
            int fastMAPeriod = FastMAPeriod;
            int slowMAPeriod = SlowMAPeriod;

            switch (ind.Type)
            {
                case 1: // SMA
                {
                    // Handle debug case or zero period
                    if (ind.DebugCase && period <= 0)
                        // For debug case with zero period, return the last available price
                        return priceBuffer.Length > 0 ? priceBuffer[priceBuffer.Length - 1] : 0.0;
                    if (period <= 0) return 0.0;
                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);
                    var v = priceBuffer.Skip(selectedPriceBuffer.Length - period).Take(period).Average();
                    return v;
                }
                case 2: // EMA
                {
                    if (ind.DebugCase && period <= 0)
                        return priceBuffer.Length > 0 ? priceBuffer[priceBuffer.Length - 1] : 0.0;
                    if (period <= 0) return 0.0;
                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);
                    var v = CalculateEMA(selectedPriceBuffer, period);
                    return v;
                }
                case 3: // SMMA
                {
                    if (ind.DebugCase && period <= 0)
                        return priceBuffer.Length > 0 ? priceBuffer[priceBuffer.Length - 1] : 0.0;
                    if (period <= 0) return 0.0;
                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);
                    var v = CalculateSMMA(selectedPriceBuffer, period);
                    return v;
                }
                case 4: // LWMA
                {
                    if (ind.DebugCase && period <= 0)
                        return priceBuffer.Length > 0 ? priceBuffer[priceBuffer.Length - 1] : 0.0;
                    if (period <= 0) return 0.0;
                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);
                    var v = CalculateLWMA(selectedPriceBuffer, period);
                    return v;
                }
                case 9: // AMA
                {
                    if (ind.DebugCase && period <= 0)
                        return priceBuffer.Length > 0 ? priceBuffer[priceBuffer.Length - 1] : 0.0;
                    if (period <= 0) return 0.0;
                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);
                    var v = AmaIndicator.Calculate(selectedPriceBuffer, period, fastMAPeriod, slowMAPeriod)
                        .LastOrDefault();
                    return v;
                }
                case 19: // DEMA
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral DEMA value
                    if (period <= 0) return 0.0;
                    var idx = priceBuffer.Length - 1;
                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);
                    var result = DEMA.Calculate(selectedPriceBuffer, period, 0);

                    switch (ind.Mode)
                    {
                        case 0:
                            return result.DEMA.LastOrDefault();
                        case 1:
                            return result.EMA.LastOrDefault();
                        case 2:
                            return result.EMAofEMA.LastOrDefault();
                        default:
                            return result.DEMA.LastOrDefault();
                    }
                }
                case 25: // FrAMA (Fractal Adaptive Moving Average)
                {
                    if (ind.DebugCase && period <= 0)
                        return priceBuffer.Length > 0 ? priceBuffer[priceBuffer.Length - 1] : 0.0;
                    if (period <= 0) return 0.0;

                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);

                    var result = FrAMA.Calculate(selectedPriceBuffer, highPrices, lowPrices, period);

                    var v = result.Length > 0 ? result.LastOrDefault() : 0.0;
                    return v;
                }
                case 42: // Triple Exponential Moving Average (TEMA)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral TEMA value
                    if (period <= 0) return 0.0;

                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);

                    var result = TEMA.Calculate(selectedPriceBuffer, period, 0);

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // TEMA (main output)
                            return result.TEMA.Length > 0 ? result.TEMA.LastOrDefault() : 0.0;

                        case 1: // First EMA
                            return result.EMA.Length > 0 ? result.EMA.LastOrDefault() : 0.0;

                        case 2: // EMA of EMA
                            return result.EMAofEMA.Length > 0 ? result.EMAofEMA.LastOrDefault() : 0.0;

                        case 3: // EMA of EMA of EMA
                            return result.EMAofEMAofEMA.Length > 0 ? result.EMAofEMAofEMA.LastOrDefault() : 0.0;

                        default:
                            // Default to TEMA
                            return result.TEMA.Length > 0 ? result.TEMA.LastOrDefault() : 0.0;
                    }
                }
                case 45: // Variable Index Dynamic Average (VIDYA)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral VIDYA value
                    if (period <= 0) return 0.0;

                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);

                    // Use period for both CMO and EMA periods, or scale them proportionally
                    // Default VIDYA uses periodCMO=9, periodEMA=12
                    var periodCMO = Math.Max(period * 9 / 12, 9);
                    var periodEMA = period;

                    // If period is provided, use it as EMA period and scale CMO proportionally
                    if (period > 0)
                    {
                        periodCMO = Math.Max(period * 9 / 12, 1);
                        periodEMA = period;
                    }

                    var result = VIDYA.Calculate(selectedPriceBuffer, periodCMO, periodEMA, 0);

                    var v = result.VIDYA.Length > 0 ? result.VIDYA.LastOrDefault() : 0.0;
                    return v;
                }

                default:
                    return 0.0; // Not a trend indicator
            }
        }
    }
}