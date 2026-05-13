using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Trade.Prices2;

namespace Trade.Polygon2
{
    /// <summary>
    ///     Utility class for splitting and processing S3 financial data files.
    ///     Provides functionality to split bulk option files into per-contract CSV files
    ///     and verify the integrity of split files against original bulk data.
    /// </summary>
    internal static class S3FileSplitter
    {
        /// <summary>
        ///     Verifies that all option prices in the split per-contract files match the original bulk files.
        ///     Throws an exception if any price is missing or mismatched.
        /// </summary>
        /// <param name="bulkDirectory">Directory containing the original bulk Polygon CSV files</param>
        /// <param name="splitDirectory">Directory containing the split per-contract CSV files</param>
        /// <param name="targetSymbol">Underlying symbol (e.g., "SPY")</param>
        /// <param name="useHashing">Switch: false = string comparison, true = hashing</param>
        public static void VerifySplitOptionFiles(
            string bulkDirectory,
            string splitDirectory,
            string targetSymbol,
            bool useHashing = false)
        {
            var allBulkFiles = Directory.GetFiles(bulkDirectory, "*.csv");
            var totalBulkFiles = allBulkFiles.Length;
            var bulkFilesRead = 0;

            var splitFiles = Directory.GetFiles(splitDirectory, "*.csv");
            var totalSplitFiles = splitFiles.Length;
            var filesVerified = 0;

            // We'll accumulate lines/hashes for all contracts as we go, but verify split files after each batch
            var bulkByContract = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            for (var batchStart = 0; batchStart < totalBulkFiles; batchStart += 20)
            {
                var batchEnd = Math.Min(batchStart + 20, totalBulkFiles);
                var batch = allBulkFiles.Skip(batchStart).Take(batchEnd - batchStart).ToArray();

                using (var sha1 = useHashing ? SHA1.Create() : null)
                {
                    foreach (var file in batch)
                    {
                        using (var reader = new StreamReader(file))
                        {
                            var header = reader.ReadLine(); // skip header
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                line = line.Trim();
                                if (string.IsNullOrWhiteSpace(line)) continue;

                                var parts = line.Split(',');
                                if (parts.Length < 8) continue;

                                var ticker = parts[0].Trim().ToUpper();
                                var parsedTicker = Ticker.ParseToOption(ticker);

                                // Only consider options for the target symbol
                                if (!parsedTicker.IsOption || parsedTicker.UnderlyingSymbol != targetSymbol.ToUpper())
                                    continue;

                                var contractSymbol = parsedTicker.Symbol;

                                if (!bulkByContract.TryGetValue(contractSymbol, out var set))
                                {
                                    set = new HashSet<string>();
                                    bulkByContract[contractSymbol] = set;
                                }

                                // Store either the line or its hash
                                if (useHashing)
                                {
                                    var hash = Convert.ToBase64String(sha1.ComputeHash(Encoding.UTF8.GetBytes(line)));
                                    set.Add(hash);
                                }
                                else
                                {
                                    set.Add(line);
                                }
                            }
                        }

                        bulkFilesRead++;
                        var processInfo = Process.GetCurrentProcess();
                        var ramBytes = processInfo.WorkingSet64;
                        var ramMB = ramBytes / (1024.0 * 1024.0);
                        ConsoleUtilities.WriteLine($"📁 Bulk files read: {bulkFilesRead:N0} | RAM used: {ramMB:N2} MB");
                    }
                }

                // 2. After each batch of bulk files, verify all split files against the lines/hashes so far
                Parallel.ForEach(splitFiles, splitFile =>
                {
                    var fileName = Path.GetFileNameWithoutExtension(splitFile);

                    // Find the closest matching contract symbol in the bulk data
                    var matchingBulkSymbol = bulkByContract.Keys
                        .FirstOrDefault(k =>
                            GenerateSafeFileName(k).Equals(fileName, StringComparison.OrdinalIgnoreCase));

                    if (matchingBulkSymbol == null)
                        // It's possible the contract hasn't been seen yet in the current batch, so skip for now
                        return;

                    var bulkSet = bulkByContract[matchingBulkSymbol];

                    using (var reader = new StreamReader(splitFile))
                    {
                        var header = reader.ReadLine(); // skip header
                        string line;
                        var splitSet = new HashSet<string>();
                        using (var sha1 = useHashing ? SHA1.Create() : null)
                        {
                            var lineNumber = 1;
                            var lineSet = new HashSet<string>();
                            while ((line = reader.ReadLine()) != null)
                            {
                                line = line.Trim();
                                if (string.IsNullOrWhiteSpace(line))
                                {
                                    lineNumber++;
                                    continue;
                                }

                                // Check for duplicate lines in the split file
                                if (!lineSet.Add(line))
                                    throw new InvalidDataException(
                                        $"Duplicate line detected in split file {splitFile} at line {lineNumber}: {line}");

                                // Ensure all lines are for the contract named by the file
                                var parts = line.Split(',');
                                if (parts.Length < 1)
                                    throw new InvalidDataException(
                                        $"Malformed line in split file {splitFile} at line {lineNumber}: {line}");

                                var ticker = parts[0].Trim().ToUpper();
                                var parsedTicker = Ticker.ParseToOption(ticker);
                                var contractSymbol = parsedTicker.IsOption ? parsedTicker.Symbol : ticker;
                                if (!GenerateSafeFileName(contractSymbol)
                                        .Equals(fileName, StringComparison.OrdinalIgnoreCase))
                                    throw new InvalidDataException(
                                        $"Line in split file {splitFile} at line {lineNumber} does not match contract symbol for this file. " +
                                        $"Expected: {fileName}, Found: {GenerateSafeFileName(contractSymbol)} (from ticker: {ticker})");

                                // Store either the line or its hash
                                if (useHashing)
                                {
                                    var hash = Convert.ToBase64String(sha1.ComputeHash(Encoding.UTF8.GetBytes(line)));
                                    splitSet.Add(hash);
                                }
                                else
                                {
                                    splitSet.Add(line);
                                }

                                lineNumber++;
                            }

                            // Check for missing prices (in bulk but not in split)
                            var missing = bulkSet.Where(bulkItem => !splitSet.Contains(bulkItem)).ToList();
                            if (missing.Count > 0)
                                throw new InvalidDataException(
                                    $"Missing {missing.Count} prices in split file {splitFile} for contract {matchingBulkSymbol} ({(useHashing ? "by hash" : "by string")})."
                                );
                        }
                    }

                    Interlocked.Increment(ref filesVerified);
                    if (filesVerified % 20 == 0 || filesVerified == totalSplitFiles)
                        ConsoleUtilities.WriteLine(
                            $"✅ Verified {filesVerified:N0} of {totalSplitFiles:N0} split files...");
                });

                bulkByContract.Clear();
            }

