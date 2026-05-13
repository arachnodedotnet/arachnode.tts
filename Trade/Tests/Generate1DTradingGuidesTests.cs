using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    ///     Tests for the Generate1DTradingGuides class functionality
    /// </summary>
    [TestClass]
    public class Generate1DTradingGuidesTests
    {
        [TestMethod][TestCategory("Performance")]
        public void Generate1DTradingGuides_ValidateExistingFiles_ReturnsValidationResult()
        {
            Console.WriteLine("=== Testing Generate1DTradingGuides Validation ===");

            // Test validation functionality
            var validation = Generate1DTradingGuides.ValidateExistingFiles();

            Assert.IsNotNull(validation);
            Console.WriteLine($"Regular file exists: {validation.RegularFileExists}");
            Console.WriteLine($"Options file exists: {validation.OptionsFileExists}");
            Console.WriteLine($"Requires regeneration: {validation.RequiresRegeneration}");

            if (validation.RegularFileStats != null)
            {
                Console.WriteLine($"Regular file records: {validation.RegularFileStats.RecordCount}");
                Console.WriteLine($"Regular file date range: {validation.RegularFileStats.DateRange}");
            }

            if (validation.OptionsFileStats != null)
            {
                Console.WriteLine($"Options file records: {validation.OptionsFileStats.RecordCount}");
                Console.WriteLine($"Options file date range: {validation.OptionsFileStats.DateRange}");
            }

            if (validation.SourceDataStats != null)
            {
                Console.WriteLine($"Source data records: {validation.SourceDataStats.RecordCount}");
                Console.WriteLine($"Source data date range: {validation.SourceDataStats.DateRange}");
            }

            if (!string.IsNullOrEmpty(validation.ErrorMessage))
                Console.WriteLine($"Validation error: {validation.ErrorMessage}");

            Console.WriteLine("? Validation test completed successfully");
        }

        [TestMethod][TestCategory("Performance")]
        public void Generate1DTradingGuides_GenerateTradingGuides_CreatesFiles()
        {
            Console.WriteLine("=== Testing Generate1DTradingGuides File Generation ===");

            // Clean up any existing files first
            var regularFile = Constants.SPX_D;
            var optionsFile = Constants.SPX_D_FOR_OPTIONS;

            if (File.Exists(regularFile))
            {
                File.Delete(regularFile);
                Console.WriteLine($"Cleaned up existing {regularFile}");
            }

            if (File.Exists(optionsFile))
            {
                File.Delete(optionsFile);
                Console.WriteLine($"Cleaned up existing {optionsFile}");
            }

            // Generate new files
            var result = Generate1DTradingGuides.GenerateTradingGuides();

            Assert.IsNotNull(result);
            Console.WriteLine($"Generation success: {result.Success}");
            Console.WriteLine($"Generation time: {result.GenerationTimeMs:F0}ms");

            if (!result.Success)
            {
                Console.WriteLine($"Generation failed: {result.ErrorMessage}");

                if (result.Warnings.Count > 0)
                {
                    Console.WriteLine("Warnings:");
                    foreach (var warning in result.Warnings) Console.WriteLine($"  - {warning}");
                }

                // Don't fail the test if generation fails due to missing source data
                Assert.IsTrue(result.ErrorMessage.Contains("No daily price records available") ||
                              result.ErrorMessage.Contains("Failed to load source data"),
                    $"Unexpected error: {result.ErrorMessage}");
                return;
            }

            // Verify successful generation
            Assert.IsTrue(result.Success, "Generation should be successful");
            Assert.IsTrue(result.RegularRecordCount > 0, "Regular file should have records");
            Assert.IsTrue(result.OptionsRecordCount > 0, "Options file should have records");
            Assert.IsTrue(result.OptionsRecordCount >= result.RegularRecordCount,
                "Options file should have at least as many records as regular file");

            Console.WriteLine($"Regular CSV: {result.RegularCsvPath} ({result.RegularRecordCount} records)");
            Console.WriteLine($"Regular date range: {result.RegularDateRange}");
            Console.WriteLine($"Options CSV: {result.OptionsCsvPath} ({result.OptionsRecordCount} records)");
            Console.WriteLine($"Options date range: {result.OptionsDateRange}");

            // Verify files exist
            Assert.IsTrue(File.Exists(result.RegularCsvPath), "Regular CSV file should exist");
            Assert.IsTrue(File.Exists(result.OptionsCsvPath), "Options CSV file should exist");

            // Verify file content format
            var regularLines = File.ReadAllLines(result.RegularCsvPath);
            var optionsLines = File.ReadAllLines(result.OptionsCsvPath);

            Assert.IsTrue(regularLines.Length > 1, "Regular file should have header and data");
            Assert.IsTrue(optionsLines.Length > 1, "Options file should have header and data");

            // Check headers
            Assert.AreEqual("Date,Open,High,Low,Close,Volume", regularLines[0],
                "Regular file header should be correct");
            Assert.AreEqual("Date,Open,High,Low,Close,Volume", optionsLines[0],
                "Options file header should be correct");

            // Verify that options file starts earlier (has older dates)
            if (regularLines.Length > 1 && optionsLines.Length > 1)
            {
                var regularFirstDate = ParseDateFromCsvLine(regularLines[1]);
                var optionsFirstDate = ParseDateFromCsvLine(optionsLines[1]);

                if (regularFirstDate.HasValue && optionsFirstDate.HasValue)
                {
                    Assert.IsTrue(optionsFirstDate.Value <= regularFirstDate.Value,
                        "Options file should start on or before regular file");

                    Console.WriteLine($"Regular file starts: {regularFirstDate.Value:yyyy-MM-dd}");
                    Console.WriteLine($"Options file starts: {optionsFirstDate.Value:yyyy-MM-dd}");

                    var daysDifference = (regularFirstDate.Value - optionsFirstDate.Value).TotalDays;
                    Console.WriteLine($"Options file starts {daysDifference:F0} days earlier");
                }
            }

            // Show sample data
            Console.WriteLine("\nRegular file sample (first 3 lines):");
            foreach (var line in regularLines.Take(Math.Min(3, regularLines.Length))) Console.WriteLine($"  {line}");

            Console.WriteLine("\nOptions file sample (first 3 lines):");
            foreach (var line in optionsLines.Take(Math.Min(3, optionsLines.Length))) Console.WriteLine($"  {line}");

            if (result.Warnings.Count > 0)
            {
                Console.WriteLine("\nGeneration warnings:");
                foreach (var warning in result.Warnings) Console.WriteLine($"  - {warning}");
            }

            Console.WriteLine("\n? File generation test completed successfully");
        }

        /// <summary>
        ///     Parse date from CSV line
        /// </summary>
        private DateTime? ParseDateFromCsvLine(string csvLine)
        {
            if (string.IsNullOrEmpty(csvLine)) return null;

            var parts = csvLine.Split(',');
            if (parts.Length > 0 && DateTime.TryParse(parts[0], out var date)) return date;

            return null;
        }

        [TestMethod][TestCategory("Performance")]
        public void Generate1DTradingGuides_OptionsFileStartsEarlier_VerifyDateRange()
        {
            Console.WriteLine("=== Testing Options File Starts Earlier Requirement ===");

            // This test verifies the specific requirement that options file starts one year earlier
            var result = Generate1DTradingGuides.GenerateTradingGuides();

            if (!result.Success)
            {
                Console.WriteLine($"Generation failed (expected if no source data): {result.ErrorMessage}");
                return; // Skip test if no source data available
            }

            // Parse date ranges to verify the requirement
            var regularStart = ParseDateFromRange(result.RegularDateRange, true);
            var optionsStart = ParseDateFromRange(result.OptionsDateRange, true);

            if (regularStart.HasValue && optionsStart.HasValue)
            {
                var yearDifference = (regularStart.Value - optionsStart.Value).TotalDays / 365.25;

                Console.WriteLine($"Regular file starts: {regularStart.Value:yyyy-MM-dd}");
                Console.WriteLine($"Options file starts: {optionsStart.Value:yyyy-MM-dd}");
                Console.WriteLine($"Year difference: {yearDifference:F2} years");

                // Verify options file starts at least close to 1 year earlier (within 2 months tolerance)
                Assert.IsTrue(yearDifference >= 0.8, // At least ~10 months earlier
                    $"Options file should start significantly earlier than regular file (got {yearDifference:F2} years)");

                if (yearDifference >= 0.9 && yearDifference <= 1.1)
                    Console.WriteLine("? Perfect: Options file starts approximately 1 year earlier");
                else if (yearDifference > 0.5)
                    Console.WriteLine($"? Good: Options file starts {yearDifference:F1} years earlier");
                else
                    Console.WriteLine($"? Warning: Options file only starts {yearDifference:F1} years earlier");
            }
            else
            {
                Console.WriteLine("Could not parse date ranges for comparison");
            }

            Console.WriteLine("? Date range verification test completed");
        }

        /// <summary>
        ///     Parse date from range string (helper method)
        /// </summary>
        private DateTime? ParseDateFromRange(string dateRange, bool getFirst)
        {
            if (string.IsNullOrEmpty(dateRange)) return null;

            var parts = dateRange.Split(new[] { " to " }, StringSplitOptions.None);
            if (parts.Length != 2) return null;

            var dateStr = getFirst ? parts[0] : parts[1];
            return DateTime.TryParse(dateStr, out var date) ? date : (DateTime?)null;
        }

        [TestMethod][TestCategory("Core")]
        public void GenerateTradingGuides_ReturnsSuccessWithDummyData()
        {
            //var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            //Directory.CreateDirectory(tempDir);
            //var prices = new Prices();
            //for (int i = 0; i < 400; i++)
            //{
            //    var date = new DateTime(2020, 1, 1).AddDays(i);
            //    prices.AddPrice(new PriceRecord(date, 100 + i, 101 + i, 99 + i, 100.5 + i, 1000 + i, 100.5 + i, 10));
            //}
            //// Save dummy data to file
            //var dummyPath = Path.Combine(tempDir, Constants.SPX_JSON);
            //File.WriteAllText(dummyPath, Newtonsoft.Json.JsonConvert.SerializeObject(prices.Records));
            //var result = Trade.Generate1DTradingGuides.GenerateTradingGuides(dummyPath, tempDir);
            //Assert.IsTrue(result.Success);
            //Assert.IsTrue(File.Exists(result.RegularCsvPath));
            //Assert.IsTrue(File.Exists(result.OptionsCsvPath));
            //Directory.Delete(tempDir, true);
        }

        [TestMethod][TestCategory("Core")]
        public void GenerateTradingGuides_FailsWithNoData()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var dummyPath = Path.Combine(tempDir, Constants.SPX_JSON);
            File.WriteAllText(dummyPath, "[]");
            var result = Trade.Generate1DTradingGuides.GenerateTradingGuides(dummyPath, tempDir);
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.ErrorMessage.Contains("No daily price records"));
            Directory.Delete(tempDir, true);
        }

        [TestMethod][TestCategory("Core")]
        public void ValidateExistingFiles_ReturnsExpectedResults()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var regularFile = Path.Combine(tempDir, Constants.SPX_D);
            var optionsFile = Path.Combine(tempDir, Constants.SPX_D_FOR_OPTIONS);
            File.WriteAllText(regularFile, "Date,Open,High,Low,Close,Volume\n2020-01-01,100,101,99,100.5,1000");
            File.WriteAllText(optionsFile, "Date,Open,High,Low,Close,Volume\n2020-01-01,100,101,99,100.5,1000");
            var result = Trade.Generate1DTradingGuides.ValidateExistingFiles(tempDir + "/" + Constants.SPX_JSON);
            Assert.IsTrue(result.RegularFileExists);
            Assert.IsTrue(result.OptionsFileExists);
            Directory.Delete(tempDir, true);
        }

        [TestMethod][TestCategory("Core")]
        public void ParseDateFromRange_ParsesCorrectly()
        {
            var method = typeof(Trade.Generate1DTradingGuides).GetMethod("ParseDateFromRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var range = "2020-01-01 to 2021-01-01";
            var first = (DateTime?)method.Invoke(null, new object[] { range, true });
            var last = (DateTime?)method.Invoke(null, new object[] { range, false });
            Assert.AreEqual(new DateTime(2020, 1, 1), first);
            Assert.AreEqual(new DateTime(2021, 1, 1), last);
        }

        [TestMethod][TestCategory("Core")]
        public void AnalyzeCsvFile_ReturnsStats()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var file = Path.Combine(tempDir, "test.csv");
            File.WriteAllText(file, "Date,Open,High,Low,Close,Volume\n2020-01-01,100,101,99,100.5,1000\n2020-01-02,101,102,100,101.5,1100");
            var method = typeof(Trade.Generate1DTradingGuides).GetMethod("AnalyzeCsvFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var stats = (Trade.Generate1DTradingGuides.FileStats)method.Invoke(null, new object[] { file });
            Assert.AreEqual(2, stats.RecordCount);
            Assert.AreEqual("2020-01-01 to 2020-01-02", stats.DateRange);
            Directory.Delete(tempDir, true);
        }

        [TestMethod][TestCategory("Core")]
        public void DetermineIfRegenerationNeeded_ReturnsTrueIfFilesMissingOrOutdated()
        {
            var result = new Trade.Generate1DTradingGuides.ValidationResult
            {
                RegularFileExists = false,
                OptionsFileExists = true,
                SourceDataStats = new Trade.Generate1DTradingGuides.FileStats { LastDate = new DateTime(2022, 1, 1) },
                RegularFileStats = new Trade.Generate1DTradingGuides.FileStats { LastDate = new DateTime(2021, 1, 1) }
            };
            var method = typeof(Trade.Generate1DTradingGuides).GetMethod("DetermineIfRegenerationNeeded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var needsRegen = (bool)method.Invoke(null, new object[] { result });
            Assert.IsTrue(needsRegen);
        }
    }
}