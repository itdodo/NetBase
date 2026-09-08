namespace NetBase.Common.Realtime;

/// <summary>实时通知负载（业务事件通用结构）</summary>
public record NoticePayload
{
    /// <summary>消息类型：1-系统 2-站内信 3-业务（审批/工单等）</summary>
    public int MsgType { get; init; } = 1;

    /// <summary>标题</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>内容</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>发送者名称</summary>
    public string? SenderName { get; init; }

    /// <summary>业务类型（如 approval/order，前端点击跳转定位）</summary>
    public string? BizType { get; init; }

    /// <summary>业务单据ID</summary>
    public string? BizId { get; init; }
}

/// <summary>
/// 实时通知服务（业务模块统一入口）：落库 sys_message（离线可见）+ SignalR 实时推送（在线秒达）。
/// 场景示例：单据送审 `PushToUsersAsync(审核人Ids, new NoticePayload { ... BizType = "approval", BizId = 单据Id })`。
/// 实现位于 Api 层（依赖 SignalR Hub），业务服务层仅依赖本接口。
/// </summary>
public interface INotifyService
{
    /// <summary>推送给指定用户（落库 + 在线实时推）</summary>
    Task PushToUsersAsync(IReadOnlyCollection<long> userIds, NoticePayload notice, CancellationToken ct = default);

    /// <summary>广播给全部在线用户（不落库）</summary>
    Task PushToOnlineAsync(NoticePayload notice, CancellationToken ct = default);

    /// <summary>强制下线通知（向目标用户全部连接推送，由前端主动断开并跳登录）</summary>
    Task PushForceLogoutAsync(long userId, string reason, CancellationToken ct = default);
}
