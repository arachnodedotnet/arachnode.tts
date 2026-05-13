using System;
using System.Collections.Generic;
using System.Linq;
using Trade.Prices2;

namespace Trade
{
    public partial class GeneticIndividual
    {
        internal static double RoundMoney(double balance)
        {
            // Round to the nearest cent (2 decimal places) using banker's rounding
            // This is the standard for financial calculations and stock accounting
            return RoundMoney((decimal)balance);
        }

        internal static double RoundMoney(decimal balance)
        {
            // Round to the nearest cent (2 decimal places) using banker's rounding
            // This is the standard for financial calculations and stock accounting
            return (double)Math.Round(balance, 2, MidpointRounding.ToEven);
        }

        /// <summary>
        /// Finalize trades at end of buffer
        /// </summary>
        internal void FinalizeStockTrades(PriceRecord[] priceRecords, PriceRecord[] guideRecords, ref bool holding, ref double position,
            ref int openIndex, ref double openPrice, ref double balance, int indicatorIndex = -1)
        {
            if (holding)
            {
                var finalPrice = guideRecords[guideRecords.Length - 1].Close;

                if (position > 0)
                {
                    // Long position: sell at final price
                    // PROPER ACCOUNTING: Credit cash with full sale proceeds (principal + gain/loss)
                    var grossProceeds = position * finalPrice;
                    balance += grossProceeds;  // Credit cash with full sale amount

                    var trade = new TradeResult
                    {
                        OpenIndex = openIndex,
                        CloseIndex = guideRecords.Length - 1,
                        OpenPrice = openPrice,
                        ClosePrice = finalPrice,
                        AllowedTradeType = AllowedTradeType.Buy,
                        AllowedSecurityType = AllowedSecurityType.Stock,
                        Position = position,
                        PositionInDollars = Math.Abs(position) * openPrice,
                        Balance = balance,
                        ResponsibleIndicatorIndex = indicatorIndex,
                        PriceRecordForOpen = guideRecords[openIndex],
                        PriceRecordForClose = guideRecords[guideRecords.Length - 1]
                    };
                    Trades.Add(trade);

                    var actionTag = indicatorIndex >= 0 ? $"SE{indicatorIndex};" : "SE;";
                    TradeActions[TradeActions.Count - 1] += actionTag;
                    FinalBalance = balance;

                    // Fire the TradeClosed event
                    OnTradeClosed(new TradeClosedEventArgs
                    {
                        Trade = trade,
                        DateTime = guideRecords[guideRecords.Length - 1].DateTime,
                        ClosePrice = finalPrice,
                        Proceeds = grossProceeds,
                        Balance = balance,
                        ActionTag = actionTag,
                        IsEarlyTakeProfit = false
                    });
                }
                else if (position < 0)
                {
                    // Short position: buy to cover at final price
                    // PROPER ACCOUNTING: Debit cash to buy shares for covering the short
                    var cashToCover = Math.Abs(position) * finalPrice;
                    balance -= cashToCover;  // Debit cash to buy shares for covering

                    var trade = new TradeResult
                    {
                        OpenIndex = openIndex,
                        CloseIndex = guideRecords.Length - 1,
                        OpenPrice = openPrice,
                        ClosePrice = finalPrice,
                        AllowedTradeType = AllowedTradeType.SellShort,
                        AllowedSecurityType = AllowedSecurityType.Stock,
                        Position = position,
                        PositionInDollars = Math.Abs(position) * openPrice,
                        Balance = balance,
                        ResponsibleIndicatorIndex = indicatorIndex,
                        PriceRecordForOpen = guideRecords[openIndex],
                        PriceRecordForClose = guideRecords[guideRecords.Length - 1]
                    };
                    Trades.Add(trade);

                    var actionTag = indicatorIndex >= 0 ? $"BC{indicatorIndex};" : "BC;";
                    TradeActions[TradeActions.Count - 1] += actionTag;
                    FinalBalance = balance;

                    // Fire the TradeClosed event
                    OnTradeClosed(new TradeClosedEventArgs
                    {
                        Trade = trade,
                        DateTime = guideRecords[guideRecords.Length - 1].DateTime,
                        ClosePrice = finalPrice,
                        Proceeds = -cashToCover, // Negative because it's a cost
                        Balance = balance,
                        ActionTag = actionTag,
                        IsEarlyTakeProfit = false
                    });
                }

                position = 0.0;
                holding = false;
            }
        }

