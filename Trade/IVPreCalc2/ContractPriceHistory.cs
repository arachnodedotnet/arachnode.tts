using System.Collections.Generic;

namespace Trade.IVPreCalc2
{
    /// <summary>
    /// Contains the complete price history for a contract symbol,
    /// including daily aggregated prices and metadata about the search operation.
    /// </summary>
    internal sealed class ContractPriceHistory
    {
        public string ContractSymbol { get; set; }
        public List<DailyContractPrice> Prices { get; set; }
        public int FilesSearched { get; set; }
        public int TotalFilesAvailable { get; set; }
        public ContractSearchStats SearchStats { get; set; }
        
        /// <summary>
        /// Earliest and latest files where this contract appears (by file date)
        /// </summary>
        public string FirstSourceFile { get; set; }
        public string LastSourceFile { get; set; }

        /// <summary>
        /// The last file that was actually searched, regardless of whether it contained matches.
        /// This provides debugging information about search termination and is useful when 
        /// FirstSourceFile and LastSourceFile are the same (only one match found).
        /// </summary>
        public string LastFileSearched { get; set; }
    }
}