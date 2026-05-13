using System;
using System.Linq;

namespace Trade.Indicators
{
    // Static class for Awesome Oscillator (AO) indicator calculation
    public static class AwesomeOscillator
    {
        // Calculate AO for price buffers
        // Returns an array of AO values
        public static double[] Calculate(double[] high, double[] low, int fastPeriod = 5, int slowPeriod = 34)
        {
            // ? FIXED: Handle null inputs gracefully
            if (high == null || low == null)
                return new double[0];

            var rates_total = Math.Min(high.Length, low.Length);
            if (rates_total < slowPeriod)
                return new double[0];

            var median = new double[rates_total];
            for (var i = 0; i < rates_total; i++)
                median[i] = (high[i] + low[i]) / 2.0;

            var fastSMA = CalculateSMA(median, fastPeriod);
            var slowSMA = CalculateSMA(median, slowPeriod);
            var ao = new double[rates_total];

            for (var i = 0; i < rates_total; i++)
                // AO is only valid when both fast and slow SMAs are valid
                // Fast SMA is valid from index (fastPeriod-1)
                // Slow SMA is valid from index (slowPeriod-1)
                // So AO is valid from index (slowPeriod-1) since slowPeriod > fastPeriod
                if (i >= slowPeriod - 1)
                    ao[i] = fastSMA[i] - slowSMA[i];
                else
                    ao[i] = 0.0;
            return ao;
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