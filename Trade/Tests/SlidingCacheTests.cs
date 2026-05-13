using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Trade.Caching;

namespace Trade.Tests
{
    [TestClass]
    public class SlidingCacheTests
    {
        private static string NewCacheName() => "SlidingCacheTests-" + Guid.NewGuid().ToString("N");

        [TestMethod][TestCategory("Core")]
        public void PinnedEntrySurvivesTrimming()
        {
            var cache = NewCacheName();
            int maxItems = 3;

            // Add pinned A
            SlidingCache.Set(cache, "A", 1, slidingSeconds: 300, maxItems: maxItems, pinned: true);

            // Add non-pinned B1..B3 with slight delays to ensure ordering
            SlidingCache.Set(cache, "B1", 1, 300, maxItems);
            Thread.Sleep(1);
            SlidingCache.Set(cache, "B2", 1, 300, maxItems);
            Thread.Sleep(1);
            SlidingCache.Set(cache, "B3", 1, 300, maxItems);

            // Adding B3 exceeds cap and should evict oldest non-pinned (B1). Pinned A must remain.
            object v;
            Assert.IsTrue(SlidingCache.TryGet(cache, "A", out v), "Pinned entry A should remain after trimming.");
            Assert.IsFalse(SlidingCache.TryGet(cache, "B1", out v), "Oldest non-pinned B1 should be evicted.");
            Assert.IsTrue(SlidingCache.TryGet(cache, "B2", out v), "Newer non-pinned B2 should remain.");
            Assert.IsTrue(SlidingCache.TryGet(cache, "B3", out v), "Newest non-pinned B3 should remain.");
        }

        [TestMethod][TestCategory("Core")]
        public void OldestNonPinnedEvictedFirst()
        {
            var cache = NewCacheName();
            int maxItems = 2;

            SlidingCache.Set(cache, "P", 1, 300, maxItems, pinned: true);
            SlidingCache.Set(cache, "X1", 1, 300, maxItems);
            Thread.Sleep(1);
            SlidingCache.Set(cache, "X2", 1, 300, maxItems); // triggers eviction: remove X1 (oldest non-pinned)

            object v;
            Assert.IsTrue(SlidingCache.TryGet(cache, "P", out v), "Pinned P must remain.");
            Assert.IsFalse(SlidingCache.TryGet(cache, "X1", out v), "Oldest non-pinned X1 should be evicted.");
            Assert.IsTrue(SlidingCache.TryGet(cache, "X2", out v), "Newer non-pinned X2 should remain.");
        }

        [TestMethod][TestCategory("Core")]
        public void SetPinnedPreventsFutureEviction()
        {
            var cache = NewCacheName();
            int maxItems = 2;

            SlidingCache.Set(cache, "Y1", 1, 300, maxItems); // non-pinned
            SlidingCache.SetPinned(cache, "Y1", true);       // pin it

            // Add another non-pinned; when exceeding cap, trimming should remove the other one
            Thread.Sleep(1);
            SlidingCache.Set(cache, "Y2", 1, 300, maxItems);
            Thread.Sleep(1);
            SlidingCache.Set(cache, "Y3", 1, 300, maxItems); // triggers eviction of Y2 (oldest non-pinned)

            object v;
            Assert.IsTrue(SlidingCache.TryGet(cache, "Y1", out v), "Pinned Y1 should remain after trimming.");
            Assert.IsFalse(SlidingCache.TryGet(cache, "Y2", out v), "Oldest non-pinned Y2 should be evicted.");
            Assert.IsTrue(SlidingCache.TryGet(cache, "Y3", out v), "Newest non-pinned Y3 should remain.");
        }
    }
}
