using System;

namespace Trade.Indicators
{
    /// <summary>
    /// Result class for Fractals indicator
    /// </summary>
    public class FractalsResult
    {
        public double[] UpperFractal { get; set; }
        public double[] LowerFractal { get; set; }
    }

    /// <summary>
    /// Static class for Fractals indicator calculation (Bill Williams)
    /// </summary>
    public static class Fractals
    {
        public const double EmptyValue = 0;

        /// <summary>
        ///     Calculates upper and lower fractals using Bill Williams' 5-bar fractal method.
        ///     A fractal is a reversal pattern that occurs when a price bar's high or low 
        ///     extends beyond those of at least 2 bars on each side.
        ///     
        ///     Upper Fractal: High[i] > High[i±1] and High[i] > High[i±2] and High[i] >= High[i-1] and High[i] >= High[i-2]
        ///     Lower Fractal: Low[i] < Low[i±1] and Low[i] < Low[i±2] and Low[i] <= Low[i-1] and Low[i] <= Low[i-2]
        /// </summary>
        /// <param name="high">High price array.</param>
        /// <param name="low">Low price array.</param>
        /// <param name="arrowShift">Arrow shift for display purposes (default -10).</param>
        /// <returns>FractalsResult containing upper and lower fractal arrays.</returns>
        public static FractalsResult Calculate(double[] high, double[] low, int arrowShift = -10)
        {
            // Input validation
            if (high == null || low == null)
                return new FractalsResult
                {
                    UpperFractal = new double[0],
                    LowerFractal = new double[0]
                };

            var ratesTotal = Math.Min(high.Length, low.Length);
            var upperFractal = new double[ratesTotal];
            var lowerFractal = new double[ratesTotal];

            // Initialize buffers to EmptyValue (NaN)
            for (var i = 0; i < ratesTotal; i++)
            {
                upperFractal[i] = EmptyValue;
                lowerFractal[i] = EmptyValue;
            }

            // Need at least 5 bars for fractal calculation
            if (ratesTotal < 5)
                return new FractalsResult
                {
                    UpperFractal = upperFractal,
                    LowerFractal = lowerFractal
                };

            // Main calculation loop (5-bar fractals)
            // Start from index 2 and end at ratesTotal-3 to ensure we have 2 bars on each side
            for (var i = 2; i < ratesTotal - 2; i++)
            {
                // Upper Fractal Check
                // Current high must be higher than the next 2 highs (strict inequality)
                // AND current high must be higher than or equal to the previous 2 highs (allows equality)
                if (high[i] > high[i + 1] && high[i] > high[i + 2] &&
                    high[i] >= high[i - 1] && high[i] >= high[i - 2])
                {
                    upperFractal[i] = high[i];
                }

                // Lower Fractal Check
                // Current low must be lower than the next 2 lows (strict inequality)
                // AND current low must be lower than or equal to the previous 2 lows (allows equality)
                if (low[i] < low[i + 1] && low[i] < low[i + 2] &&
                    low[i] <= low[i - 1] && low[i] <= low[i - 2])
                {
                    lowerFractal[i] = low[i];
                }
            }

            return new FractalsResult
            {
                UpperFractal = upperFractal,
                LowerFractal = lowerFractal
            };
        }

        /// <summary>
        ///     Calculates fractals returning tuple format (for backward compatibility).
        /// </summary>
        /// <param name="high">High price array.</param>
        /// <param name="low">Low price array.</param>
        /// <param name="arrowShift">Arrow shift for display purposes (default -10).</param>
        /// <returns>Tuple of (upperFractal, lowerFractal) arrays.</returns>
        public static (double[] upperFractal, double[] lowerFractal) CalculateTuple(
            double[] high,
            double[] low,
            int arrowShift = -10)
        {
            var result = Calculate(high, low, arrowShift);
            return (result.UpperFractal, result.LowerFractal);
        }
    }
}