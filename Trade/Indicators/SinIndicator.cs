using System;

namespace Trade.Indicators
{
    //HACK: Consolidate duplicate SinIndicator class definitions
    internal partial class Program
    {
        /// <summary>
        ///     Simple Sin indicator implementation for testing and validation
        /// </summary>
        public static class SinIndicator
        {
            public static double Calculate(int index, int length, double param1, double param2, double param3,
                double param4, double param5)
            {
                // Sin formula: param3 + param4 * Math.Sin((index / length) * param1 * Math.PI * param2 + param5)
                var x = (double)index / length * param1 * Math.PI * param2 + param5;
                return param3 + param4 * Math.Sin(x);
            }
        }
    }

    // Static class for Sin indicator calculation
    public static class SinIndicator
    {
        public static double Calculate(int index, int length, double param1, double param2, double param3,
            double param4, double param5)
        {
            var x = (double)index / length * param1 * Math.PI * param2;
            return param3 + param4 * Math.Sin(x + param5);
        }
    }
}