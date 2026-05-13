using System;

namespace Trade.Indicators
{
    /// <summary>
    /// Static class for Detrended Price Oscillator (DPO) indicator calculation
    /// </summary>
    public static class DetrendedPriceOscillator
    {
        /// <summary>
        ///     Calculates the Detrended Price Oscillator (DPO) for the given price array.
        ///     The DPO removes the trend from prices by comparing the current price to a simple moving average
        ///     from a specified number of periods in the past. This creates an oscillator that fluctuates
        ///     above and below zero.
        ///     Formula: DPO[i] = Price[i] - SMA[i - (Period/2 + 1)]
        ///     Where SMA is a Simple Moving Average of the specified period.
        ///     The time shift (Period/2 + 1) centers the moving average, making the oscillator more effective
        ///     at identifying cycles without the influence of trend.
        /// </summary>
        /// <param name="price">Array of prices (typically close prices).</param>
        /// <param name="period">DPO period (default 12).</param>
        /// <returns>Array of DPO values.</returns>
        /// <exception cref="ArgumentNullException">Thrown when price array is null.</exception>
        public static double[] Calculate(double[] price, int period = 12)
        {
            if (price == null)
                throw new ArgumentNullException(nameof(price), "Price array cannot be null");

            // Validate period
            if (period <= 0)
                period = 12; // Default fallback

            var ratesTotal = price.Length;
            if (ratesTotal == 0)
                return new double[0];

            var dpoBuffer = new double[ratesTotal];

            // Calculate the shift for centering the moving average
            var shift = period / 2 + 1;

            // Need sufficient data for meaningful calculation
            if (ratesTotal < period + shift)
                // Return array of zeros if insufficient data
                return dpoBuffer;

            // Calculate simple moving average for the entire array
            var maBuffer = CalculateSimpleMA(price, period);

            // Calculate DPO using the shifted moving average
            for (var i = 0; i < ratesTotal; i++)
                if (i >= period + shift - 1)
                {
                    // DPO[i] = Price[i] - SMA[i - shift]
                    var maIndex = i - shift;
                    if (maIndex >= 0 && maIndex < maBuffer.Length)
                        dpoBuffer[i] = price[i] - maBuffer[maIndex];
                }

            // Earlier values remain 0.0 (insufficient data for calculation)
            return dpoBuffer;
        }

        /// <summary>
        ///     Calculates a simple moving average of the specified period.
        /// </summary>
        /// <param name="source">Source price array.</param>
        /// <param name="period">Period for the moving average.</param>
        /// <returns>Array of moving average values.</returns>
        private static double[] CalculateSimpleMA(double[] source, int period)
        {
            var maBuffer = new double[source.Length];
            var sum = 0.0;

            for (var i = 0; i < source.Length; i++)
            {
                // Add current value to sum
                sum += source[i];

                // Remove the value that's now outside the window
                if (i >= period) sum -= source[i - period];

                // Calculate MA when we have enough data
                if (i >= period - 1)
                    maBuffer[i] = sum / period;
                else
                    maBuffer[i] = 0.0;
            }

            return maBuffer;
        }
    }
}