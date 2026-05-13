using System;
using Trade.Polygon2;

namespace Trade.Tests.Models
{
    /// <summary>
    /// Result of loading contract files into tree storage
    /// Contains performance metrics and loaded data statistics
    /// </summary>
    [Serializable]
    public class TreeLoadResult
    {
        public OptionsCompressionTree Tree { get; set; }
        public int FilesProcessed { get; set; }
        public int ContractsLoaded { get; set; }
        public int TotalPriceRecords { get; set; }
        public long LoadingTimeMs { get; set; }
    }
}