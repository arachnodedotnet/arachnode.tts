using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Trade.Prices2
{
    public class Prices
    {
        private static PriceRecord[] _dailyPriceRecords;
        private readonly ConcurrentDictionary<TimeFrame, AggregatedPriceData> _aggregatedData;
        private readonly object _lockObject = new object();

        public Prices(string filePath = Constants.SPX_JSON, bool isOption = false)
        {
            Records = new List<PriceRecord>();
            _aggregatedData = new ConcurrentDictionary<TimeFrame, AggregatedPriceData>();

            // Initialize all timeframes
            foreach (TimeFrame tf in Enum.GetValues(typeof(TimeFrame)))
                if (tf != TimeFrame.BridgeBar)
                    _aggregatedData[tf] = new AggregatedPriceData(tf, isOption);

            if (!string.IsNullOrEmpty(filePath)) Load(filePath);
        }

        /// <summary>
        ///     Constructor that allows initializing without loading a file
        /// </summary>
        public Prices() : this(null)
        {
        }

        // Core properties
        public List<PriceRecord> Records { get; }

        public DateTime? FirstTimestamp => Records.Count > 0 ? Records[0].DateTime : (DateTime?)null;
        public DateTime? LastTimestamp => Records.Count > 0 ? Records[Records.Count - 1].DateTime : (DateTime?)null;

        /// <summary>
        ///     Last load result for diagnostics
        /// </summary>
        public LoadResult LastLoadResult { get; private set; }

        /// <summary>
        ///     Get aggregated data for a specific timeframe with O(1) access
        /// </summary>
        public AggregatedPriceData GetTimeFrame(TimeFrame timeFrame)
        {
            return _aggregatedData[timeFrame];
        }

        /// <summary>
        ///     Enhanced method to add a new price record with validation
        /// </summary>
        public void AddPrice(PriceRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record), "Price record cannot be null");

            // Validate the record before adding
            var errors = new ConcurrentBag<string>();
            var warnings = new ConcurrentBag<string>();

            if (!ValidatePriceRecordStatic(record, -1, errors, warnings))
                throw new ArgumentException($"Invalid price record: {string.Join("; ", errors)}", nameof(record));

            // Log warnings if any
            if (warnings.Any()) ConsoleUtilities.WriteLine($"Price record warnings: {string.Join("; ", warnings)}");

            lock (_lockObject)
            {
                // Add to base records
                var insertIndex = Records.BinarySearch(record, new PriceRecordComparer());
                if (insertIndex < 0) insertIndex = ~insertIndex;
                Records.Insert(insertIndex, record);

                // Update all timeframe aggregations
                UpdateAggregations(record);
            }
        }

        /// <summary>
        ///     Enhanced method to add multiple price records with validation
        /// </summary>
        public void AddPricesBatch(IEnumerable<PriceRecord> records)
        {
            var stopwatch = Stopwatch.StartNew();

            if (records == null)
                throw new ArgumentNullException(nameof(records), "Records collection cannot be null");

            var recordList = records.ToList();
            if (recordList.Count == 0) return;

            // Validate all records first
            var validRecords = new List<PriceRecord>();
            var validationErrors = new ConcurrentBag<string>();
            var validationWarnings = new ConcurrentBag<string>();

            for (var i = 0; i < recordList.Count; i++)
            {
                var record = recordList[i];
                if (record == null)
                {
                    validationErrors.Add($"Record {i + 1}: Null record in batch");
                    continue;
                }

                var errors = new ConcurrentBag<string>();
                var warnings = new ConcurrentBag<string>();

                if (ValidatePriceRecordStatic(record, i + 1, errors, warnings))
                {
                    validRecords.Add(record);
                    foreach (var validationWarning in warnings) validationWarnings.Add(validationWarning);
                }
                else
                {
                    foreach (var validationError in errors) validationErrors.Add(validationError);
                }
            }

            // Log validation results
            if (validationErrors.Any() || validationWarnings.Any())
            {
                ConsoleUtilities.WriteLine($"Batch validation: {validRecords.Count}/{recordList.Count} records valid");

                if (validationErrors.Any())
                {
                    ConsoleUtilities.WriteLine($"Batch errors: {string.Join("; ", validationErrors.Take(10))}");
                    if (validationErrors.Count > 10)
                        ConsoleUtilities.WriteLine($"... and {validationErrors.Count - 10} more errors");
                }

                if (validationWarnings.Any())
                {
                    ConsoleUtilities.WriteLine($"Batch warnings: {string.Join("; ", validationWarnings.Take(25))}");
                    if (validationWarnings.Count > 25)
                        ConsoleUtilities.WriteLine($"... and {validationWarnings.Count - 25} more warnings");
                }
            }

            // Throw if no valid records
            if (validRecords.Count == 0)
                throw new ArgumentException("No valid price records in batch", nameof(records));

            lock (_lockObject)
            {
                // Add all valid records to base collection first
                Records.AddRange(validRecords);
                Records.Sort(new PriceRecordComparer());

                // Parallel aggregation update for large batches
                if (validRecords.Count > 100)
                    BuildAllAggregationsParallel();
                else
                    // Sequential for small batches to avoid overhead
                    foreach (var record in validRecords)
                        UpdateAggregations(record);
            }

            stopwatch.Stop();
            //ConsoleUtilities.WriteLine(
            //    $"AddPricesBatch completed in {stopwatch.ElapsedMilliseconds:N0} ms.");
        }

        /// <summary>
        ///     Enhanced method to update current price with validation
        /// </summary>
        public void UpdateCurrentPrice(DateTime timestamp, TimeFrame timeFrame, double open, double high, double low, double close,
            double volume = 0, bool isComplete = false)
        {
            // Validate input parameters
            if (open <= 0) throw new ArgumentException("Open price must be positive", nameof(open));
            if (high <= 0) throw new ArgumentException("High price must be positive", nameof(high));
            if (low <= 0) throw new ArgumentException("Low price must be positive", nameof(low));
            if (close <= 0) throw new ArgumentException("Close price must be positive", nameof(close));
            if (volume < 0) throw new ArgumentException("Volume cannot be negative", nameof(volume));
            if (high < low) throw new ArgumentException("High cannot be less than Low", nameof(high));

            // Validate OHLC relationships
            if (open < low || open > high)
                ConsoleUtilities.WriteLine($"Warning: Open ({open:F2}) outside High-Low range [{low:F2}-{high:F2}]");

            if (close < low || close > high)
                ConsoleUtilities.WriteLine($"Warning: Close ({close:F2}) outside High-Low range [{low:F2}-{high:F2}]");

            var record = new PriceRecord(timestamp, timeFrame, open, high, low, close, volume: volume, wap: 0, count: 0, option: null, isComplete: isComplete);

            lock (_lockObject)
            {
                // Update or add the current minute bar
                var existingIndex = Records.FindLastIndex(r => r.DateTime.Date == timestamp.Date &&
                                                               r.DateTime.Hour == timestamp.Hour &&
                                                               r.DateTime.Minute == timestamp.Minute);

                if (existingIndex >= 0)
                {
                    Records[existingIndex] = record;
                }
                else
                {
                    AddPrice(record);
                    return; // AddPrice already calls UpdateAggregations
                }

                // Update all aggregations
                UpdateAggregations(record);
            }
        }

        public void Sort()
        {
            lock (_lockObject)
            {
                Records.Sort(new PriceRecordComparer());
                foreach (var aggregate in _aggregatedData) aggregate.Value.Sort();
            }
        }

        /// <summary>
        ///     Get price data optimized for indicator calculations - with caching
        /// </summary>
        public double[] GetCloses(TimeFrame timeFrame = TimeFrame.M1)
        {
            return _aggregatedData[timeFrame].GetCloseArray();
        }

        public double[] GetOpens(TimeFrame timeFrame = TimeFrame.M1)
        {
            return _aggregatedData[timeFrame].GetOpenArray();
        }

        public double[] GetHighs(TimeFrame timeFrame = TimeFrame.M1)
        {
            return _aggregatedData[timeFrame].GetHighArray();
        }

        public double[] GetLows(TimeFrame timeFrame = TimeFrame.M1)
        {
            return _aggregatedData[timeFrame].GetLowArray();
        }

        /// <summary>
        ///     Get complete bars only (excludes current incomplete bar)
        /// </summary>
        public IEnumerable<PriceRecord> GetCompleteBars(TimeFrame timeFrame = TimeFrame.M1)
        {
            return _aggregatedData[timeFrame].GetCompletePrices();
        }

        /// <summary>
        ///     Get price at specific timestamp
        /// </summary>
        public PriceRecord GetPriceAt(DateTime timestamp, TimeFrame timeFrame = TimeFrame.M1)
        {
            return _aggregatedData[timeFrame].GetByTimestamp(timestamp);
        }

        public PriceRecord GetPriceAtForOptions(DateTime timestamp, TimeFrame timeFrame = TimeFrame.M1)
        {
            return _aggregatedData[timeFrame].GetByTimestampForOptions(timestamp);
        }

        /// <summary>
        ///     Get price range for backtesting
        ///     CRITICAL: End date is EXCLUSIVE - most recent record will NEVER be >= end date
        ///     Jan 3rd - Jan 7th returns Jan 3rd - Jan 6th (end date exclusive for backtesting integrity)
        /// </summary>
        public IEnumerable<PriceRecord> GetRange(DateTime start, DateTime end, TimeFrame timeFrame = TimeFrame.M1,
            int? period = 0, bool allowIncomplete = false, bool allowManufactured = true)
        {
            return _aggregatedData[timeFrame].GetRange(start, end, period, allowIncomplete, allowManufactured);
        }

        /// <summary>
        ///     Get all prices for a timeframe sorted by timestamp with most recent first
        /// </summary>
        public IEnumerable<PriceRecord> GetPricesByTimestampDescending(TimeFrame timeFrame = TimeFrame.M1)
        {
            return _aggregatedData[timeFrame].GetPricesByTimestampDescending();
        }

        /// <summary>
        ///     Get all prices for a timeframe sorted by timestamp with oldest first
        /// </summary>
        public IEnumerable<PriceRecord> GetPricesByTimestampAscending(TimeFrame timeFrame = TimeFrame.M1)
        {
            return _aggregatedData[timeFrame].GetPricesByTimestampAscending();
        }

        /// <summary>
        ///     Get the most recent N prices for a timeframe (most recent first)
        /// </summary>
        public IEnumerable<PriceRecord> GetRecentPrices(int count, TimeFrame timeFrame = TimeFrame.M1)
        {
            return _aggregatedData[timeFrame].GetRecentPrices(count);
        }

        /// <summary>
        ///     Get the latest price for a specific timeframe
        /// </summary>
        public PriceRecord GetLatestPrice(TimeFrame timeFrame = TimeFrame.M1)
        {
            return _aggregatedData[timeFrame].GetLatest();
        }

        private void Load(string filePath)
        {
            var loadResult = new LoadResult();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // File validation
                var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
                if (!ValidateFile(fullPath, loadResult))
                {
                    LastLoadResult = loadResult;
                    return; // Don't throw, just return with validation errors
                }

                // Read and validate file content
                var lines = File.ReadAllLines(fullPath);
                loadResult.TotalLinesProcessed = lines.Length;

                if (lines.Length == 0)
                {
                    loadResult.ValidationWarnings.Add("File is empty");
                    loadResult.Success = true; // Empty file is technically valid
                    LastLoadResult = loadResult;
                    return;
                }

                var distinctLines = lines.Distinct().ToArray();
                if (lines.Length != distinctLines.Length)
                {
                    loadResult.ValidationWarnings.Add("File lines are not distinct");
                    loadResult.TotalLinesProcessed = distinctLines.Length;
                }

                lines = distinctLines;

                // Process records with validation
                if (lines.Length > 1000)
                    // Parallel processing for large files with validation
                    ProcessLargeFileWithValidation(lines, loadResult);
                else
                    // Sequential processing for small files with validation
                    ProcessSmallFileWithValidation(lines, loadResult);

                // Final validation and sorting
                if (Records.Count > 0)
                {
                    Records.Sort(new PriceRecordComparer());
                    ValidateDataIntegrity(loadResult);
                    BuildAllAggregationsParallel();

                    loadResult.FirstRecordDate = Records.First().DateTime;
                    loadResult.LastRecordDate = Records.Last().DateTime;
                }

                // Determine overall success
                loadResult.Success = loadResult.ValidationErrors.Count == 0 && loadResult.ValidRecordsLoaded > 0;

                stopwatch.Stop();
                loadResult.LoadTimeMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                LastLoadResult = loadResult;

                // Log summary if there were issues
                if (loadResult.ValidationErrors.Count > 0 || loadResult.ValidationWarnings.Count > 0)
                    LogLoadingSummary(loadResult);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                loadResult.LoadTimeMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                loadResult.Success = false;
                loadResult.ValidationErrors.Add($"Unexpected error during loading: {ex.Message}");
                LastLoadResult = loadResult;

                // If we have partial data, don't throw - let the caller decide
                if (Records.Count == 0)
                    throw new InvalidDataException($"Failed to load price data from '{filePath}': {ex.Message}", ex);
            }

            stopwatch.Stop();
            ConsoleUtilities.WriteLine(
                $"Load completed in {stopwatch.ElapsedMilliseconds:N0} ms.");
        }

        private bool ValidateFile(string fullPath, LoadResult loadResult)
        {
            // Check file existence
            if (!File.Exists(fullPath))
            {
                loadResult.ValidationErrors.Add($"Price file not found: {fullPath}");
                return false;
            }

            // Check file size
            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length == 0)
            {
                loadResult.ValidationWarnings.Add("File is empty");
                return true; // Empty file is technically valid
            }

            // Check for reasonable file size (warn if > 100MB)
            const long maxReasonableSize = 100 * 1024 * 1024; // 100MB
            if (fileInfo.Length > maxReasonableSize)
                loadResult.ValidationWarnings.Add(
                    $"Large file detected ({fileInfo.Length / (1024 * 1024):F1}MB) - loading may take time");

            // Check file extension
            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (extension != ".json" && extension != ".jsonl")
                loadResult.ValidationWarnings.Add(
                    $"Unexpected file extension '{extension}' - expected .json or .jsonl");

            // Check file accessibility
            try
            {
                using (var stream = File.OpenRead(fullPath))
                {
                    // Just verify we can open it
                }
            }
            catch (UnauthorizedAccessException)
            {
                loadResult.ValidationErrors.Add($"Access denied to file: {fullPath}");
                return false;
            }
            catch (IOException ex)
            {
                loadResult.ValidationErrors.Add($"I/O error accessing file: {ex.Message}");
                return false;
            }

            return true;
        }

        private void ProcessLargeFileWithValidation(string[] lines, LoadResult loadResult)
        {
            var records = new ConcurrentBag<PriceRecord>();
            var validationErrors = new ConcurrentBag<string>();
            var validationWarnings = new ConcurrentBag<string>();
            var processedCount = 0;

            Parallel.ForEach(lines, (line, loop, index) =>
            {
                Interlocked.Increment(ref processedCount);

                if (string.IsNullOrWhiteSpace(line))
                {
                    validationWarnings.Add($"Line {index + 1}: Empty line skipped");
                    return;
                }

                try
                {
                    var record = JsonConvert.DeserializeObject<PriceRecord>(line);
                    if (record != null)
                    {
                        record.TimeFrame = TimeFrame.M1;
                        if (ValidatePriceRecord(record, (int)index + 1, validationErrors, validationWarnings))
                        {
                            record.DateTime = ParseDateTimeWithTimezone(record.Time);
                            records.Add(record);
                        }
                    }
                    else
                    {
                        validationErrors.Add($"Line {index + 1}: Failed to deserialize JSON - null result");
                    }
                }
                catch (JsonException ex)
                {
                    validationErrors.Add($"Line {index + 1}: JSON parsing error - {ex.Message}");
                }
                catch (Exception ex)
                {
                    validationErrors.Add($"Line {index + 1}: Unexpected error - {ex.Message}");
                }
            });

            Records.AddRange(records);
            loadResult.ValidRecordsLoaded = records.Count;
            loadResult.SkippedRecords = processedCount - records.Count;
            foreach (var validationError in validationErrors) loadResult.ValidationErrors.Add(validationError);
            foreach (var validationWarning in validationWarnings) loadResult.ValidationWarnings.Add(validationWarning);
        }

        private void ProcessSmallFileWithValidation(string[] lines, LoadResult loadResult)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    loadResult.ValidationWarnings.Add($"Line {i + 1}: Empty line skipped");
                    loadResult.SkippedRecords++;
                    continue;
                }

                try
                {
                    var record = JsonConvert.DeserializeObject<PriceRecord>(line);
                    if (record != null)
                    {
                        if (ValidatePriceRecord(record, i + 1, loadResult.ValidationErrors,
                                loadResult.ValidationWarnings))
                        {
                            record.DateTime = ParseDateTimeWithTimezone(record.Time);
                            Records.Add(record);
                            loadResult.ValidRecordsLoaded++;
                        }
                        else
                        {
                            loadResult.SkippedRecords++;
                        }
                    }
                    else
                    {
                        loadResult.ValidationErrors.Add($"Line {i + 1}: Failed to deserialize JSON - null result");
                        loadResult.SkippedRecords++;
                    }
                }
                catch (JsonException ex)
                {
                    loadResult.ValidationErrors.Add($"Line {i + 1}: JSON parsing error - {ex.Message}");
                    loadResult.SkippedRecords++;
                }
                catch (Exception ex)
                {
                    loadResult.ValidationErrors.Add($"Line {i + 1}: Unexpected error - {ex.Message}");
                    loadResult.SkippedRecords++;
                }
            }
        }

        private bool ValidatePriceRecord(PriceRecord record, int lineNumber, ConcurrentBag<string> errors,
            ConcurrentBag<string> warnings)
        {
            var isValid = true;

            // Validate required fields
            if (string.IsNullOrWhiteSpace(record.Time))
            {
                errors.Add($"Line {lineNumber}: Missing or empty Time field");
                isValid = false;
            }

            // Validate OHLC data
            if (!ValidateOHLCPrices(record, lineNumber, errors, warnings)) isValid = false;

            // Validate volume and count
            if (record.Volume < 0)
            {
                warnings.Add($"Line {lineNumber}: Negative volume ({record.Volume}) - setting to 0");
                record.Volume = 0;
            }

            if (record.Count < 0)
            {
                warnings.Add($"Line {lineNumber}: Negative count ({record.Count}) - setting to 0");
                record.Count = 0;
            }

            // Validate WAP if provided
            if (record.WAP > 0 && (record.WAP < record.Low || record.WAP > record.High))
                warnings.Add(
                    $"Line {lineNumber}: WAP ({record.WAP:F2}) outside High-Low range [{record.Low:F2}-{record.High:F2}]");

            // Validate DateTime conversion
            try
            {
                var dateTime = ParseDateTimeWithTimezone(record.Time);
                if (dateTime == DateTime.MinValue)
                {
                    errors.Add($"Line {lineNumber}: Invalid time format '{record.Time}'");
                    isValid = false;
                }
                else if (dateTime.Year < 1900 || dateTime.Year > 2100)
                {
                    warnings.Add($"Line {lineNumber}: Unusual year ({dateTime.Year}) in timestamp");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Line {lineNumber}: Date/time parsing error for '{record.Time}': {ex.Message}");
                isValid = false;
            }

            return isValid;
        }

        private bool ValidateOHLCPrices(PriceRecord record, int lineNumber, ConcurrentBag<string> errors,
            ConcurrentBag<string> warnings)
        {
            var isValid = true;

            // Check for non-positive prices
            if (record.Open <= 0)
            {
                errors.Add($"Line {lineNumber}: Invalid Open price ({record.Open}) - must be positive");
                isValid = false;
            }

            if (record.High <= 0)
            {
                errors.Add($"Line {lineNumber}: Invalid High price ({record.High}) - must be positive");
                isValid = false;
            }

            if (record.Low <= 0)
            {
                errors.Add($"Line {lineNumber}: Invalid Low price ({record.Low}) - must be positive");
                isValid = false;
            }

            if (record.Close <= 0)
            {
                errors.Add($"Line {lineNumber}: Invalid Close price ({record.Close}) - must be positive");
                isValid = false;
            }

            if (!isValid) return false; // Don't continue validation if basic prices are invalid

            // Validate OHLC relationships
            if (record.High < record.Low)
            {
                errors.Add($"Line {lineNumber}: High ({record.High:F2}) cannot be less than Low ({record.Low:F2})");
                isValid = false;
            }

            if (record.Open < record.Low || record.Open > record.High)
                warnings.Add(
                    $"Line {lineNumber}: Open ({record.Open:F2}) outside High-Low range [{record.Low:F2}-{record.High:F2}]");

            if (record.Close < record.Low || record.Close > record.High)
                warnings.Add(
                    $"Line {lineNumber}: Close ({record.Close:F2}) outside High-Low range [{record.Low:F2}-{record.High:F2}]");

            // Check for extreme price movements (likely data errors)
            const double maxReasonablePriceRatio = 2.0; // 100% change in one period
            var priceRange = record.High - record.Low;
            var midPrice = (record.High + record.Low) / 2;

            if (midPrice > 0 && priceRange / midPrice > maxReasonablePriceRatio)
                warnings.Add(
                    $"Line {lineNumber}: Extreme price range detected - {priceRange / midPrice * 100:F1}% range in one period");

            // Check for unreasonably high prices (likely data format issues)
            const double maxReasonablePrice = 1000000; // $1M per share
            if (record.High > maxReasonablePrice)
                warnings.Add(
                    $"Line {lineNumber}: Extremely high price detected ({record.High:F2}) - possible data format issue");

            // Check for unreasonably low prices (likely data format issues)
            const double minReasonablePrice = 0.001; // $0.001 per share
            if (record.Low < minReasonablePrice)
                warnings.Add(
                    $"Line {lineNumber}: Extremely low price detected ({record.Low:F6}) - possible data format issue");

            return isValid;
        }

        private void ValidateDataIntegrity(LoadResult loadResult)
        {
            if (Records.Count == 0) return;

            // Check for duplicates
            var duplicates = Records
                .GroupBy(r => r.DateTime)
                .Where(g => g.Count() > 1)
                .ToList();

            if (duplicates.Any())
            {
                loadResult.ValidationWarnings.Add(
                    $"Found {duplicates.Count} duplicate timestamps - later records will overwrite earlier ones");

                foreach (var duplicate in duplicates.Take(5)) // Show first 5 duplicates
                    loadResult.ValidationWarnings.Add(
                        $"  Duplicate timestamp: {duplicate.Key:yyyy-MM-dd HH:mm:ss} ({duplicate.Count()} records)");

                if (duplicates.Count > 5)
                    loadResult.ValidationWarnings.Add($"  ... and {duplicates.Count - 5} more duplicates");
            }

            // Check for data gaps - NOW WITH WEEKEND AWARENESS
            if (Records.Count > 1)
            {
                var sortedRecords = Records.OrderBy(r => r.DateTime).ToList();
                var gaps = new List<(DateTime from, DateTime to, TimeSpan duration, int businessDaysGap)>();

                for (var i = 1; i < sortedRecords.Count; i++)
                {
                    var gap = sortedRecords[i].DateTime - sortedRecords[i - 1].DateTime;
                    var fromDate = sortedRecords[i - 1].DateTime;
                    var toDate = sortedRecords[i].DateTime;

                    // Calculate business days gap (excludes weekends)
                    var businessDaysGap = CalculateBusinessDayGap(fromDate, toDate);

                    // Consider gaps > 3 business days as significant (allows for long weekends/holidays)
                    // This means Fri->Mon (0 business days) and Fri->Tue (1 business day) are normal
                    if (businessDaysGap > 3) gaps.Add((fromDate, toDate, gap, businessDaysGap));
                }

                if (gaps.Any())
                {
                    loadResult.ValidationWarnings.Add($"Found {gaps.Count} significant data gaps (> 3 business days)");

                    foreach (var gap in gaps.Take(5)) // Show first 5 gaps
                        loadResult.ValidationWarnings.Add($"  Gap: {gap.from:yyyy-MM-dd} to {gap.to:yyyy-MM-dd} " +
                                                          $"({gap.duration.TotalDays:F1} calendar days, {gap.businessDaysGap} business days)");

                    if (gaps.Count > 5) loadResult.ValidationWarnings.Add($"  ... and {gaps.Count - 5} more gaps");
                }
            }

            // NEW: Validate daily market hours coverage (9:30 AM - 3:59 PM Eastern Time)
            ValidateDailyMarketHours(loadResult);

            // Validate date range
            if (Records.Count > 0)
            {
                var firstDate = Records.Min(r => r.DateTime);
                var lastDate = Records.Max(r => r.DateTime);
                var timeSpan = lastDate - firstDate;

                if (timeSpan.TotalDays > 365 * 50) // More than 50 years
                    loadResult.ValidationWarnings.Add(
                        $"Large date range detected: {timeSpan.TotalDays:F0} days ({firstDate:yyyy-MM-dd} to {lastDate:yyyy-MM-dd})");

                if (firstDate.Year < 1900)
                    loadResult.ValidationWarnings.Add(
                        $"Very old data detected: earliest record from {firstDate:yyyy-MM-dd}");

                if (lastDate > DateTime.Now.AddDays(1))
                    loadResult.ValidationWarnings.Add(
                        $"Future data detected: latest record from {lastDate:yyyy-MM-dd}");
            }
        }

        /// <summary>
        ///     Validate market hours coverage for loaded data (called from ValidateLoadedData)
        ///     Enhanced with US market holiday recognition
        /// </summary>
        private void ValidateLoadedDataMarketHours(ValidationResult result)
        {
            // Group records by trading day (weekdays only)
            var recordsByDay = Records
                .Where(r => r.DateTime.DayOfWeek != DayOfWeek.Saturday && r.DateTime.DayOfWeek != DayOfWeek.Sunday)
                .GroupBy(r => r.DateTime.Date)
                .ToList();

            var marketHourIssues = 0;
            var perfectDays = 0;
            var checkedDays = 0;
            var holidayDays = 0;

            foreach (var dayGroup in recordsByDay)
            {
                var date = dayGroup.Key;
                var dayRecords = dayGroup.OrderBy(r => r.DateTime).ToList();

                if (dayRecords.Count == 0) continue;

                checkedDays++;
                var firstRecord = dayRecords.First();
                var lastRecord = dayRecords.Last();

                // Check if this is a known US market holiday or half-day
                var holidayInfo = GetUSMarketHolidayInfo(date);

                // Skip full market closure days from validation
                if (holidayInfo.IsClosed) continue; // Don't count closed days in validation

                // Market hours in Eastern Time: 9:30 AM to 3:59 PM (or 1:00 PM for half days)
                var marketOpen = date.AddHours(9).AddMinutes(30); // 9:30 AM Eastern
                var marketClose = holidayInfo.IsHalfDay
                    ? date.AddHours(13).AddMinutes(0)
                    : date.AddHours(15).AddMinutes(59); // 1:00 PM or 3:59 PM Eastern

                var hasMarketOpenIssue = false;
                var hasMarketCloseIssue = false;

                // Check if first price is significantly after market open (allow 5 minute tolerance)
                if (firstRecord.DateTime > marketOpen.AddMinutes(5))
                {
                    hasMarketOpenIssue = true;
                    marketHourIssues++;
                }

                // Check if last price is significantly before market close (allow appropriate tolerance)
                var earlyCloseToleranceMinutes = holidayInfo.IsHalfDay ? 5 : 30; // Stricter tolerance for half days
                if (lastRecord.DateTime < marketClose.AddMinutes(-earlyCloseToleranceMinutes))
                {
                    hasMarketCloseIssue = true;
                    marketHourIssues++;
                }

                // For half days, be more lenient if close is near 1:00 PM
                if (holidayInfo.IsHalfDay)
                {
                    holidayDays++;
                    var expectedHalfDayClose = date.AddHours(13).AddMinutes(0); // 1:00 PM
                    var timeDifferenceFromExpected =
                        Math.Abs((lastRecord.DateTime - expectedHalfDayClose).TotalMinutes);

                    if (timeDifferenceFromExpected <= 5) // Within 5 minutes of 1:00 PM
                    {
                        // This is correct for a half day - don't mark as an issue
                        hasMarketCloseIssue = false;
                        if (marketHourIssues > 0) marketHourIssues--; // Remove the issue we just added
                    }
                }

                // Count perfect days (proper market hours coverage)
                if (!hasMarketOpenIssue && !hasMarketCloseIssue) perfectDays++;
            }

            // Report market hours validation results
            if (checkedDays > 0)
            {
                var marketHoursCoverage = perfectDays / (double)checkedDays * 100;

                if (holidayDays > 0)
                    result.Warnings.Add(
                        $"Market hours summary: {perfectDays}/{checkedDays} days ({marketHoursCoverage:F1}%) perfect coverage, {holidayDays} US market half-day holidays recognized");
                else if (marketHoursCoverage < 90.0) // Less than 90% perfect days
                    result.Warnings.Add(
                        $"Market hours coverage: {perfectDays}/{checkedDays} days ({marketHoursCoverage:F1}%) have proper 9:30 AM - 3:59 PM coverage");

                if (marketHourIssues > checkedDays * 0.2) // More than 20% of days have issues
                    result.Warnings.Add(
                        $"Market hours issues detected on {marketHourIssues} day occurrences - verify timezone conversion");
            }
        }

        /// <summary>
        ///     Validate that each trading day has proper market hours coverage
        ///     Ensures first price is at/after 9:30 AM Eastern and last price is at/before 3:59 PM Eastern
        ///     Enhanced with US market holiday and half-day recognition
        /// </summary>
        private void ValidateDailyMarketHours(LoadResult loadResult)
        {
            if (Records.Count == 0) return;

            // Group records by trading day (weekdays only)
            var recordsByDay = Records
                .Where(r => r.DateTime.DayOfWeek != DayOfWeek.Saturday && r.DateTime.DayOfWeek != DayOfWeek.Sunday)
                .GroupBy(r => r.DateTime.Date)
                .ToList();

            var marketHourIssues = new List<string>();
            var perfectDays = 0;
            var checkedDays = 0;
            var holidayDays = 0;

            foreach (var dayGroup in recordsByDay)
            {
                var date = dayGroup.Key;
                var dayRecords = dayGroup.OrderBy(r => r.DateTime).ToList();

                if (dayRecords.Count == 0) continue;

                checkedDays++;
                var firstRecord = dayRecords.First();
                var lastRecord = dayRecords.Last();

                // Check if this is a known US market holiday or half-day
                var holidayInfo = GetUSMarketHolidayInfo(date);

                // Market hours in Eastern Time: 9:30 AM to 3:59 PM (or 1:00 PM for half days)
                var marketOpen = date.AddHours(9).AddMinutes(30); // 9:30 AM Eastern
                var marketClose = holidayInfo.IsHalfDay
                    ? date.AddHours(13).AddMinutes(0)
                    : date.AddHours(15).AddMinutes(59); // 1:00 PM or 3:59 PM Eastern

                var hasMarketOpenIssue = false;
                var hasMarketCloseIssue = false;

                // Check if first price is significantly after market open (allow 1 minute tolerance)
                if (firstRecord.DateTime > marketOpen.AddMinutes(1))
                {
                    var delayMinutes = (firstRecord.DateTime - marketOpen).TotalMinutes;
                    marketHourIssues.Add($"  {date:yyyy-MM-dd}: First price at {firstRecord.DateTime:HH:mm:ss} " +
                                         $"({delayMinutes:F1} min after 9:30 AM market open)");
                    hasMarketOpenIssue = true;
                }

                // Check if last price is significantly before market close
                var earlyCloseToleranceMinutes = holidayInfo.IsHalfDay ? 5 : 30; // Stricter tolerance for half days
                if (lastRecord.DateTime < marketClose.AddMinutes(-earlyCloseToleranceMinutes))
                {
                    var earlyMinutes = (marketClose - lastRecord.DateTime).TotalMinutes;
                    var expectedCloseTime = holidayInfo.IsHalfDay ? "1:00 PM" : "4:15 PM";
                    marketHourIssues.Add($"  {date:yyyy-MM-dd}: Last price at {lastRecord.DateTime:HH:mm:ss} " +
                                         $"({earlyMinutes:F1} min before {expectedCloseTime} market close)");
                    hasMarketCloseIssue = true;
                }

                // For half days, validate that last price is actually around 1:00 PM
                if (holidayInfo.IsHalfDay)
                {
                    holidayDays++;
                    var expectedHalfDayClose = date.AddHours(13).AddMinutes(0); // 1:00 PM
                    var timeDifferenceFromExpected =
                        Math.Abs((lastRecord.DateTime - expectedHalfDayClose).TotalMinutes);

                    if (timeDifferenceFromExpected <= 5) // Within 5 minutes of 1:00 PM
                    {
                        // This is correct for a half day - don't mark as an issue
                        hasMarketCloseIssue = false;
                        // Remove the issue we might have added above
                        marketHourIssues.RemoveAll(issue =>
                            issue.Contains(date.ToString("yyyy-MM-dd")) && issue.Contains("before"));

                        // Add positive confirmation
                        marketHourIssues.Add(
                            $"  {date:yyyy-MM-dd}: ✅ {holidayInfo.Description} - correct 1:00 PM close at {lastRecord.DateTime:HH:mm:ss}");
                    }
                }

                // Check for data that's suspiciously outside market hours
                var preMarketRecords =
                    dayRecords.Where(r => r.DateTime < marketOpen.AddMinutes(-60)).ToList(); // Before 8:30 AM
                var lateAfterHoursRecords =
                    dayRecords.Where(r => r.DateTime > marketClose.AddMinutes(180))
                        .ToList(); // More than 3 hours after close

                if (preMarketRecords.Any())
                {
                    var earliestPreMarket = preMarketRecords.Min(r => r.DateTime);
                    marketHourIssues.Add(
                        $"  {date:yyyy-MM-dd}: Has pre-market data as early as {earliestPreMarket:HH:mm:ss}");
                }

                if (lateAfterHoursRecords.Any())
                {
                    var latestAfterHours = lateAfterHoursRecords.Max(r => r.DateTime);
                    marketHourIssues.Add(
                        $"  {date:yyyy-MM-dd}: Has late after-hours data as late as {latestAfterHours:HH:mm:ss}");
                }

                // Count perfect days (proper market hours coverage)
                if (!hasMarketOpenIssue && !hasMarketCloseIssue) perfectDays++;
            }

            // Report market hours validation results
            if (marketHourIssues.Any())
            {
                loadResult.ValidationWarnings.Add(
                    $"Market hours coverage analysis for {checkedDays} trading day(s) ({holidayDays} half-day holidays detected):");
                foreach (var issue in marketHourIssues.Take(15)) // Show first 15 issues
                    loadResult.ValidationWarnings.Add(issue);
                if (marketHourIssues.Count > 15)
                    loadResult.ValidationWarnings.Add(
                        $"  ... and {marketHourIssues.Count - 15} more market hours notes");
            }

            // Summary stats
            if (checkedDays > 0)
            {
                var marketHoursCoverage = perfectDays / (double)checkedDays * 100;
                if (holidayDays > 0)
                    loadResult.ValidationWarnings.Add(
                        $"Market hours summary: {perfectDays}/{checkedDays} days ({marketHoursCoverage:F1}%) perfect, {holidayDays} US market half-day holidays detected");
                else if (marketHoursCoverage < 95.0) // Less than 95% perfect days
                    loadResult.ValidationWarnings.Add(
                        $"Market hours coverage: {perfectDays}/{checkedDays} days ({marketHoursCoverage:F1}%) have proper 9:30 AM - 3:59 PM coverage");
                else
                    // This is good news - add to info log if we had a place for it
                    ConsoleUtilities.WriteLine(
                        $"[INFO] Excellent market hours coverage: {perfectDays}/{checkedDays} days ({marketHoursCoverage:F1}%)");
            }
        }

        /// <summary>
        ///     Get US market holiday information for a specific date
        ///     Returns information about market closures and half-day schedules
        /// </summary>
        public static (bool IsHalfDay, bool IsClosed, string Description) GetUSMarketHolidayInfo(DateTime date)
        {
            var year = date.Year;
            var month = date.Month;
            var day = date.Day;
            var dayOfWeek = date.DayOfWeek;

            // Only check weekdays
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
                return (false, false, "Weekend");

            // Half-day holidays (1:00 PM ET close)

            // Day before Independence Day (when July 4th falls on weekday)
            if (month == 7 && day == 3)
            {
                var july4th = new DateTime(year, 7, 4);
                if (july4th.DayOfWeek >= DayOfWeek.Monday && july4th.DayOfWeek <= DayOfWeek.Friday)
                    return (true, false, "Day before Independence Day");
            }

            // Black Friday (day after Thanksgiving)
            var thanksgiving = GetThanksgivingDate(year);
            var blackFriday = thanksgiving.AddDays(1);
            if (date.Date == blackFriday.Date) return (true, false, "Black Friday (day after Thanksgiving)");

            // Christmas Eve (when December 25th falls on weekday)
            if (month == 12 && day == 24)
            {
                var christmas = new DateTime(year, 12, 25);
                if (christmas.DayOfWeek >= DayOfWeek.Monday && christmas.DayOfWeek <= DayOfWeek.Friday)
                    return (true, false, "Christmas Eve");
            }

            // Day before New Year's Day (when January 1st falls on weekday)
            if (month == 12 && day == 31)
            {
                var newYears = new DateTime(year + 1, 1, 1);
                if (newYears.DayOfWeek >= DayOfWeek.Monday && newYears.DayOfWeek <= DayOfWeek.Friday)
                    return (true, false, "New Year's Eve");
            }

            // Full market closures (no trading)

            // New Year's Day - with Monday observation if Sunday
            if (month == 1 && day == 1) return (false, true, "New Year's Day");
            if (month == 1 && day == 2 && dayOfWeek == DayOfWeek.Monday)
            {
                var newYears = new DateTime(year, 1, 1);
                if (newYears.DayOfWeek == DayOfWeek.Sunday)
                    return (false, true, "New Year's Day (Observed)");
            }

            // Martin Luther King Jr. Day (third Monday in January)
            var mlkDay = GetNthWeekdayOfMonth(year, 1, DayOfWeek.Monday, 3);
            if (date.Date == mlkDay.Date) return (false, true, "Martin Luther King Jr. Day");

            // Presidents Day (third Monday in February)
            var presidentsDay = GetNthWeekdayOfMonth(year, 2, DayOfWeek.Monday, 3);
            if (date.Date == presidentsDay.Date) return (false, true, "Presidents Day");

            // Good Friday (2 days before Easter Sunday) - NYSE/Nasdaq closed
            // (Add only once; Easter algorithm local to avoid wider surface)
            {
                // Compute Easter Sunday (Meeus/Jones/Butcher)
                int a = year % 19;
                int b = year / 100;
                int c = year % 100;
                int d = b / 4;
                int e = b % 4;
                int f = (b + 8) / 25;
                int g = (b - f + 1) / 3;
                int h = (19 * a + b - d - g + 15) % 30;
                int i = c / 4;
                int k = c % 4;
                int l = (32 + 2 * e + 2 * i - h - k) % 7;
                int m2 = (a + 11 * h + 22 * l) / 451;
                int monthE = (h + l - 7 * m2 + 114) / 31;
                int dayE = ((h + l - 7 * m2 + 114) % 31) + 1;
                var easterSunday = new DateTime(year, monthE, dayE);
                var goodFriday = easterSunday.AddDays(-2);
                if (date.Date == goodFriday.Date) return (false, true, "Good Friday");
            }

            // Memorial Day (last Monday in May)
            var memorialDay = GetLastWeekdayOfMonth(year, 5, DayOfWeek.Monday);
            if (date.Date == memorialDay.Date) return (false, true, "Memorial Day");

            // Juneteenth (June 19) - added as federal market holiday (since 2022)
            // With Monday observation if Sunday
            if (month == 6 && day == 19) return (false, true, "Juneteenth National Independence Day");
            if (month == 6 && day == 20 && dayOfWeek == DayOfWeek.Monday)
            {
                var juneteenth = new DateTime(year, 6, 19);
                if (juneteenth.DayOfWeek == DayOfWeek.Sunday)
                    return (false, true, "Juneteenth National Independence Day (Observed)");
            }

            // Independence Day - with Monday observation if Sunday
            if (month == 7 && day == 4) return (false, true, "Independence Day");
            if (month == 7 && day == 5 && dayOfWeek == DayOfWeek.Monday)
            {
                var july4th = new DateTime(year, 7, 4);
                if (july4th.DayOfWeek == DayOfWeek.Sunday)
                    return (false, true, "Independence Day (Observed)");
            }

            // Labor Day (first Monday in September)
            var laborDay = GetNthWeekdayOfMonth(year, 9, DayOfWeek.Monday, 1);
            if (date.Date == laborDay.Date) return (false, true, "Labor Day");

            // Thanksgiving
            if (date.Date == thanksgiving.Date) return (false, true, "Thanksgiving Day");

            // Christmas Day - with Monday observation if Sunday
            if (month == 12 && day == 25) return (false, true, "Christmas Day");
            if (month == 12 && day == 26 && dayOfWeek == DayOfWeek.Monday)
            {
                var christmas = new DateTime(year, 12, 25);
                if (christmas.DayOfWeek == DayOfWeek.Sunday)
                    return (false, true, "Christmas Day (Observed)");
            }

            // Regular trading day
            return (false, false, "Regular trading day");
        }

        /// <summary>
        ///     Get Thanksgiving date (fourth Thursday in November)
        /// </summary>
        private static DateTime GetThanksgivingDate(int year)
        {
            return GetNthWeekdayOfMonth(year, 11, DayOfWeek.Thursday, 4);
        }

        /// <summary>
        ///     Get the Nth occurrence of a specific weekday in a month
        /// </summary>
        private static DateTime GetNthWeekdayOfMonth(int year, int month, DayOfWeek dayOfWeek, int occurrence)
        {
            var firstDayOfMonth = new DateTime(year, month, 1);
            var firstOccurrence = firstDayOfMonth.AddDays(((int)dayOfWeek - (int)firstDayOfMonth.DayOfWeek + 7) % 7);
            return firstOccurrence.AddDays((occurrence - 1) * 7);
        }

        /// <summary>
        ///     Get the last occurrence of a specific weekday in a month
        /// </summary>
        private static DateTime GetLastWeekdayOfMonth(int year, int month, DayOfWeek dayOfWeek)
        {
            var lastDayOfMonth = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            var lastOccurrence = lastDayOfMonth.AddDays(-((int)lastDayOfMonth.DayOfWeek - (int)dayOfWeek + 7) % 7);
            return lastOccurrence;
        }

        public void LogLoadingSummary(LoadResult loadResult)
        {
            // This would typically log to a logging framework, but for now we'll use Console
            // In production, replace with proper logging
            ConsoleUtilities.WriteLine("Price Data Loading Summary:");
            ConsoleUtilities.WriteLine($"  File: Processed {loadResult.TotalLinesProcessed} lines");
            ConsoleUtilities.WriteLine(
                $"  Success: {loadResult.ValidRecordsLoaded} records loaded ({loadResult.SuccessRate:F1}% success rate)");
            ConsoleUtilities.WriteLine($"  Skipped: {loadResult.SkippedRecords} records");
            ConsoleUtilities.WriteLine($"  Time: {loadResult.LoadTimeMilliseconds:F0}ms");

            if (loadResult.ValidationErrors.Count > 0)
            {
                ConsoleUtilities.WriteLine($"  Errors: {loadResult.ValidationErrors.Count}");
                foreach (var error in loadResult.ValidationErrors.Take(10))
                    ConsoleUtilities.WriteLine($"    - {error}");
                if (loadResult.ValidationErrors.Count > 10)
                    ConsoleUtilities.WriteLine($"    ... and {loadResult.ValidationErrors.Count - 10} more errors");
            }

            if (loadResult.ValidationWarnings.Count > 0)
            {
                ConsoleUtilities.WriteLine($"  Warnings: {loadResult.ValidationWarnings.Count}");
                foreach (var warning in loadResult.ValidationWarnings.Take(5))
                    ConsoleUtilities.WriteLine($"    - {warning}");
                if (loadResult.ValidationWarnings.Count > 5)
                    ConsoleUtilities.WriteLine($"    ... and {loadResult.ValidationWarnings.Count - 5} more warnings");
            }
        }

        private void BuildAllAggregations()
        {
            // Build aggregations for all timeframes sequentially
            foreach (var record in Records) UpdateAggregations(record);
        }

        private void BuildAllAggregationsParallel()
        {
            // Group records by timeframe periods for parallel processing
            var timeFrames = _aggregatedData.Keys.ToArray();

            // Process each timeframe in parallel
            Parallel.ForEach(timeFrames, timeFrame =>
            {
                if (timeFrame == TimeFrame.M1)
                    // 1-minute is just the base data
                    foreach (var record in Records)
                        _aggregatedData[timeFrame].AddOrUpdate(record);
            });

            Parallel.ForEach(timeFrames, timeFrame =>
            {
                if (timeFrame != TimeFrame.M1) BuildTimeFrameAggregation(timeFrame);
            });
        }

        private void BuildTimeFrameAggregation(TimeFrame timeFrame)
        {
            var minutes = (int)timeFrame;
            var aggregatedData = _aggregatedData[timeFrame];

            // Group base records by normalized timestamp for this timeframe
            var groupedRecords = Records
                .GroupBy(r => GetNormalizedTimestamp(r.DateTime, minutes).Ticks)
                .ToList();

            // Process groups in parallel for large datasets
            if (groupedRecords.Count > 100)
            {
                var aggregatedRecords = new ConcurrentBag<PriceRecord>();

                Parallel.ForEach(groupedRecords, group =>
                {
                    var normalizedTime = new DateTime(group.Key);
                    var recordsInPeriod = group.OrderBy(r => r.DateTime).ToList();

                    if (recordsInPeriod.Count > 0)
                    {
                        var aggregatedRecord =
                            CreateAggregatedRecordFromGroup(recordsInPeriod, normalizedTime, timeFrame);
                        aggregatedRecords.Add(aggregatedRecord);
                    }
                });

                foreach (var record in aggregatedRecords) aggregatedData.AddOrUpdate(record);
            }
            else
            {
                // Sequential processing for smaller datasets
                foreach (var group in groupedRecords)
                {
                    var normalizedTime = new DateTime(group.Key);
                    var recordsInPeriod = group.OrderBy(r => r.DateTime).ToList();

                    if (recordsInPeriod.Count > 0)
                    {
                        var aggregatedRecord =
                            CreateAggregatedRecordFromGroup(recordsInPeriod, normalizedTime, timeFrame);
                        aggregatedData.AddOrUpdate(aggregatedRecord);
                    }
                }
            }
        }

        /// <summary>
        ///     Determine if a bar is complete based on Eastern Time market hours and current time
        /// </summary>
        private static bool IsBarComplete(DateTime barStartTime, TimeFrame timeFrame, DateTime currentTime)
        {
            var minutes = (int)timeFrame;
            var barEndTime = barStartTime.AddMinutes(minutes);

            // Convert current time to Eastern Time for market hours comparison
            var easternTz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            var currentEasternTime = TimeZoneInfo.ConvertTime(currentTime, easternTz);
            var barStartEasternTime = TimeZoneInfo.ConvertTime(barStartTime, easternTz);
            var barEndEasternTime = TimeZoneInfo.ConvertTime(barEndTime, easternTz);

            // Market hours in Eastern Time: 9:30 AM to 4:15 PM
            var marketOpen = barStartEasternTime.Date.AddHours(9).AddMinutes(30); // 9:30 AM ET
            var marketClose = barStartEasternTime.Date.AddHours(16).AddMinutes(15); // 4:15 PM ET

            // For daily bars, complete only after market close
            if (timeFrame == TimeFrame.D1) return currentEasternTime >= marketClose;

            // For intraday bars, complete when:
            // 1. Bar end time has passed AND
            // 2. We're either still in market hours OR past market close for the day
            if (currentEasternTime >= barEndEasternTime)
            {
                // If we're past market close, all bars for that day are complete
                if (currentEasternTime >= marketClose) return true;

                // If we're during market hours, bar is complete if end time has passed
                if (currentEasternTime >= marketOpen && currentEasternTime <= marketClose) return true;

                // If we're before market open but bar end time has passed (e.g., pre-market data)
                // Consider it complete if it's not extending into market hours
                if (barEndEasternTime <= marketOpen) return true;
            }

            return false;
        }

        private PriceRecord CreateAggregatedRecordFromGroup(List<PriceRecord> recordsInPeriod, DateTime normalizedTime,
            TimeFrame timeFrame)
        {
            // Aggregate OHLC data
            var open = recordsInPeriod.First().Open;
            var high = recordsInPeriod.Max(r => r.High);
            var low = recordsInPeriod.Min(r => r.Low);
            var close = recordsInPeriod.Last().Close;
            var volume = recordsInPeriod.Sum(r => r.Volume);
            var count = recordsInPeriod.Sum(r => r.Count);

            var option = recordsInPeriod.First().Option;

            // Calculate WAP (weighted average price)
            var totalVolume = volume;
            var wap = totalVolume > 0 ? recordsInPeriod.Sum(r => r.WAP * r.Volume) / totalVolume : close;

            // Determine if bar is complete based on market hours
            var now = DateTime.Now;
            var isComplete = IsBarComplete(normalizedTime, timeFrame, now);

            return new PriceRecord(normalizedTime, timeFrame, open, high, low, close, volume: volume, wap: wap, count: count, option: option, isComplete: isComplete);
        }

        private void UpdateAggregations(PriceRecord baseRecord)
        {
            // Parallel update of timeframes for better performance
            var timeFrames = _aggregatedData.Keys.ToArray();

            Parallel.ForEach(timeFrames, timeFrame =>
            {
                if (timeFrame == TimeFrame.M1)
                {
                    // 1-minute is just the base data
                    _aggregatedData[timeFrame].AddOrUpdate(baseRecord);
                }
                else
                {
                    // Aggregate from base record
                    var aggregatedRecord = CreateAggregatedRecord(baseRecord, timeFrame);
                    _aggregatedData[timeFrame].AddOrUpdate(aggregatedRecord);
                }
            });
        }

        private PriceRecord CreateAggregatedRecord(PriceRecord baseRecord, TimeFrame timeFrame)
        {
            var minutes = (int)timeFrame;
            var normalizedTime = GetNormalizedTimestamp(baseRecord.DateTime, minutes);

            // Find all base records within this timeframe period
            var periodStart = normalizedTime;
            var periodEnd = normalizedTime.AddMinutes(minutes);

            var recordsInPeriod = Records.Where(r => r.DateTime >= periodStart && r.DateTime < periodEnd).ToList();

            if (recordsInPeriod.Count == 0)
                return baseRecord.Clone();

            return CreateAggregatedRecordFromGroup(recordsInPeriod, normalizedTime, timeFrame);
        }

        private static DateTime GetNormalizedTimestamp(DateTime timestamp, int minutes)
        {
            if (minutes >= 1440) // Daily or larger
                return timestamp.Date;

            var totalMinutes = timestamp.Hour * 60 + timestamp.Minute;
            var normalizedMinutes = totalMinutes / minutes * minutes;

            return timestamp.Date.AddMinutes(normalizedMinutes);
        }

        private DateTime ParseDateTimeWithTimezone(string timeStr)
        {
            // Example: "20250808 03:30:00 Pacific/Honolulu"
            var parts = timeStr.Split(' ');
            if (parts.Length >= 3)
            {
                var datePart = parts[0];
                var timePart = parts[1];
                var tzPart = string.Join(" ", parts, 2, parts.Length - 2);
                var dtStr = $"{datePart} {timePart}";

                if (DateTime.TryParseExact(dtStr, "yyyyMMdd HH:mm:ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt))
                    // Convert from source timezone to Eastern Time
                    return ConvertToEasternTime(dt, tzPart);
            }

            // Fallback: try direct parse and assume it's already in Eastern Time
            DateTime.TryParse(timeStr, out var fallback);
            return fallback;
        }

        /// <summary>
        ///     Convert datetime from source timezone to Eastern Time
        /// </summary>
        private static DateTime ConvertToEasternTime(DateTime sourceDateTime, string sourceTimezone)
        {
            try
            {
                // Get Eastern Time zone info
                var easternTz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

                // Map common timezone strings to TimeZoneInfo IDs
                var sourceTz = GetTimeZoneInfo(sourceTimezone);

                if (sourceTz != null)
                {
                    // Create DateTime with source timezone
                    var sourceTimeZoned = TimeZoneInfo.ConvertTimeToUtc(sourceDateTime, sourceTz);

                    // Convert to Eastern Time
                    return TimeZoneInfo.ConvertTimeFromUtc(sourceTimeZoned, easternTz);
                }

                // If we can't identify the timezone, assume it's already Eastern Time
                return sourceDateTime;
            }
            catch
            {
                // If timezone conversion fails, return original datetime
                return sourceDateTime;
            }
        }

        /// <summary>
        ///     Map timezone strings to .NET TimeZoneInfo
        ///     CRITICAL: Hawaii does NOT observe Daylight Saving Time (stays UTC-10 year-round)
        /// </summary>
        private static TimeZoneInfo GetTimeZoneInfo(string timezoneString)
        {
            if (string.IsNullOrWhiteSpace(timezoneString))
                return null;

            var tz = timezoneString.Trim().ToLowerInvariant();

            try
            {
                switch (tz)
                {
                    case "pacific/honolulu":
                    case "hst":
                    case "hawaii":
                        // Hawaiian Standard Time - CRITICAL: NO DST, always UTC-10
                        // This ensures consistent market timing conversion year-round
                        return TimeZoneInfo.FindSystemTimeZoneById("Hawaiian Standard Time");

                    case "us/pacific":
                    case "pacific/los_angeles":
                    case "pst":
                    case "pdt":
                    case "pacific":
                        return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

                    case "us/mountain":
                    case "america/denver":
                    case "mst":
                    case "mdt":
                    case "mountain":
                        return TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");

                    case "us/central":
                    case "america/chicago":
                    case "cst":
                    case "cdt":
                    case "central":
                        return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

                    case "us/eastern":
                    case "america/new_york":
                    case "est":
                    case "edt":
                    case "eastern":
                        return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

                    case "utc":
                    case "gmt":
                    case "zulu":
                        return TimeZoneInfo.Utc;

                    default:
                        // Try to find by direct ID match
                        return TimeZoneInfo.FindSystemTimeZoneById(timezoneString);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Static method to parse a single JSON line
        /// </summary>
        public static PriceRecord ParseJsonLine(string jsonLine)
        {
            if (string.IsNullOrWhiteSpace(jsonLine))
                return null;

            try
            {
                var record = JsonConvert.DeserializeObject<PriceRecord>(jsonLine);
                if (record != null)
                {
                    // Validate the record
                    var errors = new ConcurrentBag<string>();
                    var warnings = new ConcurrentBag<string>();

                    if (ValidatePriceRecordStatic(record, 1, errors, warnings))
                    {
                        record.DateTime = ParseDateTimeFromString(record.Time);
                        return record;
                    }

                    // Log validation errors for debugging
                    if (errors.Any())
                        throw new InvalidDataException($"Price record validation failed: {string.Join("; ", errors)}");
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"JSON parsing error: {ex.Message}", ex);
            }

            return null;
        }

        /// <summary>
        ///     Static method to parse multiple JSON lines in parallel
        /// </summary>
        public static List<PriceRecord> ParseJsonLines(IEnumerable<string> jsonLines)
        {
            var lines = jsonLines.ToList();
            if (lines.Count == 0) return new List<PriceRecord>();

            var validRecords = new List<PriceRecord>();
            var validationErrors = new ConcurrentBag<string>();
            var validationWarnings = new ConcurrentBag<string>();

            if (lines.Count < 100)
            {
                // Sequential processing for small datasets
                for (var i = 0; i < lines.Count; i++)
                    try
                    {
                        var record = ParseJsonLineWithValidation(lines[i], i + 1, validationErrors, validationWarnings);
                        if (record != null) validRecords.Add(record);
                    }
                    catch (Exception ex)
                    {
                        validationErrors.Add($"Line {i + 1}: {ex.Message}");
                    }
            }
            else
            {
                // Parallel processing for large datasets
                var records = new ConcurrentBag<PriceRecord>();
                var errors = new ConcurrentBag<string>();
                var warnings = new ConcurrentBag<string>();

                Parallel.ForEach(lines, (line, loop, index) =>
                {
                    try
                    {
                        var record = ParseJsonLineWithValidation(line, (int)index + 1, errors, warnings);
                        if (record != null) records.Add(record);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Line {index + 1}: {ex.Message}");
                    }
                });

                validRecords.AddRange(records.OrderBy(r => r.DateTime));
                foreach (var validationError in errors) validationErrors.Add(validationError);
                foreach (var validationWarning in warnings) validationWarnings.Add(validationWarning);
            }

            // Log validation summary if there were issues
            if (validationErrors.Any() || validationWarnings.Any())
            {
                ConsoleUtilities.WriteLine(
                    $"JSON Lines Parsing Summary: {validRecords.Count}/{lines.Count} records parsed successfully");

                if (validationErrors.Any())
                {
                    ConsoleUtilities.WriteLine($"Errors: {validationErrors.Count}");
                    foreach (var error in validationErrors.Take(5)) ConsoleUtilities.WriteLine($"  - {error}");
                    if (validationErrors.Count > 5)
                        ConsoleUtilities.WriteLine($"  ... and {validationErrors.Count - 5} more errors");
                }

                if (validationWarnings.Any())
                {
                    ConsoleUtilities.WriteLine($"Warnings: {validationWarnings.Count}");
                    foreach (var warning in validationWarnings.Take(3)) ConsoleUtilities.WriteLine($"  - {warning}");
                    if (validationWarnings.Count > 3)
                        ConsoleUtilities.WriteLine($"  ... and {validationWarnings.Count - 3} more warnings");
                }
            }

            return validRecords;
        }

        private static PriceRecord ParseJsonLineWithValidation(string jsonLine, int lineNumber,
            ConcurrentBag<string> errors, ConcurrentBag<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(jsonLine))
            {
                warnings.Add($"Line {lineNumber}: Empty line skipped");
                return null;
            }

            try
            {
                var record = JsonConvert.DeserializeObject<PriceRecord>(jsonLine);
                if (record != null)
                {
                    if (ValidatePriceRecordStatic(record, lineNumber, errors, warnings))
                    {
                        record.DateTime = ParseDateTimeFromString(record.Time);
                        return record;
                    }
                }
                else
                {
                    errors.Add($"Line {lineNumber}: Failed to deserialize JSON - null result");
                }
            }
            catch (JsonException ex)
            {
                errors.Add($"Line {lineNumber}: JSON parsing error - {ex.Message}");
            }

            return null;
        }

        private static bool ValidatePriceRecordStatic(PriceRecord record, int lineNumber, ConcurrentBag<string> errors,
            ConcurrentBag<string> warnings)
        {
            var isValid = true;

            // Validate required fields
            if (string.IsNullOrWhiteSpace(record.Time))
            {
                errors.Add($"Line {lineNumber}: Missing or empty Time field");
                isValid = false;
            }

            // Validate OHLC data
            if (!ValidateOHLCPricesStatic(record, lineNumber, errors, warnings)) isValid = false;

            // Validate volume and count
            if (record.Volume < 0)
            {
                warnings.Add($"Line {lineNumber}: Negative volume ({record.Volume}) - setting to 0");
                record.Volume = 0;
            }

            if (record.Count < 0)
            {
                warnings.Add($"Line {lineNumber}: Negative count ({record.Count}) - setting to 0");
                record.Count = 0;
            }

            // Validate WAP if provided
            if (record.WAP > 0 && (record.WAP < record.Low || record.WAP > record.High))
                warnings.Add(
                    $"Line {lineNumber}: WAP ({record.WAP:F2}) outside High-Low range [{record.Low:F2}-{record.High:F2}]");

            return isValid;
        }

        private static bool ValidateOHLCPricesStatic(PriceRecord record, int lineNumber, ConcurrentBag<string> errors,
            ConcurrentBag<string> warnings)
        {
            var isValid = true;

            // Check for non-positive prices
            if (record.Open <= 0)
            {
                errors.Add($"Line {lineNumber}: Invalid Open price ({record.Open}) - must be positive");
                isValid = false;
            }

            if (record.High <= 0)
            {
                errors.Add($"Line {lineNumber}: Invalid High price ({record.High}) - must be positive");
                isValid = false;
            }

            if (record.Low <= 0)
            {
                errors.Add($"Line {lineNumber}: Invalid Low price ({record.Low}) - must be positive");
                isValid = false;
            }

            if (record.Close <= 0)
            {
                errors.Add($"Line {lineNumber}: Invalid Close price ({record.Close}) - must be positive");
                isValid = false;
            }

            if (!isValid) return false;

            // Validate OHLC relationships
            if (record.High < record.Low)
            {
                errors.Add($"Line {lineNumber}: High ({record.High:F2}) cannot be less than Low ({record.Low:F2})");
                isValid = false;
            }

            if (record.Open < record.Low || record.Open > record.High)
                warnings.Add(
                    $"Line {lineNumber}: Open ({record.Open:F2}) outside High-Low range [{record.Low:F2}-{record.High:F2}]");

            if (record.Close < record.Low || record.Close > record.High)
                warnings.Add(
                    $"Line {lineNumber}: Close ({record.Close:F2}) outside High-Low range [{record.Low:F2}-{record.High:F2}]");

            return isValid;
        }

        public static DateTime ParseDateTimeFromString(string timeStr)
        {
            // Example: "20250808 03:30:00 Pacific/Honolulu"
            var parts = timeStr.Split(' ');
            if (parts.Length >= 3)
            {
                var datePart = parts[0];
                var timePart = parts[1];
                var tzPart = string.Join(" ", parts, 2, parts.Length - 2);
                var dtStr = $"{datePart} {timePart}";

                if (DateTime.TryParseExact(dtStr, "yyyyMMdd HH:mm:ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt))
                    // Convert from source timezone to Eastern Time
                    return ConvertToEasternTime(dt, tzPart);
            }

            // Fallback: try direct parse and assume it's already in Eastern Time
            DateTime.TryParse(timeStr, out var fallback);
            return fallback;
        }

        /// <summary>
        ///     Get price records as array for D1 timeframe - for genetic algorithm processing
        /// </summary>
        /// <returns>Array of daily price records</returns>
        public PriceRecord[] GetDailyPriceRecords()
        {
            var dailyData = GetTimeFrame(TimeFrame.D1);
            var records = new PriceRecord[dailyData.Count];

            for (var i = 0; i < dailyData.Count; i++) records[i] = dailyData[i];

            return records;
        }

        /// <summary>
        ///     Enhanced method to create PriceRecord array from CSV file with comprehensive validation
        /// </summary>
        /// <param name="csvPath">Path to CSV file with format: Date,Open,High,Low,Close,Volume</param>
        /// <returns>Array of daily price records</returns>
        public static PriceRecord[] CreateDailyPriceRecordsFromCsv(string csvPath)
        {
            if (string.IsNullOrWhiteSpace(csvPath))
                throw new ArgumentException("CSV path cannot be null or empty", nameof(csvPath));

            if (!File.Exists(csvPath))
                throw new FileNotFoundException($"CSV file not found: {csvPath}");

            var lines = File.ReadAllLines(csvPath);
            var records = new List<PriceRecord>();
            var validationErrors = new List<string>();
            var validationWarnings = new List<string>();

            if (lines.Length == 0) throw new InvalidDataException("CSV file is empty");

            // Validate header row
            if (lines.Length > 0)
            {
                var header = lines[0].ToLowerInvariant();
                var expectedFields = new[] { "date", "open", "high", "low", "close", "volume" };
                var hasValidHeader = expectedFields.All(field => header.Contains(field));

                if (!hasValidHeader)
                    validationWarnings.Add(
                        "Header row may not match expected format (Date,Open,High,Low,Close,Volume)");
            }

            // Parse data rows (skip header)
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    validationWarnings.Add($"Line {i + 1}: Empty line skipped");
                    continue;
                }

                var parts = line.Split(',');
                if (parts.Length < 6)
                {
                    validationErrors.Add(
                        $"Line {i + 1}: Insufficient columns ({parts.Length}/6) - expected Date,Open,High,Low,Close,Volume");
                    continue;
                }

                try
                {
                    // Parse and validate each field
                    if (!DateTime.TryParse(parts[0].Trim(), out var date))
                    {
                        validationErrors.Add($"Line {i + 1}: Invalid date format '{parts[0].Trim()}'");
                        continue;
                    }

                    if (!double.TryParse(parts[1].Trim(), out var open) || open <= 0)
                    {
                        validationErrors.Add(
                            $"Line {i + 1}: Invalid open price '{parts[1].Trim()}' - must be positive number");
                        continue;
                    }

                    if (!double.TryParse(parts[2].Trim(), out var high) || high <= 0)
                    {
                        validationErrors.Add(
                            $"Line {i + 1}: Invalid high price '{parts[2].Trim()}' - must be positive number");
                        continue;
                    }

                    if (!double.TryParse(parts[3].Trim(), out var low) || low <= 0)
                    {
                        validationErrors.Add(
                            $"Line {i + 1}: Invalid low price '{parts[3].Trim()}' - must be positive number");
                        continue;
                    }

                    if (!double.TryParse(parts[4].Trim(), out var close) || close <= 0)
                    {
                        validationErrors.Add(
                            $"Line {i + 1}: Invalid close price '{parts[4].Trim()}' - must be positive number");
                        continue;
                    }

                    if (!double.TryParse(parts[5].Trim(), out var volume) || volume < 0)
                    {
                        validationWarnings.Add($"Line {i + 1}: Invalid volume '{parts[5].Trim()}' - setting to 0");
                        volume = 0;
                    }

                    // Validate OHLC relationships
                    if (high < low)
                    {
                        validationErrors.Add($"Line {i + 1}: High ({high:F2}) cannot be less than Low ({low:F2})");
                        continue;
                    }

                    if (open < low || open > high)
                        validationWarnings.Add(
                            $"Line {i + 1}: Open ({open:F2}) outside High-Low range [{low:F2}-{high:F2}]");

                    if (close < low || close > high)
                        validationWarnings.Add(
                            $"Line {i + 1}: Close ({close:F2}) outside High-Low range [{low:F2}-{high:F2}]");

                    // Check for reasonable date range
                    if (date.Year < 1900 || date.Year > DateTime.Now.Year + 1)
                        validationWarnings.Add($"Line {i + 1}: Unusual year ({date.Year}) in date");

                    // Create and add valid record
                    var record = new PriceRecord(date, TimeFrame.D1, open, high, low, close, volume: volume, wap: close, count: 1);
                    records.Add(record);
                }
                catch (Exception ex)
                {
                    validationErrors.Add($"Line {i + 1}: Unexpected parsing error - {ex.Message}");
                }
            }

            // Check for data gaps in daily data - NOW WITH WEEKEND AWARENESS
            if (records.Count > 1)
            {
                var sortedRecords = records.OrderBy(r => r.DateTime).ToList();
                var gaps = new List<(DateTime from, DateTime to, int businessDays)>();

                for (var i = 1; i < sortedRecords.Count; i++)
                {
                    var fromDate = sortedRecords[i - 1].DateTime;
                    var toDate = sortedRecords[i].DateTime;
                    var businessDaysGap = CalculateBusinessDayGap(fromDate, toDate);

                    // Consider gaps > 5 business days as significant for daily data
                    // This allows for normal weekends (0 days) and even week-long holidays
                    if (businessDaysGap > 5) gaps.Add((fromDate, toDate, businessDaysGap));
                }

                if (gaps.Any())
                {
                    validationWarnings.Add(
                        $"Found {gaps.Count} significant data gaps (> 5 business days) in daily data");
                    foreach (var gap in gaps.Take(5))
                        validationWarnings.Add(
                            $"  Gap: {gap.from:yyyy-MM-dd} to {gap.to:yyyy-MM-dd} ({gap.businessDays} business days)");
                    if (gaps.Count > 5) validationWarnings.Add($"  ... and {gaps.Count - 5} more gaps");
                }
            }

            // NEW: Validate that CSV daily data represents trading days properly
            ValidateCSVMarketDaysCoverage(records, validationWarnings);

            // Log validation summary
            if (validationErrors.Any() || validationWarnings.Any())
            {
                ConsoleUtilities.WriteLine(
                    $"CSV Loading Summary: {records.Count}/{lines.Length - 1} records loaded successfully");

                if (validationErrors.Any())
                {
                    ConsoleUtilities.WriteLine($"Errors: {validationErrors.Count}");
                    foreach (var error in validationErrors.Take(10)) ConsoleUtilities.WriteLine($"  - {error}");
                    if (validationErrors.Count > 10)
                        ConsoleUtilities.WriteLine($"  ... and {validationErrors.Count - 10} more errors");
                }

                if (validationWarnings.Any())
                {
                    ConsoleUtilities.WriteLine($"Warnings: {validationWarnings.Count}");
                    foreach (var warning in validationWarnings.Take(5)) ConsoleUtilities.WriteLine($"  - {warning}");
                    if (validationWarnings.Count > 5)
                        ConsoleUtilities.WriteLine($"  ... and {validationWarnings.Count - 5} more warnings");
                }
            }

            // Throw if we have critical errors or no valid data
            if (validationErrors.Any() && records.Count == 0)
                throw new InvalidDataException(
                    $"Failed to load any valid records from CSV. Errors: {string.Join("; ", validationErrors.Take(5))}");

            if (records.Count == 0) throw new InvalidDataException("No valid price records found in CSV file");

            return _dailyPriceRecords = records.ToArray();
        }

        /// <summary>
        ///     Create PriceRecord array from close prices for testing/fallback with enhanced historical data support
        ///     Enhanced with D1 vs M1 data integrity validation when minute data is provided
        /// </summary>
        /// <param name="priceBuffer">Array of close prices</param>
        /// <param name="minutePrices">Dictionary mapping day index to 390-minute intraday data (optional)</param>
        /// <param name="startDate">Starting date for the price series</param>
        /// <param name="addToPrices">Whether to create historical data for indicator calculations</param>
        /// <param name="historicalMultiplier">
        ///     How many times the buffer length to create as historical data (default 10x for 1,000
        ///     extra points)
        /// </param>
        /// <returns>Array of daily price records</returns>
        public static PriceRecord[] CreateDailyPriceRecordsFromClosePrices(double[] priceBuffer,
            Dictionary<int, double[]> minutePrices, DateTime? startDate, int historicalMultiplier = 10)
        {
            var baseDate = startDate ?? new DateTime(1900, 1, 1); // Start around 1900AD as you suggested
            var records = new PriceRecord[priceBuffer.Length];

            // Generate daily records first
            for (var i = 0; i < priceBuffer.Length; i++)
            {
                var date = baseDate.AddDays(i);
                var price = priceBuffer[i];
                records[i] = new PriceRecord(date, TimeFrame.D1, price, price, price, price, volume: 1000, wap: price, count: 1);
                if (i > 0)
                {
                    if (records[i].Close > records[i - 1].Close)
                    {
                        //HACK: this needs to adjust for the other direction, when we're going down...
                        records[i - 1].High = records[i].Open;
                        records[i - 1].Close = records[i].Open;
                    }

                    if (records[i].Close < records[i - 1].Close)
                    {
                        //HACK: this needs to adjust for the other direction, when we're going down...
                        records[i - 1].Low = records[i].Open;
                        records[i - 1].Close = records[i].Open;
                    }
                }
            }

            // CRITICAL VALIDATION: Verify D1 vs M1 data consistency when minute data is provided
            if (minutePrices != null && minutePrices.Any())
            {
                var validationErrors = new List<string>();
                var validationWarnings = new List<string>();
                var validatedDays = 0;
                var totalMinutePoints = 0;

                ConsoleUtilities.WriteLine(
                    $"[VALIDATION] Starting D1 vs M1 data integrity check for {minutePrices.Count} days...");

                foreach (var kvp in minutePrices)
                {
                    var dayIndex = kvp.Key;
                    var dayMinuteData = kvp.Value;

                    // Skip if day index is out of bounds
                    if (dayIndex < 0 || dayIndex >= records.Length)
                    {
                        validationWarnings.Add(
                            $"Day {dayIndex}: Index out of bounds (0-{records.Length - 1}), skipping");
                        continue;
                    }

                    // Skip if minute data is null or empty
                    if (dayMinuteData == null || dayMinuteData.Length == 0)
                    {
                        validationWarnings.Add($"Day {dayIndex}: No minute data available, skipping");
                        continue;
                    }

                    // Expected: 390 minutes (6.5 hours of trading: 9:30 AM - 4:15 PM)
                    const int expectedMinutes = 390;
                    if (dayMinuteData.Length != expectedMinutes)
                        validationWarnings.Add(
                            $"Day {dayIndex}: Expected {expectedMinutes} minutes, got {dayMinuteData.Length}");

                    var dailyRecord = records[dayIndex];
                    var firstMinutePrice = dayMinuteData[0]; // Should match daily open
                    var lastMinutePrice = dayMinuteData[dayMinuteData.Length - 1]; // Should match daily close

                    // Tolerance for floating-point comparison (allow small precision differences)
                    const double tolerance = 1e-6;

                    // Validate daily open vs first minute
                    if (Math.Abs(dailyRecord.Open - firstMinutePrice) > tolerance)
                        validationErrors.Add(
                            $"Day {dayIndex} ({dailyRecord.DateTime:yyyy-MM-dd}): Daily Open ({dailyRecord.Open:F6}) != First Minute ({firstMinutePrice:F6}), Diff: {Math.Abs(dailyRecord.Open - firstMinutePrice):F6}");

                    // Validate daily close vs last minute  
                    if (Math.Abs(dailyRecord.Close - lastMinutePrice) > tolerance)
                        validationErrors.Add(
                            $"Day {dayIndex} ({dailyRecord.DateTime:yyyy-MM-dd}): Daily Close ({dailyRecord.Close:F6}) != Last Minute ({lastMinutePrice:F6}), Diff: {Math.Abs(dailyRecord.Close - lastMinutePrice):F6}");

                    // Calculate minute-level high/low for additional validation
                    var minuteHigh = dayMinuteData.Max();
                    var minuteLow = dayMinuteData.Min();

                    // Daily high should be >= minute high (daily can include gaps/extended hours)
                    if (dailyRecord.High < minuteHigh - tolerance)
                        validationWarnings.Add(
                            $"Day {dayIndex} ({dailyRecord.DateTime:yyyy-MM-dd}): Daily High ({dailyRecord.High:F6}) < Minute High ({minuteHigh:F6})");

                    // Daily low should be <= minute low (daily can include gaps/extended hours)
                    if (dailyRecord.Low > minuteLow + tolerance)
                        validationWarnings.Add(
                            $"Day {dayIndex} ({dailyRecord.DateTime:yyyy-MM-dd}): Daily Low ({dailyRecord.Low:F6}) > Minute Low ({minuteLow:F6})");

                    validatedDays++;
                    totalMinutePoints += dayMinuteData.Length;
                }

                // Display validation summary
                ConsoleUtilities.WriteLine("[VALIDATION] D1 vs M1 Data Integrity Check Complete:");
                ConsoleUtilities.WriteLine($"  Days Validated: {validatedDays}/{minutePrices.Count}");
                ConsoleUtilities.WriteLine($"  Total Minute Points: {totalMinutePoints:N0}");
                ConsoleUtilities.WriteLine($"  Validation Errors: {validationErrors.Count}");
                ConsoleUtilities.WriteLine($"  Validation Warnings: {validationWarnings.Count}");

                // Log errors if any
                if (validationErrors.Any())
                {
                    ConsoleUtilities.WriteLine("[ERROR] Critical D1 vs M1 data mismatches detected:");
                    foreach (var error in validationErrors.Take(10)) ConsoleUtilities.WriteLine($"  ❌ {error}");
                    if (validationErrors.Count > 10)
                        ConsoleUtilities.WriteLine($"  ... and {validationErrors.Count - 10} more errors");

                    // Throw exception for critical data integrity issues
                    throw new InvalidDataException(
                        $"D1 vs M1 data validation failed with {validationErrors.Count} critical mismatches. " +
                        "Daily open/close prices must match first/last minute prices for data integrity.");
                }

                // Log warnings if any
                if (validationWarnings.Any())
                {
                    ConsoleUtilities.WriteLine("[WARNING] D1 vs M1 data validation warnings:");
                    foreach (var warning in validationWarnings.Take(5)) ConsoleUtilities.WriteLine($"  ⚠️  {warning}");
                    if (validationWarnings.Count > 5)
                        ConsoleUtilities.WriteLine($"  ... and {validationWarnings.Count - 5} more warnings");
                }

                if (validationErrors.Count == 0 && validationWarnings.Count == 0)
                {
                    ConsoleUtilities.WriteLine(
                        "[SUCCESS] ✅ Perfect D1 vs M1 data alignment - all daily open/close prices match minute data!");

                    var allRecordsForMinute = new List<PriceRecord>();
                    var day = 0;
                    foreach (var keyValuePair in minutePrices)
                    {
                        ConsoleUtilities.WriteLine(day);
                        var baseDateForMinutes = baseDate.AddDays(day++);
                        baseDateForMinutes = baseDateForMinutes.AddHours(9);
                        baseDateForMinutes = baseDateForMinutes.AddMinutes(30);
                        var recordsForMinute = new PriceRecord[keyValuePair.Value.Length];

                        // Generate daily records first
                        for (var i = 0; i < keyValuePair.Value.Length; i++)
                        {
                            var date = baseDateForMinutes.AddMinutes(i);
                            var price = keyValuePair.Value[i];
                            recordsForMinute[i] = new PriceRecord(date, TimeFrame.D1, price + 1, price + 2, price - 2, price, volume: 1000,
                                wap: price, count: 1);
                            recordsForMinute[i].Debug = day.ToString();
                        }

                        allRecordsForMinute.AddRange(recordsForMinute);
                    }

                    GeneticIndividual.Prices.AddPricesBatch(allRecordsForMinute);
                }
            }
            else
            {
                ConsoleUtilities.WriteLine("[INFO] No minute data provided - skipping D1 vs M1 validation check");
            }


            return records;
        }


        /// <summary>
        ///     Validate the integrity of all loaded data
        /// </summary>
        public ValidationResult ValidateLoadedData()
        {
            var result = new ValidationResult();

            if (Records.Count == 0)
            {
                result.Warnings.Add("No price data loaded");
                return result;
            }

            // Check for duplicates
            var duplicates = Records
                .GroupBy(r => r.DateTime)
                .Where(g => g.Count() > 1)
                .ToList();

            if (duplicates.Any()) result.Warnings.Add($"Found {duplicates.Count} duplicate timestamps");

            // Check data continuity - NOW WITH WEEKEND AWARENESS
            var sortedRecords = Records.OrderBy(r => r.DateTime).ToList();
            var largeGaps = new List<(DateTime from, DateTime to, int businessDays)>();

            for (var i = 1; i < sortedRecords.Count; i++)
            {
                var fromDate = sortedRecords[i - 1].DateTime;
                var toDate = sortedRecords[i].DateTime;
                var businessDaysGap = CalculateBusinessDayGap(fromDate, toDate);

                // More than 5 business days (allows for week-long holidays)
                if (businessDaysGap > 5) largeGaps.Add((fromDate, toDate, businessDaysGap));
            }

            if (largeGaps.Any())
            {
                result.Warnings.Add($"Found {largeGaps.Count} large data gaps (> 5 business days)");
                foreach (var gap in largeGaps.Take(3))
                    result.Warnings.Add(
                        $"  Gap: {gap.from:yyyy-MM-dd} to {gap.to:yyyy-MM-dd} ({gap.businessDays} business days)");
                if (largeGaps.Count > 3) result.Warnings.Add($"  ... and {largeGaps.Count - 3} more gaps");
            }

            // NEW: Validate daily market hours coverage
            ValidateLoadedDataMarketHours(result);

            // Validate OHLC relationships
            var ohlcErrors = 0;
            foreach (var record in Records)
                if (record.High < record.Low ||
                    record.Open < record.Low || record.Open > record.High ||
                    record.Close < record.Low || record.Close > record.High)
                    ohlcErrors++;

            if (ohlcErrors > 0) result.Errors.Add($"Found {ohlcErrors} records with invalid OHLC relationships");

            result.IsValid = result.Errors.Count == 0;
            result.TotalRecords = Records.Count;
            result.FirstRecord = sortedRecords.First().DateTime;
            result.LastRecord = sortedRecords.Last().DateTime;

            return result;
        }

        /// <summary>
        ///     Validate that CSV daily data represents proper trading days
        /// </summary>
        private static void ValidateCSVMarketDaysCoverage(List<PriceRecord> records, List<string> validationWarnings)
        {
            if (records.Count == 0) return;

            // Group records by trading day (weekdays only)
            var recordsByDay = records
                .Where(r => r.DateTime.DayOfWeek != DayOfWeek.Saturday && r.DateTime.DayOfWeek != DayOfWeek.Sunday)
                .GroupBy(r => r.DateTime.Date)
                .ToList();

            var marketDayIssues = new List<string>();
            var perfectDays = 0;
            var checkedDays = 0;

            foreach (var dayGroup in recordsByDay)
            {
                var date = dayGroup.Key;
                var dayRecords = dayGroup.OrderBy(r => r.DateTime).ToList();

                if (dayRecords.Count == 0) continue;

                checkedDays++;
                var firstRecord = dayRecords.First();
                var lastRecord = dayRecords.Last();

                // Market hours in Eastern Time: 9:30 AM to 3:59 PM
                var marketOpen = date.AddHours(9).AddMinutes(30); // 9:30 AM Eastern
                var marketClose = date.AddHours(15).AddMinutes(59); // 3:59 PM Eastern

                var hasMarketOpenIssue = false;
                var hasMarketCloseIssue = false;

                // Check if first price is significantly after market open (allow 1 minute tolerance)
                if (firstRecord.DateTime > marketOpen.AddMinutes(1))
                {
                    var delayMinutes = (firstRecord.DateTime - marketOpen).TotalMinutes;
                    marketDayIssues.Add($"  {date:yyyy-MM-dd}: First price at {firstRecord.DateTime:HH:mm:ss} " +
                                        $"({delayMinutes:F1} min after 9:30 AM market open)");
                    hasMarketOpenIssue = true;
                }

                // Check if last price is significantly before market close (allow some flexibility for early close days)
                if (lastRecord.DateTime < marketClose.AddMinutes(-30)) // Allow 30 min early close
                {
                    var earlyMinutes = (marketClose - lastRecord.DateTime).TotalMinutes;
                    marketDayIssues.Add($"  {date:yyyy-MM-dd}: Last price at {lastRecord.DateTime:HH:mm:ss} " +
                                        $"({earlyMinutes:F1} min before 4:15 PM market close)");
                    hasMarketCloseIssue = true;
                }

                // Count perfect days (proper market hours coverage)
                if (!hasMarketOpenIssue && !hasMarketCloseIssue) perfectDays++;
            }

            // Report market day validation results
            if (marketDayIssues.Any())
            {
                validationWarnings.Add($"Market day coverage issues found for {marketDayIssues.Count} day(s):");
                foreach (var issue in marketDayIssues.Take(10)) // Show first 10 issues
                    validationWarnings.Add(issue);
                if (marketDayIssues.Count > 10)
                    validationWarnings.Add($"  ... and {marketDayIssues.Count - 10} more market day issues");
            }

            // Summary stats
            if (checkedDays > 0)
            {
                var marketDayCoverage = perfectDays / (double)checkedDays * 100;
                if (marketDayCoverage < 95.0) // Less than 95% perfect days
                    validationWarnings.Add(
                        $"Market day coverage: {perfectDays}/{checkedDays} days ({marketDayCoverage:F1}%) have proper 9:30 AM - 3:59 PM coverage");
                else
                    ConsoleUtilities.WriteLine(
                        $"[INFO] Excellent market day coverage: {perfectDays}/{checkedDays} days ({marketDayCoverage:F1}%)");
            }
        }

        //HACK: Make this the gold standard...
        /// <summary>
        ///     Calculate the number of business days between two dates (excluding weekends)
        ///     This ensures weekend gaps don't trigger false alarms in data validation
        /// </summary>
        private static int CalculateBusinessDayGap(DateTime fromDate, DateTime toDate)
        {
            if (fromDate >= toDate) return 0;

            var businessDays = 0;
            var currentDate = fromDate.Date.AddDays(1); // Start from day after fromDate

            while (currentDate < toDate.Date)
            {
                // Monday = 1, Sunday = 0 in DayOfWeek enum
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
                    businessDays++;
                currentDate = currentDate.AddDays(1);
            }

            return businessDays;
        }

        /// <summary>
        ///     Represents the result of a data loading operation
        /// </summary>
        public class LoadResult
        {
            public bool Success { get; set; }
            public int TotalLinesProcessed { get; set; }
            public int ValidRecordsLoaded { get; set; }
            public int SkippedRecords { get; set; }
            public ConcurrentBag<string> ValidationErrors { get; set; } = new ConcurrentBag<string>();
            public ConcurrentBag<string> ValidationWarnings { get; set; } = new ConcurrentBag<string>();
            public DateTime? FirstRecordDate { get; set; }
            public DateTime? LastRecordDate { get; set; }
            public double LoadTimeMilliseconds { get; set; }

            public double SuccessRate =>
                TotalLinesProcessed > 0 ? ValidRecordsLoaded / (double)TotalLinesProcessed * 100 : 0;

            public override string ToString()
            {
                return $"LoadResult: {ValidRecordsLoaded}/{TotalLinesProcessed} records ({SuccessRate:F1}% success), " +
                       $"{SkippedRecords} skipped, {ValidationErrors.Count} errors, {ValidationWarnings.Count} warnings, " +
                       $"Load time: {LoadTimeMilliseconds:F0}ms";
            }
        }
    }
}