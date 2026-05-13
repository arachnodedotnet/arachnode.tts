using System;

namespace Trade.Indicators
{
    // Static class for Bears Power indicator calculation
    public static class BearsPower
    {
        // Calculate Bears Power for price buffers
        // Returns an array of Bears Power values
        public static double[] Calculate(double[] low, double[] close, int period)
        {
            // ✅ FIXED: Handle null inputs gracefully
            if (low == null || close == null)
                return new double[0];

            var rates_total = Math.Min(low.Length, close.Length);
            // ✅ FIXED: Need sufficient data for EMA calculation to be meaningful
            // For EMA, we need at least the period length to produce reasonable results
            if (rates_total < period)
                return new double[0];

            var ema = CalculateEMA(close, period);
            var bears = new double[rates_total];
            for (var i = 0; i < rates_total; i++) bears[i] = low[i] - ema[i];
            return bears;
        }

        // Helper: Exponential Moving Average
        private static double[] CalculateEMA(double[] buffer, int period)
        {
            var ema = new double[buffer.Length];
            var k = 2.0 / (period + 1);
            ema[0] = buffer[0];
            for (var i = 1; i < buffer.Length; i++) ema[i] = buffer[i] * k + ema[i - 1] * (1 - k);
            return ema;
        }
    }
}