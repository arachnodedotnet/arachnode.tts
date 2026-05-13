using System.Collections.Generic;

namespace Trade
{
    internal partial class Program
    {
        /// <summary>
        ///     Utility class for generating test data buffers and synthetic market data patterns.
        ///     Provides methods for creating triangular wave price patterns with corresponding minute-level data,
        ///     used extensively in trading system testing, backtesting validation, and algorithm development.
        ///     The generated data maintains realistic market characteristics while being predictable for testing.
        /// </summary>
        public static class BufferUtilities
        {
            #region Public Test Data Generation Methods

            /// <summary>
            ///     Generates a triangular wave price buffer with corresponding minute-level intraday data.
            ///     This method creates both daily price patterns and detailed 390-minute intraday patterns for each day,
            ///     maintaining mathematical relationships that mirror real market data structure.
            ///     Essential for D1 vs M1 validation testing and multi-timeframe algorithm verification.
            /// </summary>
            /// <param name="minPrice">Minimum price value for the triangular wave pattern</param>
            /// <param name="maxPrice">Maximum price value for the triangular wave pattern</param>
            /// <param name="cycles">Number of complete triangular wave cycles to generate across the dataset</param>
            /// <param name="totalPoints">Total number of daily data points to generate</param>
            /// <returns>
            ///     A tuple containing:
            ///     - triangleBuffer: Array of daily prices following triangular wave pattern
            ///     - minuteData: Dictionary mapping each day index to 390 minute-level prices for that day
            /// </returns>
            public static (double[] triangleBuffer, Dictionary<int, double[]> minuteData)
                GenerateTriangularWaveBufferWithMinuteData(
                    double minPrice, double maxPrice, int cycles, int totalPoints)
            {
                // Initialize output containers
                var triangleBuffer = new double[totalPoints];
                var minuteData = new Dictionary<int, double[]>();

                // Calculate wave parameters for consistent triangular pattern generation
                var priceRange = maxPrice - minPrice;
                var pointsPerCycle = (double)totalPoints / cycles;

                // Generate daily price points with triangular wave pattern
                for (var dayIndex = 0; dayIndex < totalPoints; dayIndex++)
                {
                    // Calculate current position within the triangular wave cycle (0.0 to 1.0)
                    var cyclePosition = dayIndex % pointsPerCycle / pointsPerCycle;

                    // Generate triangular wave value based on cycle position
                    double dailyPrice;
                    if (cyclePosition <= 0.5)
                        // First half of cycle: ascending from minimum to maximum price
                        // Linear interpolation: 0.0-0.5 maps to minPrice-maxPrice
                        dailyPrice = minPrice + cyclePosition * 2.0 * priceRange;
                    else
                        // Second half of cycle: descending from maximum to minimum price
                        // Linear interpolation: 0.5-1.0 maps to maxPrice-minPrice
                        dailyPrice = maxPrice - (cyclePosition - 0.5) * 2.0 * priceRange;

                    triangleBuffer[dayIndex] = dailyPrice;

                    // Generate corresponding minute-level data for this trading day
                    var minutePricesForDay = GenerateMinuteDataForDay(dailyPrice);
                    minuteData[dayIndex] = minutePricesForDay;
                }

                return (triangleBuffer, minuteData);
            }

            #endregion

            #region Private Helper Methods

            /// <summary>
            ///     Generates 390 minute-level price points for a single trading day.
            ///     Creates an intraday triangular pattern that oscillates around the daily price,
            ///     maintaining realistic intraday volatility while preserving mathematical relationships.
            ///     The 390 minutes represent a standard US trading session (6.5 hours × 60 minutes).
            /// </summary>
            /// <param name="dailyPrice">The base daily price around which to generate minute data</param>
            /// <returns>Array of 390 minute-level prices representing one trading day</returns>
            private static double[] GenerateMinuteDataForDay(double dailyPrice)
            {
                const int MinutesPerTradingDay = 390; // US market: 9:30 AM - 4:15 PM (6.5 hours)
                var minutePrices = new double[MinutesPerTradingDay];

                // Generate intraday triangular pattern with realistic volatility
                for (var minuteIndex = 0; minuteIndex < MinutesPerTradingDay; minuteIndex++)
                {
                    // Calculate position within the trading day (0.0 to 1.0)
                    var intradayCyclePosition = (double)minuteIndex / MinutesPerTradingDay;

                    // Generate minute-level price with triangular intraday pattern
                    double minutePrice;
                    if (intradayCyclePosition <= 0.5)
                        // Morning session: gradual rise with 5% downward bias, 10% total range
                        minutePrice = dailyPrice * 0.95 + intradayCyclePosition * 2.0 * (dailyPrice * 0.1);
                    else
                        // Afternoon session: gradual decline with 5% upward bias, 10% total range
                        minutePrice = dailyPrice * 1.05 - (intradayCyclePosition - 0.5) * 2.0 * (dailyPrice * 0.1);

                    minutePrices[minuteIndex] = minutePrice;
                }

                return minutePrices;
            }

            #endregion
        }
    }
}