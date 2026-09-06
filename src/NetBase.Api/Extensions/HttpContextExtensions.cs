using Microsoft.AspNetCore.Http;

namespace NetBase.Api.Extensions;

/// <summary>HttpContext 扩展</summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// 获取客户端真实 IP：反向代理场景优先取 X-Forwarded-For 第一段。
    /// 框架内所有 IP 记录（登录日志/操作日志/限流）统一走此方法。
    /// </summary>
    public static string? GetClientIp(this HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }
        return context.Connection.RemoteIpAddress?.ToString();
    }
}
