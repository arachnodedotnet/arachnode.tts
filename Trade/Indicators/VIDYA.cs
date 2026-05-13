using System;

namespace Trade.Indicators
{
    public class VIDYAResult
    {
        public double[] VIDYA;
    }

    public static class VIDYA
    {
        /// <summary>
        ///     Calculates the Variable Index Dynamic Average (VIDYA) indicator.
        ///     VIDYA uses the Chande Momentum Oscillator (CMO) to vary the smoothing factor
        ///     of an EMA, making it more responsive during trending periods and less
        ///     responsive during sideways periods.
        ///     The correct VIDYA formula is:
        ///     VIDYA[i] = alpha * VI * (price[i] - VIDYA[i-1]) + VIDYA[i-1]
        ///     where VI = |CMO| / 100 (Volatility Index)
        /// </summary>
        /// <param name="prices">Array of price values (e.g., close prices)</param>
        /// <param name="periodCMO">CMO period (default 9)</param>
        /// <param name="periodEMA">EMA period (default 12)</param>
        /// <param name="shift">Indicator shift (default 0)</param>
        /// <returns>VIDYAResult containing the VIDYA buffer</returns>
        /// <exception cref="ArgumentNullException">Thrown when prices array is null</exception>
        /// <exception cref="ArgumentException">Thrown when periods are invalid</exception>
        public static VIDYAResult Calculate(double[] prices, int periodCMO = 9, int periodEMA = 12, int shift = 0)
        {
            // Input validation
            if (prices == null) throw new ArgumentNullException(nameof(prices));
            if (periodCMO <= 0) throw new ArgumentException("CMO period must be positive", nameof(periodCMO));
            if (periodEMA <= 0) throw new ArgumentException("EMA period must be positive", nameof(periodEMA));

            var length = prices.Length;
            if (length == 0) return new VIDYAResult { VIDYA = new double[0] };

            var vidya = new double[length];
            var minBars = Math.Max(periodCMO, periodEMA);
            var alpha = 2.0 / (periodEMA + 1.0); // EMA smoothing factor

            // Initialize first values - use simple average of available data
            if (length > 0)
            {
                vidya[0] = prices[0];

                for (var i = 1; i < minBars && i < length; i++)
                {
                    // Use simple moving average for initialization
                    var sum = 0.0;
                    for (var j = 0; j <= i; j++)
                        sum += prices[j];
                    vidya[i] = sum / (i + 1);
                }
            }

            // Main calculation loop - apply VIDYA formula
            for (var i = minBars; i < length; i++)
            {
                // Calculate CMO and normalize it to get Volatility Index (VI)
                var cmo = CalculateCMO(i, periodCMO, prices);
                var vi = Math.Abs(cmo) / 100.0; // Volatility Index [0,1]

                // Correct VIDYA formula: VIDYA[i] = alpha * VI * (price[i] - VIDYA[i-1]) + VIDYA[i-1]
                // This is equivalent to: VIDYA[i] = VIDYA[i-1] + alpha * VI * (price[i] - VIDYA[i-1])
                vidya[i] = vidya[i - 1] + alpha * vi * (prices[i] - vidya[i - 1]);
            }

            // Apply shift if needed
            if (shift != 0)
            {
                var shiftedVidya = new double[length];

                if (shift > 0)
                {
                    // Positive shift - move values to the right
                    for (var i = 0; i < length - shift; i++)
                        shiftedVidya[i + shift] = vidya[i];
                    // First 'shift' values remain 0
                }
                else
                {
                    // Negative shift - move values to the left
                    var absShift = Math.Abs(shift);
                    for (var i = absShift; i < length; i++)
                        shiftedVidya[i - absShift] = vidya[i];
                    // Last 'absShift' values remain 0
                }

                vidya = shiftedVidya;
            }

            return new VIDYAResult
            {
                VIDYA = vidya
            };
        }

        /// <summary>
        ///     Calculates the Chande Momentum Oscillator for a given position.
        ///     CMO measures momentum and returns values between -100 and +100.
        ///     Formula: CMO = 100 * (SumUp - SumDown) / (SumUp + SumDown)
        /// </summary>
        /// <param name="pos">Current position in the price array</param>
        /// <param name="period">CMO calculation period</param>
        /// <param name="prices">Price array</param>
        /// <returns>CMO value (-100 to +100)</returns>
        private static double CalculateCMO(int pos, int period, double[] prices)
        {
            // Ensure we have enough data and valid position
            if (pos < period || pos >= prices.Length || period <= 0)
                return 0.0;

            double sumUp = 0.0, sumDown = 0.0;

            // Calculate sum of positive and negative price changes over the period
            for (var i = 0; i < period; i++)
            {
                var currentIndex = pos - i;
                var previousIndex = pos - i - 1;

                // Ensure we don't go out of bounds
                if (previousIndex < 0)
                    break;

                var diff = prices[currentIndex] - prices[previousIndex];
                if (diff > 0.0)
                    sumUp += diff;
                else if (diff < 0.0)
                    sumDown += -diff; // Convert to positive
            }

            // Calculate CMO: 100 * (sumUp - sumDown) / (sumUp + sumDown)
            var totalSum = sumUp + sumDown;
            if (totalSum > 0.0)
                return 100.0 * (sumUp - sumDown) / totalSum;
            return 0.0;
        }
    }
}