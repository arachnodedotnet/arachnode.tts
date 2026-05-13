using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class ChaikinOscillatorTests
    {
        [TestMethod]
        [TestCategory("Core")]
        public void ChaikinOscillator_HandlesEmptyArrays()
        {
            var result = ChaikinOscillator.Calculate(new double[0], new double[0], new double[0], new long[0], 3, 5,
                ChaikinOscillatorMaMethod.EMA);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ChaikinOscillator_HandlesShortArrays()
        {
            double[] high = { 10, 11 };
            double[] low = { 5, 6 };
            double[] close = { 7, 8 };
            long[] volume = { 100, 110 };
            var result = ChaikinOscillator.Calculate(high, low, close, volume, 3, 5, ChaikinOscillatorMaMethod.EMA);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ChaikinOscillator_ZeroVolumeProducesZeroAD()
        {
            double[] high = { 10, 11, 12, 13, 14 };
            double[] low = { 5, 6, 7, 8, 9 };
            double[] close = { 7, 8, 9, 10, 11 };
            long[] volume = { 0, 0, 0, 0, 0 };
            var result = ChaikinOscillator.Calculate(high, low, close, volume, 2, 3, ChaikinOscillatorMaMethod.EMA);
            // All AD values should be zero, so oscillator should be zero
            Assert.IsTrue(result.All(x => Math.Abs(x) < 1e-8));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ChaikinOscillator_MA_Methods_ProduceDifferentResults()
        {
            double[] high = { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };
            double[] low = { 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 };
            double[] close = { 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            long[] volume = { 100, 110, 120, 130, 140, 150, 160, 170, 180, 190 };
            var fastMA = 3;
            var slowMA = 5;
            var ema = ChaikinOscillator.Calculate(high, low, close, volume, fastMA, slowMA,
                ChaikinOscillatorMaMethod.EMA);
            var smma = ChaikinOscillator.Calculate(high, low, close, volume, fastMA, slowMA,
                ChaikinOscillatorMaMethod.SMMA);
            var lwma = ChaikinOscillator.Calculate(high, low, close, volume, fastMA, slowMA,
                ChaikinOscillatorMaMethod.LWMA);
            var sma = ChaikinOscillator.Calculate(high, low, close, volume, fastMA, slowMA,
                ChaikinOscillatorMaMethod.SMA);
            // At least one value should differ between methods
            var anyDiff = false;
            for (var i = slowMA; i < ema.Length; i++)
                if (Math.Abs(ema[i] - smma[i]) > 1e-6 || Math.Abs(ema[i] - lwma[i]) > 1e-6 ||
                    Math.Abs(ema[i] - sma[i]) > 1e-6)
                {
                    anyDiff = true;
                    break;
                }

            Assert.IsTrue(anyDiff);
        }
    }
}