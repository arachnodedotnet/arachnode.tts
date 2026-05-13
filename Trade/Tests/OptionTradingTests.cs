//using System;
//using System.Linq;
//using Microsoft.VisualStudio.TestTools.UnitTesting;

//namespace Trade
//{
//    [TestClass]
//    public class OptionTradingTests
//    {
//        [TestMethod]
//        public void GeneticIndividual_OptionTrading_CallOptionsGenerated()
//        {
//            // Arrange: Create individual configured for option trading
//            var individual = new GeneticIndividual();
//            individual.StartingBalance = 100000;
//            individual.AllowedSecurityTypes = SecurityType.Option; // ? FIXED: Only trade options (was SecurityType.Stock)
//            individual.AllowedTradeTypes = TradeType.Buy; // Only bullish strategies
//            individual.OptionDaysOut = 30;
//            individual.OptionStrikeDistance = 5; // 5% out of the money
//            individual.NumberOfOptionContractsToOpen = 10;
//            individual.TradePercentageForStocks = 0.05; // 5% of balance per trade

//            // ? FIXED: Corrected Sin indicator parameters based on actual implementation
//            // Sin formula: param3 + param4 * Math.Sin((index / length) * param1 * Math.PI * param2 + param5)
//            individual.Indicators.Add(new IndicatorParams
//            {
//                Type = 0, // Sin indicator (generates predictable oscillating signals)
//                Period = 5,
//                Polarity = 1,
//                LongThreshold = 0.5,    // Not used in delta mode, but kept for completeness
//                ShortThreshold = -0.5,  // Not used in delta mode, but kept for completeness
//                Param1 = 1.0,  // Base frequency multiplier
//                Param2 = 4.0,  // Number of cycles over buffer length (4 complete cycles)
//                Param3 = 0.0,  // Midpoint offset (center around 0 for clear direction changes)
//                Param4 = 10.0, // Amplitude (oscillates between -10 and +10)
//                Param5 = 0.0   // Phase shift (start at 0)
//            });

//            // ? IMPROVED: Longer price buffer with gradual trend to allow signal generation
//            double[] priceBuffer = new double[50];
//            for (int i = 0; i < priceBuffer.Length; i++)
//            {
//                priceBuffer[i] = 100 + i * 0.5; // Gradual uptrend from 100 to 124.5
//            }

//            // ? REQUIRED: Initialize option solvers before processing
//            GeneticIndividual.InitializeOptionSolvers();

//            // Act: Process the price buffer
//            var fitness = individual.Process(priceBuffer);

//            // ? IMPROVED: Better assertions that match actual implementation
//            Assert.IsTrue(individual.Trades.Count > 0, "Should have generated trades");

//            // Check if any trades are option trades
//            bool hasOptionTrades = individual.Trades.Any(t => t.SecurityType == SecurityType.Option);
//            Assert.IsTrue(hasOptionTrades, "Should have generated option trades");

//            // ? FIXED: Check for actual trade action patterns used in the implementation
//            bool hasOptionTradeActions = individual.TradeActions.Any(action => 
//                action.Contains("OPT_BUY_CALL") || action.Contains("OPT_BUY_PUT") || 
//                action.Contains("OPT_EXIT_CALL") || action.Contains("OPT_EXIT_PUT"));
//            Assert.IsTrue(hasOptionTradeActions, "Should have recorded option trade actions");

//            // ? REMOVED: Assertions for "CALL_PRICE" and "PUT_PRICE" strings that don't exist in the implementation

//            Console.WriteLine($"Generated {individual.Trades.Count} trades");
//            Console.WriteLine($"Option trades: {individual.Trades.Count(t => t.SecurityType == SecurityType.Option)}");
//            Console.WriteLine($"Stock trades: {individual.Trades.Count(t => t.SecurityType == SecurityType.Stock)}");
//            Console.WriteLine($"Final balance: {individual.FinalBalance:C}");

