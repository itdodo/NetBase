using Microsoft.AspNetCore.Authorization;
using NetBase.Common.Users;
using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.System;

/// <summary>系统菜单管理</summary>
[Route("api/v1/sys/menu")]
public class SysMenuController(
    ISysMenuService menuService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>查询菜单树（全量）</summary>
    [HasPermission("sys:menu:list")]
        [HttpGet("tree")]
    public async Task<ApiResult<List<MenuTreeDto>>> GetTree()
    {
        return Success(await menuService.GetTreeAsync());
    }

    /// <summary>查询当前用户可见菜单树（登录即可调用；无角色用户返回空树）</summary>
    [Authorize]
    [HttpGet("tree/my")]
    public async Task<ApiResult<List<MenuTreeDto>>> MyTree()
    {
        return Success(await menuService.GetTreeByUserAsync(currentUserService.UserId ?? 0));
    }

    /// <summary>查询指定角色的菜单树</summary>
    [HasPermission("sys:menu:list")]
        [HttpGet("tree/role/{roleId:long}")]
    public async Task<ApiResult<List<MenuTreeDto>>> GetTreeByRole(long roleId)
    {
        return Success(await menuService.GetTreeByRoleAsync(roleId));
    }

    /// <summary>查询菜单详情</summary>
    [HasPermission("sys:menu:list")]
        [HttpGet("{id:long}")]
    public async Task<ApiResult<MenuTreeDto?>> GetDetail(long id)
    {
        return Success(await menuService.GetDetailAsync(id));
    }

    /// <summary>创建菜单（目录/菜单/按钮）</summary>
    [HasPermission("sys:menu:add")]
        [HttpPost]
    public async Task<ApiResult<string>> Create([FromBody] MenuSaveDto dto)
    {
        var id = await menuService.CreateAsync(dto, OperatorName);
        return SuccessId(id, "创建成功");
    }

    /// <summary>更新菜单（父级不能是自身或子孙节点）</summary>
    [HasPermission("sys:menu:edit")]
        [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] MenuSaveDto dto)
    {
        await menuService.UpdateAsync(id, dto, OperatorName);
        return Success();
    }

    /// <summary>删除菜单（存在子节点或被角色引用时禁止删除）</summary>
    [HasPermission("sys:menu:delete")]
        [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await menuService.DeleteAsync(id, OperatorName);
        return Success();
    }
}
