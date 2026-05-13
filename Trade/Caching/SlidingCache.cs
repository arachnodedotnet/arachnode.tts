using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Trade.Caching
{
    /// <summary>
    /// Sliding-window in-memory cache with named buckets. No external dependencies.
    /// Thread-safe, approximate LRU trim, periodic expiry sweep.
    /// Supports pinning entries to avoid eviction/expiry.
    /// </summary>
    internal static class SlidingCache
    {
        private sealed class CacheEntry
        {
            public object Value;
            public int SlidingSeconds;
            public long LastAccessTicks;   // for expiry
            public long LastAccessOrder;   // for deterministic trimming
            public bool IsPinned;
        }

        private sealed class CacheBucket : IDisposable
        {
            public readonly string Name;
            public readonly ConcurrentDictionary<string, CacheEntry> Entries = new ConcurrentDictionary<string, CacheEntry>(StringComparer.Ordinal);
            public readonly int MaxItems;
            private readonly Timer _sweeper;
            private long _seq; // per-bucket sequence for deterministic ordering

            public CacheBucket(string name, int maxItems)
            {
                Name = name;
                MaxItems = maxItems;
                // Sweep every 30 seconds
                _sweeper = new Timer(_ => Sweep(), null, dueTime: 30000, period: 30000);
            }

            public void Dispose()
            {
                _sweeper.Dispose();
            }

            private void Touch(CacheEntry e)
            {
                e.LastAccessTicks = DateTime.UtcNow.Ticks;
                e.LastAccessOrder = Interlocked.Increment(ref _seq);
            }

            private void Sweep()
            {
                var now = DateTime.UtcNow.Ticks;
                foreach (var kvp in Entries)
                {
                    var e = kvp.Value;
                    if (e.IsPinned) continue; // do not expire pinned entries
                    var ageSeconds = (now - e.LastAccessTicks) / TimeSpan.TicksPerSecond;
                    if (ageSeconds >= e.SlidingSeconds)
                    {
                        CacheEntry removed;
                        Entries.TryRemove(kvp.Key, out removed);
                    }
                }

                TrimIfNeeded();
            }

            private void TrimIfNeeded()
            {
                if (MaxItems > 0 && Entries.Count > MaxItems)
                {
                    // Remove oldest non-pinned entries until under cap (by access order)
                    var excess = Entries.Count - MaxItems;
                    var candidates = Entries
                        .Where(kvp => !kvp.Value.IsPinned)
                        .OrderBy(kvp => kvp.Value.LastAccessOrder)
                        .Take(Math.Max(1, excess))
                        .Select(kvp => kvp.Key)
                        .ToList();
                    foreach (var key in candidates)
                    {
                        CacheEntry removed;
                        Entries.TryRemove(key, out removed);
                        if (Entries.Count <= MaxItems) break;
                    }
                }
            }

            public object GetOrAdd(string key, Func<object> factory, int slidingSeconds, bool pinned)
            {
                CacheEntry existing;
                if (Entries.TryGetValue(key, out existing))
                {
                    Touch(existing);
                    return existing.Value;
                }

                var value = factory();
                var entry = new CacheEntry
                {
                    Value = value,
                    SlidingSeconds = Math.Max(1, slidingSeconds),
                    IsPinned = pinned
                };
                Touch(entry);
                Entries[key] = entry;

                TrimIfNeeded();
                return value;
            }

            public bool TryGet(string key, out object value)
            {
                CacheEntry entry;
                if (Entries.TryGetValue(key, out entry))
                {
                    Touch(entry);
                    value = entry.Value;
                    return true;
                }
                value = null;
                return false;
            }

            public void Set(string key, object value, int slidingSeconds, bool pinned)
            {
                var entry = new CacheEntry
                {
                    Value = value,
                    SlidingSeconds = Math.Max(1, slidingSeconds),
                    IsPinned = pinned
                };
                Touch(entry);
                Entries[key] = entry;
                TrimIfNeeded();
            }
        }

        private static readonly ConcurrentDictionary<string, CacheBucket> Buckets = new ConcurrentDictionary<string, CacheBucket>(StringComparer.Ordinal);

        public static object GetOrAdd(string cacheName, string key, Func<object> factory, int slidingSeconds, int maxItems, bool pinned = false)
        {
            var bucket = Buckets.GetOrAdd(cacheName, n => new CacheBucket(n, maxItems));
            return bucket.GetOrAdd(key, factory, slidingSeconds, pinned);
        }

        public static bool TryGet(string cacheName, string key, out object value)
        {
            CacheBucket bucket;
            if (Buckets.TryGetValue(cacheName, out bucket))
            {
                return bucket.TryGet(key, out value);
            }
            value = null;
            return false;
        }

        public static void Set(string cacheName, string key, object value, int slidingSeconds, int maxItems, bool pinned = false)
        {
            var bucket = Buckets.GetOrAdd(cacheName, n => new CacheBucket(n, maxItems));
            bucket.Set(key, value, slidingSeconds, pinned);
        }

        public static void SetPinned(string cacheName, string key, bool pinned)
        {
            CacheBucket bucket;
            if (Buckets.TryGetValue(cacheName, out bucket))
            {
                CacheEntry entry;
                if (bucket.Entries.TryGetValue(key, out entry))
                {
                    entry.IsPinned = pinned;
                }
            }
        }
    }
}
