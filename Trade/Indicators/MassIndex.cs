namespace Trade.Indicators
{
    public class MassIndexResult
    {
        public double[] EMA_EMA_HL;
        public double[] EMA_HL;
        public double[] HL;
        public double[] MI;
    }

    public static class MassIndex
    {
        /// <summary>
        ///     Calculates the Mass Index (MI) indicator.
        /// </summary>
        /// <param name="high">High price array</param>
        /// <param name="low">Low price array</param>
        /// <param name="periodEma">First EMA period</param>
        /// <param name="secondPeriodEma">Second EMA period</param>
        /// <param name="sumPeriod">Sum period</param>
        /// <returns>Mass Index result buffers</returns>
        public static MassIndexResult Calculate(
            double[] high,
            double[] low,
            int periodEma = 9,
            int secondPeriodEma = 9,
            int sumPeriod = 25)
        {
            var length = high.Length;
            var hl = new double[length];
            var emaHl = new double[length];
            var emaEmaHl = new double[length];
            var mi = new double[length];

            // Calculate HL buffer
            for (var i = 0; i < length; i++)
                hl[i] = high[i] - low[i];

            // Calculate EMA of HL
            CalculateEMA(hl, periodEma, emaHl);

            // Calculate EMA of EMA_HL
            CalculateEMA(emaHl, secondPeriodEma, emaEmaHl);

            // Calculate Mass Index
            var posMi = sumPeriod + periodEma + secondPeriodEma - 3;
            for (var i = 0; i < length; i++)
            {
                var dtmp = 0.0;
                if (i >= posMi)
                    for (var j = 0; j < sumPeriod; j++)
                    {
                        var idx = i - j;
                        if (idx >= 0 && emaEmaHl[idx] != 0.0)
                            dtmp += emaHl[idx] / emaEmaHl[idx];
                    }

                mi[i] = dtmp;
            }

            return new MassIndexResult
            {
                MI = mi,
                HL = hl,
                EMA_HL = emaHl,
                EMA_EMA_HL = emaEmaHl
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