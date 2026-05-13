using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon.Runtime.Internal;
using static Trade.Tests.SECForm4DownloadTests;

namespace Trade.Form4
{
    /// <summary>
    /// Form 4 downloader and parser
    /// </summary>
    public class Form4Downloader : IDisposable
    {
        public static readonly string SEC_BASE_URL = "https://www.sec.gov";
        public static readonly string EDGAR_FULL_INDEX = "https://www.sec.gov/Archives/edgar/full-index";
        public static readonly string USER_AGENT = "TradeResearchApp/1.0 (contact@example.com)"; // SEC requires User-Agent

        // Cache directory for downloaded files to avoid repeated downloads during testing
        public static readonly string CACHE_DIR = Path.Combine(Environment.CurrentDirectory, "SECForm4Cache");

        private readonly HashSet<string> _cache = new HashSet<string>();
        private readonly SECHttpClient _httpClient;

        static Form4Downloader()
        {
            Directory.CreateDirectory(CACHE_DIR);
        }

        public Form4Downloader(string userAgent)
        {
            _httpClient = new SECHttpClient(userAgent);
        }

        public static async Task<(List<Form4Transaction>, List<IndividualForm4Signal>)> Build(int numberOfDaysToProcess, string jsonCacheFileName = null)
        {
            var downloader = new Form4Downloader(Form4Downloader.USER_AGENT);

            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-numberOfDaysToProcess);

