using System;

namespace Trade.Indicators
{
    // Static class for Accumulation/Distribution (A/D) indicator calculation
    public static class AccumulationDistribution
    {
        // Calculate A/D values for a price buffer
        // Returns an array of A/D values
        public static double[] Calculate(double[] high, double[] low, double[] close, long[] volume)
        {
            var rates_total = Math.Min(Math.Min(high.Length, low.Length), Math.Min(close.Length, volume.Length));
            if (rates_total < 2)
                return new double[0];
            var ad = new double[rates_total];
            for (var i = 0; i < rates_total; i++)
            {
                var hi = high[i];
                var lo = low[i];
                var cl = close[i];
                var sum = cl - lo - (hi - cl);
                if (hi == lo)
                    sum = 0.0;
                else
                    sum = sum / (hi - lo) * volume[i];
                if (i > 0)
                    sum += ad[i - 1];
                ad[i] = sum;
            }

            return ad;
        }
    }
}