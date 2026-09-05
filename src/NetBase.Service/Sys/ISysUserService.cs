using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;

namespace NetBase.Service.Sys;

/// <summary>用户业务接口</summary>
public interface ISysUserService
{
    /// <summary>分页查询用户（含角色）</summary>
    Task<PageResult<UserDto>> GetPageListAsync(UserQueryDto query);

    /// <summary>查询全部可用用户（下拉框用）</summary>
    Task<List<UserDto>> GetAllEnabledAsync();

    /// <summary>查询用户详情（含角色）</summary>
    Task<UserDto?> GetDetailAsync(long id);

    /// <summary>创建用户</summary>
    Task<long> CreateAsync(UserCreateDto dto, string? operatorName = null);

    /// <summary>更新用户基本信息与角色</summary>
    Task UpdateAsync(long id, UserUpdateDto dto, string? operatorName = null);

    /// <summary>删除用户（软删除，同时清理角色关联；不允许删除 admin）</summary>
    Task DeleteAsync(long id, string? operatorName = null);

    /// <summary>重置密码为指定值（为空则用默认密码）</summary>
    Task ResetPasswordAsync(long id, string? newPassword, string? operatorName = null);

    /// <summary>为用户分配角色（全量重设）</summary>
    Task AssignRolesAsync(long userId, List<long> roleIds);

    /// <summary>按用户名查询（登录用，含已停用）</summary>
    Task<SysUser?> GetByUserNameAsync(string userName);
}
