using System;

namespace Trade.Indicators
{
    public class ZigZagResult
    {
        public double[] HighMap;
        public double[] LowMap;
        public double[] ZigZag;
    }

    public static class ZigZag
    {
        public static ZigZagResult Calculate(
            double[] high,
            double[] low,
            int depth = 12,
            int deviation = 5,
            int backstep = 3,
            double point = 0.00001)
        {
            // Input validation
            if (high == null || low == null)
                throw new ArgumentNullException("High and low arrays cannot be null");

            if (high.Length != low.Length)
                throw new ArgumentException("High and low arrays must have the same length");

            if (high.Length == 0)
                return new ZigZagResult
                {
                    ZigZag = new double[0],
                    HighMap = new double[0],
                    LowMap = new double[0]
                };

            var length = high.Length;
            var zigZag = new double[length];
            var highMap = new double[length];
            var lowMap = new double[length];

            // Ensure minimum parameters
            depth = Math.Max(depth, 1);
            deviation = Math.Max(deviation, 0);
            backstep = Math.Max(backstep, 1);
            point = Math.Max(point, 0.000001);

            // Initialize arrays
            for (var i = 0; i < length; i++)
            {
                zigZag[i] = 0.0;
                highMap[i] = 0.0;
                lowMap[i] = 0.0;
            }

            var start = Math.Max(depth, backstep);
            if (length <= start)
                return new ZigZagResult
                {
                    ZigZag = zigZag,
                    HighMap = highMap,
                    LowMap = lowMap
                };

            double lastHigh = 0, lastLow = 0;

            // Phase 1: Find local extremes and store in HighMap/LowMap
            for (var shift = start; shift < length; shift++)
            {
                // Find the lowest low in the depth period
                var lowestIdx = FindLowest(low, depth, shift);
                var lowestValue = low[lowestIdx];

                if (lowestValue != lastLow)
                {
                    lastLow = lowestValue;

                    // Check if current low is sufficiently different from the lowest
                    if (Math.Abs(low[shift] - lowestValue) <= deviation * point)
                    {
                        // Clear any previous lows within backstep range
                        for (var back = 1; back <= backstep && shift - back >= 0; back++)
                            if (lowMap[shift - back] != 0 && lowMap[shift - back] > lowestValue)
                                lowMap[shift - back] = 0.0;

                        if (low[shift] == lowestValue) lowMap[shift] = lowestValue;
                    }
                }

                // Find the highest high in the depth period  
                var highestIdx = FindHighest(high, depth, shift);
                var highestValue = high[highestIdx];

                if (highestValue != lastHigh)
                {
                    lastHigh = highestValue;

                    // Check if current high is sufficiently different from the highest
                    if (Math.Abs(highestValue - high[shift]) <= deviation * point)
                    {
                        // Clear any previous highs within backstep range
                        for (var back = 1; back <= backstep && shift - back >= 0; back++)
                            if (highMap[shift - back] != 0 && highMap[shift - back] < highestValue)
                                highMap[shift - back] = 0.0;

                        if (high[shift] == highestValue) highMap[shift] = highestValue;
                    }
                }
            }

            // Phase 2: Build the ZigZag line by connecting significant extremes
            var extremeSearch =
                0; // 0=looking for first extreme, 1=looking for high after low, -1=looking for low after high
            int lastHighPos = -1, lastLowPos = -1;
            double zigZagHigh = 0, zigZagLow = 0;

            for (var shift = start; shift < length; shift++)
                switch (extremeSearch)
                {
                    case 0: // Looking for first extreme
                        if (highMap[shift] != 0)
                        {
                            zigZagHigh = highMap[shift];
                            lastHighPos = shift;
                            extremeSearch = -1; // Now look for low
                            zigZag[shift] = zigZagHigh;
                        }
                        else if (lowMap[shift] != 0)
                        {
                            zigZagLow = lowMap[shift];
                            lastLowPos = shift;
                            extremeSearch = 1; // Now look for high
                            zigZag[shift] = zigZagLow;
                        }

                        break;

                    case 1: // Looking for high after low
                        if (lowMap[shift] != 0 && lowMap[shift] < zigZagLow)
                        {
                            // Found a lower low, replace the previous one
                            if (lastLowPos >= 0)
                                zigZag[lastLowPos] = 0.0;
                            zigZagLow = lowMap[shift];
                            lastLowPos = shift;
                            zigZag[shift] = zigZagLow;
                        }
                        else if (highMap[shift] != 0)
                        {
                            // Found a high, complete the low-to-high move
                            zigZagHigh = highMap[shift];
                            lastHighPos = shift;
                            zigZag[shift] = zigZagHigh;
                            extremeSearch = -1; // Now look for low
                        }

                        break;

                    case -1: // Looking for low after high
                        if (highMap[shift] != 0 && highMap[shift] > zigZagHigh)
                        {
                            // Found a higher high, replace the previous one
                            if (lastHighPos >= 0)
                                zigZag[lastHighPos] = 0.0;
                            zigZagHigh = highMap[shift];
                            lastHighPos = shift;
                            zigZag[shift] = zigZagHigh;
                        }
                        else if (lowMap[shift] != 0)
                        {
                            // Found a low, complete the high-to-low move
                            zigZagLow = lowMap[shift];
                            lastLowPos = shift;
                            zigZag[shift] = zigZagLow;
                            extremeSearch = 1; // Now look for high
                        }

                        break;
                }

            return new ZigZagResult
            {
                ZigZag = zigZag,
                HighMap = highMap,
                LowMap = lowMap
            };
        }

        /// <summary>
        ///     Finds the index of the highest value within 'depth' periods ending at 'start'
        /// </summary>
        private static int FindHighest(double[] array, int depth, int start)
        {
            if (start < 0 || start >= array.Length)
                return Math.Max(0, Math.Min(start, array.Length - 1));

            var begin = Math.Max(0, start - depth + 1);
            var end = Math.Min(start + 1, array.Length);

            var max = array[begin];
            var maxIndex = begin;

            for (var i = begin; i < end; i++)
                if (array[i] > max)
                {
                    max = array[i];
                    maxIndex = i;
                }

            return maxIndex;
        }

        /// <summary>
        ///     Finds the index of the lowest value within 'depth' periods ending at 'start'
        /// </summary>
        private static int FindLowest(double[] array, int depth, int start)
        {
            if (start < 0 || start >= array.Length)
                return Math.Max(0, Math.Min(start, array.Length - 1));

            var begin = Math.Max(0, start - depth + 1);
            var end = Math.Min(start + 1, array.Length);

            var min = array[begin];
            var minIndex = begin;

            for (var i = begin; i < end; i++)
                if (array[i] < min)
                {
                    min = array[i];
                    minIndex = i;
                }

            return minIndex;
        }
    }
}