using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Trade.Polygon2
{
    public partial class Polygon
    {
        /// <summary>
        ///     ✅ NEW: Stream wrapper that calculates hash during read/write operations
        /// </summary>
        private class HashingStream : Stream
        {
            private readonly Stream _baseStream;
            private readonly SHA256 _sha256;

            public HashingStream(Stream baseStream)
            {
                _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
                _sha256 = SHA256.Create();
            }

            // Implement required Stream members
            public override bool CanRead => _baseStream.CanRead;
            public override bool CanSeek => _baseStream.CanSeek;
            public override bool CanWrite => _baseStream.CanWrite;
            public override long Length => _baseStream.Length;

            public override long Position
            {
                get => _baseStream.Position;
                set => _baseStream.Position = value;
            }

            public string GetHash()
            {
                _sha256.TransformFinalBlock(new byte[0], 0, 0);
                var hash = _sha256.Hash;
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _baseStream.Write(buffer, offset, count);
                _sha256.TransformBlock(buffer, offset, count, null, 0);
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count,
                CancellationToken cancellationToken)
            {
                await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
                _sha256.TransformBlock(buffer, offset, count, null, 0);
            }

            public override void Flush()
            {
                _baseStream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _baseStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _baseStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _baseStream.SetLength(value);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _sha256?.Dispose();
                    _baseStream?.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}