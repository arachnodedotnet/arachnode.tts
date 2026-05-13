using System;

namespace Trade.Indicators
{
    // Static class for ADX Wilder indicator calculation
    public static class AdxWilderIndicator
    {
        // Calculate ADX Wilder, +DI, -DI for a price buffer
        // Returns a tuple of arrays: (ADXW, +DI, -DI)
        public static (double[] adxw, double[] plusDi, double[] minusDi) Calculate(double[] high, double[] low,
            double[] close, int period)
        {
            var rates_total = Math.Min(Math.Min(high.Length, low.Length), close.Length);
            if (rates_total < period)
                return (new double[0], new double[0], new double[0]);
            var pdBuffer = new double[rates_total];
            var ndBuffer = new double[rates_total];
            var trBuffer = new double[rates_total];
            var atrBuffer = new double[rates_total];
            var pdsBuffer = new double[rates_total];
            var ndsBuffer = new double[rates_total];
            var pdibuffer = new double[rates_total];
            var ndibuffer = new double[rates_total];
            var dxBuffer = new double[rates_total];
            var adxwBuffer = new double[rates_total];
            // Main loop
            for (var i = 1; i < rates_total; i++)
            {
                var high_price = high[i];
                var prev_high = high[i - 1];
                var low_price = low[i];
                var prev_low = low[i - 1];
                var prev_close = close[i - 1];
                var tmp_pos = high_price - prev_high;
                var tmp_neg = prev_low - low_price;
                if (tmp_pos < 0.0) tmp_pos = 0.0;
                if (tmp_neg < 0.0) tmp_neg = 0.0;
                if (tmp_neg == tmp_pos)
                {
                    tmp_neg = 0.0;
                    tmp_pos = 0.0;
                }
                else if (tmp_pos < tmp_neg)
                {
                    tmp_pos = 0.0;
                }
                else
                {
                    tmp_neg = 0.0;
                }

                pdBuffer[i] = tmp_pos;
                ndBuffer[i] = tmp_neg;
                trBuffer[i] = Math.Max(Math.Max(Math.Abs(high_price - low_price), Math.Abs(high_price - prev_close)),
                    Math.Abs(low_price - prev_close));
            }

            // Smoothed buffers
            for (var i = 0; i < rates_total; i++)
            {
                if (i < period)
                {
                    atrBuffer[i] = 0.0;
                    pdsBuffer[i] = 0.0;
                    ndsBuffer[i] = 0.0;
                }
                else
                {
                    atrBuffer[i] = SmoothedMA(i, period, atrBuffer[i - 1], trBuffer);
                    pdsBuffer[i] = SmoothedMA(i, period, pdsBuffer[i - 1], pdBuffer);
                    ndsBuffer[i] = SmoothedMA(i, period, ndsBuffer[i - 1], ndBuffer);
                }

                if (atrBuffer[i] != 0.0)
                {
                    pdibuffer[i] = 100.0 * pdsBuffer[i] / atrBuffer[i];
                    ndibuffer[i] = 100.0 * ndsBuffer[i] / atrBuffer[i];
                }
                else
                {
                    pdibuffer[i] = 0.0;
                    ndibuffer[i] = 0.0;
                }

                var dTmp = pdibuffer[i] + ndibuffer[i];
                if (dTmp != 0.0)
                    dxBuffer[i] = 100.0 * Math.Abs((pdibuffer[i] - ndibuffer[i]) / dTmp);
                else
                    dxBuffer[i] = 0.0;
                adxwBuffer[i] = i < period ? 0.0 : SmoothedMA(i, period, adxwBuffer[i - 1], dxBuffer);
            }

            return (adxwBuffer, pdibuffer, ndibuffer);
        }

        // Helper: Wilder's Smoothed Moving Average
        private static double SmoothedMA(int i, int period, double prev, double[] buffer)
        {
            // Wilder's smoothing: prev + (current - prev) / period
            return prev + (buffer[i] - prev) / period;
        }
    }
}