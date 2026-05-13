using System;

namespace Trade.Indicators
{
    public class DEMAResult
    {
        public double[] DEMA { get; set; }
        public double[] EMA { get; set; }
        public double[] EMAofEMA { get; set; }
    }

    public static class DEMA
    {
        /// <summary>
        ///     Calculates the Double Exponential Moving Average (DEMA) for the given price array.
        ///     DEMA = 2 × EMA - EMA(EMA)
        ///     DEMA reduces lag compared to traditional EMA by using double smoothing.
        /// </summary>
        /// <param name="prices">Array of price values (e.g., close prices)</param>
        /// <param name="period">EMA period (default 14)</param>
        /// <param name="shift">Indicator shift (default 0)</param>
        /// <returns>DEMAResult containing DEMA and intermediate EMA values</returns>
        /// <exception cref="ArgumentNullException">Thrown when prices array is null</exception>
        /// <exception cref="ArgumentException">Thrown when period is invalid</exception>
        public static DEMAResult Calculate(double[] prices, int period = 14, int shift = 0)
        {
            // Input validation
            if (prices == null) throw new ArgumentNullException(nameof(prices));
            if (period <= 0) throw new ArgumentException("Period must be positive", nameof(period));

            var length = prices.Length;
            var ema = new double[length];
            var emaOfEma = new double[length];
            var dema = new double[length];

            if (length == 0)
                return new DEMAResult
                {
                    DEMA = dema,
                    EMA = ema,
                    EMAofEMA = emaOfEma
                };

            // Calculate EMA of prices
            CalculateEMA(prices, period, ema);

            // Calculate EMA of EMA
            CalculateEMA(ema, period, emaOfEma);

            // Calculate DEMA: 2 × EMA - EMA(EMA)
            for (var i = 0; i < length; i++) dema[i] = 2.0 * ema[i] - emaOfEma[i];

            // Apply shift if needed
            if (shift != 0)
            {
                var shiftedDema = new double[length];

                if (shift > 0)
                {
                    // Positive shift - move values to the right
                    for (var i = 0; i < length - shift; i++)
                        shiftedDema[i + shift] = dema[i];
                }
                else
                {
                    // Negative shift - move values to the left
                    var absShift = Math.Abs(shift);
                    for (var i = absShift; i < length; i++)
                        shiftedDema[i - absShift] = dema[i];
                }

                dema = shiftedDema;
            }

            return new DEMAResult
            {
                DEMA = dema,
                EMA = ema,
                EMAofEMA = emaOfEma
            };
        }

        /// <summary>
        ///     Calculates Exponential Moving Average using the standard formula.
        ///     EMA = α × Price + (1 - α) × Previous_EMA, where α = 2 / (period + 1)
        /// </summary>
        /// <param name="source">Source data array</param>
        /// <param name="period">EMA period</param>
        /// <param name="destination">Output array</param>
        private static void CalculateEMA(double[] source, int period, double[] destination)
        {
            if (source.Length == 0) return;

            var smoothingFactor = 2.0 / (period + 1);
            destination[0] = source[0];

            for (var i = 1; i < source.Length; i++)
                destination[i] = smoothingFactor * source[i] + (1 - smoothingFactor) * destination[i - 1];
        }
    }
}