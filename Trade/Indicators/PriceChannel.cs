namespace Trade.Indicators
{
    public class PriceChannelResult
    {
        public double[] Lower;
        public double[] Median;
        public double[] Upper;
    }

    public static class PriceChannel
    {
        /// <summary>
        ///     Calculates the Price Channel indicator.
        /// </summary>
        /// <param name="high">High price array</param>
        /// <param name="low">Low price array</param>
        /// <param name="period">Channel period (default 22)</param>
        /// <returns>PriceChannelResult containing upper, lower, and median buffers</returns>
        public static PriceChannelResult Calculate(
            double[] high,
            double[] low,
            int period = 22)
        {
            var length = high.Length;
            var upper = new double[length];
            var lower = new double[length];
            var median = new double[length];

            // Initialize first 'period' values as 0.0 (empty)
            for (var i = 0; i < period; i++)
            {
                upper[i] = 0.0;
                lower[i] = 0.0;
                median[i] = 0.0;
            }

            for (var i = period; i < length; i++)
            {
                upper[i] = Highest(high, period, i);
                lower[i] = Lowest(low, period, i);
                median[i] = (upper[i] + lower[i]) / 2.0;
            }

            return new PriceChannelResult
            {
                Upper = upper,
                Lower = lower,
                Median = median
            };
        }

        private static double Highest(double[] array, int range, int fromIndex)
        {
            var res = array[fromIndex];
            for (var i = fromIndex - 1; i > fromIndex - range && i >= 0; i--)
                if (res < array[i])
                    res = array[i];
            return res;
        }

        private static double Lowest(double[] array, int range, int fromIndex)
        {
            var res = array[fromIndex];
            for (var i = fromIndex - 1; i > fromIndex - range && i >= 0; i--)
                if (res > array[i])
                    res = array[i];
            return res;
        }
    }
}