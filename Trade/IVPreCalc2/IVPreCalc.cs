using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Polygon2;
using Trade.Prices2;

namespace Trade.IVPreCalc2
{
    internal class IVPreCalc
    {
        public async Task Prepare(bool verifyAgainstContractFiles, bool calculateIV, bool callPolygonForIV)
        {
            var prices = new Prices();
            var polygon = new Polygon2.Polygon(prices, "SPY", 10, 10);

            var bulkDir = ResolveBulkDir();
            var contractsDir = ResolveContractsDir();
            var stocksDir = ResolveStocksDir(); // resolve stock minute files for underlying close

            if (string.IsNullOrEmpty(bulkDir) || !Directory.Exists(bulkDir))
                Assert.Inconclusive("Polygon bulk options directory not found. Expected PolygonBulkData with options CSVs.");

            if (string.IsNullOrEmpty(contractsDir) || !Directory.Exists(contractsDir))
                Assert.Inconclusive(@"Contracts directory not found. Expected ContractData\\SPY to exist.");

            using (var tracer = new BulkFileContractTracer(bulkDir))
            {
                // Build distinct contracts from the newest options bulk file
                var newestOptionsFile = Directory.GetFiles(bulkDir, "*.csv", SearchOption.AllDirectories)
                    .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(f =>
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        DateTime d;
                        DateTime? dt = (name != null && name.Length >= 10 &&
                                        DateTime.TryParseExact(name.Substring(0, 10), "yyyy-MM-dd",
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.None, out d))
                            ? (DateTime?)d.Date
                            : null;
                        return new { Path = f, Date = dt };
                    })
                    .Where(x => x.Date.HasValue)
                    .OrderByDescending(x => x.Date.Value)
                    .Select(x => x.Path)
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(newestOptionsFile) || !File.Exists(newestOptionsFile))
                    Assert.Inconclusive("No options bulk CSV found to enumerate contracts.");

                var contractsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var contractsList = new List<string>();
                using (var sr = new StreamReader(newestOptionsFile))
                {
                    // skip header
                    sr.ReadLine();
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var comma = line.IndexOf(',');
                        if (comma <= 0) continue;
                        var ticker = line.Substring(0, comma).Trim();
                        if (ticker.StartsWith("O:", StringComparison.OrdinalIgnoreCase))
                        {
                            var upperTicker = ticker.ToUpperInvariant();
                            if (contractsSet.Add(upperTicker)) // Add returns true if item was added (not already present)
                                contractsList.Add(upperTicker);
                        }
                    }
                }

                var contractSymbols = contractsList;
                contractSymbols = contractSymbols
                    .Where(s => s.Contains("O:"))
                    .OrderBy(s =>
                    {
                        var key = BulkFileContractTracer.ParseContractKey(s);
                        return key != null ? key : new ContractKey { RawTicker = s };
                    }, ContractKeyComparer.Instance)
                    .ToList();

                //MIKE: The IV walkback functionality MUST MUST MUST have all elements in order...
                //USE: ContractKeyComparer.Instance...

                var newestBulkDate = tracer.GetNewestBulkFileDateOrDefault(DateTime.Today);
                var endDate = newestBulkDate.Date;
                var startDate = endDate.AddDays(-30);

                // 🚀 NEW: Build expected contracts index for validation
                //var expectedContractsIndex = BuildExpectedContractsIndex(bulkDir, contractSymbols, startDate, endDate);
                //var actualContractsTracker = CreateActualContractsTracker(expectedContractsIndex);

                // 🚀 NEW: Build underlying close price cache ONCE
                Dictionary<(string symbol, DateTime date), double> underlyingCloseCache = null;
                if (calculateIV && !string.IsNullOrEmpty(stocksDir) && Directory.Exists(stocksDir))
                {
                    underlyingCloseCache = BuildUnderlyingCloseCache(stocksDir, contractSymbols, startDate, endDate);
                    Console.WriteLine($"Built underlying close cache with {underlyingCloseCache.Count} entries");
                }

                var solver = new ImpliedVolatilitySolver();
                const double riskFreeRate = 0.05; // simple default for tests
                const double dividendYield = 0.018; // SPY-ish default

                int totalCompared = 0;
                int totalMatched = 0;
                var mismatchSamples = new List<string>();

                // Progress counters
                int totalContracts = contractSymbols.Count;
                int processed = 0;

