using System;

namespace Trade.Indicators
{
    /// <summary>
    /// Result structure for Gator Oscillator indicator
    /// </summary>
    public struct GatorResult
    {
        public double[] UpperBuffer;
        public double[] UpperColors;
        public double[] LowerBuffer;
        public double[] LowerColors;
        public double[] Jaws;
        public double[] Teeth;
        public double[] Lips;
    }

    /// <summary>
    /// Static class for Gator Oscillator indicator calculation (Bill Williams)
    /// </summary>
    public static class GatorOscillator
    {
        /// <summary>
        ///     Calculates the Gator Oscillator indicator developed by Bill Williams.
        ///     The Gator Oscillator is a histogram that shows the relationship between 
        ///     the Alligator's three moving averages (Jaws, Teeth, and Lips).
        ///     
        ///     Formula:
        ///     - Upper Histogram = |Jaws - Teeth|
        ///     - Lower Histogram = -|Teeth - Lips|
        ///     - Colors indicate expansion (green) or contraction (red)
        ///     
        ///     The oscillator helps identify when the Alligator is waking up (expanding)
        ///     or going to sleep (contracting).
        /// </summary>
        /// <param name="open">Open price array.</param>
        /// <param name="high">High price array.</param>
        /// <param name="low">Low price array.</param>
        /// <param name="close">Close price array.</param>
        /// <param name="jawsPeriod">Jaws period (default 13).</param>
        /// <param name="jawsShift">Jaws shift (default 8).</param>
        /// <param name="teethPeriod">Teeth period (default 8).</param>
        /// <param name="teethShift">Teeth shift (default 5).</param>
        /// <param name="lipsPeriod">Lips period (default 5).</param>
        /// <param name="lipsShift">Lips shift (default 3).</param>
        /// <param name="maType">Moving average method (default SMMA).</param>
        /// <param name="priceType">Applied price type (default Median).</param>
        /// <returns>GatorResult containing upper/lower histograms, colors, and Alligator lines.</returns>
        public static GatorResult Calculate(
            double[] open, double[] high, double[] low, double[] close,
            int jawsPeriod = 13, int jawsShift = 8,
            int teethPeriod = 8, int teethShift = 5,
            int lipsPeriod = 5, int lipsShift = 3,
            MaMethod maType = MaMethod.SMMA,
            AppliedPrice priceType = AppliedPrice.Median)
        {
            // Input validation
            var ratesTotal = close?.Length ?? 0;
            if (open == null || high == null || low == null || close == null || ratesTotal == 0)
                return new GatorResult
                {
                    UpperBuffer = new double[0],
                    UpperColors = new double[0],
                    LowerBuffer = new double[0],
                    LowerColors = new double[0],
                    Jaws = new double[0],
                    Teeth = new double[0],
                    Lips = new double[0]
                };

            // Validate periods and shifts
            if (!IsValidConfiguration(jawsPeriod, jawsShift, teethPeriod, teethShift, lipsPeriod, lipsShift))
                return new GatorResult
                {
                    UpperBuffer = new double[ratesTotal],
                    UpperColors = new double[ratesTotal],
                    LowerBuffer = new double[ratesTotal],
                    LowerColors = new double[ratesTotal],
                    Jaws = new double[ratesTotal],
                    Teeth = new double[ratesTotal],
                    Lips = new double[ratesTotal]
                };

            // Check minimum data requirement
            var minDataRequired = Math.Max(jawsPeriod, Math.Max(teethPeriod, lipsPeriod));
            if (ratesTotal < minDataRequired)
                return new GatorResult
                {
                    UpperBuffer = new double[ratesTotal],
                    UpperColors = new double[ratesTotal],
                    LowerBuffer = new double[ratesTotal],
                    LowerColors = new double[ratesTotal],
                    Jaws = new double[ratesTotal],
                    Teeth = new double[ratesTotal],
                    Lips = new double[ratesTotal]
                };

            var priceBuffer = GetAppliedPriceBuffer(open, high, low, close, priceType);
            var jaws = new double[ratesTotal];
            var teeth = new double[ratesTotal];
            var lips = new double[ratesTotal];
            var upperBuffer = new double[ratesTotal];
            var upperColors = new double[ratesTotal];
            var lowerBuffer = new double[ratesTotal];
            var lowerColors = new double[ratesTotal];

            // Calculate moving averages
            CalculateMA(priceBuffer, jaws, jawsPeriod, maType);
            CalculateMA(priceBuffer, teeth, teethPeriod, maType);
            CalculateMA(priceBuffer, lips, lipsPeriod, maType);

            var upperShift = jawsShift - teethShift;
            var lowerShift = teethShift - lipsShift;
            var shift = Math.Max(upperShift, lowerShift);
            var lowerLimit = lowerShift + lipsShift + lipsPeriod;
            var upperLimit = upperShift + teethShift + teethPeriod;

            // Initialize buffers
            for (var i = 0; i < shift; i++)
            {
                upperBuffer[i] = 0.0;
                upperColors[i] = 0.0;
                lowerBuffer[i] = 0.0;
                lowerColors[i] = 0.0;
            }

            // Main calculation loop
            for (var i = shift; i < ratesTotal; i++)
            {
                // Lower buffer: -|Teeth - Lips|
                if (i >= lowerLimit)
                {
                    var curValue = -Math.Abs(teeth[i - lowerShift] - lips[i]);
                    var prevValue = i > 0 ? lowerBuffer[i - 1] : 0.0;
                    lowerBuffer[i] = curValue;
                    lowerColors[i] = prevValue == curValue ? (i > 0 ? lowerColors[i - 1] : 0.0)
                        : (prevValue < curValue ? 1.0 : 0.0); // 1=Green (expanding), 0=Red (contracting)
                }
                else
                {
                    lowerBuffer[i] = 0.0;
                    lowerColors[i] = 0.0;
                }

                // Upper buffer: |Jaws - Teeth|
                if (i >= upperLimit)
                {
                    var curValue = Math.Abs(jaws[i - upperShift] - teeth[i]);
                    var prevValue = i > 0 ? upperBuffer[i - 1] : 0.0;
                    upperBuffer[i] = curValue;
                    upperColors[i] = prevValue == curValue ? (i > 0 ? upperColors[i - 1] : 0.0)
                        : (prevValue < curValue ? 0.0 : 1.0); // 0=Green (expanding), 1=Red (contracting)
                }
                else
                {
                    upperBuffer[i] = 0.0;
                    upperColors[i] = 0.0;
                }
            }

            return new GatorResult
            {
                UpperBuffer = upperBuffer,
                UpperColors = upperColors,
                LowerBuffer = lowerBuffer,
                LowerColors = lowerColors,
                Jaws = jaws,
                Teeth = teeth,
                Lips = lips
            };
        }

