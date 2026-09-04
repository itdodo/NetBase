using Microsoft.Extensions.Options;
using NetBase.Common.Cache;

namespace NetBase.Middleware.Cache;

/// <summary>缓存配置（appsettings.json 的 Cache 节点）</summary>
public class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>Memory 或 Redis</summary>
    public string Provider { get; set; } = "Memory";

    /// <summary>Redis 连接串，Provider=Redis 时必填</summary>
    public string RedisConnectionString { get; set; } = string.Empty;

    /// <summary>Redis 键前缀，避免多项目共用 Redis 冲突</summary>
    public string RedisKeyPrefix { get; set; } = "netbase:";
}
