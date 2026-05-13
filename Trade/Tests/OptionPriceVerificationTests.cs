//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Linq;
//using Trade.Interfaces;
//using Trade.Polygon2;
//using Trade.Prices2;

//namespace Trade.Tests
//{
//    /// <summary>
//    /// Comprehensive options price verification system using:
//    /// 1. Minute-by-minute stock data from Polygon
//    /// 2. Per-contract option data from CSV files
//    /// 3. IV solver and Black-Scholes model
//    /// 4. Four years of SPY historical data
//    /// </summary>
//    [TestClass]
//    public class OptionPriceVerificationTests
//    {
//        private const string STOCK_DATA_PATH = @"C:\Applications\Trade\Trade\bin\Debug\PolygonBulkData\us_stocks_sip_minute_aggs";
//        private const string OPTION_DATA_PATH = @"C:\Applications\Trade\Trade\bin\Debug\ContractData\SPY";
//        private const double RISK_FREE_RATE = 0.05; // 5% risk-free rate
//        private const double DIVIDEND_YIELD = 0.018; // ~1.8% SPY dividend yield
//        private const double PRICE_TOLERANCE = 0.05; // $0.05 tolerance
//        private const double IV_TOLERANCE = 0.02; // 2% IV tolerance

//        private Prices _spyPrices;
//        private OptionPrices _optionPrices;
//        private IImpliedVolatilitySolver _ivSolver;

//        [TestInitialize]
//        public void TestInitialize()
//        {
//            // Initialize SPY stock data
//            _spyPrices = new Prices();
//            LoadSPYData();

//            // Initialize option prices
//            _optionPrices = new OptionPrices();
//            LoadOptionData();

//            // Initialize IV solver
//            _ivSolver = new ImpliedVolatilitySolver();
//            InitializeIVSolver();

//            ConsoleUtilities.WriteLine($"✅ Loaded SPY data: {_spyPrices.GetDailyPriceRecords().Length:N0} daily records");
//            ConsoleUtilities.WriteLine($"✅ Loaded option data: {_optionPrices.SymbolCount:N0} option symbols");
//        }

//        #region Data Loading Methods

//        private void LoadSPYData()
//        {
//            try
//            {
//                // Look for SPY minute data files
//                var spyFiles = Directory.GetFiles(STOCK_DATA_PATH, "*.csv", SearchOption.AllDirectories);
//                ConsoleUtilities.WriteLine($"Found {spyFiles.Length} SPY data files");

//                if (spyFiles.Length > 0)
//                {
//                    // Load the most recent or combined file
//                    var latestFile = spyFiles.OrderByDescending(f => f).FirstOrDefault();
//                    if (File.Exists(latestFile))
//                    {
//                        // Parse the Polygon CSV format and create PriceRecords
//                        var lines = File.ReadAllLines(latestFile);
//                        ConsoleUtilities.WriteLine($"📊 Processing {lines.Length:N0} lines from {Path.GetFileName(latestFile)}");

//                        // Create a new Prices instance (empty constructor)
//                        _spyPrices = new Prices(@"C:\Applications\Trade\Trade\bin\Debug\Constants.SPX_JSON");

//                        // Create a temporary Polygon instance for CSV parsing
//                        var tempPolygon = new Polygon(_spyPrices, "SPY", 5, 5, true);

//                        // Parse the CSV data using Polygon's parser
//                        var priceRecords = tempPolygon.LoadPolygonCsvFormat(lines, "SPY");

//                        if (priceRecords != null && priceRecords.Length > 0)
//                        {
//                            // Add the parsed records to our Prices instance via batch
//                            //_spyPrices.AddPricesBatch(priceRecords);
//                            ConsoleUtilities.WriteLine($"✅ Loaded SPY data from: {Path.GetFileName(latestFile)} ({priceRecords.Length:N0} records)");
//                        }
//                        else
//                        {
//                            ConsoleUtilities.WriteLine($"⚠️ No SPY records found in {Path.GetFileName(latestFile)}");
//                        }
//                    }
//                }
//                else
//                {
//                    // Fallback to default Constants.SPX_JSON in project directory
//                    _spyPrices = new Prices("Constants.SPX_JSON");
//                    ConsoleUtilities.WriteLine("⚠️ Using fallback Constants.SPX_JSON data");
//                }
//            }
//            catch (Exception ex)
//            {
//                ConsoleUtilities.WriteLine($"❌ Error loading SPY data: {ex.Message}");
//                _spyPrices = new Prices(); // Empty fallback
//            }
//        }

