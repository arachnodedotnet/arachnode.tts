using System;

namespace Trade.Indicators
{
    public enum SmoothMethod
    {
        SMA = 0, // Simple MA
        EMA = 1 // Exponential MA
    }

    public class ChaikinVolatility
    {
        // Static instance (singleton) with default parameters //TODO: Cache this...
        public static readonly ChaikinVolatility Instance = new ChaikinVolatility();

        public ChaikinVolatility(int smoothPeriod = 10, int chvPeriod = 10, SmoothMethod smoothType = SmoothMethod.EMA)
        {
            SmoothPeriod = smoothPeriod > 0 ? smoothPeriod : 10;
            CHVPeriod = chvPeriod > 0 ? chvPeriod : 10;
            SmoothType = smoothType;
        }

        public int SmoothPeriod { get; private set; }
        public int CHVPeriod { get; private set; }
        public SmoothMethod SmoothType { get; }

        public double[] Calculate(double[] high, double[] low, int fast = 10, int slow = 10)
        {
            //TODO: this may not be optimal...
            CHVPeriod = fast;
            SmoothPeriod = slow;
            
            var ratesTotal = Math.Min(high.Length, low.Length);
            var minBars = CHVPeriod + SmoothPeriod - 2;
            if (ratesTotal < minBars)
                return new double[0];

            var hlBuffer = new double[ratesTotal];
            var shlBuffer = new double[ratesTotal];
            var chvBuffer = new double[ratesTotal];

            // Fill H-L buffer
            for (var i = 0; i < ratesTotal; i++)
                hlBuffer[i] = high[i] - low[i];

            // Calculate smoothed H-L buffer
            if (SmoothType == SmoothMethod.SMA)
                SimpleMAOnBuffer(hlBuffer, shlBuffer, SmoothPeriod);
            else
                ExponentialMAOnBuffer(hlBuffer, shlBuffer, SmoothPeriod);

            // Calculate CHV buffer
            for (var i = minBars; i < ratesTotal; i++)
            {
                var prevIdx = i - CHVPeriod;
                if (shlBuffer[prevIdx] != 0.0)
                    chvBuffer[i] = 100.0 * (shlBuffer[i] - shlBuffer[prevIdx]) / shlBuffer[prevIdx];
                else
                    chvBuffer[i] = 0.0;
            }

            return chvBuffer;
        }

        private void SimpleMAOnBuffer(double[] source, double[] dest, int period)
        {
            var sum = 0.0;
            for (var i = 0; i < source.Length; i++)
            {
                sum += source[i];
                if (i >= period)
                    sum -= source[i - period];
                if (i >= period - 1)
                    dest[i] = sum / period;
                else
                    dest[i] = 0.0;
            }
        }

        private void ExponentialMAOnBuffer(double[] source, double[] dest, int period)
        {
            var k = 2.0 / (period + 1);
            dest[0] = source[0];
            for (var i = 1; i < source.Length; i++) dest[i] = k * source[i] + (1 - k) * dest[i - 1];
        }
    }
}