using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Trade.Caching
{
    /// <summary>
    /// Deterministically builds a cache key from a method, instance, and arguments.
    /// Produces a compact string: Type::Method#HEX_SHA256(json(args))
    /// </summary>
    internal static class CacheKeyBuilder
    {
        private static readonly JsonWriterOptions JsonWriterOptions = new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        };

        public static string BuildKey(MethodBase method, object instance, object[] args)
        {
            var sb = new StringBuilder(128);
            sb.Append(method.DeclaringType != null ? method.DeclaringType.FullName : "<null>")
              .Append("::")
              .Append(method.Name);

            using (var sha = SHA256.Create())
            using (var buffer = new PooledBufferWriter())
            {
                using (var writer = new Utf8JsonWriter(buffer, JsonWriterOptions))
                {
                    writer.WriteStartArray();
                    writer.WriteStringValue(instance != null ? instance.GetType().FullName : "static");

                    var pinfos = method.GetParameters();
                    for (int i = 0; i < args.Length; i++)
                    {
                        var p = pinfos.Length > i ? pinfos[i] : null;
                        if (p != null)
                        {
                            var ignore = p.GetCustomAttributes(typeof(CacheIgnoreAttribute), false).Length > 0;
                            if (ignore)
                            {
                                writer.WriteStringValue("__ignored:" + p.Position);
                                continue;
                            }
                        }

                        var includeAllProps = p != null && p.GetCustomAttributes(typeof(CacheIncludeAttribute), false).Length > 0;
                        WriteValue(writer, args[i], includeAllProps);
                    }

                    writer.WriteEndArray();
                }

                var data = buffer.ToArray();
                var hash = sha.ComputeHash(data);
                sb.Append('#').Append(ToHex(hash));
            }

            return sb.ToString();
        }

        private static void WriteValue(Utf8JsonWriter w, object value, bool includeAllProps)
        {
            if (value == null) { w.WriteNullValue(); return; }

            // Primitive fast-paths
            if (value is string)
            {
                w.WriteStringValue((string)value);
                return;
            }
            if (value is int)
            {
                w.WriteNumberValue((int)value);
                return;
            }
            if (value is long)
            {
                w.WriteNumberValue((long)value);
                return;
            }
            if (value is short)
            {
                w.WriteNumberValue((short)value);
                return;
            }
            if (value is byte)
            {
                w.WriteNumberValue((byte)value);
                return;
            }
            if (value is double)
            {
                var d = (double)value;
                if (!(double.IsNaN(d) || double.IsInfinity(d))) w.WriteNumberValue(d); else w.WriteStringValue(d.ToString());
                return;
            }
            if (value is float)
            {
                var f = (float)value;
                if (!(float.IsNaN(f) || float.IsInfinity(f))) w.WriteNumberValue(f); else w.WriteStringValue(f.ToString());
                return;
            }
            if (value is decimal)
            {
                w.WriteNumberValue((decimal)value);
                return;
            }
            if (value is bool)
            {
                w.WriteBooleanValue((bool)value);
                return;
            }
            if (value is DateTime)
            {
                var dt = (DateTime)value;
                w.WriteStringValue(dt.ToUniversalTime());
                return;
            }
            if (value is DateTimeOffset)
            {
                var dto = (DateTimeOffset)value;
                w.WriteStringValue(dto.ToUniversalTime().UtcDateTime);
                return;
            }
            if (value is Guid)
            {
                w.WriteStringValue(((Guid)value).ToString());
                return;
            }

            var type = value.GetType();
            if (type.IsEnum)
            {
                w.WriteStringValue(type.FullName + ":" + Convert.ToInt64(value));
                return;
            }

            // If type supplies deterministic key
            var asKey = value as ICacheKey;
            if (asKey != null)
            {
                w.WriteStartObject();
                asKey.WriteCacheKey(w);
                w.WriteEndObject();
                return;
            }

            // IDictionary
            var asDict = value as IDictionary;
            if (asDict != null)
            {
                var keys = new object[asDict.Count];
                asDict.Keys.CopyTo(keys, 0);
                Array.Sort(keys, Comparer<object>.Create((a, b) => string.Compare(a == null ? null : a.ToString(), b == null ? null : b.ToString(), StringComparison.Ordinal)));
                w.WriteStartArray();
                foreach (var k in keys)
                {
                    w.WriteStartArray();
                    WriteValue(w, k, includeAllProps);
                    WriteValue(w, asDict[k], includeAllProps);
                    w.WriteEndArray();
                }
                w.WriteEndArray();
                return;
            }

            // IEnumerable (but not string, handled above)
            var asEnum = value as IEnumerable;
            if (asEnum != null && !(value is string))
            {
                w.WriteStartArray();
                foreach (var item in asEnum) WriteValue(w, item, includeAllProps);
                w.WriteEndArray();
                return;
            }

            // Fallback: serialize public properties in stable order
            var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                            .Where(p => includeAllProps || p.GetCustomAttributes(typeof(CacheIgnoreAttribute), false).Length == 0)
                            .OrderBy(p => p.Name, StringComparer.Ordinal)
                            .ToArray();

            w.WriteStartObject();
            w.WriteString("~type", type.FullName);
            for (int i = 0; i < props.Length; i++)
            {
                var p = props[i];
                w.WritePropertyName(p.Name);
                WriteValue(w, p.GetValue(value, null), includeAllProps: false);
            }
            w.WriteEndObject();
        }

        private static string ToHex(byte[] bytes)
        {
            var chars = new char[bytes.Length * 2];
            int ci = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                chars[ci++] = NibbleToHex((b >> 4) & 0xF);
                chars[ci++] = NibbleToHex(b & 0xF);
            }
            return new string(chars);
        }

        private static char NibbleToHex(int n)
        {
            return (char)(n < 10 ? ('0' + n) : ('A' + (n - 10)));
        }

        private sealed class PooledBufferWriter : IBufferWriter<byte>, IDisposable
        {
            private byte[] _buffer = ArrayPool<byte>.Shared.Rent(4096);
            private int _written;

            public void Advance(int count) { _written += count; }
            public Memory<byte> GetMemory(int sizeHint = 0) { Ensure(sizeHint); return _buffer.AsMemory(_written); }
            public Span<byte> GetSpan(int sizeHint = 0) { Ensure(sizeHint); return _buffer.AsSpan(_written); }

            private void Ensure(int sizeHint)
            {
                if (sizeHint == 0) sizeHint = 256;
                if (_buffer.Length - _written < sizeHint)
                {
                    var newBuf = ArrayPool<byte>.Shared.Rent(Math.Max(_buffer.Length * 2, _written + sizeHint));
                    Array.Copy(_buffer, 0, newBuf, 0, _written);
                    ArrayPool<byte>.Shared.Return(_buffer);
                    _buffer = newBuf;
                }
            }

            public byte[] ToArray()
            {
                var arr = new byte[_written];
                Array.Copy(_buffer, 0, arr, 0, _written);
                return arr;
            }

            public void Dispose()
            {
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = new byte[0];
            }
        }
    }
}
