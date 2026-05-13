namespace Trade.Indicators
{
    public class PVTResult
    {
        public double[] PVT;
    }

    public static class PVT
    {
        /// <summary>
        ///     Calculates the Price and Volume Trend (PVT) indicator.
        /// </summary>
        /// <param name="close">Array of close prices</param>
        /// <param name="tickVolume">Array of tick volumes</param>
        /// <param name="realVolume">Array of real volumes</param>
        /// <param name="volumeType">Volume type (Tick or Real)</param>
        /// <returns>PVTResult containing the PVT buffer</returns>
        public static PVTResult Calculate(
            double[] close,
            long[] tickVolume,
            long[] realVolume,
            VolumeType volumeType = VolumeType.Tick)
        {
            var length = close.Length;
            var pvt = new double[length];
            var volume = volumeType == VolumeType.Tick ? tickVolume : realVolume;

            if (length < 2)
                return new PVTResult { PVT = pvt };

            pvt[0] = 0.0;

            for (var i = 1; i < length; i++)
            {
                var prevClose = close[i - 1];
                if (prevClose != 0)
                    pvt[i] = (close[i] - prevClose) / prevClose * volume[i] + pvt[i - 1];
                else
                    pvt[i] = pvt[i - 1];
            }

            return new PVTResult
            {
                PVT = pvt
            };
        }
    }
}