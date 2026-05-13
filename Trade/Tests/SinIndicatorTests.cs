using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Indicators;

namespace Trade.Tests
{
    [TestClass]
    public class SinIndicatorTests
    {
        // Helper to compute the expected value using the same formula
        private static double Expected(int index, int length, double p1, double p2, double p3, double p4, double p5)
        {
            var x = (double)index / length * p1 * Math.PI * p2;
            return p3 + p4 * Math.Sin(x + p5);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_ZeroPhase_AtIndex0_EqualsOffset()
        {
            // f(0) = p3 + p4 * sin(0 + p5) ; with p5 = 0 => f(0) = p3
            int index = 0, length = 100;
            double p1 = 1, p2 = 1, p3 = 1.25, p4 = 2.5, p5 = 0;

            var value = SinIndicator.Calculate(index, length, p1, p2, p3, p4, p5);

            Assert.AreEqual(p3, value, 1e-12, "At index 0 and zero phase, output must equal offset p3.");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_AmplitudeAndOffset_AreAppliedCorrectly()
        {
            int index = 37, length = 256;
            double p1 = 0.75, p2 = 1.2, p3 = -0.5, p4 = 3.0, p5 = 0.33;

            var actual = SinIndicator.Calculate(index, length, p1, p2, p3, p4, p5);
            var expected = Expected(index, length, p1, p2, p3, p4, p5);

            Assert.AreEqual(expected, actual, 1e-9, "Output should match the analytical formula exactly (within float tolerance).");
            // Range check: must be within [p3 - |p4|, p3 + |p4|]
            var min = p3 - Math.Abs(p4);
            var max = p3 + Math.Abs(p4);
            Assert.IsTrue(actual >= min - 1e-9 && actual <= max + 1e-9, "Value must lie within offset ± amplitude.");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_PhaseShift_ShiftsWaveProperly()
        {
            int index = 50, length = 200;
            double p1 = 1.0, p2 = 1.0, p3 = 0.0, p4 = 2.0, p5 = Math.PI / 4.0; // +45 degrees

            var withPhase = SinIndicator.Calculate(index, length, p1, p2, p3, p4, p5);
            var expected = Expected(index, length, p1, p2, p3, p4, p5);

            Assert.AreEqual(expected, withPhase, 1e-9, "Phase shift should affect the angle argument additively.");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_Periodicity_RepeatsAfterExpectedIndexDelta()
        {
            // The argument is x = (i/length)*p1*pi*p2 + p5
            // Periodicity requires Δx = 2π => (Δi/length)*p1*pi*p2 = 2π
            // => Δi = 2*length/(p1*p2)
            int length = 512;
            double p1 = 2.0, p2 = 1.0, p3 = 0.25, p4 = 1.75, p5 = 0.0;

            var delta = 2.0 * length / (p1 * p2);
            int periodI = (int)Math.Round(delta); // nearest integer index period

            int i0 = 123;
            var v0 = SinIndicator.Calculate(i0, length, p1, p2, p3, p4, p5);
            var v1 = SinIndicator.Calculate(i0 + periodI, length, p1, p2, p3, p4, p5);

            Assert.AreEqual(v0, v1, 1e-6, "Values separated by the computed integer period should be (approximately) equal.");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_NegativeAmplitude_InvertsAroundOffset()
        {
            int index = 77, length = 300;
            double p1 = 1.1, p2 = 0.9, p3 = 0.0, p4 = 2.0, p5 = 0.2;

            var pos = SinIndicator.Calculate(index, length, p1, p2, p3, p4, p5);
            var neg = SinIndicator.Calculate(index, length, p1, p2, p3, -p4, p5);

            // With same offset p3, changing sign of amplitude should flip sign of (value - p3)
            Assert.AreEqual(-(pos - p3), (neg - p3), 1e-9, "Negative amplitude should invert the wave around the offset.");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Calculate_IndexAndLengthBoundaries_WorkWithoutException()
        {
            // Index at boundaries: 0 and length
            int length = 1000;
            double p1 = 0.5, p2 = 2.0, p3 = 10.0, p4 = 0.1, p5 = 0.0;

            var atStart = SinIndicator.Calculate(0, length, p1, p2, p3, p4, p5);
            var atEnd = SinIndicator.Calculate(length, length, p1, p2, p3, p4, p5);

            // Just basic sanity checks: outputs are finite and within offset ± amplitude
            Assert.IsFalse(double.IsNaN(atStart) || double.IsInfinity(atStart));
            Assert.IsFalse(double.IsNaN(atEnd) || double.IsInfinity(atEnd));
            Assert.IsTrue(atStart >= p3 - Math.Abs(p4) - 1e-9 && atStart <= p3 + Math.Abs(p4) + 1e-9);
            Assert.IsTrue(atEnd >= p3 - Math.Abs(p4) - 1e-9 && atEnd <= p3 + Math.Abs(p4) + 1e-9);
        }
    }
}
