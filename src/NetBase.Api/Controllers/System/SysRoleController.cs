using NetBase.Common.Users;
using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.System;

/// <summary>系统角色管理</summary>
[Route("api/v1/sys/role")]
public class SysRoleController(
    ISysRoleService roleService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>分页查询角色</summary>
    [HasPermission("sys:role:list")]
        [HttpGet("page")]
    public async Task<ApiResult<PageResult<RoleDto>>> GetPageList([FromQuery] RoleQueryDto query)
    {
        var result = await roleService.GetPageListAsync(query);
        return Success(result);
    }

    /// <summary>查询全部启用角色（下拉框用）</summary>
    [HasPermission("sys:role:list")]
        [HttpGet("list")]
    public async Task<ApiResult<List<RoleSimpleDto>>> GetAllEnabled()
    {
        return Success(await roleService.GetAllEnabledAsync());
    }

    /// <summary>查询角色详情</summary>
    [HasPermission("sys:role:list")]
        [HttpGet("{id:long}")]
    public async Task<ApiResult<RoleDto?>> GetDetail(long id)
    {
        return Success(await roleService.GetDetailAsync(id));
    }

    /// <summary>导出角色列表（xlsx，条件同分页）</summary>
    [HasPermission("sys:role:list")]
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] RoleQueryDto query)
    {
        var list = await roleService.GetExportListAsync(query);
        var rows = list.Select(r => new
        {
            角色名称 = r.RoleName,
            角色编码 = r.RoleCode,
            状态 = r.Status == 1 ? "启用" : "停用",
            排序 = r.Sort,
            创建时间 = r.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
        });
        return ExcelResult(rows, $"角色列表_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>创建角色</summary>
    [HasPermission("sys:role:add")]
        [HttpPost]
    public async Task<ApiResult<long>> Create([FromBody] RoleSaveDto dto)
    {
        var id = await roleService.CreateAsync(dto, OperatorName);
        return Success(id, "创建成功");
    }

    /// <summary>更新角色</summary>
    [HasPermission("sys:role:edit")]
        [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] RoleSaveDto dto)
    {
        await roleService.UpdateAsync(id, dto, OperatorName);
        return Success();
    }

    /// <summary>删除角色（不允许删除内置 admin 角色）</summary>
    [HasPermission("sys:role:delete")]
        [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await roleService.DeleteAsync(id, OperatorName);
        return Success();
    }

    /// <summary>查询角色已分配的菜单ID</summary>
    [HasPermission("sys:role:list")]
        [HttpGet("{id:long}/menu-ids")]
    public async Task<ApiResult<List<long>>> GetMenuIds(long id)
    {
        return Success(await roleService.GetMenuIdsAsync(id));
    }

    /// <summary>为角色分配菜单（全量重设）</summary>
    [HasPermission("sys:role:edit")]
        [HttpPut("{id:long}/menus")]
    public async Task<ApiResult> AssignMenus(long id, [FromBody] AssignMenusDto dto)
    {
        await roleService.AssignMenusAsync(id, dto.MenuIds);
        return ApiResult.Ok("菜单分配成功");
    }
}
