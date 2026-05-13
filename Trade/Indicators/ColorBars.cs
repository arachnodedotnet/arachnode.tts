using System;

namespace Trade.Indicators
{
    public class ColorBarsResult
    {
        public ColorBarsResult(int length)
        {
            Open = new double[length];
            High = new double[length];
            Low = new double[length];
            Close = new double[length];
            Colors = new int[length];
        }

        public double[] Open { get; }
        public double[] High { get; }
        public double[] Low { get; }
        public double[] Close { get; }
        public int[] Colors { get; }
    }

    public static class ColorBars
    {
        /// <summary>
        ///     Calculates the ColorBars indicator based on tick volume trends.
        /// </summary>
        /// <param name="open">Array of open prices.</param>
        /// <param name="high">Array of high prices.</param>
        /// <param name="low">Array of low prices.</param>
        /// <param name="close">Array of close prices.</param>
        /// <param name="tickVolume">Array of tick volumes.</param>
        /// <returns>ColorBarsResult containing price buffers and color indices (0=up volume, 1=down volume).</returns>
        public static ColorBarsResult Calculate(
            double[] open,
            double[] high,
            double[] low,
            double[] close,
            long[] tickVolume)
        {
            // Input validation
            if (open == null || high == null || low == null || close == null || tickVolume == null)
                return new ColorBarsResult(0);

            // Find minimum length to handle array size mismatches
            var ratesTotal = Math.Min(
                Math.Min(Math.Min(open.Length, high.Length), Math.Min(low.Length, close.Length)),
                tickVolume.Length);

            if (ratesTotal == 0)
                return new ColorBarsResult(0);

            var result = new ColorBarsResult(ratesTotal);

            // Initialize first bar
            result.Open[0] = open[0];
            result.High[0] = high[0];
            result.Low[0] = low[0];
            result.Close[0] = close[0];
            result.Colors[0] = 0; // Default first bar to up (neutral choice)

            // Process remaining bars
            for (var i = 1; i < ratesTotal; i++)
            {
                result.Open[i] = open[i];
                result.High[i] = high[i];
                result.Low[i] = low[i];
                result.Close[i] = close[i];

                // Determine color based on volume trend
                if (tickVolume[i] > tickVolume[i - 1])
                    result.Colors[i] = 0; // Volume increasing = up color
                else if (tickVolume[i] < tickVolume[i - 1])
                    result.Colors[i] = 1; // Volume decreasing = down color
                else
                    result.Colors[i] = result.Colors[i - 1]; // Volume equal = keep previous color
            }

            return result;
        }
    }
}