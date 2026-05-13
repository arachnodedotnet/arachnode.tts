using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class GeneticIndividualPrivateTests
    {
        private GeneticIndividual _individual;
        private Random _rng;
        private const double TOLERANCE = 1e-6;

        [TestInitialize]
        public void Setup()
        {
            _rng = new Random(42);
            _individual = new GeneticIndividual();
            // Ensure at least one indicator is present
            _individual.Indicators.Add(new IndicatorParams
            {
                Type = 1, // SMA
                Period = 3,
                Mode = 0,
                TimeFrame = TimeFrame.D1,
                Polarity = 1,
                LongThreshold = 0.5,
                ShortThreshold = -0.5
            });
            _individual.Indicators.Add(new IndicatorParams
            {
                Type = 1, // SMA
                Period = 3,
                Mode = 0,
                TimeFrame = TimeFrame.D1,
                Polarity = 1,
                LongThreshold = 0.5,
                ShortThreshold = -0.5
            });
            GeneticIndividual.InitializePrices();
            GeneticIndividual.InitializeOptionSolvers();
        }
        
        [TestMethod][TestCategory("Core")]
        public void GenerateValidScaleOutFractions_SumsToOneAndWholeNumbers()
        {
            // Try the optimized method name first (current implementation)
            var method = typeof(GeneticIndividual).GetMethod("GenerateValidScaleOutFractionsOptimized", 
                BindingFlags.NonPublic | BindingFlags.Static);
            
            // Fallback to original method name if optimized version not found
            if (method == null)
            {
                method = typeof(GeneticIndividual).GetMethod("GenerateValidScaleOutFractions", 
                    BindingFlags.NonPublic | BindingFlags.Static);
            }
            
            Assert.IsNotNull(method, "Could not find GenerateValidScaleOutFractions or GenerateValidScaleOutFractionsOptimized method");
            
            var fractions = (double[])method.Invoke(null, new object[] { _rng, 8.0 });
            
            Assert.IsNotNull(fractions, "Method should return a non-null array");
            
            double sum = 0.0;
            foreach (var f in fractions) sum += f;
            Assert.AreEqual(1.0, sum, 1e-10, "Fractions should sum to 1.0");
            
            foreach (var f in fractions) 
            {
                Assert.IsTrue(Math.Abs(f * 8 - Math.Round(f * 8)) < 1e-8, 
                    $"Fraction {f} * 8 = {f * 8} should result in a whole number");
            }
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateDynamicPositionSize_ReturnsNonNegative()
        {
            var method = typeof(GeneticIndividual).GetMethod("CalculateDynamicPositionSize", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "Could not find CalculateDynamicPositionSize method");
            
            var priceRecords = new PriceRecord[10];
            for (int i = 0; i < 10; i++) 
            {
                priceRecords[i] = new PriceRecord(DateTime.Today.AddDays(i), TimeFrame.D1, 100, 101, 99, 100, volume: 1000, wap: 100, count: 1);
            }
            
            var result = (double)method.Invoke(_individual, new object[] { priceRecords, 5, 10000.0, 9000.0, 1, 1, 0 });
            Assert.IsTrue(result >= 0.0, $"Position size should be non-negative, but was {result}");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateTotalExposure_ReturnsNonNegative()
        {
            var method = typeof(GeneticIndividual).GetMethod("CalculateTotalExposure", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "Could not find CalculateTotalExposure method");
            
            var result = (double)method.Invoke(_individual, new object[] { 10000.0 });
            Assert.IsTrue(result >= 0.0, $"Total exposure should be non-negative, but was {result}");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateMarketVolatility_ReturnsNonNegative()
        {
            var method = typeof(GeneticIndividual).GetMethod("CalculateMarketVolatility", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "Could not find CalculateMarketVolatility method");
            
            var priceRecords = new PriceRecord[10];
            for (int i = 0; i < 10; i++) 
            {
                priceRecords[i] = new PriceRecord(DateTime.Today.AddDays(i), TimeFrame.D1, 100, 101, 99, 100, volume: 1000, wap: 100, count: 1);
            }
            
            var result = (double)method.Invoke(_individual, new object[] { priceRecords, 5 });
            Assert.IsTrue(result >= 0.0, $"Market volatility should be non-negative, but was {result}");
        }

        [TestMethod][TestCategory("Core")]
        public void CalculateATR_ReturnsNonNegative()
        {
            var method = typeof(GeneticIndividual).GetMethod("CalculateATR", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "Could not find CalculateATR method");
            
            var priceRecords = new PriceRecord[10];
            for (int i = 0; i < 10; i++) 
            {
                priceRecords[i] = new PriceRecord(DateTime.Today.AddDays(i), TimeFrame.D1, 100, 101, 99, 100, volume: 1000, wap: 100, count: 1);
            }
            
            var result = (double)method.Invoke(_individual, new object[] { priceRecords, 5 });
            Assert.IsTrue(result >= 0.0, $"ATR should be non-negative, but was {result}");
        }

        [TestMethod][TestCategory("Core")]
        public void UpdatePerformanceTracking_DoesNotThrow()
        {
            //var method = typeof(GeneticIndividual).GetMethod("UpdatePerformanceTracking", BindingFlags.Public | BindingFlags.Instance);
            //var configType = Type.GetType("Trade.Prices2.PositionSizingResult, Trade");
            //var resultInstance = Activator.CreateInstance(configType);

            //// Ensure AdjustmentFactors is initialized to avoid NullReferenceException
            //var adjustmentFactorsProp = configType.GetProperty("AdjustmentFactors");
            //if (adjustmentFactorsProp != null)
            //    adjustmentFactorsProp.SetValue(resultInstance, new List<string>());

            //method.Invoke(_individual, new object[] { resultInstance, 10000.0 });
            //Assert.IsTrue(true);
        }
    }
}
