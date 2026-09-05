using NetBase.Common.Users;
using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.System;

/// <summary>系统用户管理</summary>
[Route("api/v1/sys/user")]
public class SysUserController(
    ISysUserService userService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>分页查询用户</summary>
    [HasPermission("sys:user:list")]
        [HttpGet("page")]
    public async Task<ApiResult<PageResult<UserDto>>> GetPageList([FromQuery] UserQueryDto query)
    {
        var result = await userService.GetPageListAsync(query);
        return Success(result);
    }

    /// <summary>查询全部启用用户（下拉框用）</summary>
    [HasPermission("sys:user:list")]
        [HttpGet("list")]
    public async Task<ApiResult<List<UserDto>>> GetAllEnabled()
    {
        return Success(await userService.GetAllEnabledAsync());
    }

    /// <summary>查询用户详情（含角色）</summary>
    [HasPermission("sys:user:list")]
        [HttpGet("{id:long}")]
    public async Task<ApiResult<UserDto?>> GetDetail(long id)
    {
        return Success(await userService.GetDetailAsync(id));
    }

    /// <summary>导出用户列表（xlsx，条件同分页）</summary>
    [HasPermission("sys:user:list")]
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] UserQueryDto query)
    {
        var list = await userService.GetExportListAsync(query);
        var rows = list.Select(u => new
        {
            用户名 = u.UserName,
            昵称 = u.NickName,
            手机号 = u.Phone,
            邮箱 = u.Email,
            状态 = u.Status == 1 ? "启用" : "停用",
            角色 = string.Join(",", u.Roles.Select(r => r.RoleName)),
            最后登录时间 = u.LastLoginTime?.ToString("yyyy-MM-dd HH:mm:ss"),
            创建时间 = u.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
        });
        return ExcelResult(rows, $"用户列表_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>创建用户</summary>
    [HasPermission("sys:user:add")]
        [HttpPost]
    public async Task<ApiResult<long>> Create([FromBody] UserCreateDto dto)
    {
        var id = await userService.CreateAsync(dto, OperatorName);
        return Success(id, "创建成功");
    }

    /// <summary>更新用户（RoleIds 传入则全量重设角色）</summary>
    [HasPermission("sys:user:edit")]
        [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] UserUpdateDto dto)
    {
        await userService.UpdateAsync(id, dto, OperatorName);
        return Success();
    }

    /// <summary>删除用户（不允许删除内置 admin）</summary>
    [HasPermission("sys:user:delete")]
        [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await userService.DeleteAsync(id, OperatorName);
        return Success();
    }

    /// <summary>重置密码（newPassword 为空则重置为默认密码 123456）</summary>
    [HasPermission("sys:user:edit")]
        [HttpPut("{id:long}/password/reset")]
    public async Task<ApiResult> ResetPassword(long id, [FromBody] ResetPasswordDto? dto)
    {
        await userService.ResetPasswordAsync(id, dto?.NewPassword, OperatorName);
        return ApiResult.Ok("密码已重置");
    }

    /// <summary>为用户分配角色（全量重设）</summary>
    [HasPermission("sys:user:edit")]
        [HttpPut("{id:long}/roles")]
    public async Task<ApiResult> AssignRoles(long id, [FromBody] AssignRolesDto dto)
    {
        await userService.AssignRolesAsync(id, dto.RoleIds);
        return ApiResult.Ok("角色分配成功");
    }
}
