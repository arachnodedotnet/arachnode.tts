namespace Trade.Indicators
{
    public class OsMAResult
    {
        public double[] FastEMA;
        public double[] MACD;
        public double[] OsMA;
        public double[] Signal;
        public double[] SlowEMA;
    }

    public static class OsMA
    {
        /// <summary>
        ///     Calculates the OsMA (MACD histogram) indicator.
        /// </summary>
        /// <param name="prices">Array of price values (e.g., close prices)</param>
        /// <param name="fastEmaPeriod">Fast EMA period (default 12)</param>
        /// <param name="slowEmaPeriod">Slow EMA period (default 26)</param>
        /// <param name="signalEmaPeriod">Signal EMA period (default 9)</param>
        /// <returns>OsMAResult containing all buffers</returns>
        public static OsMAResult Calculate(
            double[] prices,
            int fastEmaPeriod = 12,
            int slowEmaPeriod = 26,
            int signalEmaPeriod = 9)
        {
            var length = prices.Length;
            var fastEma = new double[length];
            var slowEma = new double[length];
            var macd = new double[length];
            var signal = new double[length];
            var osma = new double[length];

            // Calculate Fast EMA
            CalculateEMA(prices, fastEmaPeriod, fastEma);

            // Calculate Slow EMA
            CalculateEMA(prices, slowEmaPeriod, slowEma);

            // Calculate MACD line
            for (var i = 0; i < length; i++)
                macd[i] = fastEma[i] - slowEma[i];

            // Calculate Signal line (EMA of MACD)
            CalculateEMA(macd, signalEmaPeriod, signal);

            // Calculate OsMA (MACD - Signal)
            for (var i = 0; i < length; i++)
                osma[i] = macd[i] - signal[i];

            return new OsMAResult
            {
                OsMA = osma,
                MACD = macd,
                Signal = signal,
                FastEMA = fastEma,
                SlowEMA = slowEma
            };
        }

        // Exponential Moving Average
        private static void CalculateEMA(double[] input, int period, double[] output)
        {
            var multiplier = 2.0 / (period + 1);
            output[0] = input[0];
            for (var i = 1; i < input.Length; i++)
                output[i] = (input[i] - output[i - 1]) * multiplier + output[i - 1];
        }
    }
}