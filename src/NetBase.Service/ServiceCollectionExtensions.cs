using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NetBase.Service.Base;
using NetBase.Service.Sys;
using NetBase.Service.Sys.Flow;

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
        services.TryAddScoped<NetBase.Common.Email.IEmailService, EmailService>();
        services.TryAddScoped<GenMetaService>();
        services.TryAddScoped<SysGenTableService>();

        // 审批流引擎（业务单据经 IFlowBusinessHandler + FlowEngine.SubmitAsync 接入）
        services.TryAddScoped<ApproverResolver>();
        services.TryAddScoped<IFlowEngine, FlowEngine>();
        services.TryAddScoped<ISysFlowDefinitionService, SysFlowDefinitionService>();
        services.TryAddScoped<ISysFlowBindingService, SysFlowBindingService>();
        services.TryAddScoped<ISysFlowDelegateService, SysFlowDelegateService>();
        services.TryAddScoped<ISysPositionService, SysPositionService>();
        services.TryAddScoped<IFlowQueryService, SysFlowQueryService>();
        // 业务模块的 IFlowBusinessHandler 实现由业务侧扩展方法注册（见 Biz/AddNetBaseBiz）
        // Lazy.Captcha.Core：验证码生成（内存存储默认；分布式部署可换其 Redis 存储）
        services.AddCaptcha(options =>
        {
            options.CodeLength = 4;
            options.ImageOption.FontSize = 26;
            // 内部系统：去掉干扰线与气泡噪点，验证码清晰易读（防机器识别由限流/锁定兜底）
            options.ImageOption.InterferenceLineCount = 0;
            options.ImageOption.BubbleCount = 0;
        });
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
