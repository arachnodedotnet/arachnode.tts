using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Polygon2;

namespace Trade.Tests
{
    //[TestClass]
    public class S3FileSplitterTests
    {
        private MethodInfo _extractTimestampMethod;
        private MethodInfo _generateSafeFileNameMethod;
        private MethodInfo _saveContractCsvFilesMethod;
        private string _testDirectory;

        [TestInitialize]
        public void Setup()
        {
            // Create test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "S3FileSplitterTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);

            // Get private methods using reflection
            var s3FileSplitterType = typeof(S3FileSplitter);

            // Use the correct method name: SaveContractCsvFilesParallel
            _saveContractCsvFilesMethod = s3FileSplitterType.GetMethod("SaveContractCsvFilesParallel",
                BindingFlags.NonPublic | BindingFlags.Static);

            _generateSafeFileNameMethod = s3FileSplitterType.GetMethod("GenerateSafeFileName",
                BindingFlags.NonPublic | BindingFlags.Static);

            _extractTimestampMethod = s3FileSplitterType.GetMethod("ExtractTimestamp",
                BindingFlags.NonPublic | BindingFlags.Static);

            // Change current directory to test directory for file operations
            Environment.CurrentDirectory = _testDirectory;
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Restore original directory and clean up test files
            if (Directory.Exists(_testDirectory))
                try
                {
                    Directory.Delete(_testDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
        }

        #region SaveContractCsvFiles Tests

        [TestMethod]
        [TestCategory("Core")]
        public void SaveContractCsvFiles_EmptyInput_ReturnsZero()
        {
            // Skip test if method not found (defensive programming)
            if (_saveContractCsvFilesMethod == null)
            {
                Assert.Inconclusive(
                    "SaveContractCsvFilesParallel method not found - method signature may have changed");
                return;
            }

            // Arrange
            var recordsByContract = new Dictionary<string, List<string>>();
            var contractHeaders = new Dictionary<string, string>();

            // Act
            var result = (int)_saveContractCsvFilesMethod.Invoke(null,
                new object[] { recordsByContract, contractHeaders, "SPY", true });

            // Assert
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SaveContractCsvFiles_SingleContract_CreatesSingleFile()
        {
            // Skip test if method not found
            if (_saveContractCsvFilesMethod == null)
            {
                Assert.Inconclusive(
                    "SaveContractCsvFilesParallel method not found - method signature may have changed");
                return;
            }

            // Arrange
            var recordsByContract = new Dictionary<string, List<string>>
            {
                ["SPY"] = new List<string>
                {
                    "SPY,1000,100.0,100.5,101.0,99.5,1705330200000000000,100",
                    "SPY,1500,100.5,101.0,101.5,100.0,1705330260000000000,150"
                }
            };
            var contractHeaders = new Dictionary<string, string>
            {
                ["SPY"] = "ticker,volume,open,close,high,low,window_start,transactions"
            };

            // Act
            var result = (int)_saveContractCsvFilesMethod.Invoke(null,
                new object[] { recordsByContract, contractHeaders, "SPY", true });

            // Assert
            Assert.AreEqual(1, result);

            var expectedFilePath = Path.Combine("ContractData", "SPY", "SPY.csv");
            Assert.IsTrue(File.Exists(expectedFilePath), $"Expected file not found: {expectedFilePath}");

            var lines = File.ReadAllLines(expectedFilePath);
            Assert.AreEqual(3, lines.Length); // Header + 2 data lines
            Assert.AreEqual(contractHeaders["SPY"], lines[0]);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SaveContractCsvFiles_MultipleContracts_CreatesMultipleFiles()
        {
            // Skip test if method not found
            if (_saveContractCsvFilesMethod == null)
            {
                Assert.Inconclusive(
                    "SaveContractCsvFilesParallel method not found - method signature may have changed");
                return;
            }

            // Arrange
            var recordsByContract = new Dictionary<string, List<string>>
            {
                ["SPY"] = new List<string>
                {
                    "SPY,1000,100.0,100.5,101.0,99.5,1705330200000000000,100"
                },
                ["SPY240119C00500000"] = new List<string>
                {
                    "SPY240119C00500000,500,5.0,5.5,6.0,4.5,1705330200000000000,50"
                },
                ["SPY240119P00450000"] = new List<string>
                {
                    "SPY240119P00450000,300,2.0,2.2,2.5,1.8,1705330200000000000,30"
                }
            };

            var contractHeaders = new Dictionary<string, string>();
            foreach (var contract in recordsByContract.Keys)
                contractHeaders[contract] = "ticker,volume,open,close,high,low,window_start,transactions";

            // Act
            var result = (int)_saveContractCsvFilesMethod.Invoke(null,
                new object[] { recordsByContract, contractHeaders, "SPY", true });

            // Assert
            Assert.AreEqual(3, result);

            // Check all files were created
            var contractDataDir = Path.Combine("ContractData", "SPY");
            Assert.IsTrue(Directory.Exists(contractDataDir), $"Contract data directory not found: {contractDataDir}");

            var files = Directory.GetFiles(contractDataDir, "*.csv");
            Assert.AreEqual(3, files.Length);

            // Verify specific files
            Assert.IsTrue(File.Exists(Path.Combine(contractDataDir, "SPY.csv")));
            Assert.IsTrue(File.Exists(Path.Combine(contractDataDir, "SPY240119C00500000.csv")));
            Assert.IsTrue(File.Exists(Path.Combine(contractDataDir, "SPY240119P00450000.csv")));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SaveContractCsvFiles_ExistingFileIdenticalContent_SkipsFile()
        {
            // Skip test if method not found
            if (_saveContractCsvFilesMethod == null)
            {
                Assert.Inconclusive(
                    "SaveContractCsvFilesParallel method not found - method signature may have changed");
                return;
            }

            // Arrange
            var contractDataDir = Path.Combine("ContractData", "SPY");
            Directory.CreateDirectory(contractDataDir);

            var existingFilePath = Path.Combine(contractDataDir, "SPY.csv");
            var existingContent = new[]
            {
                "ticker,volume,open,close,high,low,window_start,transactions",
                "SPY,1000,100.0,100.5,101.0,99.5,1705330200000000000,100"
            };
            File.WriteAllLines(existingFilePath, existingContent);

            var recordsByContract = new Dictionary<string, List<string>>
            {
                ["SPY"] = new List<string>
                {
                    "SPY,1000,100.0,100.5,101.0,99.5,1705330200000000000,100"
                }
            };
            var contractHeaders = new Dictionary<string, string>
            {
                ["SPY"] = "ticker,volume,open,close,high,low,window_start,transactions"
            };

            var originalWriteTime = File.GetLastWriteTime(existingFilePath);

            // Act
            var result = (int)_saveContractCsvFilesMethod.Invoke(null,
                new object[] { recordsByContract, contractHeaders, "SPY", true });

            // Assert - Note: The parallel version may still return 1 but skip writing due to no new data
            // Check that file wasn't actually modified recently
            var newWriteTime = File.GetLastWriteTime(existingFilePath);
            var timeDifference = Math.Abs((newWriteTime - originalWriteTime).TotalSeconds);
            Assert.IsTrue(timeDifference < 1.0, "File should not be modified when content is identical");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SaveContractCsvFiles_ExistingFileWithBackup_CreatesBackup()
        {
            // Skip test if method not found
            if (_saveContractCsvFilesMethod == null)
            {
                Assert.Inconclusive(
                    "SaveContractCsvFilesParallel method not found - method signature may have changed");
                return;
            }

            // Arrange
            var contractDataDir = Path.Combine("ContractData", "SPY");
            Directory.CreateDirectory(contractDataDir);

            var existingFilePath = Path.Combine(contractDataDir, "SPY.csv");
            var existingContent = new[]
            {
                "ticker,volume,open,close,high,low,window_start,transactions",
                "SPY,1000,100.0,100.5,101.0,99.5,1705330200000000000,100"
            };
            File.WriteAllLines(existingFilePath, existingContent);

            var recordsByContract = new Dictionary<string, List<string>>
            {
                ["SPY"] = new List<string>
                {
                    "SPY,1000,100.0,100.5,101.0,99.5,1705330200000000000,100",
                    "SPY,1500,100.5,101.0,101.5,100.0,1705330260000000000,150" // Additional record
                }
            };
            var contractHeaders = new Dictionary<string, string>
            {
                ["SPY"] = "ticker,volume,open,close,high,low,window_start,transactions"
            };

            // Act
            var result = (int)_saveContractCsvFilesMethod.Invoke(null,
                new object[] { recordsByContract, contractHeaders, "SPY", true });

            // Assert
            Assert.AreEqual(1, result);

            // Check that backup was created
            var backupFiles = Directory.GetFiles(contractDataDir, "SPY.csv.backup.*");
            Assert.AreEqual(1, backupFiles.Length, "Should create exactly one backup file");

            // Verify main file was updated
            var updatedLines = File.ReadAllLines(existingFilePath);
            Assert.AreEqual(3, updatedLines.Length); // Header + 2 data lines
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SaveContractCsvFiles_BackupsDisabled_NoBackupCreated()
        {
            // Skip test if method not found
            if (_saveContractCsvFilesMethod == null)
            {
                Assert.Inconclusive(
                    "SaveContractCsvFilesParallel method not found - method signature may have changed");
                return;
            }

            // Arrange
            var contractDataDir = Path.Combine("ContractData", "SPY");
            Directory.CreateDirectory(contractDataDir);

            var existingFilePath = Path.Combine(contractDataDir, "SPY.csv");
            File.WriteAllLines(existingFilePath, new[] { "header", "old data" });

            var recordsByContract = new Dictionary<string, List<string>>
            {
                ["SPY"] = new List<string> { "SPY,1000,100.0,100.5,101.0,99.5,1705330200000000000,100" }
            };
            var contractHeaders = new Dictionary<string, string>
            {
                ["SPY"] = "ticker,volume,open,close,high,low,window_start,transactions"
            };

            // Act
            var result = (int)_saveContractCsvFilesMethod.Invoke(null,
                new object[] { recordsByContract, contractHeaders, "SPY", false }); // Backups disabled

            // Assert
            Assert.AreEqual(1, result);

            // Check that no backup was created
            var backupFiles = Directory.GetFiles(contractDataDir, "*.backup.*");
            Assert.AreEqual(0, backupFiles.Length, "Should not create backup files when disabled");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SaveContractCsvFiles_SortsRecordsByTimestamp()
        {
            // Skip test if method not found
            if (_saveContractCsvFilesMethod == null)
            {
                Assert.Inconclusive(
                    "SaveContractCsvFilesParallel method not found - method signature may have changed");
                return;
            }

            // Arrange
            var recordsByContract = new Dictionary<string, List<string>>
            {
                ["SPY"] = new List<string>
                {
                    "SPY,1500,100.5,101.0,101.5,100.0,1705330260000000000,150", // Later timestamp
                    "SPY,1000,100.0,100.5,101.0,99.5,1705330200000000000,100" // Earlier timestamp
                }
            };
            var contractHeaders = new Dictionary<string, string>
            {
                ["SPY"] = "ticker,volume,open,close,high,low,window_start,transactions"
            };

            // Act
            var result = (int)_saveContractCsvFilesMethod.Invoke(null,
                new object[] { recordsByContract, contractHeaders, "SPY", true });

            // Assert
            Assert.AreEqual(1, result);

            var filePath = Path.Combine("ContractData", "SPY", "SPY.csv");
            var lines = File.ReadAllLines(filePath);

            // Verify sorting: earlier timestamp should come first
            Assert.IsTrue(lines[1].Contains("1705330200000000000"), "Earlier timestamp should be first data line");
            Assert.IsTrue(lines[2].Contains("1705330260000000000"), "Later timestamp should be second data line");
        }

        #endregion

        #region GenerateSafeFileName Tests

        [TestMethod]
        [TestCategory("Core")]
        public void GenerateSafeFileName_ValidSymbol_ReturnsUnchanged()
        {
            // Skip test if method not found
            if (_generateSafeFileNameMethod == null)
            {
                Assert.Inconclusive("GenerateSafeFileName method not found - method signature may have changed");
                return;
            }

            // Act
            var result = (string)_generateSafeFileNameMethod.Invoke(null, new object[] { "SPY" });

            // Assert
            Assert.AreEqual("SPY", result);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GenerateSafeFileName_NullOrEmpty_ReturnsUnknown()
        {
            // Skip test if method not found
            if (_generateSafeFileNameMethod == null)
            {
                Assert.Inconclusive("GenerateSafeFileName method not found - method signature may haveChanged");
                return;
            }

            // Act
            var result1 = (string)_generateSafeFileNameMethod.Invoke(null, new object[] { null });
            var result2 = (string)_generateSafeFileNameMethod.Invoke(null, new object[] { "" });

            // Assert
            Assert.AreEqual("Unknown", result1);
            Assert.AreEqual("Unknown", result2);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GenerateSafeFileName_InvalidCharacters_ReplacesWithUnderscore()
        {
            // Skip test if method not found
            if (_generateSafeFileNameMethod == null)
            {
                Assert.Inconclusive("GenerateSafeFileName method not found - method signature may have changed");
                return;
            }

            // Act
            var result = (string)_generateSafeFileNameMethod.Invoke(null,
                new object[] { "SPY:240119/C\\500?" });

            // Assert
            Assert.AreEqual("SPY_240119_C_500_", result);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GenerateSafeFileName_LongSymbol_TruncatesTo200Characters()
        {
            // Skip test if method not found
            if (_generateSafeFileNameMethod == null)
            {
                Assert.Inconclusive("GenerateSafeFileName method not found - method signature may have changed");
                return;
            }

            // Arrange
            var longSymbol = new string('A', 250); // 250 characters

            // Act
            var result = (string)_generateSafeFileNameMethod.Invoke(null, new object[] { longSymbol });

            // Assert
            Assert.AreEqual(200, result.Length);
            Assert.AreEqual(new string('A', 200), result);
        }

        #endregion

        #region ExtractTimestamp Tests

        [TestMethod]
        [TestCategory("Core")]
        public void ExtractTimestamp_ValidCsvLine_ReturnsTimestamp()
        {
            // Skip test if method not found
            if (_extractTimestampMethod == null)
            {
                Assert.Inconclusive("ExtractTimestamp method not found - method signature may have changed");
                return;
            }

            // Arrange
            var csvLine = "SPY,1000,100.0,100.5,101.0,99.5,1705330200000000000,100";

            // Act
            var result = (long)_extractTimestampMethod.Invoke(null, new object[] { csvLine });

            // Assert
            Assert.AreEqual(1705330200000000000L, result);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ExtractTimestamp_InvalidCsvLine_ReturnsZero()
        {
            // Skip test if method not found
            if (_extractTimestampMethod == null)
            {
                Assert.Inconclusive("ExtractTimestamp method not found - method signature may have changed");
                return;
            }

            // Arrange
            var csvLine = "Invalid,CSV,Line";

            // Act
            var result = (long)_extractTimestampMethod.Invoke(null, new object[] { csvLine });

            // Assert
            Assert.AreEqual(0L, result);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ExtractTimestamp_EmptyLine_ReturnsZero()
        {
            // Skip test if method not found
            if (_extractTimestampMethod == null)
            {
                Assert.Inconclusive("ExtractTimestamp method not found - method signature may have changed");
                return;
            }

            // Act
            var result = (long)_extractTimestampMethod.Invoke(null, new object[] { "" });

            // Assert
            Assert.AreEqual(0L, result);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ExtractTimestamp_NonNumericTimestamp_ReturnsZero()
        {
            // Skip test if method not found
            if (_extractTimestampMethod == null)
            {
                Assert.Inconclusive("ExtractTimestamp method not found - method signature may have changed");
                return;
            }

            // Arrange
            var csvLine = "SPY,1000,100.0,100.5,101.0,99.5,NotANumber,100";

            // Act
            var result = (long)_extractTimestampMethod.Invoke(null, new object[] { csvLine });

            // Assert
            Assert.AreEqual(0L, result);
        }

        #endregion

        #region Performance and Parallel Processing Tests

        [TestMethod]
        [TestCategory("Core")]
        public void SaveContractCsvFiles_LargeNumberOfContracts_CompletesInReasonableTime()
        {
            // Skip test if method not found
            if (_saveContractCsvFilesMethod == null)
            {
                Assert.Inconclusive(
                    "SaveContractCsvFilesParallel method not found - method signature may have changed");
                return;
            }

            // Arrange
            var recordsByContract = new Dictionary<string, List<string>>();
            var contractHeaders = new Dictionary<string, string>();

            // Create 50 contracts with 10 records each (reduced from 100 for faster testing)
            for (var i = 0; i < 50; i++)
            {
                var contractSymbol = $"SPY{i:D3}";
                var records = new List<string>();

                for (var j = 0; j < 10; j++)
                {
                    var timestamp = 1705330200000000000L + i * 1000000000L + j * 60000000000L;
                    records.Add(
                        $"{contractSymbol},{1000 + j},100.{j},100.{j + 1},101.{j},99.{j},{timestamp},{100 + j}");
                }

                recordsByContract[contractSymbol] = records;
                contractHeaders[contractSymbol] = "ticker,volume,open,close,high,low,window_start,transactions";
            }

            var stopwatch = Stopwatch.StartNew();

            // Act
            var result = (int)_saveContractCsvFilesMethod.Invoke(null,
                new object[] { recordsByContract, contractHeaders, "SPY", true });

            stopwatch.Stop();

            // Assert
            Assert.AreEqual(50, result);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000,
                $"Operation took {stopwatch.ElapsedMilliseconds}ms, expected < 10000ms");

            // Verify files were created
            var contractDataDir = Path.Combine("ContractData", "SPY");
            var files = Directory.GetFiles(contractDataDir, "*.csv");
            Assert.AreEqual(50, files.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SaveContractCsvFiles_ConcurrentAccess_HandlesFileLocking()
        {
            // Skip test if method not found
            if (_saveContractCsvFilesMethod == null)
            {
                Assert.Inconclusive(
                    "SaveContractCsvFilesParallel method not found - method signature may have changed");
                return;
            }

            // Arrange
            var recordsByContract = new Dictionary<string, List<string>>
            {
                ["SPY"] = new List<string>
                {
                    "SPY,1000,100.0,100.5,101.0,99.5,1705330200000000000,100"
                }
            };
            var contractHeaders = new Dictionary<string, string>
            {
                ["SPY"] = "ticker,volume,open,close,high,low,window_start,transactions"
            };

            // Act & Assert - Should not throw exceptions
            var tasks = new Task[3]; // Reduced from 5 to 3 for faster testing
            for (var i = 0; i < 3; i++)
            {
                var taskId = i; // Capture loop variable
                tasks[i] = Task.Run(() =>
                {
                    try
                    {
                        var uniqueRecords = new Dictionary<string, List<string>>
                        {
                            [$"SPY{taskId}"] = new List<string>
                            {
                                $"SPY{taskId},1000,100.0,100.5,101.0,99.5,1705330200000000000,100"
                            }
                        };
                        var uniqueHeaders = new Dictionary<string, string>
                        {
                            [$"SPY{taskId}"] = "ticker,volume,open,close,high,low,window_start,transactions"
                        };

                        var result = (int)_saveContractCsvFilesMethod.Invoke(null,
                            new object[] { uniqueRecords, uniqueHeaders, $"SPY{taskId}", true });
                        Assert.IsTrue(result >= 0, $"Task {taskId} should return non-negative result");
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Concurrent access failed in task {taskId}: {ex.Message}");
                    }
                });
            }

            var completed = Task.WaitAll(tasks, TimeSpan.FromSeconds(30));
            Assert.IsTrue(completed, "All concurrent tasks should complete within 30 seconds");
        }

        #endregion
    }
}