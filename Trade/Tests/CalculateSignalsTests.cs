using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Prices2;

namespace Trade.Tests
{
    [TestClass]
    public class CalculateSignalsTests
    {
        private List<List<double>> _indicatorValues;
        private GeneticIndividual _individual;
        private List<List<double>> _signals;
        private PriceRecord[] _testPriceRecords;

        [TestInitialize]
        public void Setup()
        {
            _individual = new GeneticIndividual();
            _signals = new List<List<double>>();
            _indicatorValues = new List<List<double>>();

            // Create test price records
            _testPriceRecords = CreateTestPriceRecords(100);

            // Reset static prices
            GeneticIndividual.Prices = null;
        }

        private PriceRecord[] CreateTestPriceRecords(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = new DateTime(2023, 1, 1);
            var random = new Random(42); // Seed for reproducibility

            for (var i = 0; i < count; i++)
            {
                var date = baseDate.AddDays(i);
                var price = 100.0 + Math.Sin(i * 0.1) * 10 + random.NextDouble() * 5; // Trending with noise

                records[i] = new PriceRecord
                {
                    DateTime = date,
                    Open = price - 0.5,
                    High = price + 1.0,
                    Low = price - 1.0,
                    Close = price,
                    Volume = 1000 + random.Next(500),
                    WAP = price,
                    Count = 100,
                    Option = null,
                    IsComplete = true
                };
            }

            return records;
        }

        private void SetupIndicators(params IndicatorParams[] indicators)
        {
            _individual.Indicators.Clear();
            _individual.Indicators.AddRange(indicators);

            // Initialize indicator values lists
            _indicatorValues.Clear();
            for (var i = 0; i < indicators.Length; i++)
            {
                _signals.Add(new List<double>());
                _indicatorValues.Add(new List<double>());
            }
        }

        #region Edge Case Tests

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_WithZeroPeriod_NonDebugCase_ReturnsZero()
        {
            // Arrange
            SetupIndicators(new IndicatorParams
            {
                Type = 1,
                Period = 0,
                TimeFrame = TimeFrame.D1,
                DebugCase = false
            });

            // Act
            _individual.CalculateSignals(_testPriceRecords.Take(5).ToArray(), _signals, _indicatorValues);

            // Assert
            Assert.AreEqual(5, _indicatorValues[0].Count);
            Assert.IsTrue(_indicatorValues[0].All(v => v == 0.0));
        }

        #endregion

        #region Multiple Indicators Tests

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_WithMixedIndicatorTypes_ProducesConsistentResults()
        {
            // Arrange
            SetupIndicators(
                new IndicatorParams { Type = 1, Period = 5, TimeFrame = TimeFrame.D1 }, // SMA
                new IndicatorParams { Type = 3, Period = 10, TimeFrame = TimeFrame.D1 }, // EMA
                new IndicatorParams { Type = 5, Period = 14, TimeFrame = TimeFrame.D1 }, // ATR
                new IndicatorParams { Type = 6, Period = 14, TimeFrame = TimeFrame.D1 } // ADX
            );

            // Act
            _individual.CalculateSignals(_testPriceRecords, _signals, _indicatorValues);

            // Assert
            Assert.AreEqual(4, _indicatorValues.Count);
            foreach (var values in _indicatorValues)
            {
                Assert.AreEqual(_testPriceRecords.Length, values.Count);
                Assert.IsTrue(values.All(v => !double.IsInfinity(v)));
            }
        }

        #endregion

        #region TimeFrame Tests

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_WithDifferentTimeFrames_HandlesCorrectly()
        {
            // Arrange
            SetupIndicators(
                new IndicatorParams { Type = 1, Period = 5, TimeFrame = TimeFrame.M1 },
                new IndicatorParams { Type = 1, Period = 5, TimeFrame = TimeFrame.H1 },
                new IndicatorParams { Type = 1, Period = 5, TimeFrame = TimeFrame.D1 }
            );

            // Act
            _individual.CalculateSignals(_testPriceRecords.Take(20).ToArray(), _signals, _indicatorValues);

            // Assert
            Assert.AreEqual(3, _indicatorValues.Count);
            foreach (var values in _indicatorValues) Assert.AreEqual(20, values.Count);
        }

        #endregion

        #region Helper Methods

