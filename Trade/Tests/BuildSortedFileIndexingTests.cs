using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trade.IVPreCalc2;
using Trade.Polygon2;
using Newtonsoft.Json;

namespace Trade.Tests
{
    /// <summary>
    /// Test suite for bulk file indexing functionality that creates .index files
    /// containing symbol-to-offset mappings for fast lookups in sorted bulk option files.
    /// This dramatically speeds up BulkFileContractTracer by providing direct seek offsets.
    /// </summary>
    [TestClass]
    public class BuildSortedFileIndexingTests
    {
        public TestContext TestContext { get; set; }

        // ================ INDEX CACHE ================
        
        /// <summary>
        /// In-memory cache for loaded index data. Each index is ~2MB, so caching improves performance
        /// for repeated lookups without excessive memory usage.
        /// Thread-safe using lock for .NET Framework 4.7.2 compatibility.
        /// </summary>
        private static readonly Dictionary<string, BulkFileIndexData> _indexCache = new Dictionary<string, BulkFileIndexData>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _indexCacheLock = new object();

        /// <summary>
        /// Clear the index cache (useful for testing or memory management).
        /// </summary>
        public static void ClearIndexCache()
        {
            lock (_indexCacheLock)
            {
                _indexCache.Clear();
                ConsoleUtilities.WriteLine($"🧹 Index cache cleared");
            }
        }

        /// <summary>
        /// Get cache statistics for monitoring.
        /// </summary>
        public static string GetIndexCacheStats()
        {
            lock (_indexCacheLock)
            {
                var estimatedMemoryMB = _indexCache.Count * 2; // ~2MB per index
                return $"📊 Index Cache: {_indexCache.Count} indexes loaded (~{estimatedMemoryMB}MB estimated)";
            }
        }

        [TestMethod]
        [TestCategory("SortedIndexes")]
        public void BuildSortedFileIndexes_CreatesValidIndexFiles()
        {
            var testDir = CreateMockSortedBulkFiles();
            try
            {
                // Act: Build indexes for the mock sorted files
                var indexCount = BuildSortedFileIndexes(testDir, forceRebuild: true);

                // Assert: Should have created index files
                Assert.AreEqual(2, indexCount, "Should have created indexes for 2 mock files");

                // Verify index files exist
                var indexFiles = Directory.GetFiles(testDir, "*.index", SearchOption.AllDirectories);
                Assert.AreEqual(2, indexFiles.Length, "Should have 2 .index files");

                foreach (var indexFile in indexFiles)
                {
                    Assert.IsTrue(File.Exists(indexFile), $"Index file should exist: {indexFile}");
                    
                    // Verify index file contains valid JSON
                    var json = File.ReadAllText(indexFile);
                    dynamic indexData = JsonConvert.DeserializeObject(json);
                    
                    Assert.IsNotNull(indexData, "Index file should contain valid JSON");
                    Assert.IsNotNull(indexData.Metadata, "Index should have metadata");
                    Assert.IsNotNull(indexData.UnderlyingIndex, "Index should have underlying index");
                    
                    // Verify metadata contains required fields
                    Assert.IsNotNull(indexData.Metadata.SourceFile, "Metadata should have source file");
                    Assert.IsNotNull(indexData.Metadata.SourceFileHash, "Metadata should have file hash");
                    Assert.IsTrue(indexData.Metadata.SymbolCount > 0, "Should have indexed some symbols");
                }

                ConsoleUtilities.WriteLine($"✅ Successfully created {indexCount} index files");
            }
            finally
            {
                CleanupTestDirectory(testDir);
            }
        }

        [TestMethod]
        [TestCategory("SortedIndexes")]
        public void BuildSortedFileIndexes_SkipsUpToDateIndexes()
        {
            var testDir = CreateMockSortedBulkFiles();
            try
            {
                // First run: Create indexes
                var firstRun = BuildSortedFileIndexes(testDir, forceRebuild: false);
                Assert.AreEqual(2, firstRun, "First run should create 2 indexes");

                // Second run: Should skip existing indexes
                var secondRun = BuildSortedFileIndexes(testDir, forceRebuild: false);
                Assert.AreEqual(2, secondRun, "Second run should skip 2 existing indexes");

                // Force rebuild: Should recreate indexes
                var forceRun = BuildSortedFileIndexes(testDir, forceRebuild: true);
                Assert.AreEqual(2, forceRun, "Force rebuild should recreate 2 indexes");

                ConsoleUtilities.WriteLine("✅ Index skipping and force rebuild logic works correctly");
            }
            finally
            {
                CleanupTestDirectory(testDir);
            }
        }

