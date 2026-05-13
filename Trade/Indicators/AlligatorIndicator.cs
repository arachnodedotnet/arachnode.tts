using System;
using System.Linq;

namespace Trade.Indicators
{
    // Static class for Alligator indicator calculation
    public static class AlligatorIndicator
    {
        // Calculate Alligator indicator (Jaws, Teeth, Lips) for a price buffer
        // Returns a tuple of arrays: (Jaws, Teeth, Lips)
        public static (double[] jaws, double[] teeth, double[] lips) Calculate(
            double[] high, double[] low,
            int jawsPeriod, int jawsShift,
            int teethPeriod, int teethShift,
            int lipsPeriod, int lipsShift)
        {
            // ✅ FIXED: Handle null inputs gracefully
            if (high == null || low == null)
                return (new double[0], new double[0], new double[0]);

            var rates_total = Math.Min(high.Length, low.Length);
            // ✅ FIXED: Need sufficient data for meaningful calculation
            var maxPeriod = Math.Max(Math.Max(jawsPeriod, teethPeriod), lipsPeriod);
            if (rates_total < maxPeriod)
                return (new double[0], new double[0], new double[0]);

            var median = new double[rates_total];
            for (var i = 0; i < rates_total; i++)
                median[i] = (high[i] + low[i]) / 2.0;
            var jaws = SmoothedMAWithShift(median, jawsPeriod, jawsShift);
            var teeth = SmoothedMAWithShift(median, teethPeriod, teethShift);
            var lips = SmoothedMAWithShift(median, lipsPeriod, lipsShift);
            return (jaws, teeth, lips);
        }

        // Helper: Smoothed Moving Average with shift
        private static double[] SmoothedMAWithShift(double[] buffer, int period, int shift)
        {
            var smma = new double[buffer.Length];
            if (buffer.Length < period)
                return smma; // Return array of zeros

            var prev = buffer.Take(period).Average();
            for (var i = 0; i < buffer.Length; i++)
                if (i < period)
                {
                    smma[i] = 0.0;
                }
                else
                {
                    prev = (prev * (period - 1) + buffer[i]) / period;
                    var shiftedIdx = i + shift;
                    if (shiftedIdx < buffer.Length)
                        smma[shiftedIdx] = prev;
                }

            return smma;
        }
    }
}