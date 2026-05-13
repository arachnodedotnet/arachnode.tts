using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Trade.Tests
{
    [TestClass]
    public class IntradayLinearDataGeneratorTests
    {
        [TestMethod][TestCategory("Core")]
        public void TestGenerateIntradayLinearPriceData()
        {
            // Test parameters based on SPX_d date range and intraday requirements
            const string testFileName = "test_intraday_linear_prices.csv";
            var startDate = new DateTime(2024, 5, 1);
            var endDate = new DateTime(2025, 8, 1);
            const int minutesPerDay = 390; // Standard market hours: 9:30 AM - 4:15 PM
            const int cycleLengthDays = 10;
            
            try
            {
                GenerateIntradayLinearDataFile(testFileName, startDate, endDate, cycleLengthDays, minutesPerDay);
                
                // Verify file was created
                Assert.IsTrue(File.Exists(testFileName), "Test file should be created");
                
                // Read and verify the content
                var lines = File.ReadAllLines(testFileName);
                
                // Should have header + data rows
                Assert.IsTrue(lines.Length > 1, "Should have header plus data rows");
                
                // Verify header format
                Assert.AreEqual("DateTime,Open,High,Low,Close,Volume", lines[0], "Header should match expected intraday format");
                
                // Verify some data integrity
                VerifyIntradayDataIntegrity(lines, startDate, endDate, minutesPerDay, cycleLengthDays);
                
                var totalMinutes = lines.Length - 1; // excluding header
                Console.WriteLine($"Successfully generated {testFileName} with {totalMinutes:N0} minutes of intraday data");
                Console.WriteLine($"Pattern: {cycleLengthDays}-day cycles with linear appreciation/depreciation");
                Console.WriteLine($"Date range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            }
            finally
            {
                // Clean up test file
                if (File.Exists(testFileName))
                {
                    File.Delete(testFileName);
                }
            }
        }
        
        [TestMethod][TestCategory("Core")]
        public void TestGenerateActualIntradayLinearFile()
        {
            // Generate actual file for use (this one we keep)
            const string fileName = "intraday_linear_spx_data.csv";
            var startDate = new DateTime(2024, 5, 1);
            var endDate = new DateTime(2025, 8, 1);
            const int cycleLengthDays = 10;
            const int minutesPerDay = 390;
            
            GenerateIntradayLinearDataFile(fileName, startDate, endDate, cycleLengthDays, minutesPerDay);
            
            // Verify it exists
            Assert.IsTrue(File.Exists(fileName), "Intraday linear data file should be created");
            
            var lines = File.ReadAllLines(fileName);
            var totalTradingDays = GetTradingDayCount(startDate, endDate);
            var expectedMinutes = totalTradingDays * minutesPerDay;
            
            Console.WriteLine($"Generated {fileName} with comprehensive intraday data");
            Console.WriteLine($"File size: {new FileInfo(fileName).Length / 1024.0:F2} KB");
            
            Console.WriteLine($"\n?? Intraday Data Statistics:");
            Console.WriteLine($"   • Total minutes: {lines.Length - 1:N0}");
            Console.WriteLine($"   • Trading days covered: {totalTradingDays:N0}");
            Console.WriteLine($"   • Minutes per day: {minutesPerDay}");
            Console.WriteLine($"   • Expected total minutes: {expectedMinutes:N0}");
            Console.WriteLine($"   • Date range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            Console.WriteLine($"   • Cycle length: {cycleLengthDays} trading days");
            
            // Show first few and last few lines for verification
            Console.WriteLine("\nFirst 5 data rows:");
            for (int i = 1; i <= Math.Min(5, lines.Length - 1); i++)
            {
                Console.WriteLine($"  {lines[i]}");
            }
            
            Console.WriteLine("\nLast 5 data rows:");
            for (int i = Math.Max(1, lines.Length - 5); i < lines.Length; i++)
            {
                Console.WriteLine($"  {lines[i]}");
            }
            
            Console.WriteLine($"\n?? Comparison with Daily SPX Data:");
            Console.WriteLine($"   • Daily SPX records: {totalTradingDays:N0} days");
            Console.WriteLine($"   • Intraday records: {lines.Length - 1:N0} minutes");
            Console.WriteLine($"   • Granularity increase: {(lines.Length - 1) / (double)totalTradingDays:F0}x more data points");
        }
        
        private void GenerateIntradayLinearDataFile(string fileName, DateTime startDate, DateTime endDate, 
            int cycleLengthDays, int minutesPerDay)
        {
            const double basePrice = 5000.0; // Starting around SPX levels
            const double cyclePriceRange = 500.0; // ±500 points over cycle
            const long baseVolume = 1000000; // 1M base volume per minute
            
            var random = new Random(42); // Fixed seed for reproducible results
            var tradingDays = GetTradingDays(startDate, endDate);
            
            Console.WriteLine($"Generating intraday data for {tradingDays.Count:N0} trading days...");
            
            var totalMinutesWritten = 0; // Move variable to correct scope
            
            using (var writer = new StreamWriter(fileName))
            {
                // Write header
                writer.WriteLine("DateTime,Open,High,Low,Close,Volume");
                
                var cycleDay = 0;
                
                foreach (var tradingDay in tradingDays)
                {
                    if (tradingDay.Subtract(startDate).Days % 50 == 0)
                    {
                        Console.WriteLine($"  Processing {tradingDay:yyyy-MM-dd} ({totalMinutesWritten:N0} minutes generated)...");
                    }
                    
                    // Determine cycle position (0-9 within 10-day cycle)
                    var dayInCycle = cycleDay % cycleLengthDays;
                    
                    // Calculate day's price trend
                    double dayStartPrice, dayEndPrice;
                    if (dayInCycle < cycleLengthDays / 2)
                    {
                        // First half of cycle: appreciation
                        var progress = (double)dayInCycle / (cycleLengthDays / 2 - 1);
                        dayStartPrice = basePrice + (cyclePriceRange * progress);
                        dayEndPrice = basePrice + (cyclePriceRange * (progress + 1.0 / (cycleLengthDays / 2 - 1)));
                    }
                    else
                    {
                        // Second half of cycle: depreciation
                        var progress = (double)(dayInCycle - cycleLengthDays / 2) / (cycleLengthDays / 2 - 1);
                        dayStartPrice = basePrice + cyclePriceRange - (cyclePriceRange * progress);
                        dayEndPrice = basePrice + cyclePriceRange - (cyclePriceRange * (progress + 1.0 / (cycleLengthDays / 2 - 1)));
                    }
                    
                    // Generate intraday minutes for this trading day
                    GenerateIntradayMinutes(writer, tradingDay, dayStartPrice, dayEndPrice, 
                        minutesPerDay, random, baseVolume);
                    
                    totalMinutesWritten += minutesPerDay;
                    cycleDay++;
                }
            }
            
            Console.WriteLine($"Generation complete: {totalMinutesWritten:N0} minutes across {tradingDays.Count:N0} trading days");
        }
        
        private void GenerateIntradayMinutes(StreamWriter writer, DateTime tradingDay, 
            double dayStartPrice, double dayEndPrice, int minutesPerDay, Random random, long baseVolume)
        {
            var marketOpen = tradingDay.Add(new TimeSpan(9, 30, 0)); // 9:30 AM
            var priceRange = dayEndPrice - dayStartPrice;
            
            double previousClose = dayStartPrice;
            
            for (int minute = 0; minute < minutesPerDay; minute++)
            {
                var currentTime = marketOpen.AddMinutes(minute);
                var progress = (double)minute / (minutesPerDay - 1);
                
                // Linear interpolation for the day's trend
                var targetPrice = dayStartPrice + (priceRange * progress);
                
                // Add some intraday noise (±0.1% random variation)
                var noise = (random.NextDouble() - 0.5) * 0.002 * targetPrice;
                var currentPrice = Math.Round(targetPrice + noise, 2);
                
                // Calculate OHLC for this minute
                var open = Math.Round(previousClose, 2);
                var close = currentPrice;
                
                // High and Low with realistic intraday ranges
                var volatility = 0.001; // 0.1% intraday volatility
                var high = Math.Round(Math.Max(open, close) + (random.NextDouble() * volatility * currentPrice), 2);
                var low = Math.Round(Math.Min(open, close) - (random.NextDouble() * volatility * currentPrice), 2);
                
                // Generate volume with some intraday patterns
                var volumeMultiplier = GetVolumeMultiplier(minute, minutesPerDay);
                var volume = (long)(baseVolume * volumeMultiplier * (0.8 + 0.4 * random.NextDouble()));
                
                // Write the minute bar
                var timeStr = currentTime.ToString("yyyy-MM-dd HH:mm:ss");
                writer.WriteLine($"{timeStr},{open:F2},{high:F2},{low:F2},{close:F2},{volume}");
                
                previousClose = close;
            }
        }
        
        private double GetVolumeMultiplier(int minute, int minutesPerDay)
        {
            // Higher volume at market open/close, lower in middle of day
            var progress = (double)minute / minutesPerDay;
            
            if (progress < 0.1) // First hour
                return 2.0;
            else if (progress > 0.9) // Last hour
                return 1.8;
            else if (progress > 0.4 && progress < 0.6) // Lunch time
                return 0.6;
            else
                return 1.0; // Normal trading
        }
        
        private List<DateTime> GetTradingDays(DateTime startDate, DateTime endDate)
        {
            var tradingDays = new List<DateTime>();
            var current = startDate;
            
            while (current <= endDate)
            {
                // Skip weekends (Saturday = 6, Sunday = 0)
                if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
                {
                    // Skip major holidays (simplified - you could add more sophisticated holiday detection)
                    if (!IsHoliday(current))
                    {
                        tradingDays.Add(current);
                    }
                }
                current = current.AddDays(1);
            }
            
            return tradingDays;
        }
        
        private bool IsHoliday(DateTime date)
        {
            // Simplified holiday detection - add more holidays as needed
            return (date.Month == 12 && date.Day == 25) || // Christmas
                   (date.Month == 1 && date.Day == 1) ||    // New Year's Day
                   (date.Month == 7 && date.Day == 4) ||    // Independence Day
                   (date.Month == 11 && date.DayOfWeek == DayOfWeek.Thursday && date.Day >= 22 && date.Day <= 28); // Thanksgiving (4th Thursday)
        }
        
        private int GetTradingDayCount(DateTime startDate, DateTime endDate)
        {
            return GetTradingDays(startDate, endDate).Count;
        }
        
        private void VerifyIntradayDataIntegrity(string[] lines, DateTime startDate, DateTime endDate, 
            int minutesPerDay, int cycleLengthDays)
        {
            Assert.IsTrue(lines.Length > minutesPerDay, "Should have at least one full trading day of data");
            
            // Verify first few records have proper DateTime format and price progression
            for (int i = 1; i <= Math.Min(10, lines.Length - 1); i++)
            {
                var parts = lines[i].Split(',');
                Assert.AreEqual(6, parts.Length, $"Line {i} should have 6 columns (DateTime,O,H,L,C,V)");
                
                // Verify DateTime can be parsed
                Assert.IsTrue(DateTime.TryParse(parts[0], out var dateTime), 
                    $"Line {i} should have valid DateTime: {parts[0]}");
                
                // Verify prices are reasonable
                Assert.IsTrue(double.TryParse(parts[1], out var open) && open > 0, "Open price should be positive");
                Assert.IsTrue(double.TryParse(parts[2], out var high) && high > 0, "High price should be positive");
                Assert.IsTrue(double.TryParse(parts[3], out var low) && low > 0, "Low price should be positive");
                Assert.IsTrue(double.TryParse(parts[4], out var close) && close > 0, "Close price should be positive");
                
                // Verify OHLC relationships
                Assert.IsTrue(high >= Math.Max(open, close), $"High should be >= max(open,close) on line {i}");
                Assert.IsTrue(low <= Math.Min(open, close), $"Low should be <= min(open,close) on line {i}");
                
                // Verify volume is reasonable
                Assert.IsTrue(long.TryParse(parts[5], out var volume) && volume > 0, "Volume should be positive");
            }
            
            Console.WriteLine($"? Data integrity verification passed");
            Console.WriteLine($"   Verified OHLC relationships, DateTime format, and positive values");
        }
    }
}