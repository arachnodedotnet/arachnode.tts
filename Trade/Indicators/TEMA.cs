namespace Trade.Indicators
{
    public class TEMAResult
    {
        public double[] EMA;
        public double[] EMAofEMA;
        public double[] EMAofEMAofEMA;
        public double[] TEMA;
    }

    public static class TEMA
    {
        /// <summary>
        ///     Calculates the Triple Exponential Moving Average (TEMA) indicator.
        /// </summary>
        /// <param name="prices">Array of price values (e.g., close prices)</param>
        /// <param name="period">EMA period (default 14)</param>
        /// <param name="shift">Indicator shift (default 0)</param>
        /// <returns>TEMAResult containing all buffers</returns>
        public static TEMAResult Calculate(double[] prices, int period = 14, int shift = 0)
        {
            var length = prices.Length;
            var ema = new double[length];
            var emaOfEma = new double[length];
            var emaOfEmaOfEma = new double[length];
            var tema = new double[length];

            if (length < 3 * period - 3)
                return new TEMAResult { TEMA = tema, EMA = ema, EMAofEMA = emaOfEma, EMAofEMAofEMA = emaOfEmaOfEma };

            // Calculate EMA
            CalculateEMA(prices, period, ema);

            // Calculate EMA of EMA
            CalculateEMA(ema, period, emaOfEma);

            // Calculate EMA of EMA of EMA
            CalculateEMA(emaOfEma, period, emaOfEmaOfEma);

            // Calculate TEMA
            for (var i = 0; i < length; i++) tema[i] = 3 * ema[i] - 3 * emaOfEma[i] + emaOfEmaOfEma[i];

            // Apply shift if needed
            if (shift > 0)
            {
                var shiftedTema = new double[length];
                for (var i = 0; i < length - shift; i++)
                    shiftedTema[i + shift] = tema[i];
                tema = shiftedTema;
            }

            return new TEMAResult
            {
                TEMA = tema,
                EMA = ema,
                EMAofEMA = emaOfEma,
                EMAofEMAofEMA = emaOfEmaOfEma
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