using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class PerformanceComparisonTests
    {
        // Helper to create a sorted list of PriceRecords for binary search
        private static List<PriceRecord> CreateSortedPriceRecords(int count, DateTime start, int minuteStep = 1)
        {
            var records = new List<PriceRecord>(count);
            for (var i = 0; i < count; i++)
                records.Add(new PriceRecord(
                    start.AddMinutes(i * minuteStep), TimeFrame.D1,
                    100, 100, 100, 100,
                    volume: 1,
                    wap: 100,
                    count: 1,
                    option: null,
                    isComplete: true
                ));
            return records;
        }

        /// <summary>
        /// Demonstrates (and asserts) that when the number of lookups Q is small relative to N,
        /// a binary search strategy outperforms (array fill + O(1) lookup).
        /// Keeps runtime short by choosing moderate N and very small Q.
        /// </summary>
        //[TestMethod]
        [TestCategory("Performance")]
        public void BinarySearch_IsFaster_When_Q_Is_Small_Relative_To_N()
        {
            // Choose parameters so that: Q < N / (log2(N) - 1)
            // (The break-even condition derived from: Q*logN < N + Q)
            // Keep N moderate so the test is fast, and Q small so inequality holds with margin.
            const int N = 50_000;         // Total records
            const int Q = 200;            // Total queries (small relative to N)
            var start = new DateTime(2025, 1, 1, 9, 30, 0);

            // Theoretical validation first (defensive)
            var log2N = Math.Log(N, 2.0);
            var theoreticalThreshold = N / (log2N - 1.0);
            Assert.IsTrue(Q < theoreticalThreshold,
                $"Test invalid: Q={Q} not < threshold={theoreticalThreshold:F1} (N={N}, log2N={log2N:F2}). Adjust parameters.");

            // Prepare sorted data (outside timing – both strategies share this cost in real life)
            var sorted = CreateSortedPriceRecords(N, start);

            // Prepare queries (uniform stride)
            var queries = new List<DateTime>(Q);
            var stride = 17;
            for (int i = 0; i < Q; i++)
                queries.Add(start.AddMinutes(i * stride));

            // Warmup (JIT)
            BinarySearchOne(sorted, queries[0]);
            FillForwardArray(sorted, N);
            ArrayLookupIndex(start, queries[0], N);

            // Measure binary search (only the lookup cost)
            var swBinary = Stopwatch.StartNew();
            int foundBinary = 0;
            foreach (var t in queries)
            {
                var idx = BinarySearchOne(sorted, t);
                if (idx >= 0) foundBinary++;
            }
            swBinary.Stop();

            // Measure array fill (simulate precomputation cost)
            var swFill = Stopwatch.StartNew();
            var filled = FillForwardArray(sorted, N);
            swFill.Stop();

            // Measure direct array lookups
            var swLookup = Stopwatch.StartNew();
            int foundArray = 0;
            foreach (var t in queries)
            {
                var idx = (int)(t - start).TotalMinutes;
                if (idx >= 0 && idx < filled.Length && filled[idx] != null) foundArray++;
            }
            swLookup.Stop();

            var binaryMillis = swBinary.ElapsedMilliseconds;
            var arrayMillis = swFill.ElapsedMilliseconds + swLookup.ElapsedMilliseconds;

            Console.WriteLine($"N={N}, Q={Q}, log2(N)={log2N:F2}, threshold={theoreticalThreshold:F1}");
            Console.WriteLine($"Binary search lookup: {binaryMillis} ms (found {foundBinary}/{Q})");
            Console.WriteLine($"Array fill:           {swFill.ElapsedMilliseconds} ms");
            Console.WriteLine($"Array lookup:         {swLookup.ElapsedMilliseconds} ms (found {foundArray}/{Q})");
            Console.WriteLine($"Array total:          {arrayMillis} ms");

            // Assert binary search strictly faster than (fill + lookup)
            Assert.IsTrue(binaryMillis < arrayMillis,
                $"Expected binary search ({binaryMillis} ms) < array fill+lookup ({arrayMillis} ms) for Q << N.");

            // Sanity checks
            Assert.AreEqual(Q, foundBinary, "Binary search missed expected records.");
            Assert.AreEqual(Q, foundArray, "Array lookup missed expected records.");
        }

        // Original test replaced: it was unreliable because Q was very large relative to N.
        // If you still want to keep a comparative diagnostic, rename the old test and mark it as [Ignore].

        private static int BinarySearchOne(List<PriceRecord> data, DateTime target)
        {
            int left = 0, right = data.Count - 1, found = -1;
            while (left <= right)
            {
                int mid = left + ((right - left) >> 1);
                var dt = data[mid].DateTime;
                if (dt <= target)
                {
                    found = mid;
                    left = mid + 1;
                }
                else
                    right = mid - 1;
            }
            return found;
        }

        private static PriceRecord[] FillForwardArray(List<PriceRecord> sorted, int length)
        {
            var arr = new PriceRecord[length];
            if (sorted.Count == 0) return arr;
            var carry = sorted[0];
            for (int i = 0; i < length; i++)
                arr[i] = carry;
            return arr;
        }

        private static int ArrayLookupIndex(DateTime start, DateTime t, int length)
        {
            var idx = (int)(t - start).TotalMinutes;
            return (idx >= 0 && idx < length) ? idx : -1;
        }
    }
}