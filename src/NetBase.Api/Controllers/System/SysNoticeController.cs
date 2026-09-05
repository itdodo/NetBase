using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Common.Users;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.System;

/// <summary>通知公告</summary>
[ApiController]
[Route("api/v1/sys/notice")]
public class SysNoticeController(ISysNoticeService noticeService, ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>公告分页（管理端，含停用）</summary>
    [HasPermission("sys:notice:list")]
    [HttpGet("page")]
    public async Task<ApiResult<PageResult<SysNotice>>> Page([FromQuery] NoticeQueryDto query)
    {
        return Success(await noticeService.GetPageAsync(query));
    }

    /// <summary>公告详情</summary>
    [HasPermission("sys:notice:list")]
    [HttpGet("{id:long}")]
    public async Task<ApiResult<SysNotice?>> Detail(long id)
    {
        return Success(await noticeService.GetDetailAsync(id));
    }

    /// <summary>登录用户取最新启用公告（顶栏铃铛，无需权限码）</summary>
    [Authorize]
    [HttpGet("latest")]
    public async Task<ApiResult<List<SysNotice>>> Latest()
    {
        return Success(await noticeService.GetLatestAsync());
    }

    /// <summary>创建公告</summary>
    [HasPermission("sys:notice:add")]
    [HttpPost]
    public async Task<ApiResult<long>> Create([FromBody] NoticeSaveDto dto)
    {
        return Success(await noticeService.CreateAsync(dto, OperatorName), "创建成功");
    }

    /// <summary>更新公告</summary>
    [HasPermission("sys:notice:edit")]
    [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] NoticeSaveDto dto)
    {
        await noticeService.UpdateAsync(id, dto, OperatorName);
        return Success();
    }

    /// <summary>删除公告</summary>
    [HasPermission("sys:notice:delete")]
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await noticeService.DeleteAsync(id);
        return Success();
    }
}
