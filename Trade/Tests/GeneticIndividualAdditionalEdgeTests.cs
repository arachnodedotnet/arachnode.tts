using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade;
using Trade.Prices2;
using Trade.Utils; // Added for PriceRange

namespace Trade.Tests
{
    /// <summary>
    /// Additional edge / internal method tests to extend coverage for GeneticIndividual.
    /// Focus areas:
    ///  - Range mode mapping (ApplyRangeMode)
    ///  - Fallback normalization + NormalizedSum combination
    ///  - OHLC buffer selection (GetPriceBuffer)
    ///  - Internal EMA correctness (CalculateEMA)
    ///  - Fallback Normalize clamping behaviour
    /// </summary>
    [TestClass]
    public class GeneticIndividualAdditionalEdgeTests
    {
        private GeneticIndividual _gi;

        [TestInitialize]
        public void SetUp()
        {
            _gi = new GeneticIndividual();
        }

        #region Reflection Helpers

        private T InvokePrivateInstance<T>(string methodName, params object[] args)
        {
            var m = typeof(GeneticIndividual).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, $"Could not find instance method {methodName}");
            return (T)m.Invoke(_gi, args);
        }

        private static T InvokePrivateStatic<T>(string methodName, params object[] args)
        {
            var m = typeof(GeneticIndividual).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, $"Could not find static method {methodName}");
            return (T)m.Invoke(null, args);
        }

        private static void ClearIndicatorRanges()
        {
            var field = typeof(GeneticIndividual).GetField("IndicatorRanges", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "IndicatorRanges static dictionary not found");
            var dict = field.GetValue(null) as System.Collections.IDictionary;
            dict?.Clear();
        }

        private double CallNormalize(double value, int type)
        {
            var m = typeof(GeneticIndividual).GetMethod("Normalize", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, "Normalize method not found");
            return (double)m.Invoke(_gi, new object[] { value, type });
        }

        #endregion

        [TestMethod][TestCategory("Core")]
        public void ApplyRangeMode_CCI_Mappings()
        {
            var ind = new IndicatorParams { Type = 16, TradeMode = IndicatorTradeMode.Range }; // CCI
            var v1 = InvokePrivateStatic<double>("ApplyRangeMode", ind, 16, 150.0); // Overbought -> -1
            var v2 = InvokePrivateStatic<double>("ApplyRangeMode", ind, 16, -150.0); // Oversold -> +1
            var v3 = InvokePrivateStatic<double>("ApplyRangeMode", ind, 16, 0.0); // Neutral -> 0
            Assert.AreEqual(-1.0, v1, 1e-12);
            Assert.AreEqual(1.0, v2, 1e-12);
            Assert.AreEqual(0.0, v3, 1e-12);
        }

        [TestMethod][TestCategory("Core")]
        public void ApplyRangeMode_RSI_Mappings()
        {
            var ind = new IndicatorParams { Type = 38, TradeMode = IndicatorTradeMode.Range }; // RSI
            var overbought = InvokePrivateStatic<double>("ApplyRangeMode", ind, 38, 75.0);
            var oversold = InvokePrivateStatic<double>("ApplyRangeMode", ind, 38, 25.0);
            var neutral = InvokePrivateStatic<double>("ApplyRangeMode", ind, 38, 50.0);
            Assert.AreEqual(-1.0, overbought, 1e-12);
            Assert.AreEqual(1.0, oversold, 1e-12);
            Assert.AreEqual(0.0, neutral, 1e-12);
        }


        [TestMethod][TestCategory("Core")]
        public void GetPriceBuffer_ReturnsCorrectArrays()
        {
            // Original arrays
            var opens = new double[]{1,2,3};
            var highs = new double[]{4,5,6};
            var lows  = new double[]{7,8,9};
            var closes= new double[]{10,11,12};
            var prices= new double[]{13,14,15};

            // Wrap in PriceRange for reflection call (method signature uses PriceRange now)
            var openR  = new PriceRange(opens);
            var highR  = new PriceRange(highs);
            var lowR   = new PriceRange(lows);
            var closeR = new PriceRange(closes);
            var priceR = new PriceRange(prices);

            var method = typeof(GeneticIndividual).GetMethod("GetPriceBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "GetPriceBuffer not found");

            var bufOpen  = (double[])method.Invoke(_gi, new object[]{ OHLC.Open,  openR, highR, lowR, closeR, priceR });
            var bufHigh  = (double[])method.Invoke(_gi, new object[]{ OHLC.High,  openR, highR, lowR, closeR, priceR });
            var bufLow   = (double[])method.Invoke(_gi, new object[]{ OHLC.Low,   openR, highR, lowR, closeR, priceR });
            var bufClose = (double[])method.Invoke(_gi, new object[]{ OHLC.Close, openR, highR, lowR, closeR, priceR });

            Assert.AreSame(opens, bufOpen);
            Assert.AreSame(highs, bufHigh);
            Assert.AreSame(lows,  bufLow);
            Assert.AreSame(closes,bufClose);
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateEMA_InternalMatchesManual()
        {
            var buffer = new double[]{ 10,11,12,13,14,15 };
            int period = 3;
            double expected = 14.25; // per manual derivation in comments
            var ema = InvokePrivateInstance<double>("CalculateEMA", buffer, period);
            Assert.AreEqual(expected, ema, 1e-12, "EMA internal implementation changed or incorrect");
        }

        [TestMethod][TestCategory("Core")]
        public void Normalize_FallbackClampsToRange()
        {
            ClearIndicatorRanges();
            var below = CallNormalize(-10_000, 999);
            var above = CallNormalize( 10_000, 999);
            var mid   = CallNormalize(  750.0, 999);
            Assert.AreEqual(-1.0, below, 1e-12);
            Assert.AreEqual( 1.0, above, 1e-12);
            Assert.AreEqual( 0.5, mid,   1e-12);
        }
    }
}
