using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.IVPreCalc2;

namespace Trade.Tests
{
    /// <summary>
    /// Console application to test line number tracking in BulkFileContractTracer
    /// Validates the exact scenario from the user's log output
    /// </summary>
    [TestClass]
    public class LineNumberTrackingConsoleTest
    {
        [TestMethod][TestCategory("Core")]
        public async Task TestLineNumberScenario()
        {
            // Test data matching the user's exact scenario
            var testCsvContent = @"ticker,volume,open,close,high,low,window_start,transactions
O:A250919C00120000,1,7.4,7.4,7.4,7.4,1758202980000000000,1
O:A250919C00120000,1,8,8,8,8,1758206280000000000,1
O:A250919C00120000,5,8.08,8.09,8.09,8.08,1758216300000000000,3
O:A250919C00120000,1,7.96,7.96,7.96,7.96,1758220320000000000,1
O:A250919C00120000,1,7.6,7.6,7.6,7.6,1758225120000000000,1
O:A250919C00125000,1,3.05,3.05,3.05,3.05,1758205500000000000,1
O:A250919C00125000,1,3.35,3.35,3.35,3.35,1758206040000000000,1
O:A250919C00130000,1,0.3,0.3,0.3,0.3,1758203400000000000,1";

            var tempDir = Path.Combine(Path.GetTempPath(), "LineNumberTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            var testFile = Path.Combine(tempDir, "2025-09-18_us_options_opra_minute_aggs.csv");
            
            try
            {
                Console.WriteLine($"Creating test file: {testFile}");
                Directory.CreateDirectory(tempDir);
                File.WriteAllText(testFile, testCsvContent, Encoding.UTF8);

                // Verify file was created correctly
                var lines = File.ReadAllLines(testFile);
                Console.WriteLine($"Test file created with {lines.Length} lines:");
                for (int i = 0; i < lines.Length; i++)
                {
                    Console.WriteLine($"  Line {i + 1}: {lines[i]}");
                }

                Console.WriteLine("\n--- RUNNING BULK FILE CONTRACT TRACER ---");
                using (var tracer = new BulkFileContractTracer(tempDir))
                {
                    var startDate = new DateTime(2025, 9, 18);
                    var endDate = new DateTime(2025, 9, 18);
                    
                    Console.WriteLine($"Tracing contract: O:A250919C00120000 from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
                    
                    var result = await tracer.TraceContractBackwardsAsync("O:A250919C00120000", startDate, endDate);
                    
                    Console.WriteLine("\n--- RESULTS ---");
                    if (result == null)
                    {
                        Console.WriteLine("? FAILED: Result is null");
                        return;
                    }
                    
                    if (result.Prices == null || result.Prices.Count == 0)
                    {
                        Console.WriteLine("? FAILED: No prices found");
                        return;
                    }
                    
                    Console.WriteLine($"? Found {result.Prices.Count} daily price summary");
                    var dailySummary = result.Prices[0];
                    Console.WriteLine($"? Record count: {dailySummary.RecordCount}");
                    Console.WriteLine($"? OHLC: O={dailySummary.Open:F2}, H={dailySummary.High:F2}, L={dailySummary.Low:F2}, C={dailySummary.Close:F2}");
                    
                    // Validate expectations
                    if (dailySummary.RecordCount == 5)
                        Console.WriteLine("? PASSED: Found exactly 5 records as expected");
                    else
                        Console.WriteLine($"? FAILED: Expected 5 records, got {dailySummary.RecordCount}");
                        
                    if (Math.Abs(dailySummary.Open - 7.4) < 0.01)
                        Console.WriteLine("? PASSED: Open price 7.4 matches line 2");
                    else
                        Console.WriteLine($"? FAILED: Expected open 7.4, got {dailySummary.Open}");
                        
                    if (Math.Abs(dailySummary.High - 8.09) < 0.01)
                        Console.WriteLine("? PASSED: High price 8.09 matches line 4");
                    else
                        Console.WriteLine($"? FAILED: Expected high 8.09, got {dailySummary.High}");
                        
                    if (Math.Abs(dailySummary.Close - 7.6) < 0.01)
                        Console.WriteLine("? PASSED: Close price 7.6 matches line 6");
                    else
                        Console.WriteLine($"? FAILED: Expected close 7.6, got {dailySummary.Close}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? EXCEPTION: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                        Console.WriteLine($"Cleaned up temp directory: {tempDir}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not clean up temp directory: {ex.Message}");
                }
            }
            
            Console.WriteLine("\n=== TEST COMPLETE ===");
        }
    }
}