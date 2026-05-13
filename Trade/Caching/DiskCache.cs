using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Trade.Caching
{
    /// <summary>
    /// Simple disk cache for serialized objects keyed by a string. Intended for large results
    /// where deserialize+disk round-trip may be faster than recomputation.
    /// Uses BinaryFormatter for .NET Framework compatibility (only for internal types you control).
    /// </summary>
    internal static class DiskCache
    {
        private static readonly object _sync = new object();
        private static string _root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");

        public static void ConfigureRoot(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath)) return;
            _root = rootPath;
        }

        private static string GetPath(string cacheName, string key)
        {
            var safeName = MakeSafe(cacheName);
            var safeKey = MakeSafe(key);
            var dir = Path.Combine(_root, safeName);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, safeKey + ".bin");
        }

        private static string MakeSafe(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            foreach (var c in Path.GetInvalidPathChars()) s = s.Replace(c, '_');
            return s.Length > 180 ? s.Substring(0, 180) : s;
        }

        public static bool TryRead(string cacheName, string key, out object value)
        {
            var path = GetPath(cacheName, key);
            if (!File.Exists(path)) { value = null; return false; }
            lock (_sync)
            {
                using (var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bf = new BinaryFormatter();
#pragma warning disable SYSLIB0011
                    value = bf.Deserialize(fs);
#pragma warning restore SYSLIB0011
                    return true;
                }
            }
        }

        public static void Write(string cacheName, string key, object value)
        {
            var path = GetPath(cacheName, key);
            lock (_sync)
            {
                using (var fs = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var bf = new BinaryFormatter();
#pragma warning disable SYSLIB0011
                    bf.Serialize(fs, value);
#pragma warning restore SYSLIB0011
                }
            }
        }
    }
}
