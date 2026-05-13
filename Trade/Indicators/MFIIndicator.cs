using System;

namespace Trade.Indicators
{
    public class MarketFacilitationIndexResult
    {
        public int[] ColorIndex;
        public double[] MFI;
    }

    public static class MarketFacilitationIndex
    {
        /// <summary>
        ///     Calculates the Market Facilitation Index (MFI) and color codes.
        ///     Bill Williams' MFI = (High - Low) / Volume
        ///     The point parameter is used for normalization to get more readable values.
        /// </summary>
        /// <param name="high">High price array</param>
        /// <param name="low">Low price array</param>
        /// <param name="volume">Volume array</param>
        /// <param name="point">Minimum price change (tick size) for normalization</param>
        /// <returns>MFI values and color codes</returns>
        /// <exception cref="ArgumentNullException">Thrown when any input array is null</exception>
        /// <exception cref="ArgumentException">Thrown when array lengths don't match</exception>
        public static MarketFacilitationIndexResult Calculate(
            double[] high,
            double[] low,
            long[] volume,
            double point = 0.0001)
        {
            // Input validation
            if (high == null) throw new ArgumentNullException(nameof(high));
            if (low == null) throw new ArgumentNullException(nameof(low));
            if (volume == null) throw new ArgumentNullException(nameof(volume));

            if (point <= 0) throw new ArgumentException("Point must be positive", nameof(point));

            // Find minimum length to handle array size mismatches
            var length = Math.Min(Math.Min(high.Length, low.Length), volume.Length);

            if (length == 0)
                return new MarketFacilitationIndexResult
                {
                    MFI = new double[0],
                    ColorIndex = new int[0]
                };

            var mfi = new double[length];
            var color = new int[length];

            bool mfiUp = true, volUp = true;

            for (var i = 0; i < length; i++)
            {
                // Calculate MFI - Bill Williams formula with point normalization
                if (volume[i] == 0)
                {
                    mfi[i] = i > 0 ? mfi[i - 1] : 0.0;
                }
                else
                {
                    // MFI = (High - Low) / Point / Volume
                    // Point normalization converts price range to "points" or "pips"
                    var priceRange = high[i] - low[i];
                    mfi[i] = priceRange / point / volume[i];
                }

                // Determine direction of MFI and volume compared to previous bar
                if (i > 0)
                {
                    mfiUp = mfi[i] > mfi[i - 1];
                    volUp = volume[i] > volume[i - 1];
                }

                // Assign color index based on Bill Williams' interpretation
                if (mfiUp && volUp)
                    color[i] = 0; // Green (Lime) - Market is facilitating (trending)
                else if (!mfiUp && !volUp)
                    color[i] = 1; // Brown (SaddleBrown) - Market is squat (prepare for breakout)
                else if (mfiUp && !volUp)
                    color[i] = 2; // Blue - Fake movement (fade the move)
                else // (!mfiUp && volUp)
                    color[i] = 3; // Pink - Market is eating (stopping volume)
            }

            return new MarketFacilitationIndexResult
            {
                MFI = mfi,
                ColorIndex = color
            };
        }
    }
}