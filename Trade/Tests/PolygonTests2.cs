using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Polygon2;
using Trade.Prices2;
using System.Text.Json;
using System.Diagnostics; // Add this at the top

namespace Trade.Tests
{
    [TestClass]
    public class PolygonTests2
    {
        private string _testConfigPath;
        private string _testDataDirectory;
        private Prices _testPrices;

        [TestInitialize]
        public void Setup()
        {
            // Create test prices instance with sample data
            _testPrices = new Prices();

            // Add some test price data
            var baseDate = new DateTime(2024, 1, 15, 9, 30, 0); // Monday market open
            for (var i = 0; i < 10; i++)
            {
                var record = new PriceRecord(
                    baseDate.AddMinutes(i), TimeFrame.M1,
                    100.0 + i * 0.1,
                    100.5 + i * 0.1,
                    99.5 + i * 0.1,
                    100.25 + i * 0.1,
                    volume: 1000,
                    wap: 100.0 + i * 0.1,
                    count: 100
                );
                _testPrices.AddPrice(record);
            }

            // Setup test directories
            _testDataDirectory = Path.Combine(Path.GetTempPath(), "PolygonTests");
            Directory.CreateDirectory(_testDataDirectory);

            _testConfigPath = Path.Combine(_testDataDirectory, "TestPolygonConfig.xml");
            CreateTestConfigFile(_testConfigPath);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up test files
            if (Directory.Exists(_testDataDirectory)) Directory.Delete(_testDataDirectory, true);
        }

        #region S3 Configuration Tests

        [TestMethod][TestCategory("Core")]
        public void LoadS3ConfigFromFile_ValidConfig_ShouldLoadSuccessfully()
        {
            // Act
            var config = Polygon.LoadS3ConfigFromFile(_testConfigPath);

            // Assert
            Assert.IsNotNull(config);
            Assert.AreEqual("TEST_API_KEY", config.PolygonApiKey);
            Assert.AreEqual("TEST_ACCESS_KEY", config.S3AccessKey);
            Assert.AreEqual("TEST_SECRET_KEY", config.S3SecretKey);
            Assert.AreEqual("https://files.polygon.io", config.S3Endpoint);
            Assert.AreEqual("flatfiles", config.S3BucketName);
            Assert.AreEqual("us-east-1", config.S3Region);
            Assert.IsTrue(config.UseS3ForBulkData);
            Assert.AreEqual(5, config.MaxConcurrentDownloads);
        }

        #endregion

        #region Local Path Generation Tests

        [TestMethod][TestCategory("Core")]
        public void GenerateLocalPath_ValidInputs_ShouldReturnCorrectPath()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testDate = new DateTime(2024, 1, 15);

            // Create a test S3 config
            var s3Config = new S3DataDownloader { LocalCacheDirectory = _testDataDirectory };
            var s3ConfigField = typeof(Polygon).GetField("_s3Config",
                BindingFlags.NonPublic | BindingFlags.Instance);
            s3ConfigField.SetValue(polygon, s3Config);

            var method = typeof(Polygon).GetMethod("GenerateLocalPath",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = (string)method.Invoke(polygon, new object[] { "SPY", testDate, "us_stocks_sip/minute_aggs" });

            // Assert
            Assert.IsTrue(result.Contains("2024-01-15_us_stocks_sip_minute_aggs.csv"));
            Assert.IsTrue(result.Contains(_testDataDirectory));
        }

        #endregion

        #region Stock and Options Data Fetch Tests

