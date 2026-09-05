using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Common.Security;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Model.Enums;
using NetBase.Repository.Repositories;
using NetBase.Service.Base;
using SqlSugar;
using System.Linq.Expressions;

namespace NetBase.Service.Sys;

/// <summary>用户业务实现</summary>
public class SysUserService : BaseService<SysUser>, ISysUserService
{
    private readonly IRepository<SysRole> _roleRepository;
    private readonly IRepository<SysUserRole> _userRoleRepository;

    /// <summary>内置管理员账号，不允许停用/删除</summary>
    public const string AdminUserName = "admin";

    public SysUserService(
        IRepository<SysUser> repository,
        IRepository<SysRole> roleRepository,
        IRepository<SysUserRole> userRoleRepository) : base(repository)
    {
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
    }

    public async Task<PageResult<UserDto>> GetPageListAsync(UserQueryDto query)
    {
        var predicate = BuildPredicate(query);
        var page = await Repository.GetPageListAsync(predicate, query);
        var result = PageResult<UserDto>.Of(
            await ToDtosAsync(page.Items), page.Total, page.PageIndex, page.PageSize);
        return result;
    }

    public async Task<List<UserDto>> GetAllEnabledAsync()
    {
        var users = await Repository.GetListAsync(x => x.Status == (int)StatusEnum.Enabled);
        return await ToDtosAsync(users);
    }

    public async Task<UserDto?> GetDetailAsync(long id)
    {
        var user = await Repository.GetByIdAsync(id);
        return user == null ? null : (await ToDtosAsync([user]))[0];
    }

    public async Task<long> CreateAsync(UserCreateDto dto, string? operatorName = null)
    {
        if (dto.UserName.IsNullOrEmpty())
        {
            throw new BusinessException("用户名不能为空", ApiResultCode.BadRequest);
        }

        if (await Repository.AnyAsync(x => x.UserName == dto.UserName))
        {
            throw new BusinessException($"用户名 {dto.UserName} 已存在", ApiResultCode.BadRequest);
        }

        if (dto.RoleIds.Count > 0)
        {
            await EnsureRolesExistAsync(dto.RoleIds);
        }

        var user = new SysUser
        {
            UserName = dto.UserName,
            Password = PasswordHelper.Encrypt(dto.Password.IsNullOrEmpty() ? PasswordHelper.DefaultPassword : dto.Password),
            NickName = dto.NickName,
            Phone = dto.Phone,
            Email = dto.Email,
            Status = dto.Status,
            CreateBy = operatorName
        };
        // 用户与角色关联整体事务，避免中途失败产生孤儿数据
        await Repository.TransactionAsync(async () =>
        {
            await Repository.InsertAsync(user);
            await SaveUserRolesAsync(user.Id, dto.RoleIds);
            return true;
        });
        return user.Id;
    }

    public async Task UpdateAsync(long id, UserUpdateDto dto, string? operatorName = null)
    {
        var user = await GetRequiredAsync(id);

        if (user.UserName == AdminUserName && dto.Status != (int)StatusEnum.Enabled)
        {
            throw new BusinessException("不允许停用内置管理员账号");
        }

        user.NickName = dto.NickName;
        user.Phone = dto.Phone;
        user.Email = dto.Email;
        user.Status = dto.Status;
        user.UpdateTime = DateTime.Now;
        user.UpdateBy = operatorName;
        await Repository.TransactionAsync(async () =>
        {
            await Repository.UpdateAsync(user);
            if (dto.RoleIds != null)
            {
                await SaveUserRolesAsync(id, dto.RoleIds);
            }
            return true;
        });
    }

    public async new Task DeleteAsync(long id, string? operatorName = null)
    {
        var user = await GetRequiredAsync(id);
        if (user.UserName == AdminUserName)
        {
            throw new BusinessException("不允许删除内置管理员账号");
        }

        user.UpdateBy = operatorName;
        user.UpdateTime = DateTime.Now;
        await Repository.TransactionAsync(async () =>
        {
            await Repository.DeleteAsync(user);
            await _userRoleRepository.DeleteWhereAsync(x => x.UserId == id);
            return true;
        });
    }

