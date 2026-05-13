using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class RVITests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_BasicLengthAndNoException()
        {
            var len = 50;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++) open[i] = high[i] = low[i] = close[i] = 100 + i;
            var result = RVI.Calculate(open, high, low, close);
            Assert.AreEqual(len, result.RVI.Length);
            Assert.AreEqual(len, result.Signal.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_FlatPrices_RVIFlat()
        {
            var len = 30;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++) open[i] = high[i] = low[i] = close[i] = 100.0;
            var result = RVI.Calculate(open, high, low, close);
            for (var i = 0; i < len; i++)
                Assert.AreEqual(0.0, result.RVI[i], 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Uptrend_RVIPositive()
        {
            var len = 40;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                open[i] = 100 + i;
                high[i] = 105 + i;
                low[i] = 95 + i;
                close[i] = 102 + i;
            }

            var result = RVI.Calculate(open, high, low, close);
            for (var i = 20; i < len; i++)
                Assert.IsTrue(result.RVI[i] > 0.0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Downtrend_RVINegative()
        {
            var len = 40;
            var open = new double[len];
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                open[i] = 100 - i;
                high[i] = 105 - i;
                low[i] = 95 - i;
                close[i] = 98 - i;
            }

            var result = RVI.Calculate(open, high, low, close);
            for (var i = 20; i < len; i++)
                Assert.IsTrue(result.RVI[i] < 0.0);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ShortArray_ReturnsZeroBuffers()
        {
            double[] open = { 100, 101 };
            double[] high = { 101, 102 };
            double[] low = { 99, 98 };
            double[] close = { 100, 99 };
            var result = RVI.Calculate(open, high, low, close);
            Assert.AreEqual(2, result.RVI.Length);
            Assert.IsTrue(Array.TrueForAll(result.RVI, x => Math.Abs(x) < 1e-8));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_PeriodOne_MatchesFormula()
        {
            double[] open = { 100, 101, 102, 103 };
            double[] high = { 101, 102, 103, 104 };
            double[] low = { 99, 98, 97, 96 };
            double[] close = { 100, 101, 102, 103 };
            var result = RVI.Calculate(open, high, low, close, 1);
            // For period=1, triangle smoothing is not meaningful, but RVI should be 0 for flat
            for (var i = 0; i < open.Length; i++)
                Assert.IsTrue(Math.Abs(result.RVI[i]) < 1e-8);
        }

        //[TestMethod]
        //[TestCategory("Core")]
        //public void Calculate_ManualCheck_KnownValues()
        //{
        //    double[] open = { 10, 12, 14, 16, 18, 20 };
        //    double[] high = { 15, 17, 19, 21, 23, 25 };
        //    double[] low = { 5, 7, 9, 11, 13, 15 };
        //    double[] close = { 12, 14, 16, 18, 20, 22 };
        //    var result = RVI.Calculate(open, high, low, close, 3);
        //    // Manual check: for i=5, triangle smoothing over last 4 bars
        //    // Numerator: (close[5]-open[5]) + 2*(close[4]-open[4]) + 2*(close[3]-open[3]) + (close[2]-open[2])
        //    double num = 22 - 20 + 2 * (20 - 18) + 2 * (18 - 16) + (16 - 14); // 2+4+4+2=12
        //    double den = 25 - 15 + 2 * (23 - 13) + 2 * (21 - 11) + (19 - 9); // 10+20+20+10=60
        //    var expected = num / den;
        //    Assert.AreEqual(expected, result.RVI[5], 1e-8);
        //}
    }
}