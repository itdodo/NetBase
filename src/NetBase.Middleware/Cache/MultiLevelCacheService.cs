using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using NetBase.Common.Cache;

namespace NetBase.Middleware.Cache;

/// <summary>
/// 双层缓存实现：L1 进程内内存（~100ns）→ L2 Redis 分布式（~1ms）→ 数据库。
/// L2 未命中时回填 L1；L1 写入时同步写 L2（写穿透模式）。
/// L2 一律走异步 API（IDistributedCache 同步方法会阻塞线程池，高并发下线程饥饿级联超时——压测实证）。
/// 需要 CacheOptions.Provider=MultiLevel 且配置 Cache:RedisConnectionString。
/// </summary>
public class MultiLevelCacheService : ICacheService
{
    private readonly IMemoryCache _l1;
    private readonly IDistributedCache _l2;
    private readonly string _keyPrefix;
    private static readonly TimeSpan L1Ttl = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public MultiLevelCacheService(IMemoryCache l1, IDistributedCache l2, CacheOptions options)
    {
        _l1 = l1;
        _l2 = l2;
        _keyPrefix = options.RedisKeyPrefix;
    }

    private string FullKey(string key) => $"{_keyPrefix}{key}";

    public async Task<T?> GetAsync<T>(string key)
    {
        var fk = FullKey(key);
        // L1
        if (_l1.TryGetValue(fk, out T? l1Value))
        {
            CacheMetrics.Record(hit: true);
            return l1Value;
        }
        // L2
        var bytes = await _l2.GetAsync(fk);
        if (bytes == null)
        {
            CacheMetrics.Record(hit: false);
            return default;
        }
        var value = JsonSerializer.Deserialize<T>(bytes, JsonOpts);
        if (value != null) _l1.Set(fk, value, L1Ttl);
        CacheMetrics.Record(hit: true);
        return value;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var fk = FullKey(key);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOpts);
        // L2 写穿透（DistributedCache 默认滑动过期不可配，用固定 10 分钟）
        await _l2.SetAsync(fk, bytes, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(10)
        });
        // L1
        _l1.Set(fk, value, expiry ?? L1Ttl);
    }

    public async Task RemoveAsync(string key)
    {
        var fk = FullKey(key);
        _l1.Remove(fk);
        await _l2.RemoveAsync(fk);
    }

    public Task<bool> ExistsAsync(string key) => GetAsync<object>(key).ContinueWith(t => t.Result != null);
}
