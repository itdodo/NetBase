using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NetBase.Common.Cache;
using NetBase.Middleware.Cache;
using NetBase.Middleware.Mq;

namespace NetBase.Middleware;

/// <summary>
/// 中间件层服务注册：缓存（默认 Memory，可切 Redis）与 RabbitMQ（默认关闭）。
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNetBaseMiddleware(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        var cacheOptions = configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new CacheOptions();
        if (cacheOptions.Provider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
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
