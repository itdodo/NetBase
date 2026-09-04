using NetBase.Model.Dtos;

namespace NetBase.Service.Sys;

/// <summary>菜单业务接口</summary>
public interface ISysMenuService
{
    /// <summary>查询全部菜单并组装为树（可选按角色过滤）</summary>
    Task<List<MenuTreeDto>> GetTreeAsync(long? roleId = null);

    /// <summary>查询角色已分配的菜单树</summary>
    Task<List<MenuTreeDto>> GetTreeByRoleAsync(long roleId);

    /// <summary>查询菜单详情</summary>
    Task<MenuTreeDto?> GetDetailAsync(long id);

    /// <summary>创建菜单</summary>
    Task<long> CreateAsync(MenuSaveDto dto, string? operatorName = null);

    /// <summary>更新菜单</summary>
    Task UpdateAsync(long id, MenuSaveDto dto, string? operatorName = null);

    /// <summary>删除菜单（校验子节点与角色引用）</summary>
    Task DeleteAsync(long id);
}