//        private void LoadOptionData()
//        {
//            try
//            {
//                if (!Directory.Exists(OPTION_DATA_PATH))
//                {
//                    ConsoleUtilities.WriteLine($"⚠️ Option data directory not found: {OPTION_DATA_PATH}");
//                    return;
//                }

//                // Find all option CSV files
//                var optionFiles = Directory.GetFiles(OPTION_DATA_PATH, "*.csv", SearchOption.AllDirectories);
//                ConsoleUtilities.WriteLine($"Found {optionFiles.Length} option CSV files");

//                if (optionFiles.Length == 0)
//                {
//                    ConsoleUtilities.WriteLine("⚠️ No option CSV files found");
//                    return;
//                }

//                // Load sample of option files for testing (first 100 to avoid memory issues)
//                var sampleFiles = optionFiles.Take(100).ToArray();
//                var allOptionRecords = new List<PriceRecord>();

//                foreach (var file in sampleFiles)
//                {
//                    try
//                    {
//                        var records = LoadOptionRecordsFromCSV(file);
//                        allOptionRecords.AddRange(records);

//                        if (allOptionRecords.Count > 10000) // Limit memory usage
//                            break;
//                    }
//                    catch (Exception ex)
//                    {
//                        ConsoleUtilities.WriteLine($"⚠️ Error loading {Path.GetFileName(file)}: {ex.Message}");
//                    }
//                }

//                if (allOptionRecords.Count > 0)
//                {
//                    _optionPrices.LoadFromPriceRecords(allOptionRecords.ToArray(), _spyPrices);
//                    ConsoleUtilities.WriteLine($"✅ Loaded {allOptionRecords.Count:N0} option records");
//                }
//            }
//            catch (Exception ex)
//            {
//                ConsoleUtilities.WriteLine($"❌ Error loading option data: {ex.Message}");
//            }
//        }

//        private PriceRecord[] LoadOptionRecordsFromCSV(string csvPath)
//        {
//            var records = new List<PriceRecord>();
//            var lines = File.ReadAllLines(csvPath);

//            // Skip header if present
//            var dataLines = lines.Skip(1);

//            foreach (var line in dataLines)
//            {
//                try
//                {
//                    var record = ParseOptionCSVLine(line, csvPath);
//                    if (record != null)
//                        records.Add(record);
//                }
//                catch (Exception ex)
//                {
//                    // Skip malformed lines
//                    continue;
//                }

//                if (records.Count > 1000) // Limit per file
//                    break;
//            }

//            return records.ToArray();
//        }

//        private PriceRecord ParseOptionCSVLine(string line, string csvPath)
//        {
//            var parts = line.Split(',');
//            if (parts.Length < 6) return null;

//            // Parse basic fields: DateTime,Open,High,Low,Close,Volume
//            if (!DateTime.TryParse(parts[0], out var dateTime)) return null;
//            if (!double.TryParse(parts[1], out var open)) return null;
//            if (!double.TryParse(parts[2], out var high)) return null;
//            if (!double.TryParse(parts[3], out var low)) return null;
//            if (!double.TryParse(parts[4], out var close)) return null;
//            if (!long.TryParse(parts[5], out var volume)) return null;

//            // Extract option symbol from filename
//            var fileName = Path.GetFileNameWithoutExtension(csvPath);
//            var optionSymbol = fileName.Replace("_", ":"); // Convert filename format to option symbol

//            var ticker = Ticker.ParseToOption(optionSymbol);

//            return new PriceRecord(dateTime, open, high, low, close, volume, close, 0, ticker, false);
//        }

//        private void InitializeIVSolver()
//        {
//            try
//            {
//                // Load SPY historical prices into IV solver
//                var tempCsvPath = CreateTempSPYCSV();
//                if (File.Exists(tempCsvPath))
//                {
//                    _ivSolver.LoadClosePrices(tempCsvPath);
//                    File.Delete(tempCsvPath); // Clean up
//                    ConsoleUtilities.WriteLine("✅ IV Solver initialized with SPY data");
//                }
//            }
//            catch (Exception ex)
//            {
//                ConsoleUtilities.WriteLine($"⚠️ IV Solver initialization warning: {ex.Message}");
//            }
//        }