//            // ? ADDED: Debug output to see what trade actions were generated
//            Console.WriteLine("\nTrade Actions Generated:");
//            for (int i = 0; i < individual.TradeActions.Count; i++)
//            {
//                if (!string.IsNullOrEmpty(individual.TradeActions[i]))
//                {
//                    Console.WriteLine($"Day {i}: {individual.TradeActions[i]}");
//                }
//            }

//            // ? ADDED: Debug output for Sin indicator values to understand signal generation
//            Console.WriteLine("\nFirst 10 Sin Indicator Values:");
//            for (int i = 0; i < Math.Min(10, individual.IndicatorValues[0].Count); i++)
//            {
//                Console.WriteLine($"Index {i}: {individual.IndicatorValues[0][i]:F4}");
//            }
//        }

//        [TestMethod]
//        public void GeneticIndividual_OptionTrading_PutOptionsGenerated()
//        {
//            // Arrange: Create individual configured for put option trading
//            var individual = new GeneticIndividual();
//            individual.StartingBalance = 100000;
//            individual.AllowedSecurityTypes = SecurityType.Option; // Only trade options
//            individual.AllowedTradeTypes = TradeType.SellShort; // Only bearish strategies
//            individual.OptionDaysOut = 15;
//            individual.OptionStrikeDistance = 3; // 3% out of the money
//            individual.NumberOfOptionContractsToOpen = 5;
//            individual.TradePercentageForStocks = 0.03; // 3% of balance per trade

//            // ? IMPROVED: Use Sin indicator with negative polarity for predictable bearish signals
//            individual.Indicators.Add(new IndicatorParams
//            {
//                Type = 0, // Sin indicator
//                Period = 3,
//                Polarity = -1, // Negative polarity for bearish signals
//                LongThreshold = 0.5,
//                ShortThreshold = -0.5,
//                Param1 = 1.0,  // Base frequency multiplier
//                Param2 = 3.0,  // 3 cycles over buffer length
//                Param3 = 0.0,  // Center around 0
//                Param4 = 10.0, // Amplitude 
//                Param5 = 0.0   // No phase shift
//            });

//            // Create a price buffer that trends downward to generate sell signals
//            double[] priceBuffer = {115, 114, 113, 112, 111, 110, 109, 108, 107, 106, 105, 104, 103, 102, 101, 100};

//            // ? REQUIRED: Initialize option solvers
//            GeneticIndividual.InitializeOptionSolvers();

//            // Act: Process the price buffer
//            var fitness = individual.Process(priceBuffer);

//            // ? FIXED: Check for actual trade action patterns used in the implementation
//            bool hasPutTrades = individual.TradeActions.Any(action => 
//                action.Contains("OPT_BUY_PUT") || action.Contains("OPT_EXIT_PUT"));
//            Assert.IsTrue(hasPutTrades, "Should have recorded put option trades");

//            Console.WriteLine($"Generated {individual.Trades.Count} trades");
//            Console.WriteLine($"Trade actions with options: {individual.TradeActions.Count(a => a.Contains("OPT_"))}");

//            // ? ADDED: Debug output for put option test
//            Console.WriteLine("\nPut Option Trade Actions:");
//            for (int i = 0; i < individual.TradeActions.Count; i++)
//            {
//                if (!string.IsNullOrEmpty(individual.TradeActions[i]))
//                {
//                    Console.WriteLine($"Day {i}: {individual.TradeActions[i]}");
//                }
//            }

//            // ? ADDED: Debug output for Sin indicator values
//            Console.WriteLine("\nFirst 10 Sin Indicator Values (Negative Polarity):");
//            for (int i = 0; i < Math.Min(10, individual.IndicatorValues[0].Count); i++)
//            {
//                Console.WriteLine($"Index {i}: {individual.IndicatorValues[0][i]:F4}");
//            }
//        }
//    }
//}

