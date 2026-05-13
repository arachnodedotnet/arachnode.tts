using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Trade.Prices2;

namespace Trade
{
    /// <summary>
    ///     Generates 1D trading guide CSV files using maximum available data from Prices.cs
    ///     Creates two files: Constants.SPX_D and Constants.SPX_D_FOR_OPTIONS (starts one year earlier)
    /// </summary>
    public static class Generate1DTradingGuides
    {
        #region Constants

        private const string REGULAR_CSV_FILENAME = Constants.SPX_D;
        private const string OPTIONS_CSV_FILENAME = Constants.SPX_D_FOR_OPTIONS;
        public const int OPTIONS_EXTRA_YEARS = 1; // Options file starts 1 year earlier

        // CSV Headers
        private const string CSV_HEADER = "Date,Open,High,Low,Close,Volume";

        #endregion

        #region Public Methods

        /// <summary>
        ///     Generate both CSV files using maximum available data from Prices.cs
        /// </summary>
        /// <param name="sourcePricesPath">Path to source price data (default: "Constants.SPX_JSON")</param>
        /// <param name="outputDirectory">Output directory for CSV files (default: current directory)</param>
        /// <returns>Generation result with statistics and file paths</returns>
        public static GenerationResult GenerateTradingGuides(string sourcePricesPath = Constants.SPX_JSON,
            string outputDirectory = null)
        {
            var result = new GenerationResult();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                ConsoleUtilities.WriteLine("=== Generate 1D Trading Guides ===");
                ConsoleUtilities.WriteLine($"Source data: {sourcePricesPath}");
                ConsoleUtilities.WriteLine($"Output directory: {outputDirectory ?? "Current directory"}");
                ConsoleUtilities.WriteLine();

                // Load source price data
                ConsoleUtilities.WriteLine("Loading source price data...");
                var prices = LoadSourcePriceData(sourcePricesPath, result);
                if (prices == null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to load source price data";
                    return result;
                }

                // Get daily price records
                ConsoleUtilities.WriteLine("Extracting daily price records...");
                var dailyRecords = ExtractDailyRecords(prices, result);
                if (dailyRecords == null || dailyRecords.Length == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "No daily price records available";
                    return result;
                }

                ConsoleUtilities.WriteLine($"Available daily records: {dailyRecords.Length}");
                ConsoleUtilities.WriteLine(
                    $"Date range: {dailyRecords.First().DateTime:yyyy-MM-dd} to {dailyRecords.Last().DateTime:yyyy-MM-dd}");
                ConsoleUtilities.WriteLine(
                    $"Time span: {(dailyRecords.Last().DateTime - dailyRecords.First().DateTime).TotalDays:F0} days");
                ConsoleUtilities.WriteLine();

                // Determine file date ranges
                var (regularStart, regularEnd, optionsStart, optionsEnd) = DetermineDateRanges(dailyRecords, result);

                ConsoleUtilities.WriteLine("File generation plan:");
                ConsoleUtilities.WriteLine(
                    $"Regular file ({REGULAR_CSV_FILENAME}): {regularStart:yyyy-MM-dd} to {regularEnd:yyyy-MM-dd}");
                ConsoleUtilities.WriteLine(
                    $"Options file ({OPTIONS_CSV_FILENAME}): {optionsStart:yyyy-MM-dd} to {optionsEnd:yyyy-MM-dd}");
                ConsoleUtilities.WriteLine();

                // Generate regular CSV file
                ConsoleUtilities.WriteLine($"Generating {REGULAR_CSV_FILENAME}...");
                var regularFilePath =
                    GenerateRegularCsvFile(dailyRecords, regularStart, regularEnd, outputDirectory, result);

                // Generate options CSV file  
                ConsoleUtilities.WriteLine($"Generating {OPTIONS_CSV_FILENAME}...");
                var optionsFilePath =
                    GenerateOptionsCsvFile(dailyRecords, optionsStart, optionsEnd, outputDirectory, result);

                // Set result properties
                result.RegularCsvPath = regularFilePath;
                result.OptionsCsvPath = optionsFilePath;
                result.RegularRecordCount = CountRecordsInDateRange(dailyRecords, regularStart, regularEnd);
                result.OptionsRecordCount = CountRecordsInDateRange(dailyRecords, optionsStart, optionsEnd);
                result.RegularDateRange = $"{regularStart:yyyy-MM-dd} to {regularEnd:yyyy-MM-dd}";
                result.OptionsDateRange = $"{optionsStart:yyyy-MM-dd} to {optionsEnd:yyyy-MM-dd}";
                result.Success = true;

                stopwatch.Stop();
                result.GenerationTimeMs = stopwatch.Elapsed.TotalMilliseconds;

                // Display summary
                DisplayGenerationSummary(result);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.GenerationTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                result.Success = false;
                result.ErrorMessage = $"Generation failed: {ex.Message}";

                ConsoleUtilities.WriteLine($"ERROR: {result.ErrorMessage}");
                if (ex.StackTrace != null) ConsoleUtilities.WriteLine($"Stack trace: {ex.StackTrace}");

                return result;
            }
        }

