using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trade.Form4
{
    /// <summary>
    /// Represents price action metrics following a signal
    /// </summary>
    public class PriceActionMetrics
    {
        public double SignalPrice { get; set; }
        public DateTime SignalPriceTimestamp { get; set; }

        public double? Return1Day { get; set; }
        public double? Return7Day { get; set; }
        public double? Return14Day { get; set; }
        public double? Return30Day { get; set; }
        public double? Return45Day { get; set; }
        public double? Return60Day { get; set; }
        public double? Return90Day { get; set; }

        public double MaxDrawdown { get; set; }
    }
}
