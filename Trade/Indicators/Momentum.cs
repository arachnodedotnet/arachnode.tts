using System;

namespace Trade.Indicators
{
    public class MomentumResult
    {
        public double[] Momentum;
    }

    public static class Momentum
    {
        /// <summary>
        ///     Calculates the Momentum indicator.
        /// </summary>
        /// <param name="prices">Array of price values (e.g., close prices)</param>
        /// <param name="period">Momentum period (default 14)</param>
        /// <returns>MomentumResult containing the momentum buffer</returns>
        public static MomentumResult Calculate(double[] prices, int period = 14)
        {
            if (period < 1)
                period = 14;

            var length = prices.Length;

            // Handle empty array case
            if (length == 0)
                return new MomentumResult
                {
                    Momentum = new double[0]
                };

            var momentum = new double[length];

            for (var i = period; i < length; i++)
                if (prices[i - period] != 0)
                    momentum[i] = prices[i] * 100.0 / prices[i - period];
                else
                    momentum[i] = 0.0;

            // The first 'period' values are set to 0.0 (empty value)
            // Only set values if we actually have array elements to set
            var maxIndexToSet = Math.Min(period, length);
            for (var i = 0; i < maxIndexToSet; i++)
                momentum[i] = 0.0;

            return new MomentumResult
            {
                Momentum = momentum
            };
        }
    }
}