            ConsoleUtilities.WriteLine($"Period: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
            var currentDate = startDate;

            var json1 = !string.IsNullOrEmpty(jsonCacheFileName) ? (File.Exists(jsonCacheFileName) ? File.ReadAllText(jsonCacheFileName) : null) : null;
            var allTransactions = !string.IsNullOrEmpty(json1) ? (JsonConvert.DeserializeObject<List<Form4Transaction>>(json1) ??
                                   new List<Form4Transaction>()).OrderBy(t => t.FilingDate).ThenBy(t => t.IssuerTicker).ThenBy(t => t.ReportingOwnerName).ToList() : new List<Form4Transaction>();

            if (true)
            {
                allTransactions.Clear();

                // Create a semaphore to limit concurrent downloads (respect rate limiting)
                var maxConcurrentDownloads = 5; // Process 5 entries at a time
                var semaphore = new SemaphoreSlim(maxConcurrentDownloads, maxConcurrentDownloads);
                var allTransactionsLock = new object(); // Thread-safe list access

                while (currentDate <= endDate && currentDate <= DateTime.Today)
                {
                    try
                    {
                        ConsoleUtilities.WriteLine($"Processing {currentDate:yyyy-MM-dd}...");
                        var entries = await downloader.DownloadQuarterlyIndexAsync(currentDate);

                        if (entries.Count == 0)
                        {
                            ConsoleUtilities.WriteLine($"  No entries for {currentDate:yyyy-MM-dd}");
                            currentDate = currentDate.AddDays(1);
                            continue;
                        }

                        ConsoleUtilities.WriteLine($"  Found {entries.Count} Form 4 filings, processing in parallel...");

                        // Process entries in parallel with controlled concurrency
                        var downloadTasks = entries.Select(async entry =>
                        {
                            await semaphore.WaitAsync(); // Wait for slot
                            try
                            {
                                ConsoleUtilities.WriteLine($"Processing {currentDate:yyyy-MM-dd}...");

                                var transactions = await downloader.DownloadAndParseForm4Async(entry);

                                // Thread-safe addition to shared list
                                lock (allTransactionsLock)
                                {
                                    allTransactions.AddRange(transactions);
                                }

                                ConsoleUtilities.WriteLine($"    ✅ Processed {entry.CompanyName}: {transactions.Count} transactions");
                            }
                            catch (Exception ex)
                            {
                                ConsoleUtilities.WriteLine($"    ❌ Failed to parse {entry.CompanyName}: {ex.Message}");
                            }
                            finally
                            {
                                semaphore.Release(); // Release slot
                            }
                        }).ToList();

                        // Wait for all downloads for this date to complete
                        await Task.WhenAll(downloadTasks);

                        ConsoleUtilities.WriteLine(
                            $"  Completed {currentDate:yyyy-MM-dd}: {entries.Count} entries processed");
                    }
                    catch (Exception ex)
                    {
                        ConsoleUtilities.WriteLine($"  Error on {currentDate:yyyy-MM-dd}: {ex.Message}");
                    }

                    currentDate = currentDate.AddDays(1);
                }

                semaphore.Dispose();

                if (!string.IsNullOrEmpty(jsonCacheFileName))
                    SerializeTransactions(jsonCacheFileName, allTransactions);
            }

            ConsoleUtilities.WriteLine($"Total transactions collected: {allTransactions.Count}");

            var individualSignals = new List<IndividualForm4Signal>();

            allTransactions = allTransactions.OrderBy(t => t.FilingDate).ThenBy(t => t.IssuerTicker).ThenBy(t => t.ReportingOwnerName).ToList();

            var transactionTasks = allTransactions.Select(txn =>
            {
                var now = DateTime.Now;
                
                if (Filter(txn, out var purchaseValue, out var trade)) return Task.CompletedTask;
                if (trade)
                {
                    // Create individual signal using FILING DATE (when WE discovered it)
                    var signal = new IndividualForm4Signal
                    {
                        Cached = txn.Cached,
                        XmlUrl = txn.XmlUrl,
                        Ticker = txn.IssuerTicker,
                        SignalDate = txn.FilingDate, // ← WHEN WE FOUND IT (not transaction date)
                        //SignalDateTime = 
                        TransactionDate = txn.TransactionDate,
                        PurchaseValue = purchaseValue,
                        ReportingOwner = txn.ReportingOwnerName,
                        PeriodOfReport = txn.PeriodOfReport,
                        OfficerTitle = txn.OfficerTitle,
                        IsDirector = txn.IsDirector,
                        IsOfficer = txn.IsOfficer,
                        IsTenPercentOwner = txn.IsTenPercentOwner,
                        IsOther = txn.IsOther,
                        SharesTransacted = txn.SharesTransacted.Value,
                        PricePerShare = txn.PricePerShare.Value,
                        AccessionNumber = txn.AccessionNumber,
                        CIK = txn.IssuerCIK
                    };

                    individualSignals.Add(signal);

                    ConsoleUtilities.WriteLine($"📌 INDIVIDUAL SIGNAL: {signal.Ticker ?? "N/A"}");
                    ConsoleUtilities.WriteLine($"   Filed: {signal.SignalDate:yyyy-MM-dd} (discovery date)");
                    ConsoleUtilities.WriteLine($"   Transaction: {signal.TransactionDate:yyyy-MM-dd} (actual trade date)");
                    ConsoleUtilities.WriteLine($"   Insider: {signal.ReportingOwner} ({signal.OfficerTitle ?? (signal.IsDirector ? "Director" : "Officer")})");
                    ConsoleUtilities.WriteLine($"   Purchase: ${signal.PurchaseValue:N0} ({signal.SharesTransacted:N0} @ ${signal.PricePerShare:F2})");

                    ConsoleUtilities.WriteLine();
                }

                return Task.CompletedTask;
            });

            await Task.WhenAll(transactionTasks);

            ConsoleUtilities.WriteLine($"=== INDIVIDUAL SIGNALS SUMMARY ===");
            ConsoleUtilities.WriteLine($"Total qualifying Form 4s: {individualSignals.Count}");

            if (individualSignals.Count > 0)
            {
                individualSignals = individualSignals.OrderBy(_ => _.Ticker).ToList();

                // Group by filing date to see daily flow
                var byFilingDate = individualSignals
                    .GroupBy(s => s.SignalDate.Date)
                    .OrderBy(g => g.Key)
                    .ToList();

                ConsoleUtilities.WriteLine($"\nDaily breakdown:");
                foreach (var dateGroup in byFilingDate)
                {
                    var totalValue = dateGroup.Sum(s => s.PurchaseValue);
                    ConsoleUtilities.WriteLine($"  {dateGroup.Key:yyyy-MM-dd}: {dateGroup.Count()} signals, ${totalValue:N0} total");
                }
            }

            downloader.Dispose();

            return (allTransactions, individualSignals);
        }

