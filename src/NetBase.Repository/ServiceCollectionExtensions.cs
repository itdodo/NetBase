using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NetBase.Repository.DbContexts;
using NetBase.Repository.Repositories;

namespace NetBase.Repository;

/// <summary>数据访问层服务注册</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNetBaseRepository(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SqlSugarOptions>(configuration.GetSection(SqlSugarOptions.SectionName));
        var options = configuration.GetSection(SqlSugarOptions.SectionName).Get<SqlSugarOptions>() ?? new SqlSugarOptions();
        services.TryAddSingleton(options);
        services.TryAddSingleton<SqlSugarContext>();
        services.TryAddScoped<DbSeeder>();

        // 泛型仓储开放注册；SqlSugarScope 线程安全单例，仓储按请求注册
        services.TryAddScoped(typeof(IRepository<>), typeof(Repository<>));

        return services;
    }
}
