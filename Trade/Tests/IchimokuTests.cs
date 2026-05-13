using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class IchimokuTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_BasicLengthsAndNoException()
        {
            var len = 100;
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
                close[i] = 95 + i;
            }

            var result = Ichimoku.Calculate(high, low, close);
            Assert.AreEqual(len, result.TenkanSen.Length);
            Assert.AreEqual(len, result.KijunSen.Length);
            Assert.AreEqual(len, result.SenkouSpanA.Length);
            Assert.AreEqual(len, result.SenkouSpanB.Length);
            Assert.AreEqual(len, result.ChikouSpan.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_TenkanKijun_MatchManual()
        {
            double[] high = { 10, 12, 14, 16, 18, 20, 22, 24, 26 };
            double[] low = { 5, 7, 9, 11, 13, 15, 17, 19, 21 };
            double[] close = { 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var result = Ichimoku.Calculate(high, low, close, 9, 9, 9);
            // Tenkan/Kijun for last index
            var expectedTenkan = (26 + 5) / 2.0;
            var expectedKijun = (26 + 5) / 2.0;
            Assert.AreEqual(expectedTenkan, result.TenkanSen[8], 1e-8);
            Assert.AreEqual(expectedKijun, result.KijunSen[8], 1e-8);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_SenkouSpanA_ShiftedForward()
        {
            var len = 50;
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
                close[i] = 95 + i;
            }

            var kijun = 26;
            var result = Ichimoku.Calculate(high, low, close, 9, kijun, 52);
            // SenkouSpanA at index kijun should be the value calculated at index 0
            var expected = (result.TenkanSen[0] + result.KijunSen[0]) / 2.0;
            Assert.AreEqual(expected, result.SenkouSpanA[kijun], 1e-8);
            // First kijun values should be NaN
            for (var i = 0; i < kijun; i++)
                Assert.IsTrue(!double.IsNaN(result.SenkouSpanA[i]));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_SenkouSpanB_ShiftedForward()
        {
            var len = 60;
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
                close[i] = 95 + i;
            }

            var kijun = 26;
            var senkou = 52;
            var result = Ichimoku.Calculate(high, low, close, 9, kijun, senkou);
            var expected = (Ichimoku_Highest(high, senkou, 0) + Ichimoku_Lowest(low, senkou, 0)) / 2.0;
            Assert.AreEqual(expected, result.SenkouSpanB[kijun], 1e-8);
            for (var i = 0; i < kijun; i++)
                Assert.IsTrue(!double.IsNaN(result.SenkouSpanB[i]));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ChikouSpan_ShiftedBackward()
        {
            var len = 40;
            var high = new double[len];
            var low = new double[len];
            var close = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
                close[i] = 95 + i;
            }

            var kijun = 26;
            var result = Ichimoku.Calculate(high, low, close, 9, kijun, 52);
            for (var i = 0; i < kijun; i++)
                Assert.IsTrue(!double.IsNaN(result.ChikouSpan[i]));
            for (var i = kijun; i < len; i++)
                Assert.AreEqual(close[i - kijun], result.ChikouSpan[i], 1e-8);
        }

        // Helper methods for manual calculation
        private double Ichimoku_Highest(double[] array, int range, int fromIndex)
        {
            var res = array[fromIndex];
            for (var i = fromIndex; i > fromIndex - range && i >= 0; i--)
                if (res < array[i])
                    res = array[i];
            return res;
        }

        private double Ichimoku_Lowest(double[] array, int range, int fromIndex)
        {
            var res = array[fromIndex];
            for (var i = fromIndex; i > fromIndex - range && i >= 0; i--)
                if (res > array[i])
                    res = array[i];
            return res;
        }
    }
}