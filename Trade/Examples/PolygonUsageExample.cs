using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Trade.Polygon2;
using Trade.Prices2;

namespace Trade.Examples
{
    /// <summary>
    ///     Example usage of the Polygon.cs class for generating option requests from price data
    ///     Enhanced with minute data functionality
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class PolygonUsageExample
    {
        /// <summary>
        ///     Creates sample price data for demonstration purposes
        ///     Enhanced with minute-level data
        /// </summary>
        private static Prices CreateSamplePriceData()
        {
            var prices = new Prices(); // Empty constructor
            var baseDate = DateTime.Now.AddDays(-60); // Start 60 days ago
            var basePrice = 450.0; // Starting price for SPY

            // Generate 60 days of sample price data with some realistic movement
            var random = new Random(42); // Fixed seed for reproducible results

            for (var day = 0; day < 60; day++)
            {
                var date = baseDate.AddDays(day);

                // Skip weekends
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                // Generate realistic price movement (random walk with slight upward bias)
                var dailyChange = (random.NextDouble() - 0.45) * 10; // Slight upward bias
                basePrice += dailyChange;

                // Ensure price doesn't go negative
                basePrice = Math.Max(basePrice, 100.0);

                // Create minute-level data for recent days (last 10 trading days)
                if (day >= 50) // Last 10 days
                {
                    CreateMinuteDataForDay(prices, date, basePrice, random);
                }
                else
                {
                    // Create daily data for older days
                    var open = basePrice + (random.NextDouble() - 0.5) * 2;
                    var close = basePrice + (random.NextDouble() - 0.5) * 2;
                    var high = Math.Max(open, close) + random.NextDouble() * 3;
                    var low = Math.Min(open, close) - random.NextDouble() * 3;
                    var volume = 50000000 + random.Next(20000000); // Realistic SPY volume

                    var record = new PriceRecord(date, TimeFrame.D1, open, high, low, close, volume: volume, wap: close, count: 1000);
                    prices.AddPrice(record);
                }
            }

            ConsoleUtilities.WriteLine(
                $"Created {prices.Records.Count} sample price records (including minute data for recent days)");

            // Calculate min/max prices manually to avoid LINQ on List<PriceRecord>
            var minPrice = double.MaxValue;
            var maxPrice = double.MinValue;

            foreach (var record in prices.Records)
            {
                if (record.Low < minPrice) minPrice = record.Low;
                if (record.High > maxPrice) maxPrice = record.High;
            }

            ConsoleUtilities.WriteLine($"Price range: ${minPrice:F2} - ${maxPrice:F2}");

            return prices;
        }

        /// <summary>
        ///     Creates realistic minute-level data for a specific trading day
        /// </summary>
        private static void CreateMinuteDataForDay(Prices prices, DateTime date, double basePrice, Random random)
        {
            // Market hours: 9:30 AM - 4:15 PM (390 minutes)
            var marketOpen = date.Date.AddHours(9).AddMinutes(30);
            var currentPrice = basePrice;

            for (var minute = 0; minute < 390; minute++)
            {
                var time = marketOpen.AddMinutes(minute);

                // Generate minute-level price movement
                var minuteChange = (random.NextDouble() - 0.5) * 0.5; // Smaller moves for minutes
                currentPrice += minuteChange;
                currentPrice = Math.Max(currentPrice, basePrice * 0.95); // Don't move too far from base
                currentPrice = Math.Min(currentPrice, basePrice * 1.05);

                // Create OHLC data for the minute
                var open = currentPrice + (random.NextDouble() - 0.5) * 0.1;
                var close = currentPrice + (random.NextDouble() - 0.5) * 0.1;
                var high = Math.Max(open, close) + random.NextDouble() * 0.2;
                var low = Math.Min(open, close) - random.NextDouble() * 0.2;
                var volume = 10000 + random.Next(5000); // Realistic minute volume

                var record = new PriceRecord(time, TimeFrame.M1, open, high, low, close, volume: volume, wap: close, count: 10);
                prices.AddPrice(record);
            }
        }

        /// <summary>
        ///     Analyzes strike price distribution for a result set
        /// </summary>
        private static void AnalyzeStrikePrices(string label, OptionRequestResult result)
        {
            if (!result.AllRequests.Any()) return;

            var strikes = result.AllRequests.Select(r => r.StrikePrice).ToList();
            var uniqueStrikes = strikes.Distinct().Count();
            var minStrike = strikes.Min();
            var maxStrike = strikes.Max();
            var avgStrike = strikes.Average();

            ConsoleUtilities.WriteLine(
                $"  {label}: {uniqueStrikes} unique strikes, range ${minStrike:F2}-${maxStrike:F2}, avg ${avgStrike:F2}");
        }

        /// <summary>
        ///     Analyzes underlying price distribution for a result set
        /// </summary>
        private static void AnalyzeUnderlyingPrices(string label, OptionRequestResult result)
        {
            if (!result.AllRequests.Any()) return;

            var underlyingPrices = result.AllRequests.Select(r => r.UnderlyingPrice).ToList();
            var uniquePrices = underlyingPrices.Distinct().Count();
            var minPrice = underlyingPrices.Min();
            var maxPrice = underlyingPrices.Max();
            var priceRange = maxPrice - minPrice;

            ConsoleUtilities.WriteLine(
                $"  {label}: {uniquePrices} unique prices, range ${minPrice:F2}-${maxPrice:F2} (${priceRange:F2} spread)");
        }
    }
}