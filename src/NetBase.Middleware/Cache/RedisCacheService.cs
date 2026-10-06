using NetBase.Common.Cache;
using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using StackExchange.Redis;

namespace NetBase.Middleware.Cache;

/// <summary>
/// Redis 缓存实现（备用方案）。Configuration:Cache:Provider=Redis 时启用。
/// 采用 Lazy 懒连接，Redis 不可用不影响启动；值以 JSON 序列化存储。
/// 全异步访问（同步 API 阻塞线程池，高并发下线程饥饿级联超时——压测实证）。
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly Lazy<ConnectionMultiplexer> _connection;
    private readonly string _keyPrefix;

    public RedisCacheService(CacheOptions options)
    {
        if (options.RedisConnectionString.IsNullOrWhiteSpace())
        {
            throw new BusinessException("Cache:Provider=Redis 时必须配置 Cache:RedisConnectionString");
        }

        _keyPrefix = options.RedisKeyPrefix;
        _connection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(options.RedisConnectionString));
    }

    private IDatabase Db => _connection.Value.GetDatabase();

    private string FullKey(string key) => $"{_keyPrefix}{key}";

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await Db.StringGetAsync(FullKey(key));
        return value.IsNull ? default : ((string)value!).FromJson<T>();
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        // StackExchange.Redis 3.x：过期时间通过 Expiration 结构传递，Default 表示永不过期
        var expiration = expiry.HasValue ? new Expiration(expiry.Value) : Expiration.Default;
        await Db.StringSetAsync(FullKey(key), value.ToJson(), expiration);
    }

    public Task RemoveAsync(string key) => Db.KeyDeleteAsync(FullKey(key));

    public Task<bool> ExistsAsync(string key) => Db.KeyExistsAsync(FullKey(key));

    /// <summary>Redis 诊断指标（memory/stats），供系统监控页展示</summary>
    public Task<object?> GetRedisDiagnosticsAsync()
    {
        try
        {
            var server = _connection.Value.GetServers().First();
            var dict = new Dictionary<string, object?>();
            // EP 2.x：Info 返回按节分组的 IGrouping 数组（memory/stats 各一节）
            foreach (var section in server.Info("memory").Concat(server.Info("stats")))
            {
                foreach (var pair in section)
                {
                    dict[pair.Key] = pair.Value;
                }
            }
            dict["dbSize"] = (long)Db.Execute("dbsize");
            return Task.FromResult<object?>(dict);
        }
        catch (Exception)
        {
            return Task.FromResult<object?>(null);
        }
    }
}
