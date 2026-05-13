using System;
using System.Collections.Generic;

namespace Trade
{
    internal partial class Program
    {
        /// <summary>
        /// Event capture class to verify trade execution consistency
        /// </summary>
        private class TradeEventCapture
        {
            public List<TradeEventRecord> EventRecords { get; } = new List<TradeEventRecord>();
            public double RunningBalance { get; private set; }
            public int EventCount { get; private set; }

            public TradeEventCapture(double startingBalance)
            {
                RunningBalance = startingBalance;
            }

            public void CaptureTradeOpen(int index, double price, AllowedTradeType tradeType, 
                AllowedSecurityType securityType, double position, double positionInDollars, int indicatorIndex)
            {
                EventRecords.Add(new TradeEventRecord
                {
                    EventType = "OPEN",
                    Index = index,
                    Price = price,
                    TradeType = tradeType,
                    SecurityType = securityType,
                    Position = position,
                    PositionInDollars = positionInDollars,
                    Balance = RunningBalance,
                    IndicatorIndex = indicatorIndex,
                    Timestamp = DateTime.Now
                });
                EventCount++;
            }

            public void CaptureTradeClose(int index, double price, AllowedTradeType tradeType,
                AllowedSecurityType securityType, double position, double positionInDollars, 
                double dollarGain, double newBalance, int indicatorIndex)
            {
                EventRecords.Add(new TradeEventRecord
                {
                    EventType = "CLOSE",
                    Index = index,
                    Price = price,
                    TradeType = tradeType,
                    SecurityType = securityType,
                    Position = position,
                    PositionInDollars = positionInDollars,
                    DollarGain = dollarGain,
                    Balance = newBalance,
                    IndicatorIndex = indicatorIndex,
                    Timestamp = DateTime.Now
                });
                RunningBalance = newBalance;
                EventCount++;
            }
        }
    }
}