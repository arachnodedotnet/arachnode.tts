using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class CreateDailyPriceRecordsFromCsvTests
    {
        private string tempFile;

        [TestCleanup]
        public void Cleanup()
        {
            if (tempFile != null && File.Exists(tempFile))
                File.Delete(tempFile);
        }

        [TestMethod][TestCategory("Core")]
        public void LoadsValidCsvFile_ReturnsRecords()
        {
            tempFile = Path.GetTempFileName();
            File.WriteAllLines(tempFile, new[]
            {
                "Date,Open,High,Low,Close,Volume",
                "2024-08-01,100,110,90,105,1000",
                "2024-08-02,105,115,95,110,2000"
            });

            var records = Prices.CreateDailyPriceRecordsFromCsv(tempFile);

            Assert.AreEqual(2, records.Length);
            Assert.AreEqual(new DateTime(2024, 8, 1), records[0].DateTime);
            Assert.AreEqual(100, records[0].Open);
            Assert.AreEqual(110, records[0].High);
            Assert.AreEqual(90, records[0].Low);
            Assert.AreEqual(105, records[0].Close);
            Assert.AreEqual(1000, records[0].Volume);
        }

        [TestMethod][TestCategory("Core")]
        public void ThrowsForNullOrEmptyPath()
        {
            Assert.ThrowsExactly<ArgumentException>(() => { Prices.CreateDailyPriceRecordsFromCsv(null); });
        }

        [TestMethod][TestCategory("Core")]
        public void ThrowsForMissingFile()
        {
            Assert.ThrowsExactly<FileNotFoundException>(() =>
            {
                Prices.CreateDailyPriceRecordsFromCsv("nonexistent.csv");
            });
        }

        [TestMethod][TestCategory("Core")]
        public void ThrowsForEmptyFile()
        {
            Assert.ThrowsExactly<InvalidDataException>(() =>
            {
                tempFile = Path.GetTempFileName();
                File.WriteAllText(tempFile, "");
                Prices.CreateDailyPriceRecordsFromCsv(tempFile);
            });
        }

        [TestMethod][TestCategory("Core")]
        public void WarnsForInvalidHeader()
        {
            tempFile = Path.GetTempFileName();
            File.WriteAllLines(tempFile, new[]
            {
                "BadHeader,Something,Else",
                "2024-08-01,100,110,90,105,1000"
            });

            var records = Prices.CreateDailyPriceRecordsFromCsv(tempFile);
            Assert.AreEqual(1, records.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void SkipsInvalidRows_BadDate()
        {
            tempFile = Path.GetTempFileName();
            File.WriteAllLines(tempFile, new[]
            {
                "Date,Open,High,Low,Close,Volume",
                "notadate,100,110,90,105,1000",
                "2024-08-01,100,110,90,105,1000"
            });

            var records = Prices.CreateDailyPriceRecordsFromCsv(tempFile);
            Assert.AreEqual(1, records.Length);
            Assert.AreEqual(new DateTime(2024, 8, 1), records[0].DateTime);
        }

        [TestMethod][TestCategory("Core")]
        public void SkipsInvalidRows_BadPrices()
        {
            tempFile = Path.GetTempFileName();
            File.WriteAllLines(tempFile, new[]
            {
                "Date,Open,High,Low,Close,Volume",
                "2024-08-01,-100,110,90,105,1000", // open negative
                "2024-08-02,100,-110,90,105,1000", // high negative
                "2024-08-03,100,110,-90,105,1000", // low negative
                "2024-08-04,100,110,90,-105,1000", // close negative
                "2024-08-05,100,110,90,105,1000"   // valid
            });

            var records = Prices.CreateDailyPriceRecordsFromCsv(tempFile);
            Assert.AreEqual(1, records.Length);
            Assert.AreEqual(new DateTime(2024, 8, 5), records[0].DateTime);
        }

        [TestMethod][TestCategory("Core")]
        public void SetsVolumeToZeroIfNegative()
        {
            tempFile = Path.GetTempFileName();
            File.WriteAllLines(tempFile, new[]
            {
                "Date,Open,High,Low,Close,Volume",
                "2024-08-01,100,110,90,105,-100"
            });

            var records = Prices.CreateDailyPriceRecordsFromCsv(tempFile);
            Assert.AreEqual(1, records.Length);
            Assert.AreEqual(0, records[0].Volume);
        }

        [TestMethod][TestCategory("Core")]
        public void SkipsRowsWhereHighLessThanLow()
        {
            tempFile = Path.GetTempFileName();
            File.WriteAllLines(tempFile, new[]
            {
                "Date,Open,High,Low,Close,Volume",
                "2024-08-01,100,90,110,105,1000", // high < low
                "2024-08-02,100,110,90,105,1000"
            });

            var records = Prices.CreateDailyPriceRecordsFromCsv(tempFile);
            Assert.AreEqual(1, records.Length);
            Assert.AreEqual(new DateTime(2024, 8, 2), records[0].DateTime);
        }

        [TestMethod][TestCategory("Core")]
        public void WarnsForOpenOrCloseOutsideHighLow()
        {
            tempFile = Path.GetTempFileName();
            File.WriteAllLines(tempFile, new[]
            {
                "Date,Open,High,Low,Close,Volume",
                "2024-08-01,120,110,90,80,1000" // open > high, close < low
            });

            var records = Prices.CreateDailyPriceRecordsFromCsv(tempFile);
            Assert.AreEqual(1, records.Length);
            Assert.AreEqual(120, records[0].Open);
            Assert.AreEqual(80, records[0].Close);
        }
    }
}