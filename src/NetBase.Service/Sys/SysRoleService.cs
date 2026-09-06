using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Model.Enums;
using NetBase.Repository.Repositories;
using NetBase.Service.Base;
using SqlSugar;
using System.Linq.Expressions;

namespace NetBase.Service.Sys;

/// <summary>角色业务实现</summary>
public class SysRoleService : BaseService<SysRole>, ISysRoleService
{
    /// <summary>内置管理员角色编码，不允许停用/删除</summary>
    public const string AdminRoleCode = "admin";

    private readonly IRepository<SysUserRole> _userRoleRepository;
    private readonly IRepository<SysRoleMenu> _roleMenuRepository;
    private readonly IRepository<SysMenu> _menuRepository;
    private readonly IRepository<SysRoleDept> _roleDeptRepository;
    private readonly IPermissionService _permissionService;

    public SysRoleService(
        IRepository<SysRole> repository,
        IRepository<SysUserRole> userRoleRepository,
        IRepository<SysRoleMenu> roleMenuRepository,
        IRepository<SysMenu> menuRepository,
        IRepository<SysRoleDept> roleDeptRepository,
        IPermissionService permissionService) : base(repository)
    {
        _userRoleRepository = userRoleRepository;
        _roleMenuRepository = roleMenuRepository;
        _menuRepository = menuRepository;
        _roleDeptRepository = roleDeptRepository;
        _permissionService = permissionService;
    }

    public async Task<PageResult<RoleDto>> GetPageListAsync(RoleQueryDto query)
    {
        var predicate = BuildPredicate(query);
        var page = await Repository.GetPageListAsync(predicate, query);
        var items = page.Items.Select(ToDto).ToList();
        return PageResult<RoleDto>.Of(items, page.Total, page.PageIndex, page.PageSize);
    }

    public async Task<List<RoleSimpleDto>> GetAllEnabledAsync()
    {
        var roles = await Repository.GetListAsync(x => x.Status == (int)StatusEnum.Enabled);
        return roles.OrderBy(x => x.Sort)
            .Select(x => new RoleSimpleDto { Id = x.Id, RoleName = x.RoleName, RoleCode = x.RoleCode })
            .ToList();
    }

    /// <summary>按查询条件取全量角色（导出用，不分页）</summary>
    public async Task<List<RoleDto>> GetExportListAsync(RoleQueryDto query)
    {
        var predicate = BuildPredicate(query);
        var roles = await Repository.GetListAsync(predicate);
        return roles.OrderBy(x => x.Sort).Select(ToDto).ToList();
    }

    public async Task<RoleDto?> GetDetailAsync(long id)
    {
        var role = await Repository.GetByIdAsync(id);
        return role == null ? null : ToDto(role);
    }

    public async Task<long> CreateAsync(RoleSaveDto dto, string? operatorName = null)
    {
        Validate(dto);
        if (await Repository.AnyAsync(x => x.RoleCode == dto.RoleCode))
        {
            throw new BusinessException($"角色编码 {dto.RoleCode} 已存在", ApiResultCode.BadRequest);
        }

        var role = new SysRole
        {
            RoleName = dto.RoleName,
            RoleCode = dto.RoleCode,
            Status = dto.Status,
            Sort = dto.Sort,
            DataScope = dto.DataScope,
            CreateBy = operatorName
        };
        await Repository.InsertAsync(role);
        if (dto.DataScope == (int)DataScopeEnum.Custom)
        {
            await SaveRoleDeptsAsync(role.Id, dto.DeptIds);
        }
        return role.Id;
    }

    public async Task UpdateAsync(long id, RoleSaveDto dto, string? operatorName = null)
    {
        var role = await GetRequiredAsync(id);
        if (role.RoleCode == AdminRoleCode && dto.Status != (int)StatusEnum.Enabled)
        {
            throw new BusinessException("不允许停用内置管理员角色");
        }

        Validate(dto);
        if (await Repository.AnyAsync(x => x.RoleCode == dto.RoleCode && x.Id != id))
        {
            throw new BusinessException($"角色编码 {dto.RoleCode} 已存在", ApiResultCode.BadRequest);
        }

        role.RoleName = dto.RoleName;
        role.RoleCode = dto.RoleCode;
        role.Status = dto.Status;
        role.Sort = dto.Sort;
        role.DataScope = dto.DataScope;
        role.UpdateTime = DateTime.Now;
        role.UpdateBy = operatorName;
        await Repository.UpdateAsync(role);

        // 数据权限=自定义时重设部门勾选；其他档清空勾选
        if (dto.DataScope == (int)DataScopeEnum.Custom && dto.DeptIds != null)
        {
            await SaveRoleDeptsAsync(id, dto.DeptIds);
        }
        else if (dto.DataScope != (int)DataScopeEnum.Custom)
        {
            await _roleDeptRepository.DeleteWhereAsync(x => x.RoleId == id);
        }
    }

