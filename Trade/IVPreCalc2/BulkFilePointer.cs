using System;
using System.Collections.Generic;
using System.IO;

namespace Trade.IVPreCalc2
{
    internal sealed class BulkFilePointer
    {
        // 1.) For learning about the S3 bulk files and the Contracts splitting...
        //var prices = new Prices();
        //var polygon = new Polygon2.Polygon(prices, "SPY", 10, 10);
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public FileStream Stream { get; set; }
        public StreamReader Reader { get; set; }
        public long FileSize { get; set; }
        public long DataStartPosition { get; set; }
        public long CurrentPosition { get; set; }
        public string Header { get; set; }
        public Dictionary<string, long> LastKnownPositions { get; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        // NEW: precise line and lookahead control
        public int EolBytes { get; set; } = 2; // 1 for LF, set to 2 if CRLF
        public string LookaheadLine { get; set; }
        public long LookaheadLineStart { get; set; }
        public long LookaheadNextStart { get; set; }
        
        // NEW: Exact line number tracking
        public long CurrentLineNumber { get; set; } = 1; // Start at line 1 (header)
        public Dictionary<string, long> LastKnownLineNumbers { get; } = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        public long LookaheadLineNumber { get; set; }
    }
}