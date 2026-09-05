using NetBase.Common.Users;
using Microsoft.AspNetCore.Mvc;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.System;

/// <summary>系统菜单管理</summary>
[Route("api/sys/menu")]
public class SysMenuController(
    ISysMenuService menuService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>查询菜单树（全量）</summary>
    [HttpGet("tree")]
    public async Task<ApiResult<List<MenuTreeDto>>> GetTree()
    {
        return Success(await menuService.GetTreeAsync());
    }

    /// <summary>查询指定角色的菜单树</summary>
    [HttpGet("tree/role/{roleId:long}")]
    public async Task<ApiResult<List<MenuTreeDto>>> GetTreeByRole(long roleId)
    {
        return Success(await menuService.GetTreeByRoleAsync(roleId));
    }

    /// <summary>查询菜单详情</summary>
    [HttpGet("{id:long}")]
    public async Task<ApiResult<MenuTreeDto?>> GetDetail(long id)
    {
        return Success(await menuService.GetDetailAsync(id));
    }

    /// <summary>创建菜单（目录/菜单/按钮）</summary>
    [HttpPost]
    public async Task<ApiResult<long>> Create([FromBody] MenuSaveDto dto)
    {
        var id = await menuService.CreateAsync(dto, OperatorName);
        return Success(id, "创建成功");
    }

    /// <summary>更新菜单（父级不能是自身或子孙节点）</summary>
    [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] MenuSaveDto dto)
    {
        await menuService.UpdateAsync(id, dto, OperatorName);
        return Success();
    }

    /// <summary>删除菜单（存在子节点或被角色引用时禁止删除）</summary>
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await menuService.DeleteAsync(id, OperatorName);
        return Success();
    }
}