        /// <summary>
        ///     Validate existing CSV files against current data
        /// </summary>
        /// <param name="sourcePricesPath">Path to source price data</param>
        /// <returns>Validation result</returns>
        public static ValidationResult ValidateExistingFiles(string sourcePricesPath = Constants.SPX_JSON)
        {
            var result = new ValidationResult();

            try
            {
                ConsoleUtilities.WriteLine("=== Validate Existing CSV Files ===");

                // Check if files exist
                result.RegularFileExists = File.Exists(REGULAR_CSV_FILENAME);
                result.OptionsFileExists = File.Exists(OPTIONS_CSV_FILENAME);

                ConsoleUtilities.WriteLine($"Regular file exists: {result.RegularFileExists}");
                ConsoleUtilities.WriteLine($"Options file exists: {result.OptionsFileExists}");

                if (result.RegularFileExists)
                {
                    result.RegularFileStats = AnalyzeCsvFile(REGULAR_CSV_FILENAME);
                    ConsoleUtilities.WriteLine(
                        $"Regular file: {result.RegularFileStats.RecordCount} records, {result.RegularFileStats.DateRange}");
                }

                if (result.OptionsFileExists)
                {
                    result.OptionsFileStats = AnalyzeCsvFile(OPTIONS_CSV_FILENAME);
                    ConsoleUtilities.WriteLine(
                        $"Options file: {result.OptionsFileStats.RecordCount} records, {result.OptionsFileStats.DateRange}");
                }

                // Load source data for comparison
                var prices = new Prices(sourcePricesPath);
                var dailyRecords = prices.GetDailyPriceRecords();

                if (dailyRecords.Length > 0)
                {
                    result.SourceDataStats = new FileStats
                    {
                        RecordCount = dailyRecords.Length,
                        DateRange =
                            $"{dailyRecords.First().DateTime:yyyy-MM-dd} to {dailyRecords.Last().DateTime:yyyy-MM-dd}",
                        FirstDate = dailyRecords.First().DateTime,
                        LastDate = dailyRecords.Last().DateTime
                    };

                    ConsoleUtilities.WriteLine(
                        $"Source data: {result.SourceDataStats.RecordCount} records, {result.SourceDataStats.DateRange}");
                }

                // Determine if regeneration is needed
                result.RequiresRegeneration = DetermineIfRegenerationNeeded(result);

                ConsoleUtilities.WriteLine($"Regeneration recommended: {result.RequiresRegeneration}");

                if (result.RequiresRegeneration)
                {
                    ConsoleUtilities.WriteLine("Reasons for regeneration:");
                    if (!result.RegularFileExists) ConsoleUtilities.WriteLine("  - Regular file missing");
                    if (!result.OptionsFileExists) ConsoleUtilities.WriteLine("  - Options file missing");
                    if (result.SourceDataStats != null && result.RegularFileStats != null)
                        if (result.SourceDataStats.LastDate > result.RegularFileStats.LastDate)
                            ConsoleUtilities.WriteLine("  - Source data has newer records");
                }

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                ConsoleUtilities.WriteLine($"ERROR: Validation failed - {ex.Message}");
                return result;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        ///     Load source price data from Prices.cs
        /// </summary>
        /// <param name="sourcePath">Path to the price data source file</param>
        /// <param name="result">Generation result to store warnings and messages</param>
        /// <returns>Loaded Prices object or null if loading failed</returns>
        private static Prices LoadSourcePriceData(string sourcePath, GenerationResult result)
        {
            try
            {
                if (string.IsNullOrEmpty(sourcePath))
                {
                    result.Warnings.Add("No source path specified, using default Constants.SPX_JSON");
                    sourcePath = Constants.SPX_JSON;
                }

                if (!File.Exists(sourcePath))
                {
                    result.Warnings.Add(
                        $"Source file {sourcePath} not found, attempting to create Prices without file");
                    return new Prices(); // Create empty Prices object
                }

                var prices = new Prices(sourcePath);

                // Validate that we loaded data successfully
                if (prices.LastLoadResult != null)
                {
                    if (!prices.LastLoadResult.Success)
                        result.Warnings.Add($"Source data loading had issues: {prices.LastLoadResult}");
                    else
                        ConsoleUtilities.WriteLine(
                            $"Source data loaded: {prices.LastLoadResult.ValidRecordsLoaded} records in {prices.LastLoadResult.LoadTimeMilliseconds:F0}ms");
                }

                return prices;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Failed to load source data: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     Extract daily records from Prices object
        /// </summary>
        /// <param name="prices">Prices object containing price data</param>
        /// <param name="result">Generation result to store warnings and messages</param>
        /// <returns>Array of daily price records</returns>
        private static PriceRecord[] ExtractDailyRecords(Prices prices, GenerationResult result)
        {
            try
            {
                // Get daily timeframe data
                var dailyData = prices.GetTimeFrame(TimeFrame.D1);

                if (dailyData.Count == 0)
                {
                    result.Warnings.Add("No daily data available, attempting to get raw records");

                    // Fallback: use base records if they exist
                    if (prices.Records != null && prices.Records.Count > 0)
                    {
                        // Group by date and create daily aggregates
                        var dailyGroups = prices.Records
                            .GroupBy(r => r.DateTime.Date)
                            .OrderBy(g => g.Key)
                            .ToList();

                        var dailyRecordsList = new List<PriceRecord>();

                        foreach (var group in dailyGroups)
                        {
                            var recordsInGroup = group.OrderBy(r => r.DateTime).ToList();
                            var date = group.Key;

                            var open = recordsInGroup.First().Open;
                            var high = recordsInGroup.Max(r => r.High);
                            var low = recordsInGroup.Min(r => r.Low);
                            var close = recordsInGroup.Last().Close;
                            var volume = recordsInGroup.Sum(r => r.Volume);

                            dailyRecordsList.Add(new PriceRecord(date, TimeFrame.D1, open, high, low, close, volume: volume, wap: close,
                                count: recordsInGroup.Count));
                        }

                        result.Warnings.Add(
                            $"Created {dailyRecordsList.Count} daily records from {prices.Records.Count} base records");
                        return dailyRecordsList.ToArray();
                    }

                    return new PriceRecord[0];
                }

                // Convert aggregated data to array
                var priceRecords = new PriceRecord[dailyData.Count];
                for (var i = 0; i < dailyData.Count; i++) priceRecords[i] = dailyData[i];

                return priceRecords;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Failed to extract daily records: {ex.Message}");
                return new PriceRecord[0];
            }
        }

        /// <summary>
        ///     Determine date ranges for both files
        /// </summary>
        /// <param name="dailyRecords">Array of daily price records</param>
        /// <param name="result">Generation result to store warnings and messages</param>
        /// <returns>Tuple containing start and end dates for regular and options files</returns>
        private static (DateTime regularStart, DateTime regularEnd, DateTime optionsStart, DateTime optionsEnd)
            DetermineDateRanges(PriceRecord[] dailyRecords, GenerationResult result)
        {
            var firstDate = dailyRecords.First().DateTime.Date;
            var lastDate = dailyRecords.Last().DateTime.Date;

            // For maximum data utilization:
            // Options file: use all available data (starts earliest)
            var optionsStart = firstDate;
            var optionsEnd = lastDate;

            // Regular file: starts 1 year later than options file
            var regularStart = optionsStart.AddYears(OPTIONS_EXTRA_YEARS);
            var regularEnd = lastDate;

            // Ensure regular start doesn't exceed available data
            if (regularStart > lastDate)
            {
                result.Warnings.Add(
                    $"Regular file start date ({regularStart:yyyy-MM-dd}) exceeds available data, using available range");
                regularStart = firstDate;
            }

            // Validate that we have enough data for the regular file
            var regularDaysAvailable = (regularEnd - regularStart).TotalDays;
            if (regularDaysAvailable < 30)
                result.Warnings.Add($"Limited data for regular file: only {regularDaysAvailable:F0} days available");

            return (regularStart, regularEnd, optionsStart, optionsEnd);
        }

        /// <summary>
        ///     Generate the regular CSV file (Constants.SPX_D)
        /// </summary>
        /// <param name="dailyRecords">Array of daily price records</param>
        /// <param name="startDate">Start date for records inclusion</param>
        /// <param name="endDate">End date for records inclusion</param>
        /// <param name="outputDirectory">Output directory for the file</param>
        /// <param name="result">Generation result to store information</param>
        /// <returns>Full path to the generated file</returns>
        private static string GenerateRegularCsvFile(PriceRecord[] dailyRecords, DateTime startDate, DateTime endDate,
            string outputDirectory, GenerationResult result)
        {
            var fileName = REGULAR_CSV_FILENAME;
            var filePath = string.IsNullOrEmpty(outputDirectory) ? fileName : Path.Combine(outputDirectory, fileName);

            var recordsInRange = dailyRecords
                .Where(r => r.DateTime.Date >= startDate && r.DateTime.Date <= endDate && !r.Manufactured)
                .OrderBy(r => r.DateTime)
                .ToList();

            WriteCsvFile(filePath, recordsInRange, result);

            ConsoleUtilities.WriteLine(
                $"Generated {fileName}: {recordsInRange.Count} records from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            return filePath;
        }

        /// <summary>
        ///     Generate the options CSV file (Constants.SPX_D_FOR_OPTIONS) - starts one year earlier
        /// </summary>
        /// <param name="dailyRecords">Array of daily price records</param>
        /// <param name="startDate">Start date for records inclusion</param>
        /// <param name="endDate">End date for records inclusion</param>
        /// <param name="outputDirectory">Output directory for the file</param>
        /// <param name="result">Generation result to store information</param>
        /// <returns>Full path to the generated file</returns>
        private static string GenerateOptionsCsvFile(PriceRecord[] dailyRecords, DateTime startDate, DateTime endDate,
            string outputDirectory, GenerationResult result)
        {
            var fileName = OPTIONS_CSV_FILENAME;
            var filePath = string.IsNullOrEmpty(outputDirectory) ? fileName : Path.Combine(outputDirectory, fileName);

            var recordsInRange = dailyRecords
                .Where(r => r.DateTime.Date >= startDate && r.DateTime.Date <= endDate && !r.Manufactured)
                .OrderBy(r => r.DateTime)
                .ToList();

            WriteCsvFile(filePath, recordsInRange, result);

            ConsoleUtilities.WriteLine(
                $"Generated {fileName}: {recordsInRange.Count} records from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            return filePath;
        }

        /// <summary>
        ///     Write CSV file with proper formatting
        /// </summary>
        /// <param name="filePath">Full path where to write the CSV file</param>
        /// <param name="records">List of price records to write</param>
        /// <param name="result">Generation result to store information</param>
        private static void WriteCsvFile(string filePath, List<PriceRecord> records, GenerationResult result)
        {
            try
            {
                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    // Write header
                    writer.WriteLine(CSV_HEADER);

                    // Write data rows
                    foreach (var record in records)
                    {
                        var line = $"{record.DateTime:yyyy-MM-dd}," +
                                   $"{record.Open.ToString("F2", CultureInfo.InvariantCulture)}," +
                                   $"{record.High.ToString("F2", CultureInfo.InvariantCulture)}," +
                                   $"{record.Low.ToString("F2", CultureInfo.InvariantCulture)}," +
                                   $"{record.Close.ToString("F2", CultureInfo.InvariantCulture)}," +
                                   $"{record.Volume:F0}";

                        writer.WriteLine(line);
                    }
                }

                result.FilesGenerated.Add(filePath);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"Failed to write {filePath}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        ///     Count records in date range
        /// </summary>
        /// <param name="records">Array of price records to count</param>
        /// <param name="startDate">Start date for counting (inclusive)</param>
        /// <param name="endDate">End date for counting (inclusive)</param>
        /// <returns>Number of records in the specified date range</returns>
        private static int CountRecordsInDateRange(PriceRecord[] records, DateTime startDate, DateTime endDate)
        {
            return records.Count(r => r.DateTime.Date >= startDate && r.DateTime.Date <= endDate);
        }

        /// <summary>
        ///     Display generation summary
        /// </summary>
        /// <param name="result">Generation result containing summary information</param>
        private static void DisplayGenerationSummary(GenerationResult result)
        {
            ConsoleUtilities.WriteLine();
            ConsoleUtilities.WriteLine("=== Generation Summary ===");
            ConsoleUtilities.WriteLine($"Success: {result.Success}");
            ConsoleUtilities.WriteLine($"Generation time: {result.GenerationTimeMs:F0}ms");
            ConsoleUtilities.WriteLine();

            ConsoleUtilities.WriteLine("Files generated:");
            ConsoleUtilities.WriteLine($"  Regular CSV: {result.RegularCsvPath}");
            ConsoleUtilities.WriteLine($"    Records: {result.RegularRecordCount}");
            ConsoleUtilities.WriteLine($"    Date range: {result.RegularDateRange}");
            ConsoleUtilities.WriteLine();
            ConsoleUtilities.WriteLine($"  Options CSV: {result.OptionsCsvPath}");
            ConsoleUtilities.WriteLine($"    Records: {result.OptionsRecordCount}");
            ConsoleUtilities.WriteLine($"    Date range: {result.OptionsDateRange}");
            ConsoleUtilities.WriteLine();

            if (result.Warnings.Any())
            {
                ConsoleUtilities.WriteLine("Warnings:");
                foreach (var warning in result.Warnings) ConsoleUtilities.WriteLine($"  - {warning}");
                ConsoleUtilities.WriteLine();
            }

            // Validate the options file starts earlier
            if (result.Success)
            {
                var optionsFirstDate = ParseDateFromRange(result.OptionsDateRange, true);
                var regularFirstDate = ParseDateFromRange(result.RegularDateRange, true);

                if (optionsFirstDate.HasValue && regularFirstDate.HasValue)
                {
                    var yearDifference = (regularFirstDate.Value - optionsFirstDate.Value).TotalDays / 365.25;
                    ConsoleUtilities.WriteLine(
                        $"✓ Options file starts {yearDifference:F1} years earlier than regular file");

                    if (Math.Abs(yearDifference - OPTIONS_EXTRA_YEARS) > 0.1)
                        ConsoleUtilities.WriteLine(
                            $"  Warning: Expected {OPTIONS_EXTRA_YEARS} year difference, got {yearDifference:F1} years");
                }
            }

            ConsoleUtilities.WriteLine("Generation complete!");
        }

        /// <summary>
        ///     Parse date from range string
        /// </summary>
        /// <param name="dateRange">Date range string in format "yyyy-MM-dd to yyyy-MM-dd"</param>
        /// <param name="getFirst">True to get first date, false to get last date</param>
        /// <returns>Parsed DateTime or null if parsing failed</returns>
        private static DateTime? ParseDateFromRange(string dateRange, bool getFirst)
        {
            if (string.IsNullOrEmpty(dateRange)) return null;

            var parts = dateRange.Split(new[] { " to " }, StringSplitOptions.None);
            if (parts.Length != 2) return null;

            var dateStr = getFirst ? parts[0] : parts[1];
            return DateTime.TryParse(dateStr, out var date) ? date : (DateTime?)null;
        }

        /// <summary>
        ///     Analyze existing CSV file
        /// </summary>
        /// <param name="filePath">Path to the CSV file to analyze</param>
        /// <returns>FileStats object containing analysis results</returns>
        private static FileStats AnalyzeCsvFile(string filePath)
        {
            var stats = new FileStats();

            try
            {
                var lines = File.ReadAllLines(filePath);
                stats.RecordCount = Math.Max(0, lines.Length - 1); // Exclude header

                if (lines.Length > 1)
                {
                    // Get first data line
                    var firstDataLine = lines[1].Split(',');
                    if (firstDataLine.Length > 0 && DateTime.TryParse(firstDataLine[0], out var firstDate))
                        stats.FirstDate = firstDate;

                    // Get last data line
                    var lastDataLine = lines[lines.Length - 1].Split(',');
                    if (lastDataLine.Length > 0 && DateTime.TryParse(lastDataLine[0], out var lastDate))
                        stats.LastDate = lastDate;

                    if (stats.FirstDate.HasValue && stats.LastDate.HasValue)
                        stats.DateRange = $"{stats.FirstDate.Value:yyyy-MM-dd} to {stats.LastDate.Value:yyyy-MM-dd}";
                }
            }
            catch (Exception ex)
            {
                stats.ErrorMessage = ex.Message;
            }

            return stats;
        }

        /// <summary>
        ///     Determine if regeneration is needed
        /// </summary>
        /// <param name="result">Validation result containing file information</param>
        /// <returns>True if regeneration is recommended, false otherwise</returns>
        private static bool DetermineIfRegenerationNeeded(ValidationResult result)
        {
            // Need regeneration if either file is missing
            if (!result.RegularFileExists || !result.OptionsFileExists)
                return true;

            // Need regeneration if source data has newer records
            if (result.SourceDataStats != null && result.RegularFileStats != null)
                if (result.SourceDataStats.LastDate.HasValue && result.RegularFileStats.LastDate.HasValue)
                    if (result.SourceDataStats.LastDate.Value > result.RegularFileStats.LastDate.Value.AddDays(7))
                        return true; // Source has significantly newer data

            return false;
        }

        #endregion

        #region Data Structures

        /// <summary>
        ///     Result of CSV generation operation
        /// </summary>
        public class GenerationResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }
            public List<string> Warnings { get; set; } = new List<string>();
            public List<string> FilesGenerated { get; set; } = new List<string>();

            public string RegularCsvPath { get; set; }
            public string OptionsCsvPath { get; set; }
            public int RegularRecordCount { get; set; }
            public int OptionsRecordCount { get; set; }
            public string RegularDateRange { get; set; }
            public string OptionsDateRange { get; set; }
            public double GenerationTimeMs { get; set; }

            public override string ToString()
            {
                var status = Success ? "SUCCESS" : "FAILED";
                return
                    $"GenerationResult: {status}, {FilesGenerated.Count} files, {Warnings.Count} warnings, {GenerationTimeMs:F0}ms";
            }
        }

        /// <summary>
        ///     Result of file validation operation
        /// </summary>
        public class ValidationResult
        {
            public bool RegularFileExists { get; set; }
            public bool OptionsFileExists { get; set; }
            public FileStats RegularFileStats { get; set; }
            public FileStats OptionsFileStats { get; set; }
            public FileStats SourceDataStats { get; set; }
            public bool RequiresRegeneration { get; set; }
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        ///     Statistics about a CSV file
        /// </summary>
        public class FileStats
        {
            public int RecordCount { get; set; }
            public string DateRange { get; set; }
            public DateTime? FirstDate { get; set; }
            public DateTime? LastDate { get; set; }
            public string ErrorMessage { get; set; }
        }

        #endregion
    }
}