//        private string CreateTempSPYCSV()
//        {
//            var tempPath = Path.GetTempFileName();
//            var dailyRecords = _spyPrices.GetDailyPriceRecords();

//            using (var writer = new StreamWriter(tempPath))
//            {
//                writer.WriteLine("Date,Open,High,Low,Close,Volume");
//                foreach (var record in dailyRecords)
//                {
//                    writer.WriteLine($"{record.DateTime:yyyy-MM-dd},{record.Open},{record.High},{record.Low},{record.Close},{record.Volume}");
//                }
//            }

//            return tempPath;
//        }

//        #endregion

//        #region Verification Tests

//        [TestMethod]
//        public void VerifyOptionPrices_BlackScholes_vs_MarketData()
//        {
//            ConsoleUtilities.WriteLine("\n🔍 OPTION PRICE VERIFICATION: Black-Scholes vs Market Data");

//            var verificationResults = new List<OptionVerificationResult>();
//            var testDate = DateTime.Now.AddDays(-30); // Test data from 30 days ago

//            // Get SPY price for test date
//            var spyPrice = _spyPrices.GetPriceAt(testDate, TimeFrame.D1);
//            if (spyPrice == null)
//            {
//                Assert.Inconclusive("No SPY data available for test date");
//                return;
//            }

//            ConsoleUtilities.WriteLine($"📊 SPY Price on {testDate:yyyy-MM-dd}: ${spyPrice.Close:F2}");

//            // Test various option contracts
//            var testStrikes = new[] { -20, -10, 0, 10, 20 }; // Strikes relative to current price
//            var testExpirations = new[] { 7, 14, 30, 60 }; // Days to expiration

//            foreach (var strikeOffset in testStrikes)
//            {
//                foreach (var daysToExpiration in testExpirations)
//                {
//                    var strike = Math.Round(spyPrice.Close) + strikeOffset;
//                    if (strike <= 0) continue;

//                    // Test both calls and puts
//                    foreach (var isCall in new[] { true, false })
//                    {
//                        var result = VerifySingleOptionContract(
//                            testDate, spyPrice.Close, strike, daysToExpiration, isCall);

//                        if (result != null)
//                            verificationResults.Add(result);
//                    }
//                }
//            }

//            // Analyze results
//            AnalyzeVerificationResults(verificationResults);
//        }

//        private OptionVerificationResult VerifySingleOptionContract(
//            DateTime date, double underlyingPrice, double strike, int daysToExpiration, bool isCall)
//        {
//            try
//            {
//                double timeToExpiration = daysToExpiration / 365.0;

//                // Calculate theoretical price using Black-Scholes
//                double theoreticalPrice = _ivSolver.BlackScholesPrice(
//                    underlyingPrice, strike, timeToExpiration,
//                    RISK_FREE_RATE, DIVIDEND_YIELD, 0.20, isCall); // Assume 20% volatility

//                // Try to get actual market price from option data
//                var optionType = isCall ? Trade.Polygon2.OptionType.Call : Trade.Polygon2.OptionType.Put;
//                var marketRecord = _optionPrices.GetOptionPrice(
//                    _spyPrices, optionType, date, TimeFrame.D1,
//                    (int)(strike - underlyingPrice), daysToExpiration);

//                double? marketPrice = marketRecord?.Close;

//                // Calculate implied volatility if we have market price
//                double? impliedVolatility = null;
//                if (marketPrice.HasValue && marketPrice > 0)
//                {
//                    impliedVolatility = _ivSolver.SolveIV(
//                        underlyingPrice, strike, timeToExpiration,
//                        RISK_FREE_RATE, DIVIDEND_YIELD, marketPrice.Value, isCall);
//                }

//                return new OptionVerificationResult
//                {
//                    Date = date,
//                    UnderlyingPrice = underlyingPrice,
//                    Strike = strike,
//                    DaysToExpiration = daysToExpiration,
//                    IsCall = isCall,
//                    TheoreticalPrice = theoreticalPrice,
//                    MarketPrice = marketPrice,
//                    ImpliedVolatility = impliedVolatility,
//                    PriceDifference = marketPrice.HasValue ?
//                        Math.Abs(theoreticalPrice - marketPrice.Value) : (double?)null
//                };
//            }
//            catch (Exception ex)
//            {
//                ConsoleUtilities.WriteLine($"⚠️ Error verifying option: {ex.Message}");
//                return null;
//            }
//        }

