using System.Net;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace NetBase.Api.Jobs;

/// <summary>
/// Hangfire Dashboard 本地授权过滤器：默认仅允许本机访问（开发调试用）。
/// 管理操作请使用系统管理页（受 monitor:job 权限码控制）。
/// </summary>
public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.Connection.RemoteIpAddress is { } ip
            && (IPAddress.IsLoopback(ip));
    }
}
