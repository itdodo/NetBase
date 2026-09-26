using Microsoft.AspNetCore.SignalR;
using NetBase.Api.Hubs;
using NetBase.Common.Realtime;
using NetBase.Service.Sys;

namespace NetBase.Api.Realtime;

/// <summary>
/// 实时通知实现：落库 sys_message + 经 NotifyHub 推送在线连接；
/// 审批事件可按白名单（sys.email.notifyBizTypes）镜像发送邮件。
/// </summary>
public class NotifyService(
    IHubContext<NotifyHub> hubContext,
    IUserConnectionMapping connectionMapping,
    ISysMessageService messageService,
    ISysConfigService configService,
    NetBase.Common.Email.IEmailService emailService,
    NetBase.Repository.Repositories.IRepository<NetBase.Model.Entities.SysUser> userRepository) : INotifyService
{
    public async Task PushToUsersAsync(IReadOnlyCollection<long> userIds, NoticePayload notice, CancellationToken ct = default)
    {
        // 审批事件邮件镜像：BizType 命中白名单（逗号分隔，空=不发）且接收人有邮箱时发送；
        // 邮件是尽力而为的辅助通道，任何失败不影响站内信与实时推（红线 19）
        var bizTypes = (await configService.GetConfigValueAsync("sys.email.notifyBizTypes"))?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var emailTargets = bizTypes != null && bizTypes.Count > 0 && notice.BizType != null && bizTypes.Contains(notice.BizType)
            ? (await userRepository.GetListAsync(x => userIds.Contains(x.Id) && x.IsDeleted == false && x.Status == 1))
                .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                .ToDictionary(u => u.Id, u => u.Email!.Trim())
            : new Dictionary<long, string>();

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

            // 邮件镜像（白名单命中且有邮箱）
            if (emailTargets.TryGetValue(userId, out var to))
            {
                await emailService.SendAsync(to, notice.Title,
                    $"<p>{System.Net.WebUtility.HtmlEncode(notice.Content).Replace("\n", "<br/>")}</p>");
            }
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
