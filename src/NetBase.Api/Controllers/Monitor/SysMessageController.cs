using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Common.Users;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.Monitor;

/// <summary>站内信：发送（管理）与收件箱</summary>
[ApiController]
[Route("api/v1/sys/message")]
public class SysMessageController(ISysMessageService messageService, ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>发送消息（管理端）</summary>
    [HasPermission("sys:notice:add")]
    [HttpPost]
    public async Task<ApiResult<string>> Send([FromBody] MessageSendDto dto)
    {
        return SuccessId(await messageService.SendAsync(dto, OperatorName), "发送成功");
    }

    /// <summary>我的收件箱分页</summary>
    [Authorize]
    [HttpGet("my/page")]
    public async Task<ApiResult<PageResult<SysMessage>>> MyPage([FromQuery] MessageQueryDto query)
    {
        return Success(await messageService.GetMyPageAsync(currentUserService.UserId ?? 0, query));
    }

    /// <summary>我的未读数</summary>
    [Authorize]
    [HttpGet("my/unread-count")]
    public async Task<ApiResult<int>> UnreadCount()
    {
        return Success(await messageService.GetUnreadCountAsync(currentUserService.UserId ?? 0));
    }

    /// <summary>标记已读</summary>
    [Authorize]
    [HttpPut("my/{id:long}/read")]
    public async Task<ApiResult> MarkRead(long id)
    {
        await messageService.MarkReadAsync(currentUserService.UserId ?? 0, id);
        return Success();
    }

    /// <summary>全部标记已读</summary>
    [Authorize]
    [HttpPut("my/read-all")]
    public async Task<ApiResult> MarkAllRead()
    {
        await messageService.MarkAllReadAsync(currentUserService.UserId ?? 0);
        return Success();
    }
}
