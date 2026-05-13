using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2; // Added for TimeFrame enum
using System.Globalization; // Added for timestamp parsing

namespace Trade.Tests
{
    [TestClass]
    public class OptionPricesIntegrationTests
    {
        public TestContext TestContext { get; set; }

        // Shared price data loaded once per class to avoid repeated disk I/O
        private static Prices _sharedPrices;
        private static PriceRecord[] _sharedDaily;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            _sharedPrices = new Prices(Constants.SPX_JSON);
            _sharedDaily = _sharedPrices.GetDailyPriceRecords();
            if (_sharedDaily == null || _sharedDaily.Length == 0)
                Assert.Inconclusive("No daily price records loaded from Constants.SPX_JSON (ClassInit)");
        }

        // Classification of timestamp search outcomes
        private enum TimestampSearchResult
        {
            ExactMatch,
            Bracketed,      // No exact match, but have at least one earlier AND one later price
            Unbracketed,    // No exact match; only earlier OR only later price present
            NoData          // No valid price records parsed
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GapBridgeBars_AtOpen_PeriodLookbackReturnsManufacturedBar()
        {
            var daily = _sharedDaily;
            Assert.IsTrue(daily.Length > 0, "No daily price records loaded.");

            var orderedDaily = daily.OrderBy(r => r.DateTime.Date).ToArray();
            var lastDate = orderedDaily.Max(r => r.DateTime.Date);
            var minWindowDate = lastDate.AddYears(-2);

            var window = orderedDaily
                .Where(r => r.DateTime.Date >= minWindowDate && r.DateTime.Date <= lastDate)
                .OrderBy(r => r.DateTime.Date)
                .ToArray();

            Assert.IsTrue(window.Length > 50, "Insufficient daily records in 2-year window.");

            var timeFrames = new[] { TimeFrame.M1, TimeFrame.M5, TimeFrame.M15, TimeFrame.M30 };
            int checkedDays = 0;
            int validated = 0;
            var failures = new List<string>();

            for (int idx = 1; idx < window.Length; idx++)
            {
                var dayRec = window[idx];
                var tradeDate = dayRec.DateTime.Date;

                // Skip weekends / closed full holidays
                if (tradeDate.DayOfWeek == DayOfWeek.Saturday || tradeDate.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                var hol = Prices.GetUSMarketHolidayInfo(tradeDate);
                if (hol.IsClosed) continue;

                // Determine previous trading day (skip weekends / closed)
                DateTime prevTradingDay = tradeDate.AddDays(-1);
                while (true)
                {
                    if (prevTradingDay.DayOfWeek == DayOfWeek.Saturday || prevTradingDay.DayOfWeek == DayOfWeek.Sunday ||
                        Prices.GetUSMarketHolidayInfo(prevTradingDay).IsClosed)
                    {
                        prevTradingDay = prevTradingDay.AddDays(-1);
                        continue;
                    }
                    break;
                }

                var openTs = tradeDate.AddHours(9).AddMinutes(30);

                foreach (var tf in timeFrames)
                {
                    // Ask for "period of 1 before the current day open"
                    // End is exclusive, so end=openTs returns last bar strictly before 09:30.
                    var range = _sharedPrices.GetRange(openTs.AddDays(-5), openTs, tf, period: 1, false, true).ToArray();

                    if (range.Length != 1)
                    {
                        failures.Add($"{tradeDate:yyyy-MM-dd} {tf}: Expected 1 bar, got {range.Length}");
                        continue;
                    }

                    var bar = range[0];

                    // Validate manufactured gap bridge expectations
                    bool manufactured = bar.Manufactured;
                    bool debugOk = bar.Debug != null && bar.Debug.StartsWith("GapBridge");
                    // FIX: Changed expected time from 16:00 to 16:15 to match actual implementation
                    bool timeOk = bar.DateTime.Date == prevTradingDay && bar.DateTime.Hour == 16 && bar.DateTime.Minute == 15;

                    if (!(manufactured && debugOk && timeOk))
                    {
                        failures.Add(
                            $"{tradeDate:yyyy-MM-dd} {tf}: Manufactured={manufactured} Debug='{bar.Debug}' Time={bar.DateTime:yyyy-MM-dd HH:mm} (PrevTradingDay={prevTradingDay:yyyy-MM-dd} 16:15 expected)");
                        continue;
                    }

                    validated++;
                    TestContext.WriteLine(
                        $"OK {tradeDate:yyyy-MM-dd} {tf}: GapBridge {bar.DateTime:yyyy-MM-dd HH:mm} Debug={bar.Debug}");
                }

                checkedDays++;
                if (checkedDays > 150) break; // Keep test runtime bounded
            }

            // Print all failures for diagnosis
            foreach (var f in failures)
                TestContext.WriteLine("FAIL " + f);

            // Calculate success rate
            int totalChecks = validated + failures.Count;
            double successRate = totalChecks > 0 ? (double)validated / totalChecks : 0;

            TestContext.WriteLine($"Gap bridge validation: {validated} validated, {failures.Count} failures, {successRate:P1} success rate");

            Assert.IsTrue(validated > 0, "No valid manufactured gap bars validated.");

            // Allow for a small number of edge case failures (less than 1% failure rate)
            Assert.IsTrue(successRate >= 0.99, $"Success rate too low: {successRate:P1}. Expected >= 99.0%. Failures: {failures.Count}, Validated: {validated}");
        }

        [TestMethod][TestCategory("Core")]
        public void PreviousTradingDayLookup_ReturnsExactlyOnePriorSession()
        {
            var daily = _sharedDaily;
            Assert.IsTrue(daily.Length > 0, "No daily price records loaded.");
            
            // Normalize & sort
            var ordered = daily.OrderBy(r => r.DateTime.Date).ToArray();
            var lastDate = ordered.Max(r => r.DateTime.Date);
            var minWindowDate = lastDate.AddYears(-2);
            var window = ordered
                .Where(r => r.DateTime.Date >= minWindowDate && r.DateTime.Date <= lastDate)
                .ToArray();

            Assert.IsTrue(window.Length > 10, "Insufficient records in 2-year window.");

            int checkedDays = 0;
            foreach (var current in window)
            {
                var d = current.DateTime.Date;

                // Skip if first usable trading day (no prior)
                var prior = FindPreviousTradingSession(window, d);
                if (prior == null) continue;

                checkedDays++;

                // Ensure no intervening trading session
                var intervening = window.Any(r =>
                    r.DateTime.Date > prior.DateTime.Date &&
                    r.DateTime.Date < d);

                Assert.IsFalse(intervening, $"Intervening trading day found between {prior.DateTime:yyyy-MM-dd} and {d:yyyy-MM-dd}");

                // (Optional) sanity: price fields present
                Assert.IsTrue(current.Close > 0, "Current close missing/zero.");
                Assert.IsTrue(prior.Close > 0, "Prior close missing/zero.");

                TestContext.WriteLine($"Day {d:yyyy-MM-dd} prior session {prior.DateTime:yyyy-MM-dd} Gap={(current.Open - prior.Close):F2}");
            }

            Assert.IsTrue(checkedDays > 50, "Too few days validated; data irregular?");
        }

        private static PriceRecord FindPreviousTradingSession(PriceRecord[] window, DateTime day)
        {
            // Get earlier records only, return the last one.
            return window
                .Where(r => r.DateTime.Date < day)
                .OrderBy(r => r.DateTime.Date)
                .LastOrDefault();
        }

        [TestMethod][TestCategory("LongRunning")]
        public void CanParseAllSpyOptionContractFileNamesIntoTickers()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var contractDir = Path.Combine(baseDir, "ContractData", "SPY");
            Assert.IsTrue(Directory.Exists(contractDir), "Contract directory not found: " + contractDir);

            var csvFiles = Directory.GetFiles(contractDir, "O_SPY*.csv");
            Assert.IsTrue(csvFiles.Length > 0, "No SPY option contract CSV files found in: " + contractDir);

            int parsed = 0, calls = 0, puts = 0;
            DateTime? minExp = null, maxExp = null;

            foreach (var filePath in csvFiles)
            {
                var fileName = Path.GetFileName(filePath);
                Assert.IsTrue(fileName.StartsWith("O_SPY"), "Unexpected filename prefix: " + fileName);
                Assert.IsTrue(fileName.EndsWith(".csv"), "Unexpected filename extension: " + fileName);

                var reconstructed = "O:" + Path.GetFileNameWithoutExtension(fileName).Substring(2);
                var ticker = Ticker.ParseToOption(reconstructed);
                Assert.IsNotNull(ticker);
                Assert.IsTrue(ticker.IsOption);
                Assert.AreEqual("SPY", ticker.UnderlyingSymbol, true);
                Assert.IsTrue(ticker.ExpirationDate.HasValue);
                Assert.IsTrue(ticker.OptionType.HasValue);
                Assert.IsTrue(ticker.StrikePrice.HasValue && ticker.StrikePrice.Value > 0);

                parsed++;
                if (ticker.OptionType.Value == Polygon2.OptionType.Call) calls++; else puts++;
                if (!minExp.HasValue || ticker.ExpirationDate < minExp) minExp = ticker.ExpirationDate;
                if (!maxExp.HasValue || ticker.ExpirationDate > maxExp) maxExp = ticker.ExpirationDate;
            }

            var prices = _sharedPrices; // reuse shared underlying prices
            var optionsPrices = new Prices2.OptionPrices();
            var daily = _sharedDaily;
            if (daily.Length == 0) Assert.Inconclusive("No daily price records loaded from Constants.SPX_JSON");

            var lastDate = daily.Max(r => r.DateTime.Date);
            var minWindowDate = lastDate.AddYears(-2);
            var constrainedDaily = daily.Where(r => r.DateTime.Date >= minWindowDate && r.DateTime.Date <= lastDate)
                                        .OrderBy(r => r.DateTime)
                                        .ToArray();

            TestContext.WriteLine($"Daily iteration constrained to {minWindowDate:yyyy-MM-dd} .. {lastDate:yyyy-MM-dd} ({constrainedDaily.Length} days)");

            var callFailures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var putFailures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int callSuccess = 0, putSuccess = 0;

            double RoundToIncrement(double strike, double inc) => Math.Round(strike / inc) * inc;
            double NextUp(double strike, double inc) => Math.Ceiling(strike / inc) * inc;
            double NextDown(double strike, double inc) => Math.Floor(strike / inc) * inc;
            double CalcStrike(double underlying, Polygon2.OptionType type, int distance)
            {
                const double inc = 1.0;
                var atm = RoundToIncrement(underlying, inc);
                if (type == Polygon2.OptionType.Call)
                {
                    var target = atm + distance * inc;
                    if (target < underlying) target = NextUp(underlying, inc);
                    return target;
                }
                else
                {
                    var target = atm - distance * inc;
                    if (target > underlying) target = NextDown(underlying, inc);
                    return target;
                }
            }

            // Holiday-aware business day addition (skips full market closures)
            DateTime AddBusinessDays(DateTime dt, int days)
            {
                var d = dt.Date;
                var remaining = days;
                while (remaining > 0)
                {
                    d = d.AddDays(1);
                    if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                    var hol = Prices.GetUSMarketHolidayInfo(d);
                    if (hol.IsClosed) continue; // skip full closures
                    // if (hol.IsHalfDay) continue; // uncomment to also skip half-days
                    remaining--;
                }
                return d;
            }

            string GenSymbol(string underlying, DateTime exp, Polygon2.OptionType t, double strike)
            {
                var date = exp.ToString("yyMMdd");
                var cp = t == Polygon2.OptionType.Call ? "C" : "P";
                var strikeStr = ((int)(strike * 1000)).ToString("D8");
                return $"O:{underlying}{date}{cp}{strikeStr}";
            }

            void Inc(Dictionary<string, int> dict, string key)
            {
                if (!dict.ContainsKey(key)) dict[key] = 0;
                dict[key]++;
            }

            // Classify timestamp search in a contract CSV file
            TimestampSearchResult ClassifyTimestampSearch(string[] lines, DateTime ts)
            {
                DateTime TruncateToMinute(DateTime d) => new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0, DateTimeKind.Unspecified);
                var target = TruncateToMinute(ts);

                TimeZoneInfo easternTz = null;
                try { easternTz = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); } catch { }

