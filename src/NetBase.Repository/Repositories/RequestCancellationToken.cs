namespace NetBase.Repository.Repositories;

/// <summary>
/// 请求作用域的取消令牌提供者：DataScopeMiddleware（管线最早点）每请求写入
/// HttpContext.RequestAborted，仓储层 ct 缺省时自动回退取用——
/// 全站数据库操作无需改签名即响应客户端断开（中断请求立即释放连接池连接）。
/// 显式传入的 ct 优先。
/// </summary>
public static class RequestCancellationToken
{
    private static readonly AsyncLocal<CancellationToken?> Current = new();

    /// <summary>当前请求令牌（非 HTTP 上下文/未设置时为 null）</summary>
    public static CancellationToken? Token => Current.Value;

    /// <summary>由管线每请求调用（注册于中间件，作用域随请求回收）</summary>
    public static void Set(CancellationToken ct) => Current.Value = ct;

    /// <summary>仓储取用：显式 ct 优先，否则回退请求令牌，再否则 None</summary>
    public static CancellationToken Resolve(CancellationToken ct) =>
        ct != default ? ct : (Current.Value ?? CancellationToken.None);
}