        [TestMethod][TestCategory("LoadFromPolygon")]
        public async Task FetchStockAndOptionsDataAsync_ValidParameters_ShouldReturnResult()
        {
            var stopwatch = Stopwatch.StartNew();

            int numberOfDaysToProcess = 30; //+ 370 + 8 + 190;

            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var startDate = DateTime.Now.Date.AddDays(-(numberOfDaysToProcess + 1));
            var endDate = DateTime.Now.Date.AddDays(1);

            // Act
            var result =
                await polygon.FetchStockAndOptionsDataAsync("SPY", startDate, endDate, true, true, false, false, false, 10, 10);

            var ivPreCalc = new IVPreCalcTests();

            await ivPreCalc.SortBulkFilesForStock(numberOfDaysToProcess);
            await ivPreCalc.SortBulkFilesForOptions(numberOfDaysToProcess);

            BuildSortedFileIndexingTests.BuildAllSortedFileIndexesForStocks(numberOfDaysToProcess);
            BuildSortedFileIndexingTests.BuildAllSortedFileIndexesForOptions(numberOfDaysToProcess);

            JoinStockAndOptionsPriceFiles.ClassInit(null);
            new JoinStockAndOptionsPriceFiles().JoinStockAndOptionsData_CreatesCombinedFile();

            stopwatch.Stop();

            new UnusualActivityTests().DetectBigBlockBuys_AnalyzesMostRecentFile();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("SPY", result.Symbol);
            Assert.AreEqual(startDate, result.StartDate);
            Assert.AreEqual(endDate, result.EndDate);
            // Note: Actual data loading will depend on S3 configuration and availability
        }

        #endregion

        #region Helper Methods

        private void CreateTestConfigFile(string configPath)
        {
            var testConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <appSettings>
    <!-- Polygon.io API Configuration -->
    <add key=""PolygonApiKey"" value=""TEST_API_KEY"" />
    
    <!-- Polygon.io S3 Flat Files Configuration -->
    <add key=""PolygonS3AccessKeyId"" value=""TEST_ACCESS_KEY"" />
    <add key=""PolygonS3SecretAccessKey"" value=""TEST_SECRET_KEY"" />
    <add key=""PolygonS3Endpoint"" value=""https://files.polygon.io"" />
    <add key=""PolygonS3BucketName"" value=""flatfiles"" />
    <add key=""PolygonS3Region"" value=""us-east-1"" />
    <add key=""PolygonS3Enabled"" value=""true"" />
    
    <!-- S3 Download Configuration -->
    <add key=""S3MaxConcurrentDownloads"" value=""5"" />
    <add key=""S3DownloadTimeoutMinutes"" value=""10"" />
    <add key=""S3LocalCacheDirectory"" value=""" + _testDataDirectory + @""" />
  </appSettings>
</configuration>";

            File.WriteAllText(configPath, testConfig);
        }

        #endregion

        #region Constructor Tests

        [TestMethod][TestCategory("Core")]
        public void Constructor_ValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange & Act
            var polygon = new Polygon(_testPrices, "SPY", 5, 10, true);

            // Assert
            Assert.IsNotNull(polygon);
            // Note: Cannot directly test private fields, but constructor should not throw
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullPrices_ShouldThrowArgumentNullException()
        {
            // Act
            var polygon = new Polygon(null, "SPY", 5, 5, true);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void Constructor_EmptySymbol_ShouldThrowArgumentException()
        {
            // Act
            var polygon = new Polygon(_testPrices, "", 5, 5, true);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentException))]
        public void Constructor_NullSymbol_ShouldThrowArgumentException()
        {
            // Act
            var polygon = new Polygon(_testPrices, null, 5, 5, true);
        }

        [TestMethod][TestCategory("Core")]
        public void Constructor_NegativeStrikesAway_ShouldUseMinimumValue()
        {
            // Arrange & Act
            var polygon = new Polygon(_testPrices, "SPY", -5, 10, true);

            // Assert
            Assert.IsNotNull(polygon);
            // The constructor should clamp negative values to minimum 1
        }

        [TestMethod][TestCategory("Core")]
        public void Constructor_NegativeDaysAway_ShouldUseMinimumValue()
        {
            // Arrange & Act
            var polygon = new Polygon(_testPrices, "SPY", 5, -10, true);

            // Assert
            Assert.IsNotNull(polygon);
            // The constructor should clamp negative values to minimum 1
        }

        #endregion

        #region S3 Key Generation Tests

        [TestMethod][TestCategory("Core")]
        public void GenerateS3Key_StockMinuteAggs_ShouldReturnCorrectFormat()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testDate = new DateTime(2024, 1, 15);