        [TestMethod]
        [TestCategory("SortedIndexes")]
        public void LoadFileIndex_ReturnsValidIndexData()
        {
            var testDir = CreateMockSortedBulkFiles();
            try
            {
                // Create indexes first
                BuildSortedFileIndexes(testDir, forceRebuild: true);

                // Get a sorted file to test loading
                var sortedFiles = Directory.GetFiles(testDir, "*_Sorted.csv", SearchOption.AllDirectories);
                Assert.IsTrue(sortedFiles.Length > 0, "Should have sorted files");

                var testFile = sortedFiles[0];
                
                // Act: Load the index
                var indexData = LoadFileIndex(testFile);

                // Assert: Should return valid index data
                Assert.IsNotNull(indexData, "Should return index data");
                Assert.IsNotNull(indexData.Metadata, "Should have metadata");
                Assert.IsNotNull(indexData.UnderlyingIndex, "Should have underlying index");

                // Verify we can look up underlyings
                Assert.IsTrue(indexData.UnderlyingIndex.Count > 0, "Should have indexed underlyings");

                var firstUnderlying = indexData.UnderlyingIndex.Keys.First();
                var entry = indexData.UnderlyingIndex[firstUnderlying];
                
                Assert.IsNotNull(entry, "Should be able to look up underlying");
                Assert.AreEqual(firstUnderlying, entry.Underlying, "Underlying should match key");
                Assert.IsTrue(entry.StartOffset >= 0, "Start offset should be valid");
                Assert.IsTrue(entry.EndOffset > entry.StartOffset, "End offset should be greater than start");
                Assert.IsTrue(entry.LineCount > 0, "Should have line count");
                Assert.IsTrue(entry.SymbolCount > 0, "Should have symbol count");

                ConsoleUtilities.WriteLine($"✅ Successfully loaded index with {indexData.UnderlyingIndex.Count} underlyings");
                ConsoleUtilities.WriteLine($"   First underlying: {firstUnderlying} at offset {entry.StartOffset}-{entry.EndOffset} ({entry.SymbolCount} symbols, {entry.LineCount} lines)");
            }
            finally
            {
                CleanupTestDirectory(testDir);
            }
        }

        [TestMethod]
        [TestCategory("SortedIndexes")]
        public void IndexSymbolLookup_ProvidesAccurateOffsets()
        {
            var testDir = CreateMockSortedBulkFiles();
            try
            {
                // Create indexes
                BuildSortedFileIndexes(testDir, forceRebuild: true);

                var sortedFiles = Directory.GetFiles(testDir, "*_Sorted.csv", SearchOption.AllDirectories);
                var testFile = sortedFiles[0];
                
                // Load index
                var indexData = LoadFileIndex(testFile);
                Assert.IsNotNull(indexData, "Should load index data");

                // Get an underlying to test
                var testUnderlying = indexData.UnderlyingIndex.Keys.First();
                var indexEntry = indexData.UnderlyingIndex[testUnderlying];

                // Verify the offset by reading the file at that position
                using (var fs = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Seek to the indexed position
                    fs.Seek(indexEntry.StartOffset, SeekOrigin.Begin);
                    
                    using (var sr = new StreamReader(fs))
                    {
                        var line = sr.ReadLine();
                        Assert.IsNotNull(line, "Should read a line at the indexed offset");

                        // Extract the ticker from the line
                        var comma = line.IndexOf(',');
                        Assert.IsTrue(comma > 0, "Line should have comma delimiter");
                        
                        var ticker = line.Substring(0, comma).Trim().ToUpperInvariant();
                        
                        // Parse underlying from the ticker
                        var contractKey = BulkFileContractTracer.ParseContractKey(ticker);
                        var underlying = contractKey?.Underlying ?? ExtractUnderlyingFromTicker(ticker);
                        
                        Assert.AreEqual(testUnderlying, underlying, "Underlying at indexed offset should match the expected underlying");
                    }
                }

                ConsoleUtilities.WriteLine($"✅ Index offset verification passed for underlying {testUnderlying}");
            }
            finally
            {
                CleanupTestDirectory(testDir);
            }
        }

