using System.Linq;

namespace Trade.Indicators
{
    public enum ChaikinOscillatorMaMethod
    {
        SMA = 0,
        EMA = 1,
        SMMA = 2,
        LWMA = 3
    }

    public static class ChaikinOscillator
    {
        // Calculate Chaikin Oscillator
        // high, low, close: price arrays
        // volume: volume array
        // fastMA, slowMA: periods
        // maMethod: moving average method
        public static double[] Calculate(double[] high, double[] low, double[] close, long[] volume, int fastMA,
            int slowMA, ChaikinOscillatorMaMethod maMethod)
        {
            // ? FIXED: Handle null inputs gracefully
            if (high == null || low == null || close == null || volume == null)
                return new double[0];

            var rates_total = new[] { high.Length, low.Length, close.Length, volume.Length }.Min();
            if (rates_total < slowMA)
                return new double[0];
            // Calculate AD buffer
            var ad = new double[rates_total];
            for (var i = 0; i < rates_total; i++)
            {
                var sum = close[i] - low[i] - (high[i] - close[i]);
                var adValue = 0.0;
                if (sum != 0.0 && high[i] != low[i])
                    adValue = sum / (high[i] - low[i]) * volume[i];
                if (i > 0)
                    adValue += ad[i - 1];
                ad[i] = adValue;
            }

            // Calculate Fast MA and Slow MA on AD buffer
            var fastMAArr = AverageOnArray(maMethod, ad, fastMA);
            var slowMAArr = AverageOnArray(maMethod, ad, slowMA);
            // Calculate oscillator
            var cho = new double[rates_total];
            for (var i = 0; i < rates_total; i++) cho[i] = fastMAArr[i] - slowMAArr[i];
            return cho;
        }

        // Helper: Calculate moving average on array
        private static double[] AverageOnArray(ChaikinOscillatorMaMethod mode, double[] source, int period)
        {
            switch (mode)
            {
                case ChaikinOscillatorMaMethod.EMA:
                    return CalculateEMA(source, period);
                case ChaikinOscillatorMaMethod.SMMA:
                    return CalculateSMMA(source, period);
                case ChaikinOscillatorMaMethod.LWMA:
                    return CalculateLWMA(source, period);
                default:
                    return CalculateSMA(source, period);
            }
        }

        // Simple Moving Average
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

        // Exponential Moving Average
        private static double[] CalculateEMA(double[] buffer, int period)
        {
            var ema = new double[buffer.Length];
            var k = 2.0 / (period + 1);

            for (var i = 0; i < buffer.Length; i++)
                if (i < period - 1)
                    ema[i] = 0.0;
                else if (i == period - 1)
                    // For the first valid point, use SMA of the first 'period' values
                    ema[i] = buffer.Take(period).Average();
                else
                    ema[i] = buffer[i] * k + ema[i - 1] * (1 - k);
            return ema;
        }

        // Smoothed Moving Average
        private static double[] CalculateSMMA(double[] buffer, int period)
        {
            var smma = new double[buffer.Length];
            smma[0] = buffer[0];
            for (var i = 1; i < buffer.Length; i++)
                if (i < period)
                    smma[i] = buffer.Take(i + 1).Average();
                else
                    smma[i] = (smma[i - 1] * (period - 1) + buffer[i]) / period;
            return smma;
        }

        // Linear Weighted Moving Average
        private static double[] CalculateLWMA(double[] buffer, int period)
        {
            var lwma = new double[buffer.Length];
            for (var i = 0; i < buffer.Length; i++)
                if (i < period - 1)
                {
                    lwma[i] = 0.0;
                }
                else
                {
                    var sum = 0.0;
                    var weightSum = 0.0;
                    for (var j = 0; j < period; j++)
                    {
                        double weight = j + 1;
                        sum += buffer[i - period + 1 + j] * weight;
                        weightSum += weight;
                    }

                    lwma[i] = sum / weightSum;
                }

            return lwma;
        }
    }
}