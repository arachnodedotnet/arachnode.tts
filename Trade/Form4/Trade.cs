using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trade.Form4.Enums;
using static Trade.Tests.SECForm4DownloadTests;

namespace Trade.Form4
{
    public class Trade
    {
        public string Ticker { get; set; }
        public TradeType TradeType { get; set; }
        public DateTime Date { get; set; }
        public double Price { get; set; }
        public double Shares { get; set; }
        public double Amount { get; set; }
        public string Reason { get; set; }
        public double? PnL { get; set; }
        public double? PnLPercent { get; set; }
        public int? HoldingPeriodDays { get; set; }
        public string PositionId { get; set; }

        public override string ToString()
        {
            var side = TradeType.ToString().ToUpperInvariant();

            var sharesText = Shares.ToString("N2");
            var priceText = Price.ToString("F2");
            var amtText = Amount.ToString("N2");

            string pnlText = PnL.HasValue
                ? PnL.Value.ToString("N2")
                : "?";

            string pnlPctText = PnLPercent.HasValue
                ? (PnLPercent.Value * 100).ToString("F2") + "%"
                : "?";

            string holdText = HoldingPeriodDays.HasValue
                ? $"{HoldingPeriodDays.Value}d"
                : "?";

            var reasonText = string.IsNullOrWhiteSpace(Reason) ? "n/a" : Reason;

            return
                $"{Date:yyyy-MM-dd} | {Ticker} | {side,-4} | " +
                $"{sharesText} @ ${priceText} = ${amtText} | " +
                $"PnL: ${pnlText} ({pnlPctText}) | " +
                $"Hold: {holdText} | " +
                $"PosId: {PositionId ?? "?"} | " +
                $"Reason: {reasonText}";
        }

    }
}
