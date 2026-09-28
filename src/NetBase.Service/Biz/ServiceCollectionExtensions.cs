using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NetBase.Service.Biz;
using NetBase.Service.Sys.Flow;

namespace NetBase.Service;

/// <summary>业务模块服务注册（业务代码只进 Biz 目录，框架注册文件不掺业务）</summary>
public static class BizServiceCollectionExtensions
{
    public static IServiceCollection AddNetBaseBiz(this IServiceCollection services)
    {
        // 业务服务
        services.TryAddScoped<IBizExpenseService, BizExpenseService>();
        services.TryAddScoped<IBizPurchaseRequestService, BizPurchaseRequestService>();

        // 审批流业务回调（FlowEngine 经 IEnumerable<IFlowBusinessHandler> 发现；
        // 多实现必须用 Add——TryAdd 同类型只收第一个）
        services.AddScoped<IFlowBusinessHandler, ExpenseFlowHandler>();
        services.AddScoped<IFlowBusinessHandler, PurchaseRequestFlowHandler>();

            services.TryAddScoped<IBizExpenseTestService, BizExpenseTestService>();
    return services;
    }
}
