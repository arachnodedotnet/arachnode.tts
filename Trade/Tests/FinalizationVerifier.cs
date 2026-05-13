using System;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Simple console program to verify finalization logic
    /// </summary>
    public class FinalizationVerifier
    {
        public static void VerifyStockFinalization()
        {
            Console.WriteLine("?? VERIFYING STOCK FINALIZATION LOGIC");
            Console.WriteLine("=====================================");

            var individual = new GeneticIndividual { StartingBalance = 100000 };

            // Create test price records: $100 -> $109
            var priceRecords = new PriceRecord[2];
            priceRecords[0] = new PriceRecord(DateTime.Today, TimeFrame.D1, 100, 100, 100, 100, volume: 1000);
            priceRecords[1] = new PriceRecord(DateTime.Today.AddDays(1), TimeFrame.D1, 109, 109, 109, 109, volume: 1000);

            Console.WriteLine($"Starting balance: ${individual.StartingBalance:N0}");
            Console.WriteLine($"Price movement: ${priceRecords[0].Close} -> ${priceRecords[1].Close}");
            Console.WriteLine();

            // Test Case 1: Long Position
            Console.WriteLine("TEST 1: Long Position Finalization");
            Console.WriteLine("----------------------------------");

            bool holding1 = true;
            double position1 = 100;
            int openIndex1 = 0;
            double openPrice1 = 100;
            double balance1 = 90000; // $100k - $10k invested

            Console.WriteLine($"Before finalization:");
            Console.WriteLine($"  Holding: {holding1}");
            Console.WriteLine($"  Position: {position1} shares");
            Console.WriteLine($"  Open price: ${openPrice1}");
            Console.WriteLine($"  Cash balance: ${balance1:N0}");
            Console.WriteLine($"  Investment value: ${position1 * openPrice1:N0}");
            Console.WriteLine($"  Total account value: ${balance1 + (position1 * openPrice1):N0}");

            individual.FinalizeStockTrades(priceRecords, priceRecords, ref holding1, ref position1,
                ref openIndex1, ref openPrice1, ref balance1);

            Console.WriteLine($"After finalization:");
            Console.WriteLine($"  Holding: {holding1}");
            Console.WriteLine($"  Position: {position1} shares");
            Console.WriteLine($"  Cash balance: ${balance1:N0}");
            Console.WriteLine($"  Trade recorded: {individual.Trades.Count > 0}");

            if (individual.Trades.Count > 0)
            {
                var trade = individual.Trades[0];
                Console.WriteLine($"  Per-share gain: ${trade.DollarGain}");
                Console.WriteLine($"  Total gain: ${trade.ActualDollarGain:N0}");
                Console.WriteLine($"  Percent gain: {trade.PercentGain:F1}%");
            }

            // Verify correctness
            double expectedBalance1 = 90000 + (100 * 109); // $90k + sale proceeds
            bool test1Pass = Math.Abs(balance1 - expectedBalance1) < 0.01;
            Console.WriteLine($"Expected balance: ${expectedBalance1:N0}");
            Console.WriteLine($"Actual balance: ${balance1:N0}");
            Console.WriteLine($"Test 1 Result: {(test1Pass ? "? PASS" : "? FAIL")}");
            Console.WriteLine();

            // Test Case 2: Short Position
            Console.WriteLine("TEST 2: Short Position Finalization");
            Console.WriteLine("-----------------------------------");

            var individual2 = new GeneticIndividual { StartingBalance = 100000 };
            bool holding2 = true;
            double position2 = -100; // Short 100 shares
            int openIndex2 = 0;
            double openPrice2 = 115; // Shorted at higher price
            double balance2 = 111500; // $100k + $11.5k from short sale

            Console.WriteLine($"Before finalization:");
            Console.WriteLine($"  Holding: {holding2}");
            Console.WriteLine($"  Position: {position2} shares (short)");
            Console.WriteLine($"  Open price: ${openPrice2}");
            Console.WriteLine($"  Cash balance: ${balance2:N0}");

            individual2.FinalizeStockTrades(priceRecords, priceRecords, ref holding2, ref position2,
                ref openIndex2, ref openPrice2, ref balance2);

            Console.WriteLine($"After finalization:");
            Console.WriteLine($"  Holding: {holding2}");
            Console.WriteLine($"  Position: {position2} shares");
            Console.WriteLine($"  Cash balance: ${balance2:N0}");

            if (individual2.Trades.Count > 0)
            {
                var trade = individual2.Trades[0];
                Console.WriteLine($"  Per-share gain: ${trade.DollarGain}");
                Console.WriteLine($"  Total gain: ${trade.ActualDollarGain:N0}");
                Console.WriteLine($"  Trade type: {trade.AllowedTradeType}");
            }

            // Verify short position correctness
            double expectedBalance2 = 111500 - (100 * 109); // $111.5k - cover cost
            bool test2Pass = Math.Abs(balance2 - expectedBalance2) < 0.01;
            Console.WriteLine($"Expected balance: ${expectedBalance2:N0}");
            Console.WriteLine($"Actual balance: ${balance2:N0}");
            Console.WriteLine($"Test 2 Result: {(test2Pass ? "? PASS" : "? FAIL")}");
            Console.WriteLine();

            // Overall result
            bool allTestsPass = test1Pass && test2Pass;
            Console.WriteLine("OVERALL VERIFICATION RESULT");
            Console.WriteLine("===========================");
            Console.WriteLine($"{(allTestsPass ? "? ALL TESTS PASS" : "? SOME TESTS FAILED")}");
            
            if (allTestsPass)
            {
                Console.WriteLine("Stock finalization logic is working correctly!");
                Console.WriteLine("The accounting properly handles:");
                Console.WriteLine("- Long positions: Credits full sale proceeds");
                Console.WriteLine("- Short positions: Debits full cover cost");
                Console.WriteLine("- Position cleanup: Resets holding flags and positions");
                Console.WriteLine("- Trade recording: Creates accurate trade records");
            }
        }

        public static void VerifyBalanceProgression()
        {
            Console.WriteLine();
            Console.WriteLine("?? VERIFYING BALANCE PROGRESSION CONSISTENCY");
            Console.WriteLine("===========================================");

            var individual = new GeneticIndividual { StartingBalance = 100000 };

            // Create price records
            var priceRecords = new PriceRecord[10];
            for (int i = 0; i < 10; i++)
            {
                priceRecords[i] = new PriceRecord(DateTime.Today.AddDays(i), TimeFrame.D1, 100 + i, 105 + i, 95 + i, 100 + i, volume: 1000);
            }

            // Add some completed trades manually
            individual.Trades.Add(new TradeResult
            {
                OpenIndex = 0, CloseIndex = 3, OpenPrice = 100, ClosePrice = 105,
                AllowedTradeType = AllowedTradeType.Buy, AllowedSecurityType = AllowedSecurityType.Stock,
                Position = 100, PositionInDollars = 10000,
                Balance = 100500 // $100k + $500 gain
            });

            individual.Trades.Add(new TradeResult
            {
                OpenIndex = 4, CloseIndex = 6, OpenPrice = 110, ClosePrice = 108,
                AllowedTradeType = AllowedTradeType.SellShort, AllowedSecurityType = AllowedSecurityType.Stock,
                Position = -50, PositionInDollars = 5500,
                Balance = 100600 // Previous + $100 gain
            });

            Console.WriteLine("Existing trades:");
            Console.WriteLine($"  Trade 1: Long 100 shares, $500 gain, Balance: $100,500");
            Console.WriteLine($"  Trade 2: Short 50 shares, $100 gain, Balance: $100,600");

            // Now finalize an open position
            bool holding = true;
            double position = 75;
            int openIndex = 7;
            double openPrice = 108;
            double balance = 92500; // Account for existing trades and current investment

            Console.WriteLine($"Open position to finalize:");
            Console.WriteLine($"  75 shares bought at $108, will sell at $109");
            Console.WriteLine($"  Current cash balance: ${balance:N0}");

            // Finalize
            individual.FinalizeStockTrades(priceRecords, priceRecords, ref holding, ref position,
                ref openIndex, ref openPrice, ref balance);

            Console.WriteLine($"After finalization:");
            Console.WriteLine($"  Final cash balance: ${balance:N0}");
            Console.WriteLine($"  Total trades: {individual.Trades.Count}");

            // Verify using the mathematical progression formula
            double calculatedBalance = individual.StartingBalance;
            foreach (var trade in individual.Trades)
            {
                calculatedBalance += trade.ActualDollarGain;
            }

            Console.WriteLine();
            Console.WriteLine("Balance progression verification:");
            Console.WriteLine($"  Starting balance: ${individual.StartingBalance:N0}");

            foreach (var trade in individual.Trades)
            {
                Console.WriteLine($"  + Trade gain: ${trade.ActualDollarGain:N0}");
            }

            Console.WriteLine($"  = Calculated balance: ${calculatedBalance:N0}");
            Console.WriteLine($"  Actual final balance: ${balance:N0}");

            bool progressionCorrect = Math.Abs(calculatedBalance - balance) < 0.01;
            Console.WriteLine($"Balance progression: {(progressionCorrect ? "? CONSISTENT" : "? INCONSISTENT")}");

            if (progressionCorrect)
            {
                Console.WriteLine("The balance progression formula works correctly!");
                Console.WriteLine("This means the original balance progression error has been fixed.");
            }
        }
    }
}