//        private void AnalyzeVerificationResults(List<OptionVerificationResult> results)
//        {
//            ConsoleUtilities.WriteLine($"\n📈 VERIFICATION RESULTS ANALYSIS");
//            ConsoleUtilities.WriteLine($"Total tests: {results.Count:N0}");

//            var withMarketData = results.Where(r => r.MarketPrice.HasValue).ToList();
//            var withoutMarketData = results.Where(r => !r.MarketPrice.HasValue).ToList();

//            ConsoleUtilities.WriteLine($"With market data: {withMarketData.Count:N0}");
//            ConsoleUtilities.WriteLine($"Without market data: {withoutMarketData.Count:N0}");

//            if (withMarketData.Count > 0)
//            {
//                // Price difference analysis
//                var avgPriceDiff = withMarketData.Average(r => r.PriceDifference.Value);
//                var maxPriceDiff = withMarketData.Max(r => r.PriceDifference.Value);
//                var withinTolerance = withMarketData.Count(r => r.PriceDifference <= PRICE_TOLERANCE);

//                ConsoleUtilities.WriteLine($"\n💰 PRICE DIFFERENCES:");
//                ConsoleUtilities.WriteLine($"Average difference: ${avgPriceDiff:F3}");
//                ConsoleUtilities.WriteLine($"Maximum difference: ${maxPriceDiff:F3}");
//                ConsoleUtilities.WriteLine($"Within tolerance (±${PRICE_TOLERANCE:F2}): {withinTolerance}/{withMarketData.Count} ({(double)withinTolerance / withMarketData.Count:P1})");

//                // Implied volatility analysis
//                var withIV = withMarketData.Where(r => r.ImpliedVolatility.HasValue).ToList();
//                if (withIV.Count > 0)
//                {
//                    var avgIV = withIV.Average(r => r.ImpliedVolatility.Value);
//                    var minIV = withIV.Min(r => r.ImpliedVolatility.Value);
//                    var maxIV = withIV.Max(r => r.ImpliedVolatility.Value);

//                    ConsoleUtilities.WriteLine($"\n📊 IMPLIED VOLATILITY:");
//                    ConsoleUtilities.WriteLine($"Average IV: {avgIV:P1}");
//                    ConsoleUtilities.WriteLine($"IV Range: {minIV:P1} - {maxIV:P1}");
//                }

//                // Display sample results
//                DisplaySampleResults(withMarketData.Take(10));
//            }

//            // Basic assertions
//            Assert.IsTrue(results.Count > 0, "Should generate verification results");
//            Assert.IsTrue(results.All(r => r.TheoreticalPrice > 0), "All theoretical prices should be positive");

//            if (withMarketData.Count > 0)
//            {
//                var accurateResults = withMarketData.Count(r => r.PriceDifference <= PRICE_TOLERANCE);
//                var accuracyRate = (double)accurateResults / withMarketData.Count;

//                ConsoleUtilities.WriteLine($"\n✅ OVERALL ACCURACY: {accuracyRate:P1} ({accurateResults}/{withMarketData.Count})");

//                // We expect at least 70% accuracy for a reasonable model
//                Assert.IsTrue(accuracyRate > 0.70, $"Accuracy rate should be > 70%, actual: {accuracyRate:P1}");
//            }
//        }

//        private void DisplaySampleResults(IEnumerable<OptionVerificationResult> sampleResults)
//        {
//            ConsoleUtilities.WriteLine($"\n📋 SAMPLE RESULTS:");
//            ConsoleUtilities.WriteLine("Type   Strike  Exp  Theoretical  Market   Diff    IV");
//            ConsoleUtilities.WriteLine("─────  ──────  ───  ───────────  ───────  ──────  ────");

//            foreach (var result in sampleResults)
//            {
//                var type = result.IsCall ? "CALL" : "PUT ";
//                var market = result.MarketPrice?.ToString("F2") ?? "N/A";
//                var diff = result.PriceDifference?.ToString("F3") ?? "N/A";
//                var iv = result.ImpliedVolatility?.ToString("P1") ?? "N/A";

//                ConsoleUtilities.WriteLine($"{type}   ${result.Strike:F0}     {result.DaysToExpiration:D2}d   ${result.TheoreticalPrice:F2}       ${market}    ${diff}    {iv}");
//            }
//        }

