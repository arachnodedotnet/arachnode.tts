using System.Collections.Generic;

namespace Trade.Tests
{
    /// <summary>
    /// Helper class for analyzing contract file ordering and structure.
    /// Used in bulk file contract alignment validation.
    /// Enhanced to include parsing issue detection and reporting.
    /// </summary>
    internal class ContractFileAnalysis
    {
        public string FilePath { get; set; }
        public int FileNumber { get; set; }
        public string Header { get; set; }
        public int TotalContracts { get; set; }
        public List<string> ContractsSampled { get; set; } = new List<string>();
        public int OutOfOrderTransitions { get; set; }
        public List<ContractOrderViolation> OrderViolations { get; set; }
        
        /// <summary>
        /// List of parsing issues discovered during file analysis.
        /// Includes malformed lines, invalid data, and structural problems.
        /// </summary>
        public List<string> ParsingIssues { get; set; } = new List<string>();
    }
}