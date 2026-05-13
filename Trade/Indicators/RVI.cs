namespace Trade.Indicators
{
    public class RVIResult
    {
        public double[] RVI;
        public double[] Signal;
    }

    public static class RVI
    {
        private const int TrianglePeriod = 3;
        private const int AveragePeriod = TrianglePeriod * 2;

        /// <summary>
        ///     Calculates the Relative Vigor Index (RVI) and its signal line.
        /// </summary>
        /// <param name="open">Array of open prices</param>
        /// <param name="high">Array of high prices</param>
        /// <param name="low">Array of low prices</param>
        /// <param name="close">Array of close prices</param>
        /// <param name="period">RVI period (default 10)</param>
        /// <returns>RVIResult containing RVI and Signal buffers</returns>
        public static RVIResult Calculate(
            double[] open,
            double[] high,
            double[] low,
            double[] close,
            int period = 10)
        {
            var length = close.Length;
            var rvi = new double[length];
            var signal = new double[length];

            var minBars = period + AveragePeriod + 2;
            if (length <= minBars)
                return new RVIResult { RVI = rvi, Signal = signal };

            // Set empty value for uncalculated bars
            for (var i = 0; i < period + TrianglePeriod; i++)
                rvi[i] = 0.0;
            for (var i = 0; i < period + AveragePeriod; i++)
                signal[i] = 0.0;

            // RVI calculation
            for (var i = period + 2; i < length; i++)
            {
                var sumUp = 0.0;
                var sumDown = 0.0;
                for (var j = i; j > i - period; j--)
                {
                    var valueUp =
                        close[j] - open[j] +
                        2 * (close[j - 1] - open[j - 1]) +
                        2 * (close[j - 2] - open[j - 2]) +
                        close[j - 3] - open[j - 3];

                    var valueDown =
                        high[j] - low[j] +
                        2 * (high[j - 1] - low[j - 1]) +
                        2 * (high[j - 2] - low[j - 2]) +
                        high[j - 3] - low[j - 3];

                    sumUp += valueUp;
                    sumDown += valueDown;
                }

                rvi[i] = sumDown != 0.0 ? sumUp / sumDown : sumUp;
            }

            // Signal line calculation
            for (var i = period + TrianglePeriod + 2; i < length; i++)
                signal[i] = (
                    rvi[i] +
                    2 * rvi[i - 1] +
                    2 * rvi[i - 2] +
                    rvi[i - 3]
                ) / AveragePeriod;

            return new RVIResult
            {
                RVI = rvi,
                Signal = signal
            };
        }
    }
}