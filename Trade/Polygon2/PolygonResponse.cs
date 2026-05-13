namespace Trade.Polygon2
{
    /// <summary>
    ///     Polygon.io API response structure for bulk data
    /// </summary>
    public class PolygonResponse
    {
        public string status { get; set; }
        public string request_id { get; set; }
        public int count { get; set; }
        public PolygonResult[] results { get; set; }
    }
}