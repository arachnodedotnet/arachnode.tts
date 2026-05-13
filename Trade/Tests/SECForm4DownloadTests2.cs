using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Trade.Form4;
using Trade.Form4.Enums;
using Trade.Polygon2;
using static Trade.Form4.SimplePnLTracker;

namespace Trade.Tests
{
    public partial class SECForm4DownloadTests
    {
        [TestMethod]
        [TestCategory("SEC")]
        public async Task Main0_Form4_Build_Verify_Today()
        {
            await Form4Downloader.Build(1, "AllTransactionsForLiveVerificationToday.json");
            await Main2_Form4_SimplePnL_VOORotation();
        }

        [TestMethod]
        [TestCategory("SEC")]
        public async Task Main0_Form4_Build_Today()
        {
            await Form4Downloader.Build(1, "AllTransactionsForLiveVerificationToday.json");
        }

        [TestMethod]
        [TestCategory("SEC")]
        public async Task Main0_Form4_Build_Week()
        {
            await Form4Downloader.Build(19, "AllTransactionsForLiveVerification.json");
        }

        [TestMethod]
        [TestCategory("SEC")]
        public async Task Main1_Form4_Build_Year()
        {
            await Form4Downloader.Build(800, "AllTransactionsForTheYear2.json");
        }

        [TestMethod]
        [TestCategory("SEC")]
        public async Task Main2_Form4_SimplePnL_VOORotation()
        {
            ConsoleUtilities.EnableFileLogging();

            bool allowFollowOnTrades = true;
            const double INITIAL_CAPITAL = 120000.0;
            const double POSITION_SIZE = 1800; //300, 600, 900, 1200, 1500, 1800
            const int HOLDING_PERIOD_DAYS = 7; //7
            const bool FORCE_CLOSE_AT_END = true; // SAFETY CHECK SWITCH

            // Role-weighted dynamic sizing (multiplier applied to POSITION_SIZE).
            // Set to 1.0 to disable.
            var roleSizeMultiplier = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                { "Director", 1.2 },
                { "CEO", 1.0 },
                { "Unknown", 1.3 },

                // Lower-priority roles: either reduce size or skip (<= 0)
                { "Chair", 0.9 },
                { "CTO", 0.9 },
                { "General Counsel", 0.9 }
            };

            // Exposure logging controls (to prevent huge output)
            const int EXPOSURE_LOG_EVERY_N_DAYS = 5;

            var pnl = new SimplePnLTracker(INITIAL_CAPITAL);

            ConsoleUtilities.WriteLine($"\n=== FORM 4 P&L SIMULATION (OPEN->OPEN, WALK-FORWARD) ===");
            ConsoleUtilities.WriteLine($"Initial Capital: ${INITIAL_CAPITAL:N0}");
            ConsoleUtilities.WriteLine($"Position Size: ${POSITION_SIZE:N0} per signal");
            ConsoleUtilities.WriteLine($"Holding Period: {HOLDING_PERIOD_DAYS} days (calendar, fixed)");
            ConsoleUtilities.WriteLine($"Max Positions: {(int)(INITIAL_CAPITAL * 0.9 / POSITION_SIZE)} concurrent");

            var json = File.ReadAllText("AllTransactionsForTheYear.json");
            var allTransactions = (JsonConvert.DeserializeObject<List<Form4Transaction>>(json) ??
                                  new List<Form4Transaction>()).OrderBy(t => t.FilingDate).ThenBy(t => t.IssuerTicker).ThenBy(t => t.ReportingOwnerName);

            //allTransactions = allTransactions.Where(_ => _.FilingDate <= DateTime.Parse("12/20/2024")).OrderBy(t => t.FilingDate).ThenBy(t => t.IssuerTicker).ThenBy(t => t.ReportingOwnerName); ;

            var cache = new HashSet<string>();

            // Build qualifying signals (discovery on filing date)
            var signals = new List<IndividualForm4Signal>();
            var transactionTasks = allTransactions.Select(txn =>
            {
                if (Form4Downloader.Filter(txn, out var purchaseValue, out var trade)) return Task.CompletedTask;
                if (trade)
                {
                    var signal = new IndividualForm4Signal
                    {
                        Ticker = txn.IssuerTicker.Trim().ToUpperInvariant(),
                        SignalDate = txn.FilingDate.Date,
                        TransactionDate = txn.TransactionDate,
                        PurchaseValue = purchaseValue,
                        ReportingOwner = txn.ReportingOwnerName,
                        OfficerTitle = txn.OfficerTitle,
                        IsDirector = txn.IsDirector,
                        IsOfficer = txn.IsOfficer,
                        Aff10b5One = txn.Aff10b5One,
                        AccessionNumber = txn.AccessionNumber
                    };

                    if (cache.Add(signal.ToString()))
                        signals.Add(signal);
                }

                return Task.CompletedTask;
            });

            await Task.WhenAll(transactionTasks);

            signals = signals.OrderBy(s => s.SignalDate).ThenBy(s => s.Ticker).ThenBy(t => t.ReportingOwner).ToList();

            ConsoleUtilities.WriteLine($"\nFound {signals.Count} Form 4 signals\n");

            if (signals.Count == 0)
            {
                Assert.Inconclusive("No Form 4 signals found.");
                return;
            }

            DateTime simStart;
            double? vooStartOpen;

            double peakEquity = double.MinValue;
            double maxDrawdownPct = 0.0;
            double maxDrawdownDollars = 0.0;

            double vooPeakEquity = double.MinValue;
            double vooMaxDrawdownPct = 0.0;
            double vooMaxDrawdownDollars = 0.0;

            var strategyEquitySeries = new List<KeyValuePair<DateTime, double>>();
            var vooEquitySeries = new List<KeyValuePair<DateTime, double>>();

            void UpdateDrawdown(DateTime dt)
            {
                var prices2 = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

                var vooOpen = GetTickerOpenPrice("VOO", dt);
                if (!vooOpen.HasValue) return; // no mark today

                prices2["VOO"] = vooOpen.Value;

                foreach (var pos in pnl.GetOpenPositions().Where(p => p.PositionType == PositionType.Form4Signal))
                {
                    var op = GetTickerOpenPrice(pos.Ticker, dt);
                    if (op.HasValue) prices2[pos.Ticker] = op.Value;
                }

                pnl.UpdateEquity(dt, prices2);

                var eq = pnl.CurrentEquity;
                if (eq > peakEquity) peakEquity = eq;

                var ddDollars = peakEquity - eq;
                var ddPct = peakEquity > 0 ? ddDollars / peakEquity : 0;

                if (ddPct > maxDrawdownPct)
                {
                    maxDrawdownPct = ddPct;
                    maxDrawdownDollars = ddDollars;
                }

                // Buy-and-hold VOO drawdown (equity implied by open-to-open return)
                // Uses the same sessions as the strategy (only days where VOO open is available).
                var vooEq = INITIAL_CAPITAL * (vooOpen.Value / vooStartOpen.Value);
                if (vooEq > vooPeakEquity) vooPeakEquity = vooEq;

                var vooDdDollars = vooPeakEquity - vooEq;
                var vooDdPct = vooPeakEquity > 0 ? vooDdDollars / vooPeakEquity : 0;

                if (vooDdPct > vooMaxDrawdownPct)
                {
                    vooMaxDrawdownPct = vooDdPct;
                    vooMaxDrawdownDollars = vooDdDollars;
                }

                strategyEquitySeries.Add(new KeyValuePair<DateTime, double>(dt.Date, eq));
                vooEquitySeries.Add(new KeyValuePair<DateTime, double>(dt.Date, vooEq));
            }

