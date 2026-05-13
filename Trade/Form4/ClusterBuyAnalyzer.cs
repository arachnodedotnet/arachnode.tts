using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using Newtonsoft.Json;

namespace Trade.Form4
{
    /// <summary>
    /// Represents a cluster entry in the buying cache (30-day rolling window)
    /// Ported from TypeScript ClusterBuyAnalyzer v2.0
    /// </summary>
    public class ClusterEntry
    {
        [JsonProperty("ts")]
        public string Timestamp { get; set; } // ISO timestamp

        [JsonProperty("value")]
        public decimal Value { get; set; } // Purchase value

        [JsonProperty("owners")]
        public List<string> Owners { get; set; }

        [JsonProperty("cik")]
        public string CIK { get; set; }

        [JsonProperty("period")]
        public string Period { get; set; }

        [JsonProperty("link")]
        public string Link { get; set; }

        [JsonProperty("roles")]
        public List<string> Roles { get; set; }

        public ClusterEntry()
        {
            Owners = new List<string>();
            Roles = new List<string>();
        }
    }

    /// <summary>
    /// Cluster metrics with scoring results
    /// Ported from TypeScript ClusterBuyAnalyzer v2.0
    /// </summary>
    public class ClusterMetrics
    {
        public string Ticker { get; set; }
        public string Timestamp { get; set; }
        public decimal Value { get; set; }
        public List<string> Owners { get; set; }
        public string CIK { get; set; }
        public string Period { get; set; }
        public string Link { get; set; }
        
        public decimal TotalClusterValue { get; set; }
        public int TotalClusterCount { get; set; }
        public decimal Last7dValue { get; set; }
        public int Last7dCount { get; set; }
        
        public int Score { get; set; }
        public string Tier { get; set; } // "A+", "A", "B", "C"
        public List<string> Roles { get; set; }
        public int DistinctInsiders { get; set; }

        public ClusterMetrics()
        {
            Owners = new List<string>();
            Roles = new List<string>();
        }

        public override string ToString()
        {
            return $"{Ticker} | Tier: {Tier} | Score: {Score} | " +
                   $"30d: ${TotalClusterValue:N0} ({TotalClusterCount} filings) | " +
                   $"7d: ${Last7dValue:N0} ({Last7dCount} filings) | " +
                   $"Insiders: {DistinctInsiders} | Roles: {string.Join(", ", Roles.Take(3))}";
        }
    }

    /// <summary>
    /// Institutional-grade insider cluster scoring for Form 4 signals
    /// Complete C# port of TypeScript ClusterBuyAnalyzer v2.0
    /// </summary>
    public static class ClusterBuyAnalyzer
    {
        private const string CACHE_FILENAME = "cluster-buying-cache.json";
        private static readonly TimeSpan THIRTY_DAYS = TimeSpan.FromDays(30);
        private static readonly TimeSpan SEVEN_DAYS = TimeSpan.FromDays(7);

        /// <summary>
        /// Role weighting multipliers (institutional)
        /// </summary>
        public static double GetRoleWeight(string role)
        {
            if (string.IsNullOrEmpty(role))
                return 1.0;

            var r = role.ToLowerInvariant();
            
            if (r.Contains("chief executive") || r.Contains("ceo"))
                return 4.0;
            if (r.Contains("chief financial") || r.Contains("cfo"))
                return 3.5;
            if (r.Contains("chief operating") || r.Contains("coo"))
                return 3.0;
            if (r.Contains("general counsel") || r.Contains("gc"))
                return 2.5;
            if (r.Contains("director"))
                return 1.25;
            
            return 1.0;
        }

