using Microsoft.AspNetCore.Http;

namespace NetBase.Api.Extensions;

/// <summary>HttpContext 扩展</summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// 获取客户端真实 IP：反向代理场景优先取 X-Forwarded-For 第一段。
    /// 框架内所有 IP 记录（登录日志/操作日志/限流）统一走此方法。
    /// Docker 网桥场景来源为网关 IP（如 ::ffff:172.20.0.1），自动去 IPv6 前缀规范化。
    /// 生产环境部署在 Nginx 反向代理后时，Nginx 需设置 X-Forwarded-For 传递真实 IP。
    /// </summary>
    public static string? GetClientIp(this HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        var ip = context.Connection.RemoteIpAddress?.ToString();
        // IPv4-mapped IPv6 规范化：::ffff:192.168.1.1 → 192.168.1.1
        if (ip != null && ip.StartsWith("::ffff:"))
        {
            ip = ip["::ffff:".Length..];
        }
        return ip;
    }
}
