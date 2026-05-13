using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trade.Form4.Enums;

namespace Trade.Form4
{
    public class Position
    {
        public string Ticker { get; set; }
        public DateTime EntryDate { get; set; }
        public double EntryPrice { get; set; }
        public double Shares { get; set; }
        public double CostBasis { get; set; }
        public double CostBasisIncludingFees { get; set; }
        public double Fees { get; set; }
        public PositionType PositionType { get; set; }
        public string PositionId { get; set; }

        public double CostBasisComp => EntryPrice * Shares;
        public double CostBasisIncludingFeesComp => (EntryPrice * Shares) + Fees;

    }
}


