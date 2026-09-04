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

    public SysRoleService(
        IRepository<SysRole> repository,
        IRepository<SysUserRole> userRoleRepository,
        IRepository<SysRoleMenu> roleMenuRepository,
        IRepository<SysMenu> menuRepository) : base(repository)
    {
        _userRoleRepository = userRoleRepository;
        _roleMenuRepository = roleMenuRepository;
        _menuRepository = menuRepository;
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
            CreateBy = operatorName
        };
        await Repository.InsertAsync(role);
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
        role.UpdateTime = DateTime.Now;
        role.UpdateBy = operatorName;
        await Repository.UpdateAsync(role);
    }

    public async new Task DeleteAsync(long id)
    {
        var role = await GetRequiredAsync(id);
        if (role.RoleCode == AdminRoleCode)
        {
            throw new BusinessException("不允许删除内置管理员角色");
        }

        await Repository.DeleteAsync(id);
        await _userRoleRepository.DeleteWhereAsync(x => x.RoleId == id);
        await _roleMenuRepository.DeleteWhereAsync(x => x.RoleId == id);
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

        await _roleMenuRepository.DeleteWhereAsync(x => x.RoleId == roleId);
        if (distinct.Count > 0)
        {
            await _roleMenuRepository.InsertRangeAsync(distinct.Select(menuId => new SysRoleMenu
            {
                RoleId = roleId,
                MenuId = menuId
            }));
        }
    }

    public async Task<List<long>> GetMenuIdsAsync(long roleId)
    {
        _ = await GetRequiredAsync(roleId);
        var relations = await _roleMenuRepository.GetListAsync(x => x.RoleId == roleId);
        return relations.Select(x => x.MenuId).ToList();
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
        var hasCondition = false;
        var exp = Expressionable.Create<SysRole>();
        if (query.Keyword.IsNotNullOrEmpty())
        {
            hasCondition = true;
            var keyword = query.Keyword!.Trim();
            exp.And(x => x.RoleName.Contains(keyword) || x.RoleCode.Contains(keyword));
        }
        if (query.Status.HasValue)
        {
            hasCondition = true;
            var status = query.Status.Value;
            exp.And(x => x.Status == status);
        }
        return hasCondition ? exp.ToExpression() : null;
    }

    private static RoleDto ToDto(SysRole role) => new()
    {
        Id = role.Id,
        RoleName = role.RoleName,
        RoleCode = role.RoleCode,
        Status = role.Status,
        Sort = role.Sort,
        CreateTime = role.CreateTime
    };
}