        private static void PruneCacheDirectory(int olderThanXDays)
        {
            foreach (var file in Directory.GetFiles(CACHE_DIR))
            {
                if(DateTime.Now.Subtract(new FileInfo(file).CreationTime).TotalDays >= olderThanXDays)
                    File.Delete(file);
            }
        }

        private static void SerializeTransactions(string jsonCacheFileName, List<Form4Transaction> allTransactions)
        {
            var json2 = JsonConvert.SerializeObject(allTransactions, Formatting.None);
            File.WriteAllText(jsonCacheFileName, json2);
        }

        public static bool Filter(Form4Transaction txn, out decimal purchaseValue, out bool trade)
        {
            if (string.IsNullOrWhiteSpace(txn.IssuerTicker))
            {
                purchaseValue = 0;
                trade = false;
                return true;
            }
            if (txn.Aff10b5One)
            {
                purchaseValue = 0;
                trade = false;
                return true;
            }
            if (!txn.SharesTransacted.HasValue || !txn.PricePerShare.HasValue)
            {
                purchaseValue = 0;
                trade = false;
                return true;
            }
            if (txn.TransactionCode != "P")
            {
                purchaseValue = 0;
                trade = false;
                return true;
            }
            //if (!txn.IsOpenMarketPurchase) continue;
            if (txn.IssuerTicker.Contains("."))
            {
                purchaseValue = 0;
                trade = false;
                return true;
            }
            if (txn.IssuerTicker == "NONE")
            {
                purchaseValue = 0;
                trade = false;
                return true;
            }
            if (txn.IssuerTicker == "N/A")
            {
                purchaseValue = 0;
                trade = false;
                return true;
            }
            if (txn.SecurityTitle?.StartsWith("[DERIVATIVE]") == true)
            {
                purchaseValue = 0;
                trade = false;
                return true;
            }

            purchaseValue = txn.SharesTransacted.Value * txn.PricePerShare.Value;
            var isDirectorOrOfficer = txn.IsDirector || txn.IsOfficer;
            trade = purchaseValue >= 250_000m && isDirectorOrOfficer;
            return !trade;
        }

        /// <summary>
        /// Download the quarterly Form 4 index file for a specific date
        /// Format: https://www.sec.gov/Archives/edgar/full-index/YYYY/QTR/form.idx
        /// </summary>
        public async Task<List<Form4IndexEntry>> DownloadQuarterlyIndexAsync(DateTime date)
        {
            var quarter = (date.Month - 1) / 3 + 1;
            var indexUrl = $"{EDGAR_FULL_INDEX}/{date.Year}/QTR{quarter}/form.idx";

            ConsoleUtilities.WriteLine($"Downloading index from: {indexUrl}");

            var content = await _httpClient.GetStringAsync(indexUrl);
            return ParseFormIndex(content, date);
        }

