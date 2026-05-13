using System;
using System.Linq;

namespace Trade.Indicators
{
    // Static class for Accelerator/Decelerator Oscillator calculation
    public static class AcceleratorOscillator
    {
        // Calculate Accelerator Oscillator (AC) values for a price buffer
        // Returns an array of AC values
        public static double[] Calculate(double[] high, double[] low, int fastPeriod = 5, int slowPeriod = 34)
        {
            // ? FIXED: Handle null inputs gracefully
            if (high == null || low == null)
                return new double[0];

            var rates_total = Math.Min(high.Length, low.Length);
            if (rates_total < slowPeriod) // SLOW_PERIOD
                return new double[0];

            var FAST_PERIOD = fastPeriod;
            var SLOW_PERIOD = slowPeriod;
            var median = new double[rates_total];
            for (var i = 0; i < rates_total; i++)
                median[i] = (high[i] + low[i]) / 2.0;

            var fastSMA = CalculateSMA(median, FAST_PERIOD);
            var slowSMA = CalculateSMA(median, SLOW_PERIOD);
            var ao = new double[rates_total];

            // Calculate AO (Awesome Oscillator)
            for (var i = 0; i < rates_total; i++)
                // AO is only valid after SLOW_PERIOD-1 points
                if (i >= SLOW_PERIOD - 1)
                    ao[i] = fastSMA[i] - slowSMA[i];
                else
                    ao[i] = 0.0;

            // Calculate smoothed AO (5-period SMA of AO)
            var smoothedAO = CalculateSMA(ao, FAST_PERIOD);

            // Calculate AC (Accelerator Oscillator)
            var ac = new double[rates_total];
            for (var i = 0; i < rates_total; i++)
            {
                // AC is only valid after we have both valid AO and valid smoothed AO
                // AO is valid from index (SLOW_PERIOD - 1)
                // Smoothed AO is valid from index (SLOW_PERIOD - 1) + (FAST_PERIOD - 1)
                var minValidIndex = SLOW_PERIOD - 1 + FAST_PERIOD - 1;
                if (i >= minValidIndex)
                    ac[i] = ao[i] - smoothedAO[i];
                else
                    ac[i] = 0.0;
            }

            return ac;
        }

        // Helper: Simple Moving Average
        private static double[] CalculateSMA(double[] buffer, int period)
        {
            var sma = new double[buffer.Length];
            for (var i = 0; i < buffer.Length; i++)
                if (i < period - 1)
                    sma[i] = 0.0;
                else
                    sma[i] = buffer.Skip(i - period + 1).Take(period).Average();
            return sma;
        }
    }
}