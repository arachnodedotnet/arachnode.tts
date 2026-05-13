using System;
using System.Collections.Generic;
using System.Linq;
using Trade.Prices2;
using Trade.Tests;

namespace Trade
{
    // Event argument classes for trade events
    public class TradeOpenedEventArgs : EventArgs
    {
        public int TradeIndex { get; set; }
        public DateTime DateTime { get; set; }
        public double Price { get; set; }
        public double Position { get; set; }
        public AllowedSecurityType SecurityType { get; set; }
        public AllowedTradeType TradeType { get; set; }
        public AllowedOptionType? OptionType { get; set; }
        public int IndicatorIndex { get; set; }
        public string ActionTag { get; set; }
        public double Balance { get; set; }
    }

    public class TradeClosedEventArgs : EventArgs
    {
        public TradeResult Trade { get; set; }
        public DateTime DateTime { get; set; }
        public double ClosePrice { get; set; }
        public double Proceeds { get; set; }
        public double Balance { get; set; }
        public string ActionTag { get; set; }
        public bool IsEarlyTakeProfit { get; set; }
    }

    /// <summary>
    /// Per-indicator state for concurrent processing
    /// </summary>
    internal struct IndicatorState
    {
        // Stock position state
        public bool HoldingStock;
        public int OpenStockIndex;
        public double OpenStockPrice;
        public double StockPosition;
        
        // Option position state  
        public bool HoldingOption;
        public int OpenOptionIndex;
        public double OpenOptionPrice;
        public double OptionPosition;
        public double OptionStrike;
        public bool IsCallOption;
        public PriceRecord PriceRecordForOpen;
        
        // Each indicator tracks its own delta direction
        public int PrevDir;
    }

    /// <summary>
    /// Portfolio-level position state (shared across all indicators)
    /// </summary>
    internal struct PortfolioState
    {
        public bool HoldingStock;
        public int OpenStockIndex;
        public double OpenStockPrice;
        public double StockPosition;
        
        public bool HoldingOption;
        public int OpenOptionIndex;
        public double OpenOptionPrice;
        public double OptionPosition;
        public double OptionStrike;
        public bool IsCallOption;
        public PriceRecord PriceRecordForOpen;
    }

    public partial class GeneticIndividual
    {
        // NEW: Option ITM take-profit configuration
        public bool EnableOptionITMTakeProfit { get; set; } = true; // allow early profit-taking
        public double OptionITMTakeProfitPct { get; set; } = 0.25;   // 25% gain on premium by default

        // NEW: Trade event handlers
        public event EventHandler<TradeOpenedEventArgs> TradeOpened;
        public event EventHandler<TradeClosedEventArgs> TradeClosed;

        private IndicatorState[] indicatorStates;

        /// <summary>
        /// Execute trading logic with proper concurrent indicator processing
        /// </summary>
        internal void ExecuteTradesDeltaMode(PriceRecord[] priceRecords, List<List<double>> indicatorValues)
        {
            var balance = StartingBalance;
            var totalUsedBalance = 0.0;

            // Per-indicator state tracking
            //TODO: This means that we should be able to keep feeding PriceRecord[] arrays to a GI...
            if (indicatorStates == null)
            {
                indicatorStates = new IndicatorState[Indicators.Count];
                for (int k = 0; k < Indicators.Count; k++)
                {
                    indicatorStates[k] = new IndicatorState { PrevDir = 0 };
                }
            }
            else
            {
                
            }
            
            if (AllowMultipleTrades)
            {
                if (SignalCombination == SignalCombinationMethod.Isolation)
                {
                    // ISOLATION MODE: Indicators trade independently with shared balance
                    ProcessIndicatorsInIsolation(priceRecords, indicatorValues, indicatorStates,
                        ref balance, ref totalUsedBalance);
                }
                else
                {
                    // AGGREGATION MODE: Indicators work together with combined signals
                    ProcessIndicatorsWithAggregation(priceRecords, indicatorValues, indicatorStates,
                        ref balance, ref totalUsedBalance);
                }
            }
            else
            {
                // Single/combined indicator logic (preserved for backward compatibility)
                //ProcessSingleIndicatorMode(priceRecords, indicatorValues, ref balance, ref totalUsedBalance);

                // ISOLATION MODE: Indicators trade independently with shared balance
                ProcessIndicatorsInIsolation(priceRecords, indicatorValues, indicatorStates,
                    ref balance, ref totalUsedBalance);
            }

            FinalBalance = balance;
        }

        /// <summary>
        /// Process indicators in isolation mode (concurrent but independent)
        /// </summary>
        internal void ProcessIndicatorsInIsolation(PriceRecord[] priceRecords, List<List<double>> indicatorValues,
            IndicatorState[] indicatorStates, ref double balance, ref double totalUsedBalance)
        {
            if(!priceRecords.Any())
                return;

            PriceRecord[] guideRecords = priceRecords;
            var mostGranularTimeFrame = TimeFrame.D1;

            if (Prices != null && Indicators.Any())
            {
                mostGranularTimeFrame = Indicators.Min(ind => ind.TimeFrame);

                try
                {
                    //guideRecords = Prices.GetRange(
                    //    priceRecords.First().DateTime,
                    //    priceRecords.Last().DateTime.AddDays(1),
                    //    mostGranularTimeFrame).ToArray();

                    guideRecords = GetSafeTimeFrameData(priceRecords, mostGranularTimeFrame);
                }
                catch (Exception)
                {
                    guideRecords = priceRecords; // Fallback to daily
                }
            }

            Dictionary<int, int> lastDeltaByK = new Dictionary<int, int>();
            for (int k = 0; k < Indicators.Count; k++)
            {
                lastDeltaByK.Add(k, 0);
            }

            int dayBarIndex = 0;
            var day = priceRecords.First().DateTime.Date;

            // FIXED: Time-first loop - all indicators compete on each bar
            int barIndex = 1;
            for (; barIndex < guideRecords.Length; barIndex++)
            {
                var priceRecord = guideRecords[barIndex];
                if (priceRecord.DateTime.Date > day)
                {
                    dayBarIndex++;
                    day = priceRecord.DateTime.Date;
                }

                // Process each indicator for this single bar
                for (int k = 0; k < Indicators.Count; k++)
                {
                    var signals = indicatorValues[k];
                    if (signals == null || barIndex >= signals.Count) continue;

                    ProcessSingleBarForIndicator(priceRecords, guideRecords, dayBarIndex, barIndex, signals, Indicators[k], k,
                        ref indicatorStates[k], ref balance, ref totalUsedBalance);
                }
            }

            //if (false)
            //{
            //    var allExtendedSignals = new List<List<double>>();
            //    for (int k = 0; k < Indicators.Count; k++)
            //    {
            //        allExtendedSignals.Add(new List<double>());
            //    }

            //    int daysToAdd = 1;
            //    while (indicatorStates.Any(_ => _.HoldingStock || _.HoldingOption))
            //    {
            //        var extendedGuideRecords = Prices.GetRange(
            //            priceRecords.Last(_ => !_.Manufactured).DateTime,
            //            priceRecords.Last(_ => !_.Manufactured).DateTime.AddDays(daysToAdd++),
            //            mostGranularTimeFrame).ToArray();

            //        CalculateSignals(extendedGuideRecords, allExtendedSignals, allExtendedSignals, null);

            //        var mergedGuideRecords = guideRecords.Concat(extendedGuideRecords).ToArray();

            //        // Process each indicator for this single bar
            //        for (int k = 0; k < Indicators.Count; k++)
            //        {
            //            var signals = indicatorValues[k];
            //            var mergedExtendedSignals = signals.Concat(allExtendedSignals[k]).ToList();

            //            ProcessSingleBarForIndicator(mergedGuideRecords, dayBarIndex, i, mergedExtendedSignals,
            //                Indicators[k], k,
            //                ref indicatorStates[k], ref balance, ref totalUsedBalance);
            //        }
            //    }
            //}

            FinalizeIndicatorPositions(indicatorStates, priceRecords, guideRecords, ref balance);
        }