            // Establish simulation start session (first day with VOO OPEN available)
            var simStartCandidate = signals.First().SignalDate.AddDays(-2);
            (simStart, vooStartOpen) = FindFirstSessionWithOpen("VOO", simStartCandidate, lookbackDays: 14);

            if (!vooStartOpen.HasValue)
            {
                Assert.Inconclusive("VOO open price data not available around simulation start.");
                return;
            }

            pnl.InitializeVOOPosition(simStart, vooStartOpen.Value);

            // Log initial exposure
            LogExposureSnapshot(ComputeExposureSnapshot(pnl, simStart));

            // Establish simulation end target: last signal filing date + holding period + buffer.
            var simEndTarget = signals.Last().SignalDate.AddDays(HOLDING_PERIOD_DAYS + 40);

            // Cap to last day with VOO OPEN available (apples-to-apples end mark and baseline at open)
            DateTime simEnd;
            double? vooEndOpen;
            (simEnd, vooEndOpen) = FindLastSessionWithOpen("VOO", simEndTarget, lookbackDays: 90);

            if (!vooEndOpen.HasValue)
            {
                Assert.Inconclusive("VOO open price data not available for simulation end window.");
                return;
            }

            // Group signals by filing date for daily discovery
            var signalsByDate = signals
                .GroupBy(s => s.SignalDate.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            var pending = new List<PendingEntry>();

            var officerTitleByPositionId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // ✅ FIX: schedule CLOSES BY POSITION ID (lot), not ticker
            // closeSessionDate -> list of positionIds
            var scheduledCloses = new Dictionary<DateTime, List<string>>();

            // ✅ FIX: failed-close handling
            // If a scheduled close can’t execute due to missing OPEN prices, reschedule it forward.
            const int MAX_CLOSE_RETRIES = 5;
            var closeRetryAttemptsByPositionId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var closeRetryRescheduled = 0;
            var closeRetryExceeded = 0;

            // Metrics (split out by actual reason)
            var processedSignals = 0;

            var skippedNoPrice = 0; // couldn’t find entry session open prices OR missing open on entry day
            var skippedNoCloseOpen = 0; // close day missing opens

            var skippedOverlapTicker = 0; // only relevant when allowFollowOnTrades=false
            var skippedNoCapital = 0;
            var skippedNoBaselineVOO = 0;
            var skippedInvalidInputs = 0;
            var skippedOtherOpenFailure = 0;

            var skippedByRoleWeight = 0;
            var roleTradeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var roleSkippedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var closeSessionNotFound = 0; // opened, but bounded scan couldn't find close session

            // Walk forward one calendar day at a time
            for (var day = simStart.Date; day <= simEnd.Date; day = day.AddDays(1))
            {
                // 0) Execute scheduled closes for today at OPEN (if any)
                if (scheduledCloses.TryGetValue(day, out var positionsToClose))
                {
                    // Copy to avoid modification issues
                    foreach (var positionId in positionsToClose.ToList())
                    {
                        // We must know which ticker this lot is for
                        var pos = pnl.GetOpenPositions()
                            .FirstOrDefault(p => p.PositionType == PositionType.Form4Signal &&
                                                 string.Equals(p.PositionId, positionId,
                                                     StringComparison.OrdinalIgnoreCase));

                        if (pos == null)
                        {
                            // Already closed or never existed; remove from schedule
                            positionsToClose.Remove(positionId);
                            continue;
                        }

                        var ticker = pos.Ticker;

                        if (!HasOpenPrices(day, "VOO", ticker))
                        {
                            skippedNoCloseOpen++;

                            if (!closeRetryAttemptsByPositionId.TryGetValue(positionId, out var attempts))
                                attempts = 0;

                            attempts++;
                            closeRetryAttemptsByPositionId[positionId] = attempts;

                            if (attempts > MAX_CLOSE_RETRIES)
                            {
                                closeRetryExceeded++;
                                ConsoleUtilities.WriteLine(
                                    $"[{day:yyyy-MM-dd}] CLOSE FAILED (giving up after {MAX_CLOSE_RETRIES} retries) | " +
                                    $"PositionId={positionId} Ticker={ticker} Missing OPEN for VOO and/or {ticker}. " +
                                    "Leaving open for FORCE_CLOSE_AT_END."
                                );

                                // Remove from schedule so it doesn't linger on a past date forever.
                                positionsToClose.Remove(positionId);
                                continue;
                            }

                            var next = FindNextTradingDayWithOpenPrices(day, "VOO", ticker);
                            if (next.HasValue)
                            {
                                if (!scheduledCloses.TryGetValue(next.Value.Date, out var nextList))
                                {
                                    nextList = new List<string>();
                                    scheduledCloses[next.Value.Date] = nextList;
                                }

                                nextList.Add(positionId);
                                positionsToClose.Remove(positionId);
                                closeRetryRescheduled++;

                                ConsoleUtilities.WriteLine(
                                    $"[{day:yyyy-MM-dd}] CLOSE RESCHEDULED | PositionId={positionId} Ticker={ticker} " +
                                    $"Attempt={attempts}/{MAX_CLOSE_RETRIES} -> {next.Value:yyyy-MM-dd}"
                                );
                            }
                            else
                            {
                                // Can't find a next valid session in bounded scan; let FORCE_CLOSE_AT_END handle it.
                                positionsToClose.Remove(positionId);

                                ConsoleUtilities.WriteLine(
                                    $"[{day:yyyy-MM-dd}] CLOSE FAILED (no next session found) | PositionId={positionId} " +
                                    $"Ticker={ticker} Attempt={attempts}/{MAX_CLOSE_RETRIES}. Leaving open for FORCE_CLOSE_AT_END."
                                );
                            }

                            continue;
                        }

                        var vooOpen = GetTickerOpenPrice("VOO", day).Value;
                        var exitOpen = GetTickerOpenPrice(ticker, day).Value;

                        // ✅ FIX: close THIS LOT, not "whatever lot matches ticker"
                        pnl.CloseForm4Position(
                            day,
                            positionId,
                            exitOpen,
                            vooOpen,
                            $"{HOLDING_PERIOD_DAYS}d Hold Complete (Open)"
                        );

                        positionsToClose.Remove(positionId);
                        closeRetryAttemptsByPositionId.Remove(positionId);
                    }

                    if (positionsToClose.Count == 0)
                        scheduledCloses.Remove(day);
                }

                // 1) Discover signals on filing date and schedule the entry session
                if (signalsByDate.TryGetValue(day, out var todaysSignals))
                {
                    foreach (var s in todaysSignals)
                    {
                        var entrySession = FindNextTradingDayWithOpenPrices(s.SignalDate, "VOO", s.Ticker);
                        if (!entrySession.HasValue)
                        {
                            skippedNoPrice++;
                            continue;
                        }

                        pending.Add(new PendingEntry
                        {
                            Signal = s,
                            EntryDate = entrySession.Value.Date
                        });
                    }
                }

                // 2) Open entries scheduled for today at OPEN
                var todaysEntries = pending
                    .Where(p => p.EntryDate.Date == day)
                    .OrderBy(p => p.Signal.Ticker, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.Signal.SignalDate)
                    .ToList();

                foreach (var entry in todaysEntries)
                {
                    if (!HasOpenPrices(day, "VOO", entry.Signal.Ticker))
                    {
                        skippedNoPrice++;
                        pending.Remove(entry);
                        continue;
                    }

                    var vooOpen = GetTickerOpenPrice("VOO", day).Value;
                    var signalOpen = GetTickerOpenPrice(entry.Signal.Ticker, day).Value;

                    var rawOfficerTitle = entry.Signal.OfficerTitle;
                    if (string.IsNullOrWhiteSpace(rawOfficerTitle))
                        rawOfficerTitle = entry.Signal.IsOfficer ? "Officer" : (entry.Signal.IsDirector ? "Director" : "Owner");

                    var canonicalTitle = CanonicalizeOfficerTitle(rawOfficerTitle);
                    if (!roleTradeCounts.ContainsKey(canonicalTitle)) roleTradeCounts[canonicalTitle] = 0;
                    roleTradeCounts[canonicalTitle]++;

                    var mult = 1.0;
                    if (roleSizeMultiplier.TryGetValue(canonicalTitle, out var m))
                        mult = m;

                    // If role multiplier is <= 0, skip entirely.
                    if (mult <= 0)
                    {
                        skippedByRoleWeight++;
                        if (!roleSkippedCounts.ContainsKey(canonicalTitle)) roleSkippedCounts[canonicalTitle] = 0;
                        roleSkippedCounts[canonicalTitle]++;
                        pending.Remove(entry);
                        continue;
                    }

                    var positionSizeForTrade = POSITION_SIZE * mult;

                    // ✅ FIX: capture positionId for scheduling closes
                    var openResult = pnl.OpenForm4Position(
                        day,
                        entry.Signal.Ticker,
                        signalOpen,
                        positionSizeForTrade,
                        vooOpen,
                        allowFollowOnTrades,
                        out var positionId
                    );

                    if (openResult == OpenForm4PositionResult.Opened)
                    {
                        processedSignals++;

                        officerTitleByPositionId[positionId] = canonicalTitle;

                        var eligibleCloseDate = day.AddDays(HOLDING_PERIOD_DAYS);

                        // ✅ Close scheduling still needs price availability for VOO + underlying.
                        // But now we schedule by positionId once we know the close session.
                        var closeSession =
                            FindTradingDayOnOrAfterWithOpenPrices(eligibleCloseDate, "VOO", entry.Signal.Ticker);

                        if (closeSession.HasValue)
                        {
                            if (!scheduledCloses.TryGetValue(closeSession.Value.Date, out var list))
                            {
                                list = new List<string>();
                                scheduledCloses[closeSession.Value.Date] = list;
                            }

                            list.Add(positionId);
                        }
                        else
                        {
                            closeSessionNotFound++;
                        }
                    }
                    else
                    {
                        switch (openResult)
                        {
                            case OpenForm4PositionResult.Skipped_OverlapTicker:
                                skippedOverlapTicker++;
                                break;

                            case OpenForm4PositionResult.Skipped_InsufficientRotatableVOO:
                                skippedNoCapital++;
                                break;

                            case OpenForm4PositionResult.Skipped_NoBaselineVOO:
                                skippedNoBaselineVOO++;
                                break;

                            case OpenForm4PositionResult.Skipped_InvalidInputs:
                                skippedInvalidInputs++;
                                break;

                            default:
                                skippedOtherOpenFailure++;
                                break;
                        }
                    }

                    pending.Remove(entry);
                }

                // Periodic exposure logging
                if (EXPOSURE_LOG_EVERY_N_DAYS > 0)
                {
                    var daysSinceStart = (day - simStart.Date).Days;
                    if (daysSinceStart >= 0 && daysSinceStart % EXPOSURE_LOG_EVERY_N_DAYS == 0)
                        LogExposureSnapshot(ComputeExposureSnapshot(pnl, day));
                }

                UpdateDrawdown(day);
            }

            Console.Beep();

            if (FORCE_CLOSE_AT_END)
            {
                var openForm4Positions = pnl.GetOpenPositions()
                    .Where(p => p.PositionType == PositionType.Form4Signal)
                    .ToList();

                foreach (var pos in openForm4Positions)
                {
                    // Defensive: price must exist
                    var exitOpen = GetTickerOpenPrice(pos.Ticker, simEnd);
                    var vooOpen = GetTickerOpenPrice("VOO", simEnd);

                    if (!exitOpen.HasValue || !vooOpen.HasValue)
                    {
                        // Log once if you want, but do NOT invent prices
                        continue;
                    }

                    pnl.CloseForm4Position(
                        simEnd,
                        pos.PositionId,
                        exitOpen.Value,
                        vooOpen.Value,
                        "FORCED_LIQUIDATION_SAFETY"
                    );

                    closeSessionNotFound--;
                }
            }

            Console.Beep();

            // Mark-to-market at end OPEN (apples-to-apples)
            pnl.UpdateEquity(simEnd, new Dictionary<string, double> { { "VOO", vooEndOpen.Value } });

            var summary = pnl.GetSummary();

            ConsoleUtilities.WriteLine($"\n=== FINAL P&L SUMMARY ===");

            ConsoleUtilities.WriteLine($"\n=== FORM 4 P&L SIMULATION (OPEN->OPEN, WALK-FORWARD) ===");
            ConsoleUtilities.WriteLine($"Initial Capital: ${INITIAL_CAPITAL:N0}");
            ConsoleUtilities.WriteLine($"Period: {simStart:yyyy-MM-dd} to {simEnd:yyyy-MM-dd} (OPEN->OPEN)");
            ConsoleUtilities.WriteLine($"Position Size: ${POSITION_SIZE:N0} per signal");
            ConsoleUtilities.WriteLine($"Holding Period: {HOLDING_PERIOD_DAYS} days (calendar, fixed)");
            ConsoleUtilities.WriteLine($"Max Positions: {(int)(INITIAL_CAPITAL * 0.9 / POSITION_SIZE)} concurrent");
            ConsoleUtilities.WriteLine($"Signals Executed: {processedSignals} / {signals.Count}");

            ConsoleUtilities.WriteLine($"\n=== SKIP REASONS (SPLIT) ===");
            ConsoleUtilities.WriteLine($"Skipped (No Entry/Open Price Data): {skippedNoPrice}");
            ConsoleUtilities.WriteLine($"Skipped (Overlap Ticker Already Open): {skippedOverlapTicker}");
            ConsoleUtilities.WriteLine($"Skipped (Insufficient Rotatable VOO / No Capital): {skippedNoCapital}");
            ConsoleUtilities.WriteLine($"Skipped (No Baseline VOO Position): {skippedNoBaselineVOO}");
            ConsoleUtilities.WriteLine($"Skipped (Invalid Inputs): {skippedInvalidInputs}");
            ConsoleUtilities.WriteLine($"Skipped (Other Open Failure): {skippedOtherOpenFailure}");
            ConsoleUtilities.WriteLine($"Skipped (No Close Open): {skippedNoCloseOpen}");
            ConsoleUtilities.WriteLine($"Skipped (Role Weight Filter): {skippedByRoleWeight}");
            ConsoleUtilities.WriteLine($"Opened But Close Session Not Found (bounded scan): {closeSessionNotFound}");
            ConsoleUtilities.WriteLine($"Close Rescheduled (missing open): {closeRetryRescheduled}");
            ConsoleUtilities.WriteLine($"Close Retry Limit Exceeded: {closeRetryExceeded}");

            ConsoleUtilities.WriteLine($"");
            ConsoleUtilities.WriteLine($"Initial Capital: ${summary.InitialCapital:N0}");
            ConsoleUtilities.WriteLine($"Final Equity: ${summary.CurrentEquity:N0}");
            ConsoleUtilities.WriteLine($"Total P&L: ${summary.TotalPnL:N0} ({summary.ReturnPct:P2})");

            // 1) Equity with ONLY VOO priced (current behavior)
            pnl.UpdateEquity(simEnd, new Dictionary<string, double> { { "VOO", vooEndOpen.Value } });
            var equityVooOnly = pnl.CurrentEquity;

            // 2) Equity with ALL open tickers priced at simEnd OPEN
            var prices = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                { "VOO", vooEndOpen.Value }
            };

