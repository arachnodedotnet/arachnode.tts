namespace Trade.Indicators
{
    public class RSIResult
    {
        public double[] NegBuffer;
        public double[] PosBuffer;
        public double[] RSI;
    }

    public static class RSI
    {
        /// <summary>
        ///     Calculates the Relative Strength Index (RSI) indicator.
        /// </summary>
        /// <param name="prices">Array of price values (e.g., close prices)</param>
        /// <param name="period">RSI period (default 14)</param>
        /// <returns>RSIResult containing RSI, positive, and negative buffers</returns>
        public static RSIResult Calculate(double[] prices, int period = 14)
        {
            if (period < 1)
                period = 14;

            var length = prices.Length;
            var rsi = new double[length];
            var posBuffer = new double[length];
            var negBuffer = new double[length];

            if (length <= period)
                return new RSIResult { RSI = rsi, PosBuffer = posBuffer, NegBuffer = negBuffer };

            // Initialize buffers
            rsi[0] = 0.0;
            posBuffer[0] = 0.0;
            negBuffer[0] = 0.0;

            var sumPos = 0.0;
            var sumNeg = 0.0;

            for (var i = 1; i <= period; i++)
            {
                rsi[i] = 0.0;
                posBuffer[i] = 0.0;
                negBuffer[i] = 0.0;
                var diff = prices[i] - prices[i - 1];
                sumPos += diff > 0 ? diff : 0;
                sumNeg += diff < 0 ? -diff : 0;
            }

            posBuffer[period] = sumPos / period;
            negBuffer[period] = sumNeg / period;

            if (negBuffer[period] != 0.0)
                rsi[period] = 100.0 - 100.0 / (1.0 + posBuffer[period] / negBuffer[period]);
            else
                rsi[period] = posBuffer[period] != 0.0 ? 100.0 : 50.0;

            // Main calculation loop
            for (var i = period + 1; i < length; i++)
            {
                var diff = prices[i] - prices[i - 1];
                posBuffer[i] = (posBuffer[i - 1] * (period - 1) + (diff > 0.0 ? diff : 0.0)) / period;
                negBuffer[i] = (negBuffer[i - 1] * (period - 1) + (diff < 0.0 ? -diff : 0.0)) / period;

                if (negBuffer[i] != 0.0)
                    rsi[i] = 100.0 - 100.0 / (1.0 + posBuffer[i] / negBuffer[i]);
                else
                    rsi[i] = posBuffer[i] != 0.0 ? 100.0 : 50.0;
            }

            return new RSIResult
            {
                RSI = rsi,
                PosBuffer = posBuffer,
                NegBuffer = negBuffer
            };
        }
    }
}