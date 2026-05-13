using System.Linq;

namespace Trade.Indicators
{
    // Static class for Bill Williams Zone Trade indicator calculation
    public static class BwZoneTrade
    {
        // Calculate Zone Trade colors for candles
        // Returns an array of color codes: 0=Green, 1=Red, 2=Gray
        public static double[] Calculate(double[] open, double[] high, double[] low, double[] close, double[] ac,
            double[] ao)
        {
            var rates_total = new[] { open.Length, high.Length, low.Length, close.Length, ac.Length, ao.Length }.Min();
            if (rates_total < 2)
                return new double[0];
            var colors = new double[rates_total];
            for (var i = 1; i < rates_total; i++)
            {
                colors[i] = 2.0; // Default: Gray
                if (ac[i] > ac[i - 1] && ao[i] > ao[i - 1])
                    colors[i] = 0.0; // Green
                else if (ac[i] < ac[i - 1] && ao[i] < ao[i - 1])
                    colors[i] = 1.0; // Red
            }

            colors[0] = 2.0; // First candle is always Gray
            return colors;
        }
    }
}