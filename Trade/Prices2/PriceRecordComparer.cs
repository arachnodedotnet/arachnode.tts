using System.Collections.Generic;

namespace Trade.Prices2
{
    /// <summary>
    ///     Provides comparison functionality for PriceRecord objects based on their DateTime values.
    ///     Used for sorting collections of price records in chronological order throughout the trading system.
    ///     Implements IComparer&lt;PriceRecord&gt; to enable use with sorting algorithms and ordered collections.
    ///     OPTIMIZED: Enhanced null handling, static instance pattern, and improved performance characteristics.
    /// </summary>
    public class PriceRecordComparer : IComparer<PriceRecord>
    {
        #region Static Instance Pattern for Performance

        /// <summary>
        ///     Shared static instance to avoid repeated object allocation in sorting operations.
        ///     Thread-safe since the comparer is stateless.
        /// </summary>
        public static readonly PriceRecordComparer Instance = new PriceRecordComparer();

        #endregion

        #region Constructors

        /// <summary>
        ///     Default constructor - public to maintain backward compatibility.
        ///     For optimal performance, consider using the static Instance property.
        /// </summary>
        public PriceRecordComparer()
        {
        }

        #endregion

        #region IComparer<PriceRecord> Implementation

        /// <summary>
        ///     Compares two PriceRecord objects based on their DateTime property.
        ///     Null values are handled gracefully, with null considered less than non-null values.
        ///     OPTIMIZED: Streamlined null checking and reduced method call overhead.
        /// </summary>
        /// <param name="firstPriceRecord">The first PriceRecord to compare</param>
        /// <param name="secondPriceRecord">The second PriceRecord to compare</param>
        /// <returns>
        ///     A signed integer that indicates the relative values of firstPriceRecord and secondPriceRecord:
        ///     - Less than zero: firstPriceRecord precedes secondPriceRecord in chronological order
        ///     - Zero: firstPriceRecord and secondPriceRecord have the same DateTime
        ///     - Greater than zero: firstPriceRecord follows secondPriceRecord in chronological order
        /// </returns>
        public int Compare(PriceRecord firstPriceRecord, PriceRecord secondPriceRecord)
        {
            // OPTIMIZATION: Streamlined null handling with early returns
            // Check both null at once to minimize branching
            if (ReferenceEquals(firstPriceRecord, secondPriceRecord))
                return 0; // Same reference or both null

            if (firstPriceRecord == null)
                return -1; // null is less than non-null

            if (secondPriceRecord == null)
                return 1; // non-null is greater than null

            // OPTIMIZATION: Direct DateTime comparison - already optimal
            // DateTime.CompareTo is highly optimized in .NET Framework
            return firstPriceRecord.DateTime.CompareTo(secondPriceRecord.DateTime);
        }

        #endregion

        #region High-Performance Comparison Methods

        /// <summary>
        ///     High-performance comparison method that assumes non-null inputs.
        ///     Use this method when you can guarantee that both PriceRecord instances are non-null
        ///     for maximum performance in tight loops.
        ///     OPTIMIZED: Eliminates null checking overhead for performance-critical scenarios.
        /// </summary>
        /// <param name="firstPriceRecord">The first PriceRecord to compare (must not be null)</param>
        /// <param name="secondPriceRecord">The second PriceRecord to compare (must not be null)</param>
        /// <returns>
        ///     A signed integer that indicates the relative values of firstPriceRecord and secondPriceRecord:
        ///     - Less than zero: firstPriceRecord precedes secondPriceRecord in chronological order
        ///     - Zero: firstPriceRecord and secondPriceRecord have the same DateTime
        ///     - Greater than zero: firstPriceRecord follows secondPriceRecord in chronological order
        /// </returns>
        /// <remarks>
        ///     This method does not perform null checks for maximum performance.
        ///     Use only when you can guarantee non-null inputs, or use the standard Compare method.
        /// </remarks>
        public int CompareNonNull(PriceRecord firstPriceRecord, PriceRecord secondPriceRecord)
        {
            // OPTIMIZATION: Direct comparison without null checks
            // Assumes caller has verified non-null inputs for maximum performance
            return firstPriceRecord.DateTime.CompareTo(secondPriceRecord.DateTime);
        }

