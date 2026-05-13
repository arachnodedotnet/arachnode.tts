using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trade.Form4.Enums;
using Trade.Tests;
using static Trade.Tests.SECForm4DownloadTests;

namespace Trade.Form4
{
    /// <summary>
    /// Simple P&L tracking with VOO baseline and Form 4 rotation strategy
    /// </summary>
    public class SimplePnLTracker
    {
        private double _cash;
        private readonly List<Position> _openPositions;
        private readonly List<Trade> _tradeHistory;

        public double InitialCapital { get; }
        public double CurrentCash => _cash;
        public double CurrentEquity { get; private set; }
        public double TotalPnL => CurrentEquity - InitialCapital;
        public double ReturnPct => (CurrentEquity - InitialCapital) / InitialCapital;
        public int OpenPositionCount => _openPositions.Count;

        public SimplePnLTracker(double initialCapital)
        {
            InitialCapital = initialCapital;
            _cash = initialCapital;
            CurrentEquity = initialCapital;
            _openPositions = new List<Position>();
            _tradeHistory = new List<Trade>();
        }

        public List<Trade> GetTradeHistory()
        {
            return new List<Trade>(_tradeHistory);
        }

        public void InitializeVOOPosition(DateTime date, double vooPrice)
        {
            if (_openPositions.Count > 0)
                throw new InvalidOperationException("Portfolio already initialized");

            var shares = _cash / vooPrice;
            var position = new Position
            {
                Ticker = "VOO",
                EntryDate = date,
                EntryPrice = vooPrice,
                Shares = shares,
                CostBasis = _cash,
                PositionType = PositionType.Baseline
            };

            _openPositions.Add(position);
            _cash = 0;

            ConsoleUtilities.WriteLine($"💰 Initialized: ${InitialCapital:N0} → {shares:F2} shares VOO @ ${vooPrice:F2}");
        }

        public enum OpenForm4PositionResult
        {
            Opened = 0,

            // Skip reasons (we want these split in metrics)
            Skipped_OverlapTicker = 10,           // already have an open Form4 position for this ticker
            Skipped_NoBaselineVOO = 11,           // VOO baseline not present (shouldn't happen, but guard)
            Skipped_InsufficientRotatableVOO = 12,// VOO value - minVOOToKeep < dollarAmount
            Skipped_InvalidInputs = 13,         // bad args (<=0 price/amount, empty ticker, etc.)
            Skipped_OtherOpenFailure = 14            // 
        }

