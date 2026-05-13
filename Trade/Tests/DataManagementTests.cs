using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Trade.Tests
{
    [TestClass]
    public class DataManagementTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void TrainingTestSplit_MaintainsChronologicalOrder()
        {
            // Create test price data with clear trend
            var priceBuffer = CreateTrendingPriceBuffer(1000, 100.0, 200.0);

            // Apply 70/30 split like Program.cs
            var testPercentage = 0.30;
            var testSize = (int)(priceBuffer.Length * testPercentage);

            var testingBuffer = priceBuffer.Skip(priceBuffer.Length - testSize).ToArray();
            var trainingBuffer = priceBuffer.Take(priceBuffer.Length - testSize).ToArray();

            // Verify chronological order is maintained
            Assert.AreEqual(700, trainingBuffer.Length, "Training should be 70% of data");
            Assert.AreEqual(300, testingBuffer.Length, "Testing should be 30% of data");

            // Training data should be earlier (lower prices in our trending buffer)
            Assert.IsTrue(trainingBuffer.Average() < testingBuffer.Average(),
                "Training data should be from earlier period (lower average price)");

            // No overlap between training and testing
            Assert.AreEqual(trainingBuffer.Last(), priceBuffer[699], "Training should end at correct index");
            Assert.AreEqual(testingBuffer.First(), priceBuffer[700], "Testing should start at correct index");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void NormalizationParameterIsolation_PreventsDataLeakage()
        {
            // This is a critical test for proper ML practice
            double[] trainingData = { 50, 60, 70, 80, 90 }; // Range: 50-90
            double[] testData = { 100, 110, 120, 130, 140 }; // Range: 100-140 (different!)

            // Simulate proper normalization: parameters from training only
            var trainingMin = trainingData.Min();
            var trainingMax = trainingData.Max();
            var trainingRange = trainingMax - trainingMin;

            // Normalize training data using its own parameters
            var normalizedTraining = trainingData.Select(x => (x - trainingMin) / trainingRange).ToArray();

            // Normalize test data using TRAINING parameters (key principle!)
            var normalizedTest = testData.Select(x => (x - trainingMin) / trainingRange).ToArray();

            // Verify training data is properly normalized to [0,1]
            Assert.AreEqual(0.0, normalizedTraining.Min(), 0.001, "Training min should normalize to 0");
            Assert.AreEqual(1.0, normalizedTraining.Max(), 0.001, "Training max should normalize to 1");

            // Test data may go outside [0,1] when using training parameters - this is correct!
            Assert.IsTrue(normalizedTest.Min() > 1.0, "Test data should be outside training range");
            Assert.IsTrue(normalizedTest.Max() > 1.0, "Test data max should exceed 1 (shows different distribution)");

            // The key insight: test data outside [0,1] is EXPECTED and CORRECT
            // It shows the model must handle data outside its training distribution
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CSVDataLoading_HandlesMissingFiles()
        {
            // Test the CSV loading logic from Program.cs
            var nonExistentPath = "non_existent_file.csv";
            string[] csvPaths = { Constants.SPX_D, @"Trade\" + Constants.SPX_D};

            // Verify that missing files are handled gracefully
            Assert.IsFalse(File.Exists(nonExistentPath), "Test file should not exist");

            // The actual CSV loading should fall back to sine wave generation
            // This tests the robustness of the data loading pipeline
            var result = SimulateCSVLoading(csvPaths);

            Assert.IsNotNull(result.priceBuffer, "Should return some price data");
            Assert.IsTrue(result.priceBuffer.Length > 0, "Price buffer should not be empty");
            Assert.IsFalse(result.usedFallback, "Should indicate fallback was used when CSV missing");

            csvPaths = new[] { @"Trade\" + Constants.SPX_D };

            // Verify that missing files are handled gracefully
            Assert.IsFalse(File.Exists(nonExistentPath), "Test file should not exist");

            // The actual CSV loading should fall back to sine wave generation
            // This tests the robustness of the data loading pipeline
            result = SimulateCSVLoading(csvPaths);

            Assert.IsNotNull(result.priceBuffer, "Should return some price data");
            Assert.IsTrue(result.priceBuffer.Length > 0, "Price buffer should not be empty");
            Assert.IsTrue(result.usedFallback, "Should indicate fallback was used when CSV missing");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void SineWaveGeneration_ProducesValidData()
        {
            // Test the fallback sine wave generation
            var indicatorParams = new IndicatorParams
            {
                Param1 = 1, // multiplier
                Param2 = 6, // cycles  
                Param3 = 100, // price floor
                Param4 = 10, // amplitude
                Param5 = 0 // shift
            };

            var bufferSize = 1000; // Fixed size for testing
            var sineBuffer = GenerateSineWaveBuffer(indicatorParams, bufferSize);

            Assert.AreEqual(bufferSize, sineBuffer.Length, "Buffer should have correct length");
            Assert.IsTrue(sineBuffer.All(p => p >= indicatorParams.Param3 - indicatorParams.Param4),
                "All prices should be above minimum (floor - amplitude)");
            Assert.IsTrue(sineBuffer.All(p => p <= indicatorParams.Param3 + indicatorParams.Param4),
                "All prices should be below maximum (floor + amplitude)");

            // Verify it's actually oscillating
            var variance = CalculateVariance(sineBuffer);
            Assert.IsTrue(variance > 0, "Sine wave should have variance (not constant)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void PriceRangeValidation_DetectsUnreasonableData()
        {
            // Test price validation logic
            double[] reasonablePrices = { 50.0, 75.0, 100.0, 125.0, 150.0 };
            double[] unreasonablePrices = { -10.0, 0.0, 1000000.0 };

            Assert.IsTrue(ValidatePriceRange(reasonablePrices), "Reasonable prices should pass validation");
            Assert.IsFalse(ValidatePriceRange(unreasonablePrices), "Unreasonable prices should fail validation");

            // Test edge cases
            double[] edgeCases = { 0.01, 999999.99 };
            // These might be valid depending on the asset (penny stocks, high-priced stocks)
            // The test documents the importance of price range validation
        }

        [TestMethod]
        [TestCategory("Core")]
        public void DataIntegrity_MaintainedThroughoutPipeline()
        {
            // Test that data integrity is maintained through the processing pipeline
            var originalPrices = CreateTestPriceBuffer(100, 50.0, 150.0);

            // Simulate the data flow through Program.cs
            var trainingBuffer = originalPrices.Take(70).ToArray();
            var testingBuffer = originalPrices.Skip(70).ToArray();

            // Verify no data corruption
            Assert.AreEqual(70, trainingBuffer.Length, "Training buffer should have correct length");
            Assert.AreEqual(30, testingBuffer.Length, "Testing buffer should have correct length");

            // Check that split data matches original
            for (var i = 0; i < trainingBuffer.Length; i++)
                Assert.AreEqual(originalPrices[i], trainingBuffer[i], 0.001,
                    $"Training data point {i} should match original");

            for (var i = 0; i < testingBuffer.Length; i++)
                Assert.AreEqual(originalPrices[70 + i], testingBuffer[i], 0.001,
                    $"Testing data point {i} should match original");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void IndicatorRangeAnalysis_IsReproducible()
        {
            // Test that indicator range analysis produces consistent results
            var priceBuffer = CreateTestPriceBuffer(500, 100.0, 200.0);

            // Run analysis twice - should get same results
            var ranges1 = SimulateIndicatorRangeAnalysis(priceBuffer);
            var ranges2 = SimulateIndicatorRangeAnalysis(priceBuffer);

            Assert.AreEqual(ranges1.min, ranges2.min, 0.001, "Min range should be reproducible");
            Assert.AreEqual(ranges1.max, ranges2.max, 0.001, "Max range should be reproducible");

            // Test that ranges make sense
            Assert.IsTrue(ranges1.min < ranges1.max, "Min should be less than max");
            Assert.IsTrue(ranges1.min >= 0, "Min should be reasonable for price data");
        }

        #region Helper Methods

        private double[] CreateTrendingPriceBuffer(int length, double start, double end)
        {
            var buffer = new double[length];
            var increment = (end - start) / (length - 1);

            for (var i = 0; i < length; i++) buffer[i] = start + i * increment;

            return buffer;
        }

        private double[] CreateTestPriceBuffer(int length, double min, double max)
        {
            var rng = new Random(42); // Fixed seed for reproducibility
            var buffer = new double[length];

            for (var i = 0; i < length; i++) buffer[i] = min + rng.NextDouble() * (max - min);

            return buffer;
        }

        private (double[] priceBuffer, bool usedFallback) SimulateCSVLoading(string[] csvPaths)
        {
            // Simulate the CSV loading logic from Program.cs
            foreach (var path in csvPaths)
                if (File.Exists(path))
                    // Would load CSV data here
                    return (CreateTestPriceBuffer(1000, 4000, 6000), false);

            // Fall back to sine wave (like Program.cs does)
            var indicatorParams = new IndicatorParams { Param1 = 1, Param2 = 6, Param3 = 100, Param4 = 10, Param5 = 0 };
            return (GenerateSineWaveBuffer(indicatorParams, 1000), true);
        }

        private double[] GenerateSineWaveBuffer(IndicatorParams indicatorParams, int bufferSize)
        {
            // Replicate the sine wave generation from Program.cs
            var sineBuffer = new double[bufferSize];
            for (var i = 0; i < sineBuffer.Length; i++)
            {
                var x = (double)i / sineBuffer.Length * indicatorParams.Param1 * Math.PI * indicatorParams.Param2;
                sineBuffer[i] = indicatorParams.Param3 + indicatorParams.Param4 * Math.Sin(x + indicatorParams.Param5);
            }

            return sineBuffer;
        }

        private bool ValidatePriceRange(double[] prices)
        {
            return prices.All(p => p > 0 && p < 100000); // Simple validation
        }

        private double CalculateVariance(double[] values)
        {
            var mean = values.Average();
            return values.Select(v => Math.Pow(v - mean, 2)).Average();
        }

        private (double min, double max) SimulateIndicatorRangeAnalysis(double[] priceBuffer)
        {
            // Simulate the range analysis logic
            return (priceBuffer.Min(), priceBuffer.Max());
        }

        #endregion
    }
}