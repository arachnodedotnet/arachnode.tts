namespace Trade.Indicators
{
    public enum VolumeType
    {
        Tick,
        Real
    }

    public class OBVResult
    {
        public double[] OBV;
    }

    public static class OBV
    {
        /// <summary>
        ///     Calculates the On Balance Volume (OBV) indicator.
        /// </summary>
        /// <param name="close">Array of close prices</param>
        /// <param name="volume">Array of volume values (tick or real)</param>
        /// <param name="volumeType">Volume type (Tick or Real)</param>
        /// <returns>OBVResult containing the OBV buffer</returns>
        public static OBVResult Calculate(
            double[] close,
            long[] tickVolume,
            long[] realVolume,
            VolumeType volumeType = VolumeType.Tick)
        {
            var length = close.Length;
            var obv = new double[length];
            var volume = volumeType == VolumeType.Tick ? tickVolume : realVolume;

            if (length < 2)
                return new OBVResult { OBV = obv };

            // Initialize first value
            obv[0] = volume[0];

            for (var i = 1; i < length; i++)
            {
                double vol = volume[i];
                var prevClose = close[i - 1];
                var currClose = close[i];

                if (currClose < prevClose)
                    obv[i] = obv[i - 1] - vol;
                else if (currClose > prevClose)
                    obv[i] = obv[i - 1] + vol;
                else
                    obv[i] = obv[i - 1];
            }

            return new OBVResult
            {
                OBV = obv
            };
        }
    }
}