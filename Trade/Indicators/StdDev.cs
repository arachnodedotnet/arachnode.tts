using System;

namespace Trade.Indicators
{
    public enum MAMethod
    {
        SMA, // Simple Moving Average
        EMA, // Exponential Moving Average
        SMMA, // Smoothed Moving Average
        LWMA // Linear Weighted Moving Average
    }

    public class StdDevResult
    {
        public double[] StdDev { get; set; }
        public double[] MA { get; set; }
    }

    public static class StdDev
    {
        /// <summary>
        ///     Calculates the Standard Deviation indicator with selectable MA method.
        ///     Standard Deviation measures the volatility/dispersion of price data around
        ///     its moving average, providing insight into market volatility.
        ///     Formula:
        ///     1. Calculate Moving Average (MA) of prices over the period
        ///     2. Calculate Variance = Σ(Price - MA)² / Period
        ///     3. Standard Deviation = √(Variance)
        ///     Higher values indicate higher volatility, lower values indicate lower volatility.
        /// </summary>
        /// <param name="prices">Array of price values (e.g., close prices)</param>
        /// <param name="period">StdDev period (default 20)</param>
        /// <param name="shift">Buffer shift (default 0)</param>
        /// <param name="maMethod">Moving average method (default SMA)</param>
        /// <returns>StdDevResult containing StdDev and MA buffers</returns>
        /// <exception cref="ArgumentNullException">Thrown when prices array is null</exception>
        /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
        public static StdDevResult Calculate(
            double[] prices,
            int period = 20,
            int shift = 0,
            MAMethod maMethod = MAMethod.SMA)
        {
            // Input validation
            if (prices == null) throw new ArgumentNullException(nameof(prices));
            if (period < 2) return null;
            if (shift < 0) throw new ArgumentException("Shift cannot be negative", nameof(shift));

            var length = prices.Length;
            var stdDev = new double[length];
            var ma = new double[length];

            if (length == 0)
                return new StdDevResult
                {
                    StdDev = stdDev,
                    MA = ma
                };

            // Initialize buffers
            for (var i = 0; i < length; i++)
            {
                stdDev[i] = 0.0;
                ma[i] = 0.0;
            }

            var startIndex = period - 1;

            // Calculate MA and StdDev based on selected method
            switch (maMethod)
            {
                case MAMethod.EMA:
                    CalculateEMAStdDev(prices, ma, stdDev, period, startIndex, length);
                    break;
                case MAMethod.SMMA:
                    CalculateSMMAStdDev(prices, ma, stdDev, period, startIndex, length);
                    break;
                case MAMethod.LWMA:
                    CalculateLWMAStdDev(prices, ma, stdDev, period, startIndex, length);
                    break;
                case MAMethod.SMA:
                default:
                    CalculateSMAStdDev(prices, ma, stdDev, period, startIndex, length);
                    break;
            }

            // Apply shift if needed (shift the buffer right by 'shift' places)
            if (shift > 0)
            {
                var shiftedStdDev = new double[length];
                // Initialize all values to zero (default)

                // Only copy values if the shift doesn't exceed the array length
                if (shift < length)
                    for (var i = 0; i < length - shift; i++)
                        shiftedStdDev[i + shift] = stdDev[i];
                // If shift >= length, the shiftedStdDev array remains all zeros

                stdDev = shiftedStdDev;
            }

            return new StdDevResult
            {
                StdDev = stdDev,
                MA = ma
            };
        }

        private static void CalculateSMAStdDev(double[] prices, double[] ma, double[] stdDev, int period,
            int startIndex, int length)
        {
            for (var i = startIndex; i < length; i++)
            {
                ma[i] = CalculateSimpleMA(i, period, prices);
                stdDev[i] = CalculateStandardDeviation(prices, ma[i], i, period);
            }
        }

        private static void CalculateEMAStdDev(double[] prices, double[] ma, double[] stdDev, int period,
            int startIndex, int length)
        {
            for (var i = startIndex; i < length; i++)
            {
                if (i == startIndex)
                    ma[i] = CalculateSimpleMA(i, period, prices);
                else
                    ma[i] = CalculateExponentialMA(i, period, ma[i - 1], prices);

                stdDev[i] = CalculateStandardDeviation(prices, ma[i], i, period);
            }
        }

        private static void CalculateSMMAStdDev(double[] prices, double[] ma, double[] stdDev, int period,
            int startIndex, int length)
        {
            for (var i = startIndex; i < length; i++)
            {
                if (i == startIndex)
                    ma[i] = CalculateSimpleMA(i, period, prices);
                else
                    ma[i] = CalculateSmoothedMA(i, period, ma[i - 1], prices);

                stdDev[i] = CalculateStandardDeviation(prices, ma[i], i, period);
            }
        }

        private static void CalculateLWMAStdDev(double[] prices, double[] ma, double[] stdDev, int period,
            int startIndex, int length)
        {
            for (var i = startIndex; i < length; i++)
            {
                ma[i] = CalculateLinearWeightedMA(i, period, prices);
                stdDev[i] = CalculateStandardDeviation(prices, ma[i], i, period);
            }
        }

        /// <summary>
        ///     Calculates Simple Moving Average at specified position.
        /// </summary>
        private static double CalculateSimpleMA(int position, int period, double[] prices)
        {
            var sum = 0.0;
            var startIdx = Math.Max(0, position - period + 1);
            var count = 0;

            for (var i = startIdx; i <= position; i++)
            {
                sum += prices[i];
                count++;
            }

            return count > 0 ? sum / count : 0.0;
        }

        /// <summary>
        ///     Calculates Exponential Moving Average at specified position.
        /// </summary>
        private static double CalculateExponentialMA(int position, int period, double previousMA, double[] prices)
        {
            var smoothingFactor = 2.0 / (period + 1);
            return prices[position] * smoothingFactor + previousMA * (1 - smoothingFactor);
        }

        /// <summary>
        ///     Calculates Smoothed Moving Average at specified position.
        /// </summary>
        private static double CalculateSmoothedMA(int position, int period, double previousMA, double[] prices)
        {
            return (previousMA * (period - 1) + prices[position]) / period;
        }

        /// <summary>
        ///     Calculates Linear Weighted Moving Average at specified position.
        /// </summary>
        private static double CalculateLinearWeightedMA(int position, int period, double[] prices)
        {
            var sum = 0.0;
            var weightSum = 0.0;
            var weight = 1;
            var startIdx = Math.Max(0, position - period + 1);

            for (var i = startIdx; i <= position; i++)
            {
                sum += prices[i] * weight;
                weightSum += weight;
                weight++;
            }

            return weightSum > 0 ? sum / weightSum : 0.0;
        }

        /// <summary>
        ///     Calculates Standard Deviation using the standard formula:
        ///     StdDev = √(Σ(Price - MA)² / N)
        /// </summary>
        private static double CalculateStandardDeviation(double[] prices, double movingAverage, int position,
            int period)
        {
            var sumOfSquaredDeviations = 0.0;
            var startIdx = Math.Max(0, position - period + 1);
            var count = 0;

            for (var i = startIdx; i <= position; i++)
            {
                var deviation = prices[i] - movingAverage;
                sumOfSquaredDeviations += deviation * deviation;
                count++;
            }

            if (count == 0) return 0.0;

            var variance = sumOfSquaredDeviations / count;
            return Math.Sqrt(variance);
        }
    }
}