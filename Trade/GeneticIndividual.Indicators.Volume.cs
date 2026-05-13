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
        /// Calculate volume-based indicator values
        /// </summary>
        private double CalculateVolumeIndicator(IndicatorParams ind, double[] openPrices, double[] highPrices,
            double[] lowPrices, double[] closePrices, double[] volumes, double[] priceBuffer,
            int totalPriceBufferLength, string caller)
        {
            var period = Math.Min(ind.Period, priceBuffer.Length);
            var ohlc = ind.OHLC;
            int fastMAPeriod = FastMAPeriod;
            int slowMAPeriod = SlowMAPeriod;

            // Helper: map double[] volumes to long[] (for indicators requiring long arrays)
            long[] AsLongVolume(double[] src, int len)
            {
                if (src == null || src.Length < len)
                    return Enumerable.Repeat(1000L, len).ToArray();
                var arr = new long[len];
                for (int i = 0; i < len; i++) arr[i] = (long)Math.Max(0, Math.Round(src[i]));
                return arr;
            }

            switch (ind.Type)
            {
                case 17: // Chaikin Oscillator (uses volume)
                {
                    var len = closePrices.Length;
                    if (len < 3) return 0.0;
                    var volume = AsLongVolume(volumes, len);
                    var fast = Math.Max(2, fastMAPeriod);
                    var slow = Math.Max(fast + 1, slowMAPeriod);

                    // Map Mode 0-3 to MA method (default EMA)
                    ChaikinOscillatorMaMethod maMethod = ChaikinOscillatorMaMethod.EMA;
                    if (ind.Mode == 0) maMethod = ChaikinOscillatorMaMethod.SMA;
                    else if (ind.Mode == 1) maMethod = ChaikinOscillatorMaMethod.EMA;
                    else if (ind.Mode == 2) maMethod = ChaikinOscillatorMaMethod.SMMA;
                    else if (ind.Mode == 3) maMethod = ChaikinOscillatorMaMethod.LWMA;

                    var cho = global::Trade.Indicators.ChaikinOscillator.Calculate(highPrices, lowPrices, closePrices, volume, fast, slow, maMethod);
                    var v = cho.Length > 0 ? cho.LastOrDefault() : 0.0;
                    return v;
                }
                case 23: // Force Index (uses volume)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral Force Index value
                    if (period <= 0) return 0.0;

                    // Prefer real volumes provided; fallback to synthetic
                    var realVolumeBuffer = AsLongVolume(volumes, priceBuffer.Length);
                    var tickVolumeBuffer = AsLongVolume(volumes, priceBuffer.Length);

                    // Convert MaMethod from Mode
                    var maMethod = MaMethod.SMA;
                    switch (ind.Mode)
                    {
                        case 0:
                            maMethod = MaMethod.SMA;
                            break;
                        case 1:
                            maMethod = MaMethod.EMA;
                            break;
                        case 2:
                            maMethod = MaMethod.SMMA;
                            break;
                        default:
                            maMethod = MaMethod.SMA;
                            break;
                    }

                    // Convert AppliedPrice from OHLC
                    var appliedPrice = AppliedPrice.Close;
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
                        default:
                            appliedPrice = AppliedPrice.Close;
                            break;
                    }

                    var result = ForceIndex.Calculate(
                        openPrices,
                        highPrices,
                        lowPrices,
                        closePrices,
                        tickVolumeBuffer,
                        realVolumeBuffer,
                        period,
                        maMethod,
                        appliedPrice,
                        AppliedVolume.Tick);

                    var v = result.Length > 0 ? result.LastOrDefault() : 0.0;
                    return v;
                }
                case 31: // Market Facilitation Index (MFI) (uses volume)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral MFI value

                    // Prefer provided volumes
                    var volumeBuffer = AsLongVolume(volumes, priceBuffer.Length);

                    // Use a reasonable point value for normalization (0.01 for most stocks, 0.0001 for forex)
                    var pointValue = 0.01; // Assuming stock prices, can be adjusted based on instrument type

                    var result = MarketFacilitationIndex.Calculate(highPrices, lowPrices, volumeBuffer, pointValue);

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // Market Facilitation Index value
                            return result.MFI.Length > 0 ? result.MFI.LastOrDefault() : 0.0;

                        case 1: // Color Index (0=Green/Trending, 1=Brown/Squat, 2=Blue/Fake, 3=Pink/Stopping)
                            return result.ColorIndex.Length > 0 ? result.ColorIndex.LastOrDefault() : 0.0;

                        default:
                            // Default to MFI value
                            return result.MFI.Length > 0 ? result.MFI.LastOrDefault() : 0.0;
                    }
                }
                case 32: // On Balance Volume (OBV) (uses volume)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral OBV value

                    var tickVolumeBuffer = AsLongVolume(volumes, priceBuffer.Length);
                    var realVolumeBuffer = AsLongVolume(volumes, priceBuffer.Length);

                    // Use Mode to select volume type
                    var volumeType = VolumeType.Tick; // Default
                    switch (ind.Mode)
                    {
                        case 0:
                            volumeType = VolumeType.Tick;
                            break;
                        case 1:
                            volumeType = VolumeType.Real;
                            break;
                    }

                    var result = OBV.Calculate(closePrices, tickVolumeBuffer, realVolumeBuffer, volumeType);

                    return result.OBV.Length > 0 ? result.OBV.LastOrDefault() : 0.0;
                }
                case 36: // Price and Volume Trend (PVT) (uses volume)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral PVT value

                    var tickVolumeBuffer = AsLongVolume(volumes, priceBuffer.Length);
                    var realVolumeBuffer = AsLongVolume(volumes, priceBuffer.Length);

                    // Use Mode to select volume type
                    var volumeType = VolumeType.Tick; // Default
                    switch (ind.Mode)
                    {
                        case 0:
                            volumeType = VolumeType.Tick;
                            break;
                        case 1:
                            volumeType = VolumeType.Real;
                            break;
                    }

                    var result = PVT.Calculate(closePrices, tickVolumeBuffer, realVolumeBuffer, volumeType);

                    return result.PVT.Length > 0 ? result.PVT.LastOrDefault() : 0.0;
                }
                case 46: // Volumes (uses volume)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral Volumes value

                    var tickVolumeBuffer = AsLongVolume(volumes, priceBuffer.Length);
                    var realVolumeBuffer = AsLongVolume(volumes, priceBuffer.Length);

                    // Use Mode to select volume type and output
                    var volumeType = VolumeType.Tick; // Default
                    switch (ind.Mode)
                    {
                        case 0: // Tick Volume, return volume values
                        case 2: // Tick Volume, return color values
                            volumeType = VolumeType.Tick;
                            break;
                        case 1: // Real Volume, return volume values
                        case 3: // Real Volume, return color values
                            volumeType = VolumeType.Real;
                            break;
                    }

                    var result = Volumes.Calculate(tickVolumeBuffer, realVolumeBuffer, volumeType);

                    // Return based on Mode selection
                    switch (ind.Mode)
                    {
                        case 0: // Tick Volume values
                        case 1: // Real Volume values
                            return result.Volumes.Length > 0 ? result.Volumes.LastOrDefault() : 0.0;

                        case 2: // Tick Volume colors (0=Green/up, 1=Red/down)
                        case 3: // Real Volume colors (0=Green/up, 1=Red/down)
                            return result.Colors.Length > 0 ? result.Colors.LastOrDefault() : 0.0;

                        default:
                            // Default to Tick Volume values
                            return result.Volumes.Length > 0 ? result.Volumes.LastOrDefault() : 0.0;
                    }
                }
                case 47: // Volume Rate of Change (VROC) (uses volume)
                {
                    if (ind.DebugCase && period <= 0) return 0.0; // Neutral VROC value
                    if (period <= 0) return 0.0;

                    var tickVolumeBuffer = AsLongVolume(volumes, priceBuffer.Length);
                    var realVolumeBuffer = AsLongVolume(volumes, priceBuffer.Length);

                    // Use Mode to select volume type
                    var volumeType = VolumeType.Tick; // Default
                    switch (ind.Mode)
                    {
                        case 0:
                            volumeType = VolumeType.Tick;
                            break;
                        case 1:
                            volumeType = VolumeType.Real;
                            break;
                    }

                    var result = VROC.Calculate(tickVolumeBuffer, realVolumeBuffer, period, volumeType);

                    return result.VROC.Length > 0 ? result.VROC.LastOrDefault() : 0.0;
                }

                default:
                    return 0.0; // Not a volume indicator
            }
        }
    }
}