        /// <summary>
        /// Finalize option trades at end of buffer
        /// </summary>
        internal void FinalizeOptionTrades(PriceRecord[] priceRecords, PriceRecord[] guideRecords, ref bool holdingOption, ref double optionPosition,
            ref int openOptionIndex, ref double openOptionPrice, ref double optionStrike, ref bool isCallOption,
            ref PriceRecord priceRecordForOpen,
            ref double balance, int indicatorIndex = -1)
        {
            if (holdingOption)
            {
                try
                {
                    double finalOptionPrice;

                    var optionRecord = OptionsPrices.GetPricesForSymbol(priceRecordForOpen.Option.Symbol)
                        .GetPriceAt(guideRecords[guideRecords.Length - 1].DateTime);

                    if (optionRecord != null)
                        finalOptionPrice = optionRecord.Close;
                    else
                        finalOptionPrice = 0;

                    finalOptionPrice = Math.Max(0.001, finalOptionPrice);

                    // PROPER ACCOUNTING: Credit cash with full sale proceeds from option sale
                    var grossProceeds = Math.Abs(optionPosition) * finalOptionPrice * 100;
                    balance += grossProceeds;  // Credit cash with sale proceeds

                    var trade = new TradeResult
                    {
                        OpenIndex = openOptionIndex,
                        CloseIndex = guideRecords.Length - 1,
                        OpenPrice = openOptionPrice,
                        ClosePrice = finalOptionPrice,
                        AllowedTradeType = AllowedTradeType.Buy,
                        AllowedSecurityType = AllowedSecurityType.Option,
                        Position = optionPosition,
                        PositionInDollars = Math.Abs(optionPosition) * openOptionPrice * 100,
                        Balance = balance,
                        ResponsibleIndicatorIndex = indicatorIndex,
                        AllowedOptionType = isCallOption ? AllowedOptionType.Calls : AllowedOptionType.Puts,
                        PriceRecordForOpen = priceRecordForOpen,
                        PriceRecordForClose = optionRecord
                    };
                    Trades.Add(trade);

                    var optionType = isCallOption ? "CALL" : "PUT";
                    var actionTag = $"OPT_FINAL_{optionType}{indicatorIndex};";
                    TradeActions[priceRecords.Length - 1] += actionTag;
                    FinalBalance = balance;

                    // Fire the TradeClosed event
                    OnTradeClosed(new TradeClosedEventArgs
                    {
                        Trade = trade,
                        DateTime = guideRecords[guideRecords.Length - 1].DateTime,
                        ClosePrice = finalOptionPrice,
                        Proceeds = grossProceeds,
                        Balance = balance,
                        ActionTag = actionTag,
                        IsEarlyTakeProfit = false
                    });
                }
                catch (Exception)
                {
                    var actionTag = "OPT_FINAL_ERROR;";
                    TradeActions[priceRecords.Length - 1] += actionTag;
                    // PROPER ACCOUNTING: On error, assume option expires worthless (no proceeds)
                    // The premium was already debited when opened, so balance remains unchanged
                    FinalBalance = balance;

                    // Note: We don't fire TradeClosed event on error since no trade record is created
                    // This is consistent with the error handling approach
                }

                optionPosition = 0.0;
                holdingOption = false;
            }
        }

        /// <summary>
        /// Calculate dynamic position size based on current market conditions and strategy performance
        /// </summary>
        private double CalculateDynamicPositionSize(PriceRecord[] priceRecords, int currentIndex,
            double accountBalance, double availableBalance, int deltaDirection, int polarity, int indicatorIndex)
        {
            try
            {
                // Build context for position sizing decision
                var context = BuildPositionSizingContext(priceRecords, currentIndex, accountBalance, availableBalance);

                // Determine trade type for sizing calculation
                var intendedTradeType = deltaDirection * polarity > 0 ? AllowedTradeType.Buy : AllowedTradeType.SellShort;

                // Calculate optimal position size
                var sizingResult =
                    _positionSizer.CalculatePositionSize(context, priceRecords[currentIndex].Close, intendedTradeType);

                // Update performance tracking
                UpdatePerformanceTracking(sizingResult, accountBalance);

                // Return calculated trade amount
                return sizingResult.PositionAmount;
            }
            catch (Exception)
            {
                // Fallback to traditional sizing if dynamic sizing fails
                return accountBalance * TradePercentageForStocks;
            }
        }