        /// <summary>
        /// Parse the form.idx file to extract Form 4 entries
        /// </summary>
        private List<Form4IndexEntry> ParseFormIndex(string indexContent, DateTime filterDate)
        {
            var entries = new List<Form4IndexEntry>();
            var lines = indexContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // Skip header lines (format description)
            var dataStarted = false;
            foreach (var line in lines)
            {
                // Data starts after the separator line
                if (line.Contains("---"))
                {
                    dataStarted = true;
                    continue;
                }

                if (!dataStarted || string.IsNullOrWhiteSpace(line))
                    continue;

                // Fixed-width format (positions approximate based on header):
                // Form Type   Company Name                                                  CIK         Date Filed  File Name
                // Note: Format uses multiple spaces as separators, need to parse carefully

                try
                {
                    // Split on multiple spaces (2 or more) to separate columns
                    var parts = Regex.Split(line, @"\s{2,}").Where(s => !string.IsNullOrEmpty(s)).ToArray();

                    if (parts.Length < 5)
                        continue;

                    var formType = parts[0].Trim();
                    if (formType != "4") // Only Form 4 filings
                        continue;

                    var companyName = parts[1].Trim();
                    var cik = parts[2].Trim();
                    var dateFiledStr = parts[3].Trim();
                    var fileName = parts[4].Trim();

                    if (!DateTime.TryParse(dateFiledStr, out var filingDate))
                        continue;

                    // Filter by date if specified
                    if (filterDate != DateTime.MinValue && filingDate.Date != filterDate.Date)
                        continue;

                    var accessionNumber = ExtractAccessionNumber(fileName);

                    entries.Add(new Form4IndexEntry
                    {
                        FormType = formType,
                        CompanyName = companyName,
                        CIK = cik,
                        FilingDate = filingDate,
                        EdgarUrl = $"{SEC_BASE_URL}/Archives/{fileName}",
                        AccessionNumber = accessionNumber
                    });
                }
                catch
                {
                    // Skip malformed lines
                    continue;
                }
            }

            return entries;
        }

        /// <summary>
        /// Download and parse a specific Form 4 XML filing
        /// </summary>
        public async Task<List<Form4Transaction>> DownloadAndParseForm4Async(Form4IndexEntry entry)
        {
            if (!_cache.Add(entry.AccessionNumber))
                return new List<Form4Transaction>();

            // Generate cache file path based on accession number
            var cacheFileName = $"Form4_{entry.AccessionNumber.Replace("-", "")}.cache";
            var cacheFilePath = Path.Combine(CACHE_DIR, cacheFileName);

            // Check if cached file exists
            if (File.Exists(cacheFilePath))
            {
                try
                {
                    ConsoleUtilities.WriteLine($"Loading cached Form 4 for {entry.CompanyName}: {cacheFileName}");

                    // Read and deserialize cached transactions
                    var json = File.ReadAllText(cacheFilePath);
                    var transactions = JsonConvert.DeserializeObject<List<Form4Transaction>>(json);

                    if (transactions != null && transactions.Count > 0)
                    {
                        foreach(var transaction in transactions)
                        {
                            transaction.Cached = true;
                        }

                        ConsoleUtilities.WriteLine($"Loaded {transactions.Count} transactions from cache");
                        return transactions;
                    }
                }
                catch (Exception cacheEx)
                {
                    ConsoleUtilities.WriteLine($"Failed to load cache, will re-download: {cacheEx.Message}");
                    // Continue to download if cache read fails
                }
            }

            // Cache miss or invalid - download and parse
            List<Form4Transaction> parsedTransactions = null;

            try
            {
                // Step 1: Download the HTML index page to find XML links
                var indexUrl = entry.EdgarUrl.Replace(".txt", "-index.htm");

                ConsoleUtilities.WriteLine($"Downloading Form 4 index: {indexUrl}");

                var indexHtml = await _httpClient.GetStringAsync(indexUrl);

                // Step 2: Extract XML file links from the HTML index
                var xmlUrls = ExtractXmlLinksFromHtml(indexHtml, entry.EdgarUrl);

                if (xmlUrls.Count == 0)
                {
                    ConsoleUtilities.WriteLine($"No XML files found in index for {entry.CompanyName}");
                    throw new Exception("No XML files found in filing index");
                }

                // Step 3: Download and parse each XML file
                parsedTransactions = new List<Form4Transaction>();

                foreach (var xmlUrl in xmlUrls)
                {
                    if (xmlUrl.Contains("/xsl"))
                        continue;

                    try
                    {
                        ConsoleUtilities.WriteLine($"Downloading XML: {xmlUrl}");
                        var xmlContent = await _httpClient.GetStringAsync(xmlUrl);
                        var transactions = ParseForm4Xml(xmlUrl, xmlContent, entry);
                        if (transactions.Any(_ => _.IssuerTicker == "VACI"))
                        {

                        }
                        parsedTransactions.AddRange(transactions);
                    }
                    catch (Exception xmlEx)
                    {
                        ConsoleUtilities.WriteLine($"Failed to parse XML {xmlUrl}: {xmlEx.Message}");
                        // Continue with other XML files
                    }
                }

                // Step 4: Save to cache if we got any transactions
                if (parsedTransactions.Count > 0)
                {
                    try
                    {
                        var json = JsonConvert.SerializeObject(parsedTransactions, Formatting.None);
                        File.WriteAllText(cacheFilePath, json);
                        ConsoleUtilities.WriteLine($"Cached {parsedTransactions.Count} transactions to {cacheFileName}");
                    }
                    catch (Exception saveEx)
                    {
                        ConsoleUtilities.WriteLine($"Warning: Failed to save cache: {saveEx.Message}");
                        // Non-fatal - continue with parsed data
                    }
                }

                return parsedTransactions;
            }
            catch (Exception ex)
            {
                ConsoleUtilities.WriteLine($"Failed to download Form 4 for {entry.CompanyName}: {ex.Message}");

                // Fallback: try to parse from the text filing
                try
                {
                    var textContent = await _httpClient.GetStringAsync(entry.EdgarUrl);
                    parsedTransactions = ParseForm4Text(textContent, entry);

                    // Cache text-parsed results too
                    if (parsedTransactions != null && parsedTransactions.Count > 0)
                    {
                        try
                        {
                            var json = JsonConvert.SerializeObject(parsedTransactions, Formatting.None);
                            File.WriteAllText(cacheFilePath, json);
                            ConsoleUtilities.WriteLine($"Cached {parsedTransactions.Count} transactions (from text) to {cacheFileName}");
                        }
                        catch (Exception saveEx)
                        {
                            ConsoleUtilities.WriteLine($"Warning: Failed to save cache: {saveEx.Message}");
                        }
                    }

                    return parsedTransactions;
                }
                catch
                {
                    throw new Exception($"Failed to parse Form 4 for {entry.CompanyName}", ex);
                }
            }
        }