        public OpenForm4PositionResult OpenForm4Position(
DateTime date,
string ticker,
double price,
double dollarAmount,
double vooPrice,
bool allowFollowOnTrades,
out string trackingId)   // ✅ new out param (position/lot id)
        {
            trackingId = null;

            // -----------------------------
            // Input validation
            // -----------------------------
            if (string.IsNullOrWhiteSpace(ticker) ||
                price <= 0 ||
                dollarAmount <= 0 ||
                vooPrice <= 0)
            {
                trackingId = "Skipped_InvalidInputs";
                return OpenForm4PositionResult.Skipped_InvalidInputs;
            }

            ticker = ticker.Trim().ToUpperInvariant();

            // Create a unique lot id that we can carry through scheduling + close
            // Format is human-readable and stable for logs.
            trackingId = $"{ticker}|OPEN|{date:yyyy-MM-dd}|${dollarAmount:0.##}|@{price:0.####}|{Guid.NewGuid():N}";

            // -----------------------------
            // Enforce "no overlapping positions per ticker" (optional)
            // -----------------------------
            if (!allowFollowOnTrades && _openPositions.Any(p =>
                    p.PositionType == PositionType.Form4Signal &&
                    string.Equals(p.Ticker, ticker, StringComparison.OrdinalIgnoreCase)))
            {
                trackingId = $"{ticker}|Skipped_OverlapTicker|{date:yyyy-MM-dd}";
                return OpenForm4PositionResult.Skipped_OverlapTicker;
            }

            // -----------------------------
            // Require baseline VOO position
            // -----------------------------
            var vooPosition = _openPositions.FirstOrDefault(p =>
                p.Ticker == "VOO" && p.PositionType == PositionType.Baseline);

            if (vooPosition == null)
            {
                trackingId = $"{ticker}|Skipped_NoBaselineVOO|{date:yyyy-MM-dd}";
                return OpenForm4PositionResult.Skipped_NoBaselineVOO;
            }

            // -----------------------------
            // Capital constraints: keep 10% of InitialCapital in VOO
            // -----------------------------
            var vooCurrentValue = vooPosition.Shares * vooPrice;
            var minVOOToKeep = InitialCapital * 0.10;
            var maxAvailableToRotate = vooCurrentValue - minVOOToKeep;

            // Guard against numerical drift / negative availability
            if (maxAvailableToRotate <= 0 || dollarAmount > maxAvailableToRotate)
            {
                trackingId =
                    $"{ticker}|Skipped_InsufficientRotatableVOO|{date:yyyy-MM-dd}|Req={dollarAmount:0.##}|Avail={Math.Max(0, maxAvailableToRotate):0.##}";
                return OpenForm4PositionResult.Skipped_InsufficientRotatableVOO;
            }

            // -----------------------------
            // Execute rotation: sell VOO -> buy signal
            // -----------------------------
            var vooSharesToSell = dollarAmount / vooPrice;

            // Defensive: don't allow VOO shares to go negative due to rounding
            if (vooSharesToSell <= 0 || vooSharesToSell > vooPosition.Shares + 1e-9)
            {
                trackingId = $"{ticker}|Skipped_OtherOpenFailure|{date:yyyy-MM-dd}|VOOShareSellInvalid";
                return OpenForm4PositionResult.Skipped_OtherOpenFailure;
            }

            vooPosition.Shares -= vooSharesToSell;
            _cash += dollarAmount;

            // ✅ optional: attach trackingId to the trade for auditability (requires Trade.PositionId field)
            _tradeHistory.Add(new Trade
            {
                Ticker = "VOO",
                TradeType = TradeType.Sell,
                Date = date,
                Price = vooPrice,
                Shares = vooSharesToSell,
                Amount = dollarAmount,
                Reason = $"Rotate to {ticker}",
                PositionId = trackingId
            });

            var signalShares = dollarAmount / price;

            // ✅ store trackingId on the Position (requires Position.PositionId field)
            _openPositions.Add(new Position
            {
                PositionId = trackingId,
                Ticker = ticker,
                EntryDate = date,
                EntryPrice = price,
                Shares = signalShares,
                CostBasis = dollarAmount,
                PositionType = PositionType.Form4Signal
            });

            _cash -= dollarAmount;

            _tradeHistory.Add(new Trade
            {
                Ticker = ticker,
                TradeType = TradeType.Buy,
                Date = date,
                Price = price,
                Shares = signalShares,
                Amount = dollarAmount,
                Reason = "Form 4 Signal",
                PositionId = trackingId
            });

            ConsoleUtilities.WriteLine(
                $"✅ OPEN {ticker}: {signalShares:F2} @ ${price:F2} = ${dollarAmount:N0} | id={trackingId}");

            return OpenForm4PositionResult.Opened;
        }

