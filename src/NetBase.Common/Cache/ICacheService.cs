namespace NetBase.Common.Cache;

/// <summary>
/// 缓存抽象接口。默认使用进程内缓存实现，可切换为 Redis 实现（见 NetBase.Middleware）。
/// </summary>
public interface ICacheService
{
    /// <summary>读取缓存，不存在或类型不匹配返回默认值</summary>
    T? Get<T>(string key);

    /// <summary>写入缓存，expiry 为空表示不过期</summary>
    void Set<T>(string key, T value, TimeSpan? expiry = null);

    /// <summary>移除指定缓存</summary>
    void Remove(string key);

    /// <summary>缓存键是否存在</summary>
    bool Exists(string key);

    Task<T?> GetAsync<T>(string key)
        => Task.FromResult(Get<T>(key));

    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        Set(key, value, expiry);
        return Task.CompletedTask;
    }

    Task RemoveAsync(string key)
    {
        Remove(key);
        return Task.CompletedTask;
    }
}
