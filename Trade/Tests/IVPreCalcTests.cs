using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trade;
using Trade.Interfaces;
using Trade.IVPreCalc2;
using Trade.Polygon2;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Comprehensive test suite for OptionsIVPreparer functionality.
    /// Tests data preparation, implied volatility calculations, and surface generation
    /// for options trading analysis and backtesting scenarios.
    /// Walks backward across Polygon S3 bulk option files, reconstructs per-contract daily OHLC
    /// from minute aggregates without duplicate scanning, and verifies against split contract CSVs.
    /// </summary>
    [TestClass]
    public class IVPreCalcTests
    {
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Maximum number of bulk files to analyze for contract alignment validation.
        /// Limits test execution time while providing meaningful validation coverage.
        /// </summary>
        private const int MAX_BULK_FILES_TO_ANALYZE = 5;

        [TestMethod]
        [TestCategory("IVWalkback")]
        public async Task SortBulkFilesForStock(int maximumNumberOfBulkFiles = int.MaxValue)
        {
            BulkDataSorter.SortBulkFiles("PolygonBulkData\\us_stocks_sip_minute_aggs",
                "*us_stocks_sip_minute_aggs*.csv", 0, maximumNumberOfBulkFiles);

            await Task.Delay(1);
        }

        [TestMethod]
        [TestCategory("IVWalkback")]
        public async Task SortBulkFilesForOptions(int maximumNumberOfBulkFiles = int.MaxValue)
        {
            BulkDataSorter.SortBulkFiles("PolygonBulkData\\us_options_opra_minute_aggs",
                "*us_options_opra_minute_aggs*.csv", 0, maximumNumberOfBulkFiles);

            await Task.Delay(1);
        }

        [TestMethod][TestCategory("IVWalkback")]
        public async Task Prepare()
        {
            // Phase 0: Run controlled mock scenarios first, then parse real files
            //await RunMockScenariosAsync().ConfigureAwait(false);

            var prices = new Prices();
            var polygon = new Polygon2.Polygon(prices, "SPY", 10, 10);

            if (true)
            {
                // Step 1.) For learning about the S3 bulk files and the Contracts splitting...
                var result = polygon.FetchStockAndOptionsDataAsync("SPY", DateTime.Now.AddDays(-30).Date, DateTime.Now.Date,
                true, true, false, false, false, 10, 10).Result;

                // Step 2.) Sort and save...
                BulkDataSorter.SortBulkFiles("PolygonBulkData\\us_options_opra_minute_aggs");

                // Sort a single per-contract file
                //BulkDataSorter.SortPerContractFile(@"ContractData\SPY\SPY240119C00500000.csv");

                // Sort all per-contract files in the ContractData directory
                //BulkDataSorter.SortAllPerContractFiles(@"ContractData");

                // Step 3.) Verify the ordering/sorting is correct...
                new BulkFileContractOrderingTests().AllBulkFiles_Contracts_Are_Sorted_With_Progress();
            }

            var ivPreCalc = new IVPreCalc();

            await ivPreCalc.Prepare(true, false, false);
        }

        [TestMethod][TestCategory("Core")]
        public async Task ValidateContractAlignmentAndParsingInBulkFiles()
        {
            // First, get the bulk directory
            var bulkDir = IVPreCalc.ResolveBulkDir();
            if (string.IsNullOrEmpty(bulkDir) || !Directory.Exists(bulkDir))
            {
                Assert.Inconclusive("Bulk directory not found for contract alignment verification.");
                return;
            }

            // Get the first N bulk files, newest to oldest (configurable via constant)
            var bulkFiles = Directory.GetFiles(bulkDir, "*.csv", SearchOption.AllDirectories)
                .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(f => new { Path = f, Date = IVPreCalc.TryExtractDateFromName(f) })
                .Where(x => x.Date.HasValue)
                .OrderByDescending(x => x.Date.Value)
                .Take(MAX_BULK_FILES_TO_ANALYZE)
                .ToList();

            if (bulkFiles.Count < 3)
            {
                Assert.Inconclusive($"Need at least 3 bulk files for alignment test. Found: {bulkFiles.Count}. Configured to analyze up to {MAX_BULK_FILES_TO_ANALYZE} files.");
                return;
            }

            Console.WriteLine("=== BULK FILE CONTRACT ALIGNMENT & PARSING ANALYSIS ===");
            Console.WriteLine($"Analyzing the first {bulkFiles.Count} bulk files (newest to oldest) out of max {MAX_BULK_FILES_TO_ANALYZE}:");
            Console.WriteLine("Validation includes: contract ordering, parsing issues, data quality checks");
            
            // Log to test context as well if available
            if (TestContext != null)
            {
                TestContext.WriteLine("=== BULK FILE CONTRACT ALIGNMENT & PARSING ANALYSIS ===");
                TestContext.WriteLine($"Analyzing the first {bulkFiles.Count} bulk files (newest to oldest) out of max {MAX_BULK_FILES_TO_ANALYZE}:");
                TestContext.WriteLine("Validation includes: contract ordering, parsing issues, data quality checks");
            }
            
            var fileAnalysis = new List<ContractFileAnalysis>();
            var parsingIssues = new List<string>();
            
            for (int fileIndex = 0; fileIndex < bulkFiles.Count; fileIndex++)
            {
                var bulkFile = bulkFiles[fileIndex];
                var message = $"File {fileIndex + 1}: {Path.GetFileName(bulkFile.Path)} (Date: {bulkFile.Date:yyyy-MM-dd})";
                Console.WriteLine($"\n{message}");
                if (TestContext != null) TestContext.WriteLine(message);
                
                var analysis = await AnalyzeBulkFileContractOrderWithValidation(bulkFile.Path, fileIndex + 1);
                fileAnalysis.Add(analysis);
                
                // Collect any parsing issues found during analysis
                if (analysis.ParsingIssues != null && analysis.ParsingIssues.Count > 0)
                {
                    parsingIssues.AddRange(analysis.ParsingIssues.Select(issue => $"File {fileIndex + 1}: {issue}"));
                }
                
                var statsMessage = $"  Total Contracts: {analysis.TotalContracts:N0}, Sample Size: {Math.Min(100, analysis.TotalContracts):N0}, Out-of-Order: {analysis.OutOfOrderTransitions}";
                Console.WriteLine(statsMessage);
                if (TestContext != null) TestContext.WriteLine(statsMessage);
                
                if (analysis.OutOfOrderTransitions > 0)
                {
                    var warningMessage = "  ⚠️  OUT-OF-ORDER CONTRACTS DETECTED!";
                    Console.WriteLine(warningMessage);
                    if (TestContext != null) TestContext.WriteLine(warningMessage);
                    
                    foreach (var transition in analysis.OrderViolations.Take(5))
                    {
                        var violationMessage = $"    Line {transition.LineNumber}: '{transition.Previous}' -> '{transition.Current}'";
                        Console.WriteLine(violationMessage);
                        if (TestContext != null) TestContext.WriteLine(violationMessage);
                    }
                    if (analysis.OrderViolations.Count > 5)
                    {
                        var moreMessage = $"    ... and {analysis.OrderViolations.Count - 5} more violations";
                        Console.WriteLine(moreMessage);
                        if (TestContext != null) TestContext.WriteLine(moreMessage);
                    }
                }
                else
                {
                    var successMessage = "  ✅ All contracts are in lexicographic order";
                    Console.WriteLine(successMessage);
                    if (TestContext != null) TestContext.WriteLine(successMessage);
                }

                // Report parsing issues if any were found
                if (analysis.ParsingIssues != null && analysis.ParsingIssues.Count > 0)
                {
                    var parsingMessage = $"  ⚠️  Parsing issues detected: {analysis.ParsingIssues.Count}";
                    Console.WriteLine(parsingMessage);
                    if (TestContext != null) TestContext.WriteLine(parsingMessage);
                    
                    foreach (var issue in analysis.ParsingIssues.Take(3))
                    {
                        var issueMessage = $"    - {issue}";
                        Console.WriteLine(issueMessage);
                        if (TestContext != null) TestContext.WriteLine(issueMessage);
                    }
                    if (analysis.ParsingIssues.Count > 3)
                    {
                        var moreIssuesMessage = $"    ... and {analysis.ParsingIssues.Count - 3} more parsing issues";
                        Console.WriteLine(moreIssuesMessage);
                        if (TestContext != null) TestContext.WriteLine(moreIssuesMessage);
                    }
                }
                else
                {
                    var cleanMessage = "  ✅ No parsing issues detected";
                    Console.WriteLine(cleanMessage);
                    if (TestContext != null) TestContext.WriteLine(cleanMessage);
                }
            }

            // Cross-file consistency analysis
            Console.WriteLine("\n=== CROSS-FILE CONTRACT CONSISTENCY ===");
            if (TestContext != null) TestContext.WriteLine("=== CROSS-FILE CONTRACT CONSISTENCY ===");
            
            await AnalyzeCrossFileContractConsistency(fileAnalysis);
            
            // Report summary of parsing issues across all files
            if (parsingIssues.Count > 0)
            {
                Console.WriteLine($"\n=== PARSING ISSUES SUMMARY ===");
                Console.WriteLine($"Total parsing issues found across {bulkFiles.Count} files: {parsingIssues.Count}");
                if (TestContext != null) 
                {
                    TestContext.WriteLine("=== PARSING ISSUES SUMMARY ===");
                    TestContext.WriteLine($"Total parsing issues found across {bulkFiles.Count} files: {parsingIssues.Count}");
                }
                
                // Show a few examples
                foreach (var issue in parsingIssues.Take(5))
                {
                    Console.WriteLine($"  - {issue}");
                    if (TestContext != null) TestContext.WriteLine($"  - {issue}");
                }
                if (parsingIssues.Count > 5)
                {
                    var moreMessage = $"  ... and {parsingIssues.Count - 5} more issues (check individual file reports above)";
                    Console.WriteLine(moreMessage);
                    if (TestContext != null) TestContext.WriteLine(moreMessage);
                }
            }
            else
            {
                Console.WriteLine("\n=== PARSING VALIDATION COMPLETE ===");
                Console.WriteLine($"✅ No parsing issues detected across {bulkFiles.Count} analyzed files");
                if (TestContext != null)
                {
                    TestContext.WriteLine("=== PARSING VALIDATION COMPLETE ===");
                    TestContext.WriteLine($"✅ No parsing issues detected across {bulkFiles.Count} analyzed files");
                }
            }
            
            // Fail test if significant ordering issues are found
            var totalViolations = fileAnalysis.Sum(f => f.OutOfOrderTransitions);
            if (totalViolations > 0)
            {
                var failureMessage = $"Found {totalViolations} out-of-order contract transitions across {fileAnalysis.Count} files. " +
                           "This indicates the bulk files may not be properly sorted, which could cause search optimization issues.";
                
                if (TestContext != null) TestContext.WriteLine($"FAILURE: {failureMessage}");
                Assert.Fail(failureMessage);
            }
            else
            {
                var successMessage = $"✅ All {fileAnalysis.Count} bulk files have properly ordered contracts and passed parsing validation";
                Console.WriteLine(successMessage);
                if (TestContext != null) TestContext.WriteLine(successMessage);
            }
            
            // Note: Parsing issues are reported but don't fail the test - they're informational
            // for identifying potential data quality issues that might need investigation
        }

        /// <summary>
        /// Enhanced version that includes parsing validation checks for common edge cases and data quality issues.
        /// Analyzes the contract ordering within a single bulk file and checks for proper ContractKey ordering violations among option contracts.
        /// </summary>
        /// <param name="filePath">Path to the bulk file to analyze</param>
        /// <param name="fileNumber">Sequential number of this file in the analysis</param>
        /// <returns>Analysis results including ordering violations and parsing issues</returns>
        private async Task<ContractFileAnalysis> AnalyzeBulkFileContractOrderWithValidation(string filePath, int fileNumber)
        {
            var analysis = new ContractFileAnalysis
            {
                FilePath = filePath,
                FileNumber = fileNumber,
                OrderViolations = new List<ContractOrderViolation>(),
                ParsingIssues = new List<string>()
            };

            try
            {
                using (var sr = new StreamReader(filePath))
                {
                    // Skip header and validate it
                    var header = await sr.ReadLineAsync();
                    analysis.Header = header;

                    if (string.IsNullOrWhiteSpace(header))
                    {
                        analysis.ParsingIssues.Add("Missing or empty header line");
                    }
                    else if (!header.ToLowerInvariant().Contains("ticker") || !header.ToLowerInvariant().Contains("window_start"))
                    {
                        analysis.ParsingIssues.Add($"Unexpected header format: {header.Substring(0, Math.Min(50, header.Length))}...");
                    }

                    ContractKey previousKey = null;
                    string previousContract = null;
                    int lineNumber = 1; // Starting from 1 since we skipped header
                    int contractsSeen = 0;
                    var contractSet = new HashSet<string>();
                    int malformedLines = 0;
                    int emptyLines = 0;
                    int nonOptionLines = 0;

                    string line;
                    while ((line = await sr.ReadLineAsync()) != null && contractsSeen < 100) // Analyze first 100 contracts per file
                    {
                        lineNumber++;

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            emptyLines++;
                            continue;
                        }

                        var comma = line.IndexOf(",");
                        if (comma <= 0)
                        {
                            malformedLines++;
                            if (malformedLines <= 3) // Only collect first few examples
                            {
                                var linePreview = line.Length > 30 ? line.Substring(0, 30) + "..." : line;
                                analysis.ParsingIssues.Add($"Line {lineNumber}: No comma delimiter found in '{linePreview}'");
                            }
                            continue;
                        }

                        var currentContract = line.Substring(0, comma).Trim().ToUpperInvariant();
                        if (!currentContract.StartsWith("O:"))
                        {
                            nonOptionLines++;
                            continue;
                        }

                        // Additional validation checks for the contract symbol
                        if (currentContract.Length < 15) // Minimum realistic option symbol length
                        {
                            analysis.ParsingIssues.Add($"Line {lineNumber}: Unusually short option symbol '{currentContract}'");
                        }

                        // Basic validation of the CSV line structure
                        var parts = line.Split(',');
                        if (parts.Length < 8)
                        {
                            analysis.ParsingIssues.Add($"Line {lineNumber}: Insufficient columns ({parts.Length} < 8) for contract '{currentContract}'");
                            continue;
                        }

                        // Check for obviously invalid price data (optional - just for first few)
                        if (contractsSeen < 10 && parts.Length >= 6)
                        {
                            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var open) || open <= 0)
                            {
                                analysis.ParsingIssues.Add($"Line {lineNumber}: Invalid open price '{parts[2]}' for contract '{currentContract}'");
                            }
                            if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var high) || high <= 0)
                            {
                                analysis.ParsingIssues.Add($"Line {lineNumber}: Invalid high price '{parts[4]}' for contract '{currentContract}'");
                            }
                        }

                        // Track unique contracts
                        if (contractSet.Add(currentContract))
                        {
                            contractsSeen++;

                            // Parse current contract using the same logic as BulkDataSorter
                            var currentKey = ParseContractKey(currentContract);

                            // Check ordering using ContractKeyComparer instead of simple string comparison
                            if (previousKey != null)
                            {
                                var comparison = ContractKeyComparer.Instance.Compare(currentKey, previousKey);
                                if (comparison < 0) // Current < Previous = out of order
                                {
                                    analysis.OutOfOrderTransitions++;
                                    analysis.OrderViolations.Add(new ContractOrderViolation
                                    {
                                        LineNumber = lineNumber,
                                        Previous = previousContract,
                                        Current = currentContract,
                                        ComparisonResult = comparison
                                    });
                                }
                            }

                            previousKey = currentKey;
                            previousContract = currentContract;
                        }
                    }

                    analysis.TotalContracts = contractSet.Count;
                    analysis.ContractsSampled = contractSet.ToList();

                    // Add summary parsing statistics
                    if (emptyLines > 0)
                        analysis.ParsingIssues.Add($"Found {emptyLines} empty lines");
                    if (malformedLines > 3) // Only report if more than our sample size
                        analysis.ParsingIssues.Add($"Found {malformedLines} malformed lines (missing comma delimiter)");
                    if (nonOptionLines > 0)
                        analysis.ParsingIssues.Add($"Found {nonOptionLines} non-option lines (not starting with 'O:')");
                }
            }
            catch (Exception ex)
            {
                analysis.ParsingIssues.Add($"File reading error: {ex.Message}");
            }

            return analysis;
        }

        /// <summary>
        /// Parse contract key using the same logic as BulkDataSorter to ensure consistent ordering validation.
        /// Copied from BulkDataSorter.ParseContractKey to maintain consistency.
        /// </summary>
        /// <param name="raw">Raw option ticker symbol</param>
        /// <returns>Parsed ContractKey for comparison</returns>
        private static ContractKey ParseContractKey(string raw)
        {
            var key = new ContractKey
            {
                RawTicker = raw ?? string.Empty,
                Underlying = raw ?? string.Empty,
                IsCall = true,
                Expiration = DateTime.MaxValue,
                Strike = double.MaxValue
            };

            if (string.IsNullOrWhiteSpace(raw)) return key;

            var s = raw.ToUpperInvariant();
            if (s.StartsWith("O:")) s = s.Substring(2);

            // Look for YYMMDD + C/P + 8-digit strike pattern
            for (int i = 0; i <= s.Length - 6 - 1 - 8; i++)
            {
                if (!char.IsDigit(s[i]) || !char.IsDigit(s[i + 5])) continue;

                // Check for 6 consecutive digits (YYMMDD)
                bool sixDigits = true;
                for (int d = 0; d < 6; d++)
                    if (!char.IsDigit(s[i + d])) { sixDigits = false; break; }
                if (!sixDigits) continue;

                int posAfterDate = i + 6;
                if (posAfterDate >= s.Length) break;
                char cp = s[posAfterDate];
                if (cp != 'C' && cp != 'P') continue;

                int strikeStart = posAfterDate + 1;
                if (strikeStart + 8 > s.Length) continue;

                // Check for 8 consecutive digits (strike price)
                bool strikeDigits = true;
                for (int d = 0; d < 8; d++)
                    if (!char.IsDigit(s[strikeStart + d])) { strikeDigits = false; break; }
                if (!strikeDigits) continue;

                try
                {
                    string underlying = s.Substring(0, i);
                    string yymmdd = s.Substring(i, 6);
                    string strikeRaw = s.Substring(strikeStart, 8);

                    int yy = int.Parse(yymmdd.Substring(0, 2), CultureInfo.InvariantCulture);
                    int mm = int.Parse(yymmdd.Substring(2, 2), CultureInfo.InvariantCulture);
                    int dd = int.Parse(yymmdd.Substring(4, 2), CultureInfo.InvariantCulture);
                    int year = yy + (yy >= 70 ? 1900 : 2000);
                    var expiration = new DateTime(year, mm, dd);
                    double strike = int.Parse(strikeRaw, CultureInfo.InvariantCulture) / 1000.0;

                    key.Underlying = underlying;
                    key.IsCall = cp == 'C';
                    key.Expiration = expiration;
                    key.Strike = strike;
                }
                catch { }
                return key;
            }
            return key;
        }

        /// <summary>
        /// Analyzes consistency of contract positions across multiple bulk files.
        /// This helps identify if contracts appear in significantly different positions,
        /// which could indicate sorting or data consistency issues.
        /// </summary>
        /// <param name="fileAnalyses">List of file analyses to compare</param>
        private Task AnalyzeCrossFileContractConsistency(List<ContractFileAnalysis> fileAnalyses)
        {
            var consistencyMessage = "Checking for contracts that appear in different positions across files...";
            Console.WriteLine(consistencyMessage);
            if (TestContext != null) TestContext.WriteLine(consistencyMessage);
            
            // Build contract position maps for each file
            var fileContractPositions = new List<Dictionary<string, int>>();
            
            for (int i = 0; i < fileAnalyses.Count; i++)
            {
                var positions = new Dictionary<string, int>();
                for (int j = 0; j < fileAnalyses[i].ContractsSampled.Count; j++)
                {
                    positions[fileAnalyses[i].ContractsSampled[j]] = j;
                }
                fileContractPositions.Add(positions);
            }
            
            // Find common contracts and check position consistency
            var allContracts = fileAnalyses[0].ContractsSampled.ToHashSet();
            for (int i = 1; i < fileAnalyses.Count; i++)
            {
                allContracts.IntersectWith(fileAnalyses[i].ContractsSampled);
            }
            
            var commonMessage = $"Common contracts across all {fileAnalyses.Count} files: {allContracts.Count}";
            Console.WriteLine(commonMessage);
            if (TestContext != null) TestContext.WriteLine(commonMessage);
            
            var positionInconsistencies = new List<string>();
            
            foreach (var contract in allContracts.Take(20)) // Check first 20 common contracts
            {
                // Use TryGetValue instead of GetValueOrDefault for .NET Framework compatibility
                var positions = fileContractPositions.Select(fp => 
                {
                    int position;
                    return fp.TryGetValue(contract, out position) ? position : -1;
                }).ToList();
                
                var maxDiff = positions.Max() - positions.Min();
                
                if (maxDiff > 5) // Allow some variance but flag large position shifts
                {
                    positionInconsistencies.Add($"  {contract}: positions {string.Join(", ", positions)} (range: {maxDiff})");
                }
            }
            
            if (positionInconsistencies.Any())
            {
                var warningMessage = $"⚠️  Position inconsistencies found ({positionInconsistencies.Count}):";
                Console.WriteLine(warningMessage);
                if (TestContext != null) TestContext.WriteLine(warningMessage);
                
                foreach (var inconsistency in positionInconsistencies)
                {
                    Console.WriteLine(inconsistency);
                    if (TestContext != null) TestContext.WriteLine(inconsistency);
                }
            }
            else
            {
                var successMessage = "✅ Contract positions are consistent across files";
                Console.WriteLine(successMessage);
                if (TestContext != null) TestContext.WriteLine(successMessage);
            }

            return Task.CompletedTask;
        }
        
        // ---------------- Mock scenarios ----------------
        private async Task RunMockScenariosAsync()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var mockBulkRoot = Path.Combine(baseDir, "MockPolygonBulkData");
            var mockContractsRoot = Path.Combine(baseDir, "MockContractData", "SPY");

            // Use valid option symbol format: SYMBOL + YYMMDD + C/P + 8-digit strike price
            // Create options expiring 30 days from today
            var exp = DateTime.Today.AddDays(30);
            var expString = exp.ToString("yyMMdd");
            var symbols = new[] 
            { 
                $"O:SPY{expString}C00450000",  // SPY call $450.00
                $"O:AAPL{expString}P00150000", // AAPL put $150.00  
                $"O:QQQ{expString}C00350000"   // QQQ call $350.00
            };

            var today = DateTime.Today;
            var dates = new[] { today.AddDays(-2), today.AddDays(-1), today };

            try
            {
                CreateBasicThreeSymbolMockDataSet(mockBulkRoot, mockContractsRoot, dates, symbols);

                // Use mocks
                var bulkDir = IVPreCalc.ResolveBulkDir(preferMocks: true);
                var contractsDir = IVPreCalc.ResolveContractsDir(preferMocks: true);

                Assert.IsFalse(string.IsNullOrEmpty(bulkDir) && string.IsNullOrEmpty(contractsDir), "Mock dirs not created");

                using (var tracer = new BulkFileContractTracer(bulkDir))
                {
                    // Build symbols from newest mock file
                    var newestOptionsFile = Directory.GetFiles(bulkDir, "*.csv", SearchOption.AllDirectories)
                        .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Select(f => new { Path = f, Date = IVPreCalc.TryExtractDateFromName(f) })
                        .Where(x => x.Date.HasValue)
                        .OrderByDescending(x => x.Date.Value)
                        .Select(x => x.Path)
                        .FirstOrDefault();

                    Assert.IsTrue(!string.IsNullOrEmpty(newestOptionsFile) && File.Exists(newestOptionsFile), "No mock options bulk CSV found.");

                    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (var sr = new StreamReader(newestOptionsFile))
                    {
                        string preLine = sr.ReadLine();
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            var comma = line.IndexOf(',');
                            if (comma <= 0) continue;
                            var ticker = line.Substring(0, comma).Trim();
                            if (ticker.StartsWith("O:", StringComparison.OrdinalIgnoreCase))
                                set.Add(ticker.ToUpperInvariant());
                        }
                    }
                    var contractSymbols = set.ToList();

                    var endDate = dates.Max().Date;
                    var startDate = dates.Min().Date;

                    int totalCompared = 0;
                    int totalMatched = 0;
                    var mismatches = new List<string>();

                    foreach (var cs in contractSymbols)
                    {
                        // Verify that each symbol can be parsed by Ticker.ParseToOption
                        var ticker = Ticker.ParseToOption(cs);
                        Assert.IsTrue(ticker.IsOption, $"Mock symbol {cs} should be parseable as an option");
                        Assert.IsNotNull(ticker.UnderlyingSymbol, $"Mock symbol {cs} should have underlying");
                        Assert.IsTrue(ticker.ExpirationDate.HasValue, $"Mock symbol {cs} should have expiration");
                        Assert.IsTrue(ticker.StrikePrice.HasValue, $"Mock symbol {cs} should have strike");
                        Assert.IsTrue(ticker.OptionType.HasValue, $"Mock symbol {cs} should have option type");

                        var history = await tracer.TraceContractBackwardsAsync(cs, startDate, endDate).ConfigureAwait(false);
                        Assert.IsNotNull(history);
                        Assert.IsTrue(history.Prices != null && history.Prices.Count == dates.Length, "Unexpected mock price count");

                        var verification = IVPreCalc.VerifyAgainstContractFiles(cs, history, contractsDir);
                        totalCompared += verification.TotalRecords;
                        totalMatched += verification.MatchedRecords;
                        if (!verification.IsValid)
                            mismatches.AddRange(verification.Mismatches);
                    }

                    Assert.AreEqual(totalCompared, totalMatched, "Mock verification must match exactly.");
                    Assert.AreEqual(0, mismatches.Count, string.Join(" | ", mismatches));
                }
            }
            finally
            {
                // Cleanup mock files regardless of pass/fail so next run starts clean
                IVPreCalc.SafeDeleteDirectory(mockBulkRoot);
                IVPreCalc.SafeDeleteDirectory(Path.Combine(baseDir, "MockContractData"));
            }
        }

        /// <summary>
        /// Creates a basic test dataset with 3 option symbols across 3 consecutive dates.
        /// Each symbol gets 3 minute-level price records per day (09:31, 12:00, 15:59 ET).
        /// Generates both bulk CSV files and corresponding per-contract CSV files.
        /// </summary>
        /// <param name="mockBulkRoot">Root directory for bulk option minute data files</param>
        /// <param name="mockContractsRoot">Root directory for per-contract CSV files</param>
        /// <param name="dates">Array of dates to generate data for</param>
        /// <param name="symbols">Array of option symbols in O:SYMBOLYYMMDDCP00000000 format</param>
        private static void CreateBasicThreeSymbolMockDataSet(string mockBulkRoot, string mockContractsRoot, DateTime[] dates, string[] symbols)
        {
            // Clean and recreate directories to ensure fresh test data
            SafeDeleteDirectoryContents(mockBulkRoot);
            SafeDeleteDirectoryContents(mockContractsRoot);
            Directory.CreateDirectory(mockBulkRoot);
            Directory.CreateDirectory(mockContractsRoot);

            // Build per-contract file writers
            var contractPaths = symbols.ToDictionary(
                s => s,
                s => Path.Combine(mockContractsRoot, S3FileSplitter.GenerateSafeFileName(s) + ".csv"));

            foreach (var kv in contractPaths)
            {
                IVPreCalc.WriteAllTextUtf8(kv.Value, "ticker,volume,open,close,high,low,window_start,transactions\n");
            }

            foreach (var date in dates)
            {
                var bulkFile = Path.Combine(mockBulkRoot, $"{date:yyyy-MM-dd}_us_options_opra_minute_aggs.csv");
                var header = "ticker,volume,open,close,high,low,window_start,transactions\n";
                var sb = new StringBuilder();
                sb.Append(header);

                // 3 prints per symbol at 09:31, 12:00, 15:59 ET
                var easternTz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                var times = new[] { new TimeSpan(9, 31, 0), new TimeSpan(12, 0, 0), new TimeSpan(15, 59, 0) };

                foreach (var symbol in symbols.OrderBy(s => s, StringComparer.Ordinal))
                {
                    // Construct prices so that open=100+idx, highs increment, lows decrement, close from last print
                    double basePx = 100 + Array.IndexOf(symbols, symbol) * 10;
                    double[] opens = { basePx, basePx + 0.5, basePx + 1.0 };
                    double[] highs = { basePx + 1.5, basePx + 2.0, basePx + 2.5 };
                    double[] lows = { basePx - 1.0, basePx - 0.5, basePx - 0.25 };
                    double[] closes = { basePx + 0.2, basePx + 0.8, basePx + 1.4 };
                    int[] vols = { 10, 20, 30 };
                    int[] txs = { 1, 2, 3 };

                    for (int i = 0; i < 3; i++)
                    {
                        var est = date.Date + times[i];
                        // Use est components directly; est is a DateTime
                        var estDt = new DateTime(est.Year, est.Month, est.Day, est.Hour, est.Minute, est.Second, DateTimeKind.Unspecified);
                        var utc = TimeZoneInfo.ConvertTimeToUtc(estDt, easternTz);
                        long nanos = (long)(new DateTimeOffset(utc)).ToUnixTimeMilliseconds() * 1_000_000;

                        var line = string.Format(CultureInfo.InvariantCulture,
                            "{0},{1},{2:F2},{3:F2},{4:F2},{5:F2},{6},{7}\n",
                            symbol, vols[i], opens[i], closes[i], highs[i], lows[i], nanos, txs[i]);

                        sb.Append(line);

                        // Append to per-contract file as well
                        IVPreCalc.AppendAllTextUtf8(contractPaths[symbol], line);
                    }
                }

                IVPreCalc.WriteAllTextUtf8(bulkFile, sb.ToString());
            }
        }

        private static void SafeDeleteDirectoryContents(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    // Delete all files in the directory
                    foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }

                    // Delete all subdirectories
                    foreach (var subDir in Directory.GetDirectories(dir))
                    {
                        Directory.Delete(subDir, recursive: true);
                    }
                }
            }
            catch
            {
                // ignore cleanup failures in tests
            }
        }
    }
}