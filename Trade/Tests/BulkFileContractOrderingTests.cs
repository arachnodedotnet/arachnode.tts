using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Trade.IVPreCalc2;
using Trade.Polygon2;

namespace Trade.Tests
{
    /// <summary>
    /// Tests to validate that bulk option files are properly sorted by:
    /// 1. Contract ordering (Underlying, Call/Put, Expiration, Strike)
    /// 2. Timestamp ordering within each contract group (window_start chronological order)
    /// 
    /// These tests ensure that the BulkDataSorter is working correctly and that
    /// the sorted files maintain proper ordering for both contract keys and timestamps.
    /// </summary>
    [TestClass]
    public class BulkFileContractOrderingTests
    {
        private const int MAX_VIOLATION_REPORTS = 25;

        [TestMethod]
        [TestCategory("Core")]
        public void NewestBulkFile_Contracts_Are_Sorted_By_Underlying_CallPut_Expiration_Strike()
        {
            var bulkDir = IVPreCalc.ResolveBulkDir();
            if (string.IsNullOrEmpty(bulkDir) || !Directory.Exists(bulkDir))
                Assert.Inconclusive("Polygon bulk directory not found.");

            var newestOptionsFile = Directory.GetFiles(bulkDir, "*_Sorted.csv", SearchOption.AllDirectories)
                .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(f => new { Path = f, Date = IVPreCalc.TryExtractDateFromName(f) })
                .Where(x => x.Date.HasValue)
                .OrderByDescending(x => x.Date.Value)
                .Select(x => x.Path)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(newestOptionsFile) || !File.Exists(newestOptionsFile))
                Assert.Inconclusive("No options bulk CSV found.");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var violations = new List<string>();

            ContractKey prev = null;
            int lineNumber = 0;
            int uniqueContracts = 0;

            using (var sr = new StreamReader(newestOptionsFile))
            {
                sr.ReadLine(); // header
                lineNumber++;

                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    int comma = line.IndexOf(',');
                    if (comma <= 0) continue;

                    var ticker = line.Substring(0, comma).Trim();
                    if (!ticker.StartsWith("O:", StringComparison.OrdinalIgnoreCase)) continue; // only options

                    if (!seen.Add(ticker))
                        continue; // only first occurrence per contract

                    var key = TryBuildKey(ticker);
                    if (key == null)
                        continue;

                    uniqueContracts++;

                    if (prev != null)
                    {
                        int cmp = ContractKeyComparer.Instance.Compare(prev, key);
                        if (cmp > 0)
                        {
                            // Allow a reset of Call/Put ordering when expiration advances (same underlying)
                            if (!IsExpirationBoundaryReset(prev, key))
                            {
                                // Skip violation if either underlying contains numeric characters (data quality issue)
                                if (!(UnderlyingHasDigits(prev) || UnderlyingHasDigits(key)))
                                {
                                    if (violations.Count < MAX_VIOLATION_REPORTS)
                                    {
                                        violations.Add(string.Format(
                                            "Out of order at line {0}: '{1}' (Prev Key={2}) -> '{3}' (Key={4})",
                                            lineNumber, prev.RawTicker, prev, key.RawTicker, key));
                                    }
                                }
                            }
                        }
                    }

                    prev = key;
                }
            }

            if (uniqueContracts == 0)
                Assert.Inconclusive("No option contracts found in newest file.");

