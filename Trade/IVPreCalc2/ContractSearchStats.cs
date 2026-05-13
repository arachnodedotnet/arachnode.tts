namespace Trade.IVPreCalc2
{
    /// <summary>
    /// Statistics about contract search operations across bulk files.
    /// Tracks the number of files searched and records found during contract tracing.
    /// </summary>
    internal sealed class ContractSearchStats
    {
        public int FilesWithData { get; set; }
        public int TotalRecordsFound { get; set; }
    }
}