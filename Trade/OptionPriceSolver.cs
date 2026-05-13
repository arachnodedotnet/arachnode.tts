using System;
using System.Collections.Generic;
using Trade.Prices2;
using Trade.Polygon2;

namespace Trade
{
    /// <summary>
    /// Lightweight wrapper around OptionPrices to retrieve option records causally and conveniently.
    /// Provides helper methods to fetch option PriceRecords by specification or by symbol, and
    /// exposes the underlying OptionPrices lookups in a single place.
    /// </summary>
    public class OptionPriceSolver
    {
        private readonly Prices _underlying;
        private readonly OptionPrices _options;

        /// <summary>
        /// Create a new OptionPriceSolver. If parameters are null, uses global GeneticIndividual dependencies.
        /// Ensure GeneticIndividual.InitializePrices(...) and GeneticIndividual.InitializeOptionSolvers(...) were called.
        /// </summary>
        public OptionPriceSolver(Prices underlying = null, OptionPrices options = null)
        {
            _underlying = underlying ?? GeneticIndividual.Prices;
            _options = options ?? GeneticIndividual.OptionsPrices;
            if (_underlying == null) throw new InvalidOperationException("Underlying Prices is not initialized");
            if (_options == null) throw new InvalidOperationException("OptionPrices is not initialized");
        }

        /// <summary>
        /// Retrieve an option PriceRecord by specification at a given timestamp.
        /// Walks forward in time if needed and respects business days when computing expiration distance.
        /// </summary>
        public PriceRecord GetOptionPrice(OptionType allowedOptionType,
                                          DateTime targetDateTime,
                                          TimeFrame timeFrame = TimeFrame.M1,
                                          int strikeDistanceAway = 10,
                                          int expirationBusinessDaysAway = 10)
        {
            return _options.GetOptionPrice(_underlying, allowedOptionType, targetDateTime, timeFrame,
                                           strikeDistanceAway, expirationBusinessDaysAway);
        }

        /// <summary>
        /// Retrieve the most recent buffer for a specific OCC option symbol.
        /// </summary>
        public PriceRecord[] GetOptionPriceBuffer(string optionSymbol, int period, TimeFrame timeFrame = TimeFrame.M1)
        {
            if (string.IsNullOrWhiteSpace(optionSymbol)) return Array.Empty<PriceRecord>();
            return _options.GetOptionPriceBuffer(optionSymbol, period, timeFrame) ?? Array.Empty<PriceRecord>();
        }

        /// <summary>
        /// Get all option Prices objects for an underlying symbol (e.g., "SPY").
        /// </summary>
        public Dictionary<string, Prices> GetOptionsByUnderlying(string underlyingSymbol)
        {
            if (string.IsNullOrWhiteSpace(underlyingSymbol)) return new Dictionary<string, Prices>();
            return _options.GetOptionsByUnderlying(underlyingSymbol) ?? new Dictionary<string, Prices>();
        }
    }
}
