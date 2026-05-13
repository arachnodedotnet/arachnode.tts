using System;

namespace Trade.Indicators
{
    public class DeMarkerResult
    {
        public double[] DeMarker { get; set; }
        public double[] DeMax { get; set; }
        public double[] DeMin { get; set; }
        public double[] AvgDeMax { get; set; }
        public double[] AvgDeMin { get; set; }
    }

    public static class DeMarker
    {
        /// <summary>
        ///     Calculates the DeMarker indicator values.
        ///     The DeMarker indicator is a technical oscillator that compares the current period's
        ///     demand (buying pressure) to the previous period's demand. It oscillates between 0 and 1.
        ///     Formula:
        ///     - DeMax[i] = Max(High[i] - High[i-1], 0) - measures upward price pressure
        ///     - DeMin[i] = Max(Low[i-1] - Low[i], 0) - measures downward price pressure
        ///     - DeMarker = AvgDeMax / (AvgDeMax + AvgDeMin)
        ///     Values above 0.7 typically indicate overbought conditions.
        ///     Values below 0.3 typically indicate oversold conditions.
        /// </summary>
        /// <param name="high">Array of high prices</param>
        /// <param name="low">Array of low prices</param>
        /// <param name="period">Period for moving average calculation (default 14)</param>
        /// <returns>DeMarkerResult containing DeMarker and intermediate values</returns>
        /// <exception cref="ArgumentNullException">Thrown when input arrays are null</exception>
        /// <exception cref="ArgumentException">Thrown when period is invalid</exception>
        public static DeMarkerResult Calculate(double[] high, double[] low, int period = 14)
        {
            // Input validation
            if (high == null) throw new ArgumentNullException(nameof(high));
            if (low == null) throw new ArgumentNullException(nameof(low));
            if (period <= 0) throw new ArgumentException("Period must be positive", nameof(period));

            var length = Math.Min(high.Length, low.Length);

            var deMarkerBuffer = new double[length];
            var deMaxBuffer = new double[length];
            var deMinBuffer = new double[length];
            var avgDeMaxBuffer = new double[length];
            var avgDeMinBuffer = new double[length];

            if (length == 0)
                return new DeMarkerResult
                {
                    DeMarker = deMarkerBuffer,
                    DeMax = deMaxBuffer,
                    DeMin = deMinBuffer,
                    AvgDeMax = avgDeMaxBuffer,
                    AvgDeMin = avgDeMinBuffer
                };

            if (length < period)
                // Not enough data for meaningful calculation
                return new DeMarkerResult
                {
                    DeMarker = new double[0],
                    DeMax = new double[0],
                    DeMin = new double[0],
                    AvgDeMax = new double[0],
                    AvgDeMin = new double[0]
                };

            // Initialize first values
            deMaxBuffer[0] = 0.0;
            deMinBuffer[0] = 0.0;

            // Calculate DeMax and DeMin for all periods
            for (var i = 1; i < length; i++)
            {
                // DeMax: Positive difference between current and previous high
                deMaxBuffer[i] = high[i] > high[i - 1] ? high[i] - high[i - 1] : 0.0;

                // DeMin: Positive difference between previous and current low
                deMinBuffer[i] = low[i - 1] > low[i] ? low[i - 1] - low[i] : 0.0;
            }

            // Initialize DeMarker values to zero for insufficient data period
            for (var i = 0; i < period; i++)
            {
                deMarkerBuffer[i] = 0.0;
                avgDeMaxBuffer[i] = 0.0;
                avgDeMinBuffer[i] = 0.0;
            }

            // Calculate moving averages and DeMarker values
            for (var i = period; i < length; i++)
            {
                // Calculate simple moving averages of DeMax and DeMin
                avgDeMaxBuffer[i] = CalculateSimpleMA(i, period, deMaxBuffer);
                avgDeMinBuffer[i] = CalculateSimpleMA(i, period, deMinBuffer);

                // Calculate DeMarker = AvgDeMax / (AvgDeMax + AvgDeMin)
                var denominator = avgDeMaxBuffer[i] + avgDeMinBuffer[i];
                deMarkerBuffer[i] = denominator != 0.0 ? avgDeMaxBuffer[i] / denominator : 0.0;
            }

            return new DeMarkerResult
            {
                DeMarker = deMarkerBuffer,
                DeMax = deMaxBuffer,
                DeMin = deMinBuffer,
                AvgDeMax = avgDeMaxBuffer,
                AvgDeMin = avgDeMinBuffer
            };
        }

        /// <summary>
        ///     Calculates Simple Moving Average for a buffer up to specified index.
        /// </summary>
        /// <param name="index">Current index</param>
        /// <param name="period">Period for moving average</param>
        /// <param name="buffer">Data buffer</param>
        /// <returns>Simple moving average value</returns>
        private static double CalculateSimpleMA(int index, int period, double[] buffer)
        {
            if (index < period - 1)
                return 0.0;

            var sum = 0.0;
            for (var j = index - period + 1; j <= index; j++)
                sum += buffer[j];

            return sum / period;
        }
    }
}