            foreach (var pos in pnl.GetOpenPositions().Where(p => p.PositionType == PositionType.Form4Signal))
            {
                var open = GetTickerOpenPrice(pos.Ticker, simEnd);
                if (open.HasValue)
                    prices[pos.Ticker] = open.Value;
            }

            pnl.UpdateEquity(simEnd, prices);
            var equityFullMtm = pnl.CurrentEquity;

            ConsoleUtilities.WriteLine($"END CHECK: Equity VOO-only:  ${equityVooOnly:N0}");
            ConsoleUtilities.WriteLine($"END CHECK: Equity Full-MTM:  ${equityFullMtm:N0}");
            ConsoleUtilities.WriteLine($"END CHECK: Difference:      ${(equityFullMtm - equityVooOnly):N0}");

            // Baseline buy-and-hold VOO: OPEN -> OPEN on the same sessions
            var vooReturn = (vooEndOpen.Value - vooStartOpen.Value) / vooStartOpen.Value;
            var vooFinalEquity = INITIAL_CAPITAL * (1 + vooReturn);
            var vooProfit = vooFinalEquity - INITIAL_CAPITAL;
            var outperformance = summary.TotalPnL - vooProfit;

            ConsoleUtilities.WriteLine($"\n=== VS BUY-AND-HOLD VOO (OPEN->OPEN) ===");
            ConsoleUtilities.WriteLine($"VOO Return: {vooReturn:P2}");
            ConsoleUtilities.WriteLine($"VOO Final: ${vooFinalEquity:N0} (${vooProfit:N0} profit)");
            ConsoleUtilities.WriteLine($"Strategy Outperformance: ${outperformance:N0}");
            ConsoleUtilities.WriteLine($"Alpha: {(summary.ReturnPct - vooReturn):P2}");

