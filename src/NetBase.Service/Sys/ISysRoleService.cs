using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;

namespace NetBase.Service.Sys;

/// <summary>角色业务接口</summary>
public interface ISysRoleService
{
    /// <summary>分页查询角色</summary>
    Task<PageResult<RoleDto>> GetPageListAsync(RoleQueryDto query);

    /// <summary>查询全部可用角色（下拉框用）</summary>
    Task<List<RoleSimpleDto>> GetAllEnabledAsync();

    /// <summary>按查询条件取全量角色（导出用，不分页）</summary>
    Task<List<RoleDto>> GetExportListAsync(RoleQueryDto query);

    /// <summary>查询角色详情</summary>
    Task<RoleDto?> GetDetailAsync(long id);

    /// <summary>创建角色</summary>
    Task<long> CreateAsync(RoleSaveDto dto, string? operatorName = null);

    /// <summary>更新角色</summary>
    Task UpdateAsync(long id, RoleSaveDto dto, string? operatorName = null);

    /// <summary>删除角色（软删除，同时清理关联；不允许删除内置 admin 角色）</summary>
    Task DeleteAsync(long id, string? operatorName = null);

    /// <summary>为角色分配菜单（全量重设）</summary>
    Task AssignMenusAsync(long roleId, List<long> menuIds);

    /// <summary>查询角色已分配的菜单ID</summary>
    Task<List<long>> GetMenuIdsAsync(long roleId);

    /// <summary>查询角色自定义数据权限的部门ID</summary>
    Task<List<long>> GetRoleDeptIdsAsync(long roleId);
}
