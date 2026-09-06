using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Common.Users;
using NetBase.Model.Dtos;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.System;

/// <summary>部门管理（组织树）</summary>
[ApiController]
[Route("api/v1/sys/dept")]
public class SysDeptController(ISysDeptService deptService, ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>部门树</summary>
    [HasPermission("sys:dept:list")]
    [HttpGet("tree")]
    public async Task<ApiResult<List<DeptTreeDto>>> Tree()
    {
        return Success(await deptService.GetTreeAsync());
    }

    /// <summary>部门详情</summary>
    [HasPermission("sys:dept:list")]
    [HttpGet("{id:long}")]
    public async Task<ApiResult<object?>> Detail(long id)
    {
        return Success(await deptService.GetDetailAsync(id) as object);
    }

    /// <summary>创建部门</summary>
    [HasPermission("sys:dept:add")]
    [HttpPost]
    public async Task<ApiResult<string>> Create([FromBody] DeptSaveDto dto)
    {
        return SuccessId(await deptService.CreateAsync(dto, OperatorName), "创建成功");
    }

    /// <summary>更新部门</summary>
    [HasPermission("sys:dept:edit")]
    [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] DeptSaveDto dto)
    {
        await deptService.UpdateAsync(id, dto, OperatorName);
        return Success();
    }

    /// <summary>删除部门（存在下级或用户时拒绝）</summary>
    [HasPermission("sys:dept:delete")]
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await deptService.DeleteAsync(id);
        return Success();
    }
}
