using System;
using Trade.Prices2;

namespace Trade
{
    public class TradeResult
    {
        public int OpenIndex;
        public int CloseIndex;

        private double _openPrice;
        private double _closePrice;

        public double OpenPrice
        {
            get => _openPrice;
            set
            {
                if (value < 0)
                    throw new ArgumentException("OpenPrice cannot be negative", nameof(OpenPrice));
                _openPrice = value;
            }
        }

        public double ClosePrice
        {
            get => _closePrice;
            set
            {
                if (value < 0)
                    throw new ArgumentException("ClosePrice cannot be negative", nameof(ClosePrice));
                _closePrice = value;
            }
        }

        public AllowedActionType AllowedActionType;
        public AllowedTradeType AllowedTradeType;
        public AllowedSecurityType AllowedSecurityType; //HACK: finish this...
        public AllowedOptionType AllowedOptionType;
        public double Position; // Number of shares/units traded
        public double PositionInDollars; // Total dollar amount of the trade

        public double TotalDollarAmount // Compatibility alias for PositionInDollars
        {
            get => PositionInDollars;
            set => PositionInDollars = value;
        }

        public double Balance; // Account balance after this trade is completed
        public int ResponsibleIndicatorIndex; // Which indicator (or combination) triggered this trade
        public PriceRecord PriceRecordForOpen;
        public PriceRecord PriceRecordForClose;

        public double DollarGain
        {
            get
            {
                if (AllowedTradeType == AllowedTradeType.Buy)
                    return ClosePrice - OpenPrice;
                // TradeType.SellShort
                return OpenPrice - ClosePrice;
            }
        }

        public double PercentGain
        {
            get
            {
                if (AllowedTradeType == AllowedTradeType.Buy)
                    return OpenPrice != 0 ? (ClosePrice - OpenPrice) / OpenPrice * 100.0 : 0.0;
                // TradeType.SellShort
                return OpenPrice != 0 ? (OpenPrice - ClosePrice) / OpenPrice * 100.0 : 0.0;
            }
        }

        public double ActualDollarGain =>
            DollarGain * Math.Abs(Position) * (AllowedSecurityType == AllowedSecurityType.Option ? 100 : 1);

        public override string ToString()
        {
            return ActualDollarGain.ToString("C");
        }
    }
}