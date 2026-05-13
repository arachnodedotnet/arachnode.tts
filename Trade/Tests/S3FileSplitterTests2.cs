using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Polygon2;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class S3FileSplitterTests2
    {
        [TestMethod][TestCategory("Core")]
        public void GenerateSafeFileName_RemovesInvalidCharacters()
        {
            var input = "O:SPY240814C00390000/\\:*?\"<>|";
            var safe = S3FileSplitter.GenerateSafeFileName(input);
            Assert.IsFalse(safe.Any(c => Path.GetInvalidFileNameChars().Contains(c)));
            Assert.IsTrue(safe.StartsWith("O_SPY240814C00390000"));
        }

        [TestMethod][TestCategory("Core")]
        public void ExtractTimestamp_ParsesTimestampCorrectly()
        {
            var csvLine = "O:SPY240814C00390000,100,101,102,103,104,1659552000000000000,10";
            var ts = S3FileSplitter.ExtractTimestamp(csvLine);
            Assert.AreEqual(1659552000000000000, ts);
        }

        [TestMethod][TestCategory("Core")]
        public void SplitFiles_ProcessesLinesAndReturnsRecords()
        {
            // Minimal valid CSV lines for one contract
            string[] lines =
            {
                "ticker,volume,open,close,high,low,window_start,transactions",
                "O:SPY240814C00390000,100,101,102,103,104,1659552000000000000,10"
            };

            // Create a dummy.csv file in a temp directory
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var dummyFile = Path.Combine(tempDir, "dummy.csv");
            File.WriteAllLines(dummyFile, lines);

            var prices = new Prices();
            var records = S3FileSplitter.SplitFiles(dummyFile, prices, lines, "SPY", false, false, false);

            Assert.AreEqual(1, records.Length);
            Assert.AreEqual("O:SPY240814C00390000", records[0].Option.Symbol);
            Assert.AreEqual(102, records[0].Close);

            // Clean up
            File.Delete(dummyFile);
            Directory.Delete(tempDir);
        }

        [TestMethod][TestCategory("Core")]
        public void VerifySplitOptionFiles_ThrowsOnMissingOrMismatchedLines()
        {
            var tempBulkDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var tempSplitDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempBulkDir);
            Directory.CreateDirectory(tempSplitDir);

            var contract = "O:SPY240814C00390000";
            var safeName = S3FileSplitter.GenerateSafeFileName(contract);

            // Bulk file with two lines
            var bulkFile = Path.Combine(tempBulkDir, "bulk.csv");
            File.WriteAllLines(bulkFile, new[]
            {
                "ticker,volume,open,close,high,low,window_start,transactions",
                $"{contract},100,101,102,103,104,1659552000000000000,10",
                $"{contract},200,201,202,203,204,1659552000000000001,20"
            });

            // Split file with only one line (should trigger missing price error)
            var splitFile = Path.Combine(tempSplitDir, $"{safeName}.csv");
            File.WriteAllLines(splitFile, new[]
            {
                "ticker,volume,open,close,high,low,window_start,transactions",
                $"{contract},100,101,102,103,104,1659552000000000000,10"
            });

            try
            {
                S3FileSplitter.VerifySplitOptionFiles(tempBulkDir, tempSplitDir, "SPY");
                Assert.Fail("Expected exception was not thrown.");
            }
            catch (AggregateException aggEx)
            {
                // Assert that at least one InvalidDataException is present
                var inner = aggEx.Flatten().InnerExceptions.OfType<InvalidDataException>().FirstOrDefault();
                Assert.IsNotNull(inner, "AggregateException should contain InvalidDataException");
            }
            finally
            {
                Directory.Delete(tempBulkDir, true);
                Directory.Delete(tempSplitDir, true);
            }
        }

        [TestMethod][TestCategory("Core")]
        public void VerifySplitOptionFiles_PassesWithMatchingLines()
        {
            var tempBulkDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var tempSplitDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempBulkDir);
            Directory.CreateDirectory(tempSplitDir);

            var contract = "O:SPY240814C00390000";
            var safeName = S3FileSplitter.GenerateSafeFileName(contract);

            // Bulk file with one line
            var bulkFile = Path.Combine(tempBulkDir, "bulk.csv");
            File.WriteAllLines(bulkFile, new[]
            {
                "ticker,volume,open,close,high,low,window_start,transactions",
                $"{contract},100,101,102,103,104,1659552000000000000,10"
            });

            // Split file with matching line
            var splitFile = Path.Combine(tempSplitDir, $"{safeName}.csv");
            File.WriteAllLines(splitFile, new[]
            {
                "ticker,volume,open,close,high,low,window_start,transactions",
                $"{contract},100,101,102,103,104,1659552000000000000,10"
            });

            // Should not throw
            S3FileSplitter.VerifySplitOptionFiles(tempBulkDir, tempSplitDir, "SPY");

            // Clean up
            Directory.Delete(tempBulkDir, true);
            Directory.Delete(tempSplitDir, true);
        }
    }
}