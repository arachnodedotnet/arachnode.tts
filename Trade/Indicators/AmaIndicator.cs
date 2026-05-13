using System;

namespace Trade.Indicators
{
    // Static class for Adaptive Moving Average (AMA) indicator calculation
    public static class AmaIndicator
    {
        // Calculate AMA for a price buffer
        // Returns an array of AMA values
        public static double[] Calculate(double[] price, int amaPeriod, int fastPeriod, int slowPeriod)
        {
            // ? FIXED: Handle null input gracefully
            if (price == null)
                return new double[0];

            var rates_total = price.Length;
            if (rates_total < amaPeriod)
                return new double[0];
            var fastSC = 2.0 / (fastPeriod + 1.0);
            var slowSC = 2.0 / (slowPeriod + 1.0);
            var ama = new double[rates_total];
            // Initial values
            for (var i = 0; i < amaPeriod - 1; i++)
                ama[i] = 0.0;
            ama[amaPeriod - 1] = price[amaPeriod - 1];
            // Main cycle
            for (var i = amaPeriod; i < rates_total; i++)
            {
                var er = CalculateER(i, price, amaPeriod);
                var currentSSC = er * (fastSC - slowSC) + slowSC;
                var prevAMA = ama[i - 1];
                ama[i] = Math.Pow(currentSSC, 2) * (price[i] - prevAMA) + prevAMA;
            }

            return ama;
        }

        // Helper: Calculate Efficiency Ratio (ER)
        private static double CalculateER(int pos, double[] price, int amaPeriod)
        {
            if (pos - amaPeriod < 0) return 0.0;
            var signal = Math.Abs(price[pos] - price[pos - amaPeriod]);
            var noise = 0.0;
            for (var delta = 0; delta < amaPeriod; delta++)
                noise += Math.Abs(price[pos - delta] - price[pos - delta - 1]);
            if (noise != 0.0)
                return signal / noise;
            return 0.0;
        }
    }
}