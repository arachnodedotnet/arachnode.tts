using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trade.Form4
{
    public class PnLSummary
    {
        public double InitialCapital { get; set; }
        public double CurrentEquity { get; set; }
        public double TotalPnL { get; set; }
        public double ReturnPct { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public double WinRate { get; set; }
        public double AveragePnL { get; set; }
        public double AveragePnLPct { get; set; }
        public double LargestWin { get; set; }
        public double LargestLoss { get; set; }
        public double AverageHoldingPeriod { get; set; }
        public double TotalBuyVolume { get; set; }
        public double TotalSellVolume { get; set; }

        public double ReturnPctCalc =>
            InitialCapital == 0 ? 0 : (CurrentEquity - InitialCapital) / InitialCapital;

        public double WinRateCalc =>
            TotalTrades == 0 ? 0 : (double)WinningTrades / TotalTrades;

    }

}
