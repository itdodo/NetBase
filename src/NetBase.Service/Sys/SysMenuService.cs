using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using Mapster;
using NetBase.Service.Base;

namespace NetBase.Service.Sys;

/// <summary>菜单业务实现</summary>
public class SysMenuService : BaseService<SysMenu>, ISysMenuService
{
    private readonly IRepository<SysRoleMenu> _roleMenuRepository;
    private readonly IRepository<SysRole> _roleRepository;
    private readonly IRepository<SysUserRole> _userRoleRepository;
    private readonly IPermissionService _permissionService;

    public SysMenuService(
        IRepository<SysMenu> repository,
        IRepository<SysRoleMenu> roleMenuRepository,
        IRepository<SysRole> roleRepository,
        IRepository<SysUserRole> userRoleRepository,
        IPermissionService permissionService) : base(repository)
    {
        _roleMenuRepository = roleMenuRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _permissionService = permissionService;
    }

    public async Task<List<MenuTreeDto>> GetTreeAsync(long? roleId = null)
    {
        var menus = await Repository.GetListAsync();

        if (roleId.HasValue)
        {
            _ = await _roleRepository.GetByIdAsync(roleId.Value)
                ?? throw new BusinessException($"角色不存在（Id={roleId}）", ApiResultCode.NotFound, ErrorCodes.SYS_ROLE_NOT_FOUND);

            menus = FilterByVisibleIds(menus, [await CollectRoleVisibleIdsAsync(roleId.Value)]);
        }

        return BuildTree(menus, 0);
    }

    public async Task<List<MenuTreeDto>> GetTreeByRoleAsync(long roleId) => await GetTreeAsync(roleId);

    /// <inheritdoc />
    public async Task<List<MenuTreeDto>> GetTreeByUserAsync(long userId)
    {
        var roleIds = (await _userRoleRepository.GetListAsync(x => x.UserId == userId))
            .Select(x => x.RoleId).Distinct().ToList();
        if (roleIds.Count == 0)
        {
            return []; // 无角色：空树（登录成功但暂无任何功能权限）
        }

        // 内置管理员 → 全量
        var admin = await _roleRepository.GetFirstAsync(x => x.RoleCode == SysRoleService.AdminRoleCode);
        if (admin != null && roleIds.Contains(admin.Id))
        {
            return await GetTreeAsync();
        }

        var menus = await Repository.GetListAsync();
        var visibleIdSets = new List<HashSet<long>>();
        foreach (var roleId in roleIds)
        {
            visibleIdSets.Add(await CollectRoleVisibleIdsAsync(roleId));
        }
        var allVisible = visibleIdSets.SelectMany(x => x).ToHashSet();
        return BuildTree(menus.Where(x => allVisible.Contains(x.Id)).ToList(), 0);
    }

    /// <summary>收集角色可见菜单（含父级链补全：授权可能只勾子节点，父目录一并展示）</summary>
    private async Task<HashSet<long>> CollectRoleVisibleIdsAsync(long roleId)
    {
        var relations = await _roleMenuRepository.GetListAsync(x => x.RoleId == roleId);
        var menus = await Repository.GetListAsync();
        var menuById = menus.ToDictionary(x => x.Id);
        var visibleIds = new HashSet<long>();
        foreach (var menuId in relations.Select(x => x.MenuId).Distinct())
        {
            var current = menuId;
            while (current != 0 && !visibleIds.Contains(current) && menuById.TryGetValue(current, out var menu))
            {
                visibleIds.Add(current);
                current = menu.ParentId;
            }
        }
        return visibleIds;
    }

    private static List<SysMenu> FilterByVisibleIds(List<SysMenu> menus, IReadOnlyCollection<HashSet<long>> visibleIdSets)
    {
        var allVisible = visibleIdSets.SelectMany(x => x).ToHashSet();
        return menus.Where(x => allVisible.Contains(x.Id)).ToList();
    }

    public async Task<MenuTreeDto?> GetDetailAsync(long id)
    {
        var menu = await Repository.GetByIdAsync(id);
        return menu == null ? null : ToTreeDto(menu);
    }

    public async Task<long> CreateAsync(MenuSaveDto dto, string? operatorName = null)
    {
        Validate(dto);
        if (dto.ParentId != 0 && !await Repository.AnyAsync(x => x.Id == dto.ParentId))
        {
            throw new BusinessException("父级菜单不存在", ApiResultCode.BadRequest, ErrorCodes.SYS_MENU_PARENT_NOT_FOUND);
        }

        var menu = new SysMenu
        {
            ParentId = dto.ParentId,
            MenuName = dto.MenuName,
            MenuType = dto.MenuType,
            Path = dto.Path,
            Component = dto.Component,
            Permission = dto.Permission,
            Icon = dto.Icon,
            Sort = dto.Sort,
            Visible = dto.Visible,
            Status = dto.Status,
            CreateBy = operatorName
        };
        await Repository.InsertAsync(menu);
        return menu.Id;
    }