        private static double[] GetAppliedPriceBuffer(double[] open, double[] high, double[] low, double[] close,
            AppliedPrice priceType)
        {
            var length = Math.Min(Math.Min(open.Length, high.Length), Math.Min(low.Length, close.Length));

            switch (priceType)
            {
                case AppliedPrice.Open:
                    return CopyArray(open, length);
                case AppliedPrice.High:
                    return CopyArray(high, length);
                case AppliedPrice.Low:
                    return CopyArray(low, length);
                case AppliedPrice.Median:
                    var median = new double[length];
                    for (var i = 0; i < length; i++)
                        median[i] = (high[i] + low[i]) / 2.0;
                    return median;
                case AppliedPrice.Close:
                default:
                    return CopyArray(close, length);
            }
        }

        private static double[] CopyArray(double[] source, int length)
        {
            var result = new double[length];
            Array.Copy(source, result, Math.Min(source.Length, length));
            return result;
        }

        private static void CalculateMA(double[] source, double[] dest, int period, MaMethod method)
        {
            switch (method)
            {
                case MaMethod.SMA:
                    SimpleMA(source, dest, period);
                    break;
                case MaMethod.EMA:
                    ExponentialMA(source, dest, period);
                    break;
                case MaMethod.SMMA:
                    SmoothedMA(source, dest, period);
                    break;
            }
        }

        private static void SimpleMA(double[] source, double[] dest, int period)
        {
            var sum = 0.0;
            for (var i = 0; i < source.Length; i++)
            {
                sum += source[i];
                if (i >= period)
                    sum -= source[i - period];
                if (i >= period - 1)
                    dest[i] = sum / period;
                else
                    dest[i] = 0.0;
            }
        }

        private static void ExponentialMA(double[] source, double[] dest, int period)
        {
            if (source.Length == 0) return;

            var k = 2.0 / (period + 1);
            dest[0] = source[0];
            for (var i = 1; i < source.Length; i++)
                dest[i] = k * source[i] + (1 - k) * dest[i - 1];
        }

        private static void SmoothedMA(double[] source, double[] dest, int period)
        {
            if (source.Length == 0) return;

            var sum = 0.0;
            for (var i = 0; i < period && i < source.Length; i++)
            {
                sum += source[i];
                dest[i] = 0.0;
            }

            if (source.Length >= period)
                dest[period - 1] = sum / period;

            for (var i = period; i < source.Length; i++)
                dest[i] = (dest[i - 1] * (period - 1) + source[i]) / period;
        }

        private static bool IsValidConfiguration(int jawsPeriod, int jawsShift, int teethPeriod, int teethShift,
            int lipsPeriod, int lipsShift)
        {
            // Validate period hierarchy: Jaws > Teeth > Lips
            if (jawsPeriod <= teethPeriod || teethPeriod <= lipsPeriod)
                return false;

            // Validate shift hierarchy: Jaws > Teeth > Lips
            if (jawsShift <= teethShift || teethShift <= lipsShift)
                return false;

            // Validate periods are greater than their respective shifts
            if (jawsPeriod <= jawsShift || teethPeriod <= teethShift || lipsPeriod <= lipsShift)
                return false;

            return true;
        }
    }
}