            // Use reflection to access private method
            var method = typeof(Polygon).GetMethod("GenerateS3Key",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = (string)method.Invoke(polygon, new object[] { "SPY", testDate, "us_stocks_sip/minute_aggs" });

            // Assert
            Assert.AreEqual("us_stocks_sip/minute_aggs_v1/2024/01/2024-01-15.csv.gz", result);
        }

        [TestMethod][TestCategory("Core")]
        public void GenerateS3Key_OptionsMinuteAggs_ShouldReturnCorrectFormat()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testDate = new DateTime(2024, 1, 15);

            var method = typeof(Polygon).GetMethod("GenerateS3Key",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result =
                (string)method.Invoke(polygon, new object[] { "SPY", testDate, "us_options_opra/minute_aggs" });

            // Assert
            Assert.AreEqual("us_options_opra/minute_aggs_v1/2024/01/2024-01-15.csv.gz", result);
        }

        [TestMethod][TestCategory("Core")]
        public void GenerateS3Key_UnknownDataType_ShouldReturnFallbackFormat()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testDate = new DateTime(2024, 1, 15);

            var method = typeof(Polygon).GetMethod("GenerateS3Key",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = (string)method.Invoke(polygon, new object[] { "SPY", testDate, "unknown/data_type" });

            // Assert
            Assert.AreEqual("unknown/data_type_v1/2024/01/2024-01-15.csv.gz", result);
        }

        #endregion

        #region File Structure Validation Tests

        [TestMethod][TestCategory("Core")]
        public async Task ValidateFileStructureAsync_ValidCSV_ShouldReturnTrue()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath = Path.Combine(_testDataDirectory, "test_valid.csv");

            var csvContent = @"ticker,volume,open,close,high,low,window_start,transactions
SPY,1000,100.0,100.5,101.0,99.5,1705330200000000000,100";
            File.WriteAllText(testFilePath, csvContent);

            var method = typeof(Polygon).GetMethod("ValidateFileStructureAsync",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = await (Task<bool>)method.Invoke(polygon, new object[] { testFilePath });

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod][TestCategory("Core")]
        public async Task ValidateFileStructureAsync_EmptyFile_ShouldReturnFalse()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath = Path.Combine(_testDataDirectory, "test_empty.csv");
            File.WriteAllText(testFilePath, "");

            var method = typeof(Polygon).GetMethod("ValidateFileStructureAsync",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = await (Task<bool>)method.Invoke(polygon, new object[] { testFilePath });

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod][TestCategory("Core")]
        public async Task ValidateFileStructureAsync_InvalidHeader_ShouldReturnFalse()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath = Path.Combine(_testDataDirectory, "test_invalid_header.csv");

            var csvContent = @"invalid,header,format
SPY,1000,100.0,100.5,101.0,99.5,1705330200000000000,100";
            File.WriteAllText(testFilePath, csvContent);

            var method = typeof(Polygon).GetMethod("ValidateFileStructureAsync",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = await (Task<bool>)method.Invoke(polygon, new object[] { testFilePath });

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod][TestCategory("Core")]
        public async Task ValidateFileStructureAsync_InsufficientColumns_ShouldReturnFalse()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath = Path.Combine(_testDataDirectory, "test_insufficient_columns.csv");

            var csvContent = @"ticker,volume
SPY,1000";
            File.WriteAllText(testFilePath, csvContent);

            var method = typeof(Polygon).GetMethod("ValidateFileStructureAsync",
                BindingFlags.NonPublic | BindingFlags.Instance);

            // Act
            var result = await (Task<bool>)method.Invoke(polygon, new object[] { testFilePath });

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region Hash Calculation Tests

        [TestMethod][TestCategory("Core")]
        public async Task CalculateFileHashAsync_ValidFile_ShouldReturnConsistentHash()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath = Path.Combine(_testDataDirectory, "test_hash.txt");
            var testContent = "Test content for hash calculation";
            File.WriteAllText(testFilePath, testContent);

            var method = typeof(Polygon).GetMethod("CalculateFileHashAsync",
                BindingFlags.Public | BindingFlags.Instance);

            // Act
            var hash1 = await (Task<string>)method.Invoke(polygon, new object[] { testFilePath });
            var hash2 = await (Task<string>)method.Invoke(polygon, new object[] { testFilePath });

