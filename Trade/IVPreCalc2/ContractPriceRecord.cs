using System;

namespace Trade.IVPreCalc2
{
    internal sealed class ContractPriceRecord
    {
        public DateTime Timestamp { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public int Volume { get; set; }
        public int Transactions { get; set; }
        public string SourceFile { get; set; }
    }
}