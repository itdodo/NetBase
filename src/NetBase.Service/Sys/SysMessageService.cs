using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using SqlSugar;

namespace NetBase.Service.Sys;

/// <summary>站内信实现</summary>
public class SysMessageService(IRepository<SysMessage> repository) : ISysMessageService
{
    public async Task<long> SendAsync(MessageSendDto dto, string senderName)
    {
        var message = new SysMessage
        {
            Title = dto.Title,
            Content = dto.Content,
            SenderName = senderName,
            ReceiverId = dto.ReceiverId,
            IsRead = false
        };
        await repository.InsertAsync(message);
        return message.Id;
    }

    public async Task<PageResult<SysMessage>> GetMyPageAsync(long userId, MessageQueryDto query)
    {
        // 接收人条件恒定携带（防越权泄露他人消息），关键字/已读为可选条件
        var exp = Expressionable.Create<SysMessage>();
        exp.And(x => x.ReceiverId == userId);
        if (query.Keyword.IsNotNullOrEmpty())
        {
            var keyword = query.Keyword!.Trim();
            exp.And(x => x.Title.Contains(keyword) || x.Content.Contains(keyword));
        }
        if (query.IsRead.HasValue)
        {
            var isRead = query.IsRead.Value == 1;
            exp.And(x => x.IsRead == isRead);
        }

        var page = await repository.GetPageListAsync(exp.ToExpression(), query);
        return PageResult<SysMessage>.Of(page.Items, page.Total, page.PageIndex, page.PageSize);
    }

    public async Task<int> GetUnreadCountAsync(long userId) =>
        (int)await repository.CountAsync(x => x.ReceiverId == userId && !x.IsRead);

    public async Task MarkReadAsync(long userId, long messageId)
    {
        var message = await repository.GetByIdAsync(messageId)
            ?? throw new BusinessException("消息不存在", ApiResultCode.NotFound);
        if (message.ReceiverId != userId)
        {
            throw new BusinessException("无权操作该消息", ApiResultCode.Forbidden);
        }
        if (!message.IsRead)
        {
            message.IsRead = true;
            message.ReadTime = DateTime.Now;
            await repository.UpdateAsync(message);
        }
    }

    public async Task MarkAllReadAsync(long userId) =>
        await repository.UpdateWhereAsync(
            x => x.ReceiverId == userId && !x.IsRead,
            x => new SysMessage { IsRead = true, ReadTime = DateTime.Now });
}
