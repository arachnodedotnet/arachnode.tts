using System;

namespace Trade.Indicators
{
    public enum AppliedVolume
    {
        Tick,
        Regular
    }

    /// <summary>
    /// Static class for Force Index indicator calculation
    /// </summary>
    public static class ForceIndex
    {
        /// <summary>
        ///     Calculates the Force Index indicator developed by Alexander Elder.
        ///     The Force Index combines price and volume to assess the amount of power used to move the price.
        ///     Formula: Force Index = Volume × (Current Price - Previous Price)
        ///     The raw values are then smoothed using a moving average.
        ///     Positive values indicate buying pressure, negative values indicate selling pressure.
        /// </summary>
        /// <param name="open">Open price array.</param>
        /// <param name="high">High price array.</param>
        /// <param name="low">Low price array.</param>
        /// <param name="close">Close price array.</param>
        /// <param name="tickVolume">Tick volume array.</param>
        /// <param name="volume">Regular volume array.</param>
        /// <param name="forcePeriod">Force Index smoothing period (default 13).</param>
        /// <param name="maType">Moving average method (default SMA).</param>
        /// <param name="priceType">Applied price type (default Close).</param>
        /// <param name="volumeType">Applied volume type (default Tick).</param>
        /// <returns>Array of Force Index values.</returns>
        public static double[] Calculate(
            double[] open,
            double[] high,
            double[] low,
            double[] close,
            long[] tickVolume,
            long[] volume,
            int forcePeriod = 13,
            MaMethod maType = MaMethod.SMA,
            AppliedPrice priceType = AppliedPrice.Close,
            AppliedVolume volumeType = AppliedVolume.Tick)
        {
            // Input validation
            if (open == null || high == null || low == null || close == null ||
                tickVolume == null || volume == null)
                return new double[0];

            // Validate period
            if (forcePeriod <= 0)
                forcePeriod = 13; // Default fallback

            var ratesTotal = Math.Min(
                Math.Min(Math.Min(open.Length, high.Length), Math.Min(low.Length, close.Length)),
                Math.Min(tickVolume.Length, volume.Length));

            if (ratesTotal < 2)
                return new double[0];

            var priceBuffer = GetAppliedPriceBuffer(open, high, low, close, priceType);
            var forceRaw = new double[ratesTotal];
            var forceBuffer = new double[ratesTotal];

            // Calculate raw Force Index
            forceRaw[0] = 0.0; // First value is always 0 (no previous price)

            for (var i = 1; i < ratesTotal; i++)
            {
                double volumeValue = volumeType == AppliedVolume.Tick ? tickVolume[i] : volume[i];
                forceRaw[i] = volumeValue * (priceBuffer[i] - priceBuffer[i - 1]);
            }

            // Apply moving average to raw Force Index
            if (maType == MaMethod.SMA)
                SimpleMAOnBuffer(forceRaw, forceBuffer, forcePeriod);
            else if (maType == MaMethod.EMA)
                ExponentialMAOnBuffer(forceRaw, forceBuffer, forcePeriod);
            else // SMMA
                SmoothedMAOnBuffer(forceRaw, forceBuffer, forcePeriod);

            return forceBuffer;
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
            dest[0] = source[0];

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
    }
}