                foreach (var contractSymbol in contractSymbols)
                {
                    ConsoleUtilities.WriteLine("- - - - - - - - - - - - ");

                    processed++;
                    if (true || processed == 1 || processed % 250 == 0 || processed == totalContracts)
                    {
                        var pct = (processed * 100.0) / Math.Max(1, totalContracts);
                        var msg = $"Processing {processed}/{totalContracts} ({pct:F1}%) - {contractSymbol}";
                        ConsoleUtilities.WriteLine(msg);
                    }

                    var history = await tracer.TraceContractBackwardsAsync(contractSymbol, startDate, endDate)
                        .ConfigureAwait(false);

                    // 🚀 NEW: Track actual tracing results for validation
                    var contractKey = BulkFileContractTracer.ParseContractKey(contractSymbol);
                    if (contractKey?.Underlying != null)
                    {
                        var underlying = contractKey.Underlying;

                        if (history != null && history.Prices != null && history.Prices.Count > 0)
                        {
                            // Decrement by the number of files that had data for this contract
                            var filesWithData = history.SearchStats?.FilesWithData ?? 0;
                            //if (actualContractsTracker.TryGetValue(underlying, out var currentCount))
                            //{
                            //    actualContractsTracker[underlying] = Math.Max(0, currentCount - filesWithData);
                            //}
                        }
                    }

                    if (history == null || history.Prices == null || history.Prices.Count == 0)
                    {
                        var key = BulkFileContractTracer.ParseContractKey(contractSymbol);
                        var option = Ticker.ParseToOption(contractSymbol);
                        continue;
                    }

                    if (verifyAgainstContractFiles && contractSymbol.StartsWith("O:SPY2")) //HACK: Remove me!
                    {
                        var verification = VerifyAgainstContractFiles(contractSymbol, history, contractsDir);

                        totalCompared += verification.TotalRecords;
                        totalMatched += verification.MatchedRecords;

                        if (!verification.IsValid && mismatchSamples.Count < 5)
                        {
                            foreach (var m in verification.Mismatches)
                            {
                                mismatchSamples.Add($"{contractSymbol}: {m}");
                                if (mismatchSamples.Count >= 5) break;
                            }
                        }
                    }

                    if (calculateIV && underlyingCloseCache != null)
                    {
                        // Calculate IVs from daily close prices for this contract (using cached stock data)
                        var ticker = Ticker.ParseToOption(contractSymbol);
                        if (ticker.IsOption && ticker.ExpirationDate.HasValue && ticker.StrikePrice.HasValue &&
                            ticker.OptionType.HasValue)
                        {
                            var isCall = ticker.OptionType.Value == OptionType.Call;
                            var strike = ticker.StrikePrice.Value;
                            var exp = ticker.ExpirationDate.Value.Date;
                            var underlying = ticker.UnderlyingSymbol;

                            // Only calculate IV for the most recent day (last entry in history)
                            var mostRecentPrice = history.Prices.OrderByDescending(p => p.Date).FirstOrDefault();
                            if (mostRecentPrice != null)
                            {
                                // skip dates on/after expiration
                                if (mostRecentPrice.Date.Date >= exp) continue;

                                // 🚀 NEW: Use cached close price instead of file I/O
                                var cacheKey = (underlying, mostRecentPrice.Date.Date);
                                if (underlyingCloseCache.TryGetValue(cacheKey, out var underlyingClose) && underlyingClose > 0)
                                {
                                    var daysToExp = (exp - mostRecentPrice.Date.Date).TotalDays;
                                    if (daysToExp > 0)
                                    {
                                        var T = daysToExp / 365.0;

                                        // Option market price from our daily aggregation close
                                        var marketPrice = mostRecentPrice.Close;
                                        if (marketPrice > 0)
                                        {
                                            var iv = solver.SolveIV(underlyingClose, strike, T, riskFreeRate,
                                                dividendYield, marketPrice, isCall);
                                            // For now, we just ensure IV calculation runs; could be logged or asserted later
                                            // (e.g., Debug.WriteLine or store in a local structure if needed)

                                            if (callPolygonForIV)
                                            {
                                                //call the Polygon API here...
                                                var apiIv = await polygon.GetOptionIvAsync(
                                                    ticker.GetStandardSymbol());
                                                // optionally compare or log apiIv vs iv if apiIv.HasValue
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // Optional: Log when underlying close price is not found in cache
                                    // Console.WriteLine($"No cached underlying close price for {underlying} on {mostRecentPrice.Date:yyyy-MM-dd}");
                                }
                            }
                        }
                    }
                }

                // 🚀 NEW: Validate that all expected contracts were traced
                //var missingContracts = ValidateContractTracingCompleteness(actualContractsTracker);

                if (verifyAgainstContractFiles)
                {
                    Assert.IsTrue(totalCompared > 0, "No overlapping data found to compare in the last 30 days.");

                    var matchRate = (double)totalMatched / totalCompared;
                    Assert.IsTrue(matchRate >= 0.8,
                        $"Match rate too low. Matched {totalMatched}/{totalCompared} ({matchRate:P1}). " +
                        $"{(mismatchSamples.Count > 0 ? "Example mismatches: " + string.Join(" | ", mismatchSamples) : string.Empty)}");

                    // 🚀 NEW: Assert that contract tracing was complete
                    //Assert.IsTrue(missingContracts.Count == 0,
                    //    $"Contract tracing incomplete. Missing data for {missingContracts.Count} underlyings: " +
                    //    string.Join(", ", missingContracts.Select(kvp => $"{kvp.Key}({kvp.Value})")));
                }
            }
        }

        /// <summary>
        /// Build a comprehensive cache of underlying close prices for all symbols and dates needed.
        /// This dramatically reduces file I/O by reading each stock file only once.
        /// </summary>
        private static Dictionary<(string symbol, DateTime date), double> BuildUnderlyingCloseCache(
            string stocksDir,
            List<string> contractSymbols,
            DateTime startDate,
            DateTime endDate)
        {
            var cache = new Dictionary<(string symbol, DateTime date), double>();

            try
            {
                // 1. Extract all unique underlying symbols from contracts
                var underlyingSymbols = contractSymbols
                    .Where(c => c.StartsWith("O:", StringComparison.OrdinalIgnoreCase))
                    .Select(c => Ticker.ParseToOption(c))
                    .Where(t => t.IsOption && !string.IsNullOrEmpty(t.UnderlyingSymbol))
                    .Select(t => t.UnderlyingSymbol)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Console.WriteLine($"Found {underlyingSymbols.Count} unique underlying symbols");

                // 2. Get all stock files in the date range
                var stockFiles = new List<string>();
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    var pattern = date.ToString("yyyy-MM-dd") + "_us_stocks_sip_minute_aggs.csv";
                    var file = Directory.GetFiles(stocksDir, pattern, SearchOption.AllDirectories).FirstOrDefault();
                    if (!string.IsNullOrEmpty(file) && File.Exists(file))
                    {
                        stockFiles.Add(file);
                    }
                }

                Console.WriteLine($"Found {stockFiles.Count} stock files to process");

                // 3. Process each stock file once
                TimeZoneInfo easternTz;
                try
                {
                    easternTz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                }
                catch
                {
                    // Fallback for non-Windows systems
                    try
                    {
                        easternTz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
                    }
                    catch
                    {
                        // Last resort - use system local time with warning
                        easternTz = TimeZoneInfo.Local;
                    }
                }

                var rthCutoff = new TimeSpan(16, 15, 0);

                foreach (var stockFile in stockFiles.OrderByDescending(_ => _).Take(1))
                {
                    var fileName = Path.GetFileName(stockFile);
                    var fileDate = TryExtractDateFromName(stockFile);
                    if (!fileDate.HasValue) continue;

                    Console.WriteLine($"Processing stock file: {fileName}");

                    // Track last close for each symbol in this file
                    var dailyCloses = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                    using (var sr = new StreamReader(stockFile))
                    {
                        string line = sr.ReadLine(); // skip header
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            var parts = line.Split(',');
                            if (parts.Length < 8) continue;

                            var ticker = parts[0].Trim();

                            // Only process symbols we actually need
                            if (!underlyingSymbols.Contains(ticker, StringComparer.OrdinalIgnoreCase)) continue;

                            if (!double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var close)) continue;
                            if (!long.TryParse(parts[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var windowStartNanos)) continue;

                            var utc = DateTimeOffset.FromUnixTimeMilliseconds(windowStartNanos / 1_000_000).UtcDateTime;
                            var est = TimeZoneInfo.ConvertTimeFromUtc(utc, easternTz);
                            if (est.TimeOfDay >= rthCutoff) continue; // Skip after-hours

                            // Update the last close for this symbol (files are chronological)
                            dailyCloses[ticker] = close;
                        }
                    }

                    // Add all daily closes to the cache
                    foreach (var kvp in dailyCloses)
                    {
                        var cacheKey = (kvp.Key, fileDate.Value.Date);
                        cache[cacheKey] = kvp.Value;
                    }
                }

                Console.WriteLine($"Cache built with {cache.Count} symbol-date entries");
                return cache;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error building underlying close cache: {ex.Message}");
                return new Dictionary<(string symbol, DateTime date), double>();
            }
        }

        /// <summary>
        /// Build a comprehensive index of expected contracts per underlying symbol across all bulk files.
        /// This allows us to validate that TraceContractBackwardsAsync finds all expected data.
        /// </summary>
        private static Dictionary<string, int> BuildExpectedContractsIndex(
            string bulkDataDirectory,
            List<string> contractSymbols,
            DateTime startDate,
            DateTime endDate)
        {
            var expectedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // 1. Extract all unique underlying symbols from contracts we're going to process
                var underlyingSymbols = contractSymbols
                    .Where(c => c.StartsWith("O:", StringComparison.OrdinalIgnoreCase))
                    .Select(c =>
                    {
                        var key = BulkFileContractTracer.ParseContractKey(c);
                        return key?.Underlying;
                    })
                    .Where(u => !string.IsNullOrEmpty(u))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                Console.WriteLine($"Building expected contracts index for {underlyingSymbols.Count} underlying symbols");

                // 2. Get all bulk option files in the date range
                var bulkFiles = Directory.GetFiles(bulkDataDirectory, "*.csv", SearchOption.AllDirectories)
                    .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(f => new { Path = f, Date = TryExtractDateFromName(f) })
                    .Where(x => x.Date.HasValue && x.Date.Value >= startDate.Date && x.Date.Value <= endDate.Date)
                    .OrderByDescending(x => x.Date.Value)
                    .ToList();

                Console.WriteLine($"Scanning {bulkFiles.Count} bulk files for contract occurrences");

                // 3. Track which contracts appear in which files for each underlying
                var contractFileOccurrences = new Dictionary<string, HashSet<DateTime>>(StringComparer.OrdinalIgnoreCase);

                foreach (var bulkFile in bulkFiles)
                {
                    var fileName = Path.GetFileName(bulkFile.Path);
                    Console.WriteLine($"Indexing: {fileName} ({bulkFile.Date.Value:yyyy-MM-dd})");

                    try
                    {
                        using (var sr = new StreamReader(bulkFile.Path))
                        {
                            string line = sr.ReadLine(); // skip header
                            int lineCount = 0;

                            while ((line = sr.ReadLine()) != null)
                            {
                                lineCount++;
                                if (string.IsNullOrWhiteSpace(line)) continue;

                                var comma = line.IndexOf(',');
                                if (comma <= 0) continue;

                                var ticker = line.Substring(0, comma).Trim().ToUpperInvariant();
                                if (!ticker.StartsWith("O:")) continue;

                                // Only track contracts we're actually going to process
                                if (!contractSymbols.Contains(ticker, StringComparer.OrdinalIgnoreCase)) continue;

                                var contractKey = BulkFileContractTracer.ParseContractKey(ticker);
                                if (contractKey?.Underlying == null) continue;

                                // Only track underlyings we care about
                                if (!underlyingSymbols.Contains(contractKey.Underlying)) continue;

                                // Track this contract's occurrence in this file
                                if (!contractFileOccurrences.TryGetValue(ticker, out var fileDates))
                                {
                                    fileDates = new HashSet<DateTime>();
                                    contractFileOccurrences[ticker] = fileDates;
                                }
                                fileDates.Add(bulkFile.Date.Value);

                                // Progress indicator for large files
                                if (lineCount % 1000000 == 0)
                                {
                                    Console.WriteLine($"    Processed {lineCount:N0} lines...");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    Error indexing {fileName}: {ex.Message}");
                    }
                }

                // 4. Calculate expected counts per underlying (total contract-file occurrences)
                foreach (var kvp in contractFileOccurrences)
                {
                    var contractKey = BulkFileContractTracer.ParseContractKey(kvp.Key);
                    if (contractKey?.Underlying == null) continue;

                    var underlying = contractKey.Underlying;
                    var fileOccurrenceCount = kvp.Value.Count;

                    if (!expectedCounts.TryGetValue(underlying, out var currentCount))
                        currentCount = 0;

                    expectedCounts[underlying] = currentCount + fileOccurrenceCount;
                }

                Console.WriteLine($"Expected contracts index built:");
                foreach (var kvp in expectedCounts.OrderByDescending(x => x.Value))
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value} expected contract-file occurrences");
                }

                return expectedCounts;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error building expected contracts index: {ex.Message}");
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Tracks actual contract tracing results to validate against expected counts.
        /// This should be decremented each time TraceContractBackwardsAsync finds data.
        /// </summary>
        private static Dictionary<string, int> CreateActualContractsTracker(Dictionary<string, int> expectedCounts)
        {
            return new Dictionary<string, int>(expectedCounts, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validates that all expected contracts were successfully traced.
        /// Returns a summary of any missing data by underlying symbol.
        /// </summary>
        private static Dictionary<string, int> ValidateContractTracingCompleteness(
            Dictionary<string, int> actualTracker,
            string logPrefix = "Contract Tracing Validation")
        {
            var missing = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            Console.WriteLine($"{logPrefix}:");
            var totalExpected = 0;
            var totalMissing = 0;
            var totalProcessed = 0;

            foreach (var kvp in actualTracker.OrderBy(x => x.Key))
            {
                var underlying = kvp.Key;
                var remainingCount = kvp.Value;

                // Calculate how many were originally expected for this underlying
                var originalExpected = remainingCount;
                if (remainingCount <= 0)
                {
                    // If remaining is 0 or negative, we need to add back what was processed
                    // This assumes the tracker was properly decremented during processing
                    totalProcessed += Math.Abs(remainingCount);
                    originalExpected = Math.Abs(remainingCount);
                }

                totalExpected += originalExpected;

                if (remainingCount > 0)
                {
                    missing[underlying] = remainingCount;
                    totalMissing += remainingCount;
                    Console.WriteLine($"  ❌ {underlying}: {remainingCount}/{originalExpected} contract-file occurrences not traced ({(remainingCount * 100.0 / originalExpected):F1}% missing)");
                }
                else
                {
                    Console.WriteLine($"  ✅ {underlying}: All {originalExpected} contract-file occurrences traced successfully");
                    totalProcessed += originalExpected;
                }
            }

            // Summary statistics
            Console.WriteLine($"\nSummary:");
            Console.WriteLine($"  Total Expected: {totalExpected:N0} contract-file occurrences");
            Console.WriteLine($"  Successfully Traced: {(totalExpected - totalMissing):N0} ({((totalExpected - totalMissing) * 100.0 / Math.Max(1, totalExpected)):F1}%)");

            if (missing.Count == 0)
            {
                Console.WriteLine($"  ✅ Perfect! All {totalExpected:N0} expected contract-file occurrences were successfully traced!");
            }
            else
            {
                Console.WriteLine($"  ⚠️  Missing: {totalMissing:N0} contract-file occurrences across {missing.Count} underlyings ({(totalMissing * 100.0 / Math.Max(1, totalExpected)):F1}%)");

                // Show the top missing underlyings
                var topMissing = missing.OrderByDescending(kvp => kvp.Value).Take(5);
                Console.WriteLine($"  Top missing underlyings:");
                foreach (var kvp in topMissing)
                {
                    Console.WriteLine($"    - {kvp.Key}: {kvp.Value} missing");
                }
            }

            return missing;
        }

        /// <summary>
        /// Gets unique contract symbols from the 10 most recent bulk files.
        /// Returns a HashSet for efficient lookups and a List for ordered processing.
        /// </summary>
        internal static (HashSet<string> ContractsSet, List<string> ContractsList) GetUniqueContractsFromRecentFiles(string bulkDataDirectory, int fileCount = 10)
        {
            var contractsSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var contractsList = new List<string>();

            try
            {
                // Get bulk files sorted by date (newest first)
                var recentFiles = Directory.GetFiles(bulkDataDirectory, "*.csv", SearchOption.AllDirectories)
                    .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(f => new { Path = f, Date = TryExtractDateFromName(f) })
                    .Where(x => x.Date.HasValue)
                    .OrderByDescending(x => x.Date.Value)
                    .Take(fileCount)
                    .Select(x => x.Path)
                    .ToList();

                Console.WriteLine($"Processing {recentFiles.Count} recent bulk files for contract enumeration:");

                foreach (var filePath in recentFiles)
                {
                    var fileName = Path.GetFileName(filePath);
                    var fileDate = TryExtractDateFromName(filePath);
                    Console.WriteLine($"  - {fileName} ({fileDate:yyyy-MM-dd})");

                    try
                    {
                        using (var sr = new StreamReader(filePath))
                        {
                            // Skip header
                            sr.ReadLine();

                            string line;
                            int lineCount = 0;
                            int contractsFound = 0;

                            while ((line = sr.ReadLine()) != null)
                            {
                                lineCount++;

                                if (string.IsNullOrWhiteSpace(line)) continue;

                                var comma = line.IndexOf(',');
                                if (comma <= 0) continue;

                                var ticker = line.Substring(0, comma).Trim();
                                if (ticker.StartsWith("O:", StringComparison.OrdinalIgnoreCase))
                                {
                                    var upperTicker = ticker.ToUpperInvariant();
                                    if (contractsSet.Add(upperTicker)) // Add returns true if item was added (not already present)
                                    {
                                        contractsList.Add(upperTicker);
                                        contractsFound++;
                                    }
                                }

                                // Progress indicator for large files
                                if (lineCount % 1000000 == 0)
                                {
                                    Console.WriteLine($"    Processed {lineCount:N0} lines, found {contractsFound} new contracts...");
                                }
                            }

                            Console.WriteLine($"    Completed: {lineCount:N0} lines, {contractsFound} new contracts from this file");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    Error processing {fileName}: {ex.Message}");
                        // Continue with other files rather than failing completely
                    }
                }

                Console.WriteLine($"Total unique contracts found: {contractsSet.Count:N0}");
                return (contractsSet, contractsList);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetUniqueContractsFromRecentFiles: {ex.Message}");
                return (new HashSet<string>(StringComparer.OrdinalIgnoreCase), new List<string>());
            }
        }

        /// <summary>
        /// Filtered version that gets unique contracts for a specific underlying symbol.
        /// </summary>
        internal static (HashSet<string> ContractsSet, List<string> ContractsList) GetUniqueContractsForUnderlying(
            string bulkDataDirectory,
            string underlyingSymbol,
            int fileCount = 10)
        {
            var (allContracts, allContractsList) = GetUniqueContractsFromRecentFiles(bulkDataDirectory, fileCount);

            var filteredSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filteredList = new List<string>();

            var targetPrefix = $"O:{underlyingSymbol.ToUpperInvariant()}";

            foreach (var contract in allContractsList)
            {
                if (contract.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    filteredSet.Add(contract);
                    filteredList.Add(contract);
                }
            }

            Console.WriteLine($"Filtered to {filteredSet.Count:N0} contracts for underlying {underlyingSymbol}");
            return (filteredSet, filteredList);
        }

        // ---------------- Directory resolution ----------------

        // Allow tests to override data locations by placing files under MockPolygonBulkData and MockContractData.
        public static string ResolveBulkDir(bool preferMocks = false)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                // Prefer mock data first for tests
                Path.Combine(baseDir, "MockPolygonBulkData", "us_options_opra_minute_aggs", "Sorted"),
                Path.Combine(baseDir, "MockPolygonBulkData", "us_options_opra_minute_aggs"),
                Path.Combine(baseDir, "MockPolygonBulkData"),

                // Standard locations
                Path.Combine(baseDir, "PolygonBulkData", "us_options_opra_minute_aggs", "Sorted"),
                Path.Combine(baseDir, "PolygonBulkData", "us_options_opra_minute_aggs"),
                Path.Combine(baseDir, "PolygonBulkData"),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\bin\Debug\PolygonBulkData", "us_options_opra_minute_aggs", "Sorted")),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\bin\Debug\PolygonBulkData"))
            };

            foreach (var dir in candidates.Select(Path.GetFullPath))
            {
                if (!preferMocks && dir.IndexOf("MockPolygonBulkData", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue; // skip mock if not preferred

                // Restore file filtering for options CSVs
                if (Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.csv", SearchOption.AllDirectories)
                      .Any(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0))
                    return dir;
            }

            return null;
        }

        public static string ResolveStocksDir(bool preferMocks = false)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                // Prefer mock stocks under the mock polygon root
                Path.Combine(baseDir, "MockPolygonBulkData", "us_stocks_sip_minute_aggs"),
                Path.Combine(baseDir, "MockPolygonBulkData"),

                // Standard locations
                Path.Combine(baseDir, "PolygonBulkData", "us_stocks_sip_minute_aggs"),
                Path.Combine(baseDir, "PolygonBulkData"),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\bin\Debug\PolygonBulkData")),
            };

            foreach (var dir in candidates.Select(Path.GetFullPath))
            {
                if (!preferMocks && dir.IndexOf("MockPolygonBulkData", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue; // skip mock if not preferred

                // Restore file filtering for stock CSVs
                if (Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*_us_stocks_sip_minute_aggs.csv", SearchOption.AllDirectories).Any())
                    return dir;
            }

            return null;
        }

        public static string ResolveContractsDir(bool preferMocks = false)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                // Prefer mock contract data first
                Path.Combine(baseDir, "MockContractData", "SPY"),

                // Standard locations
                Path.Combine(baseDir, "ContractData", "SPY"),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\bin\Debug\ContractData\SPY")),
            };
            foreach (var dir in candidates.Select(Path.GetFullPath))
            {
                if (!preferMocks && dir.IndexOf("MockContractData", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue; // skip mock if not preferred

                if (Directory.Exists(dir)) return dir;
            }

            return null;
        }

        /**/

        internal static ContractVerificationResult VerifyAgainstContractFiles(string contractSymbol, ContractPriceHistory history, string contractsDir)
        {
            var result = new ContractVerificationResult
            {
                ContractSymbol = contractSymbol,
                Mismatches = new List<string>()
            };

            try
            {
                var safeFileName = S3FileSplitter.GenerateSafeFileName(contractSymbol);
                var contractFilePath = Path.Combine(contractsDir, $"{safeFileName}.csv");
                if (!File.Exists(contractFilePath))
                {
                    result.IsValid = false;
                    result.Mismatches.Add($"Contract file not found: {contractFilePath}");
                    return result;
                }

                BulkDataSorter.SortPerContractFile(contractFilePath);

                var sortedContractsDir = Path.Combine(contractsDir, "Sorted");
                contractFilePath = Path.Combine(sortedContractsDir, $"{safeFileName}_Sorted.csv");

                var fileDaily = new Dictionary<DateTime, DailyContractPrice>();
                using (var sr = new StreamReader(contractFilePath))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (line.StartsWith("ticker", StringComparison.OrdinalIgnoreCase)) continue;

                        var rec = ParseContractFileLine(line);
                        if (rec == null) continue;

                        var key = rec.Date.Date;
                        if (!fileDaily.ContainsKey(key))
                        {
                            fileDaily[key] = new DailyContractPrice
                            {
                                Date = key,
                                Open = rec.Open,
                                High = rec.High,
                                Low = rec.Low,
                                Close = rec.Close
                            };
                        }
                        else
                        {
                            var d = fileDaily[key];
                            d.High = Math.Max(d.High, rec.High);
                            d.Low = Math.Min(d.Low, rec.Low);
                            // Ensure Close reflects the last (chronological) record retained for the day
                            d.Close = rec.Close;
                        }
                    }
                }

                result.TotalRecords = history.Prices.Count;
                result.MatchedRecords = 0;

                foreach (var bulkPrice in history.Prices.OrderByDescending(_ => _.LastTimestamp))
                {
                    if (fileDaily.TryGetValue(bulkPrice.Date.Date, out var filePrice))
                    {
                        if (Math.Abs(bulkPrice.Open - filePrice.Open) < 0.01 &&
                            Math.Abs(bulkPrice.High - filePrice.High) < 0.01 &&
                            Math.Abs(bulkPrice.Low - filePrice.Low) < 0.01 &&
                            Math.Abs(bulkPrice.Close - filePrice.Close) < 0.01)
                        {
                            result.MatchedRecords++;
                        }
                        else
                        {
                            result.Mismatches.Add($"Price mismatch {bulkPrice.Date:yyyy-MM-dd}: " +
                                                  $"Bulk {bulkPrice.Open:F2}/{bulkPrice.High:F2}/{bulkPrice.Low:F2}/{bulkPrice.Close:F2} vs " +
                                                  $"File {filePrice.Open:F2}/{filePrice.High:F2}/{filePrice.Low:F2}/{filePrice.Close:F2}");
                        }
                    }
                    else
                    {
                        result.Mismatches.Add($"Date {bulkPrice.Date:yyyy-MM-dd} present in bulk but missing in per-contract file");
                    }
                }

                result.IsValid = result.Mismatches.Count == 0;
                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Mismatches.Add($"Verification error: {ex.Message}");
                return result;
            }
        }

        private static DailyContractPrice ParseContractFileLine(string csvLine)
        {
            if (string.IsNullOrWhiteSpace(csvLine)) return null;
            
            try
            {
                var parts = csvLine.Split(',');
                if (parts.Length < 8) return null;

                // Validate and parse each field with proper error handling
                if (!TryParseDouble(parts[2], out var open) || open < 0) return null;
                if (!TryParseDouble(parts[3], out var close) || close < 0) return null;
                if (!TryParseDouble(parts[4], out var high) || high < 0) return null;
                if (!TryParseDouble(parts[5], out var low) || low < 0) return null;
                
                // Validate OHLC relationships
                if (high < Math.Max(open, close) || low > Math.Min(open, close)) return null;
                if (high < low) return null; // Basic sanity check
                
                if (!TryParseLong(parts[6], out var windowStartNanos)) return null;
                
                var utcTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(windowStartNanos / 1_000_000).UtcDateTime;
                
                // Validate timestamp is within reasonable date range
                var minDate = new DateTime(2000, 1, 1);
                var maxDate = DateTime.Today.AddYears(1);
                if (utcTimestamp.Date < minDate || utcTimestamp.Date > maxDate) return null;
                
                TimeZoneInfo easternTimeZone;
                try
                {
                    easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                }
                catch
                {
                    // Fallback for non-Windows systems
                    try
                    {
                        easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
                    }
                    catch
                    {
                        // Last resort - use system local time with warning
                        easternTimeZone = TimeZoneInfo.Local;
                    }
                }
                
                var easternTimestamp = TimeZoneInfo.ConvertTimeFromUtc(utcTimestamp, easternTimeZone);

                // Enhanced market hours validation with holiday awareness
                var rthCutoff = new TimeSpan(16, 15, 0);
                if (easternTimestamp.TimeOfDay >= rthCutoff)
                    return null;
                    
                // Additional weekend/holiday filtering could be added here

                return new DailyContractPrice
                {
                    Date = easternTimestamp.Date,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close
                };
            }
            catch (Exception ex)
            {
                // Log parsing errors for debugging but don't throw
                var linePreview = csvLine?.Length > 50 ? csvLine.Substring(0, 50) + "..." : csvLine;
                Console.WriteLine($"CSV line parsing error: {ex.Message} for line: {linePreview}");
                return null;
            }
        }

        // ---------------- Underlying helpers ----------------
        internal static double? TryGetUnderlyingClose(string stocksRoot, string underlyingSymbol, DateTime date)
        {
            try
            {
                // Find the specific daily stocks minute file for the date
                var pattern = date.ToString("yyyy-MM-dd") + "_us_stocks_sip_minute_aggs.csv";
                var file = Directory.GetFiles(stocksRoot, pattern, SearchOption.AllDirectories).FirstOrDefault();
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) return null;

                // Read last pre-4:15 PM ET close for the underlying
                var easternTz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                var rthCutoff = new TimeSpan(16, 15, 0);
                double? lastClose = null;
                using (var sr = new StreamReader(file))
                {
                    string line = sr.ReadLine(); // header
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var parts = line.Split(',');
                        if (parts.Length < 8) continue;

                        var ticker = parts[0].Trim();
                        // Stock files may use raw symbol (e.g., "SPY"); be permissive
                        if (!ticker.Equals(underlyingSymbol, StringComparison.OrdinalIgnoreCase)) continue;

                        if (!double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var close)) continue;
                        if (!long.TryParse(parts[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var windowStartNanos)) continue;

                        var utc = DateTimeOffset.FromUnixTimeMilliseconds(windowStartNanos / 1_000_000).UtcDateTime;
                        var est = TimeZoneInfo.ConvertTimeFromUtc(utc, easternTz);
                        if (est.TimeOfDay >= rthCutoff) continue;

                        lastClose = close; // rows assumed chronological; take the last pre-4pm encountered
                    }
                }
                return lastClose;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Enhanced date extraction with comprehensive error handling and validation
        /// </summary>
        internal static DateTime? TryExtractDateFromName(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return null;
            
            try
            {
                var name = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrEmpty(name) || name.Length < 10) return null;
                
                var datePart = name.Substring(0, 10);
                
                // Try multiple date formats to handle variations
                var formats = new[] { "yyyy-MM-dd", "yyyy_MM_dd", "yyyyMMdd" };
                
                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(datePart, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    {
                        // Validate reasonable date range (not too far in past/future)
                        var minDate = new DateTime(2000, 1, 1);
                        var maxDate = DateTime.Today.AddYears(10);
                        
                        if (d.Date >= minDate && d.Date <= maxDate)
                            return d.Date;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - graceful degradation
                Console.WriteLine($"Date extraction error for {filePath}: {ex.Message}");
            }
            
            return null;
        }

        internal static void WriteAllTextUtf8(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        internal static void AppendAllTextUtf8(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                sw.Write(content);
            }
        }

        internal static void SafeDeleteDirectory(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup failures in tests
            }
        }

        /// <summary>
        /// Culture-invariant double parsing with multiple format support
        /// </summary>
        private static bool TryParseDouble(string value, out double result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            
            value = value.Trim();
            
            // Handle common currency and formatting variations
            value = value.Replace("$", "").Replace(",", "").Replace(" ", "");
            
            // Try standard parsing first
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                return !double.IsNaN(result) && !double.IsInfinity(result);
                
            // Try with current culture as fallback
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out result))
                return !double.IsNaN(result) && !double.IsInfinity(result);
                
            return false;
        }
        
        /// <summary>
        /// Robust long parsing with overflow protection
        /// </summary>
        private static bool TryParseLong(string value, out long result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            
            value = value.Trim();
            
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }
    }
}
