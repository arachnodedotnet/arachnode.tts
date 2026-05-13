using System;
using System.Linq;

namespace Trade.Indicators
{
    // Static class for Bollinger Bands indicator calculation
    public static class BollingerBands
    {
        // Calculate Bollinger Bands for a price buffer
        // Returns a tuple of arrays: (middle, upper, lower)
        public static (double[] middle, double[] upper, double[] lower) Calculate(double[] price, int period,
            double deviations, int shift = 0)
        {
            var rates_total = price.Length;
            if (rates_total < period)
                return (new double[0], new double[0], new double[0]);
            var middle = new double[rates_total];
            var upper = new double[rates_total];
            var lower = new double[rates_total];
            var stddev = new double[rates_total];
            for (var i = 0; i < rates_total; i++)
                if (i < period - 1)
                {
                    middle[i] = 0.0;
                    stddev[i] = 0.0;
                    upper[i] = 0.0;
                    lower[i] = 0.0;
                }
                else
                {
                    middle[i] = SimpleMA(i, period, price);
                    stddev[i] = StdDevFunc(i, price, middle, period);
                    var shiftedIdx = i + shift;
                    if (shiftedIdx < rates_total)
                    {
                        upper[shiftedIdx] = middle[i] + deviations * stddev[i];
                        lower[shiftedIdx] = middle[i] - deviations * stddev[i];
                        middle[shiftedIdx] = middle[i];
                    }
                }

            return (middle, upper, lower);
        }

        // Helper: Simple Moving Average
        private static double SimpleMA(int position, int period, double[] price)
        {
            if (position < period - 1) return 0.0;
            return price.Skip(position - period + 1).Take(period).Average();
        }

        // Helper: Standard Deviation
        private static double StdDevFunc(int position, double[] price, double[] ma_price, int period)
        {
            var std_dev = 0.0;
            if (position >= period)
            {
                for (var i = 0; i < period; i++)
                    std_dev += Math.Pow(price[position - i] - ma_price[position], 2.0);
                std_dev = Math.Sqrt(std_dev / period);
            }

            return std_dev;
        }
    }
}