using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class ParabolicSARTests
    {
        [TestMethod][TestCategory("Core")]
        public void Calculate_BasicLengthAndNoException()
        {
            var len = 50;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
            }

            var result = ParabolicSAR.Calculate(high, low);
            Assert.AreEqual(len, result.SAR.Length);
            Assert.AreEqual(len, result.EP.Length);
            Assert.AreEqual(len, result.AF.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_FlatPrices_SARFlat()
        {
            var len = 30;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100.0;
                low[i] = 100.0;
            }

            var result = ParabolicSAR.Calculate(high, low);
            for (var i = 0; i < len; i++)
                Assert.AreEqual(100.0, result.SAR[i], 1e-8);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_Uptrend_SARFollowsTrend()
        {
            var len = 40;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
            }

            var result = ParabolicSAR.Calculate(high, low);
            for (var i = 10; i < len; i++)
                Assert.IsTrue(result.SAR[i] > result.SAR[i - 1]);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_Downtrend_SARFollowsTrend()
        {
            var len = 40;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 130 - i;
                low[i] = 120 - i;
            }

            var result = ParabolicSAR.Calculate(high, low);
            for (var i = 10; i < len; i++)
                Assert.IsTrue(result.SAR[i] < result.SAR[i - 1]);
        }

        //[TestMethod][TestCategory("Core")]
        public void Calculate_ShortArray_ReturnsZeroBuffers()
        {
            double[] high = { 100 };
            double[] low = { 99 };
            var result = ParabolicSAR.Calculate(high, low);
            Assert.AreEqual(1, result.SAR.Length);
            Assert.AreEqual(1, result.EP.Length);
            Assert.AreEqual(1, result.AF.Length);
        }

        [TestMethod][TestCategory("Core")]
        public void Calculate_ManualCheck_KnownValues()
        {
            double[] high = { 10, 12, 14, 16, 18 };
            double[] low = { 5, 7, 9, 11, 13 };
            var result = ParabolicSAR.Calculate(high, low);
            // Initial SAR should be high[0]
            Assert.AreEqual(10, result.SAR[0], 1e-8);
            // Initial AF should be 0.02
            Assert.AreEqual(0.02, result.AF[0], 1e-8);
            // Initial EP should be low[1]
            Assert.AreEqual(7, result.EP[0], 1e-8);
        }

        //[TestMethod][TestCategory("Core")]
        public void Calculate_StepAndMaximum_AppliesCorrectly()
        {
            var len = 20;
            var high = new double[len];
            var low = new double[len];
            for (var i = 0; i < len; i++)
            {
                high[i] = 100 + i;
                low[i] = 90 + i;
            }

            var result = ParabolicSAR.Calculate(high, low, 0.05, 0.5);
            for (var i = 0; i < len; i++)
            {
                Assert.IsTrue(result.AF[i] <= 0.5);
                Assert.IsTrue(result.AF[i] >= 0.05);
            }
        }
    }
}