            // Assert
            Assert.IsNotNull(hash1);
            Assert.IsNotNull(hash2);
            Assert.AreEqual(hash1, hash2);
            Assert.AreEqual(64, hash1.Length); // SHA-256 produces 64-character hex string
        }

        [TestMethod][TestCategory("Core")]
        public async Task CalculateFileHashAsync_DifferentFiles_ShouldReturnDifferentHashes()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath1 = Path.Combine(_testDataDirectory, "test_hash1.txt");
            var testFilePath2 = Path.Combine(_testDataDirectory, "test_hash2.txt");
            File.WriteAllText(testFilePath1, "Content 1");
            File.WriteAllText(testFilePath2, "Content 2");

            var method = typeof(Polygon).GetMethod("CalculateFileHashAsync",
                BindingFlags.Public | BindingFlags.Instance);

            // Act
            var hash1 = await (Task<string>)method.Invoke(polygon, new object[] { testFilePath1 });
            var hash2 = await (Task<string>)method.Invoke(polygon, new object[] { testFilePath2 });

            // Assert
            Assert.AreNotEqual(hash1, hash2);
        }

        #endregion

        #region Data Loading Tests

        [TestMethod][TestCategory("Core")]
        public void LoadBulkDataIntoPrices_ValidPolygonCsv_ShouldLoadCorrectly()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath = Path.Combine(_testDataDirectory, "test_polygon_data.csv");

            // Create timestamps within EST trading hours (9:30 AM - 4:15 PM EST)
            // Using January 15, 2024 which is a Monday (trading day)
            var estTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

            // 9:30 AM EST on January 15, 2024
            var tradingStart = new DateTime(2024, 1, 15, 9, 30, 0);
            var estTradingStart = TimeZoneInfo.ConvertTimeToUtc(tradingStart, estTimeZone);

            // 9:31 AM EST on January 15, 2024  
            var tradingNext = new DateTime(2024, 1, 15, 9, 31, 0);
            var estTradingNext = TimeZoneInfo.ConvertTimeToUtc(tradingNext, estTimeZone);

            // Convert to nanoseconds (Polygon.io format)
            var windowStart1 = new DateTimeOffset(estTradingStart).ToUnixTimeMilliseconds() * 1000000;
            var windowStart2 = new DateTimeOffset(estTradingNext).ToUnixTimeMilliseconds() * 1000000;

            // Create test CSV with Polygon.io format - within trading hours
            var csvContent = $@"ticker,volume,open,close,high,low,window_start,transactions
