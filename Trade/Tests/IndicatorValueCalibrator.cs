using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class IndicatorValueCalibrator
    {
        [TestMethod]
        [TestCategory("Core")]
        public void CalibrateIndicatorExpectedValues()
        {
            // Synthetic OHLC series with deterministic trend and volatility for consistent results
            const int N = 600;
            var close = new double[N];
            var open = new double[N];
            var high = new double[N];
            var low = new double[N];
            var volume = new double[N];

            // Create deterministic price data for consistent test results
            for (int i = 0; i < N; i++)
            {
                var basePx = 100.0 + i * 0.03 + Math.Sin(i * 0.09) * 1.7 + Math.Cos(i * 0.05) * 0.9;
                close[i] = basePx;
                open[i] = basePx + Math.Sin(i * 0.11) * 0.4;
                var maxOC = Math.Max(open[i], close[i]);
                var minOC = Math.Min(open[i], close[i]);
                high[i] = maxOC + 0.6 + Math.Abs(Math.Sin(i * 0.13)) * 0.8;
                low[i] = Math.Max(0.01, minOC - 0.6 - Math.Abs(Math.Cos(i * 0.17)) * 0.8);
                volume[i] = 1000 + Math.Abs(Math.Sin(i * 0.07)) * 500; // Deterministic volume
            }

            var gi = new GeneticIndividual();
            var mi = typeof(GeneticIndividual).GetMethod(
                "CalculateIndicatorValue",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(mi, "Could not reflect GeneticIndividual.CalculateIndicatorValue");

            var calibrationResults = new Dictionary<int, double>();

            for (int type = 1; type <= 50; type++)
            {
                try
                {
                    var ind = new IndicatorParams
                    {
                        Type = type,
                        Period = 14,
                        Mode = 0,
                        TimeFrame = TimeFrame.D1,
                        OHLC = OHLC.Close,
                        Polarity = 1,
                        LongThreshold = 0.5,
                        ShortThreshold = -0.5,
                        Param1 = 0.02,
                        Param2 = 0.2,
                        Param3 = 0.0,
                        Param4 = 0.0,
                        Param5 = 0.0
                    };

                    var valObj = mi.Invoke(gi, new object[]
                    {
                        ind, open, high, low, close, volume, close, close.Length, "calibration"
                    });
                    var value = Convert.ToDouble(valObj);
                    calibrationResults[type] = value;
                }
                catch (Exception ex)
                {
                    calibrationResults[type] = double.NaN;
                }
            }

            // Store results in a way we can inspect them
            Assert.IsTrue(calibrationResults.Count > 0, "Should have calibration results");

            // Write to test output
            TestContext.WriteLine("Calibrated Indicator Values:");
            foreach (var kvp in calibrationResults)
            {
                var value = kvp.Value;
                if (!double.IsNaN(value))
                {
                    var tolerance = Math.Max(Math.Abs(value) * 0.1, 0.1);
                    TestContext.WriteLine($"Type {kvp.Key}: {value:F6} (tolerance: {tolerance:F3})");
                }
                else
                {
                    TestContext.WriteLine($"Type {kvp.Key}: NaN/Exception");
                }
            }
        }

        public TestContext TestContext { get; set; }
    }
}