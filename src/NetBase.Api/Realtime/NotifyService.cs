using Microsoft.AspNetCore.SignalR;
using NetBase.Api.Hubs;
using NetBase.Common.Realtime;
using NetBase.Service.Sys;

namespace NetBase.Api.Realtime;

/// <summary>
/// 实时通知实现：落库 sys_message + 经 NotifyHub 推送在线连接。
/// </summary>
public class NotifyService(
    IHubContext<NotifyHub> hubContext,
    IUserConnectionMapping connectionMapping,
    ISysMessageService messageService) : INotifyService
{
    public async Task PushToUsersAsync(IReadOnlyCollection<long> userIds, NoticePayload notice, CancellationToken ct = default)
    {
        foreach (var userId in userIds.Distinct())
        {
            // 落库：离线用户登录后可在通知中心看到。
            // 通知是尽力而为的辅助通道：接收人已删除/停用时跳过落库（仅推在线连接），不阻断引擎主流程
            try
            {
                await messageService.SendAsync(new MessageSendDto
                {
                    ReceiverId = userId,
                    Title = notice.Title,
                    Content = notice.Content,
                    MsgType = notice.MsgType,
                    BizType = notice.BizType,
                    BizId = notice.BizId
                }, notice.SenderName ?? "system");
            }
            catch (Common.Exceptions.BusinessException)
            {
                // 接收人不存在（已删除）——跳过落库继续
            }

            // 在线实时推
            await PushConnectionsAsync(userId, notice);
        }
    }

    public async Task PushToOnlineAsync(NoticePayload notice, CancellationToken ct = default)
    {
        foreach (var userId in connectionMapping.GetOnlineUserIds())
        {
            await PushConnectionsAsync(userId, notice);
        }
    }

    public async Task PushForceLogoutAsync(long userId, string reason, CancellationToken ct = default)
    {
        foreach (var connectionId in connectionMapping.GetConnections(userId))
        {
            await hubContext.Clients.Client(connectionId)
                .SendAsync("force-logout", new { reason }, ct);
        }
    }

    private async Task PushConnectionsAsync(long userId, NoticePayload notice)
    {
        var connections = connectionMapping.GetConnections(userId);
        if (connections.Count == 0) return;

        await hubContext.Clients.Clients(connections)
            .SendAsync("notice", new
            {
                msgType = notice.MsgType,
                title = notice.Title,
                content = notice.Content,
                senderName = notice.SenderName,
                bizType = notice.BizType,
                bizId = notice.BizId
            });
    }
}
