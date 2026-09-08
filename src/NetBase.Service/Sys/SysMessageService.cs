using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using SqlSugar;

namespace NetBase.Service.Sys;

/// <summary>站内信实现</summary>
public class SysMessageService(IRepository<SysMessage> repository, IRepository<SysUser> userRepository) : ISysMessageService
{
    public async Task<long> SendAsync(MessageSendDto dto, string senderName)
    {
        // 接收人必须存在（防消息发往无效账号）
        if (!await userRepository.AnyAsync(x => x.Id == dto.ReceiverId))
        {
            throw new BusinessException($"接收用户不存在（Id={dto.ReceiverId}）", ApiResultCode.BadRequest);
        }

        var message = new SysMessage
        {
            Title = dto.Title,
            Content = dto.Content,
            SenderName = senderName,
            ReceiverId = dto.ReceiverId,
            MsgType = dto.MsgType,
            BizType = dto.BizType,
            BizId = dto.BizId,
            IsRead = false
        };
        await repository.InsertAsync(message);
        return message.Id;
    }

    public async Task<PageResult<SysMessage>> GetMyPageAsync(long userId, MessageQueryDto query)
    {
        // 接收人条件恒定携带（防越权泄露他人消息），关键字/已读为可选条件
        var keyword = query.Keyword?.Trim();
        var isRead = query.IsRead.HasValue ? query.IsRead.Value == 1 : (bool?)null;
        var page = await repository.GetPageListAsync(
            Expressionable.Create<SysMessage>()
                .And(x => x.ReceiverId == userId)
                .AndIF(keyword.IsNotNullOrEmpty(), x => x.Title.Contains(keyword!) || x.Content.Contains(keyword!))
                .AndIF(isRead.HasValue, x => x.IsRead == isRead!.Value)
                .ToExpression(), query);
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
