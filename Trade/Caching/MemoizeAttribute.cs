using System;

namespace Trade.Caching
{
    /// <summary>
    /// Decorate a method to indicate its result can be memoized (cached) using a sliding window.
    /// IL weaving or manual plumbing can use this metadata to enable caching.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class MemoizeAttribute : Attribute, IMemoizeAdvanced
    {
        /// <summary>
        /// Sliding expiration in seconds. Entry stays alive while accessed within this window.
        /// </summary>
        public int SlidingSeconds { get; set; } = 60;

        /// <summary>
        /// Optional approximate max items for this cache name. If exceeded, the cache will trim.
        /// 0 or negative means unlimited (subject to global memory pressure).
        /// </summary>
        public int MaxItems { get; set; } = 10000;

        /// <summary>
        /// Named cache partition. Same name -> shared cache bucket and policies.
        /// </summary>
        public string CacheName { get; set; } = "default";

        /// <summary>
        /// If true, the first invocation will time compute vs serialize+disk round-trip. If disk is faster,
        /// the entry is pinned in-memory and persisted to disk; subsequent calls can hydrate from disk quickly.
        /// </summary>
        public bool EnableDiskBacked { get; set; } = true;
    }

    /// <summary>
    /// Mark a parameter or property to be ignored in cache-key generation.
    /// Useful for CancellationToken, IProgress, etc.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public sealed class CacheIgnoreAttribute : Attribute { }

    /// <summary>
    /// Mark a parameter or property to be included verbosely in cache-key generation
    /// (even if null or default). Without this, some properties might be skipped by
    /// custom serializers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
    public sealed class CacheIncludeAttribute : Attribute { }

    /// <summary>
    /// If a complex type implements this, it can write a deterministic representation
    /// of itself that will be hashed to contribute to the cache key.
    /// </summary>
    public interface ICacheKey
    {
        void WriteCacheKey(System.Text.Json.Utf8JsonWriter writer);
    }
}
