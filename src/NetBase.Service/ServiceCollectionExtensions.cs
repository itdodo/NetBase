using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NetBase.Service.Base;
using NetBase.Service.Sys;

namespace NetBase.Service;

/// <summary>业务逻辑层服务注册</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNetBaseService(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // 泛型服务开放注册
        services.TryAddScoped(typeof(IBaseService<>), typeof(BaseService<>));

        // 业务服务：新增业务模块时在此追加
        services.TryAddScoped<ISysUserService, SysUserService>();
        services.TryAddScoped<ISysRoleService, SysRoleService>();
        services.TryAddScoped<ISysMenuService, SysMenuService>();
        services.TryAddScoped<ISysLogService, SysLogService>();
        services.TryAddScoped<ISysDictService, SysDictService>();
        services.TryAddScoped<ISysConfigService, SysConfigService>();
        services.TryAddScoped<ISysNoticeService, SysNoticeService>();
        services.TryAddScoped<ISysDeptService, SysDeptService>();
        services.TryAddScoped<IDataScopeService, DataScopeService>();
        services.TryAddScoped<ISysMessageService, SysMessageService>();
        services.TryAddScoped<IDashboardService, DashboardService>();
        services.TryAddScoped<ISystemMonitorService, SystemMonitorService>();
        services.TryAddScoped<ISysFileService, SysFileService>();
        services.TryAddScoped<ICaptchaService, CaptchaService>();

        // 认证授权
        if (configuration != null)
        {
            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        }
        services.TryAddScoped<IPermissionService, PermissionService>();
        services.TryAddScoped<ISysAuthService, SysAuthService>();

        return services;
    }
}
