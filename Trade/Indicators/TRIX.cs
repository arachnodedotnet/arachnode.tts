using System;

namespace Trade.Indicators
{
    public class TRIXResult
    {
        public double[] TRIX { get; set; }
        public double[] EMA { get; set; }
        public double[] SecondEMA { get; set; }
        public double[] ThirdEMA { get; set; }
    }

    public static class TRIX
    {
        /// <summary>
        ///     Calculates the Triple Exponential Average (TRIX) indicator.
        ///     TRIX is a momentum oscillator that displays the rate of change (percentage)
        ///     of a triple-smoothed exponential moving average.
        ///     The calculation involves:
        ///     1. First EMA of prices
        ///     2. Second EMA (EMA of first EMA)
        ///     3. Third EMA (EMA of second EMA)
        ///     4. TRIX = (ThirdEMA[i] - ThirdEMA[i-1]) / ThirdEMA[i-1]
        ///     TRIX filters out short-term price movements and focuses on longer-term trends.
        /// </summary>
        /// <param name="prices">Array of price values (e.g., close prices)</param>
        /// <param name="period">EMA period (default 14)</param>
        /// <returns>TRIXResult containing TRIX and all intermediate EMA buffers</returns>
        /// <exception cref="ArgumentNullException">Thrown when prices array is null</exception>
        /// <exception cref="ArgumentException">Thrown when period is invalid</exception>
        public static TRIXResult Calculate(double[] prices, int period = 14)
        {
            // Input validation
            if (prices == null) throw new ArgumentNullException(nameof(prices));
            if (period <= 0) throw new ArgumentException("Period must be positive", nameof(period));

            var length = prices.Length;
            var ema = new double[length];
            var secondEma = new double[length];
            var thirdEma = new double[length];
            var trix = new double[length];

            if (length == 0)
                return new TRIXResult
                {
                    TRIX = trix,
                    EMA = ema,
                    SecondEMA = secondEma,
                    ThirdEMA = thirdEma
                };

            // Calculate minimum bars needed for meaningful TRIX calculation
            // Need enough data for three levels of EMA smoothing
            var minBars = Math.Max(1, 3 * period - 3);

            // Initialize TRIX values to NaN for insufficient data period
            for (var i = 0; i < minBars && i < length; i++)
                trix[i] = 0;

            // Calculate First EMA (of prices)
            CalculateEMA(prices, period, ema);

            // Calculate Second EMA (EMA of first EMA)
            CalculateEMA(ema, period, secondEma);

            // Calculate Third EMA (EMA of second EMA)
            CalculateEMA(secondEma, period, thirdEma);

            // Calculate TRIX (rate of change of third EMA)
            for (var i = minBars; i < length; i++)
            {
                var currentThirdEMA = thirdEma[i];
                var previousThirdEMA = thirdEma[i - 1];

                if (previousThirdEMA != 0.0)
                    // TRIX = (Current - Previous) / Previous
                    trix[i] = (currentThirdEMA - previousThirdEMA) / previousThirdEMA;
                else
                    // Handle edge case where previous value is zero
                    trix[i] = 0.0;
            }

            return new TRIXResult
            {
                TRIX = trix,
                EMA = ema,
                SecondEMA = secondEma,
                ThirdEMA = thirdEma
            };
        }

        /// <summary>
        ///     Calculates Exponential Moving Average using the standard formula.
        ///     EMA = α × Current + (1 - α) × Previous_EMA, where α = 2 / (period + 1)
        /// </summary>
        /// <param name="input">Input data array</param>
        /// <param name="period">EMA period</param>
        /// <param name="output">Output array for EMA values</param>
        private static void CalculateEMA(double[] input, int period, double[] output)
        {
            if (input.Length == 0) return;

            var smoothingFactor = 2.0 / (period + 1);
            output[0] = input[0]; // Initialize first value

            for (var i = 1; i < input.Length; i++)
                output[i] = (input[i] - output[i - 1]) * smoothingFactor + output[i - 1];
        }
    }
}