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
    private readonly IPermissionService _permissionService;

    public SysMenuService(
        IRepository<SysMenu> repository,
        IRepository<SysRoleMenu> roleMenuRepository,
        IRepository<SysRole> roleRepository,
        IPermissionService permissionService) : base(repository)
    {
        _roleMenuRepository = roleMenuRepository;
        _roleRepository = roleRepository;
        _permissionService = permissionService;
    }

    public async Task<List<MenuTreeDto>> GetTreeAsync(long? roleId = null)
    {
        var menus = await Repository.GetListAsync();

        if (roleId.HasValue)
        {
            _ = await _roleRepository.GetByIdAsync(roleId.Value)
                ?? throw new BusinessException($"角色不存在（Id={roleId}）", ApiResultCode.NotFound);

            var relations = await _roleMenuRepository.GetListAsync(x => x.RoleId == roleId.Value);
            // 补全父级链：角色授权可能只勾选了子节点，父目录需一并展示
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
            menus = menus.Where(x => visibleIds.Contains(x.Id)).ToList();
        }

        return BuildTree(menus, 0);
    }

    public async Task<List<MenuTreeDto>> GetTreeByRoleAsync(long roleId) => await GetTreeAsync(roleId);

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
            throw new BusinessException("父级菜单不存在", ApiResultCode.BadRequest);
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
            throw new BusinessException("父级菜单不能是自身", ApiResultCode.BadRequest);
        }
        if (dto.ParentId != 0 && !await Repository.AnyAsync(x => x.Id == dto.ParentId))
        {
            throw new BusinessException("父级菜单不存在", ApiResultCode.BadRequest);
        }
        if (dto.ParentId != 0 && await IsDescendantAsync(id, dto.ParentId))
        {
            throw new BusinessException("父级菜单不能是自身的子孙节点", ApiResultCode.BadRequest);
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
        await Repository.UpdateAsync(menu);
        _permissionService.InvalidateAll();
    }

    public async new Task DeleteAsync(long id, string? operatorName = null)
    {
        var menu = await GetRequiredAsync(id);
        if (await Repository.AnyAsync(x => x.ParentId == id))
        {
            throw new BusinessException("存在子菜单，不允许删除");
        }
        if (await _roleMenuRepository.AnyAsync(x => x.MenuId == id))
        {
            throw new BusinessException("菜单已被角色引用，请先取消角色授权");
        }

        menu.UpdateBy = operatorName;
        menu.UpdateTime = DateTime.Now;
        await Repository.DeleteAsync(menu);
        _permissionService.InvalidateAll();
    }

    private async Task<SysMenu> GetRequiredAsync(long id) =>
        await Repository.GetByIdAsync(id)
        ?? throw new BusinessException($"菜单不存在（Id={id}）", ApiResultCode.NotFound);

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
            throw new BusinessException("菜单名称不能为空", ApiResultCode.BadRequest);
        }
        if (dto.MenuType is < 1 or > 3)
        {
            throw new BusinessException("菜单类型无效（1-目录 2-菜单 3-按钮）", ApiResultCode.BadRequest);
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
