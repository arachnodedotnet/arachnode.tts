using System;

namespace Trade.Prices2
{
    /// <summary>
    ///     Represents a price record containing OHLC data, volume, and metadata for a specific time period.
    ///     Used throughout the trading system for price analysis, indicator calculations, and trade execution.
    /// </summary>
    public class PriceRecord
    {
        /// <summary>
        ///     Default constructor for creating an empty PriceRecord
        /// </summary>
        public PriceRecord()
        {
        }

        /// <summary>
        ///     Constructor for creating a PriceRecord with specified values
        /// </summary>
        /// <param name="dateTime">Date and time for this price record</param>
        /// <param name="timeFrame"></param>
        /// <param name="open">Opening price</param>
        /// <param name="high">Highest price during the period</param>
        /// <param name="low">Lowest price during the period</param>
        /// <param name="close">Closing price</param>
        /// <param name="volume">Trading volume (default: 0)</param>
        /// <param name="wap">Volume weighted average price (default: 0)</param>
        /// <param name="count">Number of trades (default: 0)</param>
        /// <param name="option">Associated option ticker information (default: null)</param>
        /// <param name="isComplete">Whether this price record is complete (default: true)</param>
        public PriceRecord(DateTime dateTime, TimeFrame timeFrame, double open, double high, double low, double close,
            double volume = 0,
            double wap = 0, int count = 0, Ticker option = null, bool isComplete = true)
        {
            // Validate that dateTime matches the TimeFrame
            ValidateDateTimeTimeFrameAlignment(dateTime, timeFrame);

            DateTime = dateTime;
            Time = dateTime.ToString("yyyyMMdd HH:mm:ss");
            TimeFrame = timeFrame;
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
            WAP = wap;
            Count = count;
            Option = option;
            IsComplete = isComplete;
        }

        /// <summary>
        ///     Validates that the dateTime parameter is properly aligned with the specified TimeFrame.
        ///     Throws ArgumentException if the dateTime has inappropriate precision for the TimeFrame.
        /// </summary>
        /// <param name="dateTime">The date and time to validate</param>
        /// <param name="timeFrame">The time frame to validate against</param>
        /// <exception cref="ArgumentException">Thrown when dateTime doesn't match TimeFrame requirements</exception>
        private static void ValidateDateTimeTimeFrameAlignment(DateTime dateTime, TimeFrame timeFrame)
        {
            switch (timeFrame)
            {
                case TimeFrame.D1:
                    // For daily timeframe, expect midnight (00:00:00) - no intraday time components
                    if (dateTime.Hour != 0 || dateTime.Minute != 0 || dateTime.Second != 0 || dateTime.Millisecond != 0)
                    {
                        throw new ArgumentException(
                            $"TimeFrame D1 requires dateTime to be midnight (00:00:00), but got {dateTime:yyyy-MM-dd HH:mm:ss.fff}. " +
                            $"For daily bars, use dateTime.Date to ensure proper alignment.",
                            nameof(dateTime));
                    }
                    break;

                case TimeFrame.H4:
                    // For 4-hour timeframe, expect hours to be multiples of 4 (0, 4, 8, 12, 16, 20)
                    if (dateTime.Hour % 4 != 0 || dateTime.Minute != 0 || dateTime.Second != 0 || dateTime.Millisecond != 0)
                    {
                        throw new ArgumentException(
                            $"TimeFrame H4 requires dateTime hour to be a multiple of 4 (0, 4, 8, 12, 16, 20) with zero minutes/seconds, " +
                            $"but got {dateTime:yyyy-MM-dd HH:mm:ss.fff}.",
                            nameof(dateTime));
                    }
                    break;

                case TimeFrame.H1:
                    // For 1-hour timeframe, expect zero minutes and seconds
                    if (dateTime.Minute != 0 || dateTime.Second != 0 || dateTime.Millisecond != 0)
                    {
                        throw new ArgumentException(
                            $"TimeFrame H1 requires dateTime to have zero minutes and seconds, " +
                            $"but got {dateTime:yyyy-MM-dd HH:mm:ss.fff}.",
                            nameof(dateTime));
                    }
                    break;

                case TimeFrame.M30:
                    // For 30-minute timeframe, expect minutes to be 0 or 30
                    if ((dateTime.Minute != 0 && dateTime.Minute != 30) || dateTime.Second != 0 || dateTime.Millisecond != 0)
                    {
                        throw new ArgumentException(
                            $"TimeFrame M30 requires dateTime minute to be 0 or 30 with zero seconds, " +
                            $"but got {dateTime:yyyy-MM-dd HH:mm:ss.fff}.",
                            nameof(dateTime));
                    }
                    break;

                case TimeFrame.M15:
                    // For 15-minute timeframe, expect minutes to be multiples of 15 (0, 15, 30, 45)
                    if (dateTime.Minute % 15 != 0 || dateTime.Second != 0 || dateTime.Millisecond != 0)
                    {
                        throw new ArgumentException(
                            $"TimeFrame M15 requires dateTime minute to be a multiple of 15 (0, 15, 30, 45) with zero seconds, " +
                            $"but got {dateTime:yyyy-MM-dd HH:mm:ss.fff}.",
                            nameof(dateTime));
                    }
                    break;

                case TimeFrame.M10:
                    // For 10-minute timeframe, expect minutes to be multiples of 10 (0, 10, 20, 30, 40, 50)
                    if (dateTime.Minute % 10 != 0 || dateTime.Second != 0 || dateTime.Millisecond != 0)
                    {
                        throw new ArgumentException(
                            $"TimeFrame M10 requires dateTime minute to be a multiple of 10 (0, 10, 20, 30, 40, 50) with zero seconds, " +
                            $"but got {dateTime:yyyy-MM-dd HH:mm:ss.fff}.",
                            nameof(dateTime));
                    }
                    break;

                case TimeFrame.M5:
                    // For 5-minute timeframe, expect minutes to be multiples of 5
                    if (dateTime.Minute % 5 != 0 || dateTime.Second != 0 || dateTime.Millisecond != 0)
                    {
                        throw new ArgumentException(
                            $"TimeFrame M5 requires dateTime minute to be a multiple of 5 with zero seconds, " +
                            $"but got {dateTime:yyyy-MM-dd HH:mm:ss.fff}.",
                            nameof(dateTime));
                    }
                    break;

                case TimeFrame.M1:
                    // For 1-minute timeframe, expect zero seconds and milliseconds
                    if (dateTime.Second != 0 || dateTime.Millisecond != 0)
                    {
                        throw new ArgumentException(
                            $"TimeFrame M1 requires dateTime to have zero seconds and milliseconds, " +
                            $"but got {dateTime:yyyy-MM-dd HH:mm:ss.fff}.",
                            nameof(dateTime));
                    }
                    break;

                case TimeFrame.BridgeBar:
                    // BridgeBar can have any time alignment - no validation needed
                    break;

                default:
                    throw new ArgumentException($"Unknown TimeFrame: {timeFrame}", nameof(timeFrame));
            }
        }

