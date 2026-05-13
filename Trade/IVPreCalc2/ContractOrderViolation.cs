namespace Trade.IVPreCalc2
{
    /// <summary>
    /// Represents a violation in contract ordering within bulk files.
    /// Used for tracking out-of-order contract transitions.
    /// </summary>
    internal class ContractOrderViolation
    {
        public int LineNumber { get; set; }
        public string Previous { get; set; }
        public string Current { get; set; }
        public int ComparisonResult { get; set; }
    }
}