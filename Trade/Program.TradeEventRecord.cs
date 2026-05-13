using System;

namespace Trade
{
    internal partial class Program
    {
        /// <summary>
        /// Record of a single trade event for verification
        /// </summary>
        private struct TradeEventRecord
        {
            public string EventType;
            public int Index;
            public double Price;
            public AllowedTradeType TradeType;
            public AllowedSecurityType SecurityType;
            public double Position;
            public double PositionInDollars;
            public double DollarGain;
            public double Balance;
            public int IndicatorIndex;
            public DateTime Timestamp;
        }
    }
}