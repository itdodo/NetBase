using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NetBase.Common.Cache;
using NetBase.Middleware.Cache;
using NetBase.Middleware.Mq;

namespace NetBase.Middleware;

/// <summary>
/// 中间件层服务注册：缓存（默认 Memory，可切 Redis 或 Hybrid 多级）与 RabbitMQ（默认关闭）。
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNetBaseMiddleware(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        var cacheOptions = configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new CacheOptions();
        if (cacheOptions.Provider.Equals("MultiLevel", StringComparison.OrdinalIgnoreCase))
        {
            // MultiLevel 双层缓存：L1 进程内内存 + L2 Redis 分布式（写穿透模式）
            services.AddMemoryCache();
            services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration = cacheOptions.RedisConnectionString;
                o.InstanceName = cacheOptions.RedisKeyPrefix;
            });
            services.TryAddSingleton<IDistributedCache>(sp =>
                sp.GetRequiredService<IDistributedCache>());
            services.TryAddSingleton<ICacheService>(sp =>
            {
                var l1 = sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
                var l2 = sp.GetRequiredService<IDistributedCache>();
                return new MultiLevelCacheService(l1, l2, cacheOptions);
            });
        }
        else if (cacheOptions.Provider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton<ICacheService>(sp => new RedisCacheService(cacheOptions));
        }
        else
        {
            services.AddMemoryCache();
            services.TryAddSingleton<ICacheService, MemoryCacheService>();
        }

        var mqOptions = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>() ?? new RabbitMqOptions();
        if (mqOptions.Enabled)
        {
            services.TryAddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
        }

        return services;
    }
}
