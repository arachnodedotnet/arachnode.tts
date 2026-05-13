using System;
using System.Diagnostics.CodeAnalysis;

namespace Trade.Examples
{
    /// <summary>
    ///     Example usage of the new Ticker.ParseToOption method
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal class TickerExample
    {
        public static void DemonstrateTickerParsing()
        {
            ConsoleUtilities.WriteLine("=== Ticker.ParseToOption() Examples ===\n");

            // Test cases: various option symbols and tickers
            var testInputs = new[]
            {
                "O:SPY240814C00390000", // Option with O: prefix
                "SPY240814C00390000", // Option without prefix
                "AAPL250117P00150000", // Apple put option
                "SPY", // Simple ticker
                "AAPL", // Another ticker
                "O:TSLA", // Option with O: prefix
                "QQQ240816C00350000", // QQQ call option
                "INVALID", // Invalid/short ticker
                "" // Empty (will throw exception)
            };

            foreach (var input in testInputs)
                try
                {
                    if (string.IsNullOrEmpty(input))
                    {
                        ConsoleUtilities.WriteLine($"Input: '{input}' -> Skipping empty input");
                        continue;
                    }

                    var ticker = Ticker.ParseToOption(input);

                    ConsoleUtilities.WriteLine($"Input: '{input}'");
                    ConsoleUtilities.WriteLine($"  IsOption: {ticker.IsOption}");
                    ConsoleUtilities.WriteLine($"  UnderlyingSymbol: {ticker.UnderlyingSymbol}");

                    if (ticker.IsOption)
                    {
                        ConsoleUtilities.WriteLine($"  ExpirationDate: {ticker.ExpirationDate:yyyy-MM-dd}");
                        ConsoleUtilities.WriteLine($"  OptionType: {ticker.OptionType}");
                        ConsoleUtilities.WriteLine($"  StrikePrice: ${ticker.StrikePrice:F2}");
                        ConsoleUtilities.WriteLine($"  DisplayName: {ticker.GetDisplayName()}");
                        ConsoleUtilities.WriteLine($"  StandardSymbol: {ticker.GetStandardSymbol()}");
                    }
                    else
                    {
                        ConsoleUtilities.WriteLine("  -> Treated as ticker symbol");
                        ConsoleUtilities.WriteLine($"  DisplayName: {ticker.GetDisplayName()}");
                    }

                    if (!string.IsNullOrEmpty(ticker.Prefix)) ConsoleUtilities.WriteLine($"  Prefix: {ticker.Prefix}");

                    ConsoleUtilities.WriteLine($"  ToString(): {ticker}");
                    ConsoleUtilities.WriteLine();
                }
                catch (ArgumentException ex)
                {
                    ConsoleUtilities.WriteLine($"Input: '{input}' -> Error: {ex.Message}\n");
                }

            // Demonstrate IsValidOptionSymbol utility method
            ConsoleUtilities.WriteLine("=== IsValidOptionSymbol() Examples ===\n");

            var validationTests = new[]
            {
                "SPY240814C00390000", // Valid option
                "SPY", // Not an option
                "AAPL250117P00150000", // Valid option
                "INVALID123", // Not an option
                "O:QQQ240816C00350000" // Valid option with prefix
            };

            foreach (var test in validationTests)
            {
                var isValid = Ticker.IsValidOptionSymbol(test);
                ConsoleUtilities.WriteLine($"'{test}' -> IsValidOptionSymbol: {isValid}");
            }

            // Demonstrate ParseMultiple
            ConsoleUtilities.WriteLine("\n=== ParseMultiple() Example ===\n");

            var multipleSymbols = new[] { "SPY", "SPY240814C00390000", "AAPL", "QQQ240816P00300000" };
            var parsedTickers = Ticker.ParseMultiple(multipleSymbols);

            for (var i = 0; i < parsedTickers.Length; i++)
                if (parsedTickers[i] != null)
                    ConsoleUtilities.WriteLine($"{i + 1}. {multipleSymbols[i]} -> {parsedTickers[i]}");
                else
                    ConsoleUtilities.WriteLine($"{i + 1}. {multipleSymbols[i]} -> Failed to parse");
        }
    }
}