        /// <summary>
        /// Extract XML file links from the HTML index page
        /// </summary>
        private List<string> ExtractXmlLinksFromHtml(string html, string baseUrl)
        {
            var xmlUrls = new List<string>();

            // Get the base directory from the filing URL
            // Example: https://www.sec.gov/Archives/edgar/data/320193/0000320193-24-000001.txt
            // Base: https://www.sec.gov/Archives/edgar/data/320193/0000320193-24-000001/
            var baseDirectory = baseUrl.Substring(0, baseUrl.LastIndexOf('.')) + "/";

            // Look for XML file references in the HTML
            // Common patterns:
            // 1. .xml files (primary document)
            // 2. -xbrl.xml or .xml in table rows
            // 3. Files ending with .xml in links

            // Pattern 1: Find <a href="...xml"> tags
            var hrefPattern = new Regex(@"href=[""']([^""']*\.xml)[""']", RegexOptions.IgnoreCase);
            var matches = hrefPattern.Matches(html);

            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var xmlFile = match.Groups[1].Value;

                    // Build full URL
                    string fullUrl;
                    if (xmlFile.StartsWith("http"))
                    {
                        fullUrl = xmlFile;
                    }
                    else if (xmlFile.StartsWith("/"))
                    {
                        fullUrl = SEC_BASE_URL + xmlFile;
                    }
                    else
                    {
                        fullUrl = baseDirectory + xmlFile;
                    }

                    // Avoid duplicates and only include primary Form 4 XML files
                    // Typically look for files with "xbrl" or "primary_doc" or just .xml
                    if (!xmlUrls.Contains(fullUrl))
                    {
                        xmlUrls.Add(fullUrl);
                        ConsoleUtilities.WriteLine($"  Found XML: {xmlFile}");
                    }
                }
            }