        private double CalculateVariance(List<double> values)
        {
            if (values.Count < 2) return 0;

            var mean = values.Average();
            var sumSquaredDiffs = values.Sum(v => Math.Pow(v - mean, 2));
            return sumSquaredDiffs / (values.Count - 1);
        }

        #endregion

        #region Basic Functionality Tests

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_WithEmptyPriceRecords_HandlesGracefully()
        {
            // Arrange
            var emptyRecords = new PriceRecord[0];
            SetupIndicators(new IndicatorParams { Type = 1, Period = 14, TimeFrame = TimeFrame.D1 });

            // Act
            _individual.CalculateSignals(emptyRecords, _signals, _indicatorValues);

            // Assert
            Assert.AreEqual(1, _signals.Count);
            Assert.AreEqual(1, _indicatorValues.Count);
            Assert.AreEqual(0, _indicatorValues[0].Count);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_WithSinglePriceRecord_ProducesOneSignal()
        {
            // Arrange
            var singleRecord = new[] { _testPriceRecords[0] };
            SetupIndicators(new IndicatorParams { Type = 1, Period = 1, TimeFrame = TimeFrame.D1 });

            // Act
            _individual.CalculateSignals(singleRecord, _signals, _indicatorValues);

            // Assert
            Assert.AreEqual(1, _signals.Count);
            Assert.AreEqual(1, _indicatorValues[0].Count);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_WithMultipleIndicators_ProducesCorrectNumberOfValues()
        {
            // Arrange
            SetupIndicators(
                new IndicatorParams { Type = 1, Period = 5, TimeFrame = TimeFrame.D1 }, // SMA
                new IndicatorParams { Type = 2, Period = 10, TimeFrame = TimeFrame.D1 }, // EMA
                new IndicatorParams { Type = 5, Period = 14, TimeFrame = TimeFrame.D1 } // ATR
            );

            // Act
            _individual.CalculateSignals(_testPriceRecords, _signals, _indicatorValues);

            // Assert
            Assert.AreEqual(_testPriceRecords.Length, _signals[0].Count);
            Assert.AreEqual(3, _indicatorValues.Count);
            foreach (var values in _indicatorValues) Assert.AreEqual(_testPriceRecords.Length, values.Count);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_WithNoIndicators_ProducesEmptyIndicatorValues()
        {
            // Arrange
            SetupIndicators(); // No indicators
            // Act
            _individual.CalculateSignals(_testPriceRecords, _signals, _indicatorValues);

            Assert.AreEqual(0, _indicatorValues.Count);
        }

        #endregion

        #region Prices System Mode Tests

        #endregion

        #region Specific Indicator Type Tests

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_SinIndicator_Type0_ProducesExpectedValues()
        {
            // Arrange
            SetupIndicators(new IndicatorParams
            {
                Type = 0, // Sin indicator
                Period = 1,
                TimeFrame = TimeFrame.D1,
                Param1 = 1.0,
                Param2 = 0.1,
                Param3 = 0.0,
                Param4 = 1.0,
                Param5 = 0.0
            });

            // Act
            _individual.CalculateSignals(_testPriceRecords.Take(10).ToArray(), _signals, _indicatorValues);

            // Assert
            Assert.AreEqual(10, _indicatorValues[0].Count);
            // Sin values should vary
            Assert.IsTrue(_indicatorValues[0].Any(v => v != _indicatorValues[0][0]));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_ADXIndicator_Type6_ProducesDirectionalValues()
        {
            // Arrange
            SetupIndicators(new IndicatorParams { Type = 6, Period = 14, TimeFrame = TimeFrame.D1 });

            // Act
            _individual.CalculateSignals(_testPriceRecords.Take(30).ToArray(), _signals, _indicatorValues);

            // Assert
            Assert.AreEqual(30, _indicatorValues[0].Count);
            // ADX can return 0 for insufficient data, so we just check for non-negative
            Assert.IsTrue(_indicatorValues[0].All(v => v >= 0));
        }

        #endregion

        #region Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        [Timeout(10000)] // 10 second timeout
        public void CalculateSignals_WithLargeDataset_CompletesInReasonableTime()
        {
            // Arrange
            var largeDataset = CreateTestPriceRecords(1000); // Reduced from 5000 for faster tests
            SetupIndicators(
                new IndicatorParams { Type = 1, Period = 20, TimeFrame = TimeFrame.D1 },
                new IndicatorParams { Type = 2, Period = 20, TimeFrame = TimeFrame.D1 },
                new IndicatorParams { Type = 5, Period = 14, TimeFrame = TimeFrame.D1 }
            );

            // Act
            var startTime = DateTime.Now;
            _individual.CalculateSignals(largeDataset, _signals, _indicatorValues);
            var duration = DateTime.Now - startTime;

            // Assert
            Assert.IsTrue(duration.TotalSeconds < 10, $"Processing took {duration.TotalSeconds} seconds");
            Assert.AreEqual(1000, _signals[0].Count);
            Assert.AreEqual(3, _indicatorValues.Count);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_WithManyIndicators_HandlesCorrectly()
        {
            // Arrange
            var manyIndicators = new List<IndicatorParams>();
            for (var i = 0; i < 10; i++)
                manyIndicators.Add(new IndicatorParams
                {
                    Type = i % 5 + 1, // Cycle through indicator types 1-5
                    Period = 5 + i,
                    TimeFrame = TimeFrame.D1
                });

            SetupIndicators(manyIndicators.ToArray());

            // Act
            _individual.CalculateSignals(_testPriceRecords, _signals, _indicatorValues);

            // Assert
            Assert.AreEqual(10, _indicatorValues.Count);
            Assert.AreEqual(_testPriceRecords.Length, _signals[0].Count);
        }

        #endregion

        #region Data Integrity Tests

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_MaintainsDataIntegrity_AcrossMultipleCalls()
        {
            // Arrange
            SetupIndicators(new IndicatorParams { Type = 1, Period = 5, TimeFrame = TimeFrame.D1 });
            var testData = _testPriceRecords.Take(20).ToArray();

            // Clear and reinitialize for first call
            _signals.Clear();
            _signals.Add(new List<double>());
            _indicatorValues.Clear();
            _indicatorValues.Add(new List<double>());

            // Act - First call
            _individual.CalculateSignals(testData, _signals, _indicatorValues);
            var firstResults = _indicatorValues[0].ToArray();

            // Clear and reinitialize for second call
            _signals.Clear();
            _signals.Add(new List<double>());
            _indicatorValues.Clear();
            _indicatorValues.Add(new List<double>());

            // Act - Second call
            _individual.CalculateSignals(testData, _signals, _indicatorValues);
            var secondResults = _indicatorValues[0].ToArray();

            // Assert
            Assert.AreEqual(firstResults.Length, secondResults.Length);
            for (var i = 0; i < firstResults.Length; i++)
                Assert.AreEqual(firstResults[i], secondResults[i], 0.0001, $"Mismatch at index {i}");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_DoesNotModifyInputData()
        {
            // Arrange
            var originalData = _testPriceRecords.Take(10).Select(p => new PriceRecord
            {
                DateTime = p.DateTime,
                Open = p.Open,
                High = p.High,
                Low = p.Low,
                Close = p.Close,
                Volume = p.Volume,
                WAP = p.WAP,
                Count = p.Count,
                Option = p.Option,
                IsComplete = p.IsComplete
            }).ToArray();

            SetupIndicators(new IndicatorParams { Type = 1, Period = 3, TimeFrame = TimeFrame.D1 });

            // Act
            _individual.CalculateSignals(_testPriceRecords.Take(10).ToArray(), _signals, _indicatorValues);

            // Assert
            for (var i = 0; i < 10; i++)
            {
                Assert.AreEqual(originalData[i].Close, _testPriceRecords[i].Close);
                Assert.AreEqual(originalData[i].High, _testPriceRecords[i].High);
                Assert.AreEqual(originalData[i].Low, _testPriceRecords[i].Low);
                Assert.AreEqual(originalData[i].Volume, _testPriceRecords[i].Volume);
            }
        }

        #endregion

        #region Stress Tests

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_WithExtremeValues_HandlesGracefully()
        {
            // Arrange
            var extremeRecords = new PriceRecord[10];
            var baseDate = DateTime.Now;
            for (var i = 0; i < 10; i++)
                extremeRecords[i] = new PriceRecord
                {
                    DateTime = baseDate.AddDays(i),
                    Open = i % 2 == 0 ? 0.01 : 10000.0, // Extreme price swings
                    High = i % 2 == 0 ? 0.02 : 10001.0,
                    Low = i % 2 == 0 ? 0.005 : 9999.0,
                    Close = i % 2 == 0 ? 0.015 : 10000.0,
                    Volume = 1000,
                    WAP = i % 2 == 0 ? 0.015 : 10000.0,
                    Count = 100,
                    Option = null,
                    IsComplete = true
                };

            SetupIndicators(new IndicatorParams { Type = 1, Period = 5, TimeFrame = TimeFrame.D1 });

            // Act
            _individual.CalculateSignals(extremeRecords, _signals, _indicatorValues);

            // Assert
            Assert.AreEqual(10, _indicatorValues[0].Count);
            Assert.IsTrue(_indicatorValues[0].All(v => !double.IsNaN(v) && !double.IsInfinity(v)));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_WithNegativePrices_HandlesCorrectly()
        {
            // Arrange (though negative prices are unusual in trading)
            var records = CreateTestPriceRecords(10);
            for (var i = 0; i < records.Length; i++)
                records[i] = new PriceRecord
                {
                    DateTime = records[i].DateTime,
                    Open = -Math.Abs(records[i].Open),
                    High = -Math.Abs(records[i].High) + 2, // Ensure high > open
                    Low = -Math.Abs(records[i].Low) - 2, // Ensure low < open
                    Close = -Math.Abs(records[i].Close),
                    Volume = records[i].Volume,
                    WAP = records[i].WAP,
                    Count = records[i].Count,
                    Option = null,
                    IsComplete = true
                };

            SetupIndicators(new IndicatorParams { Type = 1, Period = 3, TimeFrame = TimeFrame.D1 });

            // Act & Assert - Should not throw
            _individual.CalculateSignals(records, _signals, _indicatorValues);
            Assert.AreEqual(10, _indicatorValues[0].Count);
        }

        #endregion
    }

    #region Additional Test Classes for Specific Scenarios

    [TestClass]
    public class CalculateSignalsIndicatorSpecificTests
    {
        private GeneticIndividual _individual;
        private PriceRecord[] _testData;

        [TestInitialize]
        public void Setup()
        {
            _individual = new GeneticIndividual();
            _testData = CreateRealisticTestData(50);
            GeneticIndividual.Prices = null; // Use legacy mode for these tests
        }

        private PriceRecord[] CreateRealisticTestData(int count)
        {
            var records = new PriceRecord[count];
            var baseDate = new DateTime(2023, 1, 1);
            var price = 100.0;
            var random = new Random(123);

            for (var i = 0; i < count; i++)
            {
                // Simulate realistic price movement
                var change = (random.NextDouble() - 0.5) * 4; // ±2 max change
                price = Math.Max(1, price + change);

                var high = price + random.NextDouble() * 2;
                var low = price - random.NextDouble() * 2;
                var open = low + random.NextDouble() * (high - low);

                records[i] = new PriceRecord
                {
                    DateTime = baseDate.AddDays(i),
                    Open = open,
                    High = high,
                    Low = low,
                    Close = price,
                    Volume = 1000 + random.Next(500),
                    WAP = price,
                    Count = 100,
                    Option = null,
                    IsComplete = true
                };
            }

            return records;
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_CCIIndicator_Type16_UsesCorrectIndex()
        {
            // Arrange
            _individual.Indicators.Add(new IndicatorParams
            {
                Type = 16,
                Period = 14,
                TimeFrame = TimeFrame.D1
            });

            var signals = new List<List<double>> { new List<double>() };
            var indicatorValues = new List<List<double>> { new List<double>() };

            // Act
            _individual.CalculateSignals(_testData, signals, indicatorValues);

            // Assert
            Assert.AreEqual(_testData.Length, indicatorValues[0].Count);
            // CCI can be positive or negative
            Assert.IsTrue(indicatorValues[0].Any(v => !double.IsNaN(v)));
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_ASIIndicator_Type10_DoesNotUsePeriod()
        {
            // Arrange
            _individual.Indicators.Add(new IndicatorParams
            {
                Type = 10, // ASI doesn't use period
                Period = 0, // Should not matter
                TimeFrame = TimeFrame.D1
            });

            var signals = new List<List<double>> { new List<double>() };
            var indicatorValues = new List<List<double>> { new List<double>() };

            // Act
            _individual.CalculateSignals(_testData, signals, indicatorValues);

            // Assert
            Assert.AreEqual(_testData.Length, indicatorValues[0].Count);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void CalculateSignals_AwesomeOscillator_Type14_DoesNotUsePeriod()
        {
            // Arrange
            _individual.Indicators.Add(new IndicatorParams
            {
                Type = 14, // Awesome Oscillator doesn't use period
                Period = 0,
                TimeFrame = TimeFrame.D1
            });

            var signals = new List<List<double>> { new List<double>() };
            var indicatorValues = new List<List<double>> { new List<double>() };

            // Act
            _individual.CalculateSignals(_testData, signals, indicatorValues);

            // Assert
            Assert.AreEqual(_testData.Length, indicatorValues[0].Count);
        }
    }

    [TestClass]
    public class CalculateSignalsErrorHandlingTests
    {
        private GeneticIndividual _individual;

        [TestInitialize]
        public void Setup()
        {
            _individual = new GeneticIndividual();
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CalculateSignals_WithNullPriceRecords_ThrowsException()
        {
            // Arrange
            var signals = new List<List<double>> { new List<double>() };
            var indicatorValues = new List<List<double>>();

            // Act & Assert
            _individual.CalculateSignals(null, signals, indicatorValues);
        }

        [TestMethod]
        [TestCategory("Core")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CalculateSignals_WithNullSignalsList_ThrowsException()
        {
            // Arrange
            var records = new PriceRecord[1];
            var indicatorValues = new List<List<double>>();

            // Act & Assert
            _individual.CalculateSignals(records, null, indicatorValues);
        }
    }

    [TestClass]
    public class CalculateSignalsIntegrationTests
    {
        private GeneticIndividual CreateRealisticIndividual()
        {
            var individual = new GeneticIndividual();

            // Add common trading indicators
            individual.Indicators.AddRange(new[]
            {
                new IndicatorParams { Type = 1, Period = 20, TimeFrame = TimeFrame.D1 }, // SMA 20
                new IndicatorParams { Type = 2, Period = 12, TimeFrame = TimeFrame.D1 }, // EMA 12
                new IndicatorParams { Type = 5, Period = 14, TimeFrame = TimeFrame.D1 }, // ATR 14
                new IndicatorParams { Type = 6, Period = 14, TimeFrame = TimeFrame.D1 } // ADX 14
            });

            return individual;
        }

        private PriceRecord[] CreateRealisticMarketData(int days)
        {
            var records = new PriceRecord[days];
            var baseDate = new DateTime(2023, 1, 1);
            var price = 100.0;
            var random = new Random(456);

            for (var i = 0; i < days; i++)
            {
                // Simulate realistic market behavior with trends and volatility
                var trendComponent = Math.Sin(i / 50.0) * 0.5; // Long-term trend
                var randomComponent = (random.NextDouble() - 0.5) * 2; // Daily noise
                var volatilityCluster = Math.Abs(Math.Sin(i / 20.0)) * 2; // Volatility clustering

                var change = trendComponent + randomComponent * volatilityCluster;
                price = Math.Max(1, price + change);

                var highLowRange = 1 + random.NextDouble() * 3;
                var high = price + random.NextDouble() * highLowRange;
                var low = price - random.NextDouble() * highLowRange;
                var open = low + random.NextDouble() * (high - low);

                records[i] = new PriceRecord
                {
                    DateTime = baseDate.AddDays(i),
                    Open = open,
                    High = high,
                    Low = low,
                    Close = price,
                    Volume = (long)(50000 + random.NextDouble() * 100000), // Realistic volume
                    WAP = price + (random.NextDouble() - 0.5) * 0.1, // WAP close to close price
                    Count = 1000 + random.Next(2000),
                    Option = null,
                    IsComplete = true
                };
            }

            return records;
        }

        //[TestMethod]
        [TestCategory("Core")]
        public void PreprocessHistoricalRecords_IsCalledWithCorrectParameters()
        {
            // Arrange
            var individual = new GeneticIndividual();
            individual.Indicators.Add(new IndicatorParams { Type = 1, Period = 3, TimeFrame = TimeFrame.D1 });

            var priceRecords = new[]
            {
                new PriceRecord(new DateTime(2024, 1, 1), TimeFrame.D1, 10, 11, 9, 10, volume: 1000, wap: 10, count: 1),
                new PriceRecord(new DateTime(2024, 1, 2), TimeFrame.D1, 11, 12, 10, 11, volume: 1000, wap: 11, count: 1),
                new PriceRecord(new DateTime(2024, 1, 3), TimeFrame.D1, 12, 13, 11, 12, volume: 1000, wap: 12, count: 1),
                new PriceRecord(new DateTime(2024, 1, 4), TimeFrame.D1, 13, 14, 12, 13, volume: 1000, wap: 13, count: 1)
            };

            var preprocessCalled = false;
            Func<DateTime, DateTime, DateTime, IndicatorParams, PriceRecord[], int, PriceRecord[]> preprocess =
                (start, end, current, ind, records, idx) =>
                {
                    preprocessCalled = true;
                    // Validate that the correct period is passed
                    Assert.AreEqual(ind.Period, records.Length);
                    // Validate that all records are non-null and have valid dates
                    Assert.IsTrue(records.All(r => r != null && r.DateTime >= start && r.DateTime < end));
                    return records;
                };

            var signals = new List<List<double>> { new List<double>() };
            var indicatorValues = new List<List<double>>
                { new List<double>() };

            // Act
            individual.CalculateSignals(priceRecords, signals, indicatorValues, preprocess);

            // Assert
            Assert.IsTrue(preprocessCalled, "Preprocess function was not called.");
        }

        //[TestMethod]
        [TestCategory("Core")]
        public void PreprocessHistoricalRecords_FiltersOutInvalidRecords()
        {
            // Arrange
            var individual = new GeneticIndividual();
            individual.Indicators.Add(new IndicatorParams { Type = 1, Period = 2, TimeFrame = TimeFrame.D1 });

            var priceRecords = new[]
            {
                new PriceRecord(new DateTime(2024, 1, 1), TimeFrame.D1, 10, 11, 9, 10, volume: 1000, wap: 10, count: 1),
                new PriceRecord(new DateTime(2024, 1, 2), TimeFrame.D1, 11, 12, 10, 11, volume: 1000, wap: 11, count: 1),
                new PriceRecord(new DateTime(2024, 1, 3), TimeFrame.D1, 12, 13, 11, 12, volume: 1000, wap: 12, count: 1)
            };

            Func<DateTime, DateTime, DateTime, IndicatorParams, PriceRecord[], int, PriceRecord[]> preprocess =
                (start, end, current, ind, records, idx) =>
                {
                    // Remove records with Close < 11
                    return records.Where(r => r.Close >= 11).ToArray();
                };

            var signals = new List<List<double>> { new List<double>() };
            var indicatorValues = new List<List<double>>
                { new List<double>() };

            // Act
            individual.CalculateSignals(priceRecords, signals, indicatorValues, preprocess);

            // Assert
            // The indicatorValues should be calculated only from records with Close >= 11
            Assert.IsTrue(indicatorValues[0].All(v => v >= 11),
                "Indicator values should be based on filtered records.");
        }

        //[TestMethod]
        [TestCategory("Core")]
        public void PreprocessHistoricalRecords_ThrowsOnNullRecords()
        {
            // Arrange
            var individual = new GeneticIndividual();
            individual.Indicators.Add(new IndicatorParams { Type = 1, Period = 2, TimeFrame = TimeFrame.D1 });

            var priceRecords = new[]
            {
                new PriceRecord(new DateTime(2024, 1, 1), TimeFrame.D1, 10, 11, 9, 10, volume: 1000, wap: 10, count: 1),
                null
            };

            Func<DateTime, DateTime, DateTime, IndicatorParams, PriceRecord[], int, PriceRecord[]> preprocess =
                (start, end, current, ind, records, idx) =>
                {
                    if (records.Any(r => r == null))
                        throw new ArgumentException("Null record found in historicalRecords");
                    return records;
                };

            var signals = new List<List<double>> { new List<double>() };
            var indicatorValues = new List<List<double>>
                { new List<double>() };

            // Act & Assert
            Assert.ThrowsExactly<ArgumentException>(() =>
                individual.CalculateSignals(priceRecords, signals, indicatorValues, preprocess));
        }
    }

    #endregion
}