        /// <summary>
        /// Load cluster cache from disk
        /// </summary>
        private static Dictionary<string, List<ClusterEntry>> LoadCache(string baseDir)
        {
            var cacheFile = Path.Combine(baseDir, CACHE_FILENAME);
            
            try
            {
                if (File.Exists(cacheFile))
                {
                    var json = File.ReadAllText(cacheFile);
                    var cache = JsonConvert.DeserializeObject<Dictionary<string, List<ClusterEntry>>>(json);
                    
                    if (cache != null)
                        return cache;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ClusterBuyAnalyzer: Failed reading cache: {ex.Message}");
            }

            return new Dictionary<string, List<ClusterEntry>>();
        }

        /// <summary>
        /// Save cluster cache to disk
        /// </summary>
        private static void SaveCache(string baseDir, Dictionary<string, List<ClusterEntry>> cache)
        {
            var cacheFile = Path.Combine(baseDir, CACHE_FILENAME);
            
            try
            {
                var json = JsonConvert.SerializeObject(cache, Formatting.Indented);
                File.WriteAllText(cacheFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ClusterBuyAnalyzer: Failed writing cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Main update-and-score function (ported from TypeScript)
        /// </summary>
        public static ClusterMetrics UpdateAndScore(
            string baseDir,
            string ticker,
            decimal aggregatePurchaseValue,
            List<string> ownerNames,
            List<string> roles,
            string cik = null,
            string period = null,
            string link = null,
            double[] priceHistory5d = null,
            double? marketCapOverride = null)
        {
            var cache = LoadCache(baseDir);
            var key = (ticker ?? "UNKNOWN").ToUpperInvariant();
            var nowIso = DateTime.UtcNow.ToString("o"); // ISO 8601

            // Match TS behavior:
            // - owners.map(o => o.name).filter(Boolean)
            // - roles is treated as an array
            var ownersNames = (ownerNames ?? new List<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
            var rolesList = (roles ?? new List<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList();
            
            // Create new cluster entry
            var newEntry = new ClusterEntry
            {
                Timestamp = nowIso,
                Value = aggregatePurchaseValue,
                Owners = ownersNames,
                CIK = cik,
                Period = period,
                Link = link,
                Roles = rolesList
            };

            // Initialize ticker bucket if needed
            if (!cache.ContainsKey(key))
                cache[key] = new List<ClusterEntry>();

            cache[key].Add(newEntry);

            // Prune entries older than 30 days
            var now = DateTime.UtcNow;
            cache[key] = cache[key]
                .Where(e => now - DateTimeOffset.Parse(e.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).UtcDateTime < THIRTY_DAYS)
                .ToList();

            // Calculate metrics
            var last7Entries = cache[key]
                .Where(e => now - DateTimeOffset.Parse(e.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).UtcDateTime < SEVEN_DAYS)
                .ToList();

            var totalClusterValue = cache[key].Sum(e => e.Value);
            var totalClusterCount = cache[key].Count;
            var last7dValue = last7Entries.Sum(e => e.Value);
            var last7dCount = last7Entries.Count;

            // =============================
            // SCORING — institutional v2.0
            // =============================

            // 1. Purchase value – log scaling
            var valueFactor = Math.Min(60.0, 
                Math.Log10(Math.Max(1.0, (double)aggregatePurchaseValue)) * 15.0);

            // 2. Cluster scale – 30-day rolling value
            var clusterFactor = Math.Min(50.0, 
                Math.Log10(Math.Max(1.0, (double)totalClusterValue)) * 12.0);

            // 3. Acceleration (new buy vs recent average)
            var recentAccel = last7dValue > 0
                ? (double)aggregatePurchaseValue / Math.Max(1.0, (double)last7dValue / Math.Max(1.0, last7dCount))
                : (double)aggregatePurchaseValue;
            var accelFactor = Math.Min(30.0, Math.Log(Math.Max(1.0, recentAccel), 2) * 10.0);

            // 4. Distinct insiders
            var distinctInsiders = ownersNames.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            var insiderFactor = Math.Min(30.0, distinctInsiders * 8.0);

            // 5. Role scoring
            var roleFactor = Math.Min(60.0, 
                rolesList.Sum(r => GetRoleWeight(r)) * 10.0);

            // 6. Market-cap normalization (placeholder - can integrate later)
            var mcapFactor = 0.0;
            if (marketCapOverride.HasValue && marketCapOverride.Value > 0)
            {
                var pct = (double)aggregatePurchaseValue / marketCapOverride.Value;
                mcapFactor = Math.Min(40.0, Math.Log10(1.0 + pct * 1e6));
            }

            // 7. Dip-buy detection (>10% drop in 5 days)
            var dipBuyFactor = 0.0;
            if (priceHistory5d != null && priceHistory5d.Length >= 2)
            {
                var start = priceHistory5d[0];
                var end = priceHistory5d[priceHistory5d.Length - 1];
                if (start > 0)
                {
                    var drop = (start - end) / start;
                    if (drop >= 0.1) // 10%+ drop
                        dipBuyFactor = 25.0; // highly predictive
                }
            }

            // 8. Institutional shadow (placeholder for future)
            var institutionalFactor = 0.0;

            // Final score
            var score = (int)Math.Round(
                valueFactor + clusterFactor + accelFactor + insiderFactor +
                roleFactor + mcapFactor + dipBuyFactor + institutionalFactor);

            // =============================
            // TIERING — institutional
            // =============================
            string tier;
            if (score >= 200 && distinctInsiders >= 3 && roleFactor > 40)
                tier = "A+";
            else if (score >= 140)
                tier = "A";
            else if (score >= 90)
                tier = "B";
            else
                tier = "C";

            // Save updated cache
            SaveCache(baseDir, cache);

            // Return metrics
            return new ClusterMetrics
            {
                Ticker = key,
                Timestamp = nowIso,
                Value = aggregatePurchaseValue,
                Owners = ownersNames,
                CIK = cik,
                Period = period,
                Link = link,
                TotalClusterValue = totalClusterValue,
                TotalClusterCount = totalClusterCount,
                Last7dValue = last7dValue,
                Last7dCount = last7dCount,
                Score = score,
                Tier = tier,
                Roles = rolesList,
                DistinctInsiders = distinctInsiders
            };
        }

        /// <summary>
        /// Format special instructions for Service Bus / Telegram (ported from TypeScript)
        /// </summary>
        public static List<string> ToSpecialInstructions(string baseDir, ClusterMetrics metrics)
        {
            var instructions = new List<string>();
            
            // Collect all links from cache for this ticker
            var allLinks = new HashSet<string>();
            try
            {
                var cache = LoadCache(baseDir);
                if (cache.TryGetValue(metrics.Ticker, out var value))
                {
                    allLinks = new HashSet<string>(value
                        .Where(e => !string.IsNullOrEmpty(e.Link))
                        .Select(e => e.Link));
                }
            }
            catch
            {
                // Ignore errors
            }

            instructions.Add($"clusterScore|{metrics.Score}");
            instructions.Add($"clusterTier|{metrics.Tier}");
            instructions.Add($"clusterTotalValue|{metrics.TotalClusterValue}");
            instructions.Add($"clusterTotalCount|{metrics.TotalClusterCount}");
            instructions.Add($"cluster7dValue|{metrics.Last7dValue}");
            instructions.Add($"cluster7dCount|{metrics.Last7dCount}");
            instructions.Add($"clusterDistinctInsiders|{(metrics.Owners?.Count ?? 0)}");
            instructions.Add($"clusterRoles|{string.Join(";", metrics.Roles ?? new List<string>())}");
            
            if (allLinks.Count > 0)
                instructions.Add($"clusterLinks|{string.Join(" ", allLinks)}");

            return instructions;
        }
    }
}
