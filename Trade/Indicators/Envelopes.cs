using System;

namespace Trade.Indicators
{
    public enum MaMethod
    {
        SMA,
        EMA,
        SMMA
    }

    public enum AppliedPrice
    {
        Close,
        Open,
        High,
        Low,
        Median
    }

    /// <summary>
    /// Static class for Envelopes indicator calculation
    /// </summary>
    public static class Envelopes
    {
        /// <summary>
        ///     Calculates the Envelopes indicator bands.
        ///     Envelopes create upper and lower bands around a moving average using a fixed percentage deviation.
        ///     Formula:
        ///     - Middle Line = Moving Average of applied price
        ///     - Upper Band = MA × (1 + Deviation%)
        ///     - Lower Band = MA × (1 - Deviation%)
        /// </summary>
        /// <param name="open">Open price array.</param>
        /// <param name="high">High price array.</param>
        /// <param name="low">Low price array.</param>
        /// <param name="close">Close price array.</param>
        /// <param name="maPeriod">Moving average period (default 14).</param>
        /// <param name="maShift">Moving average shift (default 0).</param>
        /// <param name="maType">Moving average method (default SMA).</param>
        /// <param name="priceType">Applied price type (default Close).</param>
        /// <param name="deviation">Deviation percentage (default 0.1 = 0.1%).</param>
        /// <returns>Tuple of (upperBand, lowerBand, maBuffer).</returns>
        public static (double[] upperBand, double[] lowerBand, double[] maBuffer) Calculate(
            double[] open,
            double[] high,
            double[] low,
            double[] close,
            int maPeriod = 14,
            int maShift = 0,
            MaMethod maType = MaMethod.SMA,
            AppliedPrice priceType = AppliedPrice.Close,
            double deviation = 0.1)
        {
            // Input validation
            if (open == null || high == null || low == null || close == null)
                return (new double[0], new double[0], new double[0]);

            // Validate period
            if (maPeriod <= 0)
                maPeriod = 14; // Default fallback

            var ratesTotal = Math.Min(Math.Min(open.Length, high.Length), Math.Min(low.Length, close.Length));
            if (ratesTotal < maPeriod)
                return (new double[0], new double[0], new double[0]);

            var priceBuffer = GetAppliedPriceBuffer(open, high, low, close, priceType);
            var maBuffer = new double[ratesTotal];
            var upperBand = new double[ratesTotal];
            var lowerBand = new double[ratesTotal];

            // Calculate moving average
            if (maType == MaMethod.SMA)
                SimpleMAOnBuffer(priceBuffer, maBuffer, maPeriod);
            else if (maType == MaMethod.EMA)
                ExponentialMAOnBuffer(priceBuffer, maBuffer, maPeriod);
            else // SMMA
                SmoothedMAOnBuffer(priceBuffer, maBuffer, maPeriod);

            // Calculate bands
            for (var i = maPeriod - 1; i < ratesTotal; i++) // Start from maPeriod - 1, not maPeriod
            {
                var deviationMultiplier = deviation / 100.0;
                upperBand[i] = maBuffer[i] * (1 + deviationMultiplier);
                lowerBand[i] = maBuffer[i] * (1 - deviationMultiplier);
            }

            // Apply shift if needed
            if (maShift != 0)
            {
                upperBand = ShiftArray(upperBand, maShift);
                lowerBand = ShiftArray(lowerBand, maShift);
                maBuffer = ShiftArray(maBuffer, maShift);
            }

            return (upperBand, lowerBand, maBuffer);
        }

        private static double[] GetAppliedPriceBuffer(double[] open, double[] high, double[] low, double[] close,
            AppliedPrice priceType)
        {
            var length = Math.Min(Math.Min(open.Length, high.Length), Math.Min(low.Length, close.Length));

            switch (priceType)
            {
                case AppliedPrice.Open:
                    return CopyArray(open, length);
                case AppliedPrice.High:
                    return CopyArray(high, length);
                case AppliedPrice.Low:
                    return CopyArray(low, length);
                case AppliedPrice.Median:
                    // Median price = (High + Low) / 2
                    var median = new double[length];
                    for (var i = 0; i < length; i++)
                        median[i] = (high[i] + low[i]) / 2.0;
                    return median;
                case AppliedPrice.Close:
                default:
                    return CopyArray(close, length);
            }
        }

        private static double[] CopyArray(double[] source, int length)
        {
            var result = new double[length];
            Array.Copy(source, result, Math.Min(source.Length, length));
            return result;
        }

        private static void SimpleMAOnBuffer(double[] source, double[] dest, int period)
        {
            var sum = 0.0;
            for (var i = 0; i < source.Length; i++)
            {
                sum += source[i];
                if (i >= period)
                    sum -= source[i - period];
                if (i >= period - 1)
                    dest[i] = sum / period;
                else
                    dest[i] = 0.0;
            }
        }

        private static void ExponentialMAOnBuffer(double[] source, double[] dest, int period)
        {
            if (source.Length == 0) return;

            var k = 2.0 / (period + 1);

            // Initialize first value
            dest[0] = source[0];

            // Calculate EMA values
            for (var i = 1; i < source.Length; i++)
                dest[i] = k * source[i] + (1 - k) * dest[i - 1];
        }

        private static void SmoothedMAOnBuffer(double[] source, double[] dest, int period)
        {
            if (source.Length == 0) return;

            var sum = 0.0;

            // Calculate initial sum for the first period
            for (var i = 0; i < period && i < source.Length; i++)
            {
                sum += source[i];
                dest[i] = 0.0; // Initialize early values to 0
            }

            // Set first smoothed MA value
            if (source.Length >= period)
                dest[period - 1] = sum / period;

            // Calculate subsequent smoothed MA values
            for (var i = period; i < source.Length; i++)
                dest[i] = (dest[i - 1] * (period - 1) + source[i]) / period;
        }

        private static double[] ShiftArray(double[] arr, int shift)
        {
            if (arr == null) return new double[0];

            var len = arr.Length;
            var shifted = new double[len];

            for (var i = 0; i < len; i++)
            {
                var idx = i - shift;
                shifted[i] = idx >= 0 && idx < len ? arr[idx] : 0.0;
            }

            return shifted;
        }
    }
}