using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NetBase.Common.Users;

namespace NetBase.Api.Hubs;

/// <summary>
/// 实时通知 Hub：登录用户连接后可接收 站内信/公告/业务事件/强制下线 推送。
/// 连接归属解析自 JWT（uid claim）；Hub 方法不暴露写操作（通知只下行）。
/// </summary>
[Authorize]
public class NotifyHub : Hub
{
    private readonly IUserConnectionMapping _mapping;
    private readonly ICurrentUserService _currentUser;

    public NotifyHub(IUserConnectionMapping mapping, ICurrentUserService currentUser)
    {
        _mapping = mapping;
        _currentUser = currentUser;
    }

    public override Task OnConnectedAsync()
    {
        var userId = _currentUser.UserId;
        if (userId.HasValue)
        {
            _mapping.Add(userId.Value, Context.ConnectionId);
        }
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = _currentUser.UserId;
        if (userId.HasValue)
        {
            _mapping.Remove(userId.Value, Context.ConnectionId);
        }
        return base.OnDisconnectedAsync(exception);
    }
}
