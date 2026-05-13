using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trade.IVPreCalc2;

namespace Trade.Tests
{
    /// <summary>
    /// Comprehensive tests for BulkFileContractTracer functionality.
    /// Tests the critical contract search and cut-bait optimization logic.
    /// </summary>
    [TestClass]
    public class BulkFileContractTracerTests
    {
        private string _testDataDirectory;
        private string _testFile1Path;
        private string _testFile2Path;

        [TestInitialize]
        public void Setup()
        {
            _testDataDirectory = Path.Combine(Path.GetTempPath(), "BulkFileContractTracerTests_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_testDataDirectory);

            _testFile1Path = Path.Combine(_testDataDirectory, "2025-01-16_options.csv");
            _testFile2Path = Path.Combine(_testDataDirectory, "2025-01-15_options.csv");

            CreateTestDataFiles();
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_testDataDirectory))
                    Directory.Delete(_testDataDirectory, true);
            }
            catch { }
        }

        #region Test Data Creation

        /// <summary>
        /// Creates test CSV files with sorted contract data for testing.
        /// Uses realistic option contract tickers in proper sort order.
        /// </summary>
        private void CreateTestDataFiles()
        {
            var header = "ticker,volume,open,close,high,low,window_start,transactions";

            // File 1: 2025-01-16 data - contracts in proper sort order
            var file1Data = new StringBuilder();
            file1Data.AppendLine(header);
            
            // AAPL contracts (should come first alphabetically)
            file1Data.AppendLine("O:AAPL250117C00220000,100,10.50,10.75,10.80,10.45,1737032400000000000,50");
            file1Data.AppendLine("O:AAPL250117C00225000,75,8.25,8.50,8.55,8.20,1737032460000000000,35");
            file1Data.AppendLine("O:AAPL250117P00220000,50,5.25,5.50,5.60,5.20,1737032520000000000,25");
            file1Data.AppendLine("O:AAPL250117P00225000,25,3.75,4.00,4.10,3.70,1737032580000000000,15");

            // SPY contracts (should come after AAPL)
            file1Data.AppendLine("O:SPY250117C00575000,200,12.50,12.75,12.85,12.40,1737032640000000000,100");
            file1Data.AppendLine("O:SPY250117C00580000,150,10.25,10.50,10.60,10.20,1737032700000000000,75");
            file1Data.AppendLine("O:SPY250117P00575000,100,8.75,9.00,9.10,8.70,1737032760000000000,50");
            file1Data.AppendLine("O:SPY250117P00580000,75,6.50,6.75,6.85,6.45,1737032820000000000,40");

            // TSLA contracts (should come after SPY)
            file1Data.AppendLine("O:TSLA250117C00250000,80,15.25,15.50,15.60,15.20,1737032880000000000,45");
            file1Data.AppendLine("O:TSLA250117P00250000,60,12.75,13.00,13.10,12.70,1737032940000000000,30");

            File.WriteAllText(_testFile1Path, file1Data.ToString());

            // File 2: 2025-01-15 data - similar structure, different day
            var file2Data = new StringBuilder();
            file2Data.AppendLine(header);
            
            // Same contracts, different prices for the next day
            file2Data.AppendLine("O:AAPL250117C00220000,110,10.75,11.00,11.05,10.70,1737118800000000000,55");
            file2Data.AppendLine("O:SPY250117C00575000,220,12.75,13.00,13.10,12.65,1737118860000000000,110");
            file2Data.AppendLine("O:TSLA250117C00250000,90,15.50,15.75,15.85,15.45,1737118920000000000,50");

            File.WriteAllText(_testFile2Path, file2Data.ToString());
        }

        #endregion

        #region Basic Functionality Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Constructor_ValidDirectory_InitializesSuccessfully()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                Assert.IsNotNull(tracer);
                
                var newestDate = tracer.GetNewestBulkFileDateOrDefault(DateTime.MinValue);
                Assert.AreEqual(new DateTime(2025, 1, 16), newestDate);
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetNewestBulkFileDateOrDefault_WithValidFiles_ReturnsNewestDate()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                var result = tracer.GetNewestBulkFileDateOrDefault(new DateTime(2020, 1, 1));
                Assert.AreEqual(new DateTime(2025, 1, 16), result);
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetNewestBulkFileDateOrDefault_WithNoValidFiles_ReturnsFallback()
        {
            var emptyDir = Path.Combine(Path.GetTempPath(), "EmptyTestDir_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(emptyDir);

            try
            {
                using (var tracer = new BulkFileContractTracer(emptyDir))
                {
                    var fallback = new DateTime(2020, 5, 15);
                    var result = tracer.GetNewestBulkFileDateOrDefault(fallback);
                    Assert.AreEqual(fallback.Date, result);
                }
            }
            finally
            {
                Directory.Delete(emptyDir, true);
            }
        }

        #endregion

        #region Contract Search Tests

        [TestMethod]
        [TestCategory("Core")]
        public async Task TraceContractBackwardsAsync_ExistingContract_FindsCorrectData()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                var result = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117C00220000", 
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(result);
                Assert.AreEqual("O:AAPL250117C00220000", result.ContractSymbol);
                Assert.AreEqual(2, result.Prices.Count); // Should find data in both files
                Assert.IsTrue(result.FilesSearched > 0);
                Assert.IsTrue(result.SearchStats.TotalRecordsFound > 0);

                // Verify that LastFileSearched is populated
                Assert.IsNotNull(result.LastFileSearched, "LastFileSearched should not be null");
                Assert.IsTrue(result.LastFileSearched.Contains("2025-01"), "LastFileSearched should be a valid file path");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public async Task TraceContractBackwardsAsync_NonExistentContract_ReturnsEmptyResult()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                var result = await tracer.TraceContractBackwardsAsync(
                    "O:NONEXISTENT250117C00100000", 
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(result);
                Assert.AreEqual("O:NONEXISTENT250117C00100000", result.ContractSymbol);
                Assert.AreEqual(0, result.Prices.Count);
                Assert.AreEqual(0, result.SearchStats.TotalRecordsFound);

                // Verify that LastFileSearched is still populated even when no matches found
                Assert.IsNotNull(result.LastFileSearched, "LastFileSearched should not be null even with no matches");
                Assert.IsTrue(result.LastFileSearched.Contains("2025-01"), "LastFileSearched should be a valid file path");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public async Task TraceContractBackwardsAsync_DateRangeFiltering_RespectsDateBounds()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                // Search only for 2025-01-15 (should exclude 2025-01-16 file)
                var result = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117C00220000", 
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 15));

                Assert.IsNotNull(result);
                Assert.AreEqual(1, result.Prices.Count); // Should find data in only one file
                Assert.AreEqual(new DateTime(2025, 1, 15), result.Prices[0].Date);
            }
        }

        #endregion

        #region Cut-Bait Optimization Tests

        [TestMethod]
        [TestCategory("Core")]
        public async Task SearchOptimization_CallBeforePut_CutsBaitCorrectly()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                // Search for a call option - should stop when it hits puts for the same underlying/expiration
                var callResult = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117C00220000",
                    new DateTime(2025, 1, 15),
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(callResult);
                Assert.IsTrue(callResult.SearchStats.TotalRecordsFound > 0);

                // Now search for a put that should come after - verify it doesn't interfere
                var putResult = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117P00220000",
                    new DateTime(2025, 1, 15),
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(putResult);
                Assert.IsTrue(putResult.SearchStats.TotalRecordsFound > 0);
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public async Task SearchOptimization_CallBeforePut_CutsBaitCorrectly_AllContracts()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                // Search for a call option - should stop when it hits puts for the same underlying/expiration
                var callResult = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117C00220000", 
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(callResult);
                Assert.IsTrue(callResult.SearchStats.TotalRecordsFound > 0);

                var callResult2 = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117C00225000",
                    new DateTime(2025, 1, 15),
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(callResult2);
                Assert.IsTrue(callResult2.SearchStats.TotalRecordsFound > 0);

                // Now search for a put that should come after - verify it doesn't interfere
                var putResult = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117P00220000", 
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 16));

                var putResult2 = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117P00225000",
                    new DateTime(2025, 1, 15),
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(putResult2);
                Assert.IsTrue(putResult2.SearchStats.TotalRecordsFound > 0);
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public async Task SearchOptimization_DifferentUnderlying_CutsBaitAtUnderlyingBoundary()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                // Search for AAPL - should stop before SPY contracts
                var aaplResult = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117C00220000", 
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(aaplResult);
                Assert.IsTrue(aaplResult.SearchStats.TotalRecordsFound > 0);

                // Search for SPY - should not see AAPL data
                var spyResult = await tracer.TraceContractBackwardsAsync(
                    "O:SPY250117C00575000", 
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(spyResult);
                Assert.IsTrue(spyResult.SearchStats.TotalRecordsFound > 0);
                
                // Verify the results don't overlap
                Assert.AreNotEqual(aaplResult.ContractSymbol, spyResult.ContractSymbol);
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public async Task SearchOptimization_StrikeOrdering_CutsBaitAtStrikeBoundary()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                // Search for lower strike
                var lowerStrike = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117C00220000", 
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 16));

                // Search for higher strike  
                var higherStrike = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117C00225000", 
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(lowerStrike);
                Assert.IsNotNull(higherStrike);
                Assert.IsTrue(lowerStrike.SearchStats.TotalRecordsFound > 0);
                Assert.IsTrue(higherStrike.SearchStats.TotalRecordsFound > 0);
            }
        }

        #endregion

        #region Contract Parsing Tests

        [TestMethod]
        [TestCategory("Core")]
        public void ParseContractKey_ValidCallOption_ParsesCorrectly()
        {
            // Test the ParseContractKey method through reflection since it's private
            var tracerType = typeof(BulkFileContractTracer);
            var parseMethod = tracerType.GetMethod("ParseContractKey", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            Assert.IsNotNull(parseMethod, "ParseContractKey method should exist");

            var result = parseMethod.Invoke(null, new object[] { "O:SPY250117C00575000" });
            
            Assert.IsNotNull(result, "Should successfully parse valid contract");
            var contractKey = result as Trade.Polygon2.ContractKey;
            
            Assert.IsNotNull(contractKey);
            Assert.AreEqual("SPY", contractKey.Underlying);
            Assert.IsTrue(contractKey.IsCall);
            Assert.AreEqual(new DateTime(2025, 1, 17), contractKey.Expiration);
            Assert.AreEqual(575.0, contractKey.Strike);
            Assert.AreEqual("O:SPY250117C00575000", contractKey.RawTicker);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ParseContractKey_ValidPutOption_ParsesCorrectly()
        {
            var tracerType = typeof(BulkFileContractTracer);
            var parseMethod = tracerType.GetMethod("ParseContractKey", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            var result = parseMethod.Invoke(null, new object[] { "O:AAPL250117P00220000" });
            var contractKey = result as Trade.Polygon2.ContractKey;
            
            Assert.IsNotNull(contractKey);
            Assert.AreEqual("AAPL", contractKey.Underlying);
            Assert.IsFalse(contractKey.IsCall);
            Assert.AreEqual(new DateTime(2025, 1, 17), contractKey.Expiration);
            Assert.AreEqual(220.0, contractKey.Strike);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ParseContractKey_InvalidTicker_ReturnsNull()
        {
            var tracerType = typeof(BulkFileContractTracer);
            var parseMethod = tracerType.GetMethod("ParseContractKey", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            // Test various invalid formats
            var invalidTickers = new[]
            {
                null,
                "",
                "INVALID",
                "SPY250117C00575000", // Missing O: prefix
                "O:SPY", // Too short
                "O:SPY250117X00575000", // Invalid option type
                "O:SPY250117C0057500", // Invalid strike format
                "O:SPY2501XX00575000" // Invalid date
            };

            foreach (var ticker in invalidTickers)
            {
                var result = parseMethod.Invoke(null, new object[] { ticker });
                Assert.IsNull(result, $"Should return null for invalid ticker: {ticker}");
            }
        }

        #endregion

        #region Performance and Stress Tests

        [TestMethod]
        [TestCategory("Performance")]
        public async Task Performance_MultipleContractSearches_CompletesReasonably()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                var contracts = new[]
                {
                    "O:AAPL250117C00220000",
                    "O:AAPL250117C00225000", 
                    "O:SPY250117C00575000",
                    "O:SPY250117C00580000",
                    "O:TSLA250117C00250000"
                };

                var startTime = DateTime.UtcNow;

                foreach (var contract in contracts)
                {
                    var result = await tracer.TraceContractBackwardsAsync(
                        contract,
                        new DateTime(2025, 1, 15),
                        new DateTime(2025, 1, 16));
                    
                    Assert.IsNotNull(result);
                }

                var elapsed = DateTime.UtcNow - startTime;
                Assert.IsTrue(elapsed < TimeSpan.FromSeconds(10), 
                    $"Multiple contract searches should complete within 10 seconds, took {elapsed}");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public async Task EdgeCase_SearchForContractNotInSortOrder_HandlesGracefully()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                // Search for a contract that would sort before our first contract
                var result = await tracer.TraceContractBackwardsAsync(
                    "O:A250117C00100000", // Should sort before AAPL
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(result);
                Assert.AreEqual(0, result.Prices.Count); // Should find no data
                Assert.AreEqual("O:A250117C00100000", result.ContractSymbol);
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public async Task EdgeCase_SearchForContractAfterAllData_HandlesGracefully()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                // Search for a contract that would sort after our last contract
                var result = await tracer.TraceContractBackwardsAsync(
                    "O:ZZZ250117C00100000", // Should sort after TSLA
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(result);
                Assert.AreEqual(0, result.Prices.Count); // Should find no data
                Assert.AreEqual("O:ZZZ250117C00100000", result.ContractSymbol);
            }
        }

        #endregion

        #region Data Integrity Tests

        [TestMethod]
        [TestCategory("Core")]
        public async Task DataIntegrity_AggregatedPrices_CalculatedCorrectly()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                var result = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117C00220000",
                    new DateTime(2025, 1, 15),
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(result);
                Assert.IsTrue(result.Prices.Count > 0);

                foreach (var dailyPrice in result.Prices)
                {
                    // Verify OHLC relationships
                    Assert.IsTrue(dailyPrice.High >= dailyPrice.Open, "High should be >= Open");
                    Assert.IsTrue(dailyPrice.High >= dailyPrice.Close, "High should be >= Close");
                    Assert.IsTrue(dailyPrice.Low <= dailyPrice.Open, "Low should be <= Open");
                    Assert.IsTrue(dailyPrice.Low <= dailyPrice.Close, "Low should be <= Close");
                    
                    // Verify volume and record count are positive
                    Assert.IsTrue(dailyPrice.Volume > 0, "Volume should be positive");
                    Assert.IsTrue(dailyPrice.RecordCount > 0, "Record count should be positive");
                    
                    // Verify timestamps make sense
                    Assert.IsTrue(dailyPrice.FirstTimestamp <= dailyPrice.LastTimestamp, 
                        "First timestamp should be <= Last timestamp");
                }
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public async Task DataIntegrity_SourceFileTracking_TracksCorrectly()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                var result = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117C00220000",
                    new DateTime(2025, 1, 15),
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(result);
                Assert.IsTrue(result.Prices.Count > 0);

                // Verify source file information is captured
                Assert.IsNotNull(result.FirstSourceFile);
                Assert.IsNotNull(result.LastSourceFile);

                foreach (var dailyPrice in result.Prices)
                {
                    Assert.IsNotNull(dailyPrice.FirstSourceFile);
                    Assert.IsNotNull(dailyPrice.LastSourceFile);
                    Assert.IsTrue(dailyPrice.SourceFiles.Count > 0);
                }
            }
        }

        #endregion

        #region LastFileSearched Tracking Tests

        [TestMethod]
        [TestCategory("Core")]
        public async Task LastFileSearched_SingleFileWithMatches_TracksCorrectly()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                // Search only for 2025-01-15 (should only search one file)
                var result = await tracer.TraceContractBackwardsAsync(
                    "O:AAPL250117C00220000", 
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 15));

                Assert.IsNotNull(result);
                Assert.AreEqual(1, result.Prices.Count); // Should find data in only one file

                // Verify FirstSourceFile, LastSourceFile, and LastFileSearched relationships
                Assert.IsNotNull(result.FirstSourceFile, "FirstSourceFile should not be null");
                Assert.IsNotNull(result.LastSourceFile, "LastSourceFile should not be null");
                Assert.IsNotNull(result.LastFileSearched, "LastFileSearched should not be null");

                // With only one file containing matches, FirstSourceFile and LastSourceFile should be the same
                Assert.AreEqual(result.FirstSourceFile, result.LastSourceFile, 
                    "FirstSourceFile and LastSourceFile should be the same when only one file has matches");

                // LastFileSearched should be the same as the source files in this case
                Assert.AreEqual(result.FirstSourceFile, result.LastFileSearched,
                    "LastFileSearched should match source file when match is found");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public async Task LastFileSearched_NoMatches_TracksLastSearchedFile()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                var result = await tracer.TraceContractBackwardsAsync(
                    "O:NONEXISTENT250117C00100000", 
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(result);
                Assert.AreEqual(0, result.Prices.Count); // Should find no data

                // When no matches are found, FirstSourceFile and LastSourceFile should be null
                Assert.IsNull(result.FirstSourceFile, "FirstSourceFile should be null when no matches found");
                Assert.IsNull(result.LastSourceFile, "LastSourceFile should be null when no matches found");

                // But LastFileSearched should still be populated with the last file we actually searched
                Assert.IsNotNull(result.LastFileSearched, "LastFileSearched should not be null even with no matches");
                Assert.IsTrue(result.LastFileSearched.Contains("options.csv"), 
                    "LastFileSearched should be a valid options file path");
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public async Task LastFileSearched_EarlyTermination_TracksCorrectFile()
        {
            using (var tracer = new BulkFileContractTracer(_testDataDirectory))
            {
                // Search for a contract that should cause early termination
                // (sorts before our test data, so cut-bait optimization should kick in)
                var result = await tracer.TraceContractBackwardsAsync(
                    "O:A250117C00100000", // Should sort before AAPL
                    new DateTime(2025, 1, 15), 
                    new DateTime(2025, 1, 16));

                Assert.IsNotNull(result);
                Assert.AreEqual(0, result.Prices.Count); // Should find no data due to cut-bait

                // FirstSourceFile and LastSourceFile should be null (no matches)
                Assert.IsNull(result.FirstSourceFile, "FirstSourceFile should be null when cut-bait terminates early");
                Assert.IsNull(result.LastSourceFile, "LastSourceFile should be null when cut-bait terminates early");

                // LastFileSearched should still show which file we searched before terminating
                Assert.IsNotNull(result.LastFileSearched, "LastFileSearched should show file searched before early termination");
                Assert.IsTrue(result.LastFileSearched.Contains("options.csv"), 
                    "LastFileSearched should be a valid options file even after early termination");
            }
        }

        #endregion
    }
}