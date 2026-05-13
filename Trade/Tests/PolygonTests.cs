using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Polygon2;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class PolygonTests
    {
        private Polygon CreatePolygonForTest()
        {
            // Use the constructor with all parameters to avoid ambiguity
            return new Polygon(new Prices(), "SPY", 5, 5, false);
        }

        [TestMethod][TestCategory("Core")]
        public async Task CalculateFileHashAsync_ReturnsCorrectHashForKnownFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "test.txt");
            var content = "Hello, Polygon!";
            File.WriteAllText(filePath, content);

            // Calculate expected hash using SHA256
            using (var sha256 = SHA256.Create())
            {
                var expectedHash = BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-", "").ToLowerInvariant();

                var polygon = CreatePolygonForTest();
                var actualHash = await polygon.CalculateFileHashAsync(filePath);
                Assert.AreEqual(expectedHash, actualHash);
            }
            Directory.Delete(tempDir, true);
        }

        [TestMethod][TestCategory("Core")]
        public async Task CalculateFileHashAsync_EmptyFile_ReturnsHashOfEmptyContent()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "empty.txt");
            File.WriteAllText(filePath, "");

            using (var sha256 = SHA256.Create())
            {
                var expectedHash = BitConverter.ToString(sha256.ComputeHash(new byte[0])).Replace("-", "").ToLowerInvariant();
                var polygon = CreatePolygonForTest();
                var actualHash = await polygon.CalculateFileHashAsync(filePath);
                Assert.AreEqual(expectedHash, actualHash);
            }
            Directory.Delete(tempDir, true);
        }

        [TestMethod][TestCategory("Core")]
        public async Task CalculateFileHashAsync_FileDoesNotExist_ThrowsException()
        {
            var polygon = CreatePolygonForTest();
            var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nofile.txt");
            await Assert.ThrowsExceptionAsync<DirectoryNotFoundException>(async () =>
            {
                await polygon.CalculateFileHashAsync(filePath);
            });
        }
    }
}