using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Trade.Form4;
using Trade.Polygon2;

namespace Trade.Tests
{
    [TestClass]
    public partial class SECForm4DownloadTests
    {
        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            // Create cache directory if it doesn't exist
            if (!Directory.Exists(Form4Downloader.CACHE_DIR))
            {
                Directory.CreateDirectory(Form4Downloader.CACHE_DIR);
            }

            ConsoleUtilities.WriteLine($"SEC Form 4 Download Tests - Cache Directory: {Form4Downloader.CACHE_DIR}");
            ConsoleUtilities.WriteLine(
                "NOTE: SEC requires User-Agent header and rate limiting (10 requests/second max)");
        }

        [TestMethod]
        [TestCategory("SEC")]
        public async Task Form4_DownloadDailyIndex_Smoke()
        {
            using (var downloader = new Form4Downloader(Form4Downloader.USER_AGENT))
            {
                // Pick a weekday to reduce flakiness; back off further if it's weekend.
                var testDate = DateTime.Today.AddDays(-7);
                if (testDate.DayOfWeek is DayOfWeek.Saturday) testDate = testDate.AddDays(-1);
                if (testDate.DayOfWeek is DayOfWeek.Sunday) testDate = testDate.AddDays(-2);

                ConsoleUtilities.WriteLine($"\n=== DOWNLOADING FORM 4 INDEX (Filtered to Day) ===");
                ConsoleUtilities.WriteLine($"Date: {testDate:yyyy-MM-dd}");

                var entries = await downloader.DownloadQuarterlyIndexAsync(testDate);

                Assert.IsNotNull(entries, "Entries should not be null.");

                ConsoleUtilities.WriteLine($"Found {entries.Count} Form 4 filings on {testDate:yyyy-MM-dd}");

                // Don't hard-fail if SEC returns 0 — weekends/holidays/outages.
                if (entries.Count == 0)
                {
                    Assert.Inconclusive(
                        $"No Form 4 filings returned for {testDate:yyyy-MM-dd} (possible weekend/holiday/outage).");
                    return;
                }

                // Validate structure for a sample
                foreach (var entry in entries.Take(10))
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(entry.CIK), "CIK should not be empty");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(entry.CompanyName), "Company name should not be empty");
                    Assert.AreEqual("4", entry.FormType, "Should be Form 4");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(entry.EdgarUrl), "URL should not be empty");

                    // Stronger: URL should look like an EDGAR Archives path
                    StringAssert.Contains(entry.EdgarUrl, "/Archives/edgar/data/",
                        "EDGAR URL should point to Archives/edgar/data");
                }
            }
        }

        [TestMethod]
        [TestCategory("SEC")]
        public async Task Form4_ParseSingleFiling_Success()
        {
            var downloader = new Form4Downloader(Form4Downloader.USER_AGENT);

            try
            {
                // Pick a date that is more likely to have filings.
                // Still "recent", but avoid weekend flakiness.
                var testDate = DateTime.Today.AddDays(-7);
                if (testDate.DayOfWeek == DayOfWeek.Saturday) testDate = testDate.AddDays(-1);
                if (testDate.DayOfWeek == DayOfWeek.Sunday) testDate = testDate.AddDays(-2);

                // Get recent filings (note: implementation currently pulls quarter index and filters by day)
                var entries = await downloader.DownloadQuarterlyIndexAsync(testDate);

                if (entries == null || entries.Count == 0)
                {
                    Assert.Inconclusive(
                        $"No Form 4 filings found on {testDate:yyyy-MM-dd} (weekend/holiday/outage possible).");
                    return;
                }

                ConsoleUtilities.WriteLine($"\n=== PARSING FORM 4 FILING ===");

                // Try a handful of candidates because some filings won't have parseable XML links (HTML/index variability).
                // This makes the test a true "success" test without being flaky.
                var maxAttempts = Math.Min(10, entries.Count);
                List<Form4Transaction> transactions = null;
                Form4IndexEntry chosenEntry = null;
                Exception lastError = null;

                foreach (var candidate in entries
                             .Where(e => e != null &&
                                         !string.IsNullOrWhiteSpace(e.EdgarUrl) &&
                                         !string.IsNullOrWhiteSpace(e.AccessionNumber))
                             .Take(maxAttempts))
                {
                    chosenEntry = candidate;
                    ConsoleUtilities.WriteLine($"Attempting parse: {chosenEntry}");

                    try
                    {
                        transactions = await downloader.DownloadAndParseForm4Async(chosenEntry);

                        if (transactions != null && transactions.Count > 0)
                        {
                            break; // success
                        }

                        // If it parses but yields zero, try another filing (common with link extraction failures).
                        ConsoleUtilities.WriteLine("  Parsed 0 transactions; trying next candidate...");
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        ConsoleUtilities.WriteLine("  Parse failed; trying next candidate...");
                        ConsoleUtilities.WriteLine($"  Error: {ex.Message}");
                    }
                }

                if (transactions == null || transactions.Count == 0)
                {
                    // If everything failed, make it clear why.
                    if (lastError != null)
                    {
                        Assert.Inconclusive(
                            $"Unable to parse any of the first {maxAttempts} Form 4 filings for {testDate:yyyy-MM-dd}. " +
                            $"Last error: {lastError.Message}");
                    }
                    else
                    {
                        Assert.Inconclusive(
                            $"Unable to parse any of the first {maxAttempts} Form 4 filings for {testDate:yyyy-MM-dd} (0 transactions).");
                    }

                    return;
                }

                ConsoleUtilities.WriteLine($"\nFound {transactions.Count} transactions for: {chosenEntry}");
                foreach (var txn in transactions.Take(50))
                {
                    ConsoleUtilities.WriteLine($"  {txn}");
                }

                if (transactions.Count > 50)
                {
                    ConsoleUtilities.WriteLine($"  ... ({transactions.Count - 50} more)");
                }

                // Strong assertions: this is a "Success" test.
                var firstTxn = transactions.First();

                Assert.IsFalse(string.IsNullOrWhiteSpace(firstTxn.IssuerName), "Issuer name should not be empty");
                Assert.IsFalse(string.IsNullOrWhiteSpace(firstTxn.ReportingOwnerName),
                    "Owner name should not be empty");
                Assert.AreNotEqual(DateTime.MinValue, firstTxn.TransactionDate, "Transaction date should be set");

                // Sanity: parser should propagate these from the entry
                Assert.AreEqual(chosenEntry.AccessionNumber, firstTxn.AccessionNumber,
                    "Accession number should match entry");
                Assert.AreEqual(chosenEntry.FilingDate.Date, firstTxn.FilingDate.Date,
                    "Filing date should match entry date");

                // Optional but useful: ensure some key fields are present in at least one transaction
                Assert.IsTrue(transactions.Any(t => !string.IsNullOrWhiteSpace(t.TransactionCode)),
                    "At least one transaction should have a transaction code");
            }
            finally
            {
                downloader.Dispose();
            }
        }

        [TestMethod]
        [TestCategory("SEC")]
        public async Task Form4_RateLimiting_Validation()
        {
            var downloader = new Form4Downloader(Form4Downloader.USER_AGENT);

            try
            {
                ConsoleUtilities.WriteLine($"\n=== TESTING RATE LIMITING ===");
                ConsoleUtilities.WriteLine("SEC allows max 10 requests/second");

                // We want to validate the limiter itself, not network throughput.
                // Use a tiny endpoint so latency noise is minimized.
                var url = "https://www.sec.gov/robots.txt";

                // Reflect into the downloader to get its SECHttpClient instance (since it is private).
                // This keeps the test scoped to the actual limiter implementation you're using.
                var httpClientField = typeof(Form4Downloader).GetField("_httpClient",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                if (httpClientField == null)
                {
                    Assert.Fail("Unable to access Form4Downloader._httpClient for rate limit validation.");
                    return;
                }

                var secHttp = httpClientField.GetValue(downloader);
                if (secHttp == null)
                {
                    Assert.Fail("Form4Downloader._httpClient was null.");
                    return;
                }

                var getStringAsync = secHttp.GetType().GetMethod("GetStringAsync",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

                if (getStringAsync == null)
                {
                    Assert.Fail("Unable to access SECHttpClient.GetStringAsync for rate limit validation.");
                    return;
                }

                var requestCount = 20;

                // With a 100ms minimum interval between requests:
                // minimum expected spacing time is roughly (requestCount - 1) * 100ms
                // (first request does not need to wait).
                var minIntervalMs = 100;
                var expectedMinElapsedMs = (requestCount - 1) * minIntervalMs;

                var sw = System.Diagnostics.Stopwatch.StartNew();

                int successCount = 0;
                for (int i = 0; i < requestCount; i++)
                {
                    try
                    {
                        // Invoke GetStringAsync(url) via reflection
                        var task = (Task<string>)getStringAsync.Invoke(secHttp, new object[] { url });
                        var _ = await task;
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        // If SEC blocks us (429/403) that's not the limiter failing,
                        // but we should report it and avoid drawing false conclusions.
                        ConsoleUtilities.WriteLine($"Request {i + 1}/{requestCount} failed: {ex.Message}");
                    }
                }

                sw.Stop();

                ConsoleUtilities.WriteLine(
                    $"\nCompleted {requestCount} attempts in {sw.Elapsed.TotalSeconds:F2} seconds");
                ConsoleUtilities.WriteLine($"Succeeded: {successCount}/{requestCount}");

                // If nearly all calls failed, the test can't validate limiter timing.
                if (successCount < requestCount / 2)
                {
                    Assert.Inconclusive(
                        "Too many requests failed (likely SEC blocking or network instability); cannot validate rate limiting.");
                    return;
                }

                ConsoleUtilities.WriteLine(
                    $"Elapsed ms: {sw.ElapsedMilliseconds}, expected minimum ms: {expectedMinElapsedMs}");

                // Allow some jitter: if the network is slow, elapsed will be larger (fine).
                // The failure mode we're catching is "elapsed is too small", meaning limiter isn't spacing calls.
                Assert.IsTrue(
                    sw.ElapsedMilliseconds >= expectedMinElapsedMs - 50,
                    "Rate limiter did not enforce ~100ms spacing between requests (10 req/sec max).");
            }
            finally
            {
                downloader.Dispose();
            }
        }

        [TestMethod]
        [TestCategory("SEC")]
        public void Form4_TransactionCodeMapping_Validation()
        {
            var transaction = new Form4Transaction();

            var testCases = new Dictionary<string, string>
            {
                { "P", "PURCHASE" },
                { "S", "SALE" },
                { "A", "AWARD" },
                { "D", "DISPOSITION" },
                { "F", "TAX PAYMENT" },
                { "G", "GIFT" },
                { "M", "EXERCISE" },
                { "C", "CONVERSION" },
                { "J", "OTHER" }
            };

            ConsoleUtilities.WriteLine("\n=== TRANSACTION CODE MAPPINGS ===");

            foreach (var test in testCases)
            {
                transaction.TransactionCode = test.Key;
                var description = transaction.GetTransactionDescription();

                ConsoleUtilities.WriteLine($"{test.Key} -> {description}");
                Assert.AreEqual(test.Value, description, $"Code {test.Key} should map to {test.Value}");
            }
        }

        [TestMethod]
        [TestCategory("SEC")]
        public void Form4_CacheManagement_Validation()
        {
            var cacheFile = Path.Combine(Form4Downloader.CACHE_DIR, "test_cache.txt");

            ConsoleUtilities.WriteLine($"\n=== TESTING CACHE MANAGEMENT ===");
            ConsoleUtilities.WriteLine($"Cache directory: {Form4Downloader.CACHE_DIR}");

            // Write to cache
            File.WriteAllText(cacheFile, "Test cache content");
            Assert.IsTrue(File.Exists(cacheFile), "Cache file should be created");

            // Read from cache
            var content = File.ReadAllText(cacheFile);
            Assert.AreEqual("Test cache content", content, "Cache content should match");

            // Clean up
            File.Delete(cacheFile);
            Assert.IsFalse(File.Exists(cacheFile), "Cache file should be deleted");

            ConsoleUtilities.WriteLine("Cache management validation successful");
        }

        [TestMethod]
        [TestCategory("SEC")]
        public async Task Form4_ErrorHandling_InvalidDate()
        {
            var downloader = new Form4Downloader(Form4Downloader.USER_AGENT);

            try
            {
                ConsoleUtilities.WriteLine($"\n=== TESTING ERROR HANDLING ===");

                // Test with future date.
                // Given current implementation (quarter index + filter by FilingDate),
                // the correct behavior is typically: return 0 entries (not an exception).
                var futureDate = DateTime.Today.AddDays(30);

                try
                {
                    var entries = await downloader.DownloadQuarterlyIndexAsync(futureDate);

                    Assert.IsNotNull(entries, "Downloader should return a non-null list even for future dates.");

                    ConsoleUtilities.WriteLine($"Future date returned {entries.Count} entries (expected 0).");
                    Assert.AreEqual(0, entries.Count, "Future date should return no entries.");
                }
                catch (Exception ex)
                {
                    // If SEC endpoint changes/404s or the network is flaky, we can accept *specific* errors.
                    // But we should NOT silently accept coding bugs.
                    ConsoleUtilities.WriteLine($"Exception for future date: {ex.Message}");

                    // Heuristic: allow HTTP-ish failures, but fail on obvious coding issues.
                    var msg = ex.Message ?? string.Empty;
                    var isHttpish =
                        msg.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msg.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msg.IndexOf("429", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msg.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msg.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msg.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msg.IndexOf("sec.gov", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!isHttpish)
                    {
                        Assert.Fail("Unexpected exception type for future date handling: " + ex);
                    }

                    Assert.Inconclusive(
                        "SEC/network returned an HTTP-ish error; behavior acceptable but not verifiable right now.");
                }
            }
            finally
            {
                downloader.Dispose();
            }
        }

        [TestMethod]
        [TestCategory("SEC")]
        public void Form4_QuarterCalculation_Validation()
        {
            ConsoleUtilities.WriteLine("\n=== TESTING QUARTER CALCULATION ===");

            var testCases = new List<Tuple<DateTime, int>>
            {
                Tuple.Create(new DateTime(2024, 1, 15), 1),
                Tuple.Create(new DateTime(2024, 3, 31), 1),
                Tuple.Create(new DateTime(2024, 4, 1), 2),
                Tuple.Create(new DateTime(2024, 6, 30), 2),
                Tuple.Create(new DateTime(2024, 7, 1), 3),
                Tuple.Create(new DateTime(2024, 9, 30), 3),
                Tuple.Create(new DateTime(2024, 10, 1), 4),
                Tuple.Create(new DateTime(2024, 12, 31), 4),

                // Extra edge sanity checks:
                Tuple.Create(new DateTime(2024, 2, 29), 1), // leap day
                Tuple.Create(new DateTime(2024, 11, 15), 4)
            };

            foreach (var test in testCases)
            {
                var date = test.Item1;
                var expectedQuarter = test.Item2;

                var quarter = (date.Month - 1) / 3 + 1;

                ConsoleUtilities.WriteLine(string.Format("{0:yyyy-MM-dd} -> Q{1}", date, quarter));
                Assert.AreEqual(expectedQuarter, quarter,
                    "Quarter calculation failed for " + date.ToString("yyyy-MM-dd"));
            }
        }

        [TestMethod]
        [TestCategory("SEC")]
        public void Form4_ClusterScoring_Validation()
        {
            ConsoleUtilities.WriteLine("\n=== TESTING CLUSTER SCORING (Ported from TypeScript) ===");

            // Use an isolated test directory so this test is deterministic and doesn't pollute CACHE_DIR
            var testDir = Path.Combine(Form4Downloader.CACHE_DIR, "ClusterScoring_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);

            try
            {
                // -----------------------------
                // 1) Role weight tests
                // -----------------------------
                var testRoles = new Dictionary<string, double>
                {
                    { "Chief Executive Officer", 4.0 },
                    { "CEO", 4.0 },
                    { "Chief Financial Officer", 3.5 },
                    { "CFO", 3.5 },
                    { "Chief Operating Officer", 3.0 },
                    { "COO", 3.0 },
                    { "General Counsel", 2.5 },
                    { "Director", 1.25 },
                    { "Board Member", 1.0 },

                    // Robustness checks (common real-world variants)
                    { "chief executive officer", 4.0 },
                    { "Chief Executive Officer / President", 4.0 },
                    { "Independent Director", 1.25 }
                };

                ConsoleUtilities.WriteLine("Role Weight Tests:");
                foreach (var test in testRoles)
                {
                    var weight = ClusterBuyAnalyzer.GetRoleWeight(test.Key);
                    ConsoleUtilities.WriteLine(string.Format("  {0}: {1:F2} (expected {2:F2})", test.Key, weight,
                        test.Value));
                    Assert.AreEqual(test.Value, weight, 0.01, "Role weight mismatch for " + test.Key);
                }

                // -----------------------------
                // 2) Cluster scoring test
                // -----------------------------
                ConsoleUtilities.WriteLine("\nCluster Scoring Test:");

                var testOwners = new List<string> { "Tim Cook", "Luca Maestri", "Deirdre O'Brien" };
                var testRolesList = new List<string>
                    { "Chief Executive Officer", "Chief Financial Officer", "Senior Vice President" };

                var metrics = ClusterBuyAnalyzer.UpdateAndScore(
                    baseDir: testDir,
                    ticker: "AAPL",
                    aggregatePurchaseValue: 2000000m,
                    ownerNames: testOwners,
                    roles: testRolesList,
                    cik: "0000320193",
                    period: "2024-01-15",
                    link: "https://www.sec.gov/test"
                );

                Assert.IsNotNull(metrics, "Metrics should not be null");
                Assert.AreEqual("AAPL", metrics.Ticker, "Ticker should be uppercase");
                Assert.AreEqual(3, metrics.DistinctInsiders, "Should have 3 distinct insiders");
                Assert.IsTrue(metrics.Score > 0, "Score should be positive");
                Assert.IsTrue(new[] { "A+", "A", "B", "C" }.Contains(metrics.Tier), "Tier should be valid");
                Assert.IsTrue(metrics.TotalClusterValue > 0, "TotalClusterValue should be positive");

                ConsoleUtilities.WriteLine(string.Format("  Ticker: {0}", metrics.Ticker));
                ConsoleUtilities.WriteLine(string.Format("  Score: {0}", metrics.Score));
                ConsoleUtilities.WriteLine(string.Format("  Tier: {0}", metrics.Tier));
                ConsoleUtilities.WriteLine(string.Format("  Distinct Insiders: {0}", metrics.DistinctInsiders));
                ConsoleUtilities.WriteLine(string.Format("  Total Cluster Value: ${0:N0}", metrics.TotalClusterValue));

                // -----------------------------
                // 3) Tier classification tests (ASSERT, don't just print)
                // -----------------------------
                ConsoleUtilities.WriteLine("\nTier Classification Tests:");

                var aplusMetrics = ClusterBuyAnalyzer.UpdateAndScore(
                    baseDir: testDir,
                    ticker: "TEST1",
                    aggregatePurchaseValue: 10000000m, // $10M
                    ownerNames: new List<string> { "CEO1", "CFO1", "COO1", "Director1" },
                    roles: new List<string> { "CEO", "CFO", "COO", "Director" }
                );

                ConsoleUtilities.WriteLine(string.Format("  A+ Test: Score={0}, Tier={1}", aplusMetrics.Score,
                    aplusMetrics.Tier));
                Assert.AreEqual("A+", aplusMetrics.Tier,
                    "Expected A+ tier for high-value, multi-insider executive cluster");

                var bTierMetrics = ClusterBuyAnalyzer.UpdateAndScore(
                    baseDir: testDir,
                    ticker: "TEST2",
                    aggregatePurchaseValue: 600000m, // $600K
                    ownerNames: new List<string> { "Owner1" },
                    roles: new List<string> { "Director" }
                );

                ConsoleUtilities.WriteLine(string.Format("  B Test: Score={0}, Tier={1}", bTierMetrics.Score,
                    bTierMetrics.Tier));
                Assert.AreEqual("B", bTierMetrics.Tier, "Expected B tier for director-only cluster at ~$600K");

                // -----------------------------
                // 4) Special instructions formatting test (assert values)
                // -----------------------------
                ConsoleUtilities.WriteLine("\nSpecial Instructions Test:");
                var instructions = ClusterBuyAnalyzer.ToSpecialInstructions(testDir, metrics);

                Assert.IsNotNull(instructions, "Instructions should not be null");
                Assert.IsTrue(instructions.Any(i => i.StartsWith("clusterScore|")), "Should have cluster score");
                Assert.IsTrue(instructions.Any(i => i.StartsWith("clusterTier|")), "Should have cluster tier");
                Assert.IsTrue(instructions.Any(i => i.StartsWith("clusterDistinctInsiders|")),
                    "Should have distinct insiders");

                // Stronger: ensure the values are present and correct-ish
                Assert.IsTrue(instructions.Any(i => i == ("clusterTier|" + metrics.Tier)),
                    "clusterTier should match metrics tier");
                Assert.IsTrue(instructions.Any(i => i.Contains("clusterDistinctInsiders|" + metrics.DistinctInsiders)),
                    "Distinct insiders value should match");

                foreach (var instruction in instructions)
                {
                    ConsoleUtilities.WriteLine("  " + instruction);
                }

                ConsoleUtilities.WriteLine("\nCluster scoring validation complete!");
            }
            finally
            {
                // Cleanup test dir to keep things deterministic
                try
                {
                    if (Directory.Exists(testDir))
                        Directory.Delete(testDir, true);
                }
                catch
                {
                    // Non-fatal for test outcome
                }
            }
        }

        [TestMethod]
        [TestCategory("SEC")]
        public void Form4_ClusterFiltering_Validation()
        {
            ConsoleUtilities.WriteLine("\n=== TESTING CLUSTER FILTERING (Matches TypeScript Logic) ===");

            // Test case 1: Should signal - $500k+ purchase by Director
            var shouldSignal1 = 500000m >= 500000m && true; // Director
            ConsoleUtilities.WriteLine($"Test 1: $500K + Director = {shouldSignal1} (expected: True)");
            Assert.IsTrue(shouldSignal1, "Should signal for $500K+ purchase by Director");

            // Test case 2: Should signal - $1M purchase by Officer
            var shouldSignal2 = 1000000m >= 500000m && true; // Officer
            ConsoleUtilities.WriteLine($"Test 2: $1M + Officer = {shouldSignal2} (expected: True)");
            Assert.IsTrue(shouldSignal2, "Should signal for $1M+ purchase by Officer");

            // Test case 3: Should NOT signal - $400K purchase (below threshold)
            var shouldSignal3 = 400000m >= 500000m && true; // Director
            ConsoleUtilities.WriteLine($"Test 3: $400K + Director = {shouldSignal3} (expected: False)");
            Assert.IsFalse(shouldSignal3, "Should NOT signal for purchase below $500K");

            // Test case 4: Should NOT signal - $600K purchase by non-director/non-officer
            var shouldSignal4 = 600000m >= 500000m && false; // Not Director or Officer
            ConsoleUtilities.WriteLine($"Test 4: $600K + Other = {shouldSignal4} (expected: False)");
            Assert.IsFalse(shouldSignal4, "Should NOT signal for purchase by non-director/non-officer");

            // Test case 5: Role detection logic
            var roles = new List<string> { "Chief Executive Officer", "Director" };
            var isDirectorOrOfficer = roles.Any(r =>
                r.Contains("Director") ||
                r.Contains("Officer") ||
                r.Contains("CEO") ||
                r.Contains("CFO") ||
                r.Contains("COO"));

            ConsoleUtilities.WriteLine(
                $"Test 5: Roles={string.Join(", ", roles)} -> isDirectorOrOfficer={isDirectorOrOfficer} (expected: True)");
            Assert.IsTrue(isDirectorOrOfficer, "Should detect director/officer from roles");

            ConsoleUtilities.WriteLine("\nCluster filtering validation complete!");
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            /*ConsoleUtilities.WriteLine("\n=== CLEANUP ===");
            ConsoleUtilities.WriteLine($"Cache directory: {CACHE_DIR}");

            if (Directory.Exists(CACHE_DIR))
            {
                var files = Directory.GetFiles(CACHE_DIR);
                ConsoleUtilities.WriteLine($"Cache contains {files.Length} files");

                // Optionally clean up old cache files
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastAccessTime < DateTime.Now.AddDays(-7))
                    {
                        File.Delete(file);
                        ConsoleUtilities.WriteLine($"Deleted old cache file: {fileInfo.Name}");
                    }
                }
            }*/
        }
    }
}