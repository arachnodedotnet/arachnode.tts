using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace Trade.Caching
{
    /// <summary>
    /// Adds disk-backed memoization: on first computation, times the compute path and
    /// the serialize+disk round-trip. If disk path is faster, pins in-memory cache and
    /// writes to disk. On subsequent calls, returns from cache or disk.
    /// </summary>
    internal static class DiskBackedMemoization
    {
        public static object Invoke(object instance, MethodBase method, object[] args, Func<object[], object> target, MemoizeAttribute settings)
        {
            var key = CacheKeyBuilder.BuildKey(method, instance, args);

            // Fast path: in-memory cache hit
            if (SlidingCache.TryGet(settings.CacheName, key, out var cached))
                return cached;

            // Disk path attempt first to avoid recompute when persisted
            object diskVal;
            if (DiskCache.TryRead(settings.CacheName, key, out diskVal))
            {
                // store in memory and return
                SlidingCache.Set(settings.CacheName, key, diskVal, settings.SlidingSeconds, settings.MaxItems, pinned: true);
                return diskVal;
            }

            // First-time compute, measure
            var swCompute = Stopwatch.StartNew();
            var result = target(args);

            // Task<T> handling: materialize result before timing disk
            if (result is Task t)
            {
                var type = result.GetType();
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    return CacheTaskGeneric(settings, key, result, swCompute);
                }

                // For Task (non-generic), don't persist; cache task for de-dupe
                SlidingCache.Set(settings.CacheName, key, result, settings.SlidingSeconds, settings.MaxItems);
                return result;
            }

            swCompute.Stop();
            
            DiskCache.Write(settings.CacheName, key, result);
            // Measure serialize+disk round-trip
            var swDisk = Stopwatch.StartNew();
            object readBack;
            DiskCache.TryRead(settings.CacheName, key, out readBack);
            swDisk.Stop();

            var diskIsFaster = swDisk.ElapsedTicks < swCompute.ElapsedTicks;
            SlidingCache.Set(settings.CacheName, key, result, settings.SlidingSeconds, settings.MaxItems, pinned: diskIsFaster);

            return result;
        }

        private static object CacheTaskGeneric(MemoizeAttribute settings, string key, object taskObj, Stopwatch swCompute)
        {
            var tType = taskObj.GetType().GetGenericArguments()[0];
            var m = typeof(DiskBackedMemoization).GetMethod(nameof(CacheTaskCore), BindingFlags.NonPublic | BindingFlags.Static);
            var gm = m.MakeGenericMethod(tType);
            return gm.Invoke(null, new object[] { settings, key, taskObj, swCompute });
        }

        private static async Task<T> CacheTaskCore<T>(MemoizeAttribute settings, string key, Task<T> task, Stopwatch swCompute)
        {
            var val = await task.ConfigureAwait(false);
            swCompute.Stop();

            // Measure disk round-trip
            var swDisk = Stopwatch.StartNew();
            DiskCache.Write(settings.CacheName, key, val);
            object readBack;
            DiskCache.TryRead(settings.CacheName, key, out readBack);
            swDisk.Stop();

            var diskIsFaster = swDisk.ElapsedTicks < swCompute.ElapsedTicks;
            SlidingCache.Set(settings.CacheName, key, val, settings.SlidingSeconds, settings.MaxItems, pinned: diskIsFaster);
            return val;
        }
    }
}
