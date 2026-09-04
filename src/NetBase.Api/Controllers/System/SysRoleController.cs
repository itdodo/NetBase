using Microsoft.AspNetCore.Mvc;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.System;

/// <summary>系统角色管理</summary>
[Route("api/sys/role")]
public class SysRoleController(ISysRoleService roleService) : BaseController
{
    /// <summary>分页查询角色</summary>
    [HttpGet("page")]
    public async Task<ApiResult<PageResult<RoleDto>>> GetPageList([FromQuery] RoleQueryDto query)
    {
        var result = await roleService.GetPageListAsync(query);
        return Success(result);
    }

    /// <summary>查询全部启用角色（下拉框用）</summary>
    [HttpGet("list")]
    public async Task<ApiResult<List<RoleSimpleDto>>> GetAllEnabled()
    {
        return Success(await roleService.GetAllEnabledAsync());
    }

    /// <summary>查询角色详情</summary>
    [HttpGet("{id:long}")]
    public async Task<ApiResult<RoleDto?>> GetDetail(long id)
    {
        return Success(await roleService.GetDetailAsync(id));
    }

    /// <summary>创建角色</summary>
    [HttpPost]
    public async Task<ApiResult<long>> Create([FromBody] RoleSaveDto dto)
    {
        var id = await roleService.CreateAsync(dto);
        return Success(id, "创建成功");
    }

    /// <summary>更新角色</summary>
    [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] RoleSaveDto dto)
    {
        await roleService.UpdateAsync(id, dto);
        return Success();
    }

    /// <summary>删除角色（不允许删除内置 admin 角色）</summary>
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await roleService.DeleteAsync(id);
        return Success();
    }

    /// <summary>查询角色已分配的菜单ID</summary>
    [HttpGet("{id:long}/menu-ids")]
    public async Task<ApiResult<List<long>>> GetMenuIds(long id)
    {
        return Success(await roleService.GetMenuIdsAsync(id));
    }

    /// <summary>为角色分配菜单（全量重设）</summary>
    [HttpPut("{id:long}/menus")]
    public async Task<ApiResult> AssignMenus(long id, [FromBody] AssignMenusDto dto)
    {
        await roleService.AssignMenusAsync(id, dto.MenuIds);
        return ApiResult.Ok("菜单分配成功");
    }
}
