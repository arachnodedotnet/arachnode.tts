using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trade.Prices2
{
    internal static class PriceRecordUtilities
    {
        /// <summary>
        ///     Converts an array of PriceRecord objects into JSON Lines format with specified timezone
        ///     Each line is a complete JSON object representing one price record
        ///     Note: PriceRecord DateTime values are always in Eastern Time
        ///     Records are sorted by Date DESC, Time ASC
        ///     Format: {"Time":"20250808 09:30:00 Eastern Standard Time","Open":634.07,"High":634.54,"Low":634.06,"Close":634.48,"Volume":7070.32,"WAP":634.255,"Count":3504}
        /// </summary>
        /// <param name="priceRecords">Array of price records to convert</param>
        /// <param name="targetTimeZone">Target timezone for time formatting (default: "Eastern Standard Time")</param>
        /// <returns>String containing JSON Lines format with one JSON object per line</returns>
        public static string ConvertPriceRecordsToJsonLines(PriceRecord[] priceRecords, string targetTimeZone = "Eastern Standard Time")
        {
            if (priceRecords == null || priceRecords.Length == 0)
            {
                return string.Empty;
            }

            // Sort by Date DESC, Time ASC
            var sortedRecords = priceRecords
                .OrderByDescending(r => r.DateTime.Date)  // Date descending (newest dates first)
                .ThenBy(r => r.DateTime.TimeOfDay)        // Time ascending (earliest times first within each date)
                .ToArray();

            var jsonLines = new List<string>();
            TimeZoneInfo targetTz;
            TimeZoneInfo sourceTz; // EST - the timezone of PriceRecord DateTime values

            try
            {
                // Get Eastern Time zone info (source timezone for all PriceRecord DateTime values)
                sourceTz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

                // Handle different target timezone formats
                targetTz = GetTimeZoneInfo(targetTimeZone);
                if (targetTz == null)
                {
                    ConsoleUtilities.WriteLine($"⚠️  Timezone '{targetTimeZone}' not found, using Eastern Standard Time");
                    targetTz = sourceTz;
                    targetTimeZone = "Eastern Standard Time";
                }
            }
            catch (TimeZoneNotFoundException ex)
            {
                ConsoleUtilities.WriteLine($"⚠️  Error loading timezone '{targetTimeZone}': {ex.Message}, using Eastern Standard Time");
                sourceTz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                targetTz = sourceTz;
                targetTimeZone = "Eastern Standard Time";
            }

            foreach (var record in sortedRecords)
            {
                try
                {
                    // Since record.DateTime is always in EST, convert from EST to target timezone
                    var convertedTime = TimeZoneInfo.ConvertTime(record.DateTime, sourceTz, targetTz);
                    var timeString = $"{convertedTime:yyyyMMdd HH:mm:ss} {targetTimeZone}";

                    // Create JSON object using anonymous type for clean serialization
                    var jsonObject = new
                    {
                        Time = timeString,
                        Open = Math.Round(record.Open, 2),
                        High = Math.Round(record.High, 2),
                        Low = Math.Round(record.Low, 2),
                        Close = Math.Round(record.Close, 2),
                        Volume = Math.Round(record.Volume, 2),
                        WAP = Math.Round(record.WAP, 3),
                        Count = record.Count
                    };

                    // Serialize to JSON without formatting (single line)
                    var jsonLine = JsonConvert.SerializeObject(jsonObject, Formatting.None);
                    jsonLines.Add(jsonLine);
                }
                catch (Exception ex)
                {
                    ConsoleUtilities.WriteLine($"❌ Error converting price record at {record.DateTime}: {ex.Message}");
                }
            }

            ConsoleUtilities.WriteLine($"✅ Converted {jsonLines.Count}/{priceRecords.Length} price records to JSON Lines format (sorted by Date DESC, Time ASC)");
            return string.Join(Environment.NewLine, jsonLines);
        }

        /// <summary>
        ///     Converts an array of PriceRecord objects to JSON Lines format and writes to file
        ///     Each line in the file is a complete JSON object representing one price record
        ///     Note: PriceRecord DateTime values are always in Eastern Time
        /// </summary>
        /// <param name="priceRecords">Array of price records to convert</param>
        /// <param name="outputPath">Path where to write the JSON Lines file</param>
        /// <param name="targetTimeZone">Target timezone for time formatting (default: "Eastern Standard Time")</param>
        /// <returns>True if file was written successfully, false otherwise</returns>
        public static bool WritePriceRecordsToJsonLinesFile(PriceRecord[] priceRecords, string outputPath, string targetTimeZone = "Eastern Standard Time")
        {
            try
            {
                var jsonLinesContent = ConvertPriceRecordsToJsonLines(priceRecords, targetTimeZone);

                if (string.IsNullOrEmpty(jsonLinesContent))
                {
                    ConsoleUtilities.WriteLine("⚠️  No content to write - price records array is empty");
                    return false;
                }

                // Ensure directory exists
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(outputPath, jsonLinesContent);

                var fileInfo = new FileInfo(outputPath);
                ConsoleUtilities.WriteLine($"✅ JSON Lines file written successfully:");
                ConsoleUtilities.WriteLine($"   📁 File: {outputPath}");
                ConsoleUtilities.WriteLine($"   📊 Records: {priceRecords.Length:N0}");
                ConsoleUtilities.WriteLine($"   📏 Size: {fileInfo.Length:N0} bytes");
                ConsoleUtilities.WriteLine($"   🕒 Timezone: {targetTimeZone}");

                return true;
            }
            catch (Exception ex)
            {
                ConsoleUtilities.WriteLine($"❌ Error writing JSON Lines file to {outputPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        ///     Map timezone strings to .NET TimeZoneInfo
        ///     Handles common timezone formats and aliases
        /// </summary>
        /// <param name="timezoneString">Timezone string to map</param>
        /// <returns>TimeZoneInfo object or null if not found</returns>
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
                    case "hawaiian standard time":
                        // Hawaiian Standard Time - NO DST, always UTC-10
                        return TimeZoneInfo.FindSystemTimeZoneById("Hawaiian Standard Time");

                    case "us/pacific":
                    case "pacific/los_angeles":
                    case "pst":
                    case "pdt":
                    case "pacific":
                    case "pacific standard time":
                        return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

                    case "us/mountain":
                    case "america/denver":
                    case "mst":
                    case "mdt":
                    case "mountain":
                    case "mountain standard time":
                        return TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");

                    case "us/central":
                    case "america/chicago":
                    case "cst":
                    case "cdt":
                    case "central":
                    case "central standard time":
                        return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

                    case "us/eastern":
                    case "america/new_york":
                    case "est":
                    case "edt":
                    case "eastern":
                    case "eastern standard time":
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
    }
}
