using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Trade.Prices2;

namespace Trade.Examples
{
    /// <summary>
    ///     Demonstration program for Enhanced WindowOptimizer with Generate1DTradingGuides integration
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class DemoEnhancedWindowOptimizer
    {
        public static void RunDemo()
        {
            ConsoleUtilities.WriteLine("=== Enhanced WindowOptimizer Demo ===");
            ConsoleUtilities.WriteLine("This demo shows how WindowOptimizer now automatically generates");
            ConsoleUtilities.WriteLine("fresh CSV files with years of market data for robust analysis.");
            ConsoleUtilities.WriteLine();

            try
            {
                // Show current directory where files will be saved
                var currentDir = Directory.GetCurrentDirectory();
                ConsoleUtilities.WriteLine($"Current directory: {currentDir}");
                ConsoleUtilities.WriteLine("CSV files will be saved to this location.");
                ConsoleUtilities.WriteLine();

                // Create some minimal dummy price records to start with
                ConsoleUtilities.WriteLine("1. Creating initial dummy price records...");
                var dummyRecords = CreateDummyPriceRecords(100);
                ConsoleUtilities.WriteLine($"   Created {dummyRecords.Length} dummy records");
                ConsoleUtilities.WriteLine(
                    $"   Dummy date range: {dummyRecords[0].DateTime:yyyy-MM-dd} to {dummyRecords[dummyRecords.Length - 1].DateTime:yyyy-MM-dd}");
                ConsoleUtilities.WriteLine();

                // Show before state
                ConsoleUtilities.WriteLine("2. Checking for existing CSV files...");
                var regularExists = File.Exists(Constants.SPX_D);
                var optionsExists = File.Exists(Constants.SPX_D_FOR_OPTIONS);
                ConsoleUtilities.WriteLine($"   Constants.SPX_D exists: {regularExists}");
                ConsoleUtilities.WriteLine($"   Constants.SPX_D_FOR_OPTIONS exists: {optionsExists}");
                ConsoleUtilities.WriteLine();

                // Call the enhanced OptimizeWindowSizes method
                ConsoleUtilities.WriteLine("3. Calling enhanced OptimizeWindowSizes...");
                ConsoleUtilities.WriteLine("   This will automatically:");
                ConsoleUtilities.WriteLine("   • Generate fresh CSV files with years of market data");
                ConsoleUtilities.WriteLine("   • Reload all price data");
                ConsoleUtilities.WriteLine("   • Run window optimization with enhanced dataset");
                ConsoleUtilities.WriteLine();

                var results = WindowOptimizer.OptimizeWindowSizes(dummyRecords);

                // Show after state
                ConsoleUtilities.WriteLine();
                ConsoleUtilities.WriteLine("4. Checking results and generated files...");
                regularExists = File.Exists(Constants.SPX_D);
                optionsExists = File.Exists(Constants.SPX_D_FOR_OPTIONS);
                ConsoleUtilities.WriteLine($"   Constants.SPX_D exists: {regularExists}");
                ConsoleUtilities.WriteLine($"   Constants.SPX_D_FOR_OPTIONS exists: {optionsExists}");

                if (regularExists)
                {
                    var lines = File.ReadAllLines(Constants.SPX_D);
                    var recordCount = Math.Max(0, lines.Length - 1);
                    ConsoleUtilities.WriteLine($"   Regular CSV has {recordCount} data records");

                    if (lines.Length > 1)
                    {
                        var parts = lines[1].Split(',');
                        if (parts.Length > 0) ConsoleUtilities.WriteLine($"   First record date: {parts[0]}");

                        parts = lines[lines.Length - 1].Split(',');
                        if (parts.Length > 0) ConsoleUtilities.WriteLine($"   Last record date: {parts[0]}");
                    }
                }

                ConsoleUtilities.WriteLine();
                ConsoleUtilities.WriteLine("5. Optimization Results Summary:");
                ConsoleUtilities.WriteLine($"   Configurations tested: {results.ConfigurationResults.Count}");
                ConsoleUtilities.WriteLine(
                    $"   Optimal configuration found: {results.OptimalConfiguration.IsRecommended}");
                ConsoleUtilities.WriteLine($"   Total recommendations: {results.Recommendations.Count}");

                if (results.OptimalConfiguration.IsRecommended)
                {
                    var optimal = results.OptimalConfiguration;
                    ConsoleUtilities.WriteLine();
                    ConsoleUtilities.WriteLine("   ? OPTIMAL CONFIGURATION:");
                    ConsoleUtilities.WriteLine(
                        $"     Training: {optimal.Configuration.TrainingSize} periods ({optimal.Configuration.TrainingMonths:F1} months)");
                    ConsoleUtilities.WriteLine(
                        $"     Testing:  {optimal.Configuration.TestingSize} periods ({optimal.Configuration.TestingMonths:F1} months)");
                    ConsoleUtilities.WriteLine(
                        $"     Step:     {optimal.Configuration.StepSize} periods ({optimal.Configuration.StepWeeks:F1} weeks)");
                    ConsoleUtilities.WriteLine($"     Score:    {optimal.OverallScore:F3}/1.000");
                    ConsoleUtilities.WriteLine($"     Windows:  {optimal.WalkforwardResults.Windows.Count}");
                }

                ConsoleUtilities.WriteLine();
                ConsoleUtilities.WriteLine("=== Demo Completed Successfully! ===");
                ConsoleUtilities.WriteLine();
                ConsoleUtilities.WriteLine("KEY BENEFITS:");
                ConsoleUtilities.WriteLine("• Automatic generation of fresh market data CSV files");
                ConsoleUtilities.WriteLine("• Access to years worth of real S&P 500 price data");
                ConsoleUtilities.WriteLine("• Enhanced window optimization with larger datasets");
                ConsoleUtilities.WriteLine("• Improved statistical power for robust configuration testing");
                ConsoleUtilities.WriteLine("• Options data available for implied volatility calculations");
            }
            catch (Exception ex)
            {
                ConsoleUtilities.WriteLine($"Demo encountered an error: {ex.Message}");

                if (ex.Message.Contains("No daily price records available") ||
                    ex.Message.Contains("Failed to load source data") ||
                    ex.Message.Contains(Constants.SPX_JSON))
                {
                    ConsoleUtilities.WriteLine();
                    ConsoleUtilities.WriteLine(
                        "NOTE: This error is expected if Constants.SPX_JSON source data is not available.");
                    ConsoleUtilities.WriteLine("In a production environment with proper data sources,");
                    ConsoleUtilities.WriteLine("the system would generate years worth of market data automatically.");
                }
                else
                {
                    ConsoleUtilities.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            }
        }

        /// <summary>
        ///     Create dummy price records for demonstration
        /// </summary>
        private static PriceRecord[] CreateDummyPriceRecords(int count)
        {
            var records = new PriceRecord[count];
            var startDate = new DateTime(2024, 1, 1);
            var random = new Random(42); // Fixed seed for reproducibility

            var price = 100.0; // Starting price

            for (var i = 0; i < count; i++)
            {
                var date = startDate.AddDays(i);

                // Simple random walk
                var change = (random.NextDouble() - 0.5) * 2.0; // -1 to +1
                price += change;
                price = Math.Max(50.0, Math.Min(200.0, price)); // Keep price in reasonable range

                var open = price + (random.NextDouble() - 0.5) * 0.5;
                var close = price + (random.NextDouble() - 0.5) * 0.5;
                var high = Math.Max(open, close) + random.NextDouble() * 0.5;
                var low = Math.Min(open, close) - random.NextDouble() * 0.5;
                var volume = 1000000 + random.Next(500000);

                records[i] = new PriceRecord(date, TimeFrame.D1, open, high, low, close, volume: volume, wap: close, count: 1);
            }

            return records;
        }
    }
}