        /// <summary>
        /// Build position sizing context from current market and account state
        /// </summary>
        private PositionSizingContext BuildPositionSizingContext(PriceRecord[] priceRecords, int currentIndex,
            double accountBalance, double availableBalance)
        {
            var context = new PositionSizingContext
            {
                // Market data
                PriceHistory = priceRecords.Take(currentIndex + 1).ToArray(),
                CurrentPrice = priceRecords[currentIndex].Close,
                AverageVolume = CalculateAverageVolume(priceRecords, currentIndex),

                // Account information
                AccountBalance = accountBalance,
                AvailableBalance = availableBalance,
                UnrealizedPnL = 0.0, // Could be calculated from open positions
                MaxDrawdownFromPeak = CalculateCurrentDrawdown(accountBalance),

                // Position information
                OpenPositions = GetCurrentOpenPositions(),
                RecentTrades = Trades.Skip(Math.Max(0, Trades.Count - 50)).ToList(),
                TotalExposure = CalculateTotalExposure(accountBalance),

                // Strategy performance
                WinRate = CalculateWinRate(),
                AverageWin = CalculateAverageWin(),
                AverageLoss = CalculateAverageLoss(),
                ProfitFactor = CalculateProfitFactor(),
                SharpeRatio = CalculateSharpeRatio(),

                // Market conditions
                MarketVolatility = CalculateMarketVolatility(priceRecords, currentIndex),
                MarketMomentum = CalculateMarketMomentum(priceRecords, currentIndex),
                ATR = CalculateATR(priceRecords, currentIndex),

                // Risk metrics
                MaxCorrelationWithExisting = CalculateMaxCorrelation(),
                IsHeatPeriod = IsInHeatPeriod()
            };

            return context;
        }

        #region Context Calculation Helpers

        private double CalculateAverageVolume(PriceRecord[] priceRecords, int currentIndex)
        {
            var lookback = Math.Min(20, currentIndex + 1);
            if (lookback == 0) return 1000.0; // Default volume

            return priceRecords.Skip(currentIndex + 1 - lookback).Take(lookback).Average(p => p.Volume);
        }

        private double CalculateCurrentDrawdown(double accountBalance)
        {
            if (_peakBalance == 0) _peakBalance = StartingBalance;

            if (accountBalance > _peakBalance)
            {
                _peakBalance = accountBalance;
                return 0.0;
            }

            return (_peakBalance - accountBalance) / _peakBalance;
        }

        private List<TradeResult> GetCurrentOpenPositions()
        {
            // In this simulation, we don't track truly open positions across time
            // This would be more relevant in live trading
            return new List<TradeResult>();
        }

        private double CalculateTotalExposure(double accountBalance)
        {
            // Calculate total exposure as percentage of account
            // For now, return a simple estimate
            return 0.0; // Would need to track actual open positions
        }

        private double CalculateWinRate()
        {
            if (Trades.Count == 0) return 0.5; // Default 50%

            var winningTrades = Trades.Count(t => t.DollarGain > 0);
            return (double)winningTrades / Trades.Count;
        }

        private double CalculateAverageWin()
        {
            var winningTrades = Trades.Where(t => t.DollarGain > 0);
            return winningTrades.Any() ? winningTrades.Average(t => Math.Abs(t.ActualDollarGain)) : 0.0;
        }

        private double CalculateAverageLoss()
        {
            var losingTrades = Trades.Where(t => t.DollarGain < 0);
            return losingTrades.Any() ? losingTrades.Average(t => Math.Abs(t.ActualDollarGain)) : 0.0;
        }

        private double CalculateProfitFactor()
        {
            var grossProfit = Trades.Where(t => t.DollarGain > 0).Sum(t => t.ActualDollarGain);
            var grossLoss = Math.Abs(Trades.Where(t => t.DollarGain < 0).Sum(t => t.ActualDollarGain));

            return grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? 2.0 : 0.0;
        }

