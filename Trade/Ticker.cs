using System;
using System.Globalization;

namespace Trade
{
    /// <summary>
    ///     Represents a parsed option contract or underlying ticker
    /// </summary>
    [Serializable] 
    public class Ticker
    {
        #region Private Helper Methods

        /// <summary>
        ///     Attempt to parse the symbol as an option contract
        /// </summary>
        /// <param name="symbol">Symbol to parse</param>
        /// <param name="ticker">Ticker object to populate</param>
        /// <returns>True if successfully parsed as option, false otherwise</returns>
        private static bool TryParseAsOption(string symbol, Ticker ticker)
        {
            // Option symbol format: SYMBOL + YYMMDD + C/P + 8-digit strike price
            // Example: SPY240814C00390000
            // Minimum length check (symbol could be 1+ chars, 6 for date, 1 for type, 8 for strike = 16+ minimum)
            if (symbol.Length < 16) return false;

            try
            {
                // Extract the 8-digit strike price from the end
                var strikePart = symbol.Substring(symbol.Length - 8);
                if (!int.TryParse(strikePart, out var strikeInThousandths)) return false;
                ticker.StrikePrice = strikeInThousandths / 1000.0; // Convert from thousandths to dollars

                // Extract option type (C or P) - one character before the strike price
                var optionTypeChar = symbol[symbol.Length - 9];
                switch (optionTypeChar)
                {
                    case 'C':
                    case 'c':
                        ticker.OptionType = Polygon2.OptionType.Call;
                        break;
                    case 'P':
                    case 'p':
                        ticker.OptionType = Polygon2.OptionType.Put;
                        break;
                    default:
                        return false; // Invalid option type
                }

                // Extract the 6-digit expiration date (YYMMDD) before the option type
                if (symbol.Length < 15) return false; // Not enough characters for date + type + strike

                var expirationPart = symbol.Substring(symbol.Length - 15, 6);
                if (!DateTime.TryParseExact(expirationPart, "yyMMdd", null, DateTimeStyles.None,
                        out var expirationDate)) return false;
                ticker.ExpirationDate = expirationDate;

                // Extract underlying symbol (everything before the date)
                ticker.UnderlyingSymbol = symbol.Substring(0, symbol.Length - 15).ToUpper();

                if (string.IsNullOrEmpty(ticker.UnderlyingSymbol)) return false; // No underlying symbol found

                return true;
            }
            catch
            {
                return false; // Any parsing error means it's not a valid option symbol
            }
        }

        #endregion

        #region Properties

        /// <summary>
        ///     The underlying symbol (e.g., "SPY", "AAPL")
        /// </summary>
        public string UnderlyingSymbol { get; set; }

        /// <summary>
        ///     Option expiration date (only valid if IsOption is true)
        /// </summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>
        ///     Option type: Call or Put (only valid if IsOption is true)
        /// </summary>
        public Polygon2.OptionType? OptionType { get; set; }

        /// <summary>
        ///     Strike price (only valid if IsOption is true)
        /// </summary>
        public double? StrikePrice { get; set; }

        /// <summary>
        ///     Complete option symbol (e.g., "SPY240814C00390000") or ticker symbol
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        ///     True if this represents an option, false if it's just a ticker
        /// </summary>
        public bool IsOption { get; set; }

        /// <summary>
        ///     Original prefix if present (e.g., "O:")
        /// </summary>
        public string Prefix { get; set; }

        #endregion

        #region Object Override Methods

        /// <summary>
        ///     Returns a string representation of the ticker
        /// </summary>
        /// <returns>Formatted string showing ticker or option details</returns>
        public override string ToString()
        {
            if (IsOption && ExpirationDate.HasValue && OptionType.HasValue && StrikePrice.HasValue)
                return
                    $"{UnderlyingSymbol} {ExpirationDate.Value:yyyy-MM-dd} {OptionType.Value} ${StrikePrice.Value:F2}";
            return $"{UnderlyingSymbol} (Ticker)";
        }

        /// <summary>
        ///     Determines whether the specified object is equal to the current ticker
        /// </summary>
        /// <param name="obj">The object to compare with the current ticker</param>
        /// <returns>True if the specified object is equal to the current ticker; otherwise, false</returns>
        public override bool Equals(object obj)
        {
            if (obj is Ticker other)
            {
                if (IsOption != other.IsOption) return false;
                if (UnderlyingSymbol != other.UnderlyingSymbol) return false;

                if (IsOption)
                    return ExpirationDate == other.ExpirationDate &&
                           OptionType == other.OptionType &&
                           Math.Abs((StrikePrice ?? 0) - (other.StrikePrice ?? 0)) < 0.01;

                return true; // For tickers, just compare underlying symbol
            }

            return false;
        }

