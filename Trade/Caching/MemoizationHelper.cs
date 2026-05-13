using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Trade.Caching
{
    /// <summary>
    /// Runtime helper that can be called from manually-placed wrappers or from IL-weaved advice.
    /// Handles sync returns, Task, and Task&lt;T&gt;.
    /// Adds support for disk-backed memoization (timed) when settings.EnableDiskBacked is true.
    /// </summary>
    internal static class MemoizationHelper
    {
        public static object InvokeWithCache(object instance, MethodBase method, object[] args, Func<object[], object> target, MemoizeAttribute settings)
        {
            if (settings is IMemoizeAdvanced adv && adv.EnableDiskBacked)
            {
                return DiskBackedMemoization.Invoke(instance, method, args, target, settings);
            }

            var key = CacheKeyBuilder.BuildKey(method, instance, args);

            if (SlidingCache.TryGet(settings.CacheName, key, out var cached))
                return cached;

            var result = target(args);

            // If Task<T>, store the T after completion and return a Task<T> that flows value through
            if (result is Task t)
            {
                var type = result.GetType();
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    return CacheTaskGeneric(settings, key, result);
                }

                // Plain Task: cache the task instance to de-dup concurrent callers
                SlidingCache.Set(settings.CacheName, key, result, settings.SlidingSeconds, settings.MaxItems);
                return result;
            }

            // Sync: cache immediately
            SlidingCache.Set(settings.CacheName, key, result, settings.SlidingSeconds, settings.MaxItems);
            return result;
        }

        private static object CacheTaskGeneric(MemoizeAttribute settings, string key, object taskObj)
        {
            var tType = taskObj.GetType().GetGenericArguments()[0]; // T
            var m = typeof(MemoizationHelper).GetMethod(nameof(CacheTaskCore), BindingFlags.NonPublic | BindingFlags.Static);
            var gm = m.MakeGenericMethod(tType);
            return gm.Invoke(null, new object[] { settings, key, taskObj });
        }

        private static async Task<T> CacheTaskCore<T>(MemoizeAttribute settings, string key, Task<T> task)
        {
            var val = await task.ConfigureAwait(false);
            // Store a completed Task<T> so subsequent retrieval returns a Task<T>, matching caller expectations
            SlidingCache.Set(settings.CacheName, key, Task.FromResult(val), settings.SlidingSeconds, settings.MaxItems);
            return val;
        }
    }

    /// <summary>
    /// Optional advanced settings mixin for [Memoize].
    /// </summary>
    internal interface IMemoizeAdvanced
    {
        bool EnableDiskBacked { get; }
    }
}