        /// <summary>
        ///     Performs a reference equality check followed by comparison if needed.
        ///     Optimized for cases where the same PriceRecord instances are frequently compared.
        ///     OPTIMIZED: Reference equality check can short-circuit expensive DateTime comparison.
        /// </summary>
        /// <param name="firstPriceRecord">The first PriceRecord to compare</param>
        /// <param name="secondPriceRecord">The second PriceRecord to compare</param>
        /// <returns>
        ///     A signed integer indicating the comparison result as per IComparer contract
        /// </returns>
        public int CompareWithReferenceCheck(PriceRecord firstPriceRecord, PriceRecord secondPriceRecord)
        {
            // OPTIMIZATION: Reference equality check first - fastest possible comparison
            if (ReferenceEquals(firstPriceRecord, secondPriceRecord))
                return 0;

            // Fall back to standard comparison
            return Compare(firstPriceRecord, secondPriceRecord);
        }

        #endregion

        #region Utility Methods for Common Scenarios

        /// <summary>
        ///     Determines if the first PriceRecord is chronologically before the second.
        ///     OPTIMIZED: Provides semantic clarity and potential for future optimizations.
        /// </summary>
        /// <param name="firstPriceRecord">The first PriceRecord</param>
        /// <param name="secondPriceRecord">The second PriceRecord</param>
        /// <returns>True if first precedes second chronologically, false otherwise</returns>
        public bool IsBefore(PriceRecord firstPriceRecord, PriceRecord secondPriceRecord)
        {
            return Compare(firstPriceRecord, secondPriceRecord) < 0;
        }

        /// <summary>
        ///     Determines if the first PriceRecord is chronologically after the second.
        ///     OPTIMIZED: Provides semantic clarity and potential for future optimizations.
        /// </summary>
        /// <param name="firstPriceRecord">The first PriceRecord</param>
        /// <param name="secondPriceRecord">The second PriceRecord</param>
        /// <returns>True if first follows second chronologically, false otherwise</returns>
        public bool IsAfter(PriceRecord firstPriceRecord, PriceRecord secondPriceRecord)
        {
            return Compare(firstPriceRecord, secondPriceRecord) > 0;
        }

        /// <summary>
        ///     Determines if two PriceRecord instances have the same DateTime.
        ///     OPTIMIZED: Provides semantic clarity and handles null cases gracefully.
        /// </summary>
        /// <param name="firstPriceRecord">The first PriceRecord</param>
        /// <param name="secondPriceRecord">The second PriceRecord</param>
        /// <returns>True if both records have the same DateTime, false otherwise</returns>
        public bool IsSameTime(PriceRecord firstPriceRecord, PriceRecord secondPriceRecord)
        {
            return Compare(firstPriceRecord, secondPriceRecord) == 0;
        }

        #endregion

        #region Static Factory Methods

        /// <summary>
        ///     Creates a comparison delegate suitable for LINQ operations.
        ///     OPTIMIZED: Pre-compiled delegate for LINQ sorting operations.
        /// </summary>
        /// <returns>A Comparison delegate that can be used with LINQ or Array.Sort</returns>
        public static System.Comparison<PriceRecord> AsComparison()
        {
            return Instance.Compare;
        }

        /// <summary>
        ///     Creates a KeySelector function for LINQ OrderBy operations.
        ///     OPTIMIZED: Direct DateTime access for LINQ operations that can use key-based sorting.
        /// </summary>
        /// <returns>A function that extracts DateTime from PriceRecord for sorting</returns>
        public static System.Func<PriceRecord, System.DateTime> AsKeySelector()
        {
            return record => record?.DateTime ?? System.DateTime.MinValue;
        }

        #endregion
    }
}