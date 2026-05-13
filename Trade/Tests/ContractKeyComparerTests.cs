using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Trade.Polygon2;

namespace Trade.Tests
{
    /// <summary>
    /// Comprehensive unit tests for ContractKeyComparer and ContractKey classes.
    /// Tests the complete sort order: Underlying ? Call/Put ? Expiration ? Strike ? RawTicker
    /// </summary>
    [TestClass]
    public class ContractKeyComparerTests
    {
        private ContractKeyComparer _comparer;

        [TestInitialize]
        public void Setup()
        {
            _comparer = ContractKeyComparer.Instance;
        }

        #region Basic Functionality Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Instance_IsNotNull_AndIsSingleton()
        {
            var instance1 = ContractKeyComparer.Instance;
            var instance2 = ContractKeyComparer.Instance;
            
            Assert.IsNotNull(instance1);
            Assert.AreSame(instance1, instance2, "Instance should be a singleton");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_IdenticalReferences_ReturnsZero()
        {
            var key = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            var result = _comparer.Compare(key, key);
            
            Assert.AreEqual(0, result, "Identical references should return 0");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_NullInputs_HandlesCorrectly()
        {
            var key = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            
            Assert.AreEqual(-1, _comparer.Compare(null, key), "null vs non-null should return -1");
            Assert.AreEqual(1, _comparer.Compare(key, null), "non-null vs null should return 1");
            Assert.AreEqual(0, _comparer.Compare(null, null), "null vs null should return 0");
        }

        #endregion

        #region Underlying Symbol Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_DifferentUnderlyings_SortsAlphabetically()
        {
            var aapl = CreateKey("AAPL", true, new DateTime(2025, 3, 21), 150.0, "O:AAPL250321C00150000");
            var spy = CreateKey("SPY", true, new DateTime(2025, 3, 21), 150.0, "O:SPY250321C00150000");
            var tsla = CreateKey("TSLA", true, new DateTime(2025, 3, 21), 150.0, "O:TSLA250321C00150000");

            Assert.IsTrue(_comparer.Compare(aapl, spy) < 0, "AAPL should come before SPY");
            Assert.IsTrue(_comparer.Compare(spy, tsla) < 0, "SPY should come before TSLA");
            Assert.IsTrue(_comparer.Compare(aapl, tsla) < 0, "AAPL should come before TSLA");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_UnderlyingCaseSensitivity_UsesOrdinalComparison()
        {
            var lower = CreateKey("spy", true, new DateTime(2025, 3, 21), 400.0, "O:spy250321C00400000");
            var upper = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            
            // Ordinal comparison: uppercase comes before lowercase
            Assert.IsTrue(_comparer.Compare(upper, lower) < 0, "SPY should come before spy (ordinal comparison)");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_NumericUnderlyings_SortsAsStrings()
        {
            var key1 = CreateKey("1", true, new DateTime(2025, 3, 21), 100.0, "O:1250321C00100000");
            var key10 = CreateKey("10", true, new DateTime(2025, 3, 21), 100.0, "O:10250321C00100000");
            var key2 = CreateKey("2", true, new DateTime(2025, 3, 21), 100.0, "O:2250321C00100000");

            // String comparison: "1" < "10" < "2"
            Assert.IsTrue(_comparer.Compare(key1, key10) < 0, "'1' should come before '10' in string comparison");
            Assert.IsTrue(_comparer.Compare(key10, key2) < 0, "'10' should come before '2' in string comparison");
        }

        #endregion

        #region Call/Put Ordering Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_CallsVsPuts_SameUnderlyingExpirationStrike_CallsFirst()
        {
            var call = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            var put = CreateKey("SPY", false, new DateTime(2025, 3, 21), 400.0, "O:SPY250321P00400000");

            Assert.IsTrue(_comparer.Compare(call, put) < 0, "Call should come before Put for same contract parameters");
            Assert.IsTrue(_comparer.Compare(put, call) > 0, "Put should come after Call for same contract parameters");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_CallsAndPuts_DifferentUnderlyings_UnderlyingTakesPrecedence()
        {
            var aaplPut = CreateKey("AAPL", false, new DateTime(2025, 3, 21), 400.0, "O:AAPL250321P00400000");
            var spyCall = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");

            Assert.IsTrue(_comparer.Compare(aaplPut, spyCall) < 0, "AAPL Put should come before SPY Call (underlying precedence)");
        }

        #endregion

        #region Expiration Date Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_DifferentExpirations_SameUnderlyingAndType_EarlierFirst()
        {
            var march = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            var april = CreateKey("SPY", true, new DateTime(2025, 4, 18), 400.0, "O:SPY250418C00400000");
            var may = CreateKey("SPY", true, new DateTime(2025, 5, 16), 400.0, "O:SPY250516C00400000");

            Assert.IsTrue(_comparer.Compare(march, april) < 0, "March expiration should come before April");
            Assert.IsTrue(_comparer.Compare(april, may) < 0, "April expiration should come before May");
            Assert.IsTrue(_comparer.Compare(march, may) < 0, "March expiration should come before May");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_SameExpirationDifferentYears_EarlierYearFirst()
        {
            var year2025 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            var year2026 = CreateKey("SPY", true, new DateTime(2026, 3, 20), 400.0, "O:SPY260320C00400000");

            Assert.IsTrue(_comparer.Compare(year2025, year2026) < 0, "2025 expiration should come before 2026");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_ExpirationWithCallPut_CallPutTakesPrecedenceOverExpiration()
        {
            var marchPut = CreateKey("SPY", false, new DateTime(2025, 3, 21), 400.0, "O:SPY250321P00400000");
            var aprilCall = CreateKey("SPY", true, new DateTime(2025, 4, 18), 400.0, "O:SPY250418C00400000");

            Assert.IsTrue(_comparer.Compare(aprilCall, marchPut) < 0, "April Call should come before March Put (call/put precedence over expiration)");
        }

        #endregion

        #region Strike Price Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_DifferentStrikes_SameContractDetails_LowerStrikeFirst()
        {
            var strike390 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 390.0, "O:SPY250321C00390000");
            var strike400 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            var strike410 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 410.0, "O:SPY250321C00410000");

            Assert.IsTrue(_comparer.Compare(strike390, strike400) < 0, "Strike 390 should come before strike 400");
            Assert.IsTrue(_comparer.Compare(strike400, strike410) < 0, "Strike 400 should come before strike 410");
            Assert.IsTrue(_comparer.Compare(strike390, strike410) < 0, "Strike 390 should come before strike 410");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_FractionalStrikes_SortsCorrectly()
        {
            var strike99_5 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 99.5, "O:SPY250321C00099500");
            var strike100 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 100.0, "O:SPY250321C00100000");
            var strike100_5 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 100.5, "O:SPY250321C00100500");

            Assert.IsTrue(_comparer.Compare(strike99_5, strike100) < 0, "Strike 99.5 should come before strike 100");
            Assert.IsTrue(_comparer.Compare(strike100, strike100_5) < 0, "Strike 100 should come before strike 100.5");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_HighPrecisionStrikes_HandlesCorrectly()
        {
            var strike1 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 100.123, "O:SPY250321C00100123");
            var strike2 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 100.124, "O:SPY250321C00100124");

            Assert.IsTrue(_comparer.Compare(strike1, strike2) < 0, "Strike 100.123 should come before strike 100.124");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_VeryLargeStrikes_SortsCorrectly()
        {
            var strike1000 = CreateKey("AMZN", true, new DateTime(2025, 3, 21), 1000.0, "O:AMZN250321C01000000");
            var strike5000 = CreateKey("AMZN", true, new DateTime(2025, 3, 21), 5000.0, "O:AMZN250321C05000000");

            Assert.IsTrue(_comparer.Compare(strike1000, strike5000) < 0, "Strike 1000 should come before strike 5000");
        }

        #endregion

        #region RawTicker Tie-Breaker Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_IdenticalContractDetails_DifferentRawTickers_UsesOrdinalComparison()
        {
            var ticker1 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            var ticker2 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400001");

            Assert.IsTrue(_comparer.Compare(ticker1, ticker2) < 0, "RawTicker should be the final tie-breaker");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_IdenticalContractDetails_SameRawTicker_ReturnsZero()
        {
            var ticker1 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            var ticker2 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");

            Assert.AreEqual(0, _comparer.Compare(ticker1, ticker2), "Identical contracts should return 0");
        }

        #endregion

        #region Complex Sort Order Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_CompleteOptionsChain_SortsInExpectedOrder()
        {
            var contracts = new List<ContractKey>
            {
                // SPY March Puts (should be after ALL SPY calls)
                CreateKey("SPY", false, new DateTime(2025, 3, 21), 390.0, "O:SPY250321P00390000"),
                CreateKey("SPY", false, new DateTime(2025, 3, 21), 400.0, "O:SPY250321P00400000"),
                
                // AAPL March Calls (different underlying, should come first)
                CreateKey("AAPL", true, new DateTime(2025, 3, 21), 150.0, "O:AAPL250321C00150000"),
                CreateKey("AAPL", true, new DateTime(2025, 3, 21), 160.0, "O:AAPL250321C00160000"),
                
                // SPY March Calls
                CreateKey("SPY", true, new DateTime(2025, 3, 21), 390.0, "O:SPY250321C00390000"),
                CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000"),
                
                // SPY April Calls (later expiration)
                CreateKey("SPY", true, new DateTime(2025, 4, 18), 390.0, "O:SPY250418C00390000"),
                CreateKey("SPY", true, new DateTime(2025, 4, 18), 400.0, "O:SPY250418C00400000"),
            };

            // Sort using our comparer
            contracts.Sort(_comparer.Compare);

            // Expected order based on ContractKeyComparer logic:
            // Underlying ? Call/Put ? Expiration ? Strike ? RawTicker
            // 1. AAPL March 150 Call
            // 2. AAPL March 160 Call  
            // 3. SPY March 390 Call
            // 4. SPY March 400 Call
            // 5. SPY April 390 Call ? April calls come after March calls but before ANY puts
            // 6. SPY April 400 Call
            // 7. SPY March 390 Put  ? March puts come after ALL SPY calls
            // 8. SPY March 400 Put  

            Assert.AreEqual("AAPL", contracts[0].Underlying);
            Assert.IsTrue(contracts[0].IsCall);
            Assert.AreEqual(150.0, contracts[0].Strike);

            Assert.AreEqual("AAPL", contracts[1].Underlying);
            Assert.IsTrue(contracts[1].IsCall);
            Assert.AreEqual(160.0, contracts[1].Strike);

            Assert.AreEqual("SPY", contracts[2].Underlying);
            Assert.IsTrue(contracts[2].IsCall);
            Assert.AreEqual(new DateTime(2025, 3, 21), contracts[2].Expiration);
            Assert.AreEqual(390.0, contracts[2].Strike);

            Assert.AreEqual("SPY", contracts[3].Underlying);
            Assert.IsTrue(contracts[3].IsCall);
            Assert.AreEqual(new DateTime(2025, 3, 21), contracts[3].Expiration);
            Assert.AreEqual(400.0, contracts[3].Strike);

            Assert.AreEqual("SPY", contracts[4].Underlying);
            Assert.IsTrue(contracts[4].IsCall); // April Call 390
            Assert.AreEqual(new DateTime(2025, 4, 18), contracts[4].Expiration);
            Assert.AreEqual(390.0, contracts[4].Strike);

            Assert.AreEqual("SPY", contracts[5].Underlying);
            Assert.IsTrue(contracts[5].IsCall); // April Call 400
            Assert.AreEqual(new DateTime(2025, 4, 18), contracts[5].Expiration);
            Assert.AreEqual(400.0, contracts[5].Strike);

            Assert.AreEqual("SPY", contracts[6].Underlying);
            Assert.IsFalse(contracts[6].IsCall); // March Put 390
            Assert.AreEqual(new DateTime(2025, 3, 21), contracts[6].Expiration);
            Assert.AreEqual(390.0, contracts[6].Strike);

            Assert.AreEqual("SPY", contracts[7].Underlying);
            Assert.IsFalse(contracts[7].IsCall); // March Put 400
            Assert.AreEqual(new DateTime(2025, 3, 21), contracts[7].Expiration);
            Assert.AreEqual(400.0, contracts[7].Strike);
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_RealWorldOptionsData_SortsCorrectly()
        {
            var contracts = new List<ContractKey>
            {
                // Real-world option tickers in random order
                CreateKey("SPY", false, new DateTime(2025, 1, 17), 580.0, "O:SPY250117P00580000"),
                CreateKey("SPY", true, new DateTime(2025, 1, 17), 575.0, "O:SPY250117C00575000"),
                CreateKey("AAPL", true, new DateTime(2025, 1, 17), 220.0, "O:AAPL250117C00220000"),
                CreateKey("SPY", true, new DateTime(2025, 1, 17), 580.0, "O:SPY250117C00580000"),
                CreateKey("AAPL", false, new DateTime(2025, 1, 17), 220.0, "O:AAPL250117P00220000"),
                CreateKey("TSLA", true, new DateTime(2025, 1, 17), 250.0, "O:TSLA250117C00250000"),
            };

            contracts.Sort(_comparer.Compare);

            // Verify sort order: AAPL ? SPY ? TSLA, then Calls before Puts, then Strike ascending
            Assert.AreEqual("AAPL", contracts[0].Underlying);
            Assert.IsTrue(contracts[0].IsCall);
            
            Assert.AreEqual("AAPL", contracts[1].Underlying);
            Assert.IsFalse(contracts[1].IsCall);
            
            Assert.AreEqual("SPY", contracts[2].Underlying);
            Assert.IsTrue(contracts[2].IsCall);
            Assert.AreEqual(575.0, contracts[2].Strike);
            
            Assert.AreEqual("SPY", contracts[3].Underlying);
            Assert.IsTrue(contracts[3].IsCall);
            Assert.AreEqual(580.0, contracts[3].Strike);
            
            Assert.AreEqual("SPY", contracts[4].Underlying);
            Assert.IsFalse(contracts[4].IsCall);
            
            Assert.AreEqual("TSLA", contracts[5].Underlying);
            Assert.IsTrue(contracts[5].IsCall);
        }

        #endregion

        #region Edge Cases and Boundary Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_EmptyStrings_HandlesCorrectly()
        {
            var key1 = CreateKey("", true, new DateTime(2025, 3, 21), 400.0, "");
            var key2 = CreateKey("AAPL", true, new DateTime(2025, 3, 21), 400.0, "O:AAPL250321C00400000");

            // Empty string should come before any non-empty string in ordinal comparison
            Assert.IsTrue(_comparer.Compare(key1, key2) < 0, "Empty underlying should come before non-empty");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_NullStrings_HandlesCorrectly()
        {
            var keyWithNull = new ContractKey
            {
                Underlying = null,
                IsCall = true,
                Expiration = new DateTime(2025, 3, 21),
                Strike = 400.0,
                RawTicker = null
            };
            
            var keyWithValue = CreateKey("AAPL", true, new DateTime(2025, 3, 21), 400.0, "O:AAPL250321C00400000");

            // Should not throw exception
            var result = _comparer.Compare(keyWithNull, keyWithValue);
            Assert.IsTrue(result < 0, "Null underlying should come before non-null");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_ExtremeStrikeValues_HandlesCorrectly()
        {
            var veryLowStrike = CreateKey("SPY", true, new DateTime(2025, 3, 21), 0.01, "O:SPY250321C00000010");
            var veryHighStrike = CreateKey("SPY", true, new DateTime(2025, 3, 21), 99999.99, "O:SPY250321C99999990");

            Assert.IsTrue(_comparer.Compare(veryLowStrike, veryHighStrike) < 0, "Very low strike should come before very high strike");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_DateTimeMinMax_HandlesCorrectly()
        {
            var minDate = CreateKey("SPY", true, DateTime.MinValue, 400.0, "O:SPY000101C00400000");
            var maxDate = CreateKey("SPY", true, DateTime.MaxValue, 400.0, "O:SPY991231C00400000");
            var normalDate = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");

            Assert.IsTrue(_comparer.Compare(minDate, normalDate) < 0, "DateTime.MinValue should come first");
            Assert.IsTrue(_comparer.Compare(normalDate, maxDate) < 0, "DateTime.MaxValue should come last");
        }

        #endregion

        #region Performance and Consistency Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_IsConsistentAndTransitive()
        {
            var keyA = CreateKey("AAPL", true, new DateTime(2025, 3, 21), 150.0, "O:AAPL250321C00150000");
            var keyB = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            var keyC = CreateKey("TSLA", true, new DateTime(2025, 3, 21), 250.0, "O:TSLA250321C00250000");

            // Test transitivity: if A < B and B < C, then A < C
            Assert.IsTrue(_comparer.Compare(keyA, keyB) < 0, "A should be less than B");
            Assert.IsTrue(_comparer.Compare(keyB, keyC) < 0, "B should be less than C");
            Assert.IsTrue(_comparer.Compare(keyA, keyC) < 0, "A should be less than C (transitivity)");

            // Test consistency: repeated calls should return same result
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(-1, Math.Sign(_comparer.Compare(keyA, keyB)), "Comparison should be consistent");
                Assert.AreEqual(1, Math.Sign(_comparer.Compare(keyB, keyA)), "Reverse comparison should be consistent");
            }
        }

        [TestMethod]
        [TestCategory("Performance")]
        public void Compare_LargeDataset_SortsCorrectly()
        {
            var random = new Random(12345); // Fixed seed for reproducibility
            var contracts = new List<ContractKey>();
            var underlyings = new[] { "AAPL", "GOOGL", "MSFT", "SPY", "TSLA" };
            var expirations = new[]
            {
                new DateTime(2025, 1, 17),
                new DateTime(2025, 2, 21),
                new DateTime(2025, 3, 21),
                new DateTime(2025, 4, 18)
            };

            // Generate 1000 random contracts
            for (int i = 0; i < 1000; i++)
            {
                var underlying = underlyings[random.Next(underlyings.Length)];
                var isCall = random.Next(2) == 0;
                var expiration = expirations[random.Next(expirations.Length)];
                var strike = Math.Round(random.NextDouble() * 500 + 100, 2); // Strike between 100-600
                var rawTicker = $"O:{underlying}{expiration:yyMMdd}{(isCall ? 'C' : 'P')}{strike * 1000:00000000}";

                contracts.Add(CreateKey(underlying, isCall, expiration, strike, rawTicker));
            }

            // Sort the contracts
            contracts.Sort(_comparer.Compare);

            // Verify sort order is correct
            for (int i = 1; i < contracts.Count; i++)
            {
                Assert.IsTrue(_comparer.Compare(contracts[i - 1], contracts[i]) <= 0,
                    $"Contract at index {i - 1} should not be greater than contract at index {i}");
            }
        }

        #endregion

        #region ContractKey ToString Tests

        [TestMethod]
        [TestCategory("Core")]
        public void ContractKey_ToString_FormatsCorrectly()
        {
            var call = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            var put = CreateKey("SPY", false, new DateTime(2025, 3, 21), 400.0, "O:SPY250321P00400000");

            Assert.AreEqual("SPY|C|2025-03-21|400", call.ToString());
            Assert.AreEqual("SPY|P|2025-03-21|400", put.ToString());
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ContractKey_ToString_HandlesFractionalStrikes()
        {
            var key = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.5, "O:SPY250321C00400500");
            Assert.AreEqual("SPY|C|2025-03-21|400.5", key.ToString());
        }

        [TestMethod]
        [TestCategory("Core")]
        public void ContractKey_ToString_HandlesNullValues()
        {
            var key = new ContractKey
            {
                Underlying = null,
                IsCall = true,
                Expiration = new DateTime(2025, 3, 21),
                Strike = 400.0,
                RawTicker = null
            };

            var result = key.ToString();
            Assert.IsTrue(result.Contains("C|2025-03-21|400"), "Should handle null underlying gracefully");
        }

        #endregion

        #region Specific Logic Validation Tests

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_Debug_Your_Specific_Case()
        {
            // Test the exact case from your debugging scenario
            var target = CreateKey("A", true, new DateTime(2025, 9, 19), 135.0, "O:A250919C00135000");
            var current = CreateKey("A", true, new DateTime(2025, 9, 19), 120.0, "O:A250919C00120000");
            var higher = CreateKey("A", true, new DateTime(2025, 9, 19), 140.0, "O:A250919C00140000");
            var put = CreateKey("A", false, new DateTime(2025, 9, 19), 135.0, "O:A250919P00135000");
            var differentExp = CreateKey("A", true, new DateTime(2025, 10, 17), 130.0, "O:A251017C00130000");

            // Current (120) should come BEFORE target (135)
            Assert.IsTrue(_comparer.Compare(current, target) < 0, "120 strike should come before 135 strike");
            
            // Higher (140) should come AFTER target (135)  
            Assert.IsTrue(_comparer.Compare(target, higher) < 0, "135 strike should come before 140 strike");
            
            // Put should come AFTER call for same underlying/expiration/strike
            Assert.IsTrue(_comparer.Compare(target, put) < 0, "Call should come before Put for same parameters");
            
            // Different expiration should sort by expiration date
            Assert.IsTrue(_comparer.Compare(target, differentExp) < 0, "Sept expiration should come before Oct expiration");

            // Debug output (commented out as ConsoleUtilities may not be available in test environment)
            // ConsoleUtilities.WriteLine($"Target (135C): {target}");
            // ConsoleUtilities.WriteLine($"Current (120C): {current}");
            // ConsoleUtilities.WriteLine($"Higher (140C): {higher}");
            // ConsoleUtilities.WriteLine($"Put (135P): {put}");
            // ConsoleUtilities.WriteLine($"Different Exp (Oct 130C): {differentExp}");
        }

        [TestMethod]
        [TestCategory("Core")]
        public void Compare_StoppingConditions_ValidateLogic()
        {
            // Test all the "stopping conditions" mentioned in your requirements
            
            // 1. Same underlying, call vs put - call comes first
            var spyCall = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            var spyPut = CreateKey("SPY", false, new DateTime(2025, 3, 21), 400.0, "O:SPY250321P00400000");
            Assert.IsTrue(_comparer.Compare(spyCall, spyPut) < 0, "When searching for call and hit put, we've gone too far");

            // 2. Same everything except higher strike
            var strike395 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 395.0, "O:SPY250321C00395000");
            var strike405 = CreateKey("SPY", true, new DateTime(2025, 3, 21), 405.0, "O:SPY250321C00405000");
            Assert.IsTrue(_comparer.Compare(strike395, strike405) < 0, "When searching for 400 and hit 405, we've gone too far");

            // 3. Same underlying but later expiration
            var marchExp = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            var aprilExp = CreateKey("SPY", true, new DateTime(2025, 4, 18), 400.0, "O:SPY250418C00400000");
            Assert.IsTrue(_comparer.Compare(marchExp, aprilExp) < 0, "When searching for March and hit April, we've gone too far");

            // 4. Different underlying entirely
            var aapl = CreateKey("AAPL", true, new DateTime(2025, 3, 21), 400.0, "O:AAPL250321C00400000");
            var spy = CreateKey("SPY", true, new DateTime(2025, 3, 21), 400.0, "O:SPY250321C00400000");
            Assert.IsTrue(_comparer.Compare(aapl, spy) < 0, "When searching for AAPL and hit SPY, we've gone too far");
        }

        #endregion

        #region Test Helper Methods

        /// <summary>
        /// Helper method to create a ContractKey with specified parameters
        /// </summary>
        private static ContractKey CreateKey(string underlying, bool isCall, DateTime expiration, double strike, string rawTicker)
        {
            return new ContractKey
            {
                Underlying = underlying,
                IsCall = isCall,
                Expiration = expiration,
                Strike = strike,
                RawTicker = rawTicker
            };
        }

        #endregion
    }
}