//using System;
//using System.IO;
//using Trade.Tests;

//namespace Trade
//{
//    /// <summary>
//    /// Quick demonstration of the doubled-scale cyclical data generation
//    /// </summary>
//    public class QuickScaleDemo
//    {
//        public static void Main(string[] args)
//        {
//            Console.WriteLine("?? Quick Scale Demonstration - Doubled Cyclical Data");
//            Console.WriteLine("=====================================================");
            
//            try
//            {
//                // Show what we're about to generate
//                Console.WriteLine("?? Generation Plan:");
//                Console.WriteLine("   • Cycles: 200 (doubled from original 100)");
//                Console.WriteLine("   • Days per cycle: 20 (10 up + 10 down)");
//                Console.WriteLine("   • Total trading days: 4,000");
//                Console.WriteLine("   • Price range: $100 ? $200 ? $100");
//                Console.WriteLine("   • Expected file size: ~200KB");
                
//                Console.WriteLine("\n?? Starting generation...");
//                var startTime = DateTime.Now;
                
//                // Generate the data
//                var generator = new CyclicalPriceDataGeneratorTests();
//                generator.TestGenerateActualCyclicalPriceFile();
                
//                var endTime = DateTime.Now;
//                var duration = endTime - startTime;
                
//                Console.WriteLine($"?? Generation completed in {duration.TotalSeconds:F2} seconds");
                
//                // Analyze the result
//                const string fileName = "cyclical_test_data.csv";
//                if (File.Exists(fileName))
//                {
//                    var lines = File.ReadAllLines(fileName);
//                    var fileInfo = new FileInfo(fileName);
                    
//                    Console.WriteLine($"\n?? Final Results:");
//                    Console.WriteLine($"   ? File created: {fileName}");
//                    Console.WriteLine($"   ? Trading days: {lines.Length - 1:N0}");
//                    Console.WriteLine($"   ? File size: {fileInfo.Length / 1024.0:F2} KB");
//                    Console.WriteLine($"   ? Equivalent years: {(lines.Length - 1) / 252.0:F1} years");
                    
//                    // Quick validation
//                    if (lines.Length - 1 == 4000)
//                    {
//                        Console.WriteLine($"   ?? Scale verification: PASSED (exactly 4,000 days)");
//                    }
//                    else
//                    {
//                        Console.WriteLine($"   ? Scale verification: FAILED (expected 4,000, got {lines.Length - 1})");
//                    }
                    
//                    Console.WriteLine($"\n?? Comparison to SPX Data:");
//                    Console.WriteLine($"   • SPX file (~825 days): Normal scale for recent market data");
//                    Console.WriteLine($"   • Cyclical file (4,000 days): Extended scale for comprehensive testing");
//                    Console.WriteLine($"   • Ratio: ~4.8x larger dataset for thorough algorithm validation");
//                }
//                else
//                {
//                    Console.WriteLine($"? File generation failed - {fileName} not found");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"? Error during generation: {ex.Message}");
//            }
            
//            Console.WriteLine("\nPress any key to exit...");
//            Console.ReadKey();
//        }
//    }
//}