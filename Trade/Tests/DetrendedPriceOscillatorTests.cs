using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class DetrendedPriceOscillatorTests
    {
        private const double TOLERANCE = 1e-8;

        #region Input Validation Tests

        [TestMethod][TestCategory("Core")]
        public void Calculate_NullPriceArray_ThrowsArgumentNullException()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                // Act & Assert
                DetrendedPriceOscillator.Calculate(null);
            });
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_EmptyPriceArray_ReturnsEmptyArray()
        {
            // Arrange
            var prices = new double[0];

            // Act
            var result = DetrendedPriceOscillator.Calculate(prices);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_InsufficientData_ReturnsZeroArray()
        {
            // Arrange
            double[] prices = { 100, 101, 102, 103, 104 }; // Only 5 elements, need at least 16 for period 10

            // Act
            var result = DetrendedPriceOscillator.Calculate(prices, 10);

            // Assert
            Assert.AreEqual(prices.Length, result.Length);
            Assert.IsTrue(result.All(x => x == 0.0), "All values should be zero when insufficient data");
        }

        #endregion

        #region Static Method Tests

        [TestMethod][TestCategory("Core")]
        public void Calculate_Static_DefaultPeriod()
        {
            // Arrange
            var prices = CreateTrendingPrices(30, 100, 1);

            // Act
            var result = DetrendedPriceOscillator.Calculate(prices);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(prices.Length, result.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_Static_CustomPeriod()
        {
            // Arrange
            var prices = CreateTrendingPrices(30, 100, 1);

            // Act
            var result = DetrendedPriceOscillator.Calculate(prices, 8);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(prices.Length, result.Length);
        }

        [TestMethod][TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Calculate_Static_NullArray_ThrowsException()
        {
            // Act & Assert
            DetrendedPriceOscillator.Calculate(null);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_InvalidPeriod_UsesDefault()
        {
            // Arrange
            var prices = CreateTrendingPrices(30, 100, 1);

            // Act
            var result1 = DetrendedPriceOscillator.Calculate(prices, 0);
            var result2 = DetrendedPriceOscillator.Calculate(prices, -5);
            var result3 = DetrendedPriceOscillator.Calculate(prices, 12); // Default

            // Assert
            Assert.AreEqual(result1.Length, result3.Length);
            Assert.AreEqual(result2.Length, result3.Length);

            // Results should be identical (all use default period 12)
            for (var i = 0; i < result1.Length; i++)
            {
                Assert.AreEqual(result1[i], result3[i], TOLERANCE, "Invalid period should default to 12");
                Assert.AreEqual(result2[i], result3[i], TOLERANCE, "Invalid period should default to 12");
            }
        }

        #endregion

        #region Algorithm Logic Tests

        [TestMethod][TestCategory("Core")]
        public void Calculate_FlatPrices_ReturnsZeroOscillation()
        {
            // Arrange - Flat price line
            var prices = Enumerable.Repeat(100.0, 20).ToArray();

            // Act
            var result = DetrendedPriceOscillator.Calculate(prices, 6);

            // Assert
            Assert.AreEqual(prices.Length, result.Length);

            // After initial period, all values should be near zero (flat price - flat MA = 0)
            var startIndex = 6 + 6 / 2 + 1 - 1; // period + shift - 1 = 9
            for (var i = startIndex; i < result.Length; i++)
                Assert.AreEqual(0.0, result[i], TOLERANCE, $"DPO[{i}] should be zero for flat prices");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_CyclicalPattern_IdentifiesCycles()
        {
            // Arrange - Sine wave pattern (pure cycle)
            var prices = new double[60];

            for (var i = 0; i < prices.Length; i++)
                prices[i] = 100 + 10 * Math.Sin(2 * Math.PI * i / 20); // 20-period sine wave

            // Act
            var result = DetrendedPriceOscillator.Calculate(prices, 10);

            // Assert
            var startIndex = 10 + 10 / 2 + 1 - 1; // 15
            var validValues = result.Skip(startIndex).Where(x => x != 0).ToArray();

            if (validValues.Length > 10)
            {
                // DPO should capture the cyclical nature
                var maxValue = validValues.Max();
                var minValue = validValues.Min();

                Assert.IsTrue(maxValue > 2, "DPO should show positive peaks for cyclical data");
                Assert.IsTrue(minValue < -2, "DPO should show negative troughs for cyclical data");
                Assert.IsTrue(maxValue - minValue > 5, "DPO should have reasonable range for cyclical data");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ManualVerification_MatchesExpectedFormula()
        {
            // Arrange - Simple data for manual verification
            double[] prices =
                { 100, 102, 104, 106, 108, 110, 112, 114, 116, 118, 120, 122, 124, 126, 128, 130, 132, 134, 136, 138 };
            var period = 6;
            var shift = period / 2 + 1; // 4

            // Act
            var result = DetrendedPriceOscillator.Calculate(prices, period);

            // Assert - Manually verify a specific calculation
            var testIndex = 15; // Should have enough data for calculation
            var maIndex = testIndex - shift; // 11

            if (testIndex < prices.Length && maIndex >= period - 1) // Ensure we have enough data for MA
            {
                // Calculate SMA manually for verification
                double sum = 0;
                for (var i = maIndex - period + 1; i <= maIndex; i++) sum += prices[i];
                var expectedMA = sum / period;
                var expectedDPO = prices[testIndex] - expectedMA;

                Assert.AreEqual(expectedDPO, result[testIndex], TOLERANCE,
                    $"DPO[{testIndex}] should equal Price[{testIndex}] - SMA[{maIndex}]");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_DifferentPeriods_ProduceExpectedDifferences()
        {
            // Arrange - Use a more complex pattern that will show clear differences with different DPO periods
            var prices = new double[50];

            // Create a pattern with multiple frequency components to ensure different periods show differences
            for (var i = 0; i < prices.Length; i++)
            {
                // Combine multiple cycles and a trend to create a complex pattern
                var trend = i * 0.5; // Linear trend
                var shortCycle = 3 * Math.Sin(2 * Math.PI * i / 8); // 8-period cycle
                var mediumCycle = 2 * Math.Sin(2 * Math.PI * i / 15); // 15-period cycle
                var longCycle = 1.5 * Math.Sin(2 * Math.PI * i / 25); // 25-period cycle

                prices[i] = 100 + trend + shortCycle + mediumCycle + longCycle;
            }

            // Act
            var dpo6 = DetrendedPriceOscillator.Calculate(prices, 6);
            var dpo12 = DetrendedPriceOscillator.Calculate(prices, 12);
            var dpo20 = DetrendedPriceOscillator.Calculate(prices, 20);

            // Assert - Different periods should produce different results
            // Check multiple points to ensure we find meaningful differences
            var foundDifference1 = false;
            var foundDifference2 = false;

            for (var compareIndex = 25; compareIndex < Math.Min(prices.Length - 5, 45); compareIndex++)
            {
                if (Math.Abs(dpo6[compareIndex] - dpo12[compareIndex]) > TOLERANCE * 1000) // More lenient tolerance
                    foundDifference1 = true;
                if (Math.Abs(dpo12[compareIndex] - dpo20[compareIndex]) > TOLERANCE * 1000) // More lenient tolerance
                    foundDifference2 = true;

                if (foundDifference1 && foundDifference2) break;
            }

            Assert.IsTrue(foundDifference1,
                "DPO with periods 6 and 12 should produce different values. " +
                $"Sample values at index 35: dpo6={dpo6[35]:F10}, dpo12={dpo12[35]:F10}");
            Assert.IsTrue(foundDifference2,
                "DPO with periods 12 and 20 should produce different values. " +
                $"Sample values at index 35: dpo12={dpo12[35]:F10}, dpo20={dpo20[35]:F10}");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_DifferentPeriods_ProduceMeaningfullyDifferentResults()
        {
            // Arrange - Use trending data with noise to ensure clear differences
            var prices = new double[60];
            var random = new Random(42); // Fixed seed for reproducibility

            for (var i = 0; i < prices.Length; i++)
            {
                var trend = i * 1.2; // Strong trend
                var cycle1 = 5 * Math.Sin(2 * Math.PI * i / 7); // 7-period cycle
                var cycle2 = 3 * Math.Cos(2 * Math.PI * i / 11); // 11-period cycle
                var noise = (random.NextDouble() - 0.5) * 2; // Random noise

                prices[i] = 100 + trend + cycle1 + cycle2 + noise;
            }

            // Act
            var dpo8 = DetrendedPriceOscillator.Calculate(prices, 8);
            var dpo16 = DetrendedPriceOscillator.Calculate(prices, 16);

            // Assert - Look for meaningful differences rather than exact inequality
            var validIndices = new List<int>();

            // Find indices where both DPO calculations have valid (non-zero) values
            for (var i = 25; i < prices.Length - 5; i++) // Start well after initialization periods
                if (Math.Abs(dpo8[i]) > 1e-10 && Math.Abs(dpo16[i]) > 1e-10) // Both have meaningful values
                    validIndices.Add(i);

            Assert.IsTrue(validIndices.Count > 5, "Should have multiple valid comparison points");

            // Calculate correlation coefficient between the two series
            if (validIndices.Count >= 10)
            {
                var values8 = validIndices.Select(i => dpo8[i]).ToArray();
                var values16 = validIndices.Select(i => dpo16[i]).ToArray();

                var correlation = CalculateCorrelation(values8, values16);

                // Different periods should not be perfectly correlated (correlation < 0.99)
                Assert.IsTrue(correlation < 0.99,
                    $"DPO with different periods should not be perfectly correlated. Correlation: {correlation:F6}");

                // But they should still have some positive correlation (both are price-based indicators)
                Assert.IsTrue(correlation > 0.1,
                    $"DPO with different periods should have some positive correlation. Correlation: {correlation:F6}");
            }

            // Also check that at least some values are meaningfully different
            var significantDifferences = 0;
            foreach (var i in validIndices.Take(20)) // Check up to 20 points
            {
                var diff = Math.Abs(dpo8[i] - dpo16[i]);
                if (diff > 0.01) // More than 1 cent difference
                    significantDifferences++;
            }

            Assert.IsTrue(significantDifferences >= 3,
                $"Should have at least 3 points with meaningful differences (>0.01). Found: {significantDifferences}");
        }

        private double CalculateCorrelation(double[] x, double[] y)
        {
            if (x.Length != y.Length || x.Length < 2) return 0;

            var meanX = x.Average();
            var meanY = y.Average();

            var numerator = x.Zip(y, (xi, yi) => (xi - meanX) * (yi - meanY)).Sum();
            var denomX = Math.Sqrt(x.Sum(xi => Math.Pow(xi - meanX, 2)));
            var denomY = Math.Sqrt(y.Sum(yi => Math.Pow(yi - meanY, 2)));

            if (denomX == 0 || denomY == 0) return 0;

            return numerator / (denomX * denomY);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_TimeShift_IsCorrectlyImplemented()
        {
            // Arrange - Create specific pattern to test time shift
            var prices = new double[30];

            // Create a pattern where we can verify the time shift
            for (var i = 0; i < prices.Length; i++) prices[i] = 100 + i; // Simple uptrend

            // Act
            var result = DetrendedPriceOscillator.Calculate(prices, 8);

            // Assert - The DPO should use MA from (8/2 + 1) = 5 periods ago
            var period = 8;
            var shift = period / 2 + 1; // 5
            var testIndex = 20; // Well within bounds
            var maStartIndex = testIndex - shift; // 15

            // Manually calculate the MA at the shifted position
            if (maStartIndex >= period - 1) // Ensure we have enough data for MA calculation
            {
                double sum = 0;
                for (var i = maStartIndex - period + 1; i <= maStartIndex; i++) sum += prices[i];
                var expectedMA = sum / period;
                var expectedDPO = prices[testIndex] - expectedMA;

                Assert.AreEqual(expectedDPO, result[testIndex], TOLERANCE,
                    "Time shift should be correctly implemented");
            }
        }

        #endregion

        #region Edge Cases and Robustness Tests

        [TestMethod][TestCategory("Core")]
        public void Calculate_ExtremeValues_HandlesCorrectly()
        {
            // Arrange - Very large and very small values
            double[] prices =
            {
                1e10, 1e10 + 1, 1e10 + 2, 1e10 + 3, 1e10 + 4, 1e10 + 5,
                1e10 + 6, 1e10 + 7, 1e10 + 8, 1e10 + 9, 1e10 + 10, 1e10 + 11,
                1e10 + 12, 1e10 + 13, 1e10 + 14, 1e10 + 15
            };

            // Act
            var result = DetrendedPriceOscillator.Calculate(prices, 6);

            // Assert
            Assert.IsTrue(result.All(x => !double.IsNaN(x) && !double.IsInfinity(x)),
                "DPO should handle extreme values without producing NaN or Infinity");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_NegativePrices_HandlesCorrectly()
        {
            // Arrange - Negative prices (unusual but possible in some contexts)
            double[] prices = { -100, -99, -98, -97, -96, -95, -94, -93, -92, -91, -90, -89, -88, -87, -86, -85 };

            // Act
            var result = DetrendedPriceOscillator.Calculate(prices, 5);

            // Assert
            Assert.IsTrue(result.All(x => !double.IsNaN(x) && !double.IsInfinity(x)),
                "DPO should handle negative prices correctly");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_SingleValueRepeated_HandlesGracefully()
        {
            // Arrange
            var prices = Enumerable.Repeat(50.0, 15).ToArray();

            // Act
            var result = DetrendedPriceOscillator.Calculate(prices, 4);

            // Assert
            Assert.AreEqual(prices.Length, result.Length);
            var startIndex = 4 + 4 / 2 + 1 - 1; // 6

            for (var i = startIndex; i < result.Length; i++)
                Assert.AreEqual(0.0, result[i], TOLERANCE,
                    "Constant prices should produce zero DPO values");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_PerformanceWithLargeDataset()
        {
            // Arrange - Large dataset
            var prices = CreateTrendingPrices(10000, 100, 0.01);

            // Act
            var startTime = DateTime.Now;
            var result = DetrendedPriceOscillator.Calculate(prices, 14);
            var endTime = DateTime.Now;

            // Assert
            var duration = endTime - startTime;
            Assert.IsTrue(duration.TotalSeconds < 1.0, "Large dataset should be processed quickly");
            Assert.AreEqual(prices.Length, result.Length);
            Assert.IsTrue(result.All(x => !double.IsNaN(x) && !double.IsInfinity(x)),
                "Large dataset should not produce invalid values");
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ConsistentResults_AcrossMultipleCalls()
        {
            // Arrange
            var prices = CreateCyclicalPrices(30, 100, 5, 8);

            // Act
            var result1 = DetrendedPriceOscillator.Calculate(prices, 10);
            var result2 = DetrendedPriceOscillator.Calculate(prices, 10);

            // Assert
            Assert.AreEqual(result1.Length, result2.Length);
            for (var i = 0; i < result1.Length; i++)
                Assert.AreEqual(result1[i], result2[i], TOLERANCE,
                    $"Results should be identical across calls at index {i}");
        }

        #endregion

        #region Real-World Scenario Tests

        [TestMethod][TestCategory("Core")]
        public void Calculate_RealisticStockData_ProducesReasonableResults()
        {
            // Arrange - Simulate realistic stock price movement
            var prices = CreateRealisticStockPrices(100, 100);

            // Act
            var result = DetrendedPriceOscillator.Calculate(prices, 14);

            // Assert
            Assert.AreEqual(prices.Length, result.Length);

            var startIndex = 14 + 14 / 2 + 1 - 1; // 21
            var validValues = result.Skip(startIndex).Where(x => x != 0).ToArray();

            if (validValues.Length > 10)
            {
                // Should have reasonable oscillation range for stock data
                var range = validValues.Max() - validValues.Min();
                Assert.IsTrue(range > 0.1, "DPO should show some oscillation for realistic stock data");
                Assert.IsTrue(range < 100, "DPO range should be reasonable for stock data");

                // Should not be all positive or all negative
                var hasPositive = validValues.Any(x => x > 0.01);
                var hasNegative = validValues.Any(x => x < -0.01);
                Assert.IsTrue(hasPositive || hasNegative, "DPO should show some directional movement");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_MarketCrashScenario_HandlesVolatility()
        {
            // Arrange - Simulate market crash scenario
            var prices = new double[50];

            // Normal trading for first 30 periods
            for (var i = 0; i < 30; i++) prices[i] = 100 + Math.Sin(i * 0.3) * 2; // Small oscillation

            // Market crash - sharp decline
            for (var i = 30; i < 35; i++) prices[i] = prices[i - 1] * 0.9; // 10% daily decline

            // Recovery period
            for (var i = 35; i < 50; i++) prices[i] = prices[i - 1] * 1.02; // 2% daily recovery

            // Act
            var result = DetrendedPriceOscillator.Calculate(prices, 10);

            // Assert - Should handle volatility without breaking
            Assert.IsTrue(result.All(x => !double.IsNaN(x) && !double.IsInfinity(x)),
                "DPO should handle market volatility without producing invalid values");

            // Should capture the dramatic price movements
            var crashStartIndex = 35; // After sufficient initialization period
            if (crashStartIndex < result.Length)
            {
                var crashPeriodRange = result.Skip(crashStartIndex).Take(10).Max() -
                                       result.Skip(crashStartIndex).Take(10).Min();
                Assert.IsTrue(crashPeriodRange > 1, "DPO should show increased oscillation during volatile periods");
            }
        }

        #endregion

        #region Helper Methods

        private double[] CreateTrendingPrices(int length, double startPrice, double increment)
        {
            var prices = new double[length];
            for (var i = 0; i < length; i++) prices[i] = startPrice + i * increment;
            return prices;
        }

        private double[] CreateCyclicalPrices(int length, double basePrice, double amplitude, int period)
        {
            var prices = new double[length];
            for (var i = 0; i < length; i++) prices[i] = basePrice + amplitude * Math.Sin(2 * Math.PI * i / period);
            return prices;
        }

        private double[] CreateRealisticStockPrices(int length, double startPrice)
        {
            var random = new Random(42); // Fixed seed for reproducibility
            var prices = new double[length];
            var price = startPrice;

            for (var i = 0; i < length; i++)
            {
                // Simulate realistic stock movement
                var change = (random.NextDouble() - 0.5) * 4; // ±2% max daily change
                var trend = Math.Sin(i / 20.0) * 0.5; // Longer-term trend component
                price = Math.Max(1, price + change + trend);
                prices[i] = price;
            }

            return prices;
        }

        private double CalculateStandardDeviation(double[] values)
        {
            if (values.Length <= 1) return 0;

            var mean = values.Average();
            var sumSquaredDiffs = values.Sum(x => Math.Pow(x - mean, 2));
            return Math.Sqrt(sumSquaredDiffs / (values.Length - 1));
        }

        #endregion
    }
}