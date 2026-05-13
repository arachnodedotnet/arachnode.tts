using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trade.Polygon2;

namespace Trade.IVPreCalc2
{
    internal sealed class BulkFileContractTracer : IDisposable
    {
        private readonly string _bulkDataDirectory;
        private readonly Dictionary<string, BulkFilePointer> _openFiles;
        private readonly List<string> _bulkFilesSortedNewestFirst;

        public BulkFileContractTracer(string bulkDataDirectory)
        {
            _bulkDataDirectory = bulkDataDirectory;
            _openFiles = new Dictionary<string, BulkFilePointer>(StringComparer.OrdinalIgnoreCase);
            _bulkFilesSortedNewestFirst = GetBulkFilesByDateDesc();
            OpenAllFilePointers();
        }

        public DateTime GetNewestBulkFileDateOrDefault(DateTime fallback)
        {
            foreach (var f in _bulkFilesSortedNewestFirst)
            {
                var d = TryExtractDate(f);
                if (d.HasValue) return d.Value;
            }
            return fallback.Date;
        }

        private List<string> GetBulkFilesByDateDesc()
        {
            var files = Directory.GetFiles(_bulkDataDirectory, "*.csv", SearchOption.AllDirectories)
                .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(f => new { Path = f, Date = TryExtractDate(f) })
                .Where(x => x.Date.HasValue)
                .OrderByDescending(x => x.Date.Value)
                .Select(x => x.Path)
                .ToList();

            return files;
        }

        private static DateTime? TryExtractDate(string filePath)
        {
            var name = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrEmpty(name) || name.Length < 10) return null;
            var datePart = name.Substring(0, 10);
            if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d.Date;
            return null;
        }

