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
        /// Calculate specialized/pattern-based indicator values
        /// </summary>
        private double CalculateSpecializedIndicator(IndicatorParams ind, double[] openPrices, double[] highPrices,
            double[] lowPrices, double[] closePrices, double[] volumes, double[] priceBuffer,
            int totalPriceBufferLength, string caller)
        {
            var period = Math.Min(ind.Period, priceBuffer.Length);
            var ohlc = ind.OHLC;
            int fastMAPeriod = FastMAPeriod;
            int slowMAPeriod = SlowMAPeriod;

            switch (ind.Type)
            {
                case 0: // Sin indicator
                {
                    var sinIdx = priceBuffer.Length - 1;
                    var v = SinIndicator.Calculate(sinIdx, totalPriceBufferLength, ind.Param1, ind.Param2,
                        ind.Param3,
                        ind.Param4, ind.Param5);
                    return v;
                }
                case 10: // ASI
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // ASI doesn't use period, return neutral value
                    var v = AsiIndicator.Calculate(openPrices, highPrices, lowPrices, closePrices).LastOrDefault();
                    return v;
                }
                case 20: // DeMarker
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral DeMarker value
                    if (period <= 0) return 0.0;
                    var idx = priceBuffer.Length - 1; //TODO: Check all period lengths... is this supposed to be -1?
                    var result = DeMarker.Calculate(highPrices, lowPrices, period);

                    double v;
                    switch (ind.Mode)
                    {
                        case 0:
                            v = result.DeMarker.LastOrDefault();
                            return ApplyRangeMode(ind, 20, v);
                        case 1:
                            v = result.DeMax.LastOrDefault();
                            return v;
                        case 3:
                            v = result.DeMin.LastOrDefault();
                            return v;
                        case 4:
                            v = result.AvgDeMax.LastOrDefault();
                            return v;
                        case 5: //TODO: I don't think we have this many options...
                            v = result.AvgDeMin.LastOrDefault();
                            return v;
                        default:
                            v = result.DeMarker.LastOrDefault();
                            return ApplyRangeMode(ind, 20, v);
                    }
                }
                case 21: // Detrended
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral Detrended value
                    if (period <= 0) return 0.0;
                    var selectedPriceBuffer = GetPriceBuffer(ohlc, openPrices, highPrices, lowPrices,
                        closePrices, priceBuffer);
                    var v = DetrendedPriceOscillator.Calculate(selectedPriceBuffer, period).LastOrDefault();
                    return v;
                }
                case 24: // Fractals
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral Fractals value

                    var result = Fractals.Calculate(highPrices, lowPrices);

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // Upper Fractal
                            // Find the last non-NaN upper fractal value
                            for (int i = result.UpperFractal.Length - 1; i >= 0; i--)
                            {
                                if (!double.IsNaN(result.UpperFractal[i]))
                                    return result.UpperFractal[i];
                            }

                            return 0.0;

                        case 1: // Lower Fractal
                            // Find the last non-NaN lower fractal value
                            for (int i = result.LowerFractal.Length - 1; i >= 0; i--)
                            {
                                if (!double.IsNaN(result.LowerFractal[i]))
                                    return result.LowerFractal[i];
                            }

                            return 0.0;

                        case 2: // Fractal Signal (1 for upper, -1 for lower, 0 for none)
                            var lastIndex = result.UpperFractal.Length - 1;
                            if (lastIndex >= 0)
                            {
                                if (!double.IsNaN(result.UpperFractal[lastIndex]))
                                    return 1.0; // Upper fractal signal
                                if (!double.IsNaN(result.LowerFractal[lastIndex]))
                                    return -1.0; // Lower fractal signal
                            }

                            return 0.0; // No fractal signal

                        default:
                            // Default to upper fractal
                            for (int i = result.UpperFractal.Length - 1; i >= 0; i--)
                            {
                                if (!double.IsNaN(result.UpperFractal[i]))
                                    return result.UpperFractal[i];
                            }

                            return 0.0;
                    }
                }
                case 27: // Heiken Ashi
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral Heiken Ashi value

                    var result = HeikenAshi.Calculate(openPrices, highPrices, lowPrices, closePrices);

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // Heiken Ashi Open
                            return result.Open.Length > 0 ? result.Open.LastOrDefault() : 0.0;

                        case 1: // Heiken Ashi High
                            return result.High.Length > 0 ? result.High.LastOrDefault() : 0.0;

                        case 2: // Heiken Ashi Low
                            return result.Low.Length > 0 ? result.Low.LastOrDefault() : 0.0;

                        case 3: // Heiken Ashi Close
                            return result.Close.Length > 0 ? result.Close.LastOrDefault() : 0.0;

                        case 4: // Heiken Ashi Color Signal (0 = bullish, 1 = bearish)
                            return result.Color.Length > 0 ? result.Color.LastOrDefault() : 0.0;

                        default:
                            // Default to Heiken Ashi Close
                            return result.Close.Length > 0 ? result.Close.LastOrDefault() : 0.0;
                    }
                }
                case 34: // Parabolic SAR
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral Parabolic SAR value

                    // Use genetic parameters for step and maximum
                    // Default values: step = 0.02, maximum = 0.2
                    // Scale based on Param1 and Param2, or use defaults
                    var step = ind.Param1 > 0 ? ind.Param1 : 0.02;
                    var maximum = ind.Param2 > 0 ? ind.Param2 : 0.2;

                    // Ensure reasonable ranges
                    step = Math.Max(0.001, Math.Min(0.1, step)); // 0.1% to 10%
                    maximum = Math.Max(0.05, Math.Min(1.0, maximum)); // 5% to 100%

                    var result = ParabolicSAR.Calculate(highPrices, lowPrices, step, maximum);

                    if (result?.SAR == null)
                    {
                        return 0;
                    }

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // SAR values
                            return result.SAR.Length > 0 ? result.SAR.LastOrDefault() : 0.0;

                        case 1: // Extreme Point (EP) values
                            return result.EP.Length > 0 ? result.EP.LastOrDefault() : 0.0;

                        case 2: // Acceleration Factor (AF) values
                            return result.AF.Length > 0 ? result.AF.LastOrDefault() : 0.0;

                        default:
                            // Default to SAR values
                            return result.SAR.Length > 0 ? result.SAR.LastOrDefault() : 0.0;
                    }
                }
                case 48: // Williams Accumulation/Distribution (WAD)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral WAD value

                    // Use a reasonable point value for tick size (0.01 for most stocks, 0.00001 for forex)
                    var pointValue = 0.01; // Assuming stock prices, can be adjusted based on instrument type

                    // WAD could use Param1 for custom point size if needed
                    if (ind.Param1 > 0 && ind.Param1 <= 0.1)
                    {
                        pointValue = ind.Param1;
                    }

                    var result = WAD.Calculate(highPrices, lowPrices, closePrices, pointValue);

                    return result.WAD.Length > 0 ? result.WAD.LastOrDefault() : 0.0;
                }
                case 50: // ZigZag
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral ZigZag value
                    if (period <= 0) return 0.0;
                    // Graceful fallback: recent swing direction magnitude
                    var n = Math.Min(10, closePrices.Length - 1);
                    if (n <= 0) return 0.0;
                    var delta = closePrices[closePrices.Length - 1] - closePrices[closePrices.Length - 1 - n];
                    // Return signed magnitude
                    var v = delta;
                    return v;
                }
                case 99:
                    return RandomNumberGenerator.Next(100, 201);

                default:
                    return 0.0; // Not a specialized indicator
            }
        }
    }
}