    public async Task ResetPasswordAsync(long id, string? newPassword, string? operatorName = null)
    {
        _ = await GetRequiredAsync(id);
        var password = newPassword.IsNullOrEmpty() ? PasswordHelper.DefaultPassword : newPassword;
        // 哈希在表达式外计算，闭包变量会被 SqlSugar 参数化；静态方法调用放入表达式树无法翻译
        var hashed = PasswordHelper.Encrypt(password);
        await Repository.UpdateWhereAsync(
            x => x.Id == id,
            x => new SysUser { Password = hashed, UpdateTime = DateTime.Now, UpdateBy = operatorName });
    }

    public async Task AssignRolesAsync(long userId, List<long> roleIds)
    {
        _ = await GetRequiredAsync(userId);
        if (roleIds.Count > 0)
        {
            await EnsureRolesExistAsync(roleIds);
        }
        await Repository.TransactionAsync(async () =>
        {
            await SaveUserRolesAsync(userId, roleIds);
            return true;
        });
    }

    public Task<SysUser?> GetByUserNameAsync(string userName) =>
        Repository.GetFirstAsync(x => x.UserName == userName);

    private async Task<SysUser> GetRequiredAsync(long id) =>
        await Repository.GetByIdAsync(id)
        ?? throw new BusinessException($"用户不存在（Id={id}）", ApiResultCode.NotFound);

    private Expression<Func<SysUser, bool>>? BuildPredicate(UserQueryDto query)
    {
        var hasCondition = false;
        var exp = Expressionable.Create<SysUser>();
        if (query.Keyword.IsNotNullOrEmpty())
        {
            hasCondition = true;
            var keyword = query.Keyword!.Trim();
            exp.And(x => x.UserName.Contains(keyword) || (x.NickName != null && x.NickName.Contains(keyword)));
        }
        if (query.Status.HasValue)
        {
            hasCondition = true;
            var status = query.Status.Value;
            exp.And(x => x.Status == status);
        }
        return hasCondition ? exp.ToExpression() : null;
    }

    private async Task<List<UserDto>> ToDtosAsync(List<SysUser> users)
    {
        if (users.Count == 0)
        {
            return [];
        }

        var userIds = users.Select(x => x.Id).ToList();
        var userRoles = await _userRoleRepository.GetListAsync(x => userIds.Contains(x.UserId));
        var roleIds = userRoles.Select(x => x.RoleId).Distinct().ToList();
        var roles = roleIds.Count == 0
            ? []
            : await _roleRepository.GetListAsync(x => roleIds.Contains(x.Id));
        var roleMap = roles.ToDictionary(x => x.Id, x => new RoleSimpleDto
        {
            Id = x.Id,
            RoleName = x.RoleName,
            RoleCode = x.RoleCode
        });

        return users.Select(x => new UserDto
        {
            Id = x.Id,
            UserName = x.UserName,
            NickName = x.NickName,
            Phone = x.Phone,
            Email = x.Email,
            Status = x.Status,
            LastLoginTime = x.LastLoginTime,
            CreateTime = x.CreateTime,
            Roles = userRoles.Where(ur => ur.UserId == x.Id && roleMap.ContainsKey(ur.RoleId))
                .Select(ur => roleMap[ur.RoleId])
                .ToList()
        }).ToList();
    }

    private async Task SaveUserRolesAsync(long userId, List<long> roleIds)
    {
        var distinct = roleIds.Distinct().ToList();
        await _userRoleRepository.DeleteWhereAsync(x => x.UserId == userId);
        if (distinct.Count > 0)
        {
            await _userRoleRepository.InsertRangeAsync(distinct.Select(roleId => new SysUserRole
            {
                UserId = userId,
                RoleId = roleId
            }));
        }
    }

    private async Task EnsureRolesExistAsync(List<long> roleIds)
    {
        var existCount = await _roleRepository.CountAsync(x => roleIds.Contains(x.Id));
        if (existCount != roleIds.Distinct().Count())
        {
            throw new BusinessException("存在无效的角色ID", ApiResultCode.BadRequest);
        }
    }
}
