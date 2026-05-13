using System;
using System.Linq;

namespace Trade.Indicators
{
    // Static class for Accumulation Swing Index (ASI) indicator calculation
    public static class AsiIndicator
    {
        // Calculate ASI for price buffers
        // Returns an array of ASI values
        public static double[] Calculate(double[] open, double[] high, double[] low, double[] close, double t = 300.0,
            double point = 1.0, int digits = 2)
        {
            var rates_total = new[] { open.Length, high.Length, low.Length, close.Length }.Min();
            if (rates_total < 2)
                return new double[0];
            var tpoints = point > 1e-7 ? t * point : t * Math.Pow(10, -digits);
            var asi = new double[rates_total];
            var si = new double[rates_total];
            var tr = new double[rates_total];
            asi[0] = 0.0;
            si[0] = 0.0;
            tr[0] = high[0] - low[0];
            for (var i = 1; i < rates_total; i++)
            {
                var dPrevClose = close[i - 1];
                var dPrevOpen = open[i - 1];
                var dClose = close[i];
                var dHigh = high[i];
                var dLow = low[i];
                tr[i] = Math.Max(dHigh, dPrevClose) - Math.Min(dLow, dPrevClose);
                var ER = 0.0;
                if (!(dPrevClose >= dLow && dPrevClose <= dHigh))
                {
                    if (dPrevClose > dHigh)
                        ER = Math.Abs(dHigh - dPrevClose);
                    if (dPrevClose < dLow)
                        ER = Math.Abs(dLow - dPrevClose);
                }

                var K = Math.Max(Math.Abs(dHigh - dPrevClose), Math.Abs(dLow - dPrevClose));
                var SH = Math.Abs(dPrevClose - dPrevOpen);
                var R = tr[i] - 0.5 * ER + 0.25 * SH;
                if (R == 0.0 || tpoints == 0.0)
                    si[i] = 0.0;
                else
                    si[i] = 50 * (dClose - dPrevClose + 0.5 * (dClose - open[i]) + 0.25 * (dPrevClose - dPrevOpen)) *
                        (K / tpoints) / R;
                asi[i] = asi[i - 1] + si[i];
            }

            return asi;
        }
    }
}