        /// <summary>
        /// Process indicators with signal aggregation (your +2/-1 threshold idea)
        /// </summary>
        internal void ProcessIndicatorsWithAggregation(PriceRecord[] priceRecords, List<List<double>> indicatorValues,
            IndicatorState[] indicatorStates, ref double balance, ref double totalUsedBalance)
        {
            if (!priceRecords.Any())
                return;

            // Portfolio-level position (one position managed by combined signals)
            var portfolioState = new PortfolioState();

            var mostGranularTimeFrame = Indicators.Min(ind => ind.TimeFrame);

            PriceRecord[] guideRecords;
            try
            {
                //guideRecords = Prices.GetRange(
                //    priceRecords.First().DateTime,
                //    priceRecords.Last().DateTime,
                //    mostGranularTimeFrame).ToArray();

                guideRecords = GetSafeTimeFrameData(priceRecords, mostGranularTimeFrame);
            }
            catch (Exception)
            {
                guideRecords = priceRecords; // Fallback to daily
            }

            Dictionary<int, int> lastDeltaByK = new Dictionary<int, int>();
            for (int k = 0; k < Indicators.Count; k++)
            {
                lastDeltaByK.Add(k, 0);
            }

            int dayBarIndex = 0;
            var day = priceRecords.First().DateTime.Date;
            // Time-first loop: aggregate signals, make portfolio decisions
            for (int barIndex = 1; barIndex < guideRecords.Length; barIndex++)
            {
                var priceRecord = guideRecords[barIndex];
                if (priceRecord.DateTime.Date > day)
                {
                    dayBarIndex++;
                    day = priceRecord.DateTime.Date;
                }

                // 1. Calculate each indicator's delta for this bar
                var deltas = new int[Indicators.Count];
                for (int k = 0; k < Indicators.Count; k++)
                {
                    var signals = indicatorValues[k];
                    if (signals == null || barIndex >= signals.Count) continue;

                    var normalizedTimestamp = AggregatedPriceData.GetNormalizedTimestamp(priceRecord.DateTime, Indicators[k].TimeFrame);
                    if (priceRecord.DateTime.Ticks == normalizedTimestamp)
                    {
                        deltas[k] = CalculateIndicatorDelta(priceRecords, barIndex, signals, Indicators[k], k,
                            ref indicatorStates[k]);
                        lastDeltaByK[k] = deltas[k];
                    }
                    else
                    {
                        deltas[k] = lastDeltaByK[k];
                    }
                }
                
                // 2. Combine all deltas into a single signal
                var combinedDelta = AggregateDeltas(deltas);
                
                // 3. Make portfolio-level trading decisions based on combined signal
                ProcessPortfolioDecision(priceRecords, guideRecords, dayBarIndex, barIndex, combinedDelta, ref portfolioState, 
                    ref balance, ref totalUsedBalance);
            }
            
            // Finalize portfolio position
            FinalizePortfolioPosition(ref portfolioState, priceRecords, guideRecords, ref balance);
        }

        /// <summary>
        /// Calculate delta for a single indicator at a single bar
        /// </summary>
        internal int CalculateIndicatorDelta(PriceRecord[] priceRecords, int barIndex, List<double> signals,
            IndicatorParams indicator, int indicatorIndex, ref IndicatorState state)
        {
            var polarity = indicator.Polarity;
            var rawSlope = Math.Sign(signals[barIndex] - signals[barIndex - 1]);
            var currDir = rawSlope * polarity; // -1, 0, +1
            
            // Track state for this indicator
            var isSwitch = currDir != 0 && currDir != state.PrevDir;
            if (currDir != 0) state.PrevDir = currDir;
            
            // Return delta: +1 for new bullish switch, -1 for new bearish switch, 0 for no change
            return isSwitch ? currDir : 0;
        }

        /// <summary>
        /// Aggregate multiple indicator deltas into a combined signal
        /// </summary>
        internal int AggregateDeltas(int[] deltas)
        {
            switch (SignalCombination)
            {
                case SignalCombinationMethod.Sum:
                    return deltas.Sum(); // Your +2 example: both indicators = +1 each

                case SignalCombinationMethod.Majority:
                    var sum = deltas.Sum();
                    return Math.Sign(sum); // -1, 0, +1 only

                case SignalCombinationMethod.Consensus:
                    // All must agree or result is neutral
                    if (deltas.All(d => d > 0)) return 1;
                    if (deltas.All(d => d < 0)) return -1;
                    return 0;

                case SignalCombinationMethod.Weighted:
                    // Use indicator weights if available
                    var weightedSum = 0.0;
                    for (int i = 0; i < deltas.Length; i++)
                    {
                        var weight = (IndicatorWeights != null && i < IndicatorWeights.Length)
                            ? IndicatorWeights[i] : 1.0;
                        weightedSum += deltas[i] * weight;
                    }
                    // Use symmetric rounding to avoid directional bias
                    return (int)Math.Round(weightedSum, 0, MidpointRounding.AwayFromZero);

                default:
                    return deltas.Sum();
            }
        }

