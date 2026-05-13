using System.Collections.Generic;

namespace Trade.Indicators
{
    internal static class BufferUtilities
    {
        /// <summary>
        ///     Generates a triangular wave price buffer with both daily and minute-level data.
        ///     Each day represents one complete triangle cycle (min→max→min).
        ///     Minute data is LINEAR within each day, aligning with daily open/close prices.
        /// </summary>
        /// <param name="minPrice">Minimum price value</param>
        /// <param name="maxPrice">Maximum price value</param>
        /// <param name="cycles">Number of complete cycles (days)</param>
        /// <param name="totalPoints">Total number of data points for daily buffer</param>
        /// <returns>Tuple of (daily buffer, minute data dictionary) where each day index maps to 390 minute prices</returns>
        public static (double[] dailyBuffer, Dictionary<int, double[]> minuteData)
            GenerateTriangularWaveBufferWithMinuteData(double minPrice, double maxPrice, int cycles, int totalPoints)
        {
            // Generate daily buffer (original functionality)
            var dailyBuffer = new double[totalPoints];
            var range = maxPrice - minPrice;
            var pointsPerCycle = (double)totalPoints / cycles;

            for (var i = 0; i < totalPoints; i++)
            {
                // Calculate position within current cycle (0.0 to 1.0)
                var cyclePosition = i % pointsPerCycle / pointsPerCycle;

                double value;
                if (cyclePosition <= 0.5)
                    // First half: rise from min to max (0.0 to 0.5 maps to min to max)
                    value = minPrice + cyclePosition * 2.0 * range;
                else
                    // Second half: fall from max to min (0.5 to 1.0 maps to max to min)
                    value = maxPrice - (cyclePosition - 0.5) * 2.0 * range;

                dailyBuffer[i] = value;
            }

            // Generate minute-level data for each day
            var minuteData = new Dictionary<int, double[]>();
            const int minutesPerDay = 390; // 6.5 hours of trading (9:30 AM - 4:15 PM)

            // FIXED: Generate minute data for ALL daily data points, not just the number of cycles
            for (var dayIndex = 0; dayIndex < totalPoints; dayIndex++)
            {
                var dayMinuteData = new double[minutesPerDay];

                // Get the daily open and close prices from the triangular wave
                var dailyOpenPrice = dailyBuffer[dayIndex];

                // Calculate the next day's open price (which becomes today's close for continuity)
                double dailyClosePrice;
                if (dayIndex + 1 < dailyBuffer.Length)
                    dailyClosePrice = dailyBuffer[dayIndex + 1];
                else
                    // Last day - close at the same price as open (completing the cycle)
                    dailyClosePrice = dailyOpenPrice;

                // LINEAR interpolation between daily open and close prices
                for (var minute = 0; minute < minutesPerDay; minute++)
                {
                    var minutePosition = (double)minute / (minutesPerDay - 1); // 0.0 to 1.0 across trading day

                    // Linear interpolation from open to close
                    var minutePrice = dailyOpenPrice + (dailyClosePrice - dailyOpenPrice) * minutePosition;

                    dayMinuteData[minute] = minutePrice;
                }

                minuteData[dayIndex] = dayMinuteData;
            }

            return (dailyBuffer, minuteData);
        }
    }
}