        private double CalculateSharpeRatio()
        {
            if (Trades.Count < 2) return 0.0;

            var returns = Trades.Select(t => t.PercentGain / 100.0).ToArray();
            var meanReturn = returns.Average();
            var stdDev = Math.Sqrt(returns.Select(r => Math.Pow(r - meanReturn, 2)).Average());

            return stdDev > 0 ? meanReturn / stdDev : 0.0;
        }

        private double CalculateMaxDrawdown()
        {
            if (_performanceHistory.Count < 2) return 0.0;
            double peak = _performanceHistory[0];
            double maxDrawdown = 0.0;
            foreach (var balance in _performanceHistory)
            {
                if (balance > peak) peak = balance;
                double drawdown = (peak - balance) / peak;
                if (drawdown > maxDrawdown) maxDrawdown = drawdown;
            }
            return maxDrawdown * 100.0; // Return as percentage
        }

        private double CalculateMarketVolatility(PriceRecord[] priceRecords, int currentIndex)
        {
            var lookback = Math.Min(20, currentIndex);
            if (lookback < 2) return 0.15; // Default 15% volatility

            var returns = new List<double>();
            for (var i = currentIndex - lookback + 1; i <= currentIndex; i++)
                if (i > 0 && i < priceRecords.Length)
                {
                    var dailyReturn = Math.Log(priceRecords[i].Close / priceRecords[i - 1].Close);
                    returns.Add(dailyReturn);
                }

            if (returns.Count < 2) return 0.15;

            var mean = returns.Average();
            var variance = returns.Select(r => Math.Pow(r - mean, 2)).Average();

            return Math.Sqrt(variance * 252); // Annualized volatility
        }

        private double CalculateMarketMomentum(PriceRecord[] priceRecords, int currentIndex)
        {
            var lookback = Math.Min(10, currentIndex);
            if (lookback < 2) return 0.0;

            var startIndex = currentIndex - lookback + 1;
            if (startIndex < 0 || startIndex >= priceRecords.Length) return 0.0;

            var startPrice = priceRecords[startIndex].Close;
            var endPrice = priceRecords[currentIndex].Close;

            return (endPrice - startPrice) / startPrice;
        }

        private double CalculateATR(PriceRecord[] priceRecords, int currentIndex)
        {
            var atrPeriod = Math.Min(14, currentIndex);
            if (atrPeriod < 2) return priceRecords[currentIndex].Close * 0.02; // Default 2% ATR

            var trueRanges = new List<double>();
            for (var i = Math.Max(1, currentIndex - atrPeriod + 1); i <= currentIndex; i++)
                if (i < priceRecords.Length)
                {
                    var high = priceRecords[i].High;
                    var low = priceRecords[i].Low;
                    var prevClose = i > 0 ? priceRecords[i - 1].Close : priceRecords[i].Close;

                    var trueRange = Math.Max(high - low,
                        Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose)));
                    trueRanges.Add(trueRange);
                }

            return trueRanges.Count > 0 ? trueRanges.Average() : priceRecords[currentIndex].Close * 0.02;
        }

        private double CalculateMaxCorrelation()
        {
            // Simplified correlation calculation
            // In real implementation, would calculate correlation with existing positions
            return 0.0; // Default no correlation
        }

        private bool IsInHeatPeriod()
        {
            // Check if recent performance has been poor
            var recentTrades = Trades.Skip(Math.Max(0, Trades.Count - 10)).ToList();
            if (recentTrades.Count < 5) return false;

            var recentWinRate = recentTrades.Count(t => t.DollarGain > 0) / (double)recentTrades.Count();
            return recentWinRate < 0.3; // Less than 30% win rate = heat period
        }

        private void UpdatePerformanceTracking(PositionSizingResult sizingResult, double accountBalance)
        {
            // Track performance for future sizing decisions
            _performanceHistory.Add(accountBalance);

            // Keep only recent history
            if (_performanceHistory.Count > 100) _performanceHistory.RemoveAt(0);
        }
        
        #endregion
    }
}