            ConsoleUtilities.WriteLine($"\n=== METRICS ===");

            var strategyDailyRets = ComputeDailyReturns(strategyEquitySeries);
            var vooDailyRets = ComputeDailyReturns(vooEquitySeries);

            var strategySharpeEq = ComputeAnnualizedSharpe(strategyDailyRets);
            var vooSharpeEq = ComputeAnnualizedSharpe(vooDailyRets);

            var strategyCagr = ComputeCagr(strategyEquitySeries);
            var vooCagr = ComputeCagr(vooEquitySeries);

            var strategyCalmar = ComputeCalmarRatio(strategyCagr, maxDrawdownPct);
            var vooCalmar = ComputeCalmarRatio(vooCagr, vooMaxDrawdownPct);

            var activeDailyRets = ComputeActiveReturns(strategyDailyRets, vooDailyRets);
            var infoRatio = ComputeAnnualizedInformationRatio(activeDailyRets);
            var trackingError = ComputeAnnualizedTrackingError(activeDailyRets);

            ConsoleUtilities.WriteLine($"\n=== SHARPE (Equity-based, Daily OPEN->OPEN, 252) ===");
            ConsoleUtilities.WriteLine($"Strategy Sharpe: {strategySharpeEq:F3}  (N={strategyDailyRets.Count} daily rets)");
            ConsoleUtilities.WriteLine($"VOO Sharpe:      {vooSharpeEq:F3}  (N={vooDailyRets.Count} daily rets)");

            ConsoleUtilities.WriteLine($"\n=== CAGR (Equity-based) ===");
            ConsoleUtilities.WriteLine($"Strategy CAGR: {strategyCagr:P2}");
            ConsoleUtilities.WriteLine($"VOO CAGR:      {vooCagr:P2}");

            ConsoleUtilities.WriteLine($"\n=== CALMAR (CAGR / MaxDD) ===");
            ConsoleUtilities.WriteLine($"Strategy Calmar: {strategyCalmar:F3}");
            ConsoleUtilities.WriteLine($"VOO Calmar:      {vooCalmar:F3}");

            ConsoleUtilities.WriteLine($"\n=== ACTIVE (vs VOO, Daily OPEN->OPEN, 252) ===");
            ConsoleUtilities.WriteLine($"Information Ratio: {infoRatio:F3}  (N={activeDailyRets.Count} active rets)");
            ConsoleUtilities.WriteLine($"Tracking Error:    {trackingError:P2}");

            // ==========================
            // Risk / stats on realized Form4 trades
            // ==========================
            var trades = pnl.GetTradeHistory();

