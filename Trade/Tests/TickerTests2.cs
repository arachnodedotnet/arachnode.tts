using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Trade.Tests
{
    [TestClass]
    public class TickerTests2
    {
        [TestMethod]
        [TestCategory("Core")]
        public void ParseToOption_TickerOnly_SetsIsOptionFalseAndUppercases()
        {
            var t = Trade.Ticker.ParseToOption("spy");
            Assert.IsFalse(t.IsOption);
            Assert.AreEqual("SPY", t.UnderlyingSymbol);
            Assert.IsNull(t.ExpirationDate);
            Assert.IsNull(t.OptionType);
            Assert.IsNull(t.StrikePrice);
            Assert.AreEqual("SPY (Ticker)", t.ToString());
            Assert.AreEqual("SPY", t.GetDisplayName());
            Assert.AreEqual("SPY", t.GetStandardSymbol());
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ParseToOption_ValidOption_ParsesAllFields()
        {
            // SPY 2024-08-14 Call 390.00 → "SPY240814C00390000"
            var sym = "SPY240814C00390000";
            var t = Trade.Ticker.ParseToOption(sym);

            Assert.IsTrue(t.IsOption);
            Assert.AreEqual("SPY", t.UnderlyingSymbol);
            Assert.AreEqual(new DateTime(2024, 8, 14), t.ExpirationDate);
            Assert.IsTrue(t.OptionType.HasValue);
            Assert.AreEqual(390.00, t.StrikePrice.Value, 1e-9);

            // Display/formatting
            Assert.AreEqual("SPY 2024-08-14 Call $390.00", t.ToString());
            Assert.AreEqual("SPY Aug 14, 2024 C $390.00", t.GetDisplayName());
            Assert.AreEqual(sym, t.GetStandardSymbol());
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ParseToOption_WithPrefixO_ParsesAndPreservesPrefix()
        {
            var t = Trade.Ticker.ParseToOption("O:SPY240814P00420000");
            Assert.IsTrue(t.IsOption);
            Assert.AreEqual("O:", t.Prefix);               // prefix captured
            Assert.AreEqual("SPY", t.UnderlyingSymbol);
            Assert.AreEqual(new DateTime(2024, 8, 14), t.ExpirationDate);
            Assert.IsTrue(t.OptionType.HasValue);
            Assert.AreEqual(420.00, t.StrikePrice.Value, 1e-9);
            Assert.AreEqual("SPY240814P00420000", t.GetStandardSymbol());
        }

        [TestMethod]
        [TestCategory("Core")]
        public void IsValidOptionSymbol_RecognizesValidAndRejectsInvalid()
        {
            Assert.IsTrue(Trade.Ticker.IsValidOptionSymbol("SPY240814C00390000"));
            Assert.IsTrue(Trade.Ticker.IsValidOptionSymbol("O:SPY240814p00390000")); // lower-case type ok

            // Too short
            Assert.IsFalse(Trade.Ticker.IsValidOptionSymbol("SPY240814C0001"));
            // Bad date
            Assert.IsFalse(Trade.Ticker.IsValidOptionSymbol("SPY991332C00390000"));
            // Bad strike (non-numeric)
            Assert.IsFalse(Trade.Ticker.IsValidOptionSymbol("SPY240814C00ABCDEF"));
            // Bad type
            Assert.IsFalse(Trade.Ticker.IsValidOptionSymbol("SPY240814X00390000"));
            // Empty/null
            Assert.IsFalse(Trade.Ticker.IsValidOptionSymbol(""));
            Assert.IsFalse(Trade.Ticker.IsValidOptionSymbol(null));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ParseToOption_InvalidOptionFallsBackToTicker()
        {
            var t = Trade.Ticker.ParseToOption("SPY240814X00390000"); // invalid type 'X'
            Assert.IsFalse(t.IsOption);
            Assert.AreEqual("SPY240814X00390000".ToUpper(), t.UnderlyingSymbol);
            Assert.AreEqual(t.UnderlyingSymbol, t.GetStandardSymbol());
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ParseMultiple_HandlesNullArrayAndInvalids()
        {
            // Null → empty
            var none = Trade.Ticker.ParseMultiple(null);
            Assert.AreEqual(0, none.Length);

            // Mix of valid, invalid (throws), and plain ticker
            var inputs = new[] { "SPY240814C00390000", "", "AAPL" };
            var arr = Trade.Ticker.ParseMultiple(inputs);

            Assert.AreEqual(3, arr.Length);
            Assert.IsNotNull(arr[0]);
            Assert.IsNull(arr[1]);            // empty → ArgumentException caught → null slot
            Assert.IsNotNull(arr[2]);

            Assert.IsTrue(arr[0].IsOption);
            Assert.IsFalse(arr[2].IsOption);
            Assert.AreEqual("AAPL", arr[2].UnderlyingSymbol);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Equality_Tickers_CompareByUnderlyingOnly()
        {
            var a = Trade.Ticker.ParseToOption("msft");
            var b = Trade.Ticker.ParseToOption("MSFT");
            var c = Trade.Ticker.ParseToOption("AAPL");

            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsFalse(a.Equals(c));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Equality_Options_CompareAllKeyFieldsWithStrikeTolerance()
        {
            var x = Trade.Ticker.ParseToOption("SPY240814C00390000"); // 390.00
            // Create an "equivalent" option with tiny strike difference within 0.01 tolerance
            var y = Trade.Ticker.ParseToOption("SPY240814C00390001"); // 390.001
            var z = Trade.Ticker.ParseToOption("SPY240814P00390000"); // Put ≠ Call
            var u = Trade.Ticker.ParseToOption("SPY240815C00390000"); // different date
            var v = Trade.Ticker.ParseToOption("QQQ240814C00390000"); // different underlying

            Assert.IsTrue(x.Equals(y), "Strike difference < 0.01 should be considered equal.");
            Assert.AreEqual(x.GetHashCode(), y.GetHashCode(), "Equal options should share hash code.");
            Assert.IsFalse(x.Equals(z), "Different option type should not be equal.");
            Assert.IsFalse(x.Equals(u), "Different expiration should not be equal.");
            Assert.IsFalse(x.Equals(v), "Different underlying should not be equal.");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ToString_FormatsTickerAndOptionAsExpected()
        {
            var tk = Trade.Ticker.ParseToOption("TSLA");
            Assert.AreEqual("TSLA (Ticker)", tk.ToString());

            var op = Trade.Ticker.ParseToOption("TSLA240101p00100000");
            // Lower-case P should parse as Put, date 2024-01-01, strike 100.000
            Assert.AreEqual("TSLA 2024-01-01 Put $100.00", op.ToString());
            Assert.AreEqual("TSLA Jan 01, 2024 P $100.00", op.GetDisplayName());
        }
    }
}