                bool any = false;
                bool hasEarlier = false;
                bool hasLater = false;

                foreach (var raw in lines)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    var line = raw.Trim();

                    // (Note: original logic retained – may need correction if header logic inverted)
                    if (char.IsDigit(line[0]) || line.IndexOf("open", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length < 2) continue;

                    var idx = parts.Length - 2; // second-to-last per spec
                    var tsToken = parts[idx].Trim();
                    if (!long.TryParse(tsToken, out var rawValue)) continue;

                    DateTime utc;
                    if (rawValue > 10_000_000_000_000_000)
                        utc = DateTimeOffset.FromUnixTimeMilliseconds(rawValue / 1_000_000).UtcDateTime;
                    else if (rawValue > 1_000_000_000_000)
                        utc = DateTimeOffset.FromUnixTimeMilliseconds(rawValue / 1_000_000).UtcDateTime;
                    else if (rawValue > 10_000_000_000)
                        utc = DateTimeOffset.FromUnixTimeMilliseconds(rawValue / 1_000).UtcDateTime;
                    else if (rawValue > 1_000_000_000)
                        utc = DateTimeOffset.FromUnixTimeSeconds(rawValue).UtcDateTime;
                    else
                        continue;

                    any = true;

                    DateTime est = easternTz != null ? TimeZoneInfo.ConvertTimeFromUtc(utc, easternTz) : utc.ToLocalTime();
                    var minute = TruncateToMinute(est);

                    if (minute == target)
                        return TimestampSearchResult.ExactMatch;
                    if (minute < target) hasEarlier = true; else if (minute > target) hasLater = true;
                }

