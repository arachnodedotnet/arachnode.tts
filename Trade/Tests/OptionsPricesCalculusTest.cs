using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Trade.IVPreCalc2;
using Trade.Polygon2;

namespace Trade.Tests
{
    /// <summary>
    /// Exploratory tests using information-theoretic style (entropy) measures to guide
    /// optimal key ordering and compression strategies for options bulk files.
    /// These tests DO NOT enforce production behavior – they surface insights.
    /// </summary>
    [TestClass]
    public class OptionsPricesCalculusTest
    {
        private const int MAX_SAMPLE_LINES = 500_000; // Safety cap for very large bulk files
        private const string OPTION_PREFIX = "O:";

        private static string _bulkDir;
        private static string _mostRecentFile;

        [ClassInitialize]
        public static void ClassInit(TestContext ctx)
        {
            _bulkDir = IVPreCalc.ResolveBulkDir();
            if (!string.IsNullOrEmpty(_bulkDir) && Directory.Exists(_bulkDir))
            {
                _mostRecentFile = Directory.GetFiles(_bulkDir, "*.csv", SearchOption.AllDirectories)
                    .Where(f => f.IndexOf("options", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(f => new { Path = f, Date = IVPreCalc.TryExtractDateFromName(f) })
                    .Where(x => x.Date.HasValue)
                    .OrderByDescending(x => x.Date.Value)
                    .Select(x => x.Path)
                    .FirstOrDefault();
            }
        }

        [TestMethod]
        [TestCategory("Core")]
        public void FindMostRecentOptionsBulkFile()
        {
            if (string.IsNullOrEmpty(_bulkDir) || !Directory.Exists(_bulkDir))
                Assert.Inconclusive("Bulk directory not found.");

            if (string.IsNullOrEmpty(_mostRecentFile) || !File.Exists(_mostRecentFile))
                Assert.Inconclusive("No recent options bulk file found.");

            var fi = new FileInfo(_mostRecentFile);
            Console.WriteLine($"Most Recent Bulk File: {fi.Name}  Size={fi.Length / (1024.0 * 1024.0):F2}MB  ModifiedUTC={fi.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss}");
            Assert.IsTrue(fi.Length > 0, "File should not be empty.");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void AnalyzeKeyDistributions_Entropy()
        {
            RequireMostRecent();

            var stats = CollectKeyStats(_mostRecentFile, MAX_SAMPLE_LINES);

            Console.WriteLine("=== Key Cardinalities (Sample) ===");
            Console.WriteLine($"Contracts (distinct full option symbols): {stats.DistinctContracts:N0}");
            Console.WriteLine($"Underlyings: {stats.UnderlyingCounts.Count}");
            Console.WriteLine($"Expirations: {stats.ExpirationCounts.Count}");
            Console.WriteLine($"Strikes (raw): {stats.StrikeCounts.Count}");
            Console.WriteLine($"Option Types: {stats.TypeCounts.Count}");
            Console.WriteLine($"Total Option Rows Sampled: {stats.TotalOptionRows:N0}");

            Console.WriteLine("\n=== Marginal Entropy (bits) ===");
            Console.WriteLine($"H(Underlying) = {Entropy(stats.UnderlyingCounts):F3}");
            Console.WriteLine($"H(Expiration) = {Entropy(stats.ExpirationCounts):F3}");
            Console.WriteLine($"H(Type)       = {Entropy(stats.TypeCounts):F3}");
            Console.WriteLine($"H(Strike)     = {Entropy(stats.StrikeCounts):F3}");

            var conditional = ComputeConditionalEntropies(stats);
            Console.WriteLine("\n=== Conditional Entropy Approximations (bits) ===");
            Console.WriteLine($"E[ H(Expiration | Underlying) ] = {conditional.H_Exp_given_U:F3}");
            Console.WriteLine($"E[ H(Type | Underlying, Expiration) ] = {conditional.H_Type_given_U_E:F3}");
            Console.WriteLine($"E[ H(Strike | Underlying, Expiration, Type) ] = {conditional.H_Strike_given_U_E_T:F3}");

            Assert.IsTrue(stats.TotalOptionRows > 0, "No option rows processed.");
            Assert.IsTrue(stats.DistinctContracts > 0, "No distinct contracts parsed.");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void CompareOrderingHeuristics()
        {
            RequireMostRecent();

            var stats = CollectKeyStats(_mostRecentFile, MAX_SAMPLE_LINES);
            if (stats.TotalOptionRows == 0)
                Assert.Inconclusive("No data to analyze.");

            var conditional = ComputeConditionalEntropies(stats);

            // Candidate orderings
            // A: Underlying -> Expiration -> Type -> Strike
            // B: Underlying -> Type -> Expiration -> Strike
            var hUnderlying = Entropy(stats.UnderlyingCounts);
            var costA = hUnderlying + conditional.H_Exp_given_U + conditional.H_Type_given_U_E + conditional.H_Strike_given_U_E_T;

            var derived = DeriveAlternativeConditionals(stats); // For ordering B
            var costB = hUnderlying + derived.H_Type_given_U + derived.H_Exp_given_U_T + derived.H_Strike_given_U_T_E;

            Console.WriteLine("=== Ordering Compression Heuristic (Sum of Conditional Entropies) ===");
            Console.WriteLine($"Order A (U->Exp->Type->Strike): {costA:F3} bits");
            Console.WriteLine($"Order B (U->Type->Exp->Strike): {costB:F3} bits");
            Console.WriteLine($"Preferred Ordering (lower heuristic): {(costA <= costB ? "U->Exp->Type->Strike" : "U->Type->Exp->Strike")}");

            Assert.IsTrue(costA > 0 && costB > 0, "Entropy costs should be positive.");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void PrototypeValueDeduplicationEstimate()
        {
            RequireMostRecent();

            long optionRows = 0;
            var uniquePriceValues = new HashSet<string>(StringComparer.Ordinal);

            using (var sr = new StreamReader(_mostRecentFile))
            {
                sr.ReadLine(); // header
                string line;
                long processed = 0;
                while ((line = sr.ReadLine()) != null)
                {
                    if (++processed > MAX_SAMPLE_LINES) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var comma = line.IndexOf(',');
                    if (comma <= 0) continue;
                    var ticker = line.Substring(0, comma).Trim();
                    if (!ticker.StartsWith(OPTION_PREFIX, StringComparison.OrdinalIgnoreCase)) continue;

                    optionRows++;
                    var parts = line.Split(',');
                    if (parts.Length < 8) continue;
                    // OHLC quadruple key (open,close,high,low in file order? file uses open,close,high,low)
                    // Original CSV order: ticker,volume,open,close,high,low,...
                    var key = parts[2] + "|" + parts[3] + "|" + parts[4] + "|" + parts[5];
                    uniquePriceValues.Add(key);
                }
            }

            double repetition = optionRows > 0 ? 1.0 - (uniquePriceValues.Count / (double)optionRows) : 0;
            Console.WriteLine("=== Value Deduplication Prototype ===");
            Console.WriteLine($"Sample Option Rows: {optionRows:N0}");
            Console.WriteLine($"Unique OHLC Quadruples: {uniquePriceValues.Count:N0}");
            Console.WriteLine($"Estimated Repeat Ratio: {repetition:P2} (higher => more savings from pooling)");

            Assert.IsTrue(optionRows > 0, "No option rows sampled.");
            Assert.IsTrue(uniquePriceValues.Count > 0, "No unique OHLC values captured.");
        }

        // ---------------- Helpers ----------------

        private static void RequireMostRecent()
        {
            if (string.IsNullOrEmpty(_bulkDir) || !Directory.Exists(_bulkDir))
                Assert.Inconclusive("Bulk directory not found.");

            if (string.IsNullOrEmpty(_mostRecentFile) || !File.Exists(_mostRecentFile))
                Assert.Inconclusive("Most recent options bulk file not found.");
        }

        private static double Entropy(Dictionary<string, long> counts)
        {
            if (counts == null || counts.Count == 0) return 0.0;
            double total = counts.Values.Sum();
            if (total <= 0) return 0.0;
            double h = 0;
            foreach (var c in counts.Values)
            {
                if (c <= 0) continue;
                double p = c / total;
                h += -p * Math.Log(p, 2);
            }
            return h;
        }

        private static KeyStats CollectKeyStats(string file, int maxLines)
        {
            var stats = new KeyStats();
            using (var sr = new StreamReader(file))
            {
                sr.ReadLine(); // header
                string line;
                int processed = 0;
                while ((line = sr.ReadLine()) != null)
                {
                    if (processed++ >= maxLines) break;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int comma = line.IndexOf(',');
                    if (comma <= 0) continue;
                    var tickerRaw = line.Substring(0, comma).Trim();
                    if (!tickerRaw.StartsWith(OPTION_PREFIX, StringComparison.OrdinalIgnoreCase)) continue;

                    Ticker t;
                    try { t = Ticker.ParseToOption(tickerRaw); }
                    catch { continue; }
                    if (!t.IsOption || !t.ExpirationDate.HasValue || !t.OptionType.HasValue || !t.StrikePrice.HasValue)
                        continue;

                    stats.TotalOptionRows++;

                    Add(stats.UnderlyingCounts, t.UnderlyingSymbol ?? "UNKNOWN");
                    var expKey = t.ExpirationDate.Value.ToString("yyyy-MM-dd");
                    Add(stats.ExpirationCounts, expKey);
                    Add(stats.TypeCounts, t.OptionType.Value.ToString());
                    Add(stats.StrikeCounts, t.StrikePrice.Value.ToString(CultureInfo.InvariantCulture));

                    var uKey = t.UnderlyingSymbol ?? "UNKNOWN";
                    var ueKey = uKey + "|" + expKey;
                    var uetKey = ueKey + "|" + t.OptionType.Value;

                    Add(stats.ExpPerUnderlying, uKey + "||" + expKey); // (U,E)
                    Add(stats.TypePerUnderlyingExp, ueKey + "||" + t.OptionType.Value); // (U,E,Type)
                    Add(stats.StrikePerUnderlyingExpType, uetKey + "||" + t.StrikePrice.Value.ToString(CultureInfo.InvariantCulture)); // (U,E,Type,Strike)

                    stats.DistinctContractsSet.Add(tickerRaw);
                }
            }
            stats.DistinctContracts = stats.DistinctContractsSet.Count;
            return stats;
        }

        private static void Add(Dictionary<string, long> dict, string key)
        {
            if (dict.TryGetValue(key, out var c)) dict[key] = c + 1; else dict[key] = 1;
        }

        private static ConditionalEntropyResult ComputeConditionalEntropies(KeyStats stats)
        {
            return new ConditionalEntropyResult
            {
                H_Exp_given_U = ConditionalEntropy(stats.UnderlyingCounts, stats.ExpPerUnderlying),
                H_Type_given_U_E = ConditionalEntropy(stats.ExpirationCounts, stats.TypePerUnderlyingExp, compositeLevel: 2),
                H_Strike_given_U_E_T = ConditionalEntropy(stats.TypeCounts, stats.StrikePerUnderlyingExpType, compositeLevel: 3)
            };
        }

        private static AlternativeConditionalEntropyResult DeriveAlternativeConditionals(KeyStats stats)
        {
            var uType = new Dictionary<string, long>();
            var uTypeExp = new Dictionary<string, long>();
            var uTypeExpStrike = new Dictionary<string, long>();

            foreach (var key in stats.TypePerUnderlyingExp.Keys)
            {
                // key: (U|Exp)||Type
                var split = key.Split(new[] { "||" }, StringSplitOptions.None);
                if (split.Length != 2) continue;
                var left = split[0];
                var typ = split[1];
                var parts = left.Split('|');
                if (parts.Length < 2) continue;
                var u = parts[0];
                var exp = parts[1];
                var utKey = u + "|" + typ;
                Add(uType, utKey);
                var uteKey = utKey + "|" + exp;
                Add(uTypeExp, uteKey);
            }

            foreach (var key in stats.StrikePerUnderlyingExpType.Keys)
            {
                // key: (U|Exp|Type)||Strike
                var split = key.Split(new[] { "||" }, StringSplitOptions.None);
                if (split.Length != 2) continue;
                var left = split[0];
                var strike = split[1];
                var parts = left.Split('|');
                if (parts.Length < 3) continue;
                var u = parts[0];
                var exp = parts[1];
                var type = parts[2];
                var uteKey = u + "|" + type + "|" + exp;
                var utesKey = uteKey + "|" + strike;
                Add(uTypeExpStrike, utesKey);
            }

            return new AlternativeConditionalEntropyResult
            {
                H_Type_given_U = ConditionalEntropy(stats.UnderlyingCounts, uType),
                H_Exp_given_U_T = ConditionalEntropy(uType, uTypeExp, compositeLevel: 2),
                H_Strike_given_U_T_E = ConditionalEntropy(uTypeExp, uTypeExpStrike, compositeLevel: 3)
            };
        }

        private static double ConditionalEntropy(Dictionary<string, long> parentCounts, Dictionary<string, long> composite, int compositeLevel = 1)
        {
            var perParentChild = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
            foreach (var kvp in composite)
            {
                var sepIndex = kvp.Key.IndexOf("||", StringComparison.Ordinal);
                if (sepIndex <= 0) continue;
                var parentKey = kvp.Key.Substring(0, sepIndex);
                var childKey = kvp.Key.Substring(sepIndex + 2);
                if (!perParentChild.TryGetValue(parentKey, out var dict))
                {
                    dict = new Dictionary<string, long>(StringComparer.Ordinal);
                    perParentChild[parentKey] = dict;
                }
                if (dict.TryGetValue(childKey, out var c)) dict[childKey] = c + kvp.Value; else dict[childKey] = kvp.Value;
            }

            double totalParents = parentCounts.Values.Sum();
            if (totalParents == 0) return 0;
            double h = 0;
            foreach (var parent in perParentChild)
            {
                parentCounts.TryGetValue(ExtractFirstSegment(parent.Key, compositeLevel), out var parentBaseCount);
                if (parentBaseCount <= 0) continue;
                double pParent = parentBaseCount / totalParents;
                double childTotal = parent.Value.Values.Sum();
                if (childTotal <= 0) continue;
                double hChild = 0;
                foreach (var cc in parent.Value.Values)
                {
                    double p = cc / childTotal;
                    hChild += -p * Math.Log(p, 2);
                }
                h += pParent * hChild;
            }
            return h;
        }

        private static string ExtractFirstSegment(string parentKey, int level)
        {
            if (level <= 1) return parentKey;
            var pipe = parentKey.IndexOf('|');
            return pipe > 0 ? parentKey.Substring(0, pipe) : parentKey;
        }

        // ------------ Internal DTOs ------------
        private sealed class KeyStats
        {
            public long TotalOptionRows;
            public long DistinctContracts;
            public HashSet<string> DistinctContractsSet = new HashSet<string>(StringComparer.Ordinal);
            public Dictionary<string, long> UnderlyingCounts = new Dictionary<string, long>(StringComparer.Ordinal);
            public Dictionary<string, long> ExpirationCounts = new Dictionary<string, long>(StringComparer.Ordinal);
            public Dictionary<string, long> TypeCounts = new Dictionary<string, long>(StringComparer.Ordinal);
            public Dictionary<string, long> StrikeCounts = new Dictionary<string, long>(StringComparer.Ordinal);
            public Dictionary<string, long> ExpPerUnderlying = new Dictionary<string, long>(StringComparer.Ordinal); // (U,E)
            public Dictionary<string, long> TypePerUnderlyingExp = new Dictionary<string, long>(StringComparer.Ordinal); // (U,E,Type)
            public Dictionary<string, long> StrikePerUnderlyingExpType = new Dictionary<string, long>(StringComparer.Ordinal); // (U,E,Type,Strike)
        }

        private sealed class ConditionalEntropyResult
        {
            public double H_Exp_given_U;
            public double H_Type_given_U_E;
            public double H_Strike_given_U_E_T;
        }

        private sealed class AlternativeConditionalEntropyResult
        {
            public double H_Type_given_U;
            public double H_Exp_given_U_T;
            public double H_Strike_given_U_T_E;
        }
    }
}
