using System.Collections.Generic;

namespace Trade.IVPreCalc2
{
    /// <summary>
    /// Represents validation results when comparing contract data 
    /// between bulk files and split contract files.
    /// </summary>
    internal sealed class ContractVerificationResult
    {
        public string ContractSymbol { get; set; }
        public bool IsValid { get; set; }
        public int TotalRecords { get; set; }
        public int MatchedRecords { get; set; }
        public List<string> Mismatches { get; set; } = new List<string>();
    }
}