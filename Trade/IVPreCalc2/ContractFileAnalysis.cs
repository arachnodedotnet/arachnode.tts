using System.Collections.Generic;

namespace Trade.IVPreCalc2
{
    /// <summary>
    /// Helper class for analyzing contract file ordering and structure.
    /// Used in bulk file contract alignment validation.
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
    }
}