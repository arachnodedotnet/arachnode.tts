using System;

namespace Trade.Indicators
{
    public class UltimateOscillatorResult
    {
        public double[] BP;
        public double[] FastATR;
        public double[] MiddleATR;
        public double[] SlowATR;
        public double[] UO;
    }

    public static class UltimateOscillator
    {
        /// <summary>
        ///     Calculates the Ultimate Oscillator indicator.
        /// </summary>
        /// <param name="high">High price array</param>
        /// <param name="low">Low price array</param>
        /// <param name="close">Close price array</param>
        /// <param name="fastPeriod">Fast ATR period (default 7)</param>
        /// <param name="middlePeriod">Middle ATR period (default 14)</param>
        /// <param name="slowPeriod">Slow ATR period (default 28)</param>
        /// <param name="fastK">Fast K (default 4)</param>
        /// <param name="middleK">Middle K (default 2)</param>
        /// <param name="slowK">Slow K (default 1)</param>
        /// <returns>UltimateOscillatorResult containing all buffers</returns>
        public static UltimateOscillatorResult Calculate(
            double[] high,
            double[] low,
            double[] close,
            int fastPeriod = 7,
            int middlePeriod = 14,
            int slowPeriod = 28,
            int fastK = 4,
            int middleK = 2,
            int slowK = 1)
        {
            var length = close.Length;
            var uo = new double[length];
            var bp = new double[length];
            var fastATR = new double[length];
            var middleATR = new double[length];
            var slowATR = new double[length];

            var maxPeriod = Math.Max(slowPeriod, Math.Max(middlePeriod, fastPeriod));
            double divider = fastK + middleK + slowK;

            // Calculate Buying Pressure (BP) and ATRs
            for (var i = 0; i < length; i++)
            {
                if (i == 0)
                {
                    bp[i] = 0.0;
                    fastATR[i] = 0.0;
                    middleATR[i] = 0.0;
                    slowATR[i] = 0.0;
                    uo[i] = 0.0;
                    continue;
                }

                var trueLow = Math.Min(low[i], close[i - 1]);
                bp[i] = close[i] - trueLow;

                fastATR[i] = ATR(i, fastPeriod, high, low, close);
                middleATR[i] = ATR(i, middlePeriod, high, low, close);
                slowATR[i] = ATR(i, slowPeriod, high, low, close);
            }

            // Calculate Ultimate Oscillator
            for (var i = 0; i < length; i++)
            {
                if (i < maxPeriod)
                {
                    uo[i] = 0.0;
                    continue;
                }

                var fastSumBP = SimpleMA(i, fastPeriod, bp);
                var middleSumBP = SimpleMA(i, middlePeriod, bp);
                var slowSumBP = SimpleMA(i, slowPeriod, bp);

                var fast = fastATR[i] != 0.0 ? fastK * fastSumBP / fastATR[i] : 0.0;
                var middle = middleATR[i] != 0.0 ? middleK * middleSumBP / middleATR[i] : 0.0;
                var slow = slowATR[i] != 0.0 ? slowK * slowSumBP / slowATR[i] : 0.0;

                var rawUO = fast + middle + slow;
                uo[i] = rawUO / divider * 100.0;
            }

            return new UltimateOscillatorResult
            {
                UO = uo,
                BP = bp,
                FastATR = fastATR,
                MiddleATR = middleATR,
                SlowATR = slowATR
            };
        }

        // Simple Moving Average (sum over period)
        private static double SimpleMA(int pos, int period, double[] buffer)
        {
            var sum = 0.0;
            for (var i = pos; i > pos - period && i >= 0; i--)
                sum += buffer[i];
            return sum;
        }

        // ATR calculation (Average True Range)
        private static double ATR(int pos, int period, double[] high, double[] low, double[] close)
        {
            if (pos < period)
                return 0.0;

            var sum = 0.0;
            for (var i = pos; i > pos - period; i--)
            {
                double tr;
                if (i == 0)
                {
                    tr = high[i] - low[i];
                }
                else
                {
                    var highLow = high[i] - low[i];
                    var highClose = Math.Abs(high[i] - close[i - 1]);
                    var lowClose = Math.Abs(low[i] - close[i - 1]);
                    tr = Math.Max(highLow, Math.Max(highClose, lowClose));
                }

                sum += tr;
            }

            return sum;
        }
    }
}