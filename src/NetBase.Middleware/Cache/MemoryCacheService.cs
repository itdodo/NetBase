using Microsoft.Extensions.Caching.Memory;
using NetBase.Common.Cache;

namespace NetBase.Middleware.Cache;

/// <summary>
/// 进程内缓存实现（默认）。单机部署或开发环境使用。
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;

    public MemoryCacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public T? Get<T>(string key) => _memoryCache.TryGetValue(key, out T? value) ? value : default;

    public void Set<T>(string key, T value, TimeSpan? expiry = null) =>
        _memoryCache.Set(key, value, expiry.HasValue
            ? new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry }
            : new MemoryCacheEntryOptions());

    public void Remove(string key) => _memoryCache.Remove(key);

    public bool Exists(string key) => _memoryCache.TryGetValue(key, out _);
}