        /// <summary>
        /// Make portfolio-level trading decision based on combined delta and thresholds
        /// </summary>
        internal void ProcessPortfolioDecision(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, double combinedDelta,
            ref PortfolioState state, ref double balance, ref double totalUsedBalance)
        {
            // Exit logic: close positions when signal weakens
            if (state.HoldingStock)
            {
                if (state.StockPosition > 0 && combinedDelta < LongExitThreshold)
                {
                    ExitLongPosition(priceRecords, guideRecords, dayBarIndex, barIndex, ref state, ref balance, ref totalUsedBalance);
                }
                else if (state.StockPosition < 0 && combinedDelta > ShortExitThreshold)
                {
                    ExitShortPosition(priceRecords, guideRecords, dayBarIndex, barIndex, ref state, ref balance, ref totalUsedBalance);
                }
            }

            // Enhanced option exit logic using option-specific thresholds
            if (state.HoldingOption)
            {
                bool shouldExit = false;

                if (state.IsCallOption)
                {
                    // Long call: exit when bullish signal weakens below call exit threshold
                    shouldExit = combinedDelta <= LongCallExitThreshold;
                }
                else
                {
                    // Long put: exit when bearish signal weakens above put exit threshold  
                    shouldExit = combinedDelta >= LongPutExitThreshold;
                }

                if (shouldExit)
                {
                    ExitOptionPosition(priceRecords, guideRecords, dayBarIndex, barIndex, ref state, ref balance, ref totalUsedBalance);
                }
            }

            // Entry logic: open positions when signal is strong enough
            if (!state.HoldingStock && !state.HoldingOption)
            {
                // Option entry logic using new option-specific thresholds
                if (AllowedSecurityTypes == AllowedSecurityType.Option || AllowedSecurityTypes == AllowedSecurityType.Any)
                {
                    if (combinedDelta >= LongCallEntryThreshold &&
                        (AllowedOptionTypes == AllowedOptionType.Calls || AllowedOptionTypes == AllowedOptionType.Any))
                    {
                        EnterCallPosition(priceRecords, guideRecords, dayBarIndex, barIndex, ref state, ref balance, ref totalUsedBalance);
                    }
                    else if (combinedDelta <= LongPutEntryThreshold &&
                             (AllowedOptionTypes == AllowedOptionType.Puts || AllowedOptionTypes == AllowedOptionType.Any))
                    {
                        EnterPutPosition(priceRecords, guideRecords, dayBarIndex, barIndex, ref state, ref balance, ref totalUsedBalance);
                    }
                }

                // Stock entry logic (existing) - only if no option position was opened
                if (!state.HoldingOption && (AllowedSecurityTypes == AllowedSecurityType.Stock || AllowedSecurityTypes == AllowedSecurityType.Any))
                {
                    if (combinedDelta >= LongEntryThreshold)
                    {
                        EnterLongPosition(priceRecords, guideRecords, dayBarIndex, barIndex, ref state, ref balance, ref totalUsedBalance);
                    }
                    else if (combinedDelta <= ShortEntryThreshold)
                    {
                        EnterShortPosition(priceRecords, guideRecords, dayBarIndex, barIndex, ref state, ref balance, ref totalUsedBalance);
                    }
                }
            }
        }

        /// <summary>
        /// Enter a call option position for portfolio-level aggregation mode
        /// </summary>
        internal void EnterCallPosition(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, ref PortfolioState state,
            ref double balance, ref double totalUsedBalance)
        {
            try
            {
                //HACK: this is a hack!  Also in the PUT code...!!!
                if (guideRecords[barIndex].TimeFrame == TimeFrame.BridgeBar)
                    return;

                var currentDateTime = guideRecords[barIndex].DateTime;
                var tradeAmount = balance * TradePercentageForOptions;

                // Get call option price using the OptionPrices system
                var optionRecord = OptionsPrices.GetOptionPrice(
                    GeneticIndividual.Prices,
                    Polygon2.OptionType.Call,
                    currentDateTime,
                    guideRecords[barIndex].TimeFrame,
                    OptionDaysOut,
                    OptionStrikeDistance
                );

                if (optionRecord?.Option == null) return; // No option available

                var optionPrice = optionRecord.Close;
                if (optionPrice <= 0.01) return; // Option too cheap or invalid
                
                if (!IsOptionPriceRealistic(optionPrice, guideRecords[barIndex].Close, OptionStrikeDistance, OptionDaysOut))
                    return;

                // Calculate position size (number of contracts)
                var contractCost = optionPrice * 100; // Standard option multiplier
                var numContracts = Math.Floor(tradeAmount / contractCost);

                if (numContracts < 1) return; // Not enough for even 1 contract

                var totalCost = numContracts * contractCost;

                // Update portfolio state
                state.HoldingOption = true;
                state.OpenOptionIndex = barIndex;
                state.OpenOptionPrice = optionPrice;
                state.OptionPosition = numContracts;
                state.OptionStrike = optionRecord.Option.StrikePrice ?? 0;
                state.IsCallOption = true;
                state.PriceRecordForOpen = optionRecord;

                // Update balances
                balance -= totalCost;
                if (AllowMultipleTrades) totalUsedBalance += totalCost;

                var actionTag = "PORTFOLIO_CALL_ENTRY;";
                TradeActions[dayBarIndex] += actionTag;

                OnTradeOpened(new TradeOpenedEventArgs
                {
                    TradeIndex = barIndex,
                    DateTime = currentDateTime,
                    Price = optionPrice,
                    Position = numContracts,
                    SecurityType = AllowedSecurityType.Option,
                    TradeType = AllowedTradeType.Buy,
                    OptionType = AllowedOptionType.Calls,
                    IndicatorIndex = -1, // Portfolio-level trade
                    ActionTag = actionTag,
                    Balance = balance
                });
            }
            catch (Exception)
            {
                // If option entry fails, log error but don't crash
                var actionTag = "PORTFOLIO_CALL_ENTRY_ERROR;";
                TradeActions[barIndex] += actionTag;
            }
        }

        internal bool IsOptionPriceRealistic(double optionPrice, double underlyingPrice, int strikeDistance, int daysToExpiration)
        {
            // Minimum exchange tick size
            if (optionPrice < 0.05) return false;

            // Sanity check: option shouldn't be more than underlying price
            if (optionPrice > underlyingPrice) return false;

            // Deep OTM with little time should be very cheap
            if (strikeDistance > 20 && daysToExpiration < 7 && optionPrice > 0.50) return false;

            // ATM options with decent time should have some value
            if (strikeDistance <= 2 && daysToExpiration >= 7 && optionPrice < 0.50) return false;

            return true;
        }

