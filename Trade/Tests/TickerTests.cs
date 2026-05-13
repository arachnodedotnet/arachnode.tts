using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Trade.Tests
{
    [TestClass]
    public class TickerTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void ParseToOption_ParsesOptionWithPrefix()
        {
            var ticker = Ticker.ParseToOption("O:SPY240814C00390000");
            Assert.IsTrue(ticker.IsOption);
            Assert.AreEqual("SPY", ticker.UnderlyingSymbol);
            Assert.AreEqual(new DateTime(2024, 8, 14), ticker.ExpirationDate);
            Assert.AreEqual(Polygon2.OptionType.Call, ticker.OptionType);
            Assert.AreEqual(390.0, ticker.StrikePrice.Value, 0.001);
            Assert.AreEqual("O:", ticker.Prefix);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ParseToOption_ParsesOptionWithoutPrefix()
        {
            var ticker = Ticker.ParseToOption("AAPL250117P00150000");
            Assert.IsTrue(ticker.IsOption);
            Assert.AreEqual("AAPL", ticker.UnderlyingSymbol);
            Assert.AreEqual(new DateTime(2025, 1, 17), ticker.ExpirationDate);
            Assert.AreEqual(Polygon2.OptionType.Put, ticker.OptionType);
            Assert.AreEqual(150.0, ticker.StrikePrice.Value, 0.001);
            Assert.IsNull(ticker.Prefix);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ParseToOption_ParsesSimpleTicker()
        {
            var ticker = Ticker.ParseToOption("SPY");
            Assert.IsFalse(ticker.IsOption);
            Assert.AreEqual("SPY", ticker.UnderlyingSymbol);
            Assert.IsNull(ticker.ExpirationDate);
            Assert.IsNull(ticker.OptionType);
            Assert.IsNull(ticker.StrikePrice);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ParseToOption_InvalidInput_ThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(() => Ticker.ParseToOption(""));
            Assert.ThrowsException<ArgumentException>(() => Ticker.ParseToOption(null));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void IsValidOptionSymbol_RecognizesValidAndInvalid()
        {
            Assert.IsTrue(Ticker.IsValidOptionSymbol("SPY240814C00390000"));
            Assert.IsFalse(Ticker.IsValidOptionSymbol("SPY"));
            Assert.IsFalse(Ticker.IsValidOptionSymbol("INVALID"));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void GetDisplayName_And_GetStandardSymbol_WorkForOptionAndTicker()
        {
            var ticker = Ticker.ParseToOption("SPY240814C00390000");
            Assert.AreEqual("SPY Aug 14, 2024 C $390.00", ticker.GetDisplayName());
            Assert.AreEqual("SPY240814C00390000", ticker.GetStandardSymbol());

            var ticker2 = Ticker.ParseToOption("SPY");
            Assert.AreEqual("SPY", ticker2.GetDisplayName());
            Assert.AreEqual("SPY", ticker2.GetStandardSymbol());
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ParseMultiple_ParsesAllSymbols()
        {
            var arr = new[] { "SPY", "SPY240814C00390000", "AAPL250117P00150000", "INVALID" };
            var result = Ticker.ParseMultiple(arr);
            Assert.AreEqual(4, result.Length);
            Assert.IsFalse(result[0].IsOption);
            Assert.IsTrue(result[1].IsOption);
            Assert.IsTrue(result[2].IsOption);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Equals_And_GetHashCode_WorkForOptionsAndTickers()
        {
            var t1 = Ticker.ParseToOption("SPY240814C00390000");
            var t2 = Ticker.ParseToOption("SPY240814C00390000");
            var t3 = Ticker.ParseToOption("SPY");
            var t4 = Ticker.ParseToOption("SPY");
            Assert.IsTrue(t1.Equals(t2));
            Assert.IsTrue(t3.Equals(t4));
            Assert.AreEqual(t1.GetHashCode(), t2.GetHashCode());
            Assert.AreEqual(t3.GetHashCode(), t4.GetHashCode());
            Assert.IsFalse(t1.Equals(t3));
        }
    }
}