SPY,1000,100.0,100.5,101.0,99.5,{windowStart1},100
SPY,1500,100.5,101.0,101.5,100.0,{windowStart2},150
AAPL,2000,150.0,151.0,152.0,149.0,{windowStart1},200";
            File.WriteAllText(testFilePath, csvContent);

            // Add the same price records to _testPrices so validation passes
            var spyRecord1 = new PriceRecord(
                tradingStart, TimeFrame.M1, // EST time
                100.0, // open
                101.0, // high  
                99.5, // low
                100.5, // close
                volume: 1000, // volume
                wap: 100.5, // WAP
                count: 100 // isComplete
            );

            var spyRecord2 = new PriceRecord(
                tradingNext, TimeFrame.M1, // EST time
                100.5, // open
                101.5, // high
                100.0, // low
                101.0, // close
                volume: 1500, // volume
                wap: 101.0, // WAP
                count: 150 // isComplete
            );

            // Add records to _testPrices to match what will be loaded from CSV
            _testPrices.AddPrice(spyRecord1);
            _testPrices.AddPrice(spyRecord2);

            // Act
            var result = polygon.LoadBulkData(_testPrices, testFilePath, "SPY", true);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Length); // Should load 2 SPY records, skip AAPL

            // Verify the loaded records are within trading hours
            foreach (var record in result)
            {
                var timeOfDay = record.DateTime.TimeOfDay;
                Assert.IsTrue(timeOfDay >= new TimeSpan(9, 30, 0), "Record should be at or after 9:30 AM EST");
                Assert.IsTrue(timeOfDay < new TimeSpan(16, 15, 0), "Record should be before 4:15 PM EST");
            }

            // Verify the data matches what we expect
            Assert.AreEqual(100.0, result[0].Open);
            Assert.AreEqual(100.5, result[0].Close);
            Assert.AreEqual(101.0, result[0].High);
            Assert.AreEqual(99.5, result[0].Low);
            Assert.AreEqual(1000, result[0].Volume);

            Assert.AreEqual(100.5, result[1].Open);
            Assert.AreEqual(101.0, result[1].Close);
            Assert.AreEqual(101.5, result[1].High);
            Assert.AreEqual(100.0, result[1].Low);
            Assert.AreEqual(1500, result[1].Volume);

            // Verify that _testPrices contains the records we added
            var loadedRecord1 = _testPrices.GetPriceAt(tradingStart);
            var loadedRecord2 = _testPrices.GetPriceAt(tradingNext);

            Assert.IsNotNull(loadedRecord1, "_testPrices should contain the first record");
            Assert.IsNotNull(loadedRecord2, "_testPrices should contain the second record");

            Assert.AreEqual(spyRecord1.Close, loadedRecord1.Close);
            Assert.AreEqual(spyRecord2.Close, loadedRecord2.Close);
        }

        [TestMethod][TestCategory("Core")]
        public void LoadBulkDataIntoPrices_NonExistentFile_ShouldReturnNull()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var nonExistentPath = Path.Combine(_testDataDirectory, "non_existent.csv");

            // Act
            var result = polygon.LoadBulkData(_testPrices, nonExistentPath, "SPY", true);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod][TestCategory("Core")]
        public void LoadBulkDataIntoPrices_EmptyFile_ShouldReturnNull()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath = Path.Combine(_testDataDirectory, "test_empty.csv");
            File.WriteAllText(testFilePath, "");

            // Act
            var result = polygon.LoadBulkData(_testPrices, testFilePath, "SPY", true);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod][TestCategory("Core")]
        public void LoadBulkDataIntoPrices_PlaceholderFile_ShouldReturnNull()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath = Path.Combine(_testDataDirectory, "test_placeholder.csv");
            File.WriteAllText(testFilePath, "# Polygon.io S3 Download Required");

            // Act
            var result = polygon.LoadBulkData(_testPrices, testFilePath, "SPY", true);

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region File Integrity Tests

        [TestMethod][TestCategory("Core")]
        public async Task VerifyFileIntegrityAsync_NonExistentFile_ShouldReturnFalse()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var nonExistentPath = Path.Combine(_testDataDirectory, "non_existent.txt");

            var method = typeof(Polygon).GetMethod("VerifyFileIntegrityAsync",
                BindingFlags.Public | BindingFlags.Instance);

            // Act
            var result = await (Task<bool>)method.Invoke(polygon, new object[] { nonExistentPath, null });

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod][TestCategory("Core")]
        public async Task VerifyFileIntegrityAsync_EmptyFile_ShouldReturnFalse()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath = Path.Combine(_testDataDirectory, "test_empty_integrity.txt");
            File.WriteAllText(testFilePath, "");

            var method = typeof(Polygon).GetMethod("VerifyFileIntegrityAsync",
                BindingFlags.Public | BindingFlags.Instance);

            // Act
            var result = await (Task<bool>)method.Invoke(polygon, new object[] { testFilePath, null });

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod][TestCategory("Core")]
        public async Task VerifyFileIntegrityAsync_ValidFileWithMatchingMetadata_ShouldReturnTrue()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath = Path.Combine(_testDataDirectory, "test_integrity_valid.txt");
            var testContent = "Valid file for integrity test";
            File.WriteAllText(testFilePath, testContent);

            // Calculate hash
            var hash = await polygon.CalculateFileHashAsync(testFilePath);
            var metadataPath = testFilePath + ".metadata";

            // Use System.Text.Json for proper JSON formatting
            var metadataObj = new
            {
                FilePath = testFilePath,
                Hash = hash,
                ETag = "etag123",
                FileSize = testContent.Length,
                DownloadTime = DateTime.UtcNow.ToString("O"),
                Source = "Polygon.io S3"
            };
            var metadataJson = JsonSerializer.Serialize(metadataObj);
            File.WriteAllText(metadataPath, metadataJson);

            var method = typeof(Polygon).GetMethod("VerifyFileIntegrityAsync", BindingFlags.Public | BindingFlags.Instance);
            var result = await (Task<bool>)method.Invoke(polygon, new object[] { testFilePath, null });
            Assert.IsTrue(result);
        }

        [TestMethod][TestCategory("Core")]
        public async Task VerifyFileIntegrityAsync_ValidFileWithMismatchedMetadata_ShouldReturnFalse()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath = Path.Combine(_testDataDirectory, "test_integrity_mismatch.txt");
            var testContent = "Valid file for integrity test";
            File.WriteAllText(testFilePath, testContent);

            // Calculate hash
            var hash = await polygon.CalculateFileHashAsync(testFilePath);
            var wrongHash = new string(hash.ToCharArray().Reverse().ToArray());
            var metadataPath = testFilePath + ".metadata";
            var metadataJson = $"{{\"FilePath\":\"{testFilePath}\",\"Hash\":\"{wrongHash}\",\"ETag\":\"etag123\",\"FileSize\":{testContent.Length},\"DownloadTime\":\"{DateTime.UtcNow:O}\",\"Source\":\"Polygon.io S3\"}}";
            File.WriteAllText(metadataPath, metadataJson);

            var method = typeof(Polygon).GetMethod("VerifyFileIntegrityAsync", BindingFlags.Public | BindingFlags.Instance);
            var result = await (Task<bool>)method.Invoke(polygon, new object[] { testFilePath, null });
            Assert.IsFalse(result);
        }

        [TestMethod][TestCategory("Core")]
        public async Task VerifyFileIntegrityAsync_ValidFileWithNoMetadata_ShouldReturnTrue()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath = Path.Combine(_testDataDirectory, "test_integrity_nometa.csv");
            var csvContent = "ticker,volume,open,close,high,low,window_start,transactions\nSPY,1000,100.0,100.5,101.0,99.5,1705330200000000000,100";
            File.WriteAllText(testFilePath, csvContent);

            var method = typeof(Polygon).GetMethod("VerifyFileIntegrityAsync", BindingFlags.Public | BindingFlags.Instance);
            var result = await (Task<bool>)method.Invoke(polygon, new object[] { testFilePath, null });
            Assert.IsTrue(result);
        }

        [TestMethod][TestCategory("Core")]
        public async Task VerifyFileIntegrityAsync_ValidFileWithWrongExpectedHash_ShouldReturnFalse()
        {
            // Arrange
            var polygon = new Polygon(_testPrices, "SPY", 5, 5, true);
            var testFilePath = Path.Combine(_testDataDirectory, "test_integrity_wronghash.txt");
            var testContent = "Valid file for integrity test";
            File.WriteAllText(testFilePath, testContent);

            var hash = await polygon.CalculateFileHashAsync(testFilePath);
            var wrongHash = new string(hash.ToCharArray().Reverse().ToArray());

            var method = typeof(Polygon).GetMethod("VerifyFileIntegrityAsync", BindingFlags.Public | BindingFlags.Instance);
            var result = await (Task<bool>)method.Invoke(polygon, new object[] { testFilePath, wrongHash });
            Assert.IsFalse(result);
        }

        #endregion
    }

    #region Test Utility Classes

    /// <summary>
    ///     Helper class for testing S3 configuration
    /// </summary>
    public class TestS3Config
    {
        public string PolygonApiKey { get; set; }
        public string S3AccessKey { get; set; }
        public string S3SecretKey { get; set; }
        public string S3Endpoint { get; set; }
        public string S3BucketName { get; set; }
        public string S3Region { get; set; }
        public bool UseS3ForBulkData { get; set; }
        public int MaxConcurrentDownloads { get; set; } = 5;
        public int DownloadTimeoutMinutes { get; set; } = 10;
        public string LocalCacheDirectory { get; set; }
    }

    #endregion
}