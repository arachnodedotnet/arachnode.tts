namespace Trade.Indicators
{
    public class VolumesResult
    {
        public int[] Colors; // 0 = Green (up), 1 = Red (down or equal)
        public double[] Volumes;
    }

    public static class Volumes
    {
        /// <summary>
        ///     Calculates the Volumes indicator with color coding.
        /// </summary>
        /// <param name="tickVolume">Array of tick volumes</param>
        /// <param name="realVolume">Array of real volumes</param>
        /// <param name="volumeType">Volume type (Tick or Real)</param>
        /// <returns>VolumesResult containing volume and color buffers</returns>
        public static VolumesResult Calculate(
            long[] tickVolume,
            long[] realVolume,
            VolumeType volumeType = VolumeType.Tick)
        {
            var volume = volumeType == VolumeType.Tick ? tickVolume : realVolume;
            var length = volume.Length;
            var volumes = new double[length];
            var colors = new int[length];

            if (length == 0)
                return new VolumesResult { Volumes = volumes, Colors = colors };

            // Handle first element (always gets volume value and Green color)
            volumes[0] = volume[0];
            colors[0] = 0;

            // Handle remaining elements (compare with previous for color)
            for (var i = 1; i < length; i++)
            {
                volumes[i] = volume[i];
                colors[i] = volume[i] > volume[i - 1] ? 0 : 1;
            }

            return new VolumesResult
            {
                Volumes = volumes,
                Colors = colors
            };
        }
    }
}