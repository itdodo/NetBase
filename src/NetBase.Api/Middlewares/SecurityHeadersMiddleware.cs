namespace NetBase.Api.Middlewares;

/// <summary>
/// 安全响应头中间件：基础安全加固（防 MIME 嗅探、防点击劫持、限制引用泄露）。
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-XSS-Protection"] = "0";
            // 允许同源与内联样式（Element Plus 需要），限制外部脚本
            headers["Content-Security-Policy"] = "frame-ancestors 'none'; base-uri 'self'";
            return Task.CompletedTask;
        });

        await next(context);
    }
}