        /// <summary>
        /// Enter a put option position for portfolio-level aggregation mode
        /// </summary>
        internal void EnterPutPosition(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, ref PortfolioState state,
            ref double balance, ref double totalUsedBalance)
        {
            try
            {
                //HACK: this is a hack!  Also in the CALL code...!!!
                if (guideRecords[barIndex].TimeFrame == TimeFrame.BridgeBar)
                    return;

                var currentDateTime = guideRecords[barIndex].DateTime;
                var tradeAmount = balance * TradePercentageForOptions;

                // Get put option price using the OptionPrices system
                var optionRecord = OptionsPrices.GetOptionPrice(
                    GeneticIndividual.Prices,
                    Polygon2.OptionType.Put,
                    currentDateTime,
                    guideRecords[barIndex].TimeFrame,
                    OptionDaysOut,
                    OptionStrikeDistance
                );
                
                if (optionRecord?.Option == null) return; // No option available

                var optionPrice = optionRecord.Close;
                if (optionPrice <= 0.01) return; // Option too cheap or invalid

                if (!IsOptionPriceRealistic(optionPrice, guideRecords[barIndex].Close, OptionStrikeDistance, OptionDaysOut))
                    return;

                // Calculate position size (number of contracts)
                var contractCost = optionPrice * 100; // Standard option multiplier
                var numContracts = Math.Floor(tradeAmount / contractCost);

                if (numContracts < 1) return; // Not enough for even 1 contract

                var totalCost = numContracts * contractCost;

                // Update portfolio state
                state.HoldingOption = true;
                state.OpenOptionIndex = barIndex;
                state.OpenOptionPrice = optionPrice;
                state.OptionPosition = numContracts;
                state.OptionStrike = optionRecord.Option.StrikePrice ?? 0;
                state.IsCallOption = false;
                state.PriceRecordForOpen = optionRecord;

                // Update balances
                balance -= totalCost;
                if (AllowMultipleTrades) totalUsedBalance += totalCost;

                var actionTag = "PORTFOLIO_PUT_ENTRY;";
                TradeActions[dayBarIndex] += actionTag;

                OnTradeOpened(new TradeOpenedEventArgs
                {
                    TradeIndex = barIndex,
                    DateTime = currentDateTime,
                    Price = optionPrice,
                    Position = numContracts,
                    SecurityType = AllowedSecurityType.Option,
                    TradeType = AllowedTradeType.Buy,
                    OptionType = AllowedOptionType.Puts,
                    IndicatorIndex = -1, // Portfolio-level trade
                    ActionTag = actionTag,
                    Balance = balance
                });
            }
            catch (Exception)
            {
                // If option entry fails, log error but don't crash
                var actionTag = "PORTFOLIO_PUT_ENTRY_ERROR;";
                TradeActions[barIndex] += actionTag;
            }
        }

        /// <summary>
        /// Process single bar for single indicator - CORE CONCURRENT LOGIC
        /// </summary>
        internal void ProcessSingleBarForIndicator(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, List<double> signals,
            IndicatorParams indicator, int indicatorIndex, ref IndicatorState state, 
            ref double balance, ref double totalUsedBalance)
        {
            var polarity = indicator.Polarity;
            
            // Current slope direction (using indicator's own PrevDir)
            var rawSlope = Math.Sign(signals[barIndex] - signals[barIndex - 1]);
            var currDir = rawSlope * polarity;
            var isSwitch = currDir != 0 && currDir != state.PrevDir;

            // Entry/exit logic only on direction switches
            if (isSwitch)
            {
                if (state.HoldingOption)
                {
                    // Convert single indicator direction to "combined delta" for threshold comparison
                    var singleIndicatorDelta = currDir; // -1, 0, +1
                    bool shouldExit = false;

                    if (state.IsCallOption)
                    {
                        shouldExit = singleIndicatorDelta <= LongCallExitThreshold;
                    }
                    else
                    {
                        shouldExit = singleIndicatorDelta >= LongPutExitThreshold;
                    }

                    if (shouldExit)
                    {
                        ExitOptionPosition(priceRecords, guideRecords, dayBarIndex, barIndex, indicator, ref state, ref balance, ref totalUsedBalance, indicatorIndex);
                    }
                }

                // Exit first if holding opposite position
                if (state.HoldingStock && ((state.StockPosition > 0 && currDir < 0) || (state.StockPosition < 0 && currDir > 0)))
                {
                    if (state.StockPosition > 0)
                        ExitLongStockPosition(priceRecords, guideRecords, dayBarIndex, barIndex, ref state, ref balance, ref totalUsedBalance, indicatorIndex);
                    else
                        ExitShortStockPosition(priceRecords, guideRecords, dayBarIndex, barIndex, ref state, ref balance, ref totalUsedBalance, indicatorIndex);
                }
                
                // Replace the existing entry logic in ProcessSingleBarForIndicator with:
                if (!state.HoldingStock && !state.HoldingOption)
                {
                    double availableBalance = AllowMultipleTrades ? balance - totalUsedBalance : balance;
                    if (availableBalance > 0)
                    {
                        double tradeAmountForOptions = (balance * TradePercentageForOptions) / Indicators.Count;
                        if (tradeAmountForOptions <= availableBalance && tradeAmountForOptions >= 1.0) // Minimum $1 trade
                        {
                            // Try options first if enabled, then fall back to stocks
                            if (AllowedSecurityTypes == AllowedSecurityType.Option || AllowedSecurityTypes == AllowedSecurityType.Any)
                            {
                                ProcessOptionTradingForState(priceRecords, guideRecords, dayBarIndex, barIndex, indicatorIndex, currDir, polarity, tradeAmountForOptions,
                                    ref state, ref totalUsedBalance, ref balance);
                            }
                        }

                        double tradeAmountForStocks = (balance * TradePercentageForStocks) / Indicators.Count;
                        if (tradeAmountForStocks <= availableBalance && tradeAmountForStocks >= 1.0) // Minimum $1 trade
                        {
                            // If no option position was opened and stocks are allowed, try stock trading
                            if (!state.HoldingOption && (AllowedSecurityTypes == AllowedSecurityType.Stock || AllowedSecurityTypes == AllowedSecurityType.Any))
                            {
                                ProcessStockTradingForState(priceRecords, guideRecords, dayBarIndex, barIndex, indicatorIndex, currDir, polarity, tradeAmountForStocks,
                                    ref state, ref totalUsedBalance, ref balance);
                            }
                        }
                    }
                }
            }
            
            // Update this indicator's PrevDir
            if (currDir != 0) state.PrevDir = currDir;
        }
        
