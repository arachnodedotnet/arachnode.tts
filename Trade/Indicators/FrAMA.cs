using System;

namespace Trade.Indicators
{
    /// <summary>
    /// Static class for Fractal Adaptive Moving Average (FrAMA) indicator calculation
    /// </summary>
    public static class FrAMA
    {
        /// <summary>
        ///     Calculates the Fractal Adaptive Moving Average (FrAMA) developed by John Ehlers.
        ///     FrAMA adapts its smoothing based on the fractal dimension of the price series.
        ///     When prices are trending (low fractal dimension), the smoothing is minimal.
        ///     When prices are ranging (high fractal dimension), the smoothing increases.
        ///     
        ///     Formula:
        ///     1. N1 = (High_N - Low_N) / N (most recent N periods)
        ///     2. N2 = (High_N - Low_N) / N (previous N periods) 
        ///     3. N3 = (High_2N - Low_2N) / 2N (entire 2N period window)
        ///     4. D = (ln(N1 + N2) - ln(N3)) / ln(2) (fractal dimension)
        ///     5. Alpha = exp(-4.6 × (D - 1)) (smoothing factor)
        ///     6. FrAMA = Alpha × Price + (1 - Alpha) × Previous_FrAMA
        /// </summary>
        /// <param name="price">Array of prices (e.g., close prices).</param>
        /// <param name="high">Array of high prices.</param>
        /// <param name="low">Array of low prices.</param>
        /// <param name="period">FrAMA period (default 14).</param>
        /// <param name="shift">Array shift for display purposes (default 0).</param>
        /// <returns>Array of FrAMA values.</returns>
        public static double[] Calculate(double[] price, double[] high, double[] low, int period = 14, int shift = 0)
        {
            // Input validation
            if (price == null || high == null || low == null)
                return new double[0];

            // Validate period
            if (period <= 0)
                period = 14; // Default fallback

            var ratesTotal = Math.Min(Math.Min(price.Length, high.Length), low.Length);
            var minBars = 2 * period;
            var framaBuffer = new double[ratesTotal];

            if (ratesTotal < minBars)
                return framaBuffer; // Returns array of zeros

            // Initialize first bars with price values
            for (var i = 0; i < 2 * period - 1 && i < ratesTotal; i++)
                framaBuffer[i] = price[i];

            var mathLog2 = Math.Log(2.0);

            for (var i = 2 * period - 1; i < ratesTotal; i++)
            {
                // Calculate indices for sub-windows
                var idx1Start = i - period + 1;      // Most recent N periods start
                var idx1End = i;                     // Most recent N periods end
                var idx2Start = i - 2 * period + 1;  // Previous N periods start
                var idx2End = i - period;            // Previous N periods end

                // Defensive bounds check
                if (idx1Start < 0 || idx2Start < 0)
                {
                    framaBuffer[i] = price[i];
                    continue;
                }

                // N1: High-Low range of most recent N periods / N
                var hi1 = MaxInRange(high, idx1Start, idx1End);
                var lo1 = MinInRange(low, idx1Start, idx1End);
                var n1 = (hi1 - lo1) / period;

                // N2: High-Low range of previous N periods / N
                var hi2 = MaxInRange(high, idx2Start, idx2End);
                var lo2 = MinInRange(low, idx2Start, idx2End);
                var n2 = (hi2 - lo2) / period;

                // N3: High-Low range of entire 2N period window / 2N
                var hi3 = MaxInRange(high, idx2Start, idx1End);
                var lo3 = MinInRange(low, idx2Start, idx1End);
                var n3 = (hi3 - lo3) / (2 * period);

                // Handle edge cases to prevent invalid logarithm operations
                if (n1 + n2 <= 0 || n3 <= 0)
                {
                    framaBuffer[i] = framaBuffer[i - 1]; // Use previous value
                    continue;
                }

                // Calculate Fractal Dimension: D = (ln(N1 + N2) - ln(N3)) / ln(2)
                var d = (Math.Log(n1 + n2) - Math.Log(n3)) / mathLog2;

                // Calculate Alpha (smoothing factor): Alpha = exp(-4.6 × (D - 1))
                var alpha = Math.Exp(-4.6 * (d - 1.0));

                // Bound alpha to reasonable range [0, 1]
                alpha = Math.Max(0.0, Math.Min(1.0, alpha));

                // FrAMA calculation: FrAMA = Alpha × Price + (1 - Alpha) × Previous_FrAMA
                framaBuffer[i] = alpha * price[i] + (1 - alpha) * framaBuffer[i - 1];
            }

            // Apply shift if needed
            if (shift != 0)
                framaBuffer = ShiftArray(framaBuffer, shift);

            return framaBuffer;
        }

        /// <summary>
        /// Finds the maximum value in a range of an array
        /// </summary>
        private static double MaxInRange(double[] arr, int start, int end)
        {
            if (start > end || start < 0 || end >= arr.Length)
                return 0.0;

            var max = arr[start];
            for (var i = start + 1; i <= end; i++)
                if (arr[i] > max)
                    max = arr[i];
            return max;
        }

        /// <summary>
        /// Finds the minimum value in a range of an array
        /// </summary>
        private static double MinInRange(double[] arr, int start, int end)
        {
            if (start > end || start < 0 || end >= arr.Length)
                return 0.0;

            var min = arr[start];
            for (var i = start + 1; i <= end; i++)
                if (arr[i] < min)
                    min = arr[i];
            return min;
        }

        /// <summary>
        /// Shifts array values by the specified amount
        /// </summary>
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