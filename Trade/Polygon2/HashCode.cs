namespace Trade.Polygon2
{
    /// <summary>
    ///     HashCode helper for .NET Framework 4.7.2 compatibility
    ///     Provides hash code combination functionality similar to System.HashCode in newer .NET versions
    /// </summary>
    internal static class HashCode
    {
        /// <summary>
        ///     Combines the hash codes of three values into a single hash code
        /// </summary>
        /// <typeparam name="T1">Type of the first value</typeparam>
        /// <typeparam name="T2">Type of the second value</typeparam>
        /// <typeparam name="T3">Type of the third value</typeparam>
        /// <param name="value1">First value to include in hash calculation</param>
        /// <param name="value2">Second value to include in hash calculation</param>
        /// <param name="value3">Third value to include in hash calculation</param>
        /// <returns>Combined hash code of the three input values</returns>
        public static int Combine<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
        {
            unchecked
            {
                var hash = 17; // Prime number starting point for hash calculation
                hash = hash * 23 + (value1?.GetHashCode() ?? 0); // Multiply by prime and add first hash
                hash = hash * 23 + (value2?.GetHashCode() ?? 0); // Multiply by prime and add second hash
                hash = hash * 23 + (value3?.GetHashCode() ?? 0); // Multiply by prime and add third hash
                return hash;
            }
        }
    }
}