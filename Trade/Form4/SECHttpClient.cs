using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Trade.Form4
{
    /// <summary>
    /// SEC-compliant HTTP client with rate limiting and required headers
    /// </summary>
    public class SECHttpClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _rateLimiter;
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly object _rateLimitLock = new object();

        public SECHttpClient(string userAgent)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            _httpClient.DefaultRequestHeaders.Add("Host", "www.sec.gov");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            // SEC allows max 10 requests per second
            _rateLimiter = new SemaphoreSlim(1, 1);
        }

        public async Task<string> GetStringAsync(string url)
        {
            await EnforceRateLimit();

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                // Check if response is gzip compressed
                var contentEncoding = response.Content.Headers.ContentEncoding;
                if (contentEncoding.Contains("gzip"))
                {
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var decompressedStream = new GZipStream(stream, CompressionMode.Decompress))
                    using (var reader = new StreamReader(decompressedStream, Encoding.UTF8))
                    {
                        return await reader.ReadToEndAsync();
                    }
                }
                else
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"{ex.Message} - {url}", ex);
            }
        }

        public async Task<byte[]> GetByteArrayAsync(string url)
        {
            await EnforceRateLimit();

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"{ex.Message} - {url}", ex);
            }
        }

        private async Task EnforceRateLimit()
        {
            await _rateLimiter.WaitAsync();
            try
            {
                lock (_rateLimitLock)
                {
                    var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
                    var minInterval = TimeSpan.FromMilliseconds(100); // 10 requests/second max

                    if (timeSinceLastRequest < minInterval)
                    {
                        var delay = minInterval - timeSinceLastRequest;
                        Thread.Sleep(delay);
                    }

                    _lastRequestTime = DateTime.UtcNow;
                }
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimiter?.Dispose();
        }
    }
}
