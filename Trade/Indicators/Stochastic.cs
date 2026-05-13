using System;

namespace Trade.Indicators
{
    public class StochasticResult
    {
        public double[] Main { get; set; } // %K line
        public double[] Signal { get; set; } // %D line
        public double[] Highes { get; set; } // Highest highs buffer
        public double[] Lowes { get; set; } // Lowest lows buffer
    }

    public static class Stochastic
    {
        /// <summary>
        ///     Calculates the Stochastic Oscillator indicator.
        ///     The Stochastic Oscillator is a momentum indicator that compares a security's
        ///     closing price to its price range over a specified period.
        ///     Formula:
        ///     - Raw %K = ((Close - Lowest Low) / (Highest High - Lowest Low)) × 100
        ///     - Slowed %K = Simple Moving Average of Raw %K over 'slowing' periods
        ///     - %D = Simple Moving Average of %K over 'dPeriod' periods
        ///     Values above 80 typically indicate overbought conditions.
        ///     Values below 20 typically indicate oversold conditions.
        /// </summary>
        /// <param name="high">High price array</param>
        /// <param name="low">Low price array</param>
        /// <param name="close">Close price array</param>
        /// <param name="kPeriod">K period for highest/lowest calculation (default 5)</param>
        /// <param name="dPeriod">D period for %D signal line (default 3)</param>
        /// <param name="slowing">Slowing factor for %K smoothing (default 3)</param>
        /// <returns>StochasticResult containing all buffers</returns>
        /// <exception cref="ArgumentNullException">Thrown when input arrays are null</exception>
        /// <exception cref="ArgumentException">Thrown when parameters are invalid</exception>
        public static StochasticResult Calculate(
            double[] high,
            double[] low,
            double[] close,
            int kPeriod = 5,
            int dPeriod = 3,
            int slowing = 3)
        {
            // Input validation
            if (high == null) throw new ArgumentNullException(nameof(high));
            if (low == null) throw new ArgumentNullException(nameof(low));
            if (close == null) throw new ArgumentNullException(nameof(close));
            if (kPeriod <= 0) throw new ArgumentException("K period must be positive", nameof(kPeriod));
            if (dPeriod <= 0) throw new ArgumentException("D period must be positive", nameof(dPeriod));
            if (slowing <= 0) throw new ArgumentException("Slowing must be positive", nameof(slowing));

            var length = Math.Min(Math.Min(high.Length, low.Length), close.Length);
            var main = new double[length];
            var signal = new double[length];
            var highes = new double[length];
            var lowes = new double[length];

            if (length == 0)
                return new StochasticResult
                {
                    Main = main,
                    Signal = signal,
                    Highes = highes,
                    Lowes = lowes
                };

            // Calculate highest highs and lowest lows over kPeriod
            for (var i = 0; i < length; i++)
                if (i < kPeriod - 1)
                {
                    highes[i] = 0.0;
                    lowes[i] = 0.0;
                }
                else
                {
                    var highestHigh = double.MinValue;
                    var lowestLow = double.MaxValue;

                    for (var k = i - kPeriod + 1; k <= i; k++)
                    {
                        if (high[k] > highestHigh) highestHigh = high[k];
                        if (low[k] < lowestLow) lowestLow = low[k];
                    }

                    highes[i] = highestHigh;
                    lowes[i] = lowestLow;
                }

            // Calculate raw %K values
            var rawK = new double[length];
            for (var i = 0; i < length; i++)
                if (i < kPeriod - 1)
                {
                    rawK[i] = 0.0;
                }
                else
                {
                    var range = highes[i] - lowes[i];
                    if (range == 0.0)
                        rawK[i] = 100.0; // When range is 0, assume maximum momentum
                    else
                        rawK[i] = (close[i] - lowes[i]) / range * 100.0;
                }

            // Calculate slowed %K (main line)
            for (var i = 0; i < length; i++)
                if (i < kPeriod - 1 + slowing - 1)
                {
                    main[i] = 0.0;
                }
                else
                {
                    var sum = 0.0;
                    for (var k = i - slowing + 1; k <= i; k++) sum += rawK[k];
                    main[i] = sum / slowing;
                }

            // Calculate %D (signal line) - SMA of %K
            for (var i = 0; i < length; i++)
                if (i < dPeriod - 1)
                {
                    signal[i] = 0.0;
                }
                else
                {
                    var sum = 0.0;
                    for (var k = i - dPeriod + 1; k <= i; k++) sum += main[k];
                    signal[i] = sum / dPeriod;
                }

            return new StochasticResult
            {
                Main = main,
                Signal = signal,
                Highes = highes,
                Lowes = lowes
            };
        }
    }
}