        // Helper methods for state-based trading
        internal void FinalizeIndicatorPositions(IndicatorState[] indicatorStates, PriceRecord[] priceRecords, PriceRecord[] guideRecords, ref double balance)
        {
            for (int k = 0; k < indicatorStates.Length; k++)
            {
                if (indicatorStates[k].HoldingStock)
                {
                    var holding = indicatorStates[k].HoldingStock;
                    var position = indicatorStates[k].StockPosition;
                    var openIndex = indicatorStates[k].OpenStockIndex;
                    var openPrice = indicatorStates[k].OpenStockPrice;
                    FinalizeStockTrades(priceRecords, guideRecords, ref holding, ref position, ref openIndex, ref openPrice, ref balance, k);
                    indicatorStates[k].HoldingStock = holding;
                    indicatorStates[k].StockPosition = position;
                    indicatorStates[k].OpenStockIndex = openIndex;
                    indicatorStates[k].OpenStockPrice = openPrice;
                }

                if (indicatorStates[k].HoldingOption)
                {
                    var holding = indicatorStates[k].HoldingOption;
                    var position = indicatorStates[k].OptionPosition;
                    var openIndex = indicatorStates[k].OpenOptionIndex;
                    var openPrice = indicatorStates[k].OpenOptionPrice;
                    var strike = indicatorStates[k].OptionStrike;
                    var isCall = indicatorStates[k].IsCallOption;
                    var priceRecord = indicatorStates[k].PriceRecordForOpen;
                    FinalizeOptionTrades(priceRecords, guideRecords, ref holding, ref position, ref openIndex, ref openPrice, 
                        ref strike, ref isCall, ref priceRecord, ref balance, k);
                    indicatorStates[k].HoldingOption = holding;
                    indicatorStates[k].OptionPosition = position;
                    indicatorStates[k].OpenOptionIndex = openIndex;
                    indicatorStates[k].OpenOptionPrice = openPrice;
                    indicatorStates[k].OptionStrike = strike;
                    indicatorStates[k].IsCallOption = isCall;
                    indicatorStates[k].PriceRecordForOpen = priceRecord;
                }
            }
        }

        internal void ExitLongStockPosition(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, ref IndicatorState state,
            ref double balance, ref double totalUsedBalance, int indicatorIndex)
        {
            var sellPrice = guideRecords[barIndex].Close;
            var grossProceeds = state.StockPosition * sellPrice;
            balance += grossProceeds;
            
            if (AllowMultipleTrades) 
            {
                totalUsedBalance -= state.StockPosition * state.OpenStockPrice;
            }

            var trade = new TradeResult
            {
                OpenIndex = state.OpenStockIndex,
                CloseIndex = barIndex,
                OpenPrice = state.OpenStockPrice,
                ClosePrice = sellPrice,
                AllowedTradeType = AllowedTradeType.Buy,
                AllowedSecurityType = AllowedSecurityType.Stock,
                Position = state.StockPosition,
                PositionInDollars = state.StockPosition * state.OpenStockPrice,
                Balance = balance,
                ResponsibleIndicatorIndex = indicatorIndex,
                PriceRecordForOpen = state.PriceRecordForOpen,
                PriceRecordForClose = guideRecords[barIndex]
            };
            Trades.Add(trade);

            var actionTag = indicatorIndex >= 0 ? $"SE{indicatorIndex};" : "SE;";
            TradeActions[dayBarIndex] += actionTag;

            OnTradeClosed(new TradeClosedEventArgs
            {
                Trade = trade,
                DateTime = guideRecords[barIndex].DateTime,
                ClosePrice = sellPrice,
                Proceeds = grossProceeds,
                Balance = balance,
                ActionTag = actionTag,
                IsEarlyTakeProfit = false
            });

            state.HoldingStock = false;
            state.StockPosition = 0.0;
        }

        internal void ExitShortStockPosition(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, ref IndicatorState state,
            ref double balance, ref double totalUsedBalance, int indicatorIndex)
        {
            var coverPrice = guideRecords[barIndex].Close;
            var cashToCovers = Math.Abs(state.StockPosition) * coverPrice;
            balance -= cashToCovers;
        
            if (AllowMultipleTrades) 
            {
                totalUsedBalance -= Math.Abs(state.StockPosition) * state.OpenStockPrice;
            }

            var trade = new TradeResult
            {
                OpenIndex = state.OpenStockIndex,
                CloseIndex = barIndex,
                OpenPrice = state.OpenStockPrice,
                ClosePrice = coverPrice,
                AllowedTradeType = AllowedTradeType.SellShort,
                AllowedSecurityType = AllowedSecurityType.Stock,
                Position = state.StockPosition,
                PositionInDollars = Math.Abs(state.StockPosition) * state.OpenStockPrice,
                Balance = balance,
                ResponsibleIndicatorIndex = indicatorIndex,
                PriceRecordForOpen = state.PriceRecordForOpen,
                PriceRecordForClose = guideRecords[barIndex]
            };
            Trades.Add(trade);

            var actionTag = indicatorIndex >= 0 ? $"BC{indicatorIndex};" : "BC;";
            TradeActions[dayBarIndex] += actionTag;

            OnTradeClosed(new TradeClosedEventArgs
            {
                Trade = trade,
                DateTime = guideRecords[barIndex].DateTime,
                ClosePrice = coverPrice,
                Proceeds = -cashToCovers,
                Balance = balance,
                ActionTag = actionTag,
                IsEarlyTakeProfit = false
            });

            state.HoldingStock = false;
            state.StockPosition = 0.0;
        }

        internal void ExitOptionPosition(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, IndicatorParams indicator,
    ref IndicatorState state, ref double balance, ref double totalUsedBalance, int indicatorIndex)
        {
            if (!state.HoldingOption) return; // Safety check

            try
            {
                double currentOptionPrice;
                PriceRecord optionRecord = null;

                // Get current option price using the same pattern as PortfolioState version
                if (state.PriceRecordForOpen?.Option != null)
                {
                    optionRecord = OptionsPrices.GetPricesForSymbol(state.PriceRecordForOpen.Option.Symbol)?
                        .GetPriceAt(guideRecords[barIndex].DateTime);

                    if (optionRecord != null)
                        currentOptionPrice = optionRecord.Close;
                    else
                        currentOptionPrice = 0.01; // Assume worthless if no price found
                }
                else
                {
                    currentOptionPrice = 0.01; // Fallback if no opening record
                }

                currentOptionPrice = Math.Max(0.001, currentOptionPrice); // Minimum $0.001

                // Calculate proceeds from selling the option position
                var grossProceeds = Math.Abs(state.OptionPosition) * currentOptionPrice * 100; // Option multiplier
                balance += grossProceeds;

                // Handle multiple trades accounting
                if (AllowMultipleTrades)
                {
                    var originalCost = Math.Abs(state.OptionPosition) * state.OpenOptionPrice * 100;
                    totalUsedBalance -= originalCost;
                }

                // Create trade record with BOTH PriceRecordForOpen AND PriceRecordForClose
                var trade = new TradeResult
                {
                    OpenIndex = state.OpenOptionIndex,
                    CloseIndex = barIndex,
                    OpenPrice = state.OpenOptionPrice,
                    ClosePrice = currentOptionPrice,
                    AllowedTradeType = AllowedTradeType.Buy,
                    AllowedSecurityType = AllowedSecurityType.Option,
                    Position = state.OptionPosition,
                    PositionInDollars = Math.Abs(state.OptionPosition) * state.OpenOptionPrice * 100,
                    Balance = balance,
                    ResponsibleIndicatorIndex = indicatorIndex,
                    AllowedOptionType = state.IsCallOption ? AllowedOptionType.Calls : AllowedOptionType.Puts,
                    // THIS IS THE KEY FIX - populate both price records:
                    PriceRecordForOpen = state.PriceRecordForOpen,
                    PriceRecordForClose = optionRecord
                };
                Trades.Add(trade);

                // Create action tag
                var optionType = state.IsCallOption ? "CALL" : "PUT";
                var actionTag = indicatorIndex >= 0 ? $"OPT_EXIT_{optionType}{indicatorIndex};" : $"OPT_EXIT_{optionType};";
                TradeActions[dayBarIndex] += actionTag;

                // Fire trade closed event
                OnTradeClosed(new TradeClosedEventArgs
                {
                    Trade = trade,
                    DateTime = guideRecords[barIndex].DateTime,
                    ClosePrice = currentOptionPrice,
                    Proceeds = grossProceeds,
                    Balance = balance,
                    ActionTag = actionTag,
                    IsEarlyTakeProfit = false
                });
            }
            catch (Exception)
            {
                // If option exit fails, just clear the position state
                var actionTag = indicatorIndex >= 0 ? $"OPT_EXIT_ERROR{indicatorIndex};" : "OPT_EXIT_ERROR;";
                TradeActions[dayBarIndex] += actionTag;
            }
            finally
            {
                // Always clear the position state
                state.HoldingOption = false;
                state.OptionPosition = 0.0;
                state.OptionStrike = 0.0;
                state.IsCallOption = false;
                state.PriceRecordForOpen = null;
            }
        }

