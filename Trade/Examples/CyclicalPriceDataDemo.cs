//using System;
//using System.IO;
//using Trade.Tests;

//namespace Trade
//{
//    /// <summary>
//    /// Simple demonstration of cyclical price data generation - DOUBLED SCALE VERSION
//    /// </summary>
//    public class CyclicalPriceDataDemo
//    {
//        public static void Main(string[] args)
//        {
//            Console.WriteLine("=== Cyclical Price Data Generator Demo (DOUBLED SCALE) ===");
            
//            try
//            {
//                // Run the test that generates the cyclical data
//                var testInstance = new CyclicalPriceDataGeneratorTests();
//                //testInstance.TestGenerateActualCyclicalPriceFile();
                
//                Console.WriteLine("\n? Cyclical price data file generated successfully!");
//                Console.WriteLine("?? Check for 'cyclical_test_data.csv' in the project directory");
                
//                // Show file stats if it exists
//                const string fileName = "cyclical_test_data.csv";
//                if (File.Exists(fileName))
//                {
//                    var fileInfo = new FileInfo(fileName);
//                    var lines = File.ReadAllLines(fileName);
                    
//                    Console.WriteLine($"\n?? File Statistics:");
//                    Console.WriteLine($"   • File size: {fileInfo.Length / 1024.0:F2} KB");
//                    Console.WriteLine($"   • Total lines: {lines.Length:N0} (including header)");
//                    Console.WriteLine($"   • Data rows: {lines.Length - 1:N0}");
//                    Console.WriteLine($"   • Trading days: {lines.Length - 1:N0}");
//                    Console.WriteLine($"   • Equivalent years: {(lines.Length - 1) / 252.0:F1} (assuming 252 trading days/year)");
                    
//                    Console.WriteLine($"\n?? Pattern Details:");
//                    Console.WriteLine($"   • 200 complete cycles (DOUBLED from original 100)");
//                    Console.WriteLine($"   • Each cycle: 20 days (10 up + 10 down)");
//                    Console.WriteLine($"   • Price range: $100.00 ? $200.00 ? $100.00");
//                    Console.WriteLine($"   • Total pattern duration: 4,000 trading days");
                    
//                    Console.WriteLine($"\n?? Comparison with Real Market Data:");
//                    Console.WriteLine($"   • Real SPX data (^spx_d_for_options.csv): ~825 trading days");
//                    Console.WriteLine($"   • Generated cyclical data: {lines.Length - 1:N0} trading days");
//                    Console.WriteLine($"   • Scale factor: ~{(lines.Length - 1) / 825.0:F1}x larger dataset");
//                    Console.WriteLine($"   • Perfect for extensive backtesting and algorithm validation");
                    
//                    // Show memory/performance implications
//                    var estimatedMemoryMB = (lines.Length - 1) * 100 / (1024 * 1024); // rough estimate
//                    Console.WriteLine($"\n? Performance Notes:");
//                    Console.WriteLine($"   • Estimated memory usage: ~{estimatedMemoryMB:F1} MB when loaded");
//                    Console.WriteLine($"   • Processing time: Expect ~{(lines.Length - 1) / 1000.0:F1}x longer than 1K record datasets");
//                    Console.WriteLine($"   • Ideal for: Stress testing, statistical significance, long-term patterns");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"? Error: {ex.Message}");
//                Console.WriteLine($"Stack trace: {ex.StackTrace}");
//            }
            
//            Console.WriteLine("\nPress any key to exit...");
//            Console.ReadKey();
//        }
//    }
//}