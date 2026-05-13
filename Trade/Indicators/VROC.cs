namespace Trade.Indicators
{
    public class VROCResult
    {
        public double[] VROC;
    }

    public static class VROC
    {
        /// <summary>
        ///     Calculates the Volume Rate of Change (VROC) indicator.
        /// </summary>
        /// <param name="tickVolume">Array of tick volumes</param>
        /// <param name="realVolume">Array of real volumes</param>
        /// <param name="period">VROC period (default 25)</param>
        /// <param name="volumeType">Volume type (Tick or Real)</param>
        /// <returns>VROCResult containing the VROC buffer</returns>
        public static VROCResult Calculate(
            long[] tickVolume,
            long[] realVolume,
            int period = 25,
            VolumeType volumeType = VolumeType.Tick)
        {
            if (period < 1)
                period = 25;

            var volume = volumeType == VolumeType.Tick ? tickVolume : realVolume;
            var length = volume.Length;

            if (length == 0)
                return new VROCResult { VROC = new double[0] };

            var vroc = new double[length];

            // Initialize first 'period' values to 0.0
            for (var i = 0; i < period && i < length; i++)
                vroc[i] = 0.0;

            for (var i = period; i < length; i++)
            {
                double prevVolume = volume[i - period];
                double currVolume = volume[i];
                if (prevVolume != 0.0)
                    vroc[i] = 100.0 * (currVolume - prevVolume) / prevVolume;
                else
                    vroc[i] = i > 0 ? vroc[i - 1] : 0.0;
            }

            return new VROCResult
            {
                VROC = vroc
            };
        }
    }
}