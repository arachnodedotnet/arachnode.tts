using System;
using System.Linq;

namespace Trade.Indicators
{
    public static class CCIIndicator
    {
        // Ported CCI calculation from MQL5
        // priceBuffer: array of prices (typical price)
        // period: CCI period
        public static double Calculate(int index, int period, double[] priceBuffer)
        {
            if (period <= 0 || index < period - 1 || priceBuffer == null || priceBuffer.Length <= index)
                return 0.0;
            // Calculate Simple Moving Average (SMA)
            var sma = priceBuffer.Skip(index - period + 1).Take(period).Average();
            // Calculate mean deviation
            var meanDeviation = 0.0;
            for (var j = 0; j < period; j++) meanDeviation += Math.Abs(priceBuffer[index - j] - sma);
            meanDeviation *= 0.015 / period;
            // Calculate M
            var m = priceBuffer[index] - sma;
            // Calculate CCI
            if (meanDeviation != 0.0)
                return m / meanDeviation;
            return 0.0;
        }
    }
}