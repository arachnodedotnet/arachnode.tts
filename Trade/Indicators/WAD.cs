using System;

namespace Trade.Indicators
{
    public class WADResult
    {
        public double[] WAD { get; set; }
    }

    public static class WAD
    {
        /// <summary>
        ///     Calculates Larry Williams' Accumulation/Distribution (WAD) indicator.
        ///     WAD measures the relationship between close price and the true range,
        ///     accumulating positive values when close is above true range low,
        ///     and negative values when close is below true range high.
        ///     Formula:
        ///     - TRH (True Range High) = Max(High, Previous Close)
        ///     - TRL (True Range Low) = Min(Low, Previous Close)
        ///     - If Close > Previous Close: WAD = Previous WAD + (Close - TRL)
        ///     - If Close
        ///     < Previous Close: WAD= Previous WAD + ( Close - TRH)
        ///         - If Close= Previous Close: WAD= Previous WAD ( no change)
        /// </summary>
        /// <param name="high">High price array</param>
        /// <param name="low">Low price array</param>
        /// <param name="close">Close price array</param>
        /// <param name="point">Minimum price change (tick size, e.g. 0.00001 for Forex)</param>
        /// <returns>WADResult containing the WAD buffer</returns>
        /// <exception cref="ArgumentNullException">Thrown when any input array is null</exception>
        /// <exception cref="ArgumentException">Thrown when point is invalid</exception>
        public static WADResult Calculate(double[] high, double[] low, double[] close, double point = 0.00001)
        {
            // Input validation
            if (high == null) throw new ArgumentNullException(nameof(high));
            if (low == null) throw new ArgumentNullException(nameof(low));
            if (close == null) throw new ArgumentNullException(nameof(close));
            if (point <= 0) throw new ArgumentException("Point must be positive", nameof(point));
            if (point > 0.1) throw new ArgumentException("Point size appears too large", nameof(point));

            // Use minimum length of all arrays for safety
            var length = Math.Min(Math.Min(high.Length, low.Length), close.Length);
            var wad = new double[length];

            if (length == 0)
                return new WADResult { WAD = wad };

            if (length < 2)
            {
                // Single value case - initialize to zero
                wad[0] = 0.0;
                return new WADResult { WAD = wad };
            }

            // Initialize first WAD value
            wad[0] = 0.0;

            // Calculate WAD for each subsequent bar
            for (var i = 1; i < length; i++)
            {
                var currentHigh = high[i];
                var currentLow = low[i];
                var currentClose = close[i];
                var previousClose = close[i - 1];

                // True Range High and Low
                var trueRangeHigh = Math.Max(currentHigh, previousClose);
                var trueRangeLow = Math.Min(currentLow, previousClose);

                // Determine WAD change based on close vs previous close
                if (IsEqualDoubles(currentClose, previousClose, point))
                    // No change when closes are equal within tick size
                    wad[i] = wad[i - 1];
                else if (currentClose > previousClose)
                    // Bullish: Add (Close - True Range Low)
                    wad[i] = wad[i - 1] + (currentClose - trueRangeLow);
                else
                    // Bearish: Add (Close - True Range High) - this will be negative
                    wad[i] = wad[i - 1] + (currentClose - trueRangeHigh);
            }

            return new WADResult { WAD = wad };
        }

        /// <summary>
        ///     Compares two double values for equality within a specified epsilon tolerance.
        ///     This accounts for floating-point precision issues in financial data.
        /// </summary>
        /// <param name="value1">First value to compare</param>
        /// <param name="value2">Second value to compare</param>
        /// <param name="epsilon">Tolerance for comparison (tick size)</param>
        /// <returns>True if values are equal within tolerance</returns>
        private static bool IsEqualDoubles(double value1, double value2, double epsilon)
        {
            // Ensure epsilon is positive and reasonable
            if (epsilon < 0.0)
                epsilon = -epsilon;
            if (epsilon > 0.1)
                epsilon = 0.00001;

            var difference = value1 - value2;
            return Math.Abs(difference) <= epsilon;
        }
    }
}