            // Final RAM usage report
            var finalProcess = Process.GetCurrentProcess();
            var finalRamBytes = finalProcess.WorkingSet64;
            var finalRamMB = finalRamBytes / (1024.0 * 1024.0);
            ConsoleUtilities.WriteLine(
                $"✅ All split option files in {splitDirectory} verified against bulk data in {bulkDirectory} for {targetSymbol}.");
            ConsoleUtilities.WriteLine($"🧠 Final RAM used: {finalRamMB:N2} MB");
        }

        /// <summary>
        ///     Main method to split files and process price records.
        ///     Handles Polygon.io CSV format with parallel processing and validation.
        /// </summary>
        /// <param name="file">Input file path</param>
        /// <param name="prices">Prices object for validation</param>
        /// <param name="lines">File content as string array</param>
        /// <param name="targetSymbol">Target symbol to filter for</param>
        /// <param name="ifValidatePrices">Whether to validate prices against existing data</param>
        /// <param name="addToPrices">Whether to add records to the prices object</param>
        /// <param name="enableBackups">Whether to create backup files when overwriting</param>
        /// <returns>Array of processed PriceRecord objects</returns>
        internal static PriceRecord[] SplitFiles(string file, Prices prices, string[] lines, string targetSymbol,
            bool ifValidatePrices, bool addToPrices, bool enableBackups = true)
        {
            ConsoleUtilities.Enabled = true;
            ConsoleUtilities.WriteLine(
                $"📊 Processing file: {file} {new FileInfo(file).Length / 1024.0 / 1024.0:F2} MB with target symbol: {targetSymbol}");
            ConsoleUtilities.Enabled = false;

            var recordsLoaded = 0;
            var invalidRecords = 0;
            var otherSymbolsSkipped = 0;
            var priceRecords = new List<PriceRecord>();

            // Use thread-safe collections for parallel processing
            var recordsByContract = new ConcurrentDictionary<string, ConcurrentBag<string>>();
            var contractHeaders = new ConcurrentDictionary<string, string>();

            ConsoleUtilities.WriteLine("📊 Processing Polygon.io CSV format...");

            // Get Eastern TimeZoneInfo once for efficiency
            var easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

            // US market hours: 9:30 AM to 4:15 PM Eastern Time
            var marketOpen = new TimeSpan(9, 30, 0);
            var marketClose = new TimeSpan(16, 15, 0);

            // Store the header line for CSV files
            var csvHeader = lines.Length > 0
                ? lines[0]
                : "ticker,volume,open,close,high,low,window_start,transactions";

            // Parallel processing of lines (skipping header)
            Parallel.For(1, lines.Length, i =>
            {
                try
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) return;

                    var parts = line.Split(',');
                    if (parts.Length < 8) // ticker,volume,open,close,high,low,window_start,transactions
                    {
                        Interlocked.Increment(ref invalidRecords);
                        return;
                    }

                    var ticker = parts[0].Trim().ToUpper();
                    var parsedTicker = Ticker.ParseToOption(ticker);

                    // Filter for target symbol
                    if (ticker != targetSymbol.ToUpper() && parsedTicker.UnderlyingSymbol != targetSymbol.ToUpper())
                    {
                        Interlocked.Increment(ref otherSymbolsSkipped);
                        return;
                    }

                    // Parse Polygon.io CSV format
                    if (!int.TryParse(parts[1], out var volume)) return;
                    if (!double.TryParse(parts[2], out var open)) return;
                    if (!double.TryParse(parts[3], out var close)) return;
                    if (!double.TryParse(parts[4], out var high)) return;
                    if (!double.TryParse(parts[5], out var low)) return;
                    if (!long.TryParse(parts[6], out var windowStartNanos)) return;
                    if (!int.TryParse(parts[7], out var transactions)) return;

                    // Convert nanoseconds to DateTime (UTC)
                    var utcTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(windowStartNanos / 1000000).UtcDateTime;

                    // Convert to US Eastern Time
                    var easternTimestamp = TimeZoneInfo.ConvertTimeFromUtc(utcTimestamp, easternTimeZone);

                    // Filter to regular market hours (inclusive open, exclusive close)
                    var timeOfDay = easternTimestamp.TimeOfDay;
                    if (timeOfDay < marketOpen || timeOfDay >= marketClose)
                        return;

                    // Create PriceRecord
                    var record = new PriceRecord(
                        easternTimestamp, TimeFrame.M1,
                        open,
                        high,
                        low,
                        close,
                        volume: volume,
                        wap: close, // WAP approximation - real files would have better calculation
                        count: transactions,
                        option: parsedTicker.IsOption ? parsedTicker : null
                    );

                    // Group by contract symbol for CSV file splitting
                    var contractSymbol = parsedTicker.IsOption ? parsedTicker.Symbol : ticker;

                    contractHeaders.TryAdd(contractSymbol, csvHeader);
                    var bag = recordsByContract.GetOrAdd(contractSymbol, _ => new ConcurrentBag<string>());
                    bag.Add(line);

                    if (!parsedTicker.IsOption)
                    {
                        // This is to check one data source against another...
                        // Prices.cs is considered the master... we are NOT adding prices to Prices.cs here...
                        var priceRecord = prices.GetPriceAt(record.DateTime);

                        if (priceRecord == null || record != priceRecord)
                        {
                            // Only check if both records are not null
                            if (priceRecord != null)
                            {
                                var diff =
                                    Math.Abs(record.Open - priceRecord.Open) +
                                    Math.Abs(record.High - priceRecord.High) +
                                    Math.Abs(record.Low - priceRecord.Low) +
                                    Math.Abs(record.Close - priceRecord.Close);

                                if (diff > 0.08)
                                    throw new InvalidDataException(
                                        $"PriceRecord mismatch at {record.DateTime:yyyy-MM-dd HH:mm:ss}: " +
                                        $"Loaded record: {record}, Existing record: {priceRecord}, OHLC diff sum: {diff:F6}"
                                    );
                            }
                            else
                            {
                                throw new InvalidDataException(
                                    $"PriceRecord missing at {record.DateTime:yyyy-MM-dd HH:mm:ss}: " +
                                    $"Loaded record: {record}, Existing record: {priceRecord}"
                                );
                            }
                        }
                    }

                    lock (priceRecords)
                    {
                        priceRecords.Add(record);
                    }

                    Interlocked.Increment(ref recordsLoaded);

                    // Progress reporting for large datasets
                    if (recordsLoaded % 1000 == 0 && recordsLoaded > 0)
                        ConsoleUtilities.WriteLine($"📈 Loaded {recordsLoaded:N0} records for {targetSymbol}...");
                }
                catch
                {
                    Interlocked.Increment(ref invalidRecords);
                }
            });

            // Convert thread-safe collections back to normal dictionaries for downstream use
            var recordsByContractFinal = recordsByContract.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
            var contractHeadersFinal = contractHeaders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 📁 Save CSV files by contract symbol
            var csvFilesSaved = SaveContractCsvFilesParallel(recordsByContractFinal, contractHeadersFinal, targetSymbol,
                enableBackups);

            // ✅ FIXED: Add the loaded records to the EXISTING _prices instance and create validation summary
            if (ifValidatePrices && priceRecords.Count > 0)
            {
                Prices prices2 = null;

                if (!addToPrices)
                    prices2 = new Prices();
                else
                    prices2 = prices;

                // Add records to the existing prices instance
                prices2.AddPricesBatch(priceRecords);

                // Validate the loaded data and get validation summary
                var validationSummary = prices.ValidateLoadedData();

                // Create a LoadResult for summary printing
                var loadResult = new Prices.LoadResult
                {
                    Success = validationSummary.IsValid,
                    ValidRecordsLoaded = priceRecords.Count,
                    SkippedRecords = invalidRecords + otherSymbolsSkipped,
                    ValidationErrors =
                        new ConcurrentBag<string>(validationSummary.Errors),
                    ValidationWarnings =
                        new ConcurrentBag<string>(validationSummary.Warnings),
                    LoadTimeMilliseconds = 0, // Set if you have timing info
                    TotalLinesProcessed = lines.Length - 1, // Exclude header line
                    FirstRecordDate = priceRecords.Count > 0 ? priceRecords.Min(r => r.DateTime) : (DateTime?)null,
                    LastRecordDate = priceRecords.Count > 0 ? priceRecords.Max(r => r.DateTime) : (DateTime?)null
                };

                // Log the validation summary
                prices.LogLoadingSummary(loadResult);

                // Also log validation-specific details
                if (validationSummary.Errors.Any() || validationSummary.Warnings.Any())
                {
                    ConsoleUtilities.WriteLine($"📊 Validation Summary for {targetSymbol}:");
                    ConsoleUtilities.WriteLine($"   ✅ Valid: {validationSummary.IsValid}");
                    ConsoleUtilities.WriteLine($"   📁 Total Records: {validationSummary.TotalRecords:N0}");
                    ConsoleUtilities.WriteLine(
                        $"   📅 Date Range: {validationSummary.FirstRecord:yyyy-MM-dd} to {validationSummary.LastRecord:yyyy-MM-dd}");

                    if (validationSummary.Errors.Any())
                    {
                        ConsoleUtilities.WriteLine($"   ❌ Errors ({validationSummary.Errors.Count}):");
                        foreach (var error in validationSummary.Errors.Take(5))
                            ConsoleUtilities.WriteLine($"      - {error}");

                        if (validationSummary.Errors.Count > 5)
                            ConsoleUtilities.WriteLine(
                                $"      ... and {validationSummary.Errors.Count - 5} more errors");
                    }

                    if (validationSummary.Warnings.Any())
                    {
                        ConsoleUtilities.WriteLine($"   ⚠️  Warnings ({validationSummary.Warnings.Count}):");
                        foreach (var warning in validationSummary.Warnings.Take(3))
                            ConsoleUtilities.WriteLine($"      - {warning}");

                        if (validationSummary.Warnings.Count > 3)
                            ConsoleUtilities.WriteLine(
                                $"      ... and {validationSummary.Warnings.Count - 3} more warnings");
                    }
                }
            }

            ConsoleUtilities.WriteLine("✅ Polygon.io CSV loading complete:");
            ConsoleUtilities.WriteLine($"   📊 {targetSymbol} records loaded: {recordsLoaded:N0}");
            ConsoleUtilities.WriteLine($"   🔄 Other symbols skipped: {otherSymbolsSkipped:N0}");
            ConsoleUtilities.WriteLine($"   ❌ Invalid records: {invalidRecords:N0}");
            ConsoleUtilities.WriteLine($"   📈 Total Prices records: {prices.Records.Count:N0}");
            ConsoleUtilities.WriteLine($"   📁 CSV files processed: {csvFilesSaved:N0} contract files");

            ConsoleUtilities.Enabled = true;

            return priceRecords.ToArray();
        }

        /// <summary>
        ///     Save CSV files grouped by contract symbol with parallel processing
        ///     Enhanced version that reads existing files, merges, deduplicates, and sorts before writing
        /// </summary>
        /// <param name="recordsByContract">Dictionary of contract symbol to CSV lines</param>
        /// <param name="contractHeaders">Dictionary of contract symbol to CSV headers</param>
        /// <param name="targetSymbol">Target symbol for directory naming</param>
        /// <param name="enableBackups">Whether to create backup files when overwriting existing files</param>
        /// <returns>Number of CSV files saved</returns>
        private static int SaveContractCsvFilesParallel(Dictionary<string, List<string>> recordsByContract,
            Dictionary<string, string> contractHeaders, string targetSymbol, bool enableBackups = true)
        {
            if (recordsByContract.Count == 0)
            {
                ConsoleUtilities.WriteLine($"⚠️  No contract data to save for {targetSymbol}");
                return 0;
            }

            // Create output directory
            var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ContractData", targetSymbol.ToUpper());
            Directory.CreateDirectory(outputDirectory);

            // Thread-safe counters
            var filesSaved = 0;
            var filesSkipped = 0;
            var filesMerged = 0;
            var totalRecordsWritten = 0;
            var totalDuplicatesRemoved = 0;
            var lockObject = new object();

            ConsoleUtilities.WriteLine($"📁 Saving contract CSV files to: {outputDirectory}");
            ConsoleUtilities.WriteLine(
                $"🔄 Processing {recordsByContract.Count} contracts with merge/deduplicate logic...");

            // Determine optimal parallelism level
            var maxDegreeOfParallelism = Math.Min(Environment.ProcessorCount,
                Math.Max(1, recordsByContract.Count / 10)); // Don't over-parallelize small sets
            //maxDegreeOfParallelism = 1;
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };

            // Process contracts in parallel
            Parallel.ForEach(recordsByContract.OrderBy(kvp => kvp.Key), parallelOptions, contractGroup =>
            {
                var contractSymbol = contractGroup.Key;
                var newCsvLines = contractGroup.Value;

                // Generate safe filename from contract symbol
                var safeFileName = GenerateSafeFileName(contractSymbol);
                var csvFilePath = Path.Combine(outputDirectory, $"{safeFileName}.csv");

                try
                {
                    var fileExists = File.Exists(csvFilePath);
                    var headerLine = contractHeaders.ContainsKey(contractSymbol)
                        ? contractHeaders[contractSymbol]
                        : "ticker,volume,open,close,high,low,window_start,transactions";

                    // Use a HashSet to deduplicate by full line content
                    var allLines = new HashSet<string>();
                    var existingRecordCount = 0;
                    var newRecordsAdded = 0;

                    // Read existing file if it exists
                    if (fileExists)
                        try
                        {
                            var existingLines = File.ReadAllLines(csvFilePath);

                            // Use existing header if available and valid
                            if (existingLines.Length > 0 && !string.IsNullOrWhiteSpace(existingLines[0]))
                                headerLine = existingLines[0].Trim();

                            // Skip header line and collect existing data
                            for (var i = 1; i < existingLines.Length; i++)
                            {
                                var line = existingLines[i].Trim();
                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    allLines.Add(line);
                                    existingRecordCount++;
                                }
                            }
                        }
                        catch (Exception readException)
                        {
                            if (ConsoleUtilities.Enabled)
                                lock (lockObject)
                                {
                                    ConsoleUtilities.WriteLine(
                                        $"      ⚠️  Error reading existing file {safeFileName}.csv: {readException.Message}");
                                    ConsoleUtilities.WriteLine("      🔄 Will create new file instead");
                                }

                            existingRecordCount = 0;
                            allLines.Clear();
                        }

                    // Add new data lines to the collection (deduplicate by full line)
                    foreach (var newLine in newCsvLines)
                    {
                        var trimmedLine = newLine.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmedLine))
                            if (allLines.Add(trimmedLine))
                                newRecordsAdded++;
                    }

                    // Convert back to list and sort by timestamp
                    var sortedDataLines = allLines
                        .OrderBy(line => ExtractTimestamp(line))
                        .ToList();

                    // Check if we need to write the file
                    var shouldWrite = !fileExists || newRecordsAdded > 0 ||
                                      sortedDataLines.Count != existingRecordCount;

                    if (!shouldWrite)
                    {
                        if (ConsoleUtilities.Enabled)
                            lock (lockObject)
                            {
                                ConsoleUtilities.WriteLine($"   ✅ {safeFileName}.csv - No new data to add, skipping");
                                filesSkipped++;
                            }

                        return; // Skip to next contract
                    }

                    // Create backup if file exists and we're modifying it
                    if (fileExists && enableBackups)
                        try
                        {
                            var backupPath = csvFilePath + $".backup.{DateTime.Now:yyyyMMdd_HHmmss}";
                            File.Copy(csvFilePath, backupPath);
                            if (ConsoleUtilities.Enabled)
                                lock (lockObject)
                                {
                                    ConsoleUtilities.WriteLine(
                                        $"      💾 Backup created: {Path.GetFileName(backupPath)}");
                                }
                        }
                        catch (Exception backupException)
                        {
                            if (ConsoleUtilities.Enabled)
                                lock (lockObject)
                                {
                                    ConsoleUtilities.WriteLine($"      ⚠️  Backup failed: {backupException.Message}");
                                }
                        }

                    // Prepare final CSV content
                    var csvContent = new List<string> { headerLine };
                    csvContent.AddRange(sortedDataLines);

                    // Write CSV file with retry logic
                    var maxRetries = 3;
                    var retryDelay = 100; // milliseconds

                    for (var retry = 0; retry < maxRetries; retry++)
                        try
                        {
                            File.WriteAllLines(csvFilePath, csvContent, Encoding.UTF8);
                            break; // Success
                        }
                        catch (IOException ioException) when (retry < maxRetries - 1)
                        {
                            // File might be temporarily locked, wait and retry
                            if (ConsoleUtilities.Enabled)
                                lock (lockObject)
                                {
                                    ConsoleUtilities.WriteLine(
                                        $"      🔄 File write retry {retry + 1}/{maxRetries} for {safeFileName}.csv: {ioException.Message}");
                                }

                            Thread.Sleep(retryDelay);
                            retryDelay *= 2; // Exponential backoff
                        }

                    // Thread-safe logging and counter updates
                    if (ConsoleUtilities.Enabled)
                        lock (lockObject)
                        {
                            filesSaved++;
                            if (fileExists) filesMerged++;

                            totalRecordsWritten += sortedDataLines.Count;

                            // totalDuplicatesRemoved is not tracked in this logic
                            // Parse contract type for better logging
                            var parsedTicker = Ticker.ParseToOption(contractSymbol);
                            var contractType = parsedTicker.IsOption ? "Option" : "Stock";
                            var contractDescription = parsedTicker.IsOption
                                ? $"{parsedTicker.UnderlyingSymbol} {parsedTicker.ExpirationDate:yyyy-MM-dd} {parsedTicker.OptionType} ${parsedTicker.StrikePrice:F2}"
                                : contractSymbol;

                            var action = fileExists ? "Merged" : "Created";
                            ConsoleUtilities.WriteLine($"   💾 {action} {contractType}: {contractDescription}");
                            ConsoleUtilities.WriteLine($"      📄 File: {safeFileName}.csv");
                            ConsoleUtilities.WriteLine(
                                $"      📊 Records: {existingRecordCount:N0} existing + {newRecordsAdded:N0} new = {sortedDataLines.Count:N0} total");

                            ConsoleUtilities.WriteLine(
                                $"      [Thread {Thread.CurrentThread.ManagedThreadId}]");
                        }
                }
                catch (Exception ex)
                {
                    if (ConsoleUtilities.Enabled)
                        lock (lockObject)
                        {
                            ConsoleUtilities.WriteLine($"   ❌ Failed to process {contractSymbol}: {ex.Message}");
                        }
                }
            });

            ConsoleUtilities.WriteLine("📊 Contract CSV Summary (Merge & Deduplicate):");
            ConsoleUtilities.WriteLine($"   📁 Files processed: {filesSaved:N0}");
            ConsoleUtilities.WriteLine($"   📁 Files merged: {filesMerged:N0}");
            ConsoleUtilities.WriteLine($"   📁 Files skipped (no new data): {filesSkipped:N0}");
            ConsoleUtilities.WriteLine($"   💾 Backups enabled: {enableBackups}");
            ConsoleUtilities.WriteLine($"   📈 Total records written: {totalRecordsWritten:N0}");
            ConsoleUtilities.WriteLine($"   🔄 Parallel degree: {maxDegreeOfParallelism}");
            ConsoleUtilities.WriteLine($"   📂 Directory: {outputDirectory}");

            return filesSaved;
        }

        /// <summary>
        ///     Generate a safe filename from contract symbol by removing invalid characters
        /// </summary>
        /// <param name="contractSymbol">Contract symbol</param>
        /// <returns>Safe filename</returns>
        internal static string GenerateSafeFileName(string contractSymbol)
        {
            if (string.IsNullOrEmpty(contractSymbol))
                return "Unknown";

            // Remove or replace invalid filename characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var safeFileName = contractSymbol;

            foreach (var invalidChar in invalidChars) safeFileName = safeFileName.Replace(invalidChar, '_');

            // Replace some common special characters that might cause issues
            safeFileName = safeFileName
                .Replace(':', '_')
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace('?', '_')
                .Replace('*', '_')
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace('|', '_')
                .Replace('"', '_');

            // Limit length to avoid filesystem limitations
            const int maxLength = 200;
            if (safeFileName.Length > maxLength) safeFileName = safeFileName.Substring(0, maxLength);

            return safeFileName;
        }

        /// <summary>
        ///     Extract timestamp from CSV line for sorting purposes
        /// </summary>
        /// <param name="csvLine">CSV line</param>
        /// <returns>Timestamp as long (nanoseconds) or 0 if parsing fails</returns>
        internal static long ExtractTimestamp(string csvLine)
        {
            try
            {
                var parts = csvLine.Split(',');
                if (parts.Length >= 7 && long.TryParse(parts[6], out var timestamp)) return timestamp;
            }
            catch
            {
                // Ignore parsing errors
            }

            return 0;
        }
    }
}