        internal void ProcessStockTradingForState(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, int indicatorIndex, int deltaDirection,
    int polarity, double tradeAmount, ref IndicatorState state, ref double totalUsedBalance, ref double balance)
        {
            // Calculate effective signal strength (always positive for bullish, negative for bearish)
            var effectiveSignal = deltaDirection;

            // Enhanced entry logic: signals must exceed minimum thresholds
            if ((AllowedTradeTypes == AllowedTradeType.Buy || AllowedTradeTypes == AllowedTradeType.Any) &&
                effectiveSignal > Math.Max(0, LongEntryThreshold))
            {
                // Enter long stock position
                state.HoldingStock = true;
                state.OpenStockIndex = barIndex;
                state.OpenStockPrice = guideRecords[barIndex].Close;
                state.StockPosition = tradeAmount / state.OpenStockPrice;

                var cashRequired = state.StockPosition * state.OpenStockPrice;
                balance -= cashRequired;

                if (AllowMultipleTrades) totalUsedBalance += cashRequired;

                var actionTag = indicatorIndex >= 0 ? $"BU{indicatorIndex};" : "BU;";
                TradeActions[dayBarIndex] += actionTag;

                OnTradeOpened(new TradeOpenedEventArgs
                {
                    TradeIndex = barIndex,
                    DateTime = guideRecords[barIndex].DateTime,
                    Price = state.OpenStockPrice,
                    Position = state.StockPosition,
                    SecurityType = AllowedSecurityType.Stock,
                    TradeType = AllowedTradeType.Buy,
                    OptionType = null,
                    IndicatorIndex = indicatorIndex,
                    ActionTag = actionTag,
                    Balance = balance
                });
            }
            else if ((AllowedTradeTypes == AllowedTradeType.SellShort || AllowedTradeTypes == AllowedTradeType.Any) &&
                     effectiveSignal < Math.Min(0, ShortEntryThreshold))
            {
                // Enter short stock position
                state.HoldingStock = true;
                state.OpenStockIndex = barIndex;
                state.OpenStockPrice = guideRecords[barIndex].Close;
                state.StockPosition = -tradeAmount / state.OpenStockPrice;

                var proceedsReceived = Math.Abs(state.StockPosition) * state.OpenStockPrice;
                balance += proceedsReceived;

                if (AllowMultipleTrades) totalUsedBalance += tradeAmount;

                var actionTag = indicatorIndex >= 0 ? $"SS{indicatorIndex};" : "SS;";
                TradeActions[dayBarIndex] += actionTag;

                OnTradeOpened(new TradeOpenedEventArgs
                {
                    TradeIndex = barIndex,
                    DateTime = guideRecords[barIndex].DateTime,
                    Price = state.OpenStockPrice,
                    Position = state.StockPosition,
                    SecurityType = AllowedSecurityType.Stock,
                    TradeType = AllowedTradeType.SellShort,
                    OptionType = null,
                    IndicatorIndex = indicatorIndex,
                    ActionTag = actionTag,
                    Balance = balance
                });
            }
        }

        // Portfolio-level position methods (simplified for aggregation mode)
        internal void EnterLongPosition(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, ref PortfolioState state, 
            ref double balance, ref double totalUsedBalance)
        {
            var currentPrice = guideRecords[barIndex].Close;
            var tradeAmount = balance * TradePercentageForStocks;
            var shareCount = tradeAmount / currentPrice;

            state.HoldingStock = true;
            state.OpenStockIndex = barIndex;
            state.OpenStockPrice = currentPrice;
            state.StockPosition = shareCount;
            state.PriceRecordForOpen = guideRecords[barIndex];

            var cashRequired = shareCount * currentPrice;
            balance -= cashRequired;
            if (AllowMultipleTrades) totalUsedBalance += cashRequired;

            var actionTag = "PORTFOLIO_LONG;";
            TradeActions[dayBarIndex] += actionTag;

            OnTradeOpened(new TradeOpenedEventArgs
            {
                TradeIndex = barIndex,
                DateTime = guideRecords[barIndex].DateTime,
                Price = currentPrice,
                Position = shareCount,
                SecurityType = AllowedSecurityType.Stock,
                TradeType = AllowedTradeType.Buy,
                OptionType = null,
                IndicatorIndex = -1,
                ActionTag = actionTag,
                Balance = balance,
            });
        }

        internal void EnterShortPosition(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, ref PortfolioState state, 
            ref double balance, ref double totalUsedBalance)
        {
            var currentPrice = guideRecords[barIndex].Close;
            var tradeAmount = balance * TradePercentageForStocks;
            var shareCount = tradeAmount / currentPrice;

            state.HoldingStock = true;
            state.OpenStockIndex = barIndex;
            state.OpenStockPrice = currentPrice;
            state.StockPosition = -shareCount;
            state.PriceRecordForOpen = guideRecords[barIndex];

            var proceedsReceived = shareCount * currentPrice;
            balance += proceedsReceived;
            if (AllowMultipleTrades) totalUsedBalance += tradeAmount;

            var actionTag = "PORTFOLIO_SHORT;";
            TradeActions[dayBarIndex] += actionTag;

            OnTradeOpened(new TradeOpenedEventArgs
            {
                TradeIndex = barIndex,
                DateTime = guideRecords[barIndex].DateTime,
                Price = currentPrice,
                Position = -shareCount,
                SecurityType = AllowedSecurityType.Stock,
                TradeType = AllowedTradeType.SellShort,
                OptionType = null,
                IndicatorIndex = -1,
                ActionTag = actionTag,
                Balance = balance
            });
        }

