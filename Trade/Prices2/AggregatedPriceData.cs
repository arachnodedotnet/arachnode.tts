using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Trade.Prices2
{
    /// <summary>
    ///     High-performance aggregated price data container with thread-safe operations and intelligent caching.
    ///     Supports multiple time frames, provides O(1) access patterns, and includes optimization for sequential data
    ///     processing.
    ///     Features automatic gap bridging for market hours and comprehensive price record management.
    /// </summary>
    public class AggregatedPriceData
    {
        // Core data storage structures
        private readonly ConcurrentDictionary<long, PriceRecord> _pricesByTimestamp;
        private readonly bool _sort = false;
        private readonly object _sortedListLock = new object();
        private readonly List<PriceRecord> _sortedPrices;
        private readonly bool _isOption; // track if this aggregated data is for options

        // Cached arrays for O(1) access - updated lazily
        private volatile double[] _cachedCloses;
        private volatile double[] _cachedHighs;
        private volatile double[] _cachedLows;
        private volatile double[] _cachedOpens;
        private volatile bool _cacheValid;
        private volatile int _lastUpdatedIndex = -1;

        // ✅ OPTIMIZATION: Cache last accessed record for sequential updates
        private volatile PriceRecord _lastUpdatedRecord;

        /// <summary>
        ///     Initializes a new instance of AggregatedPriceData for the specified time frame
        /// </summary>
        /// <param name="timeFrame">The time frame for price aggregation</param>
        /// <param name="isOption">Whether this data is for option contracts</param>
        public AggregatedPriceData(TimeFrame timeFrame, bool isOption)
        {
            TimeFrame = timeFrame;
            _isOption = isOption;
            _pricesByTimestamp = new ConcurrentDictionary<long, PriceRecord>();
            _sortedPrices = new List<PriceRecord>();
        }

        /// <summary>
        ///     Gets the time frame for this aggregated price data
        /// </summary>
        public TimeFrame TimeFrame { get; }

        /// <summary>
        ///     Gets the total number of price records
        /// </summary>
        public int Count => _sortedPrices.Count;

        /// <summary>
        ///     Get price by index - O(1) access
        /// </summary>
        /// <param name="index">Zero-based index of the price record</param>
        /// <returns>PriceRecord at the specified index</returns>
        public PriceRecord this[int index] => _sortedPrices[index];

        /// <summary>
        ///     Get prices ordered by timestamp (most recent first) - Property access
        /// </summary>
        public IEnumerable<PriceRecord> PricesByTimestampDescending =>
            _pricesByTimestamp.Values.OrderByDescending(p => p.DateTime);

        /// <summary>
        ///     Get prices ordered by timestamp (oldest first) - Property access
        /// </summary>
        public IEnumerable<PriceRecord> PricesByTimestampAscending =>
            _pricesByTimestamp.Values.OrderBy(p => p.DateTime);

        /// <summary>
        ///     Sorts the internal price records by timestamp
        /// </summary>
        public void Sort()
        {
            _sortedPrices.Sort(new PriceRecordComparer());
        }

        /// <summary>
        ///     Get price by timestamp - O(1) access
        /// </summary>
        /// <param name="timestamp">The timestamp to look up</param>
        /// <returns>PriceRecord for the timestamp or null if not found</returns>
        public PriceRecord GetByTimestamp(DateTime timestamp)
        {
            var normalizedTimestamp = GetNormalizedTimestamp(timestamp, TimeFrame);
            return _pricesByTimestamp.TryGetValue(normalizedTimestamp, out var record) ? record : null;
        }

        /// <summary>
        ///     Get price by timestamp with special handling for options (includes expiration checking)
        /// </summary>
        /// <param name="timestamp">The timestamp to look up</param>
        /// <returns>PriceRecord for the timestamp or null if not found or expired</returns>
        public PriceRecord GetByTimestampForOptions(DateTime timestamp)
        {
            var normalizedTimestamp = GetNormalizedTimestamp(timestamp, TimeFrame);
            var record = _pricesByTimestamp.TryGetValue(normalizedTimestamp, out var foundRecord) ? foundRecord : null;

            if (record == null)
            {
                var index = FindLastIndexOnOrBefore(timestamp);
                if (index == -1)
                    return null;

                record = _sortedPrices[index];

                var expiration = record.Option?.ExpirationDate;
                if (!expiration.HasValue || expiration.Value < timestamp) return null;
            }

            return record;
        }

        /// <summary>
        ///     Get all complete prices (excludes incomplete bars)
        /// </summary>
        /// <returns>Enumerable of complete price records</returns>
        public IEnumerable<PriceRecord> GetCompletePrices()
        {
            return _sortedPrices.Where(p => p.IsComplete);
        }

        /// <summary>
        ///     Get the latest price record
        /// </summary>
        /// <returns>Most recent price record or null if no records exist</returns>
        public PriceRecord GetLatest()
        {
            return _sortedPrices.Count > 0 ? _sortedPrices[_sortedPrices.Count - 1] : null;
        }

        /// <summary>
        ///     Get all prices sorted by timestamp with most recent first
        /// </summary>
        /// <returns>Enumerable of price records in descending chronological order</returns>
        public IEnumerable<PriceRecord> GetPricesByTimestampDescending()
        {
            return _pricesByTimestamp.Values.OrderByDescending(p => p.DateTime);
        }

        /// <summary>
        ///     Get all prices sorted by timestamp with oldest first (existing behavior)
        /// </summary>
        /// <returns>Enumerable of price records in ascending chronological order</returns>
        public IEnumerable<PriceRecord> GetPricesByTimestampAscending()
        {
            return _pricesByTimestamp.Values.OrderBy(p => p.DateTime);
        }

        /// <summary>
        ///     Get the most recent N prices (most recent first)
        /// </summary>
        /// <param name="count">Number of recent prices to retrieve</param>
        /// <returns>Enumerable of the most recent price records</returns>
        public IEnumerable<PriceRecord> GetRecentPrices(int count)
        {
            return _pricesByTimestamp.Values
                .OrderByDescending(p => p.DateTime)
                .Take(count);
        }

        /// <summary>
        ///     Get price array for indicators - O(1) amortized with caching
        /// </summary>
        /// <returns>Array of closing prices</returns>
        public double[] GetCloseArray()
        {
            if (!_cacheValid || _cachedCloses == null)
                RefreshCache();
            return _cachedCloses;
        }

        /// <summary>
        ///     Get array of opening prices with caching optimization
        /// </summary>
        /// <returns>Array of opening prices</returns>
        public double[] GetOpenArray()
        {
            if (!_cacheValid || _cachedOpens == null)
                RefreshCache();
            return _cachedOpens;
        }

        /// <summary>
        ///     Get array of high prices with caching optimization
        /// </summary>
        /// <returns>Array of high prices</returns>
        public double[] GetHighArray()
        {
            if (!_cacheValid || _cachedHighs == null)
                RefreshCache();
            return _cachedHighs;
        }

        /// <summary>
        ///     Get array of low prices with caching optimization
        /// </summary>
        /// <returns>Array of low prices</returns>
        public double[] GetLowArray()
        {
            if (!_cacheValid || _cachedLows == null)
                RefreshCache();
            return _cachedLows;
        }

        /// <summary>
        ///     Get price records within a date range
        ///     CRITICAL: End date is EXCLUSIVE - most recent record will NEVER be >= end date
        ///     Jan 3rd - Jan 7th returns Jan 3rd - Jan 6th (end date exclusive for backtesting integrity)
        /// </summary>
        /// <param name="start">Start date (inclusive)</param>
        /// <param name="end">End date (exclusive)</param>
        /// <param name="period">Optional specific number of periods to return</param>
        /// <param name="allowIncomplete">If true and there is not enough history to satisfy <paramref name=\"period\"/>, return the shorter available slice instead of empty (default false)</param>
        /// <returns>Enumerable of price records within the specified range</returns>
        public IEnumerable<PriceRecord> GetRange(DateTime start, DateTime end, int? period = 0,
            bool allowIncomplete = false, bool allowManfactured = true)
        {
            // Only allow incomplete buffers for options; force to false otherwise
            if (allowIncomplete && !_isOption)
                allowIncomplete = false;

            var startIndex = FindFirstIndexOnOrAfter(start);
            var endIndex = FindLastIndexBefore(end); // CRITICAL: Changed to BEFORE (exclusive end)

            if (period.GetValueOrDefault(0) != 0)
                if (endIndex - startIndex + 1 != period.GetValueOrDefault(0))
                    startIndex = endIndex - period.Value + 1;


            if (startIndex < 0 && allowIncomplete)
                startIndex = 0;

            if (startIndex >= 0 && endIndex >= 0 && startIndex <= endIndex)
                for (var i = startIndex; i <= endIndex; i++)
                {
                    if (!allowManfactured && _sortedPrices[i].Manufactured)
                        continue;
                    yield return _sortedPrices[i];
                }
        }

        /// <summary>
        ///     Add or update a price record - thread-safe with smart caching for sequential updates
        /// </summary>
        /// <param name="record">Price record to add or update</param>
        internal void AddOrUpdate(PriceRecord record)
        {
            var normalizedTimestamp = GetNormalizedTimestamp(record.DateTime, TimeFrame);

            if (_pricesByTimestamp.TryGetValue(normalizedTimestamp, out var existing))
            {
                // ✅ SUPER OPTIMIZED: Sequential data pattern optimization
                // When processing M5 data sequentially, we often update the same normalized record 4 times
                // (e.g., 9:30, 9:31, 9:32, 9:33, 9:34 all map to 9:30 M5 bar)
                lock (_sortedListLock)
                {
                    var index = -1;

                    // Check if this is the same record we just updated (very common in sequential processing)
                    if (ReferenceEquals(existing, _lastUpdatedRecord) &&
                        _lastUpdatedIndex >= 0 &&
                        _lastUpdatedIndex < _sortedPrices.Count &&
                        ReferenceEquals(_sortedPrices[_lastUpdatedIndex], existing))
                    {
                        // ✅ CACHE HIT: O(1) access - no search needed!
                        index = _lastUpdatedIndex;
                    }
                    else
                    {
                        // Cache miss: fallback to search and update cache
                        index = _sortedPrices.IndexOf(existing);
                        if (index >= 0)
                        {
                            _lastUpdatedRecord = existing;
                            _lastUpdatedIndex = index;
                        }
                    }

                    if (index >= 0)
                    {
                        _sortedPrices[index] = record;
                        _pricesByTimestamp[normalizedTimestamp] = record;

                        TryInsertBookendBridgeBar(record, index);

                        // Update cache to point to new record
                        _lastUpdatedRecord = record;
                        _lastUpdatedIndex = index;

                        _cacheValid = false; // Invalidate price arrays cache
                    }
                }
            }
            else
            {
                // Add new record
                _pricesByTimestamp[normalizedTimestamp] = record;

                lock (_sortedListLock)
                {
                    // Insert in sorted order (binary search for efficiency)
                    var insertIndex = _sortedPrices.BinarySearch(record, new PriceRecordComparer());
                    if (insertIndex < 0) insertIndex = ~insertIndex;
                    _sortedPrices.Insert(insertIndex, record);

                    // Update cache for potential future updates to this record
                    _lastUpdatedRecord = record;
                    _lastUpdatedIndex = insertIndex;

                    // Invalidate all cached indices after insertion point
                    if (_lastUpdatedIndex <= insertIndex)
                    {
                        _lastUpdatedRecord = null;
                        _lastUpdatedIndex = -1;
                    }

                    TryInsertBookendBridgeBar(record, insertIndex);

                    _cacheValid = false; // Invalidate price arrays cache
                }
            }
        }

        #region Market Hours Detection Methods

        /// <summary>
        ///     Determines if the given record represents the first trading bar of the day for the specified time frame
        /// </summary>
        /// <param name="record">Price record to check</param>
        /// <param name="timeFrame">Time frame context</param>
        /// <returns>True if this is the first bar of the trading day</returns>
        public static bool IsFirstBarOfDay(PriceRecord record, TimeFrame timeFrame)
        {
            if (record == null || !record.IsComplete) return false;

            // Market open times (EST)
            switch (timeFrame)
            {
                case TimeFrame.M1:
                case TimeFrame.M5:
                case TimeFrame.M10:
                case TimeFrame.M15:
                case TimeFrame.M30:
                    return record.DateTime.Hour == 9 && record.DateTime.Minute == 30;
                case TimeFrame.H1:
                case TimeFrame.H4:
                    return record.DateTime.Hour == 9 && record.DateTime.Minute == 0;
                case TimeFrame.D1:
                    // Daily bar: only one per day, so always first
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        ///     Determines if the given record represents the last trading bar of the day for the specified time frame
        /// </summary>
        /// <param name="record">Price record to check</param>
        /// <param name="timeFrame">Time frame context</param>
        /// <returns>True if this is the last bar of the trading day</returns>
        public static bool IsLastBarOfDay(PriceRecord record, TimeFrame timeFrame)
        {
            if (record == null || !record.IsComplete) return false;

            // Market close times (EST)
            switch (timeFrame)
            {
                case TimeFrame.M1:
                    return record.DateTime.Hour == 16 && record.DateTime.Minute == 14;
                case TimeFrame.M5:
                    return record.DateTime.Hour == 16 && record.DateTime.Minute == 10;
                case TimeFrame.M10:
                    return record.DateTime.Hour == 16 && record.DateTime.Minute == 10;
                case TimeFrame.M15:
                    return record.DateTime.Hour == 16 && record.DateTime.Minute == 0;
                case TimeFrame.M30:
                    return record.DateTime.Hour == 16 && record.DateTime.Minute == 0;
                case TimeFrame.H1:
                    return record.DateTime.Hour == 16 && record.DateTime.Minute == 0;
                case TimeFrame.H4:
                    return record.DateTime.Hour == 16 && record.DateTime.Minute == 0;
                case TimeFrame.D1:
                    // Daily bar: only one per day, so always last
                    return true;
                default:
                    return false;
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        ///     Refreshes the cached price arrays with latest data using parallel processing for large datasets
        /// </summary>
        private void RefreshCache()
        {
            lock (_sortedListLock)
            {
                if (_cacheValid) return; // Double-check pattern

                var count = _sortedPrices.Count;
                _cachedCloses = new double[count];
                _cachedOpens = new double[count];
                _cachedHighs = new double[count];
                _cachedLows = new double[count];

                // Parallel array population for large datasets
                if (count > 1000)
                    Parallel.For(0, count, i =>
                    {
                        var record = _sortedPrices[i];
                        _cachedCloses[i] = record.Close;
                        _cachedOpens[i] = record.Open;
                        _cachedHighs[i] = record.High;
                        _cachedLows[i] = record.Low;
                    });
                else
                    for (var i = 0; i < count; i++)
                    {
                        var record = _sortedPrices[i];
                        _cachedCloses[i] = record.Close;
                        _cachedOpens[i] = record.Open;
                        _cachedHighs[i] = record.High;
                        _cachedLows[i] = record.Low;
                    }

                _cacheValid = true;
            }
        }

        /// <summary>
        ///     Attempts to insert bridge bars between trading sessions to fill gaps
        /// </summary>
        /// <param name="record">Current price record</param>
        /// <param name="insertIndex">Index where the record was inserted</param>
        private void TryInsertBookendBridgeBar(PriceRecord record, int insertIndex)
        {
            var period = (int)TimeFrame;

            // Check for bridge before (previous day's last bar to today's first bar)
            if (IsFirstBarOfDay(record, TimeFrame) && insertIndex > 0)
            {
                var previousRecord = _sortedPrices[insertIndex - 1];
                if (record != previousRecord && IsLastBarOfDay(previousRecord, TimeFrame))
                {
                    var bridgeTime = previousRecord.DateTime.Date.AddHours(16).AddMinutes(15);
                    // Avoid duplicate bridge bars
                    if (!_pricesByTimestamp.ContainsKey(GetNormalizedTimestamp(bridgeTime, TimeFrame.M1)))
                    {
                        var bridgeBar = new PriceRecord(
                            bridgeTime, TimeFrame.BridgeBar,
                            previousRecord.Close,
                            Math.Max(previousRecord.Close, record.Open),
                            Math.Min(previousRecord.Close, record.Open),
                            record.Open,
                            volume: 1,
                            wap: (previousRecord.Close + record.Open) / 2,
                            count: 1,
                            option: null,
                            isComplete: true
                        )
                        {
                            Manufactured = true,
                            Debug = $"GapBridge:{previousRecord.Close:F2}->{record.Open:F2}"
                        };
                        _pricesByTimestamp[GetNormalizedTimestamp(bridgeTime, TimeFrame.M1)] = bridgeBar;
                        _sortedPrices.Insert(insertIndex, bridgeBar);
                        //Console.WriteLine($"Bridge bar inserted between {previousRecord.DateTime:yyyy-MM-dd HH:mm} and {record.DateTime:yyyy-MM-dd HH:mm}");
                    }
                }
            }

            // Check for bridge after (today's last bar to next day's first bar)
            if (IsLastBarOfDay(record, TimeFrame) && insertIndex + 1 < _sortedPrices.Count)
            {
                var nextRecord = _sortedPrices[insertIndex + 1];
                if (record != nextRecord && IsFirstBarOfDay(nextRecord, TimeFrame))
                {
                    var bridgeTime = record.DateTime.Date.AddHours(16).AddMinutes(15);
                    // Avoid duplicate bridge bars
                    if (!_pricesByTimestamp.ContainsKey(GetNormalizedTimestamp(bridgeTime, TimeFrame.M1)))
                    {
                        var bridgeBar = new PriceRecord(
                            bridgeTime, TimeFrame.BridgeBar,
                            record.Close,
                            Math.Max(record.Close, nextRecord.Open),
                            Math.Min(record.Close, nextRecord.Open),
                            nextRecord.Open,
                            volume: 1,
                            wap: (record.Close + nextRecord.Open) / 2,
                            count: 1,
                            option: null,
                            isComplete: true
                        )
                        {
                            Manufactured = true,
                            Debug = $"GapBridge:{record.Close:F2}->{nextRecord.Open:F2}"
                        };
                        _pricesByTimestamp[GetNormalizedTimestamp(bridgeTime, TimeFrame.M1)] = bridgeBar;
                        _sortedPrices.Insert(insertIndex + 1, bridgeBar);
                        //Console.WriteLine($"Bridge bar inserted between {record.DateTime:yyyy-MM-dd HH:mm} and {nextRecord.DateTime:yyyy-MM-dd HH:mm}");
                    }
                }
            }
        }

        /// <summary>
        ///     Find the first index on or after the specified timestamp using binary search optimization
        /// </summary>
        /// <param name="timestamp">Target timestamp</param>
        /// <returns>Index of first record on or after timestamp, or -1 if not found</returns>
        private int FindFirstIndexOnOrAfter(DateTime timestamp)
        {
            // Binary search optimization for large datasets
            if (_sortedPrices.Count > 1000)
            {
                int left = 0, right = _sortedPrices.Count - 1;
                while (left <= right)
                {
                    var mid = left + (right - left) / 2;
                    if (_sortedPrices[mid].DateTime >= timestamp)
                    {
                        if (mid == 0 || _sortedPrices[mid - 1].DateTime < timestamp)
                            return mid;
                        right = mid - 1;
                    }
                    else
                    {
                        left = mid + 1;
                    }
                }

                return left < _sortedPrices.Count ? left : -1;
            }

            // Linear search for smaller datasets
            for (var i = 0; i < _sortedPrices.Count; i++)
                if (_sortedPrices[i].DateTime >= timestamp)
                    return i;
            return -1;
        }

        /// <summary>
        ///     Find the last index before the specified timestamp (exclusive end for backtesting)
        /// </summary>
        /// <param name="timestamp">Target timestamp</param>
        /// <returns>Index of last record before timestamp, or -1 if not found</returns>
        private int FindLastIndexBefore(DateTime timestamp)
        {
            // CRITICAL: Find last index BEFORE timestamp (exclusive end for backtesting)
            // Binary search optimization for large datasets
            if (_sortedPrices.Count > 1000)
            {
                int left = 0, right = _sortedPrices.Count - 1;
                while (left <= right)
                {
                    var mid = left + (right - left) / 2;
                    if (_sortedPrices[mid].DateTime < timestamp) // CRITICAL: < not <=
                    {
                        if (mid == _sortedPrices.Count - 1 || _sortedPrices[mid + 1].DateTime >= timestamp)
                            return mid;
                        left = mid + 1;
                    }
                    else
                    {
                        right = mid - 1;
                    }
                }

                return right >= 0 ? right : -1;
            }

            // Linear search for smaller datasets
            for (var i = _sortedPrices.Count - 1; i >= 0; i--)
                if (_sortedPrices[i].DateTime < timestamp) // CRITICAL: < not <=
                    return i;
            return -1;
        }

        /// <summary>
        ///     Find the last index on or before the specified timestamp (inclusive search)
        /// </summary>
        /// <param name="timestamp">Target timestamp</param>
        /// <returns>Index of last record on or before timestamp, or -1 if not found</returns>
        private int FindLastIndexOnOrBefore(DateTime timestamp)
        {
            // Keep this method for internal use where inclusive end is needed
            // Binary search optimization for large datasets
            if (_sortedPrices.Count > 1000)
            {
                int left = 0, right = _sortedPrices.Count - 1;
                while (left <= right)
                {
                    var mid = left + (right - left) / 2;
                    if (_sortedPrices[mid].DateTime <= timestamp)
                    {
                        if (mid == _sortedPrices.Count - 1 || _sortedPrices[mid + 1].DateTime > timestamp)
                            return mid;
                        left = mid + 1;
                    }
                    else
                    {
                        right = mid - 1;
                    }
                }

                return right >= 0 ? right : -1;
            }

            // Linear search for smaller datasets
            for (var i = _sortedPrices.Count - 1; i >= 0; i--)
                if (_sortedPrices[i].DateTime <= timestamp)
                    return i;
            return -1;
        }

        /// <summary>
        ///     Normalizes a timestamp to align with the specified time frame boundaries
        /// </summary>
        /// <param name="timestamp">Original timestamp</param>
        /// <param name="timeFrame">Time frame for normalization</param>
        /// <returns>Normalized timestamp as ticks</returns>
        internal static long GetNormalizedTimestamp(DateTime timestamp, TimeFrame timeFrame)
        {
            var minutes = (int)timeFrame;
            var totalMinutes = timestamp.Hour * 60 + timestamp.Minute;
            var normalizedMinutes = totalMinutes / minutes * minutes;

            var normalizedTime = timestamp.Date.AddMinutes(normalizedMinutes);
            return normalizedTime.Ticks;
        }

        #endregion
    }
}