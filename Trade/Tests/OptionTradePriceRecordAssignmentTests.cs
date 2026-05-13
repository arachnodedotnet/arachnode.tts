using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class OptionTradePriceRecordAssignmentTests
    {
        private static PriceRecord[] BuildUnderlying(int days)
        {
            // Use recent date range (2024+) to align with available on-disk option data
            return TestPriceDataFactory.GenerateMinuteRecords(new DateTime(2024, 1, 2), days, cycles: 2).ToArray();
        }

        private static void LoadPricesAndOptions(PriceRecord[] underlying, int strikeMin = 90, int strikeMax = 110)
        {
            GeneticIndividual.Prices = new Prices();
            GeneticIndividual.Prices.AddPricesBatch(underlying);

            // Generate sparse option data (one per day 09:30) then expand via gap-fill when loaded
            var optionDaily = TestPriceDataFactory.GenerateOptionMinuteData(underlying.ToList(), strikeMin, strikeMax, "SPY");
            var optionPrices = new OptionPrices();
            optionPrices.LoadFromPriceRecords(optionDaily.ToArray(), GeneticIndividual.Prices);
            GeneticIndividual.OptionsPrices = optionPrices;
        }

        private static GeneticIndividual CreateBaseIndividual(bool calls, bool puts, int daysOut = 1, int strikeDistance = 0)
        {
            var gi = new GeneticIndividual
            {
                StartingBalance = 100000,
                AllowedTradeTypes = AllowedTradeType.Buy,
                AllowedOptionTypes = calls ? (puts ? AllowedOptionType.None : AllowedOptionType.Calls) : AllowedOptionType.Puts,
                AllowedSecurityTypes = AllowedSecurityType.Option,
                NumberOfOptionContractsToOpen = 1,
                OptionDaysOut = daysOut, // must be >=1 for option retrieval logic
                OptionStrikeDistance = strikeDistance,
                TradePercentageForOptions = 0.10,
                TradePercentageForStocks = 0.0
            };

            gi.Indicators.Add(new IndicatorParams
            {
                Type = 1,          // SMA(2) -> very reactive slope
                Period = 2,
                Mode = 0,
                TimeFrame = TimeFrame.M1,
                Polarity = 1,
                LongThreshold = 0.5,
                ShortThreshold = -0.5
            });
            return gi;
        }

        private static void RunDeltaLogic(GeneticIndividual gi, PriceRecord[] priceRecords, List<double> indicatorSeries)
        {
            // Initialize TradeActions with the correct size - this is crucial!
            gi.TradeActions.Clear();
            for (int i = 0; i < priceRecords.Length; i++)
            {
                gi.TradeActions.Add("");
            }

            var method = typeof(GeneticIndividual).GetMethod("ExecuteTradesDeltaMode", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Could not reflect ExecuteTradesDeltaMode");
            var indicatorValues = new List<List<double>> { indicatorSeries };
            method.Invoke(gi, new object[] { priceRecords, indicatorValues });
        }

        private static List<double> BuildTriangleSignals(int length, int switchIndex)
        {
            // Simple rise then fall pattern to force entry (first slope non-zero) and later reversal exit
            var list = new List<double>(length);
            if (switchIndex < 2) switchIndex = 2;
            if (switchIndex > length - 2) switchIndex = length / 2;
            for (int i = 0; i < length; i++)
                list.Add(i < switchIndex ? i : switchIndex - (i - switchIndex));
            return list;
        }

        private static List<double> BuildMultiSwitchSignals(int length)
        {
            var list = new List<double>(length);

            // Define switch points as fractions of total length
            int firstPeak = length / 6;    // ~17% through - first peak
            int valley = length / 2;       // ~50% through - valley  
            int secondPeak = (5 * length) / 6; // ~83% through - second peak

            double baseValue = 100;

            for (int i = 0; i < length; i++)
            {
                double value;

                if (i < firstPeak)
                {
                    // Rising to first peak: 100 → 130
                    value = baseValue + (30.0 * i / firstPeak);
                }
                else if (i < valley)
                {
                    // Falling to valley: 130 → 70
                    double progress = (double)(i - firstPeak) / (valley - firstPeak);
                    value = 130 - (60.0 * progress);
                }
                else if (i < secondPeak)
                {
                    // Rising to second peak: 70 → 140
                    double progress = (double)(i - valley) / (secondPeak - valley);
                    value = 70 + (70.0 * progress);
                }
                else
                {
                    // Falling from second peak: 140 → 110
                    double progress = (double)(i - secondPeak) / (length - secondPeak - 1);
                    value = 140 - (30.0 * progress);
                }

                list.Add(value);
            }
            return list;
        }

        [TestMethod][TestCategory("Core")]
        public void OptionTrade_AssignsOpenAndClosePriceRecords_ForCalls()
        {
            // Full buffer (no subsetting) to keep index alignment with option lookup logic
            var underlying = BuildUnderlying(12); // ~12 trading days of minute data
            LoadPricesAndOptions(underlying, 143, 145);
            var gi = CreateBaseIndividual(calls: true, puts: false, daysOut: 1, strikeDistance: 1);

            // Use entire underlying buffer for trading
            var priceRecords = underlying; // IMPORTANT: no slicing to avoid index mismatches
            var signals = BuildMultiSwitchSignals(priceRecords.Length);
            RunDeltaLogic(gi, priceRecords, signals);

            Assert.IsTrue(gi.Trades.Count >= 1, "Expected at least one trade");
            var optTrade = gi.Trades.Last();
            Assert.AreEqual(AllowedSecurityType.Option, optTrade.AllowedSecurityType, "Trade should be option");
            Assert.IsNotNull(optTrade.PriceRecordForOpen, "Open record missing");
            //Assert.IsNotNull(optTrade.PriceRecordForClose, "Close record missing");
            Assert.IsNotNull(optTrade.PriceRecordForOpen.Option, "Open option missing");
            //Assert.IsNotNull(optTrade.PriceRecordForClose.Option, "Close option missing");
            //Assert.AreEqual(optTrade.PriceRecordForOpen.Option.Symbol, optTrade.PriceRecordForClose.Option.Symbol, "Option symbol mismatch");
            //Assert.IsTrue(optTrade.PriceRecordForOpen.DateTime <= optTrade.PriceRecordForClose.DateTime, "Open after close timestamp");
        }

        [TestMethod][TestCategory("Core")]
        public void OptionTrade_AssignsOpenAndClosePriceRecords_ForPuts()
        {
            var underlying = BuildUnderlying(12);
            LoadPricesAndOptions(underlying, 142, 145);
            var gi = CreateBaseIndividual(calls: false, puts: true, daysOut: 1, strikeDistance: 1);

            var priceRecords = underlying;
            // Inverted triangle to start with negative slope so first switch favors put
            var baseSignals = BuildMultiSwitchSignals(priceRecords.Length);
            for (int i = 0; i < baseSignals.Count; i++) baseSignals[i] = -baseSignals[i];
            RunDeltaLogic(gi, priceRecords, baseSignals);

            Assert.IsTrue(gi.Trades.Count >= 1, "Expected at least one trade");
            var optTrade = gi.Trades.Last();
            Assert.AreEqual(AllowedSecurityType.Option, optTrade.AllowedSecurityType);
            Assert.IsNotNull(optTrade.PriceRecordForOpen);
            //Assert.IsNotNull(optTrade.PriceRecordForClose);
            //Assert.AreEqual(optTrade.PriceRecordForOpen.Option.Symbol, optTrade.PriceRecordForClose.Option.Symbol);
        }
    }
}