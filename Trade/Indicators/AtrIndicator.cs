using System;
using System.Linq;

namespace Trade.Indicators
{
    // Static class for Average True Range (ATR) indicator calculation
    public static class AtrIndicator
    {
        // Calculate ATR for price buffers
        // Returns an array of ATR values
        public static double[] Calculate(double[] high, double[] low, double[] close, int period)
        {
            // ✅ FIXED: Handle null arrays gracefully
            if (high == null || low == null || close == null)
                return new double[0];

            var rates_total = new[] { high.Length, low.Length, close.Length }.Min();
            if (rates_total < period)
                return new double[0];

            var tr = new double[rates_total];
            var atr = new double[rates_total];

            // Initialize first value
            tr[0] = 0.0;
            atr[0] = 0.0;

            // Calculate True Range values starting from index 1
            for (var i = 1; i < rates_total; i++)
                tr[i] = Math.Max(high[i], close[i - 1]) - Math.Min(low[i], close[i - 1]);

            // First 'period' ATR values are not calculated (initialization period)
            for (var i = 1; i < period; i++) atr[i] = 0.0;

            // Calculate first ATR value at index 'period'
            if (rates_total > period)
            {
                var firstValue = 0.0;
                for (var i = 1; i <= period; i++) firstValue += tr[i];
                atr[period] = firstValue / period;

                if (double.IsNaN(atr[period]))
                {
                    atr[period] = 0;
                }

                // Calculate subsequent ATR values using exponential smoothing
                for (var i = period + 1; i < rates_total; i++)
                {
                    atr[i] = ((period - 1) * atr[i - 1] + tr[i]) / period;
                    if (double.IsNaN(atr[i]))
                    {
                        atr[i] = 0;
                    }
                }
            }
            
            return atr;
        }
    }
}