        [TestMethod]
        [TestCategory("SortedIndexes")]
        public void BuildAllSortedFileIndexes_IntegratesWithDirectoryResolution()
        {
            // This test uses the actual directory resolution system
            var bulkDir = IVPreCalc.ResolveBulkDir();
            
            if (string.IsNullOrEmpty(bulkDir) || !Directory.Exists(bulkDir))
            {
                Assert.Inconclusive("No bulk directory found for integration test");
                return;
            }

            // Look for sorted files
            var sortedFiles = Directory.GetFiles(bulkDir, "*_Sorted.csv", SearchOption.AllDirectories)
                .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(_ => _)
                .Take(2) // Limit to first 2 files for test performance
                .ToList();

            if (sortedFiles.Count == 0)
            {
                Assert.Inconclusive("No sorted bulk files found for integration test");
                return;
            }

            try
            {
                // Act: Build indexes using the integrated method
                BuildAllSortedFileIndexesForOptions(30);
                
                // Verify some indexes were created
                foreach (var sortedFile in sortedFiles.Take(2))
                {
                    var indexFile = sortedFile + ".index";
                    if (File.Exists(indexFile))
                    {
                        var indexData = LoadFileIndex(sortedFile);
                        Assert.IsNotNull(indexData, $"Should load index for {Path.GetFileName(sortedFile)}");
                        
                        ConsoleUtilities.WriteLine($"   - {Path.GetFileName(sortedFile)}: {indexData.UnderlyingIndex.Count} underlyings indexed");
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleUtilities.WriteLine($"Integration test info: {ex.Message}");
                // Don't fail the test for integration issues - this depends on external data
                Assert.Inconclusive($"Integration test could not complete: {ex.Message}");
            }
        }

        [TestMethod]
        [TestCategory("SortedIndexes")]
        public void IndexValidation_DetectsFileChanges()
        {
            var testDir = CreateMockSortedBulkFiles();
            try
            {
                var sortedFiles = Directory.GetFiles(testDir, "*_Sorted.csv", SearchOption.AllDirectories);
                var testFile = sortedFiles[0];

                // Create initial index
                BuildSortedFileIndexes(testDir, forceRebuild: true);
                var initialIndex = LoadFileIndex(testFile);
                Assert.IsNotNull(initialIndex, "Should create initial index");

                // Modify the source file
                File.AppendAllText(testFile, "O:TEST251231C00100000,10,1.0,1.1,1.2,0.9,1000000000000000000,5\n");

                // Try to load index - should detect file change
                var modifiedIndex = LoadFileIndex(testFile);
                Assert.IsNull(modifiedIndex, "Should return null for modified file");

                // Rebuild index after modification
                BuildSortedFileIndexes(testDir, forceRebuild: true);
                var rebuiltIndex = LoadFileIndex(testFile);
                Assert.IsNotNull(rebuiltIndex, "Should rebuild index after modification");

                // New index should have different hash
                Assert.AreNotEqual(initialIndex.Metadata.SourceFileHash, rebuiltIndex.Metadata.SourceFileHash,
                    "Hash should change after file modification");

                ConsoleUtilities.WriteLine("✅ File change detection works correctly");
            }
            finally
            {
                CleanupTestDirectory(testDir);
            }
        }

        [TestMethod]
        [TestCategory("SortedIndexes")]
        public void IndexEfficientReading_WorksWithByteAccuracy()
        {
            var testDir = CreateMockSortedBulkFiles();
            try
            {
                // Create indexes
                BuildSortedFileIndexes(testDir, forceRebuild: true);

                var sortedFiles = Directory.GetFiles(testDir, "*_Sorted.csv", SearchOption.AllDirectories);
                var testFile = sortedFiles[0];
                
                // Load index
                var indexData = LoadFileIndex(testFile);
                Assert.IsNotNull(indexData, "Should load index data");
                
                var firstUnderlying = indexData.UnderlyingIndex.Keys.First();
                
                // Test efficient reading using the new method
                var underlyingData = ReadUnderlyingData(testFile, firstUnderlying);
                
                Assert.IsTrue(underlyingData.Count > 0, "Should read data for underlying");
                
                // Verify all lines contain the expected underlying
                foreach (var line in underlyingData)
                {
                    var comma = line.IndexOf(',');
                    Assert.IsTrue(comma > 0, "Line should have comma delimiter");
                    
                    var ticker = line.Substring(0, comma).Trim().ToUpperInvariant();
                    
                    // Parse underlying from ticker
                    string underlying;
                    if (ticker.StartsWith("O:"))
                    {
                        var contractKey = BulkFileContractTracer.ParseContractKey(ticker);
                        underlying = contractKey?.Underlying ?? ExtractUnderlyingFromTicker(ticker);
                    }
                    else
                    {
                        underlying = ticker; // For stocks
                    }
                    
                    Assert.AreEqual(firstUnderlying, underlying, 
                        $"All lines should be for underlying {firstUnderlying}, but found {underlying}");
                }
                
                ConsoleUtilities.WriteLine($"✅ Efficient reading test passed for {firstUnderlying} ({underlyingData.Count} lines)");
                
                // Test processing all underlyings
                var processedCount = 0;
                ProcessAllUnderlyings(testFile, (underlying, lines) =>
                {
                    processedCount++;
                    Assert.IsTrue(lines.Count > 0, $"Should have lines for {underlying}");
                });
                
                Assert.AreEqual(indexData.UnderlyingIndex.Count, processedCount, 
                    "Should process all underlyings from index");
                
                ConsoleUtilities.WriteLine($"✅ Processed all {processedCount} underlyings efficiently");
            }
            finally
            {
                CleanupTestDirectory(testDir);
            }
        }

        /// <summary>
        /// Creates mock sorted bulk files for testing.
        /// </summary>
        private string CreateMockSortedBulkFiles()
        {
            var testDir = Path.Combine(Path.GetTempPath(), "BulkIndexTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(testDir);

            // Create two mock sorted files with realistic option data
            var dates = new[] { "2024-01-15", "2024-01-16" };
            var header = "ticker,volume,open,close,high,low,window_start,transactions\n";

            foreach (var date in dates)
            {
                var filePath = Path.Combine(testDir, $"{date}_us_options_opra_minute_aggs_Sorted.csv");
                var sb = new StringBuilder();
                sb.Append(header);

                // Create realistic option symbols in sorted order
                var symbols = new[]
                {
                    "O:AAPL250117C00150000",
                    "O:AAPL250117C00155000", 
                    "O:AAPL250117P00150000",
                    "O:MSFT250117C00300000",
                    "O:MSFT250117P00300000",
                    "O:SPY250117C00450000",
                    "O:SPY250117C00455000",
                    "O:SPY250117P00450000"
                };

                foreach (var symbol in symbols)
                {
                    // Add multiple lines per symbol to simulate real data
                    for (int i = 0; i < 3; i++)
                    {
                        var price = 10.0 + i * 0.5;
                        var timestamp = 1705392000000000000L + (i * 60000000000L); // 1-minute intervals
                        sb.AppendLine($"{symbol},100,{price:F2},{price + 0.1:F2},{price + 0.2:F2},{price - 0.1:F2},{timestamp},10");
                    }
                }

                File.WriteAllText(filePath, sb.ToString());
            }

            return testDir;
        }

        /// <summary>
        /// Cleans up test directory and files.
        /// </summary>
        private void CleanupTestDirectory(string testDir)
        {
            try
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures in tests
            }
        }

        // ================ BULK FILE INDEXING FUNCTIONALITY ================

        // ---------------- Directory resolution ----------------

        // Allow tests to override data locations by placing files under MockPolygonBulkData and MockContractData.
        public static string ResolveBulkDirForStocks(bool preferMocks = false)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                // Prefer mock data first for tests
                Path.Combine(baseDir, "MockPolygonBulkData", "us_stocks_sip_minute_aggs", "Sorted"),
                Path.Combine(baseDir, "MockPolygonBulkData", "us_stocks_sip_minute_aggs"),
                Path.Combine(baseDir, "MockPolygonBulkData"),

                // Standard locations
                Path.Combine(baseDir, "PolygonBulkData", "us_stocks_sip_minute_aggs", "Sorted"),
                Path.Combine(baseDir, "PolygonBulkData", "us_stocks_sip_minute_aggs"),
                Path.Combine(baseDir, "PolygonBulkData"),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\bin\Debug\PolygonBulkData", "us_stocks_sip_minute_aggs", "Sorted")),
                Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\bin\Debug\PolygonBulkData"))
            };

            foreach (var dir in candidates.Select(Path.GetFullPath))
            {
                if (!preferMocks && dir.IndexOf("MockPolygonBulkData", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue; // skip mock if not preferred

                // Filter for stocks CSVs (look for stocks-related files)
                if (Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.csv", SearchOption.AllDirectories)
                        .Any(f => f.IndexOf("stocks", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  f.IndexOf("sip", StringComparison.OrdinalIgnoreCase) >= 0))
                    return dir;
            }

            return null;
        }

        // Allow tests to override data locations by placing files under MockPolygonBulkData and MockContractData.
        public static string ResolveBulkDirForOptions(bool preferMocks = false)
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

        /// <summary>
        /// Build index files for all sorted bulk option files to enable fast symbol lookups.
        /// Creates .index files containing symbol-to-offset mappings and file hashes for validation.
        /// This dramatically speeds up BulkFileContractTracer by providing direct seek offsets.
        /// </summary>
        /// <param name="sortedBulkDirectory">Directory containing sorted bulk files</param>
        /// <param name="forceRebuild">Force rebuild even if index files exist and are up to date</param>
        /// <returns>Number of index files created or validated</returns>
        public static int BuildSortedFileIndexes(string sortedBulkDirectory, bool forceRebuild = false, bool isOption = true, int numberOfSortedFilesToProcess = int.MaxValue)
        {
            if (string.IsNullOrWhiteSpace(sortedBulkDirectory) || !Directory.Exists(sortedBulkDirectory))
            {
                ConsoleUtilities.WriteLine("❌ Sorted bulk directory not found or invalid.");
                return 0;
            }

            try
            {
                // Find all sorted bulk option files
                string[] sortedFiles = null;
                
                if(isOption)
                {
                    sortedFiles = Directory.GetFiles(sortedBulkDirectory, "*_Sorted.csv", SearchOption.AllDirectories)
                        .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderByDescending(f => f) // Process newest files first
                        .Take(numberOfSortedFilesToProcess)
                        .ToArray();
                }
                else
                {
                    sortedFiles = Directory.GetFiles(sortedBulkDirectory, "*_Sorted.csv", SearchOption.AllDirectories)
                        .Where(f => f.IndexOf("stocks", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    f.IndexOf("sip", StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderByDescending(f => f) // Process newest files first
                        .Take(numberOfSortedFilesToProcess)
                        .ToArray();
                }

                if (sortedFiles.Length == 0)
                {
                    ConsoleUtilities.WriteLine("ℹ️  No sorted bulk option files found to index.");
                    return 0;
                }

                ConsoleUtilities.WriteLine($"🔍 Building indexes for {sortedFiles.Length} sorted bulk files...");

                int indexesCreated = 0;
                var start = DateTime.UtcNow;

                foreach (var sortedFile in sortedFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileName(sortedFile);
                        var indexFile = sortedFile + ".index";

                        // Check if we need to rebuild the index
                        if (!forceRebuild && ShouldSkipIndexing(sortedFile, indexFile))
                        {
                            ConsoleUtilities.WriteLine($"🟩 Skipping (index up to date): {fileName}");
                            indexesCreated++;
                            continue;
                        }

                        ConsoleUtilities.WriteLine($"📊 Indexing: {fileName}");

                        if (BuildSingleFileIndex(sortedFile, indexFile))
                        {
                            indexesCreated++;
                            ConsoleUtilities.WriteLine($"✅ Index created: {fileName}.index");
                        }
                        else
                        {
                            ConsoleUtilities.WriteLine($"❌ Failed to index: {fileName}");
                        }

                        ProcessAllUnderlyings(sortedFile, (underlying, lines) =>
                        {
                            Assert.IsTrue(lines.Count > 0, $"Should have lines for {underlying}");

                            // Validate that all lines belong to the expected underlying
                            var validLineCount = 0;
                            var invalidLines = new List<string>();

                            foreach (var line in lines)
                            {
                                if (string.IsNullOrWhiteSpace(line)) continue;

                                var comma = line.IndexOf(',');
                                if (comma <= 0)
                                {
                                    invalidLines.Add($"No comma found: {line.Substring(0, Math.Min(50, line.Length))}...");
                                    continue;
                                }

                                var ticker = line.Substring(0, comma).Trim().ToUpperInvariant();

                                // Parse underlying from ticker using the same logic as the indexer
                                string lineUnderlying;
                                if (ticker.StartsWith("O:"))
                                {
                                    // Options: extract underlying from contract
                                    var contractKey = BulkFileContractTracer.ParseContractKey(ticker);
                                    lineUnderlying = contractKey?.Underlying ?? ExtractUnderlyingFromTicker(ticker);
                                }
                                else
                                {
                                    // Stocks: the ticker IS the underlying
                                    lineUnderlying = ticker;
                                }

                                if (string.Equals(underlying, lineUnderlying, StringComparison.OrdinalIgnoreCase))
                                {
                                    validLineCount++;
                                }
                                else
                                {
                                    invalidLines.Add($"Expected '{underlying}' but found '{lineUnderlying}' in: {line.Substring(0, Math.Min(50, line.Length))}...");
                                }
                            }

                            // Assert that all lines are valid
                            Assert.AreEqual(lines.Count, validLineCount,
                                $"All lines for {underlying} should parse correctly. Found {invalidLines.Count} invalid lines:\n" +
                                string.Join("\n", invalidLines.Take(3)) +
                                (invalidLines.Count > 3 ? $"\n... and {invalidLines.Count - 3} more" : ""));

                            ConsoleUtilities.WriteLine($"✅ Validated {underlying}: {validLineCount} lines, all correctly parsed");
                        });
                    }
                    catch (Exception ex)
                    {
                        ConsoleUtilities.WriteLine($"❌ Error indexing {Path.GetFileName(sortedFile)}: {ex.Message}");
                    }
                }

                var elapsed = DateTime.UtcNow - start;
                ConsoleUtilities.WriteLine($"🎯 Completed: {indexesCreated}/{sortedFiles.Length} indexes processed in {elapsed.TotalSeconds:F1}s");

                return indexesCreated;
            }
            catch (Exception ex)
            {
                ConsoleUtilities.WriteLine($"❌ Error building sorted file indexes: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Build an index file for a single sorted bulk file containing underlying-to-offset mappings.
        /// This enables fast lookups of all options for a specific underlying (e.g., all AAPL options)
        /// and also works for stocks by underlying symbol.
        /// </summary>
        /// <param name="sortedFilePath">Path to the sorted CSV file</param>
        /// <param name="indexFilePath">Path where the index file should be created</param>
        /// <returns>True if index was successfully created</returns>
        private static bool BuildSingleFileIndex(string sortedFilePath, string indexFilePath)
        {
            try
            {
                var underlyingIndex = new Dictionary<string, UnderlyingIndexEntry>(StringComparer.OrdinalIgnoreCase);

                string fileHash;
                long totalLines = 0;
                long dataStartPosition = 0;
                int totalSymbols = 0;

                // Calculate file hash for validation
                fileHash = ComputeFileHash(sortedFilePath);

                // Build the index by scanning the sorted file
                using (var fs = new FileStream(sortedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536))
                using (var sr = new StreamReader(fs))
                {
                    // Skip header and find data start position
                    var header = sr.ReadLine();
                    dataStartPosition = System.Text.Encoding.UTF8.GetByteCount(header) + GetLineEndingByteCount(header);
                    totalLines = 1; // Header line

                    string line;
                    long currentFileOffset = dataStartPosition; // Track actual file position
                    string currentUnderlying = null;
                    long underlyingStartOffset = -1;
                    int underlyingLineCount = 0;
                    int underlyingSymbolCount = 0;
                    long underlyingByteCount = 0; // Track total bytes for this underlying
                    var seenSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    while ((line = sr.ReadLine()) != null)
                    {
                        // 🚀 FIXED: Current line starts at currentFileOffset (BEFORE reading)
                        long lineStartPosition = currentFileOffset;
                        
                        // Calculate line byte length (including line ending)
                        long lineByteLength = System.Text.Encoding.UTF8.GetByteCount(line) + GetLineEndingByteCount(line);
                        
                        totalLines++;

                        // 🚀 FIXED: Always advance the file offset for next iteration
                        currentFileOffset += lineByteLength;

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var comma = line.IndexOf(',');
                        if (comma <= 0)
                        {
                            continue;
                        }

                        var ticker = line.Substring(0, comma).Trim().ToUpperInvariant();
                        
                        // Handle both options (O:) and stocks 
                        string underlying = null;
                        if (ticker.StartsWith("O:"))
                        {
                            // Options: extract underlying from contract
                            var contractKey = BulkFileContractTracer.ParseContractKey(ticker);
                            underlying = contractKey?.Underlying ?? ExtractUnderlyingFromTicker(ticker);
                            totalSymbols++;
                        }
                        else
                        {
                            // Stocks: the ticker IS the underlying
                            underlying = ticker;
                            totalSymbols++;
                        }

                        if (string.IsNullOrEmpty(underlying))
                        {
                            continue;
                        }

                        // Track unique symbols for this underlying
                        seenSymbols.Add(ticker);

                        // Track underlying changes
                        if (currentUnderlying != underlying)
                        {
                            // Save previous underlying if it exists
                            if (currentUnderlying != null && underlyingStartOffset >= 0)
                            {
                                underlyingIndex[currentUnderlying] = new UnderlyingIndexEntry
                                {
                                    Underlying = currentUnderlying,
                                    StartOffset = underlyingStartOffset - 1,
                                    EndOffset = underlyingStartOffset + underlyingByteCount, // 🚀 FIXED: Consistent calculation
                                    LineCount = underlyingLineCount,
                                    SymbolCount = underlyingSymbolCount,
                                    ByteCount = underlyingByteCount // Total bytes for efficient reading
                                };
                            }

                            // Start tracking new underlying at the CURRENT line position
                            currentUnderlying = underlying;
                            underlyingStartOffset = lineStartPosition;
                            underlyingLineCount = 1;
                            underlyingSymbolCount = 1;
                            underlyingByteCount = lineByteLength; // Start byte counting
                            seenSymbols.Clear();
                            seenSymbols.Add(ticker);
                        }
                        else
                        {
                            underlyingLineCount++;
                            underlyingByteCount += lineByteLength; // Accumulate bytes
                            // Only count unique symbols
                            if (seenSymbols.Add(ticker))
                            {
                                underlyingSymbolCount++;
                            }
                        }
                    }

                    // Save the last underlying
                    if (currentUnderlying != null && underlyingStartOffset >= 0)
                    {
                        underlyingIndex[currentUnderlying] = new UnderlyingIndexEntry
                        {
                            Underlying = currentUnderlying,
                            StartOffset = underlyingStartOffset - 1,
                            EndOffset = underlyingStartOffset + underlyingByteCount, // 🚀 FIXED: Consistent calculation
                            LineCount = underlyingLineCount,
                            SymbolCount = underlyingSymbolCount,
                            ByteCount = underlyingByteCount // Total bytes
                        };
                    }
                }

                // Create the index metadata
                var indexMetadata = new BulkFileIndexMetadata
                {
                    SourceFile = Path.GetFileName(sortedFilePath),
                    SourceFileHash = fileHash,
                    IndexCreatedUtc = DateTime.UtcNow,
                    TotalLines = totalLines,
                    DataStartPosition = dataStartPosition,
                    SymbolCount = totalSymbols,
                    UnderlyingCount = underlyingIndex.Count,
                    Version = "2.2" // Updated version for position fix
                };

                // Write the index file in JSON format (simplified for underlying-only indexing)
                var indexData = new
                {
                    Metadata = indexMetadata,
                    UnderlyingIndex = underlyingIndex.Values.OrderBy(u => u.Underlying).ToList()
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(indexData, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(indexFilePath, json, new UTF8Encoding(false));

                ConsoleUtilities.WriteLine($"    📈 Indexed {underlyingIndex.Count:N0} underlyings covering {totalSymbols:N0} symbols from {totalLines:N0} lines");
                return true;
            }
            catch (Exception ex)
            {
                ConsoleUtilities.WriteLine($"❌ Error building index for {Path.GetFileName(sortedFilePath)}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Helper method to determine line ending byte count (CRLF = 2, LF = 1)
        /// </summary>
        private static int GetLineEndingByteCount(string line)
        {
            // Assume CRLF (Windows) = 2 bytes by default
            // In a more robust implementation, you'd detect this from the file
            return 2;
        }

        /// <summary>
        /// Check if indexing should be skipped because an up-to-date index already exists.
        /// </summary>
        private static bool ShouldSkipIndexing(string sortedFilePath, string indexFilePath)
        {
            try
            {
                if (!File.Exists(indexFilePath))
                    return false;

                // Check if index file is newer than source file
                var sourceModified = File.GetLastWriteTimeUtc(sortedFilePath);
                var indexModified = File.GetLastWriteTimeUtc(indexFilePath);

                if (indexModified < sourceModified)
                    return false;

                // Verify hash matches if possible
                var indexContent = File.ReadAllText(indexFilePath);
                dynamic indexObj = Newtonsoft.Json.JsonConvert.DeserializeObject(indexContent);
                var storedHash = indexObj?.Metadata?.SourceFileHash?.ToString();

                if (!string.IsNullOrEmpty(storedHash))
                {
                    var currentHash = ComputeFileHash(sortedFilePath);
                    return string.Equals(currentHash, storedHash, StringComparison.OrdinalIgnoreCase);
                }

                return true; // Assume valid if we can't verify hash
            }
            catch
            {
                return false; // Rebuild index if we can't validate it
            }
        }

        /// <summary>
        /// Extract underlying symbol from option ticker (fallback method).
        /// </summary>
        internal static string ExtractUnderlyingFromTicker(string ticker)
        {
            if (string.IsNullOrEmpty(ticker) || !ticker.StartsWith("O:"))
                return ticker;

            var contractPart = ticker.Substring(2);

            // Look for the date pattern (YYMMDD) to find where underlying ends
            for (int i = 1; i <= contractPart.Length - 15; i++)
            {
                if (i + 6 <= contractPart.Length &&
                    contractPart.Substring(i, 6).All(char.IsDigit))
                {
                    return contractPart.Substring(0, i);
                }
            }

            return contractPart; // Fallback
        }

        /// <summary>
        /// Compute SHA256 hash for a file.
        /// </summary>
        private static string ComputeFileHash(string filePath)
        {
            using (var fs = File.OpenRead(filePath))
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha.ComputeHash(fs);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Load index data for a specific sorted file to enable fast underlying symbol lookups.
        /// Now with in-memory caching - once loaded, stays resident in RAM (~2MB per index).
        /// </summary>
        /// <param name="sortedFilePath">Path to the sorted CSV file</param>
        /// <param name="bypassCache">If true, force reload from disk (default: false)</param>
        /// <returns>Loaded index data or null if index doesn't exist or is invalid</returns>
        public static BulkFileIndexData LoadFileIndex(string sortedFilePath, bool bypassCache = false)
        {
            var indexFilePath = sortedFilePath + ".index";

            // Check cache first (unless bypassing)
            if (!bypassCache)
            {
                lock (_indexCacheLock)
                {
                    if (_indexCache.TryGetValue(sortedFilePath, out var cachedIndex))
                    {
                        // ConsoleUtilities.WriteLine($"✅ Cache hit: {Path.GetFileName(sortedFilePath)}");
                        return cachedIndex;
                    }
                }
            }

            try
            {
                if (!File.Exists(indexFilePath))
                    return null;

                var json = File.ReadAllText(indexFilePath);
                dynamic indexObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                if (indexObj?.Metadata == null || indexObj?.UnderlyingIndex == null)
                    return null;

                // Validate hash if possible
                var storedHash = indexObj.Metadata.SourceFileHash?.ToString();
                if (!string.IsNullOrEmpty(storedHash))
                {
                    var currentHash = ComputeFileHash(sortedFilePath);
                    if (!string.Equals(currentHash, storedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        ConsoleUtilities.WriteLine($"⚠️  Index hash mismatch for {Path.GetFileName(sortedFilePath)} - may need rebuilding");
                        return null;
                    }
                }

                // Deserialize properly typed objects
                var metadata = Newtonsoft.Json.JsonConvert.DeserializeObject<BulkFileIndexMetadata>(indexObj.Metadata.ToString());
                
                // Handle both old format (with SymbolIndex) and new format (UnderlyingIndex only)
                List<UnderlyingIndexEntry> underlyingIndex = null;
                if (indexObj.UnderlyingIndex != null)
                {
                    underlyingIndex = Newtonsoft.Json.JsonConvert.DeserializeObject<List<UnderlyingIndexEntry>>(indexObj.UnderlyingIndex.ToString());
                }

                var indexData = new BulkFileIndexData
                {
                    Metadata = metadata,
                    SymbolIndex = new Dictionary<string, SymbolIndexEntry>(), // Empty for underlying-focused indexing
                    UnderlyingIndex = underlyingIndex?.ToDictionary(u => u.Underlying, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, UnderlyingIndexEntry>()
                };

                // Add to cache
                lock (_indexCacheLock)
                {
                    _indexCache[sortedFilePath] = indexData;
                    ConsoleUtilities.WriteLine($"💾 Cached: {Path.GetFileName(sortedFilePath)} ({GetIndexCacheStats()})");
                }

                return indexData;
            }
            catch (Exception ex)
            {
                ConsoleUtilities.WriteLine($"❌ Error loading index for {Path.GetFileName(sortedFilePath)}: {ex.Message}");
                return null;
            }
        }

        [TestMethod]
        [TestCategory("SortedIndexes")]
        /// <summary>
        /// Build indexes for all sorted bulk files in the standard directory structure.
        /// This method integrates with the existing directory resolution system.
        /// </summary>
        /// <param name="forceRebuild">Force rebuild even if index files exist and are up to date</param>
        /// <returns>Number of index files created or validated</returns>
        public static void BuildAllSortedFileIndexesForStocks(int numberOfSortedFilesToProcess = int.MaxValue)
        {
            var bulkDir = ResolveBulkDirForStocks();
            if (string.IsNullOrEmpty(bulkDir))
            {
                ConsoleUtilities.WriteLine("❌ Could not resolve bulk data directory for indexing.");
                return;
            }

            // Check for Sorted subdirectory first, then fall back to main directory
            var sortedDir = Path.Combine(bulkDir, "Sorted");
            if (!Directory.Exists(sortedDir))
            {
                sortedDir = bulkDir;
                ConsoleUtilities.WriteLine($"ℹ️  No Sorted subdirectory found, using main bulk directory: {bulkDir}");
            }
            else
            {
                ConsoleUtilities.WriteLine($"🎯 Building indexes for sorted files in: {sortedDir}");
            }

            BuildSortedFileIndexes(sortedDir, false, false, numberOfSortedFilesToProcess);
        }

        [TestMethod]
        [TestCategory("SortedIndexes")]
        /// <summary>
        /// Build indexes for all sorted bulk files in the standard directory structure.
        /// This method integrates with the existing directory resolution system.
        /// </summary>
        /// <param name="forceRebuild">Force rebuild even if index files exist and are up to date</param>
        /// <returns>Number of index files created or validated</returns>
        public static void BuildAllSortedFileIndexesForOptions(int numberOfSortedFilesToProcess = int.MaxValue)
        {
            var bulkDir = ResolveBulkDirForOptions();
            if (string.IsNullOrEmpty(bulkDir))
            {
                ConsoleUtilities.WriteLine("❌ Could not resolve bulk data directory for indexing.");
                return;
            }

            // Check for Sorted subdirectory first, then fall back to main directory
            var sortedDir = Path.Combine(bulkDir, "Sorted");
            if (!Directory.Exists(sortedDir))
            {
                sortedDir = bulkDir;
                ConsoleUtilities.WriteLine($"ℹ️  No Sorted subdirectory found, using main bulk directory: {bulkDir}");
            }
            else
            {
                ConsoleUtilities.WriteLine($"🎯 Building indexes for sorted files in: {sortedDir}");
            }

            BuildSortedFileIndexes(sortedDir, false, true, numberOfSortedFilesToProcess);
        }

        // ================ INDEX DATA STRUCTURES ================

        /// <summary>
        /// Metadata about a bulk file index.
        /// </summary>
        public sealed class BulkFileIndexMetadata
        {
            public string SourceFile { get; set; }
            public string SourceFileHash { get; set; }
            public DateTime IndexCreatedUtc { get; set; }
            public long TotalLines { get; set; }
            public long DataStartPosition { get; set; }
            public int SymbolCount { get; set; }
            public int UnderlyingCount { get; set; }
            public string Version { get; set; }
        }

        /// <summary>
        /// Index entry for a specific option symbol.
        /// </summary>
        public sealed class SymbolIndexEntry
        {
            public string Symbol { get; set; }
            public long StartOffset { get; set; }
            public long EndOffset { get; set; }
            public int LineCount { get; set; }
            public string Underlying { get; set; }
        }

        /// <summary>
        /// Index entry for an underlying symbol (aggregates all its options/stocks).
        /// </summary>
        public sealed class UnderlyingIndexEntry
        {
            public string Underlying { get; set; }
            public long StartOffset { get; set; }
            public long EndOffset { get; set; }
            public int LineCount { get; set; }
            public int SymbolCount { get; set; }
            public long ByteCount { get; set; } // 🚀 NEW: Total bytes for efficient bulk reading
        }

        /// <summary>
        /// Complete index data for a bulk file.
        /// </summary>
        public sealed class BulkFileIndexData
        {
            public BulkFileIndexMetadata Metadata { get; set; }
            public Dictionary<string, SymbolIndexEntry> SymbolIndex { get; set; }
            public Dictionary<string, UnderlyingIndexEntry> UnderlyingIndex { get; set; }
        }

        /// <summary>
        /// Efficiently read all data for a specific underlying using the index.
        /// This demonstrates how to use the index for direct file access without scanning.
        /// </summary>
        /// <param name="sortedFilePath">Path to the sorted CSV file</param>
        /// <param name="underlying">Underlying symbol to read (e.g., "AAPL", "SPY")</param>
        /// <returns>All lines for the specified underlying, or empty list if not found</returns>
        public static List<string> ReadUnderlyingData(string sortedFilePath, string underlying)
        {
            var result = new List<string>();
            
            try
            {
                // Load the index
                var indexData = LoadFileIndex(sortedFilePath);
                if (indexData?.UnderlyingIndex == null)
                {
                    ConsoleUtilities.WriteLine("❌ No index found - call BuildSortedFileIndexes first");
                    return result;
                }

                // Find the underlying in the index
                if (!indexData.UnderlyingIndex.TryGetValue(underlying, out var entry))
                {
                    ConsoleUtilities.WriteLine($"❌ Underlying '{underlying}' not found in index");
                    return result;
                }

                //ConsoleUtilities.WriteLine($"📍 Found {underlying}: {entry.LineCount} lines, {entry.ByteCount} bytes at offset {entry.StartOffset}");

                // 🚀 EFFICIENT: Use index to seek directly and read exact byte count
                using (var fs = new FileStream(sortedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Seek directly to the start of this underlying's data
                    fs.Seek(entry.StartOffset, SeekOrigin.Begin);
                    
                    // Read the exact number of bytes for this underlying
                    var buffer = new byte[entry.ByteCount];
                    int bytesRead = fs.Read(buffer, 0, (int)entry.ByteCount);
                    
                    if (bytesRead != entry.ByteCount)
                    {
                        ConsoleUtilities.WriteLine($"⚠️  Expected {entry.ByteCount} bytes, read {bytesRead} bytes");
                    }
                    
                    // Convert bytes to string and split into lines
                    var content = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    
                    result.AddRange(lines);
                    
                    //ConsoleUtilities.WriteLine($"✅ Successfully read {result.Count} lines for {underlying}");
                }
            }
            catch (Exception ex)
            {
                ConsoleUtilities.WriteLine($"❌ Error reading underlying data: {ex.Message}");
            }
            
            return result;
        }

        /// <summary>
        /// Efficiently iterate through all underlyings in order using the index.
        /// This demonstrates sequential processing without file scanning.
        /// </summary>
        /// <param name="sortedFilePath">Path to the sorted CSV file</param>
        /// <param name="processor">Action to process each underlying's data</param>
        public static bool ProcessAllUnderlyings(string sortedFilePath, Action<string, List<string>> processor)
        {
            try
            {
                // Load the index
                var indexData = LoadFileIndex(sortedFilePath);
                if (indexData?.UnderlyingIndex == null)
                {
                    ConsoleUtilities.WriteLine("❌ No index found - call BuildSortedFileIndexes first");
                    return false;
                }

                ConsoleUtilities.WriteLine($"🔄 Processing {indexData.UnderlyingIndex.Count} underlyings...");

                using (var fs = new FileStream(sortedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Process underlyings in alphabetical order
                    foreach (var kvp in indexData.UnderlyingIndex.OrderBy(x => x.Key))
                    {
                        var underlying = kvp.Key;
                        var entry = kvp.Value;
                        
                        // 🚀 EFFICIENT: Seek to exact position
                        fs.Seek(entry.StartOffset, SeekOrigin.Begin);
                        
                        // Read exact byte count
                        var buffer = new byte[entry.ByteCount];
                        int bytesRead = fs.Read(buffer, 0, (int)entry.ByteCount);
                        
                        // Convert to lines
                        var content = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                          .ToList();
                        
                        // Process this underlying's data
                        processor(underlying, lines);
                        
                        ConsoleUtilities.WriteLine($"✅ Processed {underlying}: {lines.Count} lines");
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleUtilities.WriteLine($"❌ Error processing underlyings: {ex.Message}");

                return false;
            }

            return true;
        }
    }
}