        private void OpenAllFilePointers()
        {
            foreach (var filePath in _bulkFilesSortedNewestFirst)
            {
                try
                {
                    var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 65536);
                    
                    // Find first newline in a small buffer to get exact header end position
                    var buffer = new byte[1024];
                    int bytesRead = fs.Read(buffer, 0, buffer.Length);
                    fs.Seek(0, SeekOrigin.Begin); // Reset to beginning
                    
                    long dataStart = 0;
                    int eolBytes = 2; // Default CRLF
                    
                    for (int i = 0; i < bytesRead; i++)
                    {
                        if (buffer[i] == '\n')
                        {
                            dataStart = i + 1;
                            eolBytes = (i > 0 && buffer[i-1] == '\r') ? 2 : 1;
                            break;
                        }
                    }
                    
                    // Extract header from buffer
                    var headerBytes = new byte[dataStart - eolBytes];
                    Array.Copy(buffer, 0, headerBytes, 0, headerBytes.Length);
                    var header = Encoding.UTF8.GetString(headerBytes);
                    
                    // Position StreamReader at exact data start position
                    fs.Seek(dataStart, SeekOrigin.Begin);
                    var sr = new StreamReader(fs);
                    var info = new FileInfo(filePath);

                    var ptr = new BulkFilePointer
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        Stream = fs,
                        Reader = sr,
                        FileSize = info.Length,
                        DataStartPosition = dataStart, // Exact position calculated from buffer
                        CurrentPosition = dataStart,
                        CurrentLineNumber = 2, // Line 2 = first data line (after header which is line 1)
                        Header = header,
                        EolBytes = eolBytes // Detected line ending type
                    };

                    // Key by full path to avoid name collisions across directories
                    _openFiles[ptr.FilePath] = ptr;
                }
                catch
                {
                    // skip file if it cannot be opened
                }
            }
        }

        /// <summary>
        /// Gets the exact line number for a given position in the file.
        /// Uses the tracked line numbers for accuracy.
        /// </summary>
        private static long GetExactLineNumber(BulkFilePointer fp, long position)
        {
            // If we're at or before data start, we're at line 1 (header)
            if (position <= fp.DataStartPosition) return 1;
            
            // If the position matches current position, return current line number
            if (position == fp.CurrentPosition) return fp.CurrentLineNumber;
            
            // For other positions, we'd need to track more precisely, but for now
            // return the current line number as it's the most accurate we have
            return fp.CurrentLineNumber;
        }

        public async Task<ContractPriceHistory> TraceContractBackwardsAsync(string contractSymbol, DateTime startDate, DateTime endDate)
        {
            // Aggregate minute records by date across all files
            var byDate = new Dictionary<DateTime, List<ContractPriceRecord>>();
            var stats = new ContractSearchStats();
            var targetUpper = contractSymbol.ToUpperInvariant();

            // Track earliest (chronologically) and latest file where the contract appears
            DateTime? earliestDateFound = null;
            string earliestFile = null;
            DateTime? latestDateFound = null;
            string latestFile = null;

            // Track the last file searched regardless of whether it had matches
            string lastFileSearched = null;

            int fileIndex = 0; // Track which file we're processing (0 = newest)
            int totalFilesInRange = _bulkFilesSortedNewestFirst.Count(f => {
                var dateOpt = TryExtractDate(f);
                return dateOpt.HasValue && dateOpt.Value >= startDate.Date && dateOpt.Value <= endDate.Date;
            });

            foreach (var filePath in _bulkFilesSortedNewestFirst)
            {
                var fileDateOpt = TryExtractDate(filePath);
                if (!fileDateOpt.HasValue) continue;

                var fileDate = fileDateOpt.Value;
                if (fileDate < startDate.Date || fileDate > endDate.Date)
                    continue;

                fileIndex++; // Increment for files within date range

                // Lookup pointer by full path
                if (!_openFiles.TryGetValue(filePath, out var fp)) continue;

                // Track this as the last file we actually searched
                lastFileSearched = filePath;

                // Track starting line number before search
                long startLineNumber = fp.CurrentLineNumber;

                var minuteRecs = await SearchContractInFileAsync(fp, targetUpper).ConfigureAwait(false);
                //var minuteRecs = new List<ContractPriceRecord>();

                // Track ending line number after search
                long endLineNumber = fp.CurrentLineNumber;

                // Log file processing progress on single line with exact line numbers
                ConsoleUtilities.WriteLine($"File {fileIndex}/{totalFilesInRange}: {Path.GetFileName(filePath)} ({fileDate:yyyy-MM-dd}) | " +
                                $"Lines {startLineNumber}-{endLineNumber} | Found: {minuteRecs.Count} records | " +
                                $"Contract: {contractSymbol} | LookaheadLine: ${ExtractTicker(fp.LookaheadLine)}");

                if(minuteRecs.Count == 0)
                {
                    break;
                }

                if (minuteRecs.Count > 0)
                {
                    stats.FilesWithData++;
                    stats.TotalRecordsFound += minuteRecs.Count;

                    // Update earliest/latest by file date
                    if (!earliestDateFound.HasValue || fileDate < earliestDateFound.Value)
                    {
                        earliestDateFound = fileDate;
                        earliestFile = filePath;
                    }
                    if (!latestDateFound.HasValue || fileDate > latestDateFound.Value)
                    {
                        latestDateFound = fileDate;
                        latestFile = filePath;
                    }

                    var key = fileDate.Date;
                    if (!byDate.TryGetValue(key, out var list))
                    {
                        list = new List<ContractPriceRecord>();
                        byDate[key] = list;
                    }
                    list.AddRange(minuteRecs);
                }
                else
                {
                    if (stats.FilesWithData > 0)
                        break; // we've moved past the contract's lifecycle
                }
            }

            var priceHistory = byDate
                .Select(kvp => CreateDailySummary(kvp.Value, kvp.Key))
                .Where(d => d != null)
                .OrderBy(d => d.Date)
                .ToList();

            return new ContractPriceHistory
            {
                ContractSymbol = contractSymbol,
                Prices = priceHistory,
                FilesSearched = stats.FilesWithData,
                TotalFilesAvailable = _openFiles.Count,
                SearchStats = stats,
                // First = earliest chronologically, Last = latest chronologically
                FirstSourceFile = earliestFile,
                LastSourceFile = latestFile,
                LastFileSearched = lastFileSearched
            };
        }

        // Extracts ticker (first column) uppercased or null
        private static string ExtractTicker(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            var comma = line.IndexOf(',');
            if (comma <= 0) return null;
            return line.Substring(0, comma).Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Parses a contract ticker into a ContractKey for comparison purposes.
        /// Returns null if the ticker cannot be parsed as a valid option contract.
        /// </summary>
        internal static ContractKey ParseContractKey(string ticker)
        {
            if (string.IsNullOrEmpty(ticker) || ticker.Length < 15) return null;

            try
            {
                // Expected format: O:UNDERLYING[YY]MMDD[C/P][STRIKE*1000]
                // Example: O:SPY250321C00400000, O:BABA1251219C00070000
                if (!ticker.StartsWith("O:")) return null;

                var contractPart = ticker.Substring(2); // Remove "O:" prefix

                // Find the date part (YYMMDD) by looking for the complete valid suffix pattern
                // We need exactly 15 characters: YYMMDD (6) + C/P (1) + STRIKE (8) = 15 chars
                int dateStart = -1;
                for (int i = 1; i <= contractPart.Length - 15; i++) // Need exactly 15 chars for complete suffix
                {
                    if (i + 15 <= contractPart.Length &&
                        contractPart.Substring(i, 6).All(char.IsDigit) && // 6-digit date (YYMMDD)
                        (contractPart[i + 6] == 'C' || contractPart[i + 6] == 'P') && // Option type
                        contractPart.Substring(i + 7, 8).All(char.IsDigit)) // 8-digit strike
                    {
                        dateStart = i;
                        break;
                    }
                }

                if (dateStart == -1) return null;

                var underlying = contractPart.Substring(0, dateStart);
                var dateStr = contractPart.Substring(dateStart, 6);
                var typeAndStrike = contractPart.Substring(dateStart + 6);

                if (typeAndStrike.Length < 9) return null; // Need C/P + 8 digit strike

                var optionType = typeAndStrike[0];
                var strikeStr = typeAndStrike.Substring(1);

                if (optionType != 'C' && optionType != 'P') return null;
                if (!strikeStr.All(char.IsDigit) || strikeStr.Length != 8) return null;

                // Parse date (YYMMDD)
                if (!int.TryParse(dateStr.Substring(0, 2), out var year) ||
                    !int.TryParse(dateStr.Substring(2, 2), out var month) ||
                    !int.TryParse(dateStr.Substring(4, 2), out var day))
                    return null;

                // Convert 2-digit year to 4-digit (assume 20xx for years 00-99)
                var fullYear = 2000 + year;
                var expiration = new DateTime(fullYear, month, day);

                // Parse strike (divide by 1000)
                if (!long.TryParse(strikeStr, out var strikeThousandths))
                    return null;

                var strike = strikeThousandths / 1000.0;

                return new ContractKey
                {
                    Underlying = underlying,
                    IsCall = optionType == 'C',
                    Expiration = expiration,
                    Strike = strike,
                    RawTicker = ticker
                };
            }
            catch
            {
                return null;
            }
        }

        private async Task<List<ContractPriceRecord>> SearchContractInFileAsync(BulkFilePointer fp, string targetUpper)
        {
            var results = new List<ContractPriceRecord>();
            var reader = fp.Reader;
            var enc = reader.CurrentEncoding;
            var rthCutoff = new TimeSpan(16, 15, 0);
            var easternTz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            int eolBytes = fp.EolBytes > 0 ? fp.EolBytes : 1;

            // Parse target contract for contract-aware comparison
            var targetContractKey = ParseContractKey(targetUpper);
            
            // Determine starting position and line number, prepare the reader alignment
            long pos;
            long lineNumber;
            string line = null;

            // If we have a cached lookahead line and it belongs to this target, consume it first
            if (!string.IsNullOrEmpty(fp.LookaheadLine) /*&& string.Equals(ExtractTicker(fp.LookaheadLine), targetUpper, StringComparison.Ordinal)*/)
            {
                // Start offsets from cached values
                pos = fp.LookaheadLineStart;
                lineNumber = fp.LookaheadLineNumber;
                line = fp.LookaheadLine;

                // Align reader to continue after the cached line
                try { reader.DiscardBufferedData(); } catch { }
                reader.BaseStream.Seek(fp.LookaheadNextStart, SeekOrigin.Begin);
                fp.CurrentPosition = fp.LookaheadNextStart;
                fp.CurrentLineNumber = fp.LookaheadLineNumber + 1; // Next line after lookahead

                // Clear the lookahead so it is consumed
                fp.LookaheadLine = null;
            }
            else
            {
                // Use cached position and line number if available
                pos = fp.LastKnownPositions.TryGetValue(targetUpper, out var cachedPos) ? cachedPos : fp.DataStartPosition;
                lineNumber = fp.LastKnownLineNumbers.TryGetValue(targetUpper, out var cachedLine) ? cachedLine : 2; // Start after header
                
                try { reader.DiscardBufferedData(); } catch { }
                reader.BaseStream.Seek(pos, SeekOrigin.Begin);
                fp.CurrentPosition = pos;
                fp.CurrentLineNumber = lineNumber;
            }

            bool seenAny = false;

            while (true)
            {
                if (line == null)
                {
                    line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) break; // EOF
                    lineNumber++; // Increment line number for each line read
                }

                long lineStart = pos;
                long nextLineStart = pos + enc.GetByteCount(line) + eolBytes;

                if (!string.IsNullOrWhiteSpace(line))
                {
                    var parts = line.Split(',');
                    if (parts.Length >= 8)
                    {
                        var ticker = parts[0].Trim().ToUpperInvariant();
                        var currentContractKey = ParseContractKey(ticker);
                        int cmp = 0;

                        if (targetContractKey != null && currentContractKey != null)
                        {
                            // Use proper contract comparison
                            cmp = ContractKeyComparer.Instance.Compare(currentContractKey, targetContractKey);
                        }
                        else
                        {
                            // Fallback to string comparison only if contract parsing fails
                            cmp = string.CompareOrdinal(ticker, targetUpper);
                        }

                        var optionDeleteMe = Ticker.ParseToOption(ticker);

                        if(optionDeleteMe.OptionType == OptionType.Put)
                        {

                        }

                        // Remember the first byte-offset and line number where a ticker appears
                        if (!fp.LastKnownPositions.ContainsKey(ticker))
                        {
                            fp.LastKnownPositions[ticker] = lineStart;
                            fp.LastKnownLineNumbers[ticker] = lineNumber;
                        }

                        if (cmp == 0)
                        {
                            seenAny = true;

                            // parts: ticker,volume,open,close,high,low,window_start,transactions
                            if (int.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var volume) &&
                                double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out var open) &&
                                double.TryParse(parts[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var close) &&
                                double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var high) &&
                                double.TryParse(parts[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var low) &&
                                long.TryParse(parts[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var windowStartNanos) &&
                                int.TryParse(parts[7], NumberStyles.Any, CultureInfo.InvariantCulture, out var transactions))
                            {
                                var utc = DateTimeOffset.FromUnixTimeMilliseconds(windowStartNanos / 1_000_000).UtcDateTime;
                                var est = TimeZoneInfo.ConvertTimeFromUtc(utc, easternTz);
                                if (est.TimeOfDay < rthCutoff)
                                {
                                    results.Add(new ContractPriceRecord
                                    {
                                        Timestamp = est,
                                        Open = open,
                                        High = high,
                                        Low = low,
                                        Close = close,
                                        Volume = volume,
                                        Transactions = transactions,
                                        SourceFile = fp.FilePath
                                    });
                                }
                            }
                        }
                        else if (cmp > 0)
                        {
                            // String comparison indicates we've passed target
                            // But also check contract-aware comparison if possible
                            bool shouldCutBait = seenAny; // Default behavior: cut bait if we've seen matches

                            if (targetContractKey != null)
                            {
                                //var currentContractKey2 = ParseContractKey(ticker);
                                if (currentContractKey != null)
                                {
                                    int contractCmp = ContractKeyComparer.Instance.Compare(currentContractKey, targetContractKey);

                                    // If current contract sorts AFTER target contract, we should cut bait
                                    // regardless of whether we've seen any matches yet
                                    if (contractCmp > 0)
                                    {
                                        shouldCutBait = true;
                                    }
                                    // If current contract sorts BEFORE target contract, continue searching
                                    // (our target might still be ahead in this sorted file)
                                    else if (contractCmp < 0)
                                    {
                                        shouldCutBait = false; // Keep searching
                                    }
                                }
                            }

                            if (!shouldCutBait)
                            {
                                var option1 = Ticker.ParseToOption(ticker);
                                var option2 = Ticker.ParseToOption(targetUpper);
                                
                                if (option1.OptionType != option2.OptionType)
                                {
                                    shouldCutBait = true;
                                }
                            }

                            if (shouldCutBait)
                            {
                                return CutBait(fp, targetUpper, results, reader, lineNumber, line, lineStart, nextLineStart, ticker);
                            }
                        }
                        else
                        {
                            var option1 = Ticker.ParseToOption(targetUpper);
                            var option2 = Ticker.ParseToOption(ticker);

                            if ((false && option1.UnderlyingSymbol != option2.UnderlyingSymbol) ||
                                (option1.UnderlyingSymbol == option2.UnderlyingSymbol && option1.OptionType == OptionType.Call && option2.OptionType == OptionType.Put))
                            {
                                return CutBait(fp, targetUpper, results, reader, lineNumber, line, lineStart, nextLineStart, ticker);
                            }
                        }
                    }
                }

                // Advance to next line
                pos = nextLineStart;
                fp.CurrentPosition = pos;
                fp.CurrentLineNumber = lineNumber;
                line = null;
            }

            // EOF or end of scan - update final position and line number
            fp.LastKnownPositions[targetUpper] = pos;
            fp.LastKnownLineNumbers[targetUpper] = lineNumber;
            fp.CurrentPosition = pos;
            fp.CurrentLineNumber = lineNumber;
            return results;
        }

        private static List<ContractPriceRecord> CutBait(BulkFilePointer fp, string targetUpper, List<ContractPriceRecord> results, StreamReader reader, long lineNumber, string line, long lineStart, long nextLineStart, string ticker)
        {
            // Cache this next ticker's line so the next search can start without rereading
            fp.LookaheadLine = line;
            fp.LookaheadLineStart = lineStart;
            fp.LookaheadLineNumber = lineNumber;
            fp.LookaheadNextStart = nextLineStart;

            // Also cache resume for this next ticker at its first line
            fp.LastKnownPositions[ticker] = lineStart;
            fp.LastKnownLineNumbers[ticker] = lineNumber - 1;

            // Position stream at the next line start for subsequent reads
            try { reader.DiscardBufferedData(); } catch { }
            reader.BaseStream.Seek(nextLineStart, SeekOrigin.Begin);
            fp.CurrentPosition = nextLineStart;
            fp.CurrentLineNumber = lineNumber - 1;

            // Update target resume position and line number
            fp.LastKnownPositions[targetUpper] = nextLineStart;
            fp.LastKnownLineNumbers[targetUpper] = lineNumber - 1;
            return results;
        }

        // Finds the start-of-line at or before nearPos by scanning a limited window backwards.
        // Assumes ASCII/UTF-8 CSV with LF or CRLF line endings.
        private static long FindLineStartBackward(FileStream stream, long nearPos, long minPos)
        {
            const int window = 64 * 1024; // window size to search for the prior newline
            long original = stream.Position;

            long scanStart = Math.Max(minPos, nearPos - window);
            int toRead = (int)Math.Max(0, nearPos - scanStart);
            if (toRead == 0)
            {
                // We are at the very beginning of the data segment
                return minPos;
            }

            var buf = new byte[toRead];
            stream.Seek(scanStart, SeekOrigin.Begin);
            int read = stream.Read(buf, 0, toRead);

            int lastLf = -1;
            for (int i = 0; i < read; i++)
            {
                if (buf[i] == (byte)"\n"[0]) lastLf = i;
            }

            long lineStart = scanStart + (lastLf >= 0 ? lastLf + 1 : 0);
            stream.Seek(original, SeekOrigin.Begin);
            return lineStart;
        }

        private static DailyContractPrice CreateDailySummary(List<ContractPriceRecord> records, DateTime date)
        {
            if (records == null || records.Count == 0) return null;
            records.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

            return new DailyContractPrice
            {
                Date = date.Date,
                Open = records.First().Open,
                High = records.Max(r => r.High),
                Low = records.Min(r => r.Low),
                Close = records.Last().Close,
                Volume = records.Sum(r => (double)r.Volume),
                RecordCount = records.Count,
                FirstTimestamp = records.First().Timestamp,
                LastTimestamp = records.Last().Timestamp,
                SourceFiles = records.Select(r => r.SourceFile).Distinct().ToList(),
                ClosePrices = records.Select(r => r.Close).ToList(),
                FirstSourceFile = records.First().SourceFile,
                LastSourceFile = records.Last().SourceFile
            };
        }

        public void Dispose()
        {
            foreach (var fp in _openFiles.Values)
            {
                try { fp.Reader?.Dispose(); } catch { }
                try { fp.Stream?.Dispose(); } catch { }
            }
            _openFiles.Clear();
        }
    }
}