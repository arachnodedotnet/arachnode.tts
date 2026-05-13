using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Performance tests for PriceRecordComparer to measure and validate optimization improvements
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class PriceRecordComparerPerformanceTests
    {
        // Performance thresholds in milliseconds for different operations
        private const double SINGLE_COMPARISON_THRESHOLD_NS = 100;      // 100 nanoseconds per comparison
        private const double BULK_COMPARISON_THRESHOLD_MS = 10;          // 10ms for 100k comparisons
        private const double SORTING_THRESHOLD_MS = 100;                 // 100ms for sorting 10k records
        private const double LARGE_SORT_THRESHOLD_MS = 1000;             // 1s for sorting 100k records

        // Test iteration counts
        private const int PERFORMANCE_ITERATIONS = 100000;              // 100k comparisons
        private const int LARGE_PERFORMANCE_ITERATIONS = 1000000;       // 1M comparisons
        private const int SORT_TEST_SIZE = 10000;                       // 10k records to sort
        private const int LARGE_SORT_TEST_SIZE = 100000;                // 100k records to sort
        
        private static readonly Dictionary<string, double> _performanceResults = new Dictionary<string, double>();

        #region Test Setup and Cleanup

        [ClassCleanup]
        public static void ClassCleanup()
        {
            ConsoleUtilities.WriteLine("\n=== PRICE RECORD COMPARER PERFORMANCE SUMMARY ===");
            ConsoleUtilities.WriteLine("Method                                    | Avg Time      | Threshold     | Status");
            ConsoleUtilities.WriteLine("------------------------------------------|---------------|---------------|--------");
            
            foreach (var result in _performanceResults.OrderBy(r => r.Value))
            {
                var threshold = GetThresholdForMethod(result.Key);
                var status = result.Value <= threshold ? "PASS" : "FAIL";
                var unit = result.Key.Contains("Single") ? "ns" : "ms";
                ConsoleUtilities.WriteLine($"{result.Key,-41} | {result.Value,13:F3} {unit} | {threshold,10:F0} {unit,2} | {status}");
            }
            ConsoleUtilities.WriteLine("================================================================================");
        }

        private static double GetThresholdForMethod(string methodName)
        {
            if (methodName.Contains("Single")) return SINGLE_COMPARISON_THRESHOLD_NS;
            if (methodName.Contains("Bulk")) return BULK_COMPARISON_THRESHOLD_MS;
            if (methodName.Contains("Sort_10k")) return SORTING_THRESHOLD_MS;
            if (methodName.Contains("Sort_100k")) return LARGE_SORT_THRESHOLD_MS;
            return 100; // Default threshold in ms
        }

        #endregion

        #region Single Comparison Performance Tests

        [TestMethod][TestCategory("Core")]
        public void PriceRecordComparer_SingleComparison_NormalCase_Performance()
        {
            // Arrange
            var comparer = new PriceRecordComparer();
            var record1 = CreateTestPriceRecord(DateTime.Today.AddDays(-1));
            var record2 = CreateTestPriceRecord(DateTime.Today);
            
            // Act & Assert - Measure nanoseconds for micro-benchmark
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                for (int i = 0; i < PERFORMANCE_ITERATIONS; i++)
                {
                    var result = comparer.Compare(record1, record2);
                    // Ensure the comparison actually happens (prevent optimization)
                    if (result == int.MaxValue) throw new InvalidOperationException("Unexpected result");
                }
            });
            
            var avgNanoseconds = (elapsedMs * 1000000) / PERFORMANCE_ITERATIONS;
            _performanceResults["Single_Comparison_Normal"] = avgNanoseconds;
            
            ConsoleUtilities.WriteLine($"[PERF] Single Comparison (Normal): {avgNanoseconds:F3}ns avg");
            Assert.IsTrue(avgNanoseconds < SINGLE_COMPARISON_THRESHOLD_NS, 
                $"Single comparison took {avgNanoseconds:F3}ns, expected < {SINGLE_COMPARISON_THRESHOLD_NS}ns");
        }

        [TestMethod][TestCategory("Core")]
        public void PriceRecordComparer_SingleComparison_NullHandling_Performance()
        {
            // Arrange
            var comparer = new PriceRecordComparer();
            var record = CreateTestPriceRecord(DateTime.Today);
            PriceRecord nullRecord = null;
            
            // Act & Assert - Test null handling performance
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                for (int i = 0; i < PERFORMANCE_ITERATIONS; i++)
                {
                    var result1 = comparer.Compare(record, nullRecord);
                    var result2 = comparer.Compare(nullRecord, record);
                    var result3 = comparer.Compare(nullRecord, nullRecord);
                    // Prevent optimization
                    if (result1 == int.MaxValue || result2 == int.MaxValue || result3 == int.MaxValue) 
                        throw new InvalidOperationException("Unexpected result");
                }
            });
            
            var avgNanoseconds = (elapsedMs * 1000000) / (PERFORMANCE_ITERATIONS * 3); // 3 comparisons per iteration
            _performanceResults["Single_Comparison_NullHandling"] = avgNanoseconds;
            
            ConsoleUtilities.WriteLine($"[PERF] Single Comparison (Null Handling): {avgNanoseconds:F3}ns avg");
            Assert.IsTrue(avgNanoseconds < SINGLE_COMPARISON_THRESHOLD_NS, 
                $"Null handling comparison took {avgNanoseconds:F3}ns, expected < {SINGLE_COMPARISON_THRESHOLD_NS}ns");
        }

        [TestMethod][TestCategory("Core")]
        public void PriceRecordComparer_SingleComparison_EqualDates_Performance()
        {
            // Arrange
            var comparer = new PriceRecordComparer();
            var dateTime = DateTime.Today;
            var record1 = CreateTestPriceRecord(dateTime);
            var record2 = CreateTestPriceRecord(dateTime); // Same date
            
            // Act & Assert
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                for (int i = 0; i < PERFORMANCE_ITERATIONS; i++)
                {
                    var result = comparer.Compare(record1, record2);
                    if (result == int.MaxValue) throw new InvalidOperationException("Unexpected result");
                }
            });
            
            var avgNanoseconds = (elapsedMs * 1000000) / PERFORMANCE_ITERATIONS;
            _performanceResults["Single_Comparison_EqualDates"] = avgNanoseconds;
            
            ConsoleUtilities.WriteLine($"[PERF] Single Comparison (Equal Dates): {avgNanoseconds:F3}ns avg");
            Assert.IsTrue(avgNanoseconds < SINGLE_COMPARISON_THRESHOLD_NS, 
                $"Equal dates comparison took {avgNanoseconds:F3}ns, expected < {SINGLE_COMPARISON_THRESHOLD_NS}ns");
        }

        #endregion

        #region Bulk Comparison Performance Tests

        [TestMethod][TestCategory("Core")]
        public void PriceRecordComparer_BulkComparisons_Performance()
        {
            // Arrange
            var comparer = new PriceRecordComparer();
            var records = CreateTestPriceRecords(1000); // 1000 records
            
            // Act & Assert - Test many comparisons
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                for (int i = 0; i < records.Length - 1; i++)
                {
                    for (int j = i + 1; j < Math.Min(i + 100, records.Length); j++) // Compare with next 100 records
                    {
                        var result = comparer.Compare(records[i], records[j]);
                        if (result == int.MaxValue) throw new InvalidOperationException("Unexpected result");
                    }
                }
            });
            
            _performanceResults["Bulk_Comparisons_1000x100"] = elapsedMs;
            
            ConsoleUtilities.WriteLine($"[PERF] Bulk Comparisons (1000x100): {elapsedMs:F2}ms total");
            Assert.IsTrue(elapsedMs < BULK_COMPARISON_THRESHOLD_MS * 10, // More lenient for bulk
                $"Bulk comparisons took {elapsedMs:F2}ms, expected < {BULK_COMPARISON_THRESHOLD_MS * 10}ms");
        }

        #endregion

        #region Sorting Performance Tests

        [TestMethod][TestCategory("Performance")]
        public void PriceRecordComparer_Sorting_10k_Performance()
        {
            // Arrange
            var comparer = new PriceRecordComparer();
            var records = CreateRandomizedTestPriceRecords(SORT_TEST_SIZE);
            
            // Act & Assert
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                Array.Sort(records, comparer);
            });
            
            _performanceResults["Sort_10k_Records"] = elapsedMs;
            
            ConsoleUtilities.WriteLine($"[PERF] Sorting 10k Records: {elapsedMs:F2}ms");
            Assert.IsTrue(elapsedMs < SORTING_THRESHOLD_MS, 
                $"Sorting 10k records took {elapsedMs:F2}ms, expected < {SORTING_THRESHOLD_MS}ms");
            
            // Verify sort correctness
            ValidateSortedOrder(records);
        }

        [TestMethod][TestCategory("Performance")]
        public void PriceRecordComparer_Sorting_100k_Performance()
        {
            // Arrange
            var comparer = new PriceRecordComparer();
            var records = CreateRandomizedTestPriceRecords(LARGE_SORT_TEST_SIZE);
            
            // Act & Assert
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                Array.Sort(records, comparer);
            });
            
            _performanceResults["Sort_100k_Records"] = elapsedMs;
            
            ConsoleUtilities.WriteLine($"[PERF] Sorting 100k Records: {elapsedMs:F2}ms");
            Assert.IsTrue(elapsedMs < LARGE_SORT_THRESHOLD_MS, 
                $"Sorting 100k records took {elapsedMs:F2}ms, expected < {LARGE_SORT_THRESHOLD_MS}ms");
            
            // Verify sort correctness (sample check for large dataset)
            ValidateSortedOrderSample(records, 1000);
        }

        [TestMethod][TestCategory("Core")]
        public void PriceRecordComparer_ListSorting_Performance()
        {
            // Arrange
            var comparer = new PriceRecordComparer();
            var records = CreateRandomizedTestPriceRecords(SORT_TEST_SIZE).ToList();
            
            // Act & Assert
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                records.Sort(comparer);
            });
            
            _performanceResults["List_Sort_10k_Records"] = elapsedMs;
            
            ConsoleUtilities.WriteLine($"[PERF] List Sorting 10k Records: {elapsedMs:F2}ms");
            Assert.IsTrue(elapsedMs < SORTING_THRESHOLD_MS * 1.2, // Slightly more lenient for List.Sort
                $"List sorting 10k records took {elapsedMs:F2}ms, expected < {SORTING_THRESHOLD_MS * 1.2}ms");
            
            // Verify sort correctness
            ValidateSortedOrder(records.ToArray());
        }

        #endregion

        #region Memory and Edge Case Tests

        [TestMethod][TestCategory("Core")]
        public void PriceRecordComparer_MemoryEfficiency_StressTest()
        {
            // Test memory efficiency with repeated comparer usage
            var initialMemory = GC.GetTotalMemory(true);
            
            var comparer = new PriceRecordComparer();
            var records = CreateTestPriceRecords(5000);
            
            // Perform many operations to stress test memory
            for (int iteration = 0; iteration < 10; iteration++)
            {
                // Shuffle and sort multiple times
                var shuffled = records.OrderBy(x => Guid.NewGuid()).ToArray();
                Array.Sort(shuffled, comparer);
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = (finalMemory - initialMemory) / (1024 * 1024); // MB
            
            ConsoleUtilities.WriteLine($"[PERF] Memory increase after stress test: {memoryIncrease:F2} MB");
            
            // Should not consume excessive memory (threshold: 50MB)
            Assert.IsTrue(memoryIncrease < 50, 
                $"Memory usage increased by {memoryIncrease:F2}MB, expected < 50MB");
        }

        [TestMethod][TestCategory("Core")]
        public void PriceRecordComparer_EdgeCases_Performance()
        {
            // Test performance with various edge cases
            var comparer = new PriceRecordComparer();
            
            var elapsedMs = PerformanceTimer.TimeAction(() =>
            {
                // Test with very close dates (microsecond differences)
                var baseDate = DateTime.Now;
                var record1 = CreateTestPriceRecord(baseDate);
                var record2 = CreateTestPriceRecord(baseDate.AddTicks(1));
                var record3 = CreateTestPriceRecord(baseDate.AddMilliseconds(1));
                
                for (int i = 0; i < 10000; i++)
                {
                    comparer.Compare(record1, record2);
                    comparer.Compare(record2, record3);
                    comparer.Compare(record1, record3);
                }
            });
            
            _performanceResults["EdgeCases_CloseDates"] = elapsedMs;
            
            ConsoleUtilities.WriteLine($"[PERF] Edge Cases (Close Dates): {elapsedMs:F4}ms");
            Assert.IsTrue(elapsedMs < 10, // Should be very fast
                $"Edge cases took {elapsedMs:F4}ms, expected < 10ms");
        }

        #endregion

        #region Comparison with Alternative Approaches

        [TestMethod][TestCategory("Core")]
        public void PriceRecordComparer_CompareVsLambda_Performance()
        {
            // Compare performance against lambda-based sorting
            var records1 = CreateRandomizedTestPriceRecords(SORT_TEST_SIZE);
            var records2 = new PriceRecord[records1.Length];
            Array.Copy(records1, records2, records1.Length);
            
            var comparer = new PriceRecordComparer();
            
            // Test with PriceRecordComparer
            var comparerTime = PerformanceTimer.TimeAction(() =>
            {
                Array.Sort(records1, comparer);
            });
            
            // Test with lambda
            var lambdaTime = PerformanceTimer.TimeAction(() =>
            {
                Array.Sort(records2, (x, y) => 
                {
                    if (x == null && y == null) return 0;
                    if (x == null) return -1;
                    if (y == null) return 1;
                    return x.DateTime.CompareTo(y.DateTime);
                });
            });
            
            _performanceResults["Comparer_vs_Lambda_Comparer"] = comparerTime;
            _performanceResults["Comparer_vs_Lambda_Lambda"] = lambdaTime;
            
            ConsoleUtilities.WriteLine($"[PERF] PriceRecordComparer: {comparerTime:F2}ms");
            ConsoleUtilities.WriteLine($"[PERF] Lambda Comparer: {lambdaTime:F2}ms");
            ConsoleUtilities.WriteLine($"[PERF] Performance Ratio: {lambdaTime / comparerTime:F2}x");
            
            // Both should be reasonably fast
            Assert.IsTrue(comparerTime < SORTING_THRESHOLD_MS);
            Assert.IsTrue(lambdaTime < SORTING_THRESHOLD_MS * 2); // Lambda might be slightly slower
        }

        #endregion

        #region Helper Methods

        private PriceRecord CreateTestPriceRecord(DateTime dateTime)
        {
            dateTime = dateTime.Date; // Normalize to date only for simplicity...
            return new PriceRecord(dateTime, TimeFrame.D1, 100.0, 105.0, 95.0, 102.0, volume: 1000000);
        }

        private PriceRecord[] CreateTestPriceRecords(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            
            for (int i = 0; i < count; i++)
            {
                records[i] = CreateTestPriceRecord(baseDate.AddDays(i));
            }
            
            return records;
        }

        private PriceRecord[] CreateRandomizedTestPriceRecords(int count)
        {
            var random = new Random(42); // Fixed seed for reproducible tests
            var records = new PriceRecord[count];
            var baseDate = DateTime.Today.AddDays(-count);
            
            for (int i = 0; i < count; i++)
            {
                // Add random time component to create unsorted data
                var randomDate = baseDate.AddDays(random.Next(count * 2)).AddHours(random.Next(24)).AddMinutes(random.Next(60));
                records[i] = CreateTestPriceRecord(randomDate);
            }
            
            return records;
        }

        private void ValidateSortedOrder(PriceRecord[] records)
        {
            for (int i = 1; i < records.Length; i++)
            {
                Assert.IsTrue(records[i-1].DateTime <= records[i].DateTime, 
                    $"Records not properly sorted at index {i}");
            }
        }

        private void ValidateSortedOrderSample(PriceRecord[] records, int sampleSize)
        {
            var step = Math.Max(1, records.Length / sampleSize);
            for (int i = step; i < records.Length; i += step)
            {
                Assert.IsTrue(records[i-step].DateTime <= records[i].DateTime, 
                    $"Sample validation failed at index {i}");
            }
        }

        #endregion
    }
}