    private async Task SaveRoleDeptsAsync(long roleId, List<long>? deptIds)
    {
        var distinct = (deptIds ?? []).Distinct().ToList();
        await _roleDeptRepository.DeleteWhereAsync(x => x.RoleId == roleId);
        if (distinct.Count > 0)
        {
            await _roleDeptRepository.InsertRangeAsync(distinct.Select(deptId => new SysRoleDept
            {
                RoleId = roleId,
                DeptId = deptId
            }));
        }
    }

    public async new Task DeleteAsync(long id, string? operatorName = null)
    {
        var role = await GetRequiredAsync(id);
        if (role.RoleCode == AdminRoleCode)
        {
            throw new BusinessException("不允许删除内置管理员角色");
        }

        role.UpdateBy = operatorName;
        role.UpdateTime = DateTime.Now;
        // 角色、用户角色、角色菜单三表整体事务
        await Repository.TransactionAsync(async () =>
        {
            await Repository.DeleteAsync(role);
            await _userRoleRepository.DeleteWhereAsync(x => x.RoleId == id);
            await _roleMenuRepository.DeleteWhereAsync(x => x.RoleId == id);
            await _roleDeptRepository.DeleteWhereAsync(x => x.RoleId == id);
            return true;
        });
        _permissionService.InvalidateAll();
    }

    public async Task AssignMenusAsync(long roleId, List<long> menuIds)
    {
        _ = await GetRequiredAsync(roleId);
        var distinct = menuIds.Distinct().ToList();
        if (distinct.Count > 0)
        {
            var existCount = await _menuRepository.CountAsync(x => distinct.Contains(x.Id));
            if (existCount != distinct.Count)
            {
                throw new BusinessException("存在无效的菜单ID", ApiResultCode.BadRequest);
            }
        }

        // 先删后插整体事务，避免中途失败丢失角色全部菜单授权
        await Repository.TransactionAsync(async () =>
        {
            await _roleMenuRepository.DeleteWhereAsync(x => x.RoleId == roleId);
            if (distinct.Count > 0)
            {
                await _roleMenuRepository.InsertRangeAsync(distinct.Select(menuId => new SysRoleMenu
                {
                    RoleId = roleId,
                    MenuId = menuId
                }));
            }
            return true;
        });
        _permissionService.InvalidateAll();
    }

    public async Task<List<long>> GetMenuIdsAsync(long roleId)
    {
        _ = await GetRequiredAsync(roleId);
        var relations = await _roleMenuRepository.GetListAsync(x => x.RoleId == roleId);
        return relations.Select(x => x.MenuId).ToList();
    }

    public async Task<List<long>> GetRoleDeptIdsAsync(long roleId)
    {
        _ = await GetRequiredAsync(roleId);
        var relations = await _roleDeptRepository.GetListAsync(x => x.RoleId == roleId);
        return relations.Select(x => x.DeptId).ToList();
    }

    private async Task<SysRole> GetRequiredAsync(long id) =>
        await Repository.GetByIdAsync(id)
        ?? throw new BusinessException($"角色不存在（Id={id}）", ApiResultCode.NotFound);

    private static void Validate(RoleSaveDto dto)
    {
        if (dto.RoleName.IsNullOrWhiteSpace())
        {
            throw new BusinessException("角色名称不能为空", ApiResultCode.BadRequest);
        }
        if (dto.RoleCode.IsNullOrWhiteSpace())
        {
            throw new BusinessException("角色编码不能为空", ApiResultCode.BadRequest);
        }
    }

    private Expression<Func<SysRole, bool>>? BuildPredicate(RoleQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        return Expressionable.Create<SysRole>()
            .AndIF(keyword.IsNotNullOrEmpty(), x => x.RoleName.Contains(keyword!) || x.RoleCode.Contains(keyword!))
            .AndIF(query.Status.HasValue, x => x.Status == query.Status!.Value)
            .ToExpression();
    }

    private static RoleDto ToDto(SysRole role) => new()
    {
        Id = role.Id,
        RoleName = role.RoleName,
        RoleCode = role.RoleCode,
        Status = role.Status,
        Sort = role.Sort,
        DataScope = role.DataScope,
        CreateTime = role.CreateTime
    };
}
