using System;

namespace Trade.Indicators
{
    // Static class for Average Directional Movement Index (ADX) indicator calculation
    public static class AdxIndicator
    {
        // Calculate ADX, +DI, -DI for a price buffer
        // Returns a tuple of arrays: (ADX, +DI, -DI)
        public static (double[] adx, double[] plusDi, double[] minusDi) Calculate(double[] high, double[] low,
            double[] close, int period)
        {
            var rates_total = Math.Min(Math.Min(high.Length, low.Length), close.Length);
            if (rates_total < period)
                return (new double[0], new double[0], new double[0]);
            var pd = new double[rates_total];
            var nd = new double[rates_total];
            var pdSmooth = new double[rates_total];
            var ndSmooth = new double[rates_total];
            var tmpBuffer = new double[rates_total];
            var adxBuffer = new double[rates_total];
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
                if (tmp_pos > tmp_neg)
                {
                    tmp_neg = 0.0;
                }
                else if (tmp_pos < tmp_neg)
                {
                    tmp_pos = 0.0;
                }
                else
                {
                    tmp_pos = 0.0;
                    tmp_neg = 0.0;
                }

                var tr = Math.Max(Math.Max(Math.Abs(high_price - low_price), Math.Abs(high_price - prev_close)),
                    Math.Abs(low_price - prev_close));
                if (tr != 0.0)
                {
                    pd[i] = 100.0 * tmp_pos / tr;
                    nd[i] = 100.0 * tmp_neg / tr;
                }
                else
                {
                    pd[i] = 0.0;
                    nd[i] = 0.0;
                }
            }

            // Smoothed +DI and -DI
            pdSmooth[0] = pd[0];
            ndSmooth[0] = nd[0];
            for (var i = 1; i < rates_total; i++)
            {
                pdSmooth[i] = ExponentialMA(i, period, pdSmooth[i - 1], pd);
                ndSmooth[i] = ExponentialMA(i, period, ndSmooth[i - 1], nd);
            }

            // ADX calculation
            for (var i = 0; i < rates_total; i++)
            {
                var tmp = pdSmooth[i] + ndSmooth[i];
                if (tmp != 0.0)
                    tmpBuffer[i] = 100.0 * Math.Abs((pdSmooth[i] - ndSmooth[i]) / tmp);
                else
                    tmpBuffer[i] = 0.0;
            }

            adxBuffer[0] = tmpBuffer[0];
            for (var i = 1; i < rates_total; i++) adxBuffer[i] = ExponentialMA(i, period, adxBuffer[i - 1], tmpBuffer);
            return (adxBuffer, pdSmooth, ndSmooth);
        }

        // Helper: Exponential Moving Average
        private static double ExponentialMA(int i, int period, double prev, double[] buffer)
        {
            var k = 2.0 / (period + 1);
            return buffer[i] * k + prev * (1 - k);
        }
    }
}