        public bool CloseForm4Position(DateTime date, string positionId, double exitPrice, double vooPrice, string exitReason)
        {
            if (string.IsNullOrWhiteSpace(positionId)) return false;

            var position = _openPositions.FirstOrDefault(p =>
                p.PositionType == PositionType.Form4Signal &&
                string.Equals(p.PositionId, positionId, StringComparison.OrdinalIgnoreCase));

            if (position == null) return false;

            var ticker = position.Ticker;

            var proceedsFromSale = position.Shares * exitPrice;
            _cash += proceedsFromSale;

            var pnl = proceedsFromSale - position.CostBasis;
            var pnlPct = position.CostBasis != 0 ? pnl / position.CostBasis : 0;
            var holdingDays = (date - position.EntryDate).Days;

            _tradeHistory.Add(new Trade
            {
                Ticker = ticker,
                TradeType = TradeType.Sell,
                Date = date,
                Price = exitPrice,
                Shares = position.Shares,
                Amount = proceedsFromSale,
                Reason = exitReason,
                PnL = pnl,
                PnLPercent = pnlPct,
                HoldingPeriodDays = holdingDays,
                PositionId = positionId
            });

            _openPositions.Remove(position);

            var emoji = pnl >= 0 ? "✅" : "❌";
            ConsoleUtilities.WriteLine(
                $"{emoji} CLOSE {ticker}: {position.Shares:F2} @ ${exitPrice:F2} | P&L: ${pnl:F2} ({pnlPct:P2}) | {holdingDays}d | {exitReason} | id={positionId}");

            // Rotate back into VOO
            var vooPosition = _openPositions.FirstOrDefault(p => p.Ticker == "VOO" && p.PositionType == PositionType.Baseline);
            if (vooPosition != null)
            {
                var vooSharesToBuy = proceedsFromSale / vooPrice;
                vooPosition.Shares += vooSharesToBuy;
                _cash -= proceedsFromSale;

                _tradeHistory.Add(new Trade
                {
                    Ticker = "VOO",
                    TradeType = TradeType.Buy,
                    Date = date,
                    Price = vooPrice,
                    Shares = vooSharesToBuy,
                    Amount = proceedsFromSale,
                    Reason = $"Rotate from {ticker}",
                    PositionId = positionId // keep linkage
                });
            }

            return true;
        }

        public void UpdateEquity(DateTime date, Dictionary<string, double> prices)
        {
            var totalValue = _cash;

            foreach (var position in _openPositions)
            {
                if (prices.TryGetValue(position.Ticker, out var currentPrice))
                {
                    var positionValue = position.Shares * currentPrice;
                    totalValue += positionValue;
                }
                else
                {
                    totalValue += position.Shares * position.EntryPrice;
                }
            }

            CurrentEquity = totalValue;
        }

        public List<Position> GetOpenPositions()
        {
            return new List<Position>(_openPositions);
        }

        public PnLSummary GetSummary()
        {
            var closedTrades = _tradeHistory.Where(t => t.PnL.HasValue).ToList();

            return new PnLSummary
            {
                InitialCapital = InitialCapital,
                CurrentEquity = CurrentEquity,
                TotalPnL = TotalPnL,
                ReturnPct = ReturnPct,
                TotalTrades = closedTrades.Count,
                WinningTrades = closedTrades.Count(t => t.PnL > 0),
                LosingTrades = closedTrades.Count(t => t.PnL < 0),
                WinRate = closedTrades.Count > 0 ? (double)closedTrades.Count(t => t.PnL > 0) / closedTrades.Count : 0,
                AveragePnL = closedTrades.Count > 0 ? closedTrades.Average(t => t.PnL.Value) : 0,
                AveragePnLPct = closedTrades.Count > 0 ? closedTrades.Average(t => t.PnLPercent.Value) : 0,
                LargestWin = closedTrades.Count > 0 ? closedTrades.Max(t => t.PnL.Value) : 0,
                LargestLoss = closedTrades.Count > 0 ? closedTrades.Min(t => t.PnL.Value) : 0,
                AverageHoldingPeriod = closedTrades.Count > 0 ? closedTrades.Average(t => t.HoldingPeriodDays ?? 0) : 0,
                TotalBuyVolume = _tradeHistory.Where(t => t.TradeType == TradeType.Buy).Sum(t => t.Amount),
                TotalSellVolume = _tradeHistory.Where(t => t.TradeType == TradeType.Sell).Sum(t => t.Amount)
            };
        }
    }
}
