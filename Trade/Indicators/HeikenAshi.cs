using System;

namespace Trade.Indicators
{
    public class HeikenAshiResult
    {
        public double[] Open { get; set; }
        public double[] High { get; set; }
        public double[] Low { get; set; }
        public double[] Close { get; set; }
        public int[] Color { get; set; } // 0 = DodgerBlue (bullish), 1 = Red (bearish)
    }

    public static class HeikenAshi
    {
        /// <summary>
        ///     Calculates Heiken Ashi values for the given price arrays.
        ///     Heiken Ashi creates smoothed candlesticks that help identify trend direction.
        /// </summary>
        /// <param name="open">Open price array.</param>
        /// <param name="high">High price array.</param>
        /// <param name="low">Low price array.</param>
        /// <param name="close">Close price array.</param>
        /// <returns>HeikenAshiResult containing buffers for open, high, low, close, and color.</returns>
        /// <exception cref="ArgumentNullException">Thrown when any input array is null</exception>
        public static HeikenAshiResult Calculate(double[] open, double[] high, double[] low, double[] close)
        {
            // Input validation
            if (open == null) throw new ArgumentNullException(nameof(open));
            if (high == null) throw new ArgumentNullException(nameof(high));
            if (low == null) throw new ArgumentNullException(nameof(low));
            if (close == null) throw new ArgumentNullException(nameof(close));

            var ratesTotal = Math.Min(Math.Min(open.Length, high.Length), Math.Min(low.Length, close.Length));

            var haOpen = new double[ratesTotal];
            var haHigh = new double[ratesTotal];
            var haLow = new double[ratesTotal];
            var haClose = new double[ratesTotal];
            var haColor = new int[ratesTotal]; // 0 = DodgerBlue (bullish), 1 = Red (bearish)

            if (ratesTotal == 0)
                return new HeikenAshiResult
                {
                    Open = haOpen,
                    High = haHigh,
                    Low = haLow,
                    Close = haClose,
                    Color = haColor
                };

            // Initialize first bar with regular OHLC values
            haOpen[0] = open[0];
            haHigh[0] = high[0];
            haLow[0] = low[0];
            haClose[0] = close[0];
            haColor[0] = haOpen[0] <= haClose[0] ? 0 : 1; // 0 = bullish, 1 = bearish

            // Main calculation loop
            for (var i = 1; i < ratesTotal; i++)
            {
                // Heiken Ashi formulas:
                // 1. HA Close = (Open + High + Low + Close) / 4
                var ha_close = (open[i] + high[i] + low[i] + close[i]) / 4.0;

                // 2. HA Open = (Previous HA Open + Previous HA Close) / 2
                var ha_open = (haOpen[i - 1] + haClose[i - 1]) / 2.0;

                // 3. HA High = Max(High, HA Open, HA Close)
                var ha_high = Math.Max(high[i], Math.Max(ha_open, ha_close));

                // 4. HA Low = Min(Low, HA Open, HA Close)
                var ha_low = Math.Min(low[i], Math.Min(ha_open, ha_close));

                haOpen[i] = ha_open;
                haHigh[i] = ha_high;
                haLow[i] = ha_low;
                haClose[i] = ha_close;
                haColor[i] = ha_open <= ha_close ? 0 : 1; // 0 = bullish, 1 = bearish
            }

            return new HeikenAshiResult
            {
                Open = haOpen,
                High = haHigh,
                Low = haLow,
                Close = haClose,
                Color = haColor
            };
        }
    }
}