            var profitByOfficerTitle = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var tradesByOfficerTitle = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in trades)
            {
                if (t.TradeType != TradeType.Sell) continue;
                if (!t.PnL.HasValue) continue;
                if (string.Equals(t.Ticker, "VOO", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrWhiteSpace(t.PositionId)) continue;

                if (!officerTitleByPositionId.TryGetValue(t.PositionId, out var title) || string.IsNullOrWhiteSpace(title))
                    title = "Unknown";

                if (!profitByOfficerTitle.ContainsKey(title)) profitByOfficerTitle[title] = 0.0;
                if (!tradesByOfficerTitle.ContainsKey(title)) tradesByOfficerTitle[title] = 0;

                profitByOfficerTitle[title] += t.PnL.Value;
                tradesByOfficerTitle[title]++;
            }

            // Realized Form4 closes (exclude VOO legs)
            var form4Closes = trades
                .Where(t => t.TradeType == TradeType.Sell &&
                            t.PnLPercent.HasValue &&
                            !string.Equals(t.Ticker, "VOO", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (profitByOfficerTitle.Count > 0)
            {
                ConsoleUtilities.WriteLine($"\n=== PROFIT BY OFFICER TITLE (Realized Form4 Closes) ===");
                foreach (var kv in profitByOfficerTitle.OrderByDescending(kv => kv.Value))
                {
                    var title = kv.Key;
                    var profit = kv.Value;
                    var n = tradesByOfficerTitle.TryGetValue(title, out var count) ? count : 0;
                    ConsoleUtilities.WriteLine($"{title,-30}  Trades: {n,4}  P&L: ${profit,12:N0}");
                }
            }

            if (roleTradeCounts.Count > 0)
            {
                ConsoleUtilities.WriteLine($"\n=== ROLE-WEIGHTED SIZING (Counts) ===");
                foreach (var kv in roleTradeCounts.OrderByDescending(kv => kv.Value))
                {
                    var title = kv.Key;
                    var n = kv.Value;
                    var skipped = roleSkippedCounts.TryGetValue(title, out var s) ? s : 0;
                    ConsoleUtilities.WriteLine($"{title,-30}  Seen: {n,4}  Skipped: {skipped,4}");
                }
            }

            var rets = form4Closes.Select(t => t.PnLPercent.Value).ToList();

            ConsoleUtilities.WriteLine($"\n=== RISK / STATS (Realized Form4 Closes) ===");
            ConsoleUtilities.WriteLine($"N (closed Form4 trades): {rets.Count}");

            if (rets.Count < 2)
            {
                ConsoleUtilities.WriteLine("Not enough closed trades for Sharpe/Sortino/t-test.");
            }
            else
            {
                // Profit Factor
                var grossProfit = form4Closes.Where(t => t.PnL.Value > 0).Sum(t => t.PnL.Value);
                var grossLossAbs = form4Closes.Where(t => t.PnL.Value < 0).Sum(t => Math.Abs(t.PnL.Value));
                var profitFactor = grossLossAbs > 0 ? grossProfit / grossLossAbs : double.PositiveInfinity;

                // Mean / stdev of returns (sample stdev)
                var mean = rets.Average();
                var stdev = SampleStdDev(rets);

                // Sharpe (Rf = 0) on trade returns
                var sharpe = stdev > 0 ? mean / stdev : double.PositiveInfinity;

                // Sortino (MAR = 0)
                var downsideDev = DownsideDeviation(rets, mar: 0.0);
                var sortino = downsideDev > 0 ? mean / downsideDev : double.PositiveInfinity;

                // One-sample t-test of mean return vs 0
                // H0: mu = 0
                // two-sided p-value: 2 * (1 - CDF(|t|))
                // one-sided (mu > 0): 1 - CDF(t)
                var n = rets.Count;
                var se = stdev / Math.Sqrt(n);
                var tStat = se > 0 ? mean / se : double.PositiveInfinity;
                var df = n - 1;

                var cdfSigned = SafeTCdf(tStat, df);
                var pOneSidedGreater = Math.Max(0.0, 1.0 - cdfSigned);

                var cdfAbs = SafeTCdf(Math.Abs(tStat), df);
                var tailAbs = Math.Max(0.0, 1.0 - cdfAbs);
                var pTwoSided = Math.Min(1.0, 2.0 * tailAbs);

                ConsoleUtilities.WriteLine($"Max Drawdown: ${maxDrawdownDollars:N0} ({maxDrawdownPct:P2})");
                ConsoleUtilities.WriteLine($"VOO Max Drawdown: ${vooMaxDrawdownDollars:N0} ({vooMaxDrawdownPct:P2})");
                ConsoleUtilities.WriteLine($"Mean return/trade: {mean:P4}");
                ConsoleUtilities.WriteLine($"StdDev return/trade: {stdev:P4}");
                ConsoleUtilities.WriteLine($"Profit Factor: {profitFactor:F3}");
                ConsoleUtilities.WriteLine($"Sharpe (trade-based, Rf=0): {sharpe:F3}");
                ConsoleUtilities.WriteLine($"Sortino (trade-based, MAR=0): {sortino:F3}");
                ConsoleUtilities.WriteLine($"t-stat (mu=0): {tStat:F4}  df={df}");
                ConsoleUtilities.WriteLine($"p-value (one-sided, mu>0): {pOneSidedGreater:E4}");
                ConsoleUtilities.WriteLine($"p-value (two-sided): {pTwoSided:E4}");
            }

            Console.Beep();
        }

        private static List<double> ComputeDailyReturns(List<KeyValuePair<DateTime, double>> equitySeries)
        {
            if (equitySeries == null || equitySeries.Count < 2)
                return new List<double>();

            var ordered = equitySeries
                .GroupBy(kvp => kvp.Key.Date)
                .Select(g => g.OrderBy(x => x.Key).Last())
                .OrderBy(kvp => kvp.Key)
                .ToList();

            var rets = new List<double>(Math.Max(0, ordered.Count - 1));

            for (int i = 1; i < ordered.Count; i++)
            {
                var prev = ordered[i - 1].Value;
                var cur = ordered[i].Value;

                if (prev <= 0) continue;

                rets.Add((cur - prev) / prev);
            }

            return rets;
        }

        private static double ComputeAnnualizedSharpe(IReadOnlyList<double> dailyReturns)
        {
            if (dailyReturns == null || dailyReturns.Count < 2)
                return double.NaN;

            var mean = dailyReturns.Average();
            var stdev = SampleStdDev(dailyReturns);
            if (stdev <= 0)
                return double.PositiveInfinity;

            const double TRADING_DAYS = 252.0;
            return (mean / stdev) * Math.Sqrt(TRADING_DAYS);
        }

        private static string CanonicalizeOfficerTitle(string officerTitle)
        {
            if (string.IsNullOrWhiteSpace(officerTitle))
                return "Unknown";

            // Normalize
            var raw = officerTitle.Trim();
            var upper = raw.ToUpperInvariant();

            // Quick kill: remarks/unknown garbage
            if (upper.Contains("SEE REMARK") || upper.Contains("SEE REMARKS"))
                return "Unknown";

            // Make a safer “token” version (strip punctuation -> spaces, collapse whitespace)
            var cleaned = Regex.Replace(upper, @"[^A-Z0-9]+", " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            bool HasToken(params string[] tokens)
            {
                foreach (var tok in tokens)
                {
                    if (string.IsNullOrWhiteSpace(tok)) continue;
                    // whole-token match (avoids VP matching inside other words)
                    if (Regex.IsMatch(cleaned, $@"\b{Regex.Escape(tok.ToUpperInvariant())}\b"))
                        return true;
                }
                return false;
            }

            // Special handling: VP vs President
            bool isVP = HasToken("VP") || HasToken("SVP") || HasToken("EVP") || cleaned.Contains("VICE PRESIDENT");

            // --- Canonical buckets ---
            // You can choose whether "Director" should win even when CEO exists.
            // Given your results, I'd keep Director separate:
            if (HasToken("DIRECTOR")) return "Director";

            // CEO bucket (covers Chief Exec Officer / CEO)
            if (cleaned.Contains("CHIEF EXECUTIVE") || HasToken("CEO") || cleaned.Contains("CHIEF EXEC"))
                return "CEO";

            // Chair / Executive Chair bucket
            if (cleaned.Contains("EXECUTIVE CHAIR") || cleaned.Contains("EXECUTIVE CHAIRMAN") ||
                HasToken("CHAIRMAN") || HasToken("CHAIR"))
                return "Chair";

            if (HasToken("COO") || cleaned.Contains("CHIEF OPERATING"))
                return "COO";

            if (HasToken("CFO") || cleaned.Contains("CHIEF FINANCIAL") || HasToken("TREASURER"))
                return "CFO";

            if (HasToken("CTO") || cleaned.Contains("CHIEF TECHNOLOGY"))
                return "CTO";

            if (HasToken("CMO") || cleaned.Contains("CHIEF MARKETING") || cleaned.Contains("CHIEF MEDICAL"))
                return "Other C-Suite";

            if (HasToken("CIO") || cleaned.Contains("CHIEF INVESTMENT"))
                return "CIO";

            if (cleaned.Contains("GENERAL COUNSEL"))
                return "General Counsel";

            if (isVP)
                return "VP";

            // President (only if it’s not Vice President)
            if (HasToken("PRESIDENT"))
                return "President";

            return "Other";
        }


        private static double ComputeCagr(List<KeyValuePair<DateTime, double>> equitySeries)
        {
            if (equitySeries == null || equitySeries.Count < 2)
                return double.NaN;

            var ordered = equitySeries
                .GroupBy(kvp => kvp.Key.Date)
                .Select(g => g.OrderBy(x => x.Key).Last())
                .OrderBy(kvp => kvp.Key)
                .ToList();

            if (ordered.Count < 2)
                return double.NaN;

            var start = ordered.First();
            var end = ordered.Last();

            if (start.Value <= 0 || end.Value <= 0)
                return double.NaN;

            var years = (end.Key - start.Key).TotalDays / 365.25;
            if (years <= 0)
                return double.NaN;

            return Math.Pow(end.Value / start.Value, 1.0 / years) - 1.0;
        }

        private static double ComputeCalmarRatio(double cagr, double maxDrawdownPct)
        {
            if (double.IsNaN(cagr) || double.IsInfinity(cagr))
                return double.NaN;
            if (maxDrawdownPct <= 0)
                return double.PositiveInfinity;

            return cagr / maxDrawdownPct;
        }

        private static List<double> ComputeActiveReturns(IReadOnlyList<double> strategyDailyReturns,
            IReadOnlyList<double> benchmarkDailyReturns)
        {
            if (strategyDailyReturns == null || benchmarkDailyReturns == null)
                return new List<double>();

            var n = Math.Min(strategyDailyReturns.Count, benchmarkDailyReturns.Count);
            if (n < 1)
                return new List<double>();

            var active = new List<double>(n);
            for (int i = 0; i < n; i++)
                active.Add(strategyDailyReturns[i] - benchmarkDailyReturns[i]);

            return active;
        }

        private static double ComputeAnnualizedTrackingError(IReadOnlyList<double> activeDailyReturns)
        {
            if (activeDailyReturns == null || activeDailyReturns.Count < 2)
                return double.NaN;

            var stdev = SampleStdDev(activeDailyReturns);
            if (stdev <= 0)
                return 0.0;

            const double TRADING_DAYS = 252.0;
            return stdev * Math.Sqrt(TRADING_DAYS);
        }

        private static double ComputeAnnualizedInformationRatio(IReadOnlyList<double> activeDailyReturns)
        {
            if (activeDailyReturns == null || activeDailyReturns.Count < 2)
                return double.NaN;

            var mean = activeDailyReturns.Average();
            var stdev = SampleStdDev(activeDailyReturns);
            if (stdev <= 0)
                return double.PositiveInfinity;

            const double TRADING_DAYS = 252.0;
            return (mean / stdev) * Math.Sqrt(TRADING_DAYS);
        }

        /// <summary>
        /// Analyzes price action following a cluster buying signal using indexed bulk files
        /// </summary>
        private static PriceActionMetrics AnalyzePriceActionAfterSignal(string ticker, DateTime signalDate,
            ClusterMetrics clusterMetrics)
        {
            try
            {
                // Step 1: Resolve bulk directory for stocks
                var bulkDir = BuildSortedFileIndexingTests.ResolveBulkDirForStocks();
                if (string.IsNullOrEmpty(bulkDir))
                {
                    ConsoleUtilities.WriteLine($"    ⚠️  Bulk directory not found for {ticker}");
                    return null;
                }

                // Step 2: Find sorted files for the date range we need (signal date + 90 days)
                var sortedDir = Path.Combine(bulkDir, "Sorted");
                if (!Directory.Exists(sortedDir))
                    sortedDir = bulkDir;

                var endDate = signalDate.AddDays(90);
                var relevantFiles = Directory.GetFiles(sortedDir, "*_Sorted.csv", SearchOption.AllDirectories)
                    .Where(f => f.IndexOf("stocks", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                f.IndexOf("sip", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(f => new { Path = f, Date = ExtractDateFromFilename(f) })
                    .Where(f => f.Date.HasValue && f.Date.Value >= signalDate && f.Date.Value <= endDate)
                    .OrderBy(f => f.Date)
                    .Select(f => f.Path)
                    .ToList();

                if (relevantFiles.Count == 0)
                {
                    ConsoleUtilities.WriteLine(
                        $"    ⚠️  No sorted files found for {ticker} after {signalDate:yyyy-MM-dd}");
                    return null;
                }

                ConsoleUtilities.WriteLine(
                    $"    📂 Found {relevantFiles.Count} relevant sorted files for price lookup");

                // Step 3: Use indexed files to efficiently extract price data for this ticker
                var priceData = new List<PriceDataPoint>();

                foreach (var sortedFile in relevantFiles)
                {
                    try
                    {
                        // Load the index for this file
                        var indexData = BuildSortedFileIndexingTests.LoadFileIndex(sortedFile);
                        if (indexData?.UnderlyingIndex == null)
                        {
                            ConsoleUtilities.WriteLine($"    ⚠️  No index found for {Path.GetFileName(sortedFile)}");
                            continue;
                        }

                        // 🚀 Use the index to efficiently read only this ticker's data
                        var underlyingData = BuildSortedFileIndexingTests.ReadUnderlyingData(sortedFile, ticker);

                        if (underlyingData.Count > 0)
                        {
                            // Parse price data from CSV lines
                            foreach (var line in underlyingData)
                            {
                                var parts = line.Split(',');
                                if (parts.Length < 8) continue;

                                // Format: ticker,volume,open,close,high,low,window_start,transactions
                                if (!long.TryParse(parts[6], out var windowStartNanos)) continue;
                                if (!double.TryParse(parts[3], out var close)) continue;
                                if (!double.TryParse(parts[2], out var open)) continue;
                                if (!double.TryParse(parts[4], out var high)) continue;
                                if (!double.TryParse(parts[5], out var low)) continue;

                                var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(windowStartNanos / 1000000)
                                    .UtcDateTime;

                                priceData.Add(new PriceDataPoint
                                {
                                    Timestamp = timestamp,
                                    Open = open,
                                    High = high,
                                    Low = low,
                                    Close = close
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleUtilities.WriteLine(
                            $"    ⚠️  Error reading {Path.GetFileName(sortedFile)}: {ex.Message}");
                    }
                }

                if (priceData.Count == 0)
                {
                    ConsoleUtilities.WriteLine($"    ⚠️  No price data found for {ticker}");
                    return null;
                }

                // Step 4: Calculate price action metrics
                return CalculatePriceActionMetrics(priceData, signalDate, ticker);
            }
            catch (Exception ex)
            {
                ConsoleUtilities.WriteLine($"    ❌ Error analyzing price action for {ticker}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Calculates forward returns and risk metrics from price data
        /// </summary>
        private static PriceActionMetrics CalculatePriceActionMetrics(List<PriceDataPoint> priceData,
            DateTime signalDate, string ticker)
        {
            // Sort by timestamp
            priceData = priceData.OrderBy(p => p.Timestamp).ToList();

            // Find the closing price on the first tradeable day after the signal (filing) date
            // This is when we would actually be able to buy
            var signalPrice = priceData
                .Where(p => p.Timestamp.Date > signalDate.Date)
                .OrderBy(p => p.Timestamp)
                .FirstOrDefault();

            if (signalPrice == null)
            {
                ConsoleUtilities.WriteLine($"    ⚠️  No price data found on/after signal date for {ticker}");
                return null;
            }

            var metrics = new PriceActionMetrics
            {
                SignalPrice = signalPrice.Close,
                SignalPriceTimestamp = signalPrice.Timestamp
            };

            // Calculate returns at various horizons FROM THE SIGNAL PRICE DATE
            // e.g., 1-day return = price 1 day after signalPrice date
            var horizons = new[] { 1, 7, 14, 30, 45, 60, 90 };
            foreach (var days in horizons)
            {
                // Calculate target date relative to when we bought (signalPrice.Timestamp)
                var targetDate = signalPrice.Timestamp.Date.AddDays(days);
                var futurePrice = priceData
                    .Where(p => p.Timestamp.Date >= targetDate)
                    .OrderBy(p => p.Timestamp)
                    .FirstOrDefault();

                if (futurePrice != null)
                {
                    var returnPct = (futurePrice.Close - signalPrice.Close) / signalPrice.Close;

                    switch (days)
                    {
                        case 1:
                            metrics.Return1Day = returnPct;
                            break;
                        case 7:
                            metrics.Return7Day = returnPct;
                            break;
                        case 14:
                            metrics.Return14Day = returnPct;
                            break;
                        case 30:
                            metrics.Return30Day = returnPct;
                            break;
                        case 45:
                            metrics.Return45Day = returnPct;
                            break;
                        case 60:
                            metrics.Return60Day = returnPct;
                            break;
                        case 90:
                            metrics.Return90Day = returnPct;
                            break;
                    }
                }
            }

            // Calculate max drawdown over 90 days
            var next90DaysPrices = priceData
                .Where(p => p.Timestamp >= signalPrice.Timestamp && p.Timestamp <= signalPrice.Timestamp.AddDays(90))
                .ToList();

            if (next90DaysPrices.Count > 0)
            {
                var peak = signalPrice.Close;
                var maxDrawdown = 0.0;

                foreach (var price in next90DaysPrices)
                {
                    if (price.Close > peak)
                        peak = price.Close;

                    var drawdown = (peak - price.Close) / peak;
                    if (drawdown > maxDrawdown)
                        maxDrawdown = drawdown;
                }

                metrics.MaxDrawdown = maxDrawdown;
            }

            ConsoleUtilities.WriteLine(
                $"    📊 Signal Price: ${signalPrice.Close:F2} at {signalPrice.Timestamp:yyyy-MM-dd HH:mm}");

            return metrics;
        }

        /// <summary>
        /// Extracts date from filename (e.g., "2024-01-15_us_stocks_sip_minute_aggs_Sorted.csv" → 2024-01-15)
        /// </summary>
        private static DateTime? ExtractDateFromFilename(string filename)
        {
            var name = Path.GetFileName(filename);
            var match = Regex.Match(name, @"(\d{4}-\d{2}-\d{2})");

            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date))
                return date;

            return null;
        }

        // ============================================================================
        // P&L TRACKING FOR FORM 4 SIGNALS
        // ============================================================================

        /// <summary>
        /// Get ticker price for a specific date using indexed files (currently returns close).
        /// </summary>
        private static double? GetTickerPrice(string ticker, DateTime date)
        {
            try
            {
                var bulkDir = BuildSortedFileIndexingTests.ResolveBulkDirForStocks();
                if (string.IsNullOrEmpty(bulkDir)) return null;

                var sortedDir = Path.Combine(bulkDir, "Sorted");
                if (!Directory.Exists(sortedDir)) sortedDir = bulkDir;

                var file = Directory.GetFiles(sortedDir, "*_Sorted.csv", SearchOption.AllDirectories)
                    .Select(f => new { Path = f, Date = ExtractDateFromFilename(f) })
                    .Where(f => f.Date.HasValue && f.Date.Value.Date == date.Date)
                    .Select(f => f.Path)
                    .FirstOrDefault();

                if (file == null) return null;

                var indexData = BuildSortedFileIndexingTests.LoadFileIndex(file);
                if (indexData?.UnderlyingIndex == null) return null;

                var tickerData = BuildSortedFileIndexingTests.ReadUnderlyingData(file, ticker);
                if (tickerData.Count == 0) return null;

                var lastLine = tickerData.LastOrDefault();
                if (lastLine == null) return null;

                var parts = lastLine.Split(',');
                if (parts.Length < 4) return null;

                if (double.TryParse(parts[3], out var close))
                    return close;

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get ticker OPEN for a specific date using indexed files.
        /// Uses the first minute bar for that day and returns the "open" column.
        /// </summary>
        private static double? GetTickerOpenPrice(string ticker, DateTime date, int minutesAfterOpen = 0)
        {
            try
            {
                var bulkDir = BuildSortedFileIndexingTests.ResolveBulkDirForStocks();
                if (string.IsNullOrEmpty(bulkDir)) return null;

                var sortedDir = Path.Combine(bulkDir, "Sorted");
                if (!Directory.Exists(sortedDir)) sortedDir = bulkDir;

                var file = Directory.GetFiles(sortedDir, "*_Sorted.csv", SearchOption.AllDirectories)
                    .Select(f => new { Path = f, Date = ExtractDateFromFilename(f) })
                    .Where(f => f.Date.HasValue && f.Date.Value.Date == date.Date)
                    .Select(f => f.Path)
                    .FirstOrDefault();

                if (file == null) return null;

                var indexData = BuildSortedFileIndexingTests.LoadFileIndex(file);
                if (indexData?.UnderlyingIndex == null) return null;

                var tickerData = BuildSortedFileIndexingTests.ReadUnderlyingData(file, ticker);
                if (tickerData.Count == 0) return null;

                // Select the first minute bar at or after market open + offset.
                // Market open is treated as 09:30 local exchange time (consistent with other tests).
                if (minutesAfterOpen < 0) minutesAfterOpen = 0;

                // Windows timezone id. If you ever run this on Linux, you'll need "America/New_York".
                var eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

                // Build the intended *local Eastern* market open time, then convert to a UTC instant.
                var targetEasternLocal = new DateTime(date.Year, date.Month, date.Day, 9, 30, 0, DateTimeKind.Unspecified)
                    .AddMinutes(minutesAfterOpen);

                var targetUtc = TimeZoneInfo.ConvertTimeToUtc(targetEasternLocal, eastern);
                var targetUtcOffset = new DateTimeOffset(targetUtc, TimeSpan.Zero);

                foreach (var line in tickerData)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(',');
                    if (parts.Length < 8) continue; 

                    // Format: ticker,volume,open,close,high,low,window_start,transactions
                    if (!long.TryParse(parts[6], out var windowStartNanos))
                        continue;

                    // nanos -> ms (truncation is fine for minute bars)
                    var utcBar = DateTimeOffset.FromUnixTimeMilliseconds(windowStartNanos / 1_000_000);

                    if (utcBar < targetUtcOffset)
                        continue;

                    if (double.TryParse(parts[2], out var open))
                        return open;

                    return null;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static DateTime? FindNextTradingDayWithPrices(DateTime afterDate, params string[] tickers)
        {
            // Find earliest date strictly AFTER afterDate where all tickers have price data.
            // Bounded scan to avoid infinite loops when bulk data is incomplete.
            for (var d = afterDate.Date.AddDays(1); d <= afterDate.Date.AddDays(10); d = d.AddDays(1))
            {
                var ok = true;
                foreach (var t in tickers)
                {
                    // Close lookup is sufficient as a proxy for "data exists" (file/ticker present)
                    if (!GetTickerPrice(t, d).HasValue)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                    return d;
            }

            return null;
        }

        private static DateTime? FindTradingDayOnOrAfterWithPrices(DateTime targetDate, params string[] tickers)
        {
            // Find earliest date >= targetDate where all tickers have price data.
            for (var d = targetDate.Date; d <= targetDate.Date.AddDays(10); d = d.AddDays(1))
            {
                var ok = true;
                foreach (var t in tickers)
                {
                    if (!GetTickerPrice(t, d).HasValue)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                    return d;
            }

            return null;
        }

        // Abramowitz & Stegun approximation for Normal CDF via erf approximation (stable, .NET 4.7.2 compatible)
        private static double NormalCdf(double x)
        {
            // constants
            const double p = 0.2316419;
            const double b1 = 0.319381530;
            const double b2 = -0.356563782;
            const double b3 = 1.781477937;
            const double b4 = -1.821255978;
            const double b5 = 1.330274429;

            // For very large |x|, avoid underflow/precision issues
            if (x > 8.0) return 1.0;
            if (x < -8.0) return 0.0;

            var t = 1.0 / (1.0 + p * Math.Abs(x));
            var poly = ((((b5 * t + b4) * t + b3) * t + b2) * t + b1) * t;

            var phi = Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI);
            var cdf = 1.0 - phi * poly;

            return x >= 0.0 ? cdf : 1.0 - cdf;
        }

        private static double SafeTCdf(double t, int df)
        {
            // For large df, t-dist ~ Normal; this avoids Gamma overflow in StudentTPdf
            if (df >= 200) // 200 is conservative; your df=2405 definitely qualifies
                return NormalCdf(t);

            var cdf = StudentTCdf(t, df);

            // If numerical issues still occur, fall back
            if (double.IsNaN(cdf) || double.IsInfinity(cdf))
                return NormalCdf(t);

            // Clamp just in case of tiny overshoot
            if (cdf < 0.0) cdf = 0.0;
            if (cdf > 1.0) cdf = 1.0;

            return cdf;
        }

        private static double Clamp01(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            if (x < 0.0) return 0.0;
            if (x > 1.0) return 1.0;
            return x;
        }

        private static double SampleStdDev(IReadOnlyList<double> x)
        {
            if (x == null || x.Count < 2) return 0.0;
            var mean = x.Average();
            var var = x.Select(v => (v - mean) * (v - mean)).Sum() / (x.Count - 1);
            return Math.Sqrt(var);
        }

        private static double DownsideDeviation(IReadOnlyList<double> x, double mar)
        {
            if (x == null || x.Count < 2) return 0.0;

            var downs = x.Select(r => Math.Min(0.0, r - mar)).ToList();
            var ddVar = downs.Select(d => d * d).Average(); // population downside variance
            return Math.Sqrt(ddVar);
        }

        // -----------------------------
        // Student-t CDF (no external libs)
        // Uses numerical integration of the PDF with symmetry.
        // Good enough for backtest stats; if you want ultra-precision, add MathNet.
        // -----------------------------
        private static double StudentTCdf(double t, int df)
        {
            if (df <= 0) throw new ArgumentOutOfRangeException(nameof(df));
            if (double.IsNaN(t)) return double.NaN;
            if (double.IsPositiveInfinity(t)) return 1.0;
            if (double.IsNegativeInfinity(t)) return 0.0;

            // symmetry: CDF(-t) = 1 - CDF(t)
            if (t < 0) return 1.0 - StudentTCdf(-t, df);

            // integrate pdf from 0..t, then add 0.5
            var area = IntegrateSimpson(x => StudentTPdf(x, df), 0.0, t, nEven: 4000);
            var cdf = 0.5 + area;

            // numeric safety
            if (cdf < 0) cdf = 0;
            if (cdf > 1) cdf = 1;
            return cdf;
        }

        private static double StudentTPdf(double t, int df)
        {
            // pdf = gamma((v+1)/2) / (sqrt(v*pi)*gamma(v/2)) * (1 + t^2/v)^(-(v+1)/2)
            var v = (double)df;
            var a = Gamma((v + 1.0) / 2.0);
            var b = Math.Sqrt(v * Math.PI) * Gamma(v / 2.0);
            var c = Math.Pow(1.0 + (t * t) / v, -(v + 1.0) / 2.0);
            return (a / b) * c;
        }

        private static double IntegrateSimpson(Func<double, double> f, double a, double b, int nEven)
        {
            if (nEven < 2) nEven = 2;
            if (nEven % 2 == 1) nEven++; // must be even

            if (a == b) return 0.0;
            if (b < a) return -IntegrateSimpson(f, b, a, nEven);

            var h = (b - a) / nEven;
            var sum1 = 0.0; // odd indices
            var sum2 = 0.0; // even indices

            for (int i = 1; i < nEven; i++)
            {
                var x = a + i * h;
                if (i % 2 == 0) sum2 += f(x);
                else sum1 += f(x);
            }

            return (h / 3.0) * (f(a) + f(b) + 4.0 * sum1 + 2.0 * sum2);
        }

        // -----------------------------
        // Gamma via Lanczos approximation
        // -----------------------------
        private static double Gamma(double z)
        {
            // Reflection formula for z < 0.5
            if (z < 0.5)
                return Math.PI / (Math.Sin(Math.PI * z) * Gamma(1.0 - z));

            // Lanczos coefficients (g=7, n=9)
            double[] p =
            {
                0.99999999999980993,
                676.5203681218851,
                -1259.1392167224028,
                771.32342877765313,
                -176.61502916214059,
                12.507343278686905,
                -0.13857109526572012,
                9.9843695780195716e-6,
                1.5056327351493116e-7
            };

            z -= 1.0;
            double x = p[0];
            for (int i = 1; i < p.Length; i++)
                x += p[i] / (z + i);

            double g = 7.0;
            double t = z + g + 0.5;

            return Math.Sqrt(2.0 * Math.PI) * Math.Pow(t, z + 0.5) * Math.Exp(-t) * x;
        }

        private static (DateTime date, double? open) FindFirstSessionWithOpen(string ticker, DateTime start,
            int lookbackDays)
        {
            for (var i = 0; i <= lookbackDays; i++)
            {
                var d = start.Date.AddDays(-i);
                var open = GetTickerOpenPrice(ticker, d);
                if (open.HasValue)
                    return (d, open);
            }

            return (start.Date, null);
        }

        private static (DateTime date, double? open) FindLastSessionWithOpen(string ticker, DateTime targetEnd,
            int lookbackDays)
        {
            for (var i = 0; i <= lookbackDays; i++)
            {
                var d = targetEnd.Date.AddDays(-i);
                var open = GetTickerOpenPrice(ticker, d);
                if (open.HasValue)
                    return (d, open);
            }

            return (targetEnd.Date, null);
        }

        /// <summary>
        /// Returns true if we have OPEN prices for all tickers on the given date.
        /// This matches the backtest execution assumption (all fills at the open).
        /// </summary>
        private static bool HasOpenPrices(DateTime date, params string[] tickers)
        {
            foreach (var t in tickers)
            {
                if (!GetTickerOpenPrice(t, date).HasValue)
                    return false;
            }

            return true;
        }

        private static DateTime? FindNextTradingDayWithOpenPrices(DateTime afterDate, params string[] tickers)
        {
            // Find earliest date strictly AFTER afterDate where all tickers have OPEN price data.
            // Bounded scan to avoid infinite loops when bulk data is incomplete.
            for (var d = afterDate.Date.AddDays(1); d <= afterDate.Date.AddDays(30); d = d.AddDays(1))
            {
                if (HasOpenPrices(d, tickers))
                    return d;
            }

            return null;
        }

        private static DateTime? FindTradingDayOnOrAfterWithOpenPrices(DateTime targetDate, params string[] tickers)
        {
            // Find earliest date >= targetDate where all tickers have OPEN price data.
            for (var d = targetDate.Date; d <= targetDate.Date.AddDays(30); d = d.AddDays(1))
            {
                if (HasOpenPrices(d, tickers))
                    return d;
            }

            return null;
        }

        private static ExposureSnapshot ComputeExposureSnapshot(SimplePnLTracker pnl, DateTime date)
        {
            var positions = pnl.GetOpenPositions();

            var cash = pnl.CurrentCash;
            var vooValue = 0.0;
            var form4Value = 0.0;
            var vooCount = 0;
            var form4Count = 0;

            foreach (var p in positions)
            {
                var open = GetTickerOpenPrice(p.Ticker, date);
                if (!open.HasValue)
                    continue; // with "rock solid" prices this won't happen

                var value = p.Shares * open.Value;

                if (p.PositionType == PositionType.Baseline)
                {
                    vooValue += value;
                    vooCount++;
                }
                else
                {
                    form4Value += value;
                    form4Count++;
                }
            }

            return new ExposureSnapshot
            {
                Date = date,
                VooPositions = vooCount,
                Form4Positions = form4Count,
                VooValue = vooValue,
                Form4Value = form4Value,
                Cash = cash
            };
        }

        private static void LogExposureSnapshot(ExposureSnapshot s)
        {
            var total = Math.Max(1e-9, s.Total);
            var vooPct = s.VooValue / total;
            var form4Pct = s.Form4Value / total;
            var cashPct = s.Cash / total;

            ConsoleUtilities.WriteLine(
                $"[{s.Date:yyyy-MM-dd}] Exposure | " +
                $"VOO: {s.VooPositions} pos  ${s.VooValue,10:N0} ({vooPct,6:P1}) | " +
                $"Form4: {s.Form4Positions} pos  ${s.Form4Value,10:N0} ({form4Pct,6:P1}) | " +
                $"Cash: ${s.Cash,8:N0} ({cashPct,6:P1}) | " +
                $"Total: ${s.Total,10:N0}"
            );
        }
    }
}