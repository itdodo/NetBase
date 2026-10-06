namespace NetBase.Common.Cache;

/// <summary>
/// 缓存抽象接口（全异步）。L2 为 Redis 时同步调用会阻塞线程池——高并发下线程饥饿引发全站级联超时（压测实证），
/// 因此接口只提供异步方法。默认使用进程内缓存实现，可切换 Redis / MultiLevel 双层实现。
/// </summary>
public interface ICacheService
{
    /// <summary>读取缓存，不存在或类型不匹配返回默认值</summary>
    Task<T?> GetAsync<T>(string key);

    /// <summary>写入缓存，expiry 为空表示不过期</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);

    /// <summary>移除指定缓存</summary>
    Task RemoveAsync(string key);

    /// <summary>缓存键是否存在</summary>
    Task<bool> ExistsAsync(string key);
}
