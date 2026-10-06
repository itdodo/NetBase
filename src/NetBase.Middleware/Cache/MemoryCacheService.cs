using Microsoft.Extensions.Caching.Memory;
using NetBase.Common.Cache;

namespace NetBase.Middleware.Cache;

/// <summary>
/// 进程内缓存实现（默认）。单机部署或开发环境使用。
/// 内存操作本身非阻塞，异步方法为直接封装。
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;

    public MemoryCacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public Task<T?> GetAsync<T>(string key) =>
        Task.FromResult(_memoryCache.TryGetValue(key, out T? value) ? value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) =>
        Task.FromResult(_memoryCache.Set(key, value, expiry.HasValue
            ? new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry }
            : new MemoryCacheEntryOptions()));

    public Task RemoveAsync(string key)
    {
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key) =>
        Task.FromResult(_memoryCache.TryGetValue(key, out _));
}