        /// <summary>
        ///     Serves as the default hash function for ticker objects
        /// </summary>
        /// <returns>A hash code for the current ticker</returns>
        public override int GetHashCode()
        {
            if (IsOption && ExpirationDate.HasValue && OptionType.HasValue)
                // Custom hash calculation to avoid conflict with existing HashCode class
                unchecked
                {
                    var hash = 17;
                    hash = hash * 23 + (UnderlyingSymbol?.GetHashCode() ?? 0);
                    hash = hash * 23 + ExpirationDate.GetHashCode();
                    hash = hash * 23 + OptionType.GetHashCode();
                    return hash;
                }

            return UnderlyingSymbol?.GetHashCode() ?? 0;
        }

        #endregion

        #region Static Parsing Methods

        /// <summary>
        ///     Parse input string into a Ticker object representing either an option or underlying ticker
        ///     Supports formats like "O:SPY240814C00390000" or "SPY240814C00390000" for options, or simple tickers like "SPY"
        ///     If the input cannot be parsed as an option, it populates the underlying ticker and sets IsOption = false
        /// </summary>
        /// <param name="input">Input string to parse (option symbol or ticker)</param>
        /// <returns>Ticker object with option details if valid, or ticker with IsOption=false</returns>
        /// <exception cref="ArgumentException">Thrown when the input is null or empty</exception>
        public static Ticker ParseToOption(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be null or empty", nameof(input));

            var ticker = new Ticker
            {
                Symbol = input.Trim()
            };

            // Handle "O:" prefix if present
            var symbolToParse = input.Trim();
            if (symbolToParse.StartsWith("O:", StringComparison.OrdinalIgnoreCase))
            {
                ticker.Prefix = "O:";
                symbolToParse = symbolToParse.Substring(2);
            }

            // Try to parse as option symbol first
            if (TryParseAsOption(symbolToParse, ticker))
            {
                ticker.IsOption = true;
                return ticker;
            }

            // If parsing as option fails, treat as ticker
            ticker.UnderlyingSymbol = symbolToParse.ToUpper();
            ticker.IsOption = false;
            ticker.ExpirationDate = null;
            ticker.OptionType = null;
            ticker.StrikePrice = null;

            return ticker;
        }

        /// <summary>
        ///     Determine if input string is a valid option symbol format
        /// </summary>
        /// <param name="input">Input string to check</param>
        /// <returns>True if input appears to be an option symbol, false if it's likely just a ticker</returns>
        public static bool IsValidOptionSymbol(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;

            // Handle "O:" prefix
            var symbolToParse = input.Trim();
            if (symbolToParse.StartsWith("O:", StringComparison.OrdinalIgnoreCase))
                symbolToParse = symbolToParse.Substring(2);

            var tempTicker = new Ticker();
            return TryParseAsOption(symbolToParse, tempTicker);
        }

        /// <summary>
        ///     Parse multiple symbols into Ticker objects
        /// </summary>
        /// <param name="symbols">Collection of symbol strings</param>
        /// <returns>Array of parsed Ticker objects</returns>
        public static Ticker[] ParseMultiple(params string[] symbols)
        {
            if (symbols == null) return new Ticker[0];

            var tickers = new Ticker[symbols.Length];
            for (var i = 0; i < symbols.Length; i++)
                try
                {
                    tickers[i] = ParseToOption(symbols[i]);
                }
                catch (ArgumentException)
                {
                    // Skip invalid symbols
                    tickers[i] = null;
                }

            return tickers;
        }

        #endregion

        #region Public Instance Methods

        /// <summary>
        ///     Get the display name for the ticker
        /// </summary>
        /// <returns>Formatted display string</returns>
        public string GetDisplayName()
        {
            if (IsOption && ExpirationDate.HasValue && OptionType.HasValue && StrikePrice.HasValue)
            {
                var typeChar = OptionType.Value == Polygon2.OptionType.Call ? "C" : "P";
                return $"{UnderlyingSymbol} {ExpirationDate.Value:MMM dd, yyyy} {typeChar} ${StrikePrice.Value:F2}";
            }

            return UnderlyingSymbol;
        }

        /// <summary>
        ///     Generate the standard option symbol format if this is an option
        /// </summary>
        /// <returns>Standard option symbol or underlying symbol if not an option</returns>
        public string GetStandardSymbol()
        {
            if (IsOption && ExpirationDate.HasValue && OptionType.HasValue && StrikePrice.HasValue)
            {
                var typeChar = OptionType.Value == Polygon2.OptionType.Call ? "C" : "P";
                var expDateString = ExpirationDate.Value.ToString("yyMMdd");
                var strikePriceString = $"{(int)(StrikePrice.Value * 1000):D8}"; // Strike in thousandths, 8 digits

                return $"{UnderlyingSymbol}{expDateString}{typeChar}{strikePriceString}";
            }

            return UnderlyingSymbol;
        }

        #endregion
    }
}