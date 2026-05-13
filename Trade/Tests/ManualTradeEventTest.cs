using System;
using System.Collections.Generic;
using Trade.Prices2;

namespace Trade.Tests
{
    /// <summary>
    /// Simple manual test class to verify trade event handling functionality
    /// </summary>
    public static class ManualTradeEventTest
    {
        public static void RunEventTest()
        {
            Console.WriteLine("=== Manual Trade Event Handling Test ===");
            
            // Create test individual
            var individual = new GeneticIndividual
            {
                StartingBalance = 10000,
                TradePercentageForStocks = 0.1,
                AllowedSecurityTypes = AllowedSecurityType.Stock,
                AllowedTradeTypes = AllowedTradeType.Any,
                AllowMultipleTrades = false,
                EnableOptionITMTakeProfit = true,
                OptionITMTakeProfitPct = 0.25
            };

            // Add simple SMA indicator
            individual.Indicators.Add(new IndicatorParams
            {
                Type = 0, // SMA
                Period = 5,
                Polarity = 1,
                TimeFrame = TimeFrame.M1,
                OHLC = OHLC.Close
            });

            // Track events
            var openedEvents = new List<TradeOpenedEventArgs>();
            var closedEvents = new List<TradeClosedEventArgs>();

            // Subscribe to events
            individual.TradeOpened += (sender, e) => {
                openedEvents.Add(e);
                Console.WriteLine($"TRADE OPENED: {e.TradeType} {e.Position:F4} {e.SecurityType} @ ${e.Price:F2} [{e.ActionTag}]");
            };

            individual.TradeClosed += (sender, e) => {
                closedEvents.Add(e);
                Console.WriteLine($"TRADE CLOSED: {e.ClosePrice:F2} | Proceeds: ${e.Proceeds:F2} | Balance: ${e.Balance:F2} | Early TP: {e.IsEarlyTakeProfit} [{e.ActionTag}]");
            };

            // Create simple test data
            var testData = CreateSimpleTestData();
            
            try
            {
                // Process the data
                individual.Process(testData);
                
                // Report results
                Console.WriteLine($"\n=== Results ===");
                Console.WriteLine($"Trades opened: {openedEvents.Count}");
                Console.WriteLine($"Trades closed: {closedEvents.Count}");
                Console.WriteLine($"Actual trades recorded: {individual.Trades.Count}");
                Console.WriteLine($"Final balance: ${individual.FinalBalance:F2}");
                
                // Verify consistency
                bool isConsistent = openedEvents.Count == closedEvents.Count && 
                                   closedEvents.Count == individual.Trades.Count;
                Console.WriteLine($"Event consistency: {(isConsistent ? "PASS" : "FAIL")}");
                
                if (!isConsistent)
                {
                    Console.WriteLine($"  Expected: {openedEvents.Count} opened = {closedEvents.Count} closed = {individual.Trades.Count} trades");
                }
                
                Console.WriteLine("=== Test Completed ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed with exception: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        private static PriceRecord[] CreateSimpleTestData()
        {
            var startDate = DateTime.Today.AddDays(-10);
            var records = new List<PriceRecord>();
            
            // Create price data with clear trend changes
            var prices = new double[] 
            { 
                100.0, 101.0, 102.0, 103.0, 104.0, // Up trend - should trigger buy
                105.0, 104.0, 103.0, 102.0, 101.0  // Down trend - should trigger sell
            };
            
            for (int i = 0; i < prices.Length; i++)
            {
                var price = prices[i];
                records.Add(new PriceRecord(
                    startDate.AddDays(i), TimeFrame.D1,
                    price - 0.1, // open
                    price + 0.1, // high
                    price - 0.1, // low
                    price,        // close
                    volume: 1000          // volume
                ));
            }
            
            return records.ToArray();
        }
    }
}