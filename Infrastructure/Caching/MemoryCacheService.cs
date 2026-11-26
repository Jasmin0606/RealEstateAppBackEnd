using Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Caching;

/// <summary>
/// In-memory cache for Phase 1.
/// Swap this for RedisCacheService later — zero changes to consuming code.
/// Cache keys always include countryCode to prevent cross-country cache bleed.
/// Example keys:
///   "properties:GB:search:camden:2bed:500000"
///   "property:GB:detail:{id}"
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache) => _cache = cache;

    public Task<T?> GetAsync<T>(string key)
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan expiry)
    {
        _cache.Set(key, value, expiry);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix)
    {
        // IMemoryCache doesn't support prefix removal natively
        // Use a tracking set — or switch to Redis when needed
        // For now this is a no-op placeholder
        return Task.CompletedTask;
    }
}


