using System;

namespace Trade.Polygon2
{
    public partial class Polygon
    {
        /// <summary>
        ///     ✅ NEW: Helper class for file metadata
        /// </summary>
        private class FileMetadata
        {
            public string StoredHash { get; set; }
            public string ETag { get; set; }
            public long FileSize { get; set; }
            public DateTime DownloadTime { get; set; }
        }
    }
}