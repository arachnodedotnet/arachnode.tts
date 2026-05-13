//using System;
//using System.IO;
//using Trade.Tests;

//namespace Trade
//{
//    /// <summary>
//    /// Demonstration of intraday linear price data generation matching SPX date ranges
//    /// </summary>
//    public class IntradayLinearDataDemo
//    {
//        public static void Main(string[] args)
//        {
//            Console.WriteLine("?? Intraday Linear Price Data Generator");
//            Console.WriteLine("=======================================");
//            Console.WriteLine("Generating minute-by-minute data with linear price appreciation/depreciation");
//            Console.WriteLine("Based on SPX_d date ranges: 2024-05-01 to 2025-08-01");
            
//            try
//            {
//                // Show generation plan
//                Console.WriteLine("\n?? Generation Specifications:");
//                Console.WriteLine("   • Date Range: 2024-05-01 to 2025-08-01 (matching ^spx_d.csv)");
//                Console.WriteLine("   • Minutes per day: 390 (9:30 AM - 4:15 PM market hours)");
//                Console.WriteLine("   • Cycle length: 10 trading days");
//                Console.WriteLine("   • Pattern: Linear appreciation (5 days) ? Linear depreciation (5 days)");
//                Console.WriteLine("   • Price range: ~5000 ± 500 points per cycle");
//                Console.WriteLine("   • Format: DateTime,Open,High,Low,Close,Volume");
                
//                Console.WriteLine("\n?? Starting intraday data generation...");
//                var startTime = DateTime.Now;
                
//                // Generate the intraday data
//                var generator = new IntradayLinearDataGeneratorTests();
//                generator.TestGenerateActualIntradayLinearFile();
                
//                var endTime = DateTime.Now;
//                var duration = endTime - startTime;
                
//                Console.WriteLine($"?? Generation completed in {duration.TotalSeconds:F1} seconds");
                
//                // Analyze the generated file
//                const string fileName = "intraday_linear_spx_data.csv";
//                if (File.Exists(fileName))
//                {
//                    var lines = File.ReadAllLines(fileName);
//                    var fileInfo = new FileInfo(fileName);
                    
//                    Console.WriteLine($"\n?? Generated File Analysis:");
//                    Console.WriteLine($"   ? File: {fileName}");
//                    Console.WriteLine($"   ? Total records: {lines.Length - 1:N0} minutes");
//                    Console.WriteLine($"   ? File size: {fileInfo.Length / (1024.0 * 1024.0):F2} MB");
//                    Console.WriteLine($"   ? Equivalent trading days: {(lines.Length - 1) / 390.0:F0} days");
                    
//                    // Show data samples
//                    Console.WriteLine($"\n?? Sample Data (First 3 minutes):");
//                    for (int i = 1; i <= Math.Min(3, lines.Length - 1); i++)
//                    {
//                        var parts = lines[i].Split(',');
//                        Console.WriteLine($"   {parts[0]} | O:{parts[1]} H:{parts[2]} L:{parts[3]} C:{parts[4]} | V:{parts[5]}");
//                    }
                    
//                    Console.WriteLine($"\n?? Sample Data (Last 3 minutes):");
//                    for (int i = Math.Max(1, lines.Length - 3); i < lines.Length; i++)
//                    {
//                        var parts = lines[i].Split(',');
//                        Console.WriteLine($"   {parts[0]} | O:{parts[1]} H:{parts[2]} L:{parts[3]} C:{parts[4]} | V:{parts[5]}");
//                    }
                    
//                    Console.WriteLine($"\n?? Data Comparison:");
//                    Console.WriteLine($"   • Original SPX daily data: ~300 records (daily bars)");
//                    Console.WriteLine($"   • Generated intraday data: {lines.Length - 1:N0} records (minute bars)");
//                    Console.WriteLine($"   • Granularity increase: ~{(lines.Length - 1) / 300.0:F0}x more detailed");
                    
//                    Console.WriteLine($"\n?? Usage Scenarios:");
//                    Console.WriteLine($"   ? Intraday algorithm testing");
//                    Console.WriteLine($"   ? Scalping strategy backtesting"); 
//                    Console.WriteLine($"   ? High-frequency pattern analysis");
//                    Console.WriteLine($"   ? Market microstructure studies");
//                    Console.WriteLine($"   ? Linear trend following validation");
                    
//                    Console.WriteLine($"\n?? Pattern Details:");
//                    Console.WriteLine($"   • Each 10-day cycle: 5 days up + 5 days down");
//                    Console.WriteLine($"   • Linear intraday progression within each day");
//                    Console.WriteLine($"   • Realistic OHLC relationships maintained");
//                    Console.WriteLine($"   • Volume patterns: Higher at open/close, lower at lunch");
//                    Console.WriteLine($"   • Total data points: {lines.Length - 1:N0} minute bars");
//                }
//                else
//                {
//                    Console.WriteLine($"? File generation failed - {fileName} not found");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"? Error during generation: {ex.Message}");
//                Console.WriteLine($"Stack trace: {ex.StackTrace}");
//            }
            
//            Console.WriteLine("\nPress any key to exit...");
//            Console.ReadKey();
//        }
//    }
//}