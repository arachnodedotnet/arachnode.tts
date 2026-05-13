namespace Trade.Polygon2
{
    /// <summary>
    ///     Enhanced S3DataDownloader with additional configuration options
    /// </summary>
    public class S3DataDownloader
    {
        #region Public Methods

        public override string ToString()
        {
            return
                $"S3: {(UseS3ForBulkData ? "Enabled" : "Disabled")} - Endpoint: {S3Endpoint} - Bucket: {S3BucketName ?? "Not Set"}";
        }

        #endregion

        #region Public Properties

        public string PolygonApiKey { get; set; }
        public string S3AccessKey { get; set; }
        public string S3SecretKey { get; set; }
        public string S3BucketName { get; set; }
        public string S3Region { get; set; }
        public string S3Endpoint { get; set; } = "https://files.polygon.io";
        public bool UseS3ForBulkData { get; set; }
        public int MaxConcurrentDownloads { get; set; } = 1;
        public int DownloadTimeoutMinutes { get; set; } = 10;
        public string LocalCacheDirectory { get; set; } = "PolygonBulkData";

        #endregion
    }
}