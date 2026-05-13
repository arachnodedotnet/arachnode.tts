using System;

namespace Trade.Indicators
{
    public class ROCResult
    {
        public double[] ROC;
    }

    public static class ROC
    {
        /// <summary>
        ///     Calculates the Rate of Change (ROC) indicator.
        /// </summary>
        /// <param name="prices">Array of price values (e.g., close prices)</param>
        /// <param name="period">ROC period (default 12)</param>
        /// <returns>ROCResult containing the ROC buffer</returns>
        public static ROCResult Calculate(double[] prices, int period = 12)
        {
            if (period < 1)
                period = 12;

            var length = prices.Length;

            // Handle empty array case
            if (length == 0)
                return new ROCResult
                {
                    ROC = new double[0]
                };

            var roc = new double[length];

            // The first 'period' values are set to 0.0 (empty value)
            // Only set values if we actually have array elements to set
            var maxIndexToSet = Math.Min(period, length);
            for (var i = 0; i < maxIndexToSet; i++)
                roc[i] = 0.0;

            for (var i = period; i < length; i++)
                if (prices[i - period] != 0.0)
                    roc[i] = (prices[i] - prices[i - period]) / prices[i - period] * 100.0;
                else
                    roc[i] = 0.0;

            return new ROCResult
            {
                ROC = roc
            };
        }
    }
}