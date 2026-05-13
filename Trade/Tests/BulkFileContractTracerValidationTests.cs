using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Trade.IVPreCalc2;

namespace Trade.Tests
{
    /// <summary>
    /// Simple validation tests for the BulkFileContractTracer cut-bait optimization.
    /// </summary>
    [TestClass]
    public class BulkFileContractTracerValidationTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public async Task CutBaitOptimization_BasicValidation_WorksCorrectly()
        {
            var testDir = Path.Combine(Path.GetTempPath(), "BulkFileTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            var testFile = Path.Combine(testDir, "2025-01-15_options.csv");

            try
            {
                Directory.CreateDirectory(testDir);

                // Create test data with sorted contract symbols to test cut-bait logic
                var testData = @"ticker,volume,open,close,high,low,window_start,transactions
O:AAPL250117C00220000,100,10.50,10.75,10.80,10.45,1737032400000000000,50
O:AAPL250117C00225000,75,8.25,8.50,8.55,8.20,1737032460000000000,35
O:AAPL250117P00220000,50,5.25,5.50,5.60,5.20,1737032520000000000,25
O:SPY250117C00575000,200,12.50,12.75,12.85,12.40,1737032640000000000,100
O:SPY250117C00580000,150,10.25,10.50,10.60,10.20,1737032700000000000,75
O:SPY250117P00575000,100,8.75,9.00,9.10,8.70,1737032760000000000,50";

                File.WriteAllText(testFile, testData, Encoding.UTF8);

                using (var tracer = new BulkFileContractTracer(testDir))
                {
                    var startDate = new DateTime(2025, 1, 15);
                    var endDate = new DateTime(2025, 1, 15);

                    // Test 1: Search for AAPL call - should find it
                    var aaplCallResult = await tracer.TraceContractBackwardsAsync("O:AAPL250117C00220000", startDate, endDate);
                    Assert.IsNotNull(aaplCallResult, "AAPL call result should not be null");
                    Assert.AreEqual(1, aaplCallResult.Prices.Count, "Should find 1 day of AAPL call data");
                    Assert.IsTrue(aaplCallResult.SearchStats.TotalRecordsFound > 0, "Should find AAPL call records");
                    Assert.IsNotNull(aaplCallResult.LastFileSearched, "LastFileSearched should be populated");

                    // Test 2: Search for SPY call - should find it and cut bait correctly
                    var spyCallResult = await tracer.TraceContractBackwardsAsync("O:SPY250117C00575000", startDate, endDate);
                    Assert.IsNotNull(spyCallResult, "SPY call result should not be null");
                    Assert.AreEqual(1, spyCallResult.Prices.Count, "Should find 1 day of SPY call data");
                    Assert.IsTrue(spyCallResult.SearchStats.TotalRecordsFound > 0, "Should find SPY call records");
                    Assert.IsNotNull(spyCallResult.LastFileSearched, "LastFileSearched should be populated");

                    // Test 3: Search for contract that doesn't exist but should cut bait properly
                    var nonExistentResult = await tracer.TraceContractBackwardsAsync("O:TSLA250117C00250000", startDate, endDate);
                    Assert.IsNotNull(nonExistentResult, "Non-existent contract result should not be null");
                    Assert.AreEqual(0, nonExistentResult.Prices.Count, "Should find 0 days for non-existent contract");
                    Assert.AreEqual(0, nonExistentResult.SearchStats.TotalRecordsFound, "Should find 0 records for non-existent contract");
                    
                    // Key test: LastFileSearched should be populated even when no matches found
                    Assert.IsNotNull(nonExistentResult.LastFileSearched, "LastFileSearched should be populated even with no matches");
                    Assert.IsNull(nonExistentResult.FirstSourceFile, "FirstSourceFile should be null when no matches");
                    Assert.IsNull(nonExistentResult.LastSourceFile, "LastSourceFile should be null when no matches");
                }

                ConsoleUtilities.WriteLine("? Cut-bait optimization validation passed");
                ConsoleUtilities.WriteLine("? LastFileSearched tracking validation passed");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(testDir))
                        Directory.Delete(testDir, true);
                }
                catch { /* Ignore cleanup failures */ }
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ContractKeyParsing_BasicValidation_WorksCorrectly()
        {
            // Use reflection to test the private ParseContractKey method
            var tracerType = typeof(BulkFileContractTracer);
            var parseMethod = tracerType.GetMethod("ParseContractKey", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            Assert.IsNotNull(parseMethod, "ParseContractKey method should exist");

            // Test valid contract parsing
            var result = parseMethod.Invoke(null, new object[] { "O:SPY250117C00575000" });
            Assert.IsNotNull(result, "Should parse valid contract successfully");

            var contractKey = result as Trade.Polygon2.ContractKey;
            Assert.IsNotNull(contractKey, "Result should be a ContractKey");
            Assert.AreEqual("SPY", contractKey.Underlying, "Should parse underlying correctly");
            Assert.IsTrue(contractKey.IsCall, "Should parse as call correctly");
            Assert.AreEqual(new DateTime(2025, 1, 17), contractKey.Expiration, "Should parse expiration correctly");
            Assert.AreEqual(575.0, contractKey.Strike, "Should parse strike correctly");

            // Test invalid contract parsing
            var invalidResult = parseMethod.Invoke(null, new object[] { "INVALID_TICKER" });
            Assert.IsNull(invalidResult, "Should return null for invalid contract");

            ConsoleUtilities.WriteLine("? Contract key parsing validation passed");
        }
    }
}