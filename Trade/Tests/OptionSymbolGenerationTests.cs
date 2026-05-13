using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Trade.Tests
{
    [TestClass]
    public class OptionSymbolGenerationTests
    {
        private static string GenerateOptionSymbol(string underlyingSymbol, DateTime expirationDate,
            AllowedOptionType allowedOptionType, double strikePrice)
        {
            var dateString = expirationDate.ToString("yyMMdd");
            var optionTypeString = allowedOptionType == AllowedOptionType.Calls ? "C" : "P";
            var strikeString = ((int)(strikePrice * 1000)).ToString("D8");
            return $"O:{underlyingSymbol}{dateString}{optionTypeString}{strikeString}";
        }

        [TestMethod][TestCategory("Core")]
        public void GenerateOptionSymbol_Equals_TickerParseToOption_Call()
        {
            var underlying = "SPY";
            var expiration = new DateTime(2025, 8, 14);
            var type = AllowedOptionType.Calls;
            double strike = 390;

            var symbol = GenerateOptionSymbol(underlying, expiration, type, strike);
            var ticker = Ticker.ParseToOption(symbol);

            Assert.AreEqual(underlying, ticker.UnderlyingSymbol);
            Assert.AreEqual(expiration.Date, ticker.ExpirationDate.Value.Date);
            Assert.AreEqual(strike, ticker.StrikePrice.Value);
            Assert.IsTrue(ticker.IsOption);

            // Compare without "O:" prefix
            Assert.AreEqual(symbol.Replace("O:", ""), ticker.GetStandardSymbol());
        }

        [TestMethod][TestCategory("Core")]
        public void GenerateOptionSymbol_Equals_TickerParseToOption_Put()
        {
            var underlying = "AAPL";
            var expiration = new DateTime(2025, 12, 19);
            var type = AllowedOptionType.Puts;
            var strike = 175.5;

            var symbol = GenerateOptionSymbol(underlying, expiration, type, strike);
            var ticker = Ticker.ParseToOption(symbol);

            Assert.AreEqual(underlying, ticker.UnderlyingSymbol);
            Assert.AreEqual(expiration.Date, ticker.ExpirationDate.Value.Date);
            Assert.AreEqual(strike, ticker.StrikePrice.Value);
            Assert.IsTrue(ticker.IsOption);

            // Compare without "O:" prefix
            Assert.AreEqual(symbol.Replace("O:", ""), ticker.GetStandardSymbol());
        }

        [TestMethod][TestCategory("Core")]
        public void GenerateOptionSymbol_Equals_TickerParseToOption_DifferentStrikeFormats()
        {
            var underlying = "TSLA";
            var expiration = new DateTime(2026, 1, 17);
            var type = AllowedOptionType.Calls;
            double strike = 1000;

            var symbol = GenerateOptionSymbol(underlying, expiration, type, strike);
            var ticker = Ticker.ParseToOption(symbol);

            Assert.AreEqual(underlying, ticker.UnderlyingSymbol);
            Assert.AreEqual(expiration.Date, ticker.ExpirationDate.Value.Date);
            Assert.AreEqual(strike, ticker.StrikePrice.Value);
            Assert.IsTrue(ticker.IsOption);

            // Compare without "O:" prefix
            Assert.AreEqual(symbol.Replace("O:", ""), ticker.GetStandardSymbol());
        }
    }
}