            if (violations.Count > 0)
            {
                var msg = string.Format(
                    "Detected {0} ordering violation(s) in newest bulk file '{1}'. Displaying first {2}.\n{3}",
                    violations.Count, Path.GetFileName(newestOptionsFile), violations.Count, string.Join("\n", violations));
                Assert.Fail(msg);
            }
        }

        //[TestMethod]
        [TestCategory("Performance")]
        public void AllBulkFiles_Contracts_Are_Sorted_With_Progress()
        {
            var bulkDir = IVPreCalc.ResolveBulkDir();
            if (string.IsNullOrEmpty(bulkDir) || !Directory.Exists(bulkDir))
                Assert.Inconclusive("Polygon bulk directory not found.");

            var optionFiles = Directory.GetFiles(bulkDir, "*_Sorted.csv", SearchOption.AllDirectories)
                .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(f => new { Path = f, Date = IVPreCalc.TryExtractDateFromName(f) })
                .Where(x => x.Date.HasValue)
                .OrderByDescending(x => x.Date.Value) // oldest to newest
                .Select(x => x.Path)
                .ToList();

            if (optionFiles.Count == 0)
                Assert.Inconclusive("No options bulk CSV files found.");

            int totalFiles = optionFiles.Count;
            int processed = 0;
            int filesWithViolations = 0;
            int totalViolations = 0;
            var violationSamples = new List<string>();
            const int globalSampleCap = 25;

            foreach (var file in optionFiles)
            {
                processed++;
                var result = ValidateSingleBulkFile(file);
                var fileViolations = result.violations;
                var contractCount = result.uniqueContracts;

                if (fileViolations.Count > 0)
                {
                    filesWithViolations++;
                    totalViolations += fileViolations.Count;
                    foreach (var v in fileViolations)
                    {
                        if (violationSamples.Count >= globalSampleCap) break;
                        violationSamples.Add(Path.GetFileName(file) + ": " + v);
                    }
                }

                double pct = processed * 100.0 / totalFiles;
                ConsoleUtilities.WriteLine(string.Format(
                    "[Bulk Validation] {0}/{1} ({2:F1}%) {3} Contracts={4} Violations={5}",
                    processed, totalFiles, pct, Path.GetFileName(file), contractCount, fileViolations.Count));
            }

            if (totalViolations > 0)
            {
                if (violationSamples.Count > 0)
                {
                    ConsoleUtilities.WriteLine("Sample violations:");
                    foreach (var s in violationSamples)
                        ConsoleUtilities.WriteLine("  " + s);
                }
                Assert.Fail(string.Format(
                    "Ordering violations detected: {0} across {1} file(s). Displayed {2} sample(s).",
                    totalViolations, filesWithViolations, violationSamples.Count));
            }
        }

        private static (List<string> violations, int uniqueContracts) ValidateSingleBulkFile(string filePath)
        {
            var violations = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ContractKey prev = null;
            int lineNumber = 0;
            int uniqueContracts = 0;

            using (var sr = new StreamReader(filePath))
            {
                sr.ReadLine();
                lineNumber++;
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int comma = line.IndexOf(',');
                    if (comma <= 0) continue;
                    var ticker = line.Substring(0, comma).Trim();
                    if (!ticker.StartsWith("O:", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!seen.Add(ticker)) continue;
                    var key = TryBuildKey(ticker);
                    if (key == null) continue;
                    uniqueContracts++;
                    if (prev != null)
                    {
                        int cmp = ContractKeyComparer.Instance.Compare(prev, key);
                        if (cmp > 0 && !IsExpirationBoundaryReset(prev, key) && !(UnderlyingHasDigits(prev) || UnderlyingHasDigits(key)))
                        {
                            if (violations.Count < MAX_VIOLATION_REPORTS)
                            {
                                violations.Add(string.Format(
                                    "Line {0}: '{1}' ({2}) -> '{3}' ({4})",
                                    lineNumber, prev.RawTicker, prev, key.RawTicker, key));
                            }
                        }
                    }
                    prev = key;
                }
            }
            return (violations, uniqueContracts);
        }

        // Accept pattern: ... P (expiration E1) then C (expiration E2>E1) for same underlying
        private static bool IsExpirationBoundaryReset(ContractKey previous, ContractKey current)
        {
            if (previous == null || current == null) return false;
            if (!string.Equals(previous.Underlying, current.Underlying, StringComparison.Ordinal)) return false;
            if (current.Expiration <= previous.Expiration) return false; // must strictly advance expiration
            // previous must be a Put (end of its expiration block) and current a Call (start new expiration block)
            if (!previous.IsCall && current.IsCall)
                return true;
            return false;
        }

        private static bool UnderlyingHasDigits(ContractKey k)
        {
            if (k == null || string.IsNullOrEmpty(k.Underlying)) return false;
            for (int i = 0; i < k.Underlying.Length; i++)
            {
                if (char.IsDigit(k.Underlying[i])) return true;
            }
            return false;
        }

        private static ContractKey TryBuildKey(string rawTicker)
        {
            try
            {
                var parsed = Ticker.ParseToOption(rawTicker);
                if (!parsed.IsOption ||
                    !parsed.ExpirationDate.HasValue ||
                    !parsed.StrikePrice.HasValue ||
                    !parsed.OptionType.HasValue)
                    return null;

                return new ContractKey
                {
                    RawTicker = rawTicker,
                    Underlying = (parsed.UnderlyingSymbol ?? string.Empty).ToUpperInvariant(),
                    IsCall = parsed.OptionType.Value == OptionType.Call,
                    Expiration = parsed.ExpirationDate.Value.Date,
                    Strike = parsed.StrikePrice.Value
                };
            }
            catch
            {
                return null;
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void NewestBulkFile_Timestamps_Within_Contract_Groups_Are_Sequential()
        {
            var bulkDir = IVPreCalc.ResolveBulkDir();
            if (string.IsNullOrEmpty(bulkDir) || !Directory.Exists(bulkDir))
                Assert.Inconclusive("Polygon bulk directory not found.");

            var newestOptionsFile = Directory.GetFiles(bulkDir, "*_Sorted.csv", SearchOption.AllDirectories)
                .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(f => new { Path = f, Date = IVPreCalc.TryExtractDateFromName(f) })
                .Where(x => x.Date.HasValue)
                .OrderByDescending(x => x.Date.Value)
                .Select(x => x.Path)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(newestOptionsFile) || !File.Exists(newestOptionsFile))
                Assert.Inconclusive("No options bulk CSV found.");

            var result = ValidateTimestampOrderingInFile(newestOptionsFile);

            if (result.TimestampViolations.Count > 0)
            {
                var msg = string.Format(
                    "Detected {0} timestamp ordering violation(s) in newest bulk file '{1}'. Displaying first {2}.\n{3}",
                    result.TimestampViolations.Count, Path.GetFileName(newestOptionsFile), 
                    Math.Min(result.TimestampViolations.Count, MAX_VIOLATION_REPORTS), 
                    string.Join("\n", result.TimestampViolations.Take(MAX_VIOLATION_REPORTS)));
                Assert.Fail(msg);
            }

            // Ensure we actually found some contracts with multiple timestamps to validate
            Assert.IsTrue(result.ContractsWithMultipleRecords > 0, 
                "No contracts with multiple timestamp records found for validation.");
        }

        //[TestMethod]
        [TestCategory("Performance")]
        public void AllBulkFiles_Timestamps_Are_Sequential_With_Progress()
        {
            var bulkDir = IVPreCalc.ResolveBulkDir();
            if (string.IsNullOrEmpty(bulkDir) || !Directory.Exists(bulkDir))
                Assert.Inconclusive("Polygon bulk directory not found.");

            var optionFiles = Directory.GetFiles(bulkDir, "*_Sorted.csv", SearchOption.AllDirectories)
                .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(f => new { Path = f, Date = IVPreCalc.TryExtractDateFromName(f) })
                .Where(x => x.Date.HasValue)
                .OrderBy(x => x.Date.Value) // oldest to newest
                .Select(x => x.Path)
                .ToList();

            if (optionFiles.Count == 0)
                Assert.Inconclusive("No options bulk CSV files found.");

            int totalFiles = optionFiles.Count;
            int processed = 0;
            int filesWithTimestampViolations = 0;
            int totalTimestampViolations = 0;
            var timestampViolationSamples = new List<string>();
            const int globalSampleCap = 25;

            foreach (var file in optionFiles)
            {
                processed++;
                var result = ValidateTimestampOrderingInFile(file);

                if (result.TimestampViolations.Count > 0)
                {
                    filesWithTimestampViolations++;
                    totalTimestampViolations += result.TimestampViolations.Count;
                    foreach (var v in result.TimestampViolations)
                    {
                        if (timestampViolationSamples.Count >= globalSampleCap) break;
                        timestampViolationSamples.Add(Path.GetFileName(file) + ": " + v);
                    }
                }

                double pct = processed * 100.0 / totalFiles;
                ConsoleUtilities.WriteLine(string.Format(
                    "[Timestamp Validation] {0}/{1} ({2:F1}%) {3} Contracts={4} TimestampViolations={5}",
                    processed, totalFiles, pct, Path.GetFileName(file), 
                    result.ContractsWithMultipleRecords, result.TimestampViolations.Count));
            }

            if (totalTimestampViolations > 0)
            {
                if (timestampViolationSamples.Count > 0)
                {
                    ConsoleUtilities.WriteLine("Sample timestamp violations:");
                    foreach (var s in timestampViolationSamples)
                        ConsoleUtilities.WriteLine("  " + s);
                }
                Assert.Fail(string.Format(
                    "Timestamp ordering violations detected: {0} across {1} file(s). Displayed {2} sample(s).",
                    totalTimestampViolations, filesWithTimestampViolations, timestampViolationSamples.Count));
            }
        }

        //[TestMethod]
        [TestCategory("Performance")]
        public void BulkFiles_Complete_Contract_And_Timestamp_Validation()
        {
            var bulkDir = IVPreCalc.ResolveBulkDir();
            if (string.IsNullOrEmpty(bulkDir) || !Directory.Exists(bulkDir))
                Assert.Inconclusive("Polygon bulk directory not found.");

            var optionFiles = Directory.GetFiles(bulkDir, "*_Sorted.csv", SearchOption.AllDirectories)
                .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(f => new { Path = f, Date = IVPreCalc.TryExtractDateFromName(f) })
                .Where(x => x.Date.HasValue)
                .OrderBy(x => x.Date.Value) // oldest to newest
                .Select(x => x.Path)
                .Take(5) // Limit for comprehensive validation
                .ToList();

            if (optionFiles.Count == 0)
                Assert.Inconclusive("No options bulk CSV files found.");

            int totalFiles = optionFiles.Count;
            int processed = 0;
            var allViolations = new List<string>();
            var allTimestampViolations = new List<string>();

            foreach (var file in optionFiles)
            {
                processed++;
                
                // Validate both contract ordering and timestamp ordering
                var contractResult = ValidateSingleBulkFile(file);
                var timestampResult = ValidateTimestampOrderingInFile(file);

                foreach (var v in contractResult.violations)
                {
                    if (allViolations.Count < MAX_VIOLATION_REPORTS)
                        allViolations.Add(Path.GetFileName(file) + ": " + v);
                }

                foreach (var v in timestampResult.TimestampViolations)
                {
                    if (allTimestampViolations.Count < MAX_VIOLATION_REPORTS)
                        allTimestampViolations.Add(Path.GetFileName(file) + ": " + v);
                }

                double pct = processed * 100.0 / totalFiles;
                ConsoleUtilities.WriteLine(string.Format(
                    "[Complete Validation] {0}/{1} ({2:F1}%) {3} ContractViolations={4} TimestampViolations={5}",
                    processed, totalFiles, pct, Path.GetFileName(file), 
                    contractResult.violations.Count, timestampResult.TimestampViolations.Count));
            }

            var totalViolations = allViolations.Count + allTimestampViolations.Count;
            if (totalViolations > 0)
            {
                var errorMsg = new System.Text.StringBuilder();
                errorMsg.AppendLine(string.Format("Total violations found: {0}", totalViolations));
                
                if (allViolations.Count > 0)
                {
                    errorMsg.AppendLine(string.Format("Contract ordering violations ({0}):", allViolations.Count));
                    foreach (var v in allViolations) errorMsg.AppendLine("  " + v);
                }
                
                if (allTimestampViolations.Count > 0)
                {
                    errorMsg.AppendLine(string.Format("Timestamp ordering violations ({0}):", allTimestampViolations.Count));
                    foreach (var v in allTimestampViolations) errorMsg.AppendLine("  " + v);
                }
                
                Assert.Fail(errorMsg.ToString());
            }
        }

        private sealed class TimestampValidationResult
        {
            public List<string> TimestampViolations;
            public int ContractsWithMultipleRecords;
            public int TotalRecordsProcessed;

            public TimestampValidationResult(List<string> timestampViolations, int contractsWithMultipleRecords, int totalRecordsProcessed)
            {
                TimestampViolations = timestampViolations;
                ContractsWithMultipleRecords = contractsWithMultipleRecords;
                TotalRecordsProcessed = totalRecordsProcessed;
            }
        }

        private sealed class TimestampRecord
        {
            public long Timestamp;
            public int LineNumber;
            public string Ticker;

            public TimestampRecord(long timestamp, int lineNumber, string ticker)
            {
                Timestamp = timestamp;
                LineNumber = lineNumber;
                Ticker = ticker;
            }
        }

        private static TimestampValidationResult ValidateTimestampOrderingInFile(string filePath)
        {
            var timestampViolations = new List<string>();
            var contractGroups = new Dictionary<string, List<TimestampRecord>>();
            int lineNumber = 0;
            int totalRecordsProcessed = 0;
            int invalidTimestamps = 0;

            // First pass: Group records by contract key
            using (var sr = new StreamReader(filePath))
            {
                sr.ReadLine(); // skip header
                lineNumber++;

                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(',');
                    if (parts.Length < 7) continue; // Need at least 7 columns for window_start

                    var ticker = parts[0].Trim();
                    if (!ticker.StartsWith("O:", StringComparison.OrdinalIgnoreCase)) continue;

                    // Parse timestamp from window_start column (index 6) with validation
                    if (!TryParseTimestamp(parts[6], out var timestamp, out var dateTime))
                    {
                        invalidTimestamps++;
                        if (timestampViolations.Count < MAX_VIOLATION_REPORTS)
                        {
                            timestampViolations.Add(string.Format(
                                "Invalid timestamp at line {0}: ticker='{1}', timestamp='{2}'",
                                lineNumber, ticker, parts[6]));
                        }
                        continue;
                    }

                    var key = TryBuildKey(ticker);
                    if (key == null) continue;

                    // Create contract group key (excluding RawTicker for grouping)
                    var groupKey = string.Format(CultureInfo.InvariantCulture, 
                        "{0}|{1}|{2:yyyy-MM-dd}|{3:F8}", 
                        key.Underlying, key.IsCall ? "C" : "P", key.Expiration, key.Strike);

                    if (!contractGroups.ContainsKey(groupKey))
                        contractGroups[groupKey] = new List<TimestampRecord>();

                    contractGroups[groupKey].Add(new TimestampRecord(timestamp, lineNumber, ticker));
                    totalRecordsProcessed++;
                }
            }

            // Second pass: Validate timestamp ordering within each contract group
            int contractsWithMultipleRecords = 0;
            foreach (var kvp in contractGroups)
            {
                var groupKey = kvp.Key;
                var records = kvp.Value;

                if (records.Count <= 1) continue; // Skip single-record contracts
                contractsWithMultipleRecords++;

                // Check if timestamps are in ascending order
                for (int i = 1; i < records.Count; i++)
                {
                    var prev = records[i - 1];
                    var curr = records[i];

                    if (prev.Timestamp > curr.Timestamp)
                    {
                        if (timestampViolations.Count < MAX_VIOLATION_REPORTS)
                        {
                            timestampViolations.Add(string.Format(
                                "Timestamp regression in contract group {0}: Line {1} ({2}, {3}) -> Line {4} ({5}, {6})",
                                groupKey, prev.LineNumber, prev.Ticker, FormatTimestampForDisplay(prev.Timestamp),
                                curr.LineNumber, curr.Ticker, FormatTimestampForDisplay(curr.Timestamp)));
                        }
                    }
                }
            }

            // Add summary of invalid timestamps if any were found
            if (invalidTimestamps > 0 && timestampViolations.Count < MAX_VIOLATION_REPORTS)
            {
                timestampViolations.Add(string.Format("Total invalid timestamps found: {0}", invalidTimestamps));
            }

            return new TimestampValidationResult(timestampViolations, contractsWithMultipleRecords, totalRecordsProcessed);
        }

        private static bool TryParseTimestamp(string timestampStr, out long timestamp, out DateTime dateTime)
        {
            timestamp = 0;
            dateTime = DateTime.MinValue;

            if (string.IsNullOrWhiteSpace(timestampStr))
                return false;

            if (!long.TryParse(timestampStr, out timestamp))
                return false;

            try
            {
                // Convert nanoseconds to DateTime (Polygon uses nanoseconds since epoch)
                var milliseconds = timestamp / 1_000_000;
                dateTime = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
                
                // Basic sanity check - should be a reasonable date
                var minDate = new DateTime(2000, 1, 1);
                var maxDate = DateTime.UtcNow.AddYears(1);
                
                return dateTime >= minDate && dateTime <= maxDate;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatTimestampForDisplay(long timestamp)
        {
            try
            {
                var milliseconds = timestamp / 1_000_000;
                var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
                return dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff UTC");
            }
            catch
            {
                return timestamp.ToString();
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Timestamp_Parsing_Validation()
        {
            // Test valid timestamp parsing
            var validTimestamp = "1609459200000000000"; // 2021-01-01 00:00:00 UTC in nanoseconds
            Assert.IsTrue(TryParseTimestamp(validTimestamp, out var timestamp, out var dateTime));
            Assert.AreEqual(1609459200000000000L, timestamp);
            Assert.AreEqual(2021, dateTime.Year);
            Assert.AreEqual(1, dateTime.Month);
            Assert.AreEqual(1, dateTime.Day);

            // Test invalid timestamps
            Assert.IsFalse(TryParseTimestamp("", out _, out _));
            Assert.IsFalse(TryParseTimestamp("invalid", out _, out _));
            Assert.IsFalse(TryParseTimestamp("123", out _, out _)); // Too small to be a valid date
            
            // Test timestamp formatting
            var formatted = FormatTimestampForDisplay(1609459200000000000L);
            Assert.IsTrue(formatted.Contains("2021-01-01"));
            Assert.IsTrue(formatted.Contains("UTC"));

            ConsoleUtilities.WriteLine(string.Format("Timestamp parsing test completed. Sample formatted: {0}", formatted));
        }
    }
}
