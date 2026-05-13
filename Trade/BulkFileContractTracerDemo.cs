using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Trade.IVPreCalc2;

namespace Trade
{
    /// <summary>
    /// Quick demonstration of the enhanced BulkFileContractTracer with cut-bait optimization.
    /// This shows how the contract-aware logic prevents unnecessary file scanning.
    /// </summary>
    public static class BulkFileContractTracerDemo
    {
        public static async Task RunDemo()
        {
            ConsoleUtilities.WriteLine("=== BulkFileContractTracer Cut-Bait Optimization Demo ===");

            var testDir = Path.Combine(Path.GetTempPath(), "BulkFileDemo_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            var testFile = Path.Combine(testDir, "2025-01-15_options.csv");

            try
            {
                Directory.CreateDirectory(testDir);

                // Create realistic test data with proper contract ordering
                var testData = @"ticker,volume,open,close,high,low,window_start,transactions
O:AAPL250117C00220000,100,10.50,10.75,10.80,10.45,1737032400000000000,50
O:AAPL250117C00220000,80,10.75,10.60,10.85,10.55,1737032460000000000,40
O:AAPL250117C00225000,75,8.25,8.50,8.55,8.20,1737032520000000000,35
O:AAPL250117P00220000,50,5.25,5.50,5.60,5.20,1737032580000000000,25
O:SPY250117C00575000,200,12.50,12.75,12.85,12.40,1737032640000000000,100
O:SPY250117C00580000,150,10.25,10.50,10.60,10.20,1737032700000000000,75
O:SPY250117P00575000,100,8.75,9.00,9.10,8.70,1737032760000000000,50
O:TSLA250117C00250000,80,15.25,15.50,15.60,15.20,1737032820000000000,45";

                File.WriteAllText(testFile, testData, Encoding.UTF8);

                ConsoleUtilities.WriteLine($"Created test file with contract data at: {testFile}");
                ConsoleUtilities.WriteLine("Test data contains contracts in proper sort order:");
                ConsoleUtilities.WriteLine("  - AAPL 220 Call (2 records)");
                ConsoleUtilities.WriteLine("  - AAPL 225 Call");
                ConsoleUtilities.WriteLine("  - AAPL 220 Put");
                ConsoleUtilities.WriteLine("  - SPY 575 Call");
                ConsoleUtilities.WriteLine("  - SPY 580 Call");
                ConsoleUtilities.WriteLine("  - SPY 575 Put");
                ConsoleUtilities.WriteLine("  - TSLA 250 Call");
                ConsoleUtilities.WriteLine();

                using (var tracer = new BulkFileContractTracer(testDir))
                {
                    var startDate = new DateTime(2025, 1, 15);
                    var endDate = new DateTime(2025, 1, 15);

                    // Demo 1: Search for AAPL call - should use cut-bait when it hits AAPL puts
                    ConsoleUtilities.WriteLine("?? Searching for AAPL 220 Call:");
                    var aaplResult = await tracer.TraceContractBackwardsAsync("O:AAPL250117C00220000", startDate, endDate);
                    
                    ConsoleUtilities.WriteLine($"  ? Found {aaplResult.SearchStats.TotalRecordsFound} records in {aaplResult.SearchStats.FilesWithData} files");
                    if (aaplResult.Prices.Count > 0)
                    {
                        var daily = aaplResult.Prices[0];
                        ConsoleUtilities.WriteLine($"  ?? Daily summary: Open=${daily.Open:F2}, High=${daily.High:F2}, Low=${daily.Low:F2}, Close=${daily.Close:F2}");
                        ConsoleUtilities.WriteLine($"  ?? Volume: {daily.Volume}, Records: {daily.RecordCount}");
                    }
                    ConsoleUtilities.WriteLine($"  ?? Last file searched: {Path.GetFileName(aaplResult.LastFileSearched)}");
                    ConsoleUtilities.WriteLine();

                    // Demo 2: Search for SPY call - should cut bait when it hits SPY puts
                    ConsoleUtilities.WriteLine("?? Searching for SPY 575 Call:");
                    var spyResult = await tracer.TraceContractBackwardsAsync("O:SPY250117C00575000", startDate, endDate);
                    
                    ConsoleUtilities.WriteLine($"  ? Found {spyResult.SearchStats.TotalRecordsFound} records in {spyResult.SearchStats.FilesWithData} files");
                    if (spyResult.Prices.Count > 0)
                    {
                        var daily = spyResult.Prices[0];
                        ConsoleUtilities.WriteLine($"  ?? Daily summary: Open=${daily.Open:F2}, High=${daily.High:F2}, Low=${daily.Low:F2}, Close=${daily.Close:F2}");
                    }
                    ConsoleUtilities.WriteLine($"  ?? Last file searched: {Path.GetFileName(spyResult.LastFileSearched)}");
                    ConsoleUtilities.WriteLine();

                    // Demo 3: Search for contract that comes before our data (should cut bait immediately)
                    ConsoleUtilities.WriteLine("?? Searching for contract that sorts before our data:");
                    var earlyResult = await tracer.TraceContractBackwardsAsync("O:A250117C00100000", startDate, endDate);
                    
                    ConsoleUtilities.WriteLine($"  ? Cut bait optimization: Found {earlyResult.SearchStats.TotalRecordsFound} records (should be 0)");
                    ConsoleUtilities.WriteLine($"  ?? This demonstrates early termination when target sorts before file data");
                    ConsoleUtilities.WriteLine($"  ?? Last file searched: {Path.GetFileName(earlyResult.LastFileSearched)}");
                    ConsoleUtilities.WriteLine($"  ?? First/Last source files: {earlyResult.FirstSourceFile ?? "null"} / {earlyResult.LastSourceFile ?? "null"}");
                    ConsoleUtilities.WriteLine();

                    // Demo 4: Search for contract that comes after our data
                    ConsoleUtilities.WriteLine("?? Searching for contract that sorts after our data:");
                    var lateResult = await tracer.TraceContractBackwardsAsync("O:ZZZ250117C00100000", startDate, endDate);
                    
                    ConsoleUtilities.WriteLine($"  ? Cut bait optimization: Found {lateResult.SearchStats.TotalRecordsFound} records (should be 0)");
                    ConsoleUtilities.WriteLine($"  ?? This demonstrates termination when we pass the target in sort order");
                    ConsoleUtilities.WriteLine($"  ?? Last file searched: {Path.GetFileName(lateResult.LastFileSearched)}");
                    ConsoleUtilities.WriteLine($"  ?? First/Last source files: {lateResult.FirstSourceFile ?? "null"} / {lateResult.LastSourceFile ?? "null"}");
                }

                ConsoleUtilities.WriteLine("? Demo completed successfully!");
                ConsoleUtilities.WriteLine();
                ConsoleUtilities.WriteLine("Key benefits of the cut-bait optimization:");
                ConsoleUtilities.WriteLine("  1. ?? Stops searching when we know the target can't exist in remaining data");
                ConsoleUtilities.WriteLine("  2. ?? Uses ContractKeyComparer for intelligent sort order detection");
                ConsoleUtilities.WriteLine("  3. ?? Maintains accurate line number tracking for logging");
                ConsoleUtilities.WriteLine("  4. ?? Handles edge cases (contracts before/after file data range)");
                ConsoleUtilities.WriteLine("  5. ?? Preserves lookahead caching for optimal multi-contract searches");
                ConsoleUtilities.WriteLine("  6. ?? Tracks last file searched for debugging (even when no matches found)");
                ConsoleUtilities.WriteLine();
                ConsoleUtilities.WriteLine("LastFileSearched tracking benefits:");
                ConsoleUtilities.WriteLine("  • ?? Shows exactly where the search terminated");
                ConsoleUtilities.WriteLine("  • ?? Valuable for debugging search issues");
                ConsoleUtilities.WriteLine("  • ?? Distinguishes between 'no matches found' and 'search didn't run'");
                ConsoleUtilities.WriteLine("  • ?? Useful when FirstSourceFile and LastSourceFile are the same");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(testDir))
                        Directory.Delete(testDir, true);
                }
                catch { /* Ignore cleanup failures */ }
            }
        }
    }
}