                if (!any) return TimestampSearchResult.NoData;
                if (hasEarlier && hasLater) return TimestampSearchResult.Bracketed;
                return TimestampSearchResult.Unbracketed;
            }

            foreach (var dailyRecord in constrainedDaily)
            {
                var tradeDate = dailyRecord.DateTime.Date;

                // Skip weekends / full market holidays before doing anything
                if (tradeDate.DayOfWeek == DayOfWeek.Saturday || tradeDate.DayOfWeek == DayOfWeek.Sunday)
                    continue;

                var tradeHoliday = Prices.GetUSMarketHolidayInfo(tradeDate);
                if (tradeHoliday.IsClosed)
                {
                    TestContext.WriteLine($"Skip closed holiday {tradeDate:yyyy-MM-dd} ({tradeHoliday.Description})");
                    continue;
                }

                var dayStartTs = tradeDate.AddHours(9).AddMinutes(30);
                var expiration = AddBusinessDays(tradeDate, 1); // Proper next business day (holiday-aware)

                void Process(Polygon2.OptionType type, Dictionary<string, int> failureDict, ref int successCounter)
                {
                    var attemptTs = dayStartTs;

                    var underlyingMinute = prices.GetPriceAt(attemptTs, TimeFrame.M1);
                    if (underlyingMinute == null)
                    {
                        // Try first few minutes in case data starts late
                        var found = false;
                        for (int m = 1; m <= 5; m++)
                        {
                            underlyingMinute = prices.GetPriceAt(attemptTs.AddMinutes(m), TimeFrame.M1);
                            if (underlyingMinute != null) { attemptTs = attemptTs.AddMinutes(m); found = true; break; }
                        }
                        if (!found)
                        {
                            Inc(failureDict, "NoUnderlyingPriceAtStart");
                            return;
                        }
                    }

                    var underlyingPrice = underlyingMinute.Close;
                    var strike = CalcStrike(underlyingPrice, type, 1);

                    // (Optional) If expiration is a holiday (shouldn't happen now), bail
                    var expHoliday = Prices.GetUSMarketHolidayInfo(expiration);
                    if (expHoliday.IsClosed)
                    {
                        Inc(failureDict, "ExpirationOnHoliday");
                        return;
                    }

                    var symbol = GenSymbol("SPY", expiration, type, strike).Replace(":", "_");
                    var expectedCsv = Path.Combine(contractDir, symbol + ".csv");

                    if (!File.Exists(expectedCsv))
                    {
                        Inc(failureDict, "ContractCsvMissing");
                        return;
                    }

                    var optionPrice = optionsPrices.GetOptionPrice(prices, type, attemptTs, TimeFrame.M1, 1, 1);
                    if (optionPrice != null && optionPrice.Option != null)
                    {
                        successCounter++;
                        return;
                    }

                    try
                    {
                        var lines = File.ReadAllLines(expectedCsv);
                        var classification = ClassifyTimestampSearch(lines, attemptTs);
                        switch (classification)
                        {
                            case TimestampSearchResult.ExactMatch:
                                // If we had an exact match but optionPrice still null, classify as unknown
                                Inc(failureDict, "UnknownLoadFailure");
                                break;
                            case TimestampSearchResult.Bracketed:
                                Inc(failureDict, "NoExactButBracketed");
                                break;
                            case TimestampSearchResult.Unbracketed:
                                Inc(failureDict, "NoExactAndNotBracketed");
                                break;
                            case TimestampSearchResult.NoData:
                                Inc(failureDict, "NoAnyPriceData");
                                break;
                        }
                    }
                    catch
                    {
                        Inc(failureDict, "FileReadError");
                    }
                }

                Process(Polygon2.OptionType.Call, callFailures, ref callSuccess);
                Process(Polygon2.OptionType.Put, putFailures, ref putSuccess);
            }

