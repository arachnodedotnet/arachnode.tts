namespace Trade.Indicators
{
    public class WPRResult
    {
        public double[] WPR;
    }

    public static class WPR
    {
        /// <summary>
        ///     Calculates the Williams' Percent Range (%R) indicator.
        /// </summary>
        /// <param name="high">High price array</param>
        /// <param name="low">Low price array</param>
        /// <param name="close">Close price array</param>
        /// <param name="period">WPR period (default 14)</param>
        /// <returns>WPRResult containing the WPR buffer</returns>
        public static WPRResult Calculate(double[] high, double[] low, double[] close, int period = 14)
        {
            if (period < 3)
                period = 14;

            var length = close.Length;
            var wpr = new double[length];

            // Initialize first values to 0.0
            for (var i = 0; i < period - 1 && i < length; i++)
                wpr[i] = 0.0;

            for (var i = period - 1; i < length; i++)
            {
                var maxHigh = Highest(high, period, i);
                var minLow = Lowest(low, period, i);

                if (maxHigh != minLow)
                    wpr[i] = -(maxHigh - close[i]) * 100.0 / (maxHigh - minLow);
                else
                    wpr[i] = i > 0 ? wpr[i - 1] : 0.0;
            }

            return new WPRResult
            {
                WPR = wpr
            };
        }

        private static double Highest(double[] array, int period, int curPosition)
        {
            var res = array[curPosition];
            for (var i = curPosition - 1; i > curPosition - period && i >= 0; i--)
                if (res < array[i])
                    res = array[i];
            return res;
        }

        private static double Lowest(double[] array, int period, int curPosition)
        {
            var res = array[curPosition];
            for (var i = curPosition - 1; i > curPosition - period && i >= 0; i--)
                if (res > array[i])
                    res = array[i];
            return res;
        }
    }
}