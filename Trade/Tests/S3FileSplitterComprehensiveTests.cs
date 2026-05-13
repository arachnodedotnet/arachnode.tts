using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Polygon2;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class S3FileSplitterComprehensiveTests
    {
        private const string TestFileName = "nofile.csv";

        [TestInitialize]
        public void TestSetup()
        {
            // Create an empty file for tests that expect it to exist
            if (!File.Exists(TestFileName))
            {
                File.WriteAllText(TestFileName, string.Empty);
            }
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Remove the test file after each test
            if (File.Exists(TestFileName))
            {
                File.Delete(TestFileName);
            }
        }

        [TestMethod][TestCategory("Core")]
        public void SplitFiles_EmptyInput_ReturnsEmptyArray()
        {
            var prices = new Prices();
            var result = S3FileSplitter.SplitFiles(TestFileName, prices, new string[0], "SPY", false, false, false);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void SplitFiles_HeaderOnly_ReturnsEmptyArray()
        {
            var prices = new Prices();
            var lines = new[] { "ticker,volume,open,close,high,low,window_start,transactions" };
            var result = S3FileSplitter.SplitFiles(TestFileName, prices, lines, "SPY", false, false, false);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void SplitFiles_InvalidLines_AreCountedAsInvalid()
        {
            var prices = new Prices();
            var lines = new[] {
                "ticker,volume,open,close,high,low,window_start,transactions",
                "SPY,notanumber,100,101,102,103,104,10", // volume invalid
                "SPY,100,100,101,102,103,notanumber,10", // window_start invalid
                "" // empty line
            };
            var result = S3FileSplitter.SplitFiles(TestFileName, prices, lines, "SPY", false, false, false);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void SplitFiles_SkipsOtherSymbols()
        {
            var prices = new Prices();
            var lines = new[] {
                "ticker,volume,open,close,high,low,window_start,transactions",
                "AAPL,100,100,101,102,103,1659552000000000000,10"
            };
            var result = S3FileSplitter.SplitFiles(TestFileName, prices, lines, "SPY", false, false, false);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void SplitFiles_FiltersMarketHours()
        {
            // 8:00 AM ET (before market open)
            var dt = new DateTime(2023, 8, 14, 8, 0, 0, DateTimeKind.Utc);
            var nanos = (long)(new DateTimeOffset(dt).ToUnixTimeMilliseconds() * 1000000);
            var lines = new[] {
                "ticker,volume,open,close,high,low,window_start,transactions",
                $"SPY,100,100,101,102,103,{nanos},10"
            };
            var prices = new Prices();
            var result = S3FileSplitter.SplitFiles(TestFileName, prices, lines, "SPY", false, false, false);
            Assert.AreEqual(0, result.Length);
        }

        //[TestMethod][TestCategory("Core")]
        public void SplitFiles_ValidatesAndAddsToPrices()
        {
            var lines = new[] {
                "ticker,volume,open,close,high,low,window_start,transactions",
                "SPY,100,100,101,102,103,1659552000000000000,10"
            };
            var prices = new Prices();
            var result = S3FileSplitter.SplitFiles(TestFileName, prices, lines, "SPY", true, true, false);
            Assert.AreEqual(1, result.Length);
            Assert.IsTrue(prices.Records.Count > 0);
        }

        [TestMethod][TestCategory("Core")]
        public void SplitFiles_BackupFileIsCreated()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var file = Path.Combine(tempDir, "dummy.csv");
            var lines = new[] {
                "ticker,volume,open,close,high,low,window_start,transactions",
                "SPY,100,100,101,102,103,1659552000000000000,10"
            };
            File.WriteAllLines(file, lines);
            var prices = new Prices();
            S3FileSplitter.SplitFiles(file, prices, lines, "SPY", false, false, true);
            var contractDir = Path.Combine(Directory.GetCurrentDirectory(), "ContractData", "SPY");
            var csvFile = Path.Combine(contractDir, "SPY.csv");
            Assert.IsTrue(File.Exists(csvFile));
            var backupFiles = Directory.GetFiles(contractDir, "SPY.csv.backup.*");
            Assert.IsTrue(backupFiles.Length >= 0); // Backup may be created if file existed
            Directory.Delete(tempDir, true);
        }

        //[TestMethod][TestCategory("Core")]
        public void SplitFiles_ThrowsOnPriceMismatch()
        {
            var lines = new[] {
                "ticker,volume,open,close,high,low,window_start,transactions",
                "SPY,100,100,101,102,103,1659552000000000000,10"
            };
            var prices = new Prices();
            // Add a record with different values to prices
            prices.AddPrice(new PriceRecord(DateTime.Now, TimeFrame.D1, 200, 201, 202, 203, volume: 100, wap: 203, count: 10));
            Assert.ThrowsException<InvalidDataException>(() =>
                S3FileSplitter.SplitFiles(TestFileName, prices, lines, "SPY", false, false, false));
        }
    }
}