    public async Task UpdateAsync(long id, MenuSaveDto dto, string? operatorName = null)
    {
        var menu = await GetRequiredAsync(id);
        Validate(dto);
        if (dto.ParentId == id)
        {
            throw new BusinessException("父级菜单不能是自身", ApiResultCode.BadRequest, ErrorCodes.SYS_MENU_PARENT_SELF);
        }
        if (dto.ParentId != 0 && !await Repository.AnyAsync(x => x.Id == dto.ParentId))
        {
            throw new BusinessException("父级菜单不存在", ApiResultCode.BadRequest, ErrorCodes.SYS_MENU_PARENT_NOT_FOUND);
        }
        if (dto.ParentId != 0 && await IsDescendantAsync(id, dto.ParentId))
        {
            throw new BusinessException("父级菜单不能是自身的子孙节点", ApiResultCode.BadRequest, ErrorCodes.SYS_MENU_PARENT_CYCLE);
        }

        menu.ParentId = dto.ParentId;
        menu.MenuName = dto.MenuName;
        menu.MenuType = dto.MenuType;
        menu.Path = dto.Path;
        menu.Component = dto.Component;
        menu.Permission = dto.Permission;
        menu.Icon = dto.Icon;
        menu.Sort = dto.Sort;
        menu.Visible = dto.Visible;
        menu.Status = dto.Status;
        menu.UpdateTime = DateTime.Now;
        menu.UpdateBy = operatorName;
        // 跨会话陈旧检测：客户端回传读取时的版本参与比对（未传则用现读版本，兼容旧客户端）
        menu.Version = dto.Version ?? menu.Version;
        await UpdateWithConcurrencyCheckAsync(menu);
        await _permissionService.InvalidateAllAsync();
    }

    public async new Task DeleteAsync(long id, string? operatorName = null)
    {
        var menu = await GetRequiredAsync(id);
        if (await Repository.AnyAsync(x => x.ParentId == id))
        {
            throw new BusinessException("存在子菜单，不允许删除", ErrorCodes.SYS_MENU_HAS_CHILDREN);
        }
        if (await _roleMenuRepository.AnyAsync(x => x.MenuId == id))
        {
            throw new BusinessException("菜单已被角色引用，请先取消角色授权", ErrorCodes.SYS_MENU_IN_USE);
        }

        menu.UpdateBy = operatorName;
        menu.UpdateTime = DateTime.Now;
        await Repository.DeleteAsync(menu);
        await _permissionService.InvalidateAllAsync();
    }

    private Task<SysMenu> GetRequiredAsync(long id) =>
        GetRequiredAsync(id, $"菜单不存在（Id={id}）");

    /// <summary>判断 candidateId 是否为 ancestorId 的子孙节点（菜单量级小，内存遍历即可）</summary>
    private async Task<bool> IsDescendantAsync(long ancestorId, long candidateId)
    {
        var menus = await Repository.GetListAsync();
        var parentMap = menus.ToDictionary(x => x.Id, x => x.ParentId);
        var current = candidateId;
        while (parentMap.TryGetValue(current, out var parentId) && parentId != 0)
        {
            if (parentId == ancestorId)
            {
                return true;
            }
            current = parentId;
        }
        return false;
    }

    private static void Validate(MenuSaveDto dto)
    {
        if (dto.MenuName.IsNullOrWhiteSpace())
        {
            throw new BusinessException("菜单名称不能为空", ApiResultCode.BadRequest, ErrorCodes.SYS_MENU_NAME_REQUIRED);
        }
        if (dto.MenuType is < 1 or > 3)
        {
            throw new BusinessException("菜单类型无效（1-目录 2-菜单 3-按钮）", ApiResultCode.BadRequest, ErrorCodes.SYS_MENU_TYPE_INVALID);
        }
    }

    private static List<MenuTreeDto> BuildTree(List<SysMenu> menus, long parentId)
    {
        return menus.Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Sort)
            .Select(x =>
            {
                var node = ToTreeDto(x);
                node.Children = BuildTree(menus, x.Id);
                return node;
            })
            .ToList();
    }

    private static MenuTreeDto ToTreeDto(SysMenu x) => x.Adapt<MenuTreeDto>();
}