        // Core price data properties
        public string Time { get; set; }
        public DateTime DateTime { get; set; }
        public TimeFrame TimeFrame { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }

        // Volume and market data
        public double Volume { get; set; }
        public double WAP { get; set; } // Volume Weighted Average Price
        public int Count { get; set; } // Number of trades

        // State and metadata properties
        public bool IsComplete { get; set; } = true; // False for incomplete bars (e.g., current day)
        public string Debug { get; set; }
        public Ticker Option { get; set; }
        public bool Manufactured { get; set; }
        /// <summary>
        /// True if this bar was created by carrying forward the last known real trade price (gap fill).
        /// </summary>
        public bool FillForward { get; set; }

        /// <summary>
        ///     Creates a copy of the price record
        /// </summary>
        /// <returns>A new PriceRecord instance with the same values</returns>
        public PriceRecord Clone()
        {
            return new PriceRecord(DateTime, TimeFrame, Open, High, Low, Close, Volume, WAP, Count, Option, IsComplete)
            {
                Time = Time,
                Debug = Debug,
                Manufactured = Manufactured,
                FillForward = FillForward
            };
        }

        /// <summary>
        ///     Returns a string representation of the price record showing date and closing price
        /// </summary>
        /// <returns>Formatted string with DateTime and Close price</returns>
        public override string ToString()
        {
            return DateTime + " " + Close;
        }

        /// <summary>
        ///     Determines whether the specified object is equal to the current PriceRecord
        /// </summary>
        /// <param name="obj">The object to compare with the current PriceRecord</param>
        /// <returns>true if the specified object is equal to the current PriceRecord; otherwise, false</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is null || GetType() != obj.GetType()) return false;

            var other = (PriceRecord)obj;
            return DateTime == other.DateTime
                   && TimeFrame == other.TimeFrame
                   && Math.Abs(Open - other.Open) < 1e-8
                   && Math.Abs(High - other.High) < 1e-8
                   && Math.Abs(Low - other.Low) < 1e-8
                   && Math.Abs(Close - other.Close) < 1e-8
                   && Math.Abs(Volume - other.Volume) < 1e-8
                   && Math.Abs(WAP - other.WAP) < 1e-8
                   && Count == other.Count
                   && IsComplete == other.IsComplete
                   && Manufactured == other.Manufactured
                   && FillForward == other.FillForward;
        }

        /// <summary>
        ///     Serves as the default hash function for PriceRecord objects
        /// </summary>
        /// <returns>A hash code for the current PriceRecord</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 17;
                hashCode = hashCode * 23 + DateTime.GetHashCode();
                hashCode = hashCode * 23 + TimeFrame.GetHashCode();
                hashCode = hashCode * 23 + Open.GetHashCode();
                hashCode = hashCode * 23 + High.GetHashCode();
                hashCode = hashCode * 23 + Low.GetHashCode();
                hashCode = hashCode * 23 + Close.GetHashCode();
                hashCode = hashCode * 23 + Volume.GetHashCode();
                hashCode = hashCode * 23 + WAP.GetHashCode();
                hashCode = hashCode * 23 + Count.GetHashCode();
                hashCode = hashCode * 23 + IsComplete.GetHashCode();
                hashCode = hashCode * 23 + Manufactured.GetHashCode();
                hashCode = hashCode * 23 + FillForward.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        ///     Determines whether two PriceRecord instances are equal
        /// </summary>
        /// <param name="left">The first PriceRecord to compare</param>
        /// <param name="right">The second PriceRecord to compare</param>
        /// <returns>true if the PriceRecord instances are equal; otherwise, false</returns>
        public static bool operator ==(PriceRecord left, PriceRecord right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        /// <summary>
        ///     Determines whether two PriceRecord instances are not equal
        /// </summary>
        /// <param name="left">The first PriceRecord to compare</param>
        /// <param name="right">The second PriceRecord to compare</param>
        /// <returns>true if the PriceRecord instances are not equal; otherwise, false</returns>
        public static bool operator !=(PriceRecord left, PriceRecord right)
        {
            return !(left == right);
        }
    }
}