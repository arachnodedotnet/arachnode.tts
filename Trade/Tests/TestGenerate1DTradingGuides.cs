//using System;
//using System.IO;

//namespace Trade
//{
//    /// <summary>
//    /// Simple test program for Generate1DTradingGuides
//    /// </summary>
//    public class TestGenerate1DTradingGuides
//    {
//        public static void Main(string[] args)
//        {
//            Console.WriteLine("=== Testing Generate1DTradingGuides ===");

//            try
//            {
//                // Test validation
//                Console.WriteLine("1. Testing validation...");
//                var validation = Generate1DTradingGuides.ValidateExistingFiles("Constants.SPX_JSON");
//                Console.WriteLine($"   Regular file exists: {validation.RegularFileExists}");
//                Console.WriteLine($"   Options file exists: {validation.OptionsFileExists}");
//                Console.WriteLine($"   Requires regeneration: {validation.RequiresRegeneration}");

//                if (!string.IsNullOrEmpty(validation.ErrorMessage))
//                {
//                    Console.WriteLine($"   Validation error: {validation.ErrorMessage}");
//                }

//                // Test generation
//                Console.WriteLine("\n2. Testing generation...");
//                var result = Generate1DTradingGuides.GenerateTradingGuides("Constants.SPX_JSON");
//                Console.WriteLine($"   Generation success: {result.Success}");

//                if (result.Success)
//                {
//                    Console.WriteLine($"   Regular file: {result.RegularCsvPath} ({result.RegularRecordCount} records)");
//                    Console.WriteLine($"   Regular date range: {result.RegularDateRange}");
//                    Console.WriteLine($"   Options file: {result.OptionsCsvPath} ({result.OptionsRecordCount} records)");
//                    Console.WriteLine($"   Options date range: {result.OptionsDateRange}");
//                    Console.WriteLine($"   Generation time: {result.GenerationTimeMs:F0}ms");

//                    // Show sample content
//                    if (File.Exists(result.RegularCsvPath))
//                    {
//                        var lines = File.ReadAllLines(result.RegularCsvPath);
//                        Console.WriteLine($"\n   Regular file sample (first 3 lines):");
//                        for (int i = 0; i < Math.Min(3, lines.Length); i++)
//                        {
//                            Console.WriteLine($"     {lines[i]}");
//                        }
//                    }

//                    if (File.Exists(result.OptionsCsvPath))
//                    {
//                        var lines = File.ReadAllLines(result.OptionsCsvPath);
//                        Console.WriteLine($"\n   Options file sample (first 3 lines):");
//                        for (int i = 0; i < Math.Min(3, lines.Length); i++)
//                        {
//                            Console.WriteLine($"     {lines[i]}");
//                        }
//                    }
//                }
//                else
//                {
//                    Console.WriteLine($"   Generation failed: {result.ErrorMessage}");

//                    if (result.Warnings.Count > 0)
//                    {
//                        Console.WriteLine("   Warnings:");
//                        foreach (var warning in result.Warnings)
//                        {
//                            Console.WriteLine($"     - {warning}");
//                        }
//                    }
//                }

//                Console.WriteLine("\n✓ Test completed successfully!");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error: {ex.Message}");
//                Console.WriteLine($"Stack trace: {ex.StackTrace}");
//            }

//            Console.WriteLine("\nPress any key to exit...");
//            Console.ReadKey();
//        }
//    }
//}

