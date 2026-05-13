using System;

namespace Trade.Indicators
{
    public class ParabolicSARResult
    {
        public double[] AF;
        public double[] EP;
        public double[] SAR;
    }

    public static class ParabolicSAR
    {
        /// <summary>
        ///     Calculates the Parabolic SAR indicator.
        /// </summary>
        /// <param name="high">High price array</param>
        /// <param name="low">Low price array</param>
        /// <param name="step">SAR step (default 0.02)</param>
        /// <param name="maximum">SAR maximum (default 0.2)</param>
        /// <returns>ParabolicSARResult containing SAR, EP, and AF buffers</returns>
        public static ParabolicSARResult Calculate(
            double[] high,
            double[] low,
            double step = 0.02,
            double maximum = 0.2)
        {
            var length = high.Length;
            if (length < 2)
                return null;
            var sar = new double[length];
            var ep = new double[length];
            var af = new double[length];

            var lastRevPos = 0;
            var directionLong = false;

            // Initialization
            af[0] = step;
            af[1] = step;
            sar[0] = high[0];
            lastRevPos = 0;
            directionLong = false;
            sar[1] = GetHigh(1, lastRevPos, high);
            ep[0] = low[1];
            ep[1] = low[1];

            for (var i = 1; i < length - 1; i++)
            {
                // Check for reversal
                if (directionLong)
                {
                    if (sar[i] > low[i])
                    {
                        // Switch to SHORT
                        directionLong = false;
                        sar[i] = GetHigh(i, lastRevPos, high);
                        ep[i] = low[i];
                        lastRevPos = i;
                        af[i] = step;
                    }
                }
                else
                {
                    if (sar[i] < high[i])
                    {
                        // Switch to LONG
                        directionLong = true;
                        sar[i] = GetLow(i, lastRevPos, low);
                        ep[i] = high[i];
                        lastRevPos = i;
                        af[i] = step;
                    }
                }

                // Continue calculations
                if (directionLong)
                {
                    // New High
                    if (high[i] > ep[i - 1] && i != lastRevPos)
                    {
                        ep[i] = high[i];
                        af[i] = af[i - 1] + step;
                        if (af[i] > maximum)
                            af[i] = maximum;
                    }
                    else if (i != lastRevPos)
                    {
                        af[i] = af[i - 1];
                        ep[i] = ep[i - 1];
                    }

                    // Calculate SAR for tomorrow
                    sar[i + 1] = sar[i] + af[i] * (ep[i] - sar[i]);
                    // Check for SAR
                    if (sar[i + 1] > low[i] || sar[i + 1] > low[i - 1])
                        sar[i + 1] = Math.Min(low[i], low[i - 1]);
                }
                else
                {
                    // New Low
                    if (low[i] < ep[i - 1] && i != lastRevPos)
                    {
                        ep[i] = low[i];
                        af[i] = af[i - 1] + step;
                        if (af[i] > maximum)
                            af[i] = maximum;
                    }
                    else if (i != lastRevPos)
                    {
                        af[i] = af[i - 1];
                        ep[i] = ep[i - 1];
                    }

                    // Calculate SAR for tomorrow
                    sar[i + 1] = sar[i] + af[i] * (ep[i] - sar[i]);
                    // Check for SAR
                    if (sar[i + 1] < high[i] || sar[i + 1] < high[i - 1])
                        sar[i + 1] = Math.Max(high[i], high[i - 1]);
                }
            }

            return new ParabolicSARResult
            {
                SAR = sar,
                EP = ep,
                AF = af
            };
        }

        private static double GetHigh(int currPos, int start, double[] high)
        {
            var result = high[start];
            for (var i = start + 1; i <= currPos; i++)
                if (result < high[i])
                    result = high[i];
            return result;
        }

        private static double GetLow(int currPos, int start, double[] low)
        {
            var result = low[start];
            for (var i = start + 1; i <= currPos; i++)
                if (result > low[i])
                    result = low[i];
            return result;
        }
    }
}