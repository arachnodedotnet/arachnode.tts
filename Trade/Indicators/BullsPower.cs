using System;

namespace Trade.Indicators
{
    // Static class for Bulls Power indicator calculation
    public static class BullsPower
    {
        // Calculate Bulls Power for price buffers
        // Returns an array of Bulls Power values
        public static double[] Calculate(double[] high, double[] close, int period)
        {
            // ✅ FIXED: Handle null inputs gracefully
            if (high == null || close == null)
                return new double[0];

            var rates_total = Math.Min(high.Length, close.Length);
            // ✅ FIXED: Need sufficient data for EMA calculation to be meaningful
            // For EMA, we need at least the period length to produce reasonable results
            if (rates_total < period)
                return new double[0];

            var ema = CalculateEMA(close, period);
            var bulls = new double[rates_total];
            for (var i = 0; i < rates_total; i++) bulls[i] = high[i] - ema[i];
            return bulls;
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