        internal void ExitLongPosition(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, ref PortfolioState state, 
            ref double balance, ref double totalUsedBalance)
        {
            var sellPrice = guideRecords[barIndex].Close;
            var grossProceeds = state.StockPosition * sellPrice;
            balance += grossProceeds;
            
            if (AllowMultipleTrades) 
            {
                totalUsedBalance -= state.StockPosition * state.OpenStockPrice;
            }

            var trade = new TradeResult
            {
                OpenIndex = state.OpenStockIndex,
                CloseIndex = barIndex,
                OpenPrice = state.OpenStockPrice,
                ClosePrice = sellPrice,
                AllowedTradeType = AllowedTradeType.Buy,
                AllowedSecurityType = AllowedSecurityType.Stock,
                Position = state.StockPosition,
                PositionInDollars = state.StockPosition * state.OpenStockPrice,
                Balance = balance,
                ResponsibleIndicatorIndex = -1,
                PriceRecordForOpen = state.PriceRecordForOpen,
                PriceRecordForClose = guideRecords[barIndex]
            };
            Trades.Add(trade);

            var actionTag = "PORTFOLIO_EXIT_LONG;";
            TradeActions[dayBarIndex] += actionTag;

            OnTradeClosed(new TradeClosedEventArgs
            {
                Trade = trade,
                DateTime = guideRecords[barIndex].DateTime,
                ClosePrice = sellPrice,
                Proceeds = grossProceeds,
                Balance = balance,
                ActionTag = actionTag,
                IsEarlyTakeProfit = false
            });

            state.HoldingStock = false;
            state.StockPosition = 0.0;
        }

        internal void ExitShortPosition(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, ref PortfolioState state, 
            ref double balance, ref double totalUsedBalance)
        {
            var coverPrice = guideRecords[barIndex].Close;
            var cashToCovers = Math.Abs(state.StockPosition) * coverPrice;
            balance -= cashToCovers;
        
            if (AllowMultipleTrades) 
            {
                totalUsedBalance -= Math.Abs(state.StockPosition) * state.OpenStockPrice;
            }

            var trade = new TradeResult
            {
                OpenIndex = state.OpenStockIndex,
                CloseIndex = barIndex,
                OpenPrice = state.OpenStockPrice,
                ClosePrice = coverPrice,
                AllowedTradeType = AllowedTradeType.SellShort,
                AllowedSecurityType = AllowedSecurityType.Stock,
                Position = state.StockPosition,
                PositionInDollars = Math.Abs(state.StockPosition) * state.OpenStockPrice,
                Balance = balance,
                ResponsibleIndicatorIndex = -1,
                PriceRecordForOpen = state.PriceRecordForOpen,
                PriceRecordForClose = guideRecords[barIndex]
            };
            Trades.Add(trade);

            var actionTag = "PORTFOLIO_EXIT_SHORT;";
            TradeActions[dayBarIndex] += actionTag;

            OnTradeClosed(new TradeClosedEventArgs
            {
                Trade = trade,
                DateTime = guideRecords[barIndex].DateTime,
                ClosePrice = coverPrice,
                Proceeds = -cashToCovers,
                Balance = balance,
                ActionTag = actionTag,
                IsEarlyTakeProfit = false
            });

            state.HoldingStock = false;
            state.StockPosition = 0.0;
        }

        internal void ProcessOptionTradingForState(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int currentBar, int indicatorIndex,
            int currDir, int polarity, double tradeAmount, ref IndicatorState state,
            ref double totalUsedBalance, ref double balance)
        {
            // Check if options trading is allowed
            if (AllowedSecurityTypes != AllowedSecurityType.Option && AllowedSecurityTypes != AllowedSecurityType.Any)
                return;

            // Calculate effective signal strength (same pattern as stock trading)
            var effectiveSignal = currDir;

            // Enhanced entry logic with proper threshold validation (matching stock trading pattern)
            if ((AllowedOptionTypes == AllowedOptionType.Calls || AllowedOptionTypes == AllowedOptionType.Any) &&
                effectiveSignal >= LongCallEntryThreshold) // Use proper threshold instead of simple > 0 check
            {
                EnterOptionPosition(priceRecords, guideRecords, dayBarIndex, currentBar, indicatorIndex, true, tradeAmount, ref state,
                    ref totalUsedBalance, ref balance);
            }
            else if ((AllowedOptionTypes == AllowedOptionType.Puts || AllowedOptionTypes == AllowedOptionType.Any) &&
                     effectiveSignal <= LongPutEntryThreshold) // Use proper threshold instead of simple < 0 check
            {
                EnterOptionPosition(priceRecords, guideRecords, dayBarIndex, currentBar, indicatorIndex, false, tradeAmount, ref state,
                    ref totalUsedBalance, ref balance);
            }
        }

        internal void EnterOptionPosition(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, int indicatorIndex, bool isCall,
            double tradeAmount, ref IndicatorState state, ref double totalUsedBalance, ref double balance)
        {
            try
            {
                var timeFrame = guideRecords[barIndex].TimeFrame;

                if (timeFrame == TimeFrame.BridgeBar)
                    return;

                var currentDateTime = guideRecords[barIndex].DateTime;

                // Get option price using the OptionPrices system
                var optionRecord = OptionsPrices.GetOptionPrice(
                    GeneticIndividual.Prices,
                    isCall ? Polygon2.OptionType.Call : Polygon2.OptionType.Put,
                    currentDateTime,
                    timeFrame,
                    OptionDaysOut,
                    OptionStrikeDistance
                );

                if (optionRecord?.Option == null) return; // No option available

                var optionPrice = optionRecord.Close;
                if (optionPrice <= 0.01) return; // Option too cheap or invalid

                // Calculate position size (number of contracts)
                var contractCost = optionPrice * 100; // Standard option multiplier
                var numContracts = Math.Floor(tradeAmount / contractCost);

                if (numContracts < 1) return; // Not enough for even 1 contract

                var totalCost = numContracts * contractCost;

                // Update state
                state.HoldingOption = true;
                state.OpenOptionIndex = barIndex;
                state.OpenOptionPrice = optionPrice;
                state.OptionPosition = numContracts;
                state.OptionStrike = optionRecord.Option.StrikePrice ?? 0;
                state.IsCallOption = isCall;
                state.PriceRecordForOpen = optionRecord;

                // Update balances
                balance -= totalCost;
                if (AllowMultipleTrades) totalUsedBalance += totalCost;

                var optionType = isCall ? "CALL" : "PUT";
                var actionTag = indicatorIndex >= 0 ? $"OPT_{optionType}{indicatorIndex};" : $"OPT_{optionType};";
                TradeActions[dayBarIndex] += actionTag;

                OnTradeOpened(new TradeOpenedEventArgs
                {
                    TradeIndex = barIndex,
                    DateTime = currentDateTime,
                    Price = optionPrice,
                    Position = numContracts,
                    SecurityType = AllowedSecurityType.Option,
                    TradeType = AllowedTradeType.Buy,
                    OptionType = isCall ? AllowedOptionType.Calls : AllowedOptionType.Puts,
                    IndicatorIndex = indicatorIndex,
                    ActionTag = actionTag,
                    Balance = balance
                });
            }
            catch (Exception)
            {
                // If option trading fails, fall back to stock trading if allowed
                // This provides robustness when option data is missing
            }
        }

