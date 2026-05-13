namespace Trade.Indicators
{
    public class IchimokuResult
    {
        public double[] ChikouSpan;
        public double[] KijunSen;
        public double[] SenkouSpanA;
        public double[] SenkouSpanB;
        public double[] TenkanSen;
    }

    public static class Ichimoku
    {
        public static IchimokuResult Calculate(
            double[] high,
            double[] low,
            double[] close,
            int tenkan = 9,
            int kijun = 26,
            int senkou = 52)
        {
            var length = close.Length;
            var tenkanSen = new double[length];
            var kijunSen = new double[length];
            var senkouSpanA = new double[length];
            var senkouSpanB = new double[length];
            var chikouSpan = new double[length];

            // Calculate Tenkan-sen and Kijun-sen
            for (var i = 0; i < length; i++)
            {
                tenkanSen[i] = (Highest(high, tenkan, i) + Lowest(low, tenkan, i)) / 2.0;
                kijunSen[i] = (Highest(high, kijun, i) + Lowest(low, kijun, i)) / 2.0;
            }

            // Calculate Senkou Span A and B, shifted forward by kijun periods
            for (var i = 0; i < length; i++)
            {
                var shiftedIndex = i + kijun;
                var spanA = (tenkanSen[i] + kijunSen[i]) / 2.0;
                var spanB = (Highest(high, senkou, i) + Lowest(low, senkou, i)) / 2.0;
                if (shiftedIndex < length)
                {
                    senkouSpanA[shiftedIndex] = spanA;
                    senkouSpanB[shiftedIndex] = spanB;
                }
            }

            // Fill uninitialized values with NaN
            for (var i = 0; i < kijun; i++)
            {
                senkouSpanA[i] = 0;
                senkouSpanB[i] = 0;
            }

            // Calculate Chikou Span, shifted backward by kijun periods
            for (var i = 0; i < length; i++)
            {
                var shiftedIndex = i - kijun;
                chikouSpan[i] = shiftedIndex >= 0 ? close[shiftedIndex] : 0;
            }

            return new IchimokuResult
            {
                TenkanSen = tenkanSen,
                KijunSen = kijunSen,
                SenkouSpanA = senkouSpanA,
                SenkouSpanB = senkouSpanB,
                ChikouSpan = chikouSpan
            };
        }

        private static double Highest(double[] array, int range, int fromIndex)
        {
            var res = array[fromIndex];
            for (var i = fromIndex; i > fromIndex - range && i >= 0; i--)
                if (res < array[i])
                    res = array[i];
            return res;
        }

        private static double Lowest(double[] array, int range, int fromIndex)
        {
            var res = array[fromIndex];
            for (var i = fromIndex; i > fromIndex - range && i >= 0; i--)
                if (res > array[i])
                    res = array[i];
            return res;
        }
    }
}