//        [TestMethod]
//        public void VerifyImpliedVolatility_Consistency()
//        {
//            ConsoleUtilities.WriteLine("\n🔍 IMPLIED VOLATILITY CONSISTENCY TEST");

//            // Test IV solver with known option prices
//            var testCases = new[]
//            {
//                new { S = 100.0, K = 100.0, T = 0.25, Price = 5.0, IsCall = true },
//                new { S = 100.0, K = 105.0, T = 0.25, Price = 2.5, IsCall = true },
//                new { S = 100.0, K = 95.0, T = 0.25, Price = 2.5, IsCall = false }
//            };

//            foreach (var test in testCases)
//            {
//                // Solve for IV
//                var iv = _ivSolver.SolveIV(test.S, test.K, test.T,
//                    RISK_FREE_RATE, DIVIDEND_YIELD, test.Price, test.IsCall);

//                // Use IV to calculate price back
//                var calculatedPrice = _ivSolver.BlackScholesPrice(
//                    test.S, test.K, test.T, RISK_FREE_RATE, DIVIDEND_YIELD, iv, test.IsCall);

//                var priceDiff = Math.Abs(calculatedPrice - test.Price);

//                ConsoleUtilities.WriteLine($"{(test.IsCall ? "CALL" : "PUT ")} ${test.K} @ ${test.S}: " +
//                    $"IV={iv:P1}, Price=${calculatedPrice:F3} (target: ${test.Price:F2}, diff: ${priceDiff:F3})");

//                Assert.IsTrue(!double.IsNaN(iv), "IV should be calculable");
//                Assert.IsTrue(priceDiff < 0.01, $"Price reconstruction should be accurate within $0.01, actual diff: ${priceDiff:F3}");
//            }
//        }

//        [TestMethod]
//        public void VerifyOptionData_Coverage_and_Quality()
//        {
//            ConsoleUtilities.WriteLine("\n🔍 OPTION DATA COVERAGE & QUALITY VERIFICATION");

//            // Check data coverage
//            var summary = _optionPrices.GetSummary();
//            ConsoleUtilities.WriteLine($"📊 {summary}");

//            // Verify we have reasonable data coverage
//            Assert.IsTrue(_optionPrices.SymbolCount > 0, "Should have loaded option symbols");
//            Assert.IsTrue(_optionPrices.GetTotalRecordCount() > 0, "Should have option price records");

//            // Test data quality for a few symbols
//            var spyOptions = _optionPrices.GetOptionsByUnderlying("SPY");
//            ConsoleUtilities.WriteLine($"🎯 SPY Options: {spyOptions.Count:N0} symbols");

//            if (spyOptions.Count > 0)
//            {
//                var sampleSymbol = spyOptions.Keys.First();
//                var samplePrices = _optionPrices.GetPricesForSymbol(sampleSymbol);
//                var sampleRecords = samplePrices?.GetDailyPriceRecords();

//                if (sampleRecords?.Length > 0)
//                {
//                    ConsoleUtilities.WriteLine($"📈 Sample: {sampleSymbol} - {sampleRecords.Length:N0} records");

//                    // Check for reasonable price ranges
//                    var minPrice = sampleRecords.Min(r => r.Close);
//                    var maxPrice = sampleRecords.Max(r => r.Close);
//                    var avgPrice = sampleRecords.Average(r => r.Close);

//                    ConsoleUtilities.WriteLine($"   Price range: ${minPrice:F2} - ${maxPrice:F2} (avg: ${avgPrice:F2})");

//                    Assert.IsTrue(minPrice > 0, "All option prices should be positive");
//                    Assert.IsTrue(maxPrice > minPrice, "Should have price variation");
//                }
//            }
//        }

//        #endregion

//        #region Helper Classes

//        public class OptionVerificationResult
//        {
//            public DateTime Date { get; set; }
//            public double UnderlyingPrice { get; set; }
//            public double Strike { get; set; }
//            public int DaysToExpiration { get; set; }
//            public bool IsCall { get; set; }
//            public double TheoreticalPrice { get; set; }
//            public double? MarketPrice { get; set; }
//            public double? ImpliedVolatility { get; set; }
//            public double? PriceDifference { get; set; }
//        }

//        #endregion
//    }
//}

