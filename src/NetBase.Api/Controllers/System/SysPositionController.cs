using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Common.Users;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.System;

/// <summary>岗位管理（审批权限的载体；角色管菜单/接口权限，岗位管审批人解析）</summary>
[ApiController]
[Route("api/v1/sys/position")]
public class SysPositionController(ISysPositionService service,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>岗位分页</summary>
    [HasPermission("sys:position:list")]
    [HttpGet("page")]
    public async Task<ApiResult<PageResult<PositionDto>>> Page([FromQuery] PositionQueryDto query) =>
        Success(await service.GetPageListAsync(query));

    /// <summary>全部启用岗位（下拉框/审批人选择用）</summary>
    [Authorize]
    [HttpGet("list")]
    public async Task<ApiResult<List<PositionDto>>> List() => Success(await service.GetEnabledListAsync());

    /// <summary>创建岗位</summary>
    [HasPermission("sys:position:add")]
    [NoRepeatSubmit]
    [HttpPost]
    public async Task<ApiResult<string>> Create([FromBody] PositionSaveDto dto) =>
        SuccessId(await service.CreateAsync(dto, OperatorName), "创建成功");

    /// <summary>更新岗位</summary>
    [HasPermission("sys:position:edit")]
    [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] PositionSaveDto dto)
    {
        await service.UpdateAsync(id, dto, OperatorName);
        return Success();
    }

    /// <summary>删除岗位（有用户时不允许）</summary>
    [HasPermission("sys:position:delete")]
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await service.DeleteAsync(id);
        return Success();
    }
}
