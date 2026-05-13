using System;
using Trade.Prices2;

namespace Trade.Polygon2
{
    /// <summary>
    ///     Result container for comprehensive stock and options data fetching operations.
    ///     Encapsulates all information about a combined stock and options data retrieval request,
    ///     including success status, loaded record counts, validation results, and error handling.
    ///     Used by Polygon.io integration services to provide detailed feedback on data operations.
    /// </summary>
    public class StockAndOptionsDataResult
    {
        #region Output Methods

        /// <summary>
        ///     Returns a human-readable summary of the data fetching operation results.
        ///     Provides a concise overview of success status and key statistics.
        ///     Format varies based on whether the operation succeeded or failed.
        /// </summary>
        /// <returns>A formatted string describing the operation outcome and statistics</returns>
        public override string ToString()
        {
            if (Success)
                return
                    $"✅ {Symbol} data fetch: {StockRecordsLoaded:N0} stock + {OptionsRecordsLoaded:N0} options records ({TotalOptionRequests:N0} requests)";
            return $"❌ {Symbol} data fetch failed: {ErrorMessage}";
        }

        #endregion

        #region Request Parameters

        /// <summary>
        ///     The underlying stock symbol for which data was requested (e.g., "SPY", "AAPL", "TSLA").
        ///     This represents the primary asset for which both stock price data and related options data were fetched.
        ///     Used for identifying and organizing results across multiple data fetch operations.
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        ///     The start date of the data retrieval period (inclusive).
        ///     Represents the earliest date for which market data was requested.
        ///     Used to define the beginning of the time range for historical data analysis.
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        ///     The end date of the data retrieval period (exclusive).
        ///     Represents the latest date boundary for the data request.
        ///     Used to define the conclusion of the time range for historical data analysis.
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        ///     Number of strike prices away from the underlying price for option selection.
        ///     Determines the range of option contracts to include in the data fetch operation.
        ///     Example: StrikesAway = 10 means options from (current price - 10) to (current price + 10) strikes.
        /// </summary>
        public int StrikesAway { get; set; }

        /// <summary>
        ///     Number of calendar days to expiration for option contract filtering.
        ///     Specifies the maximum time until expiration for options to include in the request.
        ///     Used to focus on short-term or long-term option strategies based on trading objectives.
        /// </summary>
        public int DaysAway { get; set; }

        #endregion

        #region Operation Results

        /// <summary>
        ///     Indicates whether the overall data fetching operation completed successfully.
        ///     True if both stock and options data were retrieved without critical errors.
        ///     False if the operation failed due to network issues, API limits, or data validation problems.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        ///     Detailed error message if the operation failed.
        ///     Provides specific information about what went wrong during the data fetch process.
        ///     Null or empty if the operation was successful.
        ///     Used for debugging, logging, and user feedback in case of failures.
        /// </summary>
        public string ErrorMessage { get; set; }

        #endregion

        #region Data Loading Statistics

        /// <summary>
        ///     Number of stock price records successfully loaded and processed.
        ///     Represents minute-level or daily stock price data points for the underlying symbol.
        ///     Used for assessing data completeness and quality of the stock data portion.
        /// </summary>
        public int StockRecordsLoaded { get; set; }

        /// <summary>
        ///     Number of option price records successfully loaded and processed.
        ///     Represents minute-level option price data across all requested strike prices and expirations.
        ///     Used for assessing the breadth and depth of options market data coverage.
        /// </summary>
        public int OptionsRecordsLoaded { get; set; }

        /// <summary>
        ///     Total number of individual option contract requests generated for this operation.
        ///     Represents the count of distinct option symbols that were queried from the data source.
        ///     Used for understanding the scope of the data operation and API usage tracking.
        /// </summary>
        public int TotalOptionRequests { get; set; }

        #endregion

        #region Validation and Data Quality

        /// <summary>
        ///     Comprehensive validation result containing data quality assessment and integrity checks.
        ///     Includes validation errors, warnings, and statistical analysis of the loaded data.
        ///     Used for ensuring data reliability before proceeding with trading analysis or backtesting.
        /// </summary>
        public ValidationResult ValidationResult { get; set; }

        /// <summary>
        ///     Array of loaded price records for both stock and options data.
        ///     Contains the actual market data that was successfully retrieved and validated.
        ///     Used for downstream processing, analysis, and integration with trading systems.
        ///     May include both underlying stock prices and option contract prices.
        /// </summary>
        public PriceRecord[] PriceRecords { get; set; }

        #endregion
    }
}