        internal void ExitOptionPosition(PriceRecord[] priceRecords, PriceRecord[] guideRecords, int dayBarIndex, int barIndex, ref PortfolioState state,
    ref double balance, ref double totalUsedBalance)
        {
            if (!state.HoldingOption) return; // Safety check

            try
            {
                double currentOptionPrice;
                PriceRecord optionRecord = null;

                // Get current option price using the same pattern as IndicatorState version
                if (state.PriceRecordForOpen?.Option != null)
                {
                    optionRecord = OptionsPrices.GetPricesForSymbol(state.PriceRecordForOpen.Option.Symbol)
                        .GetPriceAt(guideRecords[barIndex].DateTime);

                    if (optionRecord != null)
                        currentOptionPrice = optionRecord.Close;
                    else
                        currentOptionPrice = 0.01; // Assume worthless if no price found
                }
                else
                {
                    currentOptionPrice = 0.01; // Fallback if no opening record
                }

                currentOptionPrice = Math.Max(0.001, currentOptionPrice); // Minimum $0.001

                // Calculate proceeds from selling the option position
                var grossProceeds = Math.Abs(state.OptionPosition) * currentOptionPrice * 100; // Option multiplier
                balance += grossProceeds;

                // Handle multiple trades accounting
                if (AllowMultipleTrades)
                {
                    var originalCost = Math.Abs(state.OptionPosition) * state.OpenOptionPrice * 100;
                    totalUsedBalance -= originalCost;
                }

                // Create trade record for portfolio-level option exit
                var trade = new TradeResult
                {
                    OpenIndex = state.OpenOptionIndex,
                    CloseIndex = barIndex,
                    OpenPrice = state.OpenOptionPrice,
                    ClosePrice = currentOptionPrice,
                    AllowedTradeType = AllowedTradeType.Buy,
                    AllowedSecurityType = AllowedSecurityType.Option,
                    Position = state.OptionPosition,
                    PositionInDollars = Math.Abs(state.OptionPosition) * state.OpenOptionPrice * 100,
                    Balance = balance,
                    ResponsibleIndicatorIndex = -1, // Portfolio-level trade
                    AllowedOptionType = state.IsCallOption ? AllowedOptionType.Calls : AllowedOptionType.Puts,
                    PriceRecordForOpen = state.PriceRecordForOpen,
                    PriceRecordForClose = optionRecord
                };
                Trades.Add(trade);

                // Create action tag for portfolio-level option exit
                var optionType = state.IsCallOption ? "CALL" : "PUT";
                var actionTag = $"PORTFOLIO_OPT_EXIT_{optionType};";
                TradeActions[dayBarIndex] += actionTag;

                // Fire trade closed event
                OnTradeClosed(new TradeClosedEventArgs
                {
                    Trade = trade,
                    DateTime = guideRecords[barIndex].DateTime,
                    ClosePrice = currentOptionPrice,
                    Proceeds = grossProceeds,
                    Balance = balance,
                    ActionTag = actionTag,
                    IsEarlyTakeProfit = false
                });
            }
            catch (Exception)
            {
                // If option exit fails, just clear the position state
                // This handles cases where option price lookup fails
                var actionTag = "PORTFOLIO_OPT_EXIT_ERROR;";
                TradeActions[dayBarIndex] += actionTag;

                // Don't add proceeds on error - assume option expires worthless
                // The original premium was already debited when opened
            }
            finally
            {
                // Always clear the position state
                state.HoldingOption = false;
                state.OptionPosition = 0.0;
                state.OptionStrike = 0.0;
                state.IsCallOption = false;
                state.PriceRecordForOpen = null;
            }
        }

        internal void FinalizePortfolioPosition(ref PortfolioState state, PriceRecord[] priceRecords, PriceRecord[] guideRecords, ref double balance)
        {
            if (state.HoldingStock)
            {
                var holding = state.HoldingStock;
                var position = state.StockPosition;
                var openIndex = state.OpenStockIndex;
                var openPrice = state.OpenStockPrice;
                FinalizeStockTrades(priceRecords, guideRecords, ref holding, ref position, ref openIndex, ref openPrice, ref balance, -1);
                state.HoldingStock = holding;
                state.StockPosition = position;
                state.OpenStockIndex = openIndex;
                state.OpenStockPrice = openPrice;
            }
            
            if (state.HoldingOption)
            {
                var holding = state.HoldingOption;
                var position = state.OptionPosition;
                var openIndex = state.OpenOptionIndex;
                var openPrice = state.OpenOptionPrice;
                var strike = state.OptionStrike;
                var isCall = state.IsCallOption;
                var priceRecord = state.PriceRecordForOpen;
                FinalizeOptionTrades(priceRecords, guideRecords, ref holding, ref position, ref openIndex, ref openPrice, 
                    ref strike, ref isCall, ref priceRecord, ref balance, -1);
                state.HoldingOption = holding;
                state.OptionPosition = position;
                state.OpenOptionIndex = openIndex;
                state.OpenOptionPrice = openPrice;
                state.OptionStrike = strike;
                state.IsCallOption = isCall;
                state.PriceRecordForOpen = priceRecord;
            }
        }
        
        /// <summary>
        /// Fire the TradeOpened event
        /// </summary>
        protected virtual void OnTradeOpened(TradeOpenedEventArgs e)
        {
            try
            {
                TradeOpened?.Invoke(this, e);
            }
            catch (Exception)
            {
                // Swallow event handler exceptions to prevent trading logic interruption
            }
        }

        /// <summary>
        /// Fire the TradeClosed event
        /// </summary>
        protected virtual void OnTradeClosed(TradeClosedEventArgs e)
        {
            try
            {
                TradeClosed?.Invoke(this, e);
            }
            catch (Exception)
            {
                // Swallow event handler exceptions to prevent trading logic interruption
            }
        }
    }
}