            // If no XML files found, try alternative pattern for the primary document
            if (xmlUrls.Count == 0)
            {
                // Sometimes the primary document is simply named with the accession number
                // Try constructing common XML filenames
                var accessionPattern = new Regex(@"(\d{10}-\d{2}-\d{6})");
                var accessionMatch = accessionPattern.Match(baseUrl);

                if (accessionMatch.Success)
                {
                    var accession = accessionMatch.Groups[1].Value;
                    var commonNames = new[]
                    {
                            $"{accession}.xml",
                            $"{accession.Replace("-", "")}.xml",
                            "primary_doc.xml",
                            "form4.xml"
                        };

                    foreach (var filename in commonNames)
                    {
                        var testUrl = baseDirectory + filename;
                        xmlUrls.Add(testUrl);
                        ConsoleUtilities.WriteLine($"  Trying potential XML: {filename}");
                    }
                }
            }

            return xmlUrls;
        }

        static bool ParseForm4Bool(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return false;
            v = v.Trim();
            return v == "1"
                   || v.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parse Form 4 XML (XBRL format)
        /// </summary>
        private List<Form4Transaction> ParseForm4Xml(string xmlUrl, string xmlContent, Form4IndexEntry entry)
        {
            var transactions = new List<Form4Transaction>();

            try
            {
                var doc = XDocument.Parse(xmlContent);
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                // Helper: prefer <value> inner node when present, else fallback to element.Value
                string ReadValue(XElement el)
                {
                    if (el == null) return null;

                    // Many Form-4 nodes are like <foo><value>123</value></foo>
                    var inner = el.Element(ns + "value")?.Value;
                    if (!string.IsNullOrWhiteSpace(inner)) return inner.Trim();

                    // Sometimes it's just <foo>123</foo>
                    var v = el.Value;
                    return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
                }

                DateTime? periodOfReport = null;
                var periodOfReportStr = ReadValue(doc.Descendants(ns + "periodOfReport").FirstOrDefault());
                if (DateTime.TryParse(periodOfReportStr, out var por))
                    periodOfReport = por;

                // Extract issuer information
                var issuerName = ReadValue(doc.Descendants(ns + "issuerName").FirstOrDefault());
                var issuerCik = ReadValue(doc.Descendants(ns + "issuerCik").FirstOrDefault());
                var issuerTicker = ReadValue(doc.Descendants(ns + "issuerTradingSymbol").FirstOrDefault());

                // Extract reporting owner information
                var ownerName = ReadValue(doc.Descendants(ns + "rptOwnerName").FirstOrDefault());
                var ownerCik = ReadValue(doc.Descendants(ns + "rptOwnerCik").FirstOrDefault());

                var isDirector = ParseForm4Bool(ReadValue(doc.Descendants(ns + "isDirector").FirstOrDefault()));
                var isOfficer = ParseForm4Bool(ReadValue(doc.Descendants(ns + "isOfficer").FirstOrDefault()));
                var isTenPercentOwner = ParseForm4Bool(ReadValue(doc.Descendants(ns + "isTenPercentOwner").FirstOrDefault()));
                var isOther = ParseForm4Bool(ReadValue(doc.Descendants(ns + "isOther").FirstOrDefault()));
                var officerTitle = ReadValue(doc.Descendants(ns + "officerTitle").FirstOrDefault());

                var aff10b5One = ParseForm4Bool(ReadValue(doc.Descendants(ns + "aff10b5One").FirstOrDefault()));

                // Extract non-derivative transactions
                foreach (var txn in doc.Descendants(ns + "nonDerivativeTransaction"))
                {
                    var transaction = new Form4Transaction
                    {
                        XmlUrl = xmlUrl,
                        IssuerName = issuerName,
                        IssuerCIK = issuerCik,
                        IssuerTicker = issuerTicker,
                        ReportingOwnerName = ownerName,
                        ReportingOwnerCIK = ownerCik,
                        IsDirector = isDirector,
                        IsOfficer = isOfficer,
                        IsTenPercentOwner = isTenPercentOwner,
                        IsOther = isOther,
                        OfficerTitle = officerTitle,
                        PeriodOfReport = periodOfReport,
                        Aff10b5One = aff10b5One,
                        AccessionNumber = entry.AccessionNumber,
                        FilingDate = entry.FilingDate
                    };

                    // Transaction details
                    var securityTitle = ReadValue(txn.Descendants(ns + "securityTitle").FirstOrDefault());
                    transaction.SecurityTitle = securityTitle;

                    var transactionDate = ReadValue(txn.Descendants(ns + "transactionDate").FirstOrDefault());
                    if (DateTime.TryParse(transactionDate, out var txnDate))
                        transaction.TransactionDate = txnDate;

                    // NOTE: Most filings have transactionCoding/transactionCode, but Descendants("transactionCode") works.
                    var transactionCode = ReadValue(txn.Descendants(ns + "transactionCode").FirstOrDefault());
                    transaction.TransactionCode = transactionCode;

                    // A/D: acquired/disposed code (often under transactionAmounts/transactionAcquiredDisposedCode)
                    var acquiredDisposed = ReadValue(txn.Descendants(ns + "transactionAcquiredDisposedCode").FirstOrDefault());
                    // Requires Form4Transaction.AcquiredDisposedCode (string) or similar:
                    transaction.AcquiredDisposedCode = acquiredDisposed;

                    var shares = ReadValue(txn.Descendants(ns + "transactionShares").FirstOrDefault());
                    if (decimal.TryParse(shares, out var sharesVal))
                        transaction.SharesTransacted = sharesVal;

                    var price = ReadValue(txn.Descendants(ns + "transactionPricePerShare").FirstOrDefault());
                    if (decimal.TryParse(price, out var priceVal))
                        transaction.PricePerShare = priceVal;

                    var sharesOwned = ReadValue(txn.Descendants(ns + "sharesOwnedFollowingTransaction").FirstOrDefault());
                    if (decimal.TryParse(sharesOwned, out var ownedVal))
                        transaction.SharesOwnedAfter = ownedVal;

                    // Per-transaction ownership nature (can vary)
                    var directOrIndirect = ReadValue(txn.Descendants(ns + "directOrIndirectOwnership").FirstOrDefault());
                    // Requires Form4Transaction.DirectOrIndirectOwnership (string) or similar:
                    transaction.DirectOrIndirectOwnership = directOrIndirect;

                    // Requires Form4Transaction.NatureOfOwnership (string) or similar:
                    var natureOfOwnership = ReadValue(txn.Descendants(ns + "natureOfOwnership").FirstOrDefault());
                    transaction.NatureOfOwnership = natureOfOwnership;

                    transaction.IsDirectOwnership = directOrIndirect == "D";

                    transactions.Add(transaction);
                }

                // Extract derivative transactions (options, warrants, etc.)
                foreach (var txn in doc.Descendants(ns + "derivativeTransaction"))
                {
                    var transaction = new Form4Transaction
                    {
                        XmlUrl = xmlUrl,
                        IssuerName = issuerName,
                        IssuerCIK = issuerCik,
                        IssuerTicker = issuerTicker,
                        ReportingOwnerName = ownerName,
                        ReportingOwnerCIK = ownerCik,
                        IsDirector = isDirector,
                        IsOfficer = isOfficer,
                        IsTenPercentOwner = isTenPercentOwner,
                        IsOther = isOther,
                        OfficerTitle = officerTitle,
                        PeriodOfReport = periodOfReport,
                        Aff10b5One = aff10b5One,
                        AccessionNumber = entry.AccessionNumber,
                        FilingDate = entry.FilingDate
                    };

                    var securityTitle = ReadValue(txn.Descendants(ns + "securityTitle").FirstOrDefault());
                    transaction.SecurityTitle = $"[DERIVATIVE] {securityTitle}";

                    var transactionDate = ReadValue(txn.Descendants(ns + "transactionDate").FirstOrDefault());
                    if (DateTime.TryParse(transactionDate, out var txnDate))
                        transaction.TransactionDate = txnDate;

                    var transactionCode = ReadValue(txn.Descendants(ns + "transactionCode").FirstOrDefault());
                    transaction.TransactionCode = transactionCode;

                    // A/D: acquired/disposed code
                    var acquiredDisposed = ReadValue(txn.Descendants(ns + "transactionAcquiredDisposedCode").FirstOrDefault());
                    transaction.AcquiredDisposedCode = acquiredDisposed;

                    var shares = ReadValue(txn.Descendants(ns + "transactionShares").FirstOrDefault());
                    if (decimal.TryParse(shares, out var sharesVal))
                        transaction.SharesTransacted = sharesVal;

                    var price = ReadValue(txn.Descendants(ns + "transactionPricePerShare").FirstOrDefault());
                    if (decimal.TryParse(price, out var priceVal))
                        transaction.PricePerShare = priceVal;

                    // Per-transaction ownership nature (can vary)
                    var directOrIndirect = ReadValue(txn.Descendants(ns + "directOrIndirectOwnership").FirstOrDefault());
                    transaction.DirectOrIndirectOwnership = directOrIndirect;

                    var natureOfOwnership = ReadValue(txn.Descendants(ns + "natureOfOwnership").FirstOrDefault());
                    transaction.NatureOfOwnership = natureOfOwnership;

                    transaction.IsDirectOwnership = directOrIndirect == "D";

                    transactions.Add(transaction);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to parse Form 4 XML: {ex.Message}", ex);
            }

            return transactions;
        }

        /// <summary>
        /// Parse Form 4 from text filing (fallback method)
        /// </summary>
        private List<Form4Transaction> ParseForm4Text(string textContent, Form4IndexEntry entry)
        {
            var transactions = new List<Form4Transaction>();

            // This is a simplified parser for text format
            // Real implementation would need more robust parsing
            var lines = textContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // Look for key sections in the text
            var issuerName = entry.CompanyName;
            var issuerCIK = entry.CIK;

            // Try to extract basic transaction info from text
            // This is a placeholder - actual implementation would need regex patterns
            // to extract structured data from the EDGAR text format

            ConsoleUtilities.WriteLine($"Text parsing not fully implemented for {entry.CompanyName}");

            return transactions;
        }

        private string ExtractAccessionNumber(string fileName)
        {
            // Extract accession number from file path
            // Example: edgar/data/320193/0000320193-24-000001.txt -> 0000320193-24-000001
            var match = Regex.Match(fileName, @"(\d{10}-\d{2}-\d{6})");
            return match.Success ? match.Groups[1].Value : fileName;
        }

        public static void DeleteCached(Form4Transaction form4Transaction)
        {
            // Generate cache file path based on accession number
            var cacheFileName = $"Form4_{form4Transaction.AccessionNumber.Replace("-", "")}.cache";
            var cacheFilePath = Path.Combine(CACHE_DIR, cacheFileName);

            // Check if cached file exists
            if (File.Exists(cacheFilePath))
            {
                try
                {
                   File.Delete(cacheFilePath);
                }
                catch (Exception cacheEx)
                {
                    ConsoleUtilities.WriteLine($"Failed to delete cache, will re-download: {cacheEx.Message}");
                    // Continue to download if cache read fails
                }
            }
        }

        public static void DeleteCached(IndividualForm4Signal individualForm4Signal)
        {
            // Generate cache file path based on accession number
            var cacheFileName = $"Form4_{individualForm4Signal.AccessionNumber.Replace("-", "")}.cache";
            var cacheFilePath = Path.Combine(CACHE_DIR, cacheFileName);

            // Check if cached file exists
            if (File.Exists(cacheFilePath))
            {
                try
                {
                    File.Delete(cacheFilePath);
                }
                catch (Exception cacheEx)
                {
                    ConsoleUtilities.WriteLine($"Failed to delete cache, will re-download: {cacheEx.Message}");
                    // Continue to download if cache read fails
                }
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