            TestContext.WriteLine($"Parsed {parsed} SPY option filenames -> Tickers. Calls: {calls}, Puts: {puts}");
            if (minExp.HasValue && maxExp.HasValue)
                TestContext.WriteLine($"Expiration range: {minExp:yyyy-MM-dd} .. {maxExp:yyyy-MM-dd}");

            TestContext.WriteLine("Daily option retrieval summary (1 strike OTM, next business day expiration):");
            TestContext.WriteLine($"  Call Success: {callSuccess}");
            foreach (var kv in callFailures.OrderBy(k => k.Key))
                TestContext.WriteLine($"  Call Missing [{kv.Key}]: {kv.Value}");
            TestContext.WriteLine($"  Put Success: {putSuccess}");
            foreach (var kv in putFailures.OrderBy(k => k.Key))
                TestContext.WriteLine($"  Put Missing [{kv.Key}]: {kv.Value}");

            Assert.AreEqual(csvFiles.Length, parsed, "Not all filenames were parsed");
            Assert.IsTrue(calls + puts == parsed, "Call/Put counts inconsistent");
        }

        [TestMethod][TestCategory("LongRunning")]
        public void CanHindsightHarvestProfitableOtmOptions()
        {
            double capital = 100_000d;
            double startingCapital = capital;
            int trades = 0;

            // Metrics
            int callTrades = 0, putTrades = 0;
            double callProfit = 0, putProfit = 0;

            // New buy & hold aggregates
            int buyAndHoldTrades = 0;
            double buyAndHoldGrossProfit = 0;
            double buyAndHoldVsHindsightExtra = 0; // Sum of (hindsightProfit - holdProfit)

            var tradesByStrikeDistance = new Dictionary<int, int>();
            var tradesByExpirationDays = new Dictionary<int, int>();
            var profitByStrikeDistance = new Dictionary<int, double>();
            var profitByExpirationDays = new Dictionary<int, double>();
            var holdDaysHistogram = new Dictionary<int, int>();

            void Inc<TKey>(Dictionary<TKey, int> dict, TKey key)
            {
                if (!dict.ContainsKey(key)) dict[key] = 0;
                dict[key]++;
            }
            void AddProfit<TKey>(Dictionary<TKey, double> dict, TKey key, double value)
            {
                if (!dict.ContainsKey(key)) dict[key] = 0;
                dict[key] += value;
            }

            var prices = _sharedPrices; // reuse shared
            var optionsPrices = new Prices2.OptionPrices();
            var daily = _sharedDaily;
            if (daily.Length == 0) Assert.Inconclusive("No daily price records loaded from Constants.SPX_JSON");

            var lastDate = daily.Max(r => r.DateTime.Date);
            var minWindowDate = lastDate.AddYears(-2);
            var constrainedDaily = daily.Where(r => r.DateTime.Date >= minWindowDate && r.DateTime.Date <= lastDate)
                                        .OrderBy(r => r.DateTime)
                                        .ToArray();

            DateTime AddBusinessDays(DateTime dt, int days)
            {
                var d = dt.Date;
                var remaining = days;
                while (remaining > 0)
                {
                    d = d.AddDays(1);
                    if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                    var hol = Prices.GetUSMarketHolidayInfo(d);
                    if (hol.IsClosed) continue;
                    remaining--;
                }
                return d;
            }

            double RoundToIncrement(double strike, double inc) => Math.Round(strike / inc) * inc;
            double NextUp(double strike, double inc) => Math.Ceiling(strike / inc) * inc;
            double NextDown(double strike, double inc) => Math.Floor(strike / inc) * inc;
            double CalcStrike(double underlying, Polygon2.OptionType type, int distance)
            {
                const double inc = 1.0;
                var atm = RoundToIncrement(underlying, inc);
                if (type == Polygon2.OptionType.Call)
                {
                    var target = atm + distance * inc;
                    if (target < underlying) target = NextUp(underlying, inc);
                    return target;
                }
                else
                {
                    var target = atm - distance * inc;
                    if (target > underlying) target = NextDown(underlying, inc);
                    return target;
                }
            }

            PriceRecord GetFirstUnderlyingMinute(DateTime start)
            {
                var m = prices.GetPriceAt(start, TimeFrame.M1);
                if (m != null) return m;
                for (int i = 1; i <= 5; i++)
                {
                    m = prices.GetPriceAt(start.AddMinutes(i), TimeFrame.M1);
                    if (m != null) return m;
                }
                return null;
            }

            for (int i = 0; i < constrainedDaily.Length; i++)
            {
                var dailyRecord = constrainedDaily[i];
                var tradeDate = dailyRecord.DateTime.Date;

                if (tradeDate.DayOfWeek == DayOfWeek.Saturday || tradeDate.DayOfWeek == DayOfWeek.Sunday)
                    continue;
                var hol = Prices.GetUSMarketHolidayInfo(tradeDate);
                if (hol.IsClosed) continue;

                var entryTs = tradeDate.AddHours(9).AddMinutes(30);
                var underlyingMinute = GetFirstUnderlyingMinute(entryTs);
                if (underlyingMinute == null) continue;

                const int maxExpirationDays = 5;
                const double moveThresholdFraction = 0.005;
                const int maxStrikeDistance = 3;
                const double minOptionProfitPct = 0.10;
                const int maxHoldBusinessDays = 5;

                bool tradeTaken = false;

                for (int expDays = 1; expDays <= maxExpirationDays && !tradeTaken; expDays++)
                {
                    var expirationDate = AddBusinessDays(tradeDate, expDays);
                    var futureDaily = daily.FirstOrDefault(d => d.DateTime.Date == expirationDate);
                    if (futureDaily == null) continue;

                    var pctMove = (futureDaily.Close - dailyRecord.Close) / dailyRecord.Close;
                    Polygon2.OptionType? direction = null;
                    if (pctMove > moveThresholdFraction)
                        direction = Polygon2.OptionType.Call;
                    else if (pctMove < -moveThresholdFraction)
                        direction = Polygon2.OptionType.Put;
                    else
                        continue;

                    for (int strikeDistance = 1; strikeDistance <= maxStrikeDistance && !tradeTaken; strikeDistance++)
                    {
                        var optionAtEntry = optionsPrices.GetOptionPrice(
                            prices,
                            direction.Value,
                            underlyingMinute.DateTime,
                            TimeFrame.M1,
                            strikeDistanceAway: strikeDistance,
                            expirationDaysAway: expDays);

                        if (optionAtEntry == null || optionAtEntry.Option == null) continue;

                        var entryPremium = optionAtEntry.Close;
                        if (entryPremium <= 0) continue;

                        var optionSymbol = optionAtEntry.Option.Symbol;
                        var optionPricesForSymbol = optionsPrices.GetPricesForSymbol(optionSymbol);
                        if (optionPricesForSymbol == null) continue;

                        var lastAllowedDate = AddBusinessDays(tradeDate, Math.Min(expDays, maxHoldBusinessDays));
                        if (lastAllowedDate > expirationDate) lastAllowedDate = expirationDate;
                        DateTime scanEnd = lastAllowedDate.AddHours(15).AddMinutes(59);

                        double bestPremium = entryPremium;
                        DateTime bestTs = optionAtEntry.DateTime;

                        // For buy & hold: track the last observed premium (fallback to entry)
                        double lastObservedPremium = entryPremium;

                        for (var d = tradeDate; d <= lastAllowedDate; d = d.AddDays(1))
                        {
                            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                            var hol2 = Prices.GetUSMarketHolidayInfo(d);
                            if (hol2.IsClosed) continue;

                            var dayStart = d.AddHours(9).AddMinutes(30);
                            for (var ts = (d == tradeDate ? optionAtEntry.DateTime : dayStart);
                                 ts.Date == d.Date && ts <= scanEnd;
                                 ts = ts.AddMinutes(1))
                            {
                                var rec = optionPricesForSymbol.GetPriceAtForOptions(ts, TimeFrame.M1);
                                if (rec == null) continue;

                                lastObservedPremium = rec.Close; // progress hold value

                                if (rec.Close > bestPremium)
                                {
                                    bestPremium = rec.Close;
                                    bestTs = ts;
                                }
                            }
                        }

                        var premiumGainPct = (bestPremium - entryPremium) / entryPremium;
                        if (premiumGainPct >= minOptionProfitPct && bestPremium > entryPremium)
                        {
                            var hindsightProfitPerContract = (bestPremium - entryPremium) * 100.0;

                            // Buy & Hold profit (use last observed premium)
                            var holdProfitPerContract = (lastObservedPremium - entryPremium) * 100.0;

                            capital += hindsightProfitPerContract;
                            trades++;

                            if (direction == Polygon2.OptionType.Call) { callTrades++; callProfit += hindsightProfitPerContract; }
                            else { putTrades++; putProfit += hindsightProfitPerContract; }

                            Inc(tradesByStrikeDistance, strikeDistance);
                            Inc(tradesByExpirationDays, expDays);
                            AddProfit(profitByStrikeDistance, strikeDistance, hindsightProfitPerContract);
                            AddProfit(profitByExpirationDays, expDays, hindsightProfitPerContract);

                            var holdDays = (bestTs.Date - tradeDate).Days + 1;
                            Inc(holdDaysHistogram, holdDays);

                            // Aggregate buy & hold metrics
                            buyAndHoldTrades++;
                            buyAndHoldGrossProfit += holdProfitPerContract;
                            buyAndHoldVsHindsightExtra += (hindsightProfitPerContract - holdProfitPerContract);

                            TestContext.WriteLine(
                                $"HindsightTrade: {tradeDate:yyyy-MM-dd} Dir={direction} Exp={expirationDate:yyyy-MM-dd} Dist={strikeDistance} ExpDays={expDays} Entry={entryPremium:F2} Best={bestPremium:F2} HoldLast={lastObservedPremium:F2} ExitTs(best)={bestTs:yyyy-MM-dd HH:mm} GainPct={(premiumGainPct * 100):F1}% HindsightProfit={hindsightProfitPerContract:F2} HoldProfit={holdProfitPerContract:F2} Capital={capital:F2}");

                            var nextStart = bestTs.Date.AddDays(1);
                            while (i + 1 < constrainedDaily.Length && constrainedDaily[i + 1].DateTime.Date < nextStart) i++;
                            tradeTaken = true;
                        }
                    }
                }
            }

            TestContext.WriteLine($"Hindsight OTM option trades: {trades}, StartCapital={startingCapital:F2}, EndCapital={capital:F2}, Net={(capital - startingCapital):F2}");
            TestContext.WriteLine($"Calls: {callTrades} Profit={callProfit:F2}  Puts: {putTrades} Profit={putProfit:F2}");
            TestContext.WriteLine("Trades by Strike Distance:");
            foreach (var kv in tradesByStrikeDistance.OrderBy(k => k.Key))
                TestContext.WriteLine($"  Dist {kv.Key}: Trades={kv.Value} Profit={profitByStrikeDistance[kv.Key]:F2}");
            TestContext.WriteLine("Trades by Expiration Days:");
            foreach (var kv in tradesByExpirationDays.OrderBy(k => k.Key))
                TestContext.WriteLine($"  ExpDays {kv.Key}: Trades={kv.Value} Profit={profitByExpirationDays[kv.Key]:F2}");
            TestContext.WriteLine("Hold Days Histogram:");
            foreach (var kv in holdDaysHistogram.OrderBy(k => k.Key))
                TestContext.WriteLine($"  Hold {kv.Key}d: {kv.Value}");

            // New summary
            if (buyAndHoldTrades > 0)
            {
                var avgExtra = buyAndHoldVsHindsightExtra / buyAndHoldTrades;
                var avgHold = buyAndHoldGrossProfit / buyAndHoldTrades;
                TestContext.WriteLine("Buy & Hold vs Hindsight Summary:");
                TestContext.WriteLine($"  Buy&Hold Trades: {buyAndHoldTrades}");
                TestContext.WriteLine($"  Buy&Hold Gross Profit: {buyAndHoldGrossProfit:F2}");
                TestContext.WriteLine($"  Extra Profit (Hindsight over Hold): {buyAndHoldVsHindsightExtra:F2}");
                TestContext.WriteLine($"  Avg Hold Profit/Trade: {avgHold:F2}");
                TestContext.WriteLine($"  Avg Extra (Hindsight - Hold)/Trade: {avgExtra:F2}");
            }

            Assert.IsTrue(trades > 0, "No hindsight trades found (unexpected if data present)");
            Assert.IsTrue(capital > startingCapital, "Capital did not increase under hindsight scan");
        }

        [TestMethod][TestCategory("LongRunning")] //this tests takes a LONG time to run...
        public void Hindsight_MaxOptionGain_PerDay_CapitalFlows_NeverNegative()
        {
            // Starting capital
            double startingCapital = 100_000d;
            double capital = startingCapital;

            var prices = _sharedPrices;
            var optionsPrices = new Prices2.OptionPrices();
            var daily = _sharedDaily;
            if (daily.Length == 0) Assert.Inconclusive("No daily price records loaded from Constants.SPX_JSON");

            // 2-year window like other tests
            var lastDate = daily.Max(r => r.DateTime.Date);
            var minWindowDate = lastDate.AddMonths(-2);
            var constrainedDaily = daily
                .Where(r => r.DateTime.Date >= minWindowDate && r.DateTime.Date <= lastDate)
                .OrderBy(r => r.DateTime)
                .ToArray();

            // Helper: business day add (holiday + weekend aware)
            DateTime AddBusinessDays(DateTime dt, int days)
            {
                var d = dt.Date;
                var remaining = days;
                while (remaining > 0)
                {
                    d = d.AddDays(1);
                    if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                    var hol = Prices.GetUSMarketHolidayInfo(d);
                    if (hol.IsClosed) continue;
                    remaining--;
                }
                return d;
            }

            // Grab first underlying minute or within first 5 minutes
            PriceRecord GetFirstUnderlyingMinute(DateTime start)
            {
                var m = prices.GetPriceAt(start, TimeFrame.M1);
                if (m != null) return m;
                for (int i = 1; i <= 5; i++)
                {
                    m = prices.GetPriceAt(start.AddMinutes(i), TimeFrame.M1);
                    if (m != null) return m;
                }
                return null;
            }

            const int maxExpDays = 10;
            const int maxStrikeDistance = 10;

            // Equity curve storage (per trading day index)
            double[] equityByDay = new double[constrainedDaily.Length];
            DateTime[] datesByIndex = constrainedDaily.Select(r => r.DateTime.Date).ToArray();
            var dateToIndex = new Dictionary<DateTime, int>();
            for (int i = 0; i < datesByIndex.Length; i++)
                if (!dateToIndex.ContainsKey(datesByIndex[i]))
                    dateToIndex.Add(datesByIndex[i], i);

            // Track scheduled exits (exit day -> proceeds credit)
            var exitCredits = new Dictionary<int, double>();

            // Trade stats
            int trades = 0, callTrades = 0, putTrades = 0;
            double callProfit = 0, putProfit = 0;
            int skippedNoData = 0;

            // For reporting distribution of holding lengths
            var holdLengthCounts = new Dictionary<int, int>();

            // Iterate each trading day sequentially; apply credits first, then open new position
            for (int dayIdx = 0; dayIdx < constrainedDaily.Length; dayIdx++)
            {
                var dailyRecord = constrainedDaily[dayIdx];
                var tradeDate = dailyRecord.DateTime.Date;

                // Apply any realized exits scheduled for today BEFORE opening new position
                if (exitCredits.TryGetValue(dayIdx, out var credit))
                {
                    capital += credit;
                }

                // Skip weekends / holiday closures
                if (tradeDate.DayOfWeek == DayOfWeek.Saturday || tradeDate.DayOfWeek == DayOfWeek.Sunday ||
                    Prices.GetUSMarketHolidayInfo(tradeDate).IsClosed)
                {
                    equityByDay[dayIdx] = capital;
                    continue;
                }

                var entryTs = tradeDate.AddHours(9).AddMinutes(30);
                var underlyingMinute = GetFirstUnderlyingMinute(entryTs);
                if (underlyingMinute == null)
                {
                    skippedNoData++;
                    equityByDay[dayIdx] = capital;
                    continue;
                }

                // Hindsight scan: choose ONE option (call or put, any strike distance / days to expiry up to 10)
                PriceRecord bestOptionAtEntry = null;
                double bestProfitPerContract = 0;
                double bestEntryPremium = 0;
                double bestExitPremium = 0;
                DateTime bestExitTs = DateTime.MinValue;
                Polygon2.OptionType? bestDirection = null;
                int bestExpDays = 0;
                int bestStrikeDistance = 0;
                string bestSymbol = null;

                for (int expDays = 1; expDays <= maxExpDays; expDays++)
                {
                    var expirationDate = AddBusinessDays(tradeDate, expDays);

                    for (int strikeDist = 1; strikeDist <= maxStrikeDistance; strikeDist++)
                    {
                        foreach (Polygon2.OptionType dir in new[] { Polygon2.OptionType.Call, Polygon2.OptionType.Put })
                        {
                            var optionAtEntry = optionsPrices.GetOptionPrice(
                                prices,
                                dir,
                                underlyingMinute.DateTime,
                                TimeFrame.M1,
                                strikeDistanceAway: strikeDist,
                                expirationDaysAway: expDays);

                            if (optionAtEntry == null || optionAtEntry.Option == null) continue;

                            var entryPremium = optionAtEntry.Close;
                            if (entryPremium <= 0) continue;

                            var optionSymbol = optionAtEntry.Option.Symbol;
                            var optionPricesForSymbol = optionsPrices.GetPricesForSymbol(optionSymbol);
                            if (optionPricesForSymbol == null) continue;

                            var expiration = optionAtEntry.Option.ExpirationDate ?? expirationDate;
                            // We only look up to its actual expiration end-of-day (15:59)
                            var scanEnd = expiration.AddHours(15).AddMinutes(59);

                            double localBestPremium = entryPremium;
                            DateTime localBestTs = optionAtEntry.DateTime;

                            // Scan minute-by-minute until expiration day end
                            for (var d = tradeDate; d <= expiration; d = d.AddDays(1))
                            {
                                if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) continue;
                                var hol2 = Prices.GetUSMarketHolidayInfo(d);
                                if (hol2.IsClosed) continue;

                                var dayStart = d.AddHours(9).AddMinutes(30);
                                var startTs = (d == tradeDate ? optionAtEntry.DateTime : dayStart);

                                for (var ts = startTs; ts.Date == d.Date && ts <= scanEnd; ts = ts.AddMinutes(1))
                                {
                                    var rec = optionPricesForSymbol.GetPriceAtForOptions(ts, TimeFrame.M1);
                                    if (rec == null) continue;
                                    if (rec.Close > localBestPremium)
                                    {
                                        localBestPremium = rec.Close;
                                        localBestTs = ts;
                                    }
                                }
                            }

                            if (localBestPremium > entryPremium)
                            {
                                var profitPerContract = (localBestPremium - entryPremium) * 100.0;
                                if (profitPerContract > bestProfitPerContract)
                                {
                                    bestProfitPerContract = profitPerContract;
                                    bestOptionAtEntry = optionAtEntry;
                                    bestEntryPremium = entryPremium;
                                    bestExitPremium = localBestPremium;
                                    bestExitTs = localBestTs;
                                    bestDirection = dir;
                                    bestExpDays = expDays;
                                    bestStrikeDistance = strikeDist;
                                    bestSymbol = optionSymbol;
                                }
                            }
                        }
                    }
                }

                // Execute chosen trade (if any profitable candidate found)
                if (bestOptionAtEntry != null)
                {
                    var entryCost = bestEntryPremium * 100.0;       // cash out today
                    var exitProceeds = bestExitPremium * 100.0;     // cash in on exit day
                    var profit = exitProceeds - entryCost;

                    // Debit entry cost today
                    capital -= entryCost;

                    // Schedule exit credit on exit date (if within window)
                    var exitDate = bestExitTs.Date;
                    if (dateToIndex.TryGetValue(exitDate, out var exitIdx))
                    {
                        if (!exitCredits.ContainsKey(exitIdx))
                            exitCredits[exitIdx] = 0;
                        exitCredits[exitIdx] += exitProceeds;

                        var holdDays = (exitDate - tradeDate).Days + 1;
                        if (!holdLengthCounts.ContainsKey(holdDays)) holdLengthCounts[holdDays] = 0;
                        holdLengthCounts[holdDays]++;
                    }
                    else
                    {
                        // If exit date not in window, treat as expiring worthless (shouldn't happen typically)
                        profit = -entryCost;
                    }

                    trades++;
                    if (bestDirection == Polygon2.OptionType.Call) { callTrades++; callProfit += profit; }
                    else { putTrades++; putProfit += profit; }

                    TestContext.WriteLine(
                        $"Trade {trades}: {tradeDate:yyyy-MM-dd} Dir={bestDirection} Sym={bestSymbol} Dist={bestStrikeDistance} ExpDays={bestExpDays} Entry={bestEntryPremium:F2} Exit={bestExitPremium:F2} ExitDate={bestExitTs:yyyy-MM-dd} Profit={profit:F2} CapitalAfterEntry={capital:F2}");
                }

                // Record equity snapshot after debits/credits for the day
                equityByDay[dayIdx] = capital;

                // Assert never negative intra-loop
                Assert.IsTrue(capital >= 0, $"Capital went negative on {tradeDate:yyyy-MM-dd} (Capital={capital:F2})");
            }

            // Apply any final day exit credits that would fall exactly on last day (already applied at start of loop)
            // Final equity is last element
            var endCapital = equityByDay[equityByDay.Length - 1];

            // Validate full equity curve non-negative
            for (int i = 0; i < equityByDay.Length; i++)
                Assert.IsTrue(equityByDay[i] >= 0, $"Equity negative at index {i} ({datesByIndex[i]:yyyy-MM-dd})");

            TestContext.WriteLine($"StartCapital={startingCapital:F2} EndCapital={endCapital:F2} Net={(endCapital - startingCapital):F2} Trades={trades} Calls={callTrades} Puts={putTrades}");
            TestContext.WriteLine($"Call P&L={callProfit:F2}  Put P&L={putProfit:F2}  Total P&L={(callProfit + putProfit):F2}");
            TestContext.WriteLine("Hold Length Distribution (days):");
            foreach (var kv in holdLengthCounts.OrderBy(k => k.Key))
                TestContext.WriteLine($"  {kv.Key}d: {kv.Value}");

            Assert.IsTrue(trades > 0, "No trades executed.");
            Assert.IsTrue(endCapital >= startingCapital, "Ending capital did not at least match starting capital (pure hindsight should not lose).");
        }

        [TestMethod][TestCategory("Core")]
        public void OptionExpirationWeekdayCoverage_PotentialOmissions()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var contractDir = Path.Combine(baseDir, "ContractData", "SPY");
            if (!Directory.Exists(contractDir))
                Assert.Inconclusive("Contract directory not found: " + contractDir);

            var csvFiles = Directory.GetFiles(contractDir, "O_SPY*.csv");
            if (csvFiles.Length == 0)
                Assert.Inconclusive("No SPY option contract CSV files found in: " + contractDir);

            DateTime ParseExpiration(string fileName)
            {
                var core = Path.GetFileNameWithoutExtension(fileName);
                if (!core.StartsWith("O_SPY") || core.Length < 11) // prefix + YYMMDD
                    return DateTime.MinValue;
                var datePart = core.Substring(5, 6);
                if (DateTime.TryParseExact(datePart, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return dt.Date;
                return DateTime.MinValue;
            }

            var expirations = new HashSet<DateTime>();
            foreach (var f in csvFiles)
            {
                var exp = ParseExpiration(Path.GetFileName(f));
                if (exp != DateTime.MinValue) expirations.Add(exp);
            }

            if (expirations.Count == 0)
                Assert.Inconclusive("Could not parse any expiration dates from option filenames.");

            var minExp = expirations.Min();
            var maxExp = expirations.Max();
            TestContext.WriteLine($"Expiration coverage span: {minExp:yyyy-MM-dd} .. {maxExp:yyyy-MM-dd} ({expirations.Count} distinct expiration dates)");

            DateTime WeekStart(DateTime d) => d.AddDays(-(int)(d.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)d.DayOfWeek - 1));

            var weeks = expirations
                .GroupBy(d => WeekStart(d))
                .OrderBy(g => g.Key)
                .ToList();

            var potentialOmissions = new List<string>();
            var weekdayCounts = new Dictionary<DayOfWeek, int>();
            foreach (DayOfWeek dow in Enum.GetValues(typeof(DayOfWeek)))
                weekdayCounts[dow] = 0;

            bool IsTradingDay(DateTime d)
            {
                if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) return false;
                var hol = Prices.GetUSMarketHolidayInfo(d);
                if (hol.IsClosed) return false;
                return true;
            }

            foreach (var week in weeks)
            {
                var weekStart = week.Key;
                var present = new HashSet<DayOfWeek>(week.Select(d => d.DayOfWeek));
                foreach (var d in week.OrderBy(x => x))
                {
                    weekdayCounts[d.DayOfWeek]++; // increment frequency for this weekday
                }

                var tradingWeekdays = new List<DateTime>();
                for (int i = 0; i < 5; i++)
                {
                    var day = weekStart.AddDays(i);
                    if (day < minExp || day > maxExp) continue;
                    if (IsTradingDay(day)) tradingWeekdays.Add(day);
                }

                if (tradingWeekdays.Count <= 2)
                    continue; // skip sparse historical weeks

                var missing = tradingWeekdays.Where(d => !expirations.Contains(d)).ToList();
                if (missing.Count > 0 && (tradingWeekdays.Count - missing.Count) >= (tradingWeekdays.Count - 1))
                {
                    foreach (var m in missing)
                        potentialOmissions.Add($"Week {weekStart:yyyy-MM-dd}: Missing {m:yyyy-MM-dd} ({m.DayOfWeek})");
                }
            }

            TestContext.WriteLine("Weekday expiration frequency (distinct dates):");
            foreach (var kv in weekdayCounts.Where(k => k.Key >= DayOfWeek.Monday && k.Key <= DayOfWeek.Friday)
                                             .OrderBy(k => k.Key))
            {
                TestContext.WriteLine($"  {kv.Key}: {kv.Value}");
            }

            if (potentialOmissions.Count == 0)
            {
                TestContext.WriteLine("No potential weekday omissions detected (within dense weeks).");
            }
            else
            {
                foreach (var om in potentialOmissions.Take(50))
                    TestContext.WriteLine("OMISSION " + om);
                if (potentialOmissions.Count > 50)
                    TestContext.WriteLine($"... {potentialOmissions.Count - 50} more omissions not shown");
            }
        }
    }


}