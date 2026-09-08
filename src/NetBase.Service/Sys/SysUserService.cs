using ICacheService = NetBase.Common.Cache.ICacheService;
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
        IRepository<SysUserRole> userRoleRepository,
        IRepository<SysUserSession> userSessionRepository,
        IPermissionService permissionService,
        ISysConfigService configService,
        ISysDeptService deptService,
        ICacheService cacheService) : base(repository)
    {
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _userSessionRepository = userSessionRepository;
        _permissionService = permissionService;
        _configService = configService;
        _deptService = deptService;
        _cacheService = cacheService;
    }

    private readonly IRepository<SysUserSession> _userSessionRepository;
    private readonly IPermissionService _permissionService;
    private readonly ISysConfigService _configService;
    private readonly ISysDeptService _deptService;
    private readonly ICacheService _cacheService;

    /// <summary>默认密码：优先取系统参数 sys.pwd.defaultPassword，未配置回退内置值</summary>
    private async Task<bool> DeptExistsAsync(long deptId) => await _deptService.GetDetailAsync(deptId) != null;

    private async Task<string> GetDefaultPasswordAsync()
    {
        var configured = await _configService.GetConfigValueAsync("sys.pwd.defaultPassword");
        return configured.IsNotNullOrEmpty() ? configured : PasswordHelper.DefaultPassword;
    }

    public async Task<PageResult<UserDto>> GetPageListAsync(UserQueryDto query)
    {
        var predicate = await BuildPredicateAsync(query);
        var page = await Repository.GetPageListAsync(predicate, query);
        var result = PageResult<UserDto>.Of(
            await ToDtosAsync(page.Items), page.Total, page.PageIndex, page.PageSize);
        return result;
    }

    /// <summary>按查询条件取全量用户（导出用，不分页）</summary>
    public async Task<List<UserDto>> GetExportListAsync(UserQueryDto query)
    {
        var users = await Repository.GetListAsync(await BuildPredicateAsync(query));
        return await ToDtosAsync(users);
    }

    public async Task<List<UserDto>> GetAllEnabledAsync()
    {
        var users = await Repository.GetListAsync(x => x.Status == (int)StatusEnum.Enabled);
        return await ToDtosAsync(users);
    }

    public async Task<(int SuccessCount, List<string> Errors)> ImportAsync(List<UserImportRow> rows, string? operatorName)
    {
        var errors = new List<string>();
        var successCount = 0;
        var defaultPassword = await GetDefaultPasswordAsync();

        // 预载角色编码映射（导入行按编码关联角色）
        var allRoles = await _roleRepository.GetListAsync();
        var roleMap = allRoles.ToDictionary(x => x.RoleCode, x => x.Id);

        var seenUserNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (row, index) in rows.Select((r, i) => (r, i + 2)))  // Excel 数据从第 2 行起
        {
            var userName = row.UserName?.Trim() ?? string.Empty;
            if (userName.IsNullOrEmpty())
            {
                errors.Add($"第 {index} 行：用户名为空，已跳过");
                continue;
            }
            if (!seenUserNames.Add(userName))
            {
                errors.Add($"第 {index} 行：用户名 {userName} 在文件内重复，已跳过");
                continue;
            }
            if (await Repository.AnyAsync(x => x.UserName == userName))
            {
                errors.Add($"第 {index} 行：用户名 {userName} 已存在，已跳过");
                continue;
            }

            var password = row.Password.IsNullOrEmpty() ? defaultPassword : row.Password;
            var policyError = PasswordPolicy.Validate(password);
            if (policyError != null)
            {
                errors.Add($"第 {index} 行：{userName} 密码不合规（{policyError}），已跳过");
                continue;
            }

            var user = new SysUser
            {
                UserName = userName,
                Password = PasswordHelper.Encrypt(password),
                NickName = row.NickName,
                Phone = row.Phone,
                Email = row.Email,
                Status = (int)StatusEnum.Enabled,
                CreateBy = operatorName
            };

            // 角色编码解析：无效编码写入失败明细（不阻断用户导入）
            var roleIds = new List<long>();
            foreach (var rc in (row.RoleCodes ?? string.Empty)
                         .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (roleMap.TryGetValue(rc, out var roleId))
                {
                    roleIds.Add(roleId);
                }
                else
                {
                    errors.Add($"第 {index} 行：{userName} 的角色编码 {rc} 无效，已忽略该角色");
                }
            }

            await Repository.TransactionAsync(async () =>
            {
                await Repository.InsertAsync(user);
                if (roleIds.Count > 0)
                {
                    await SaveUserRolesAsync(user.Id, roleIds);
                }
                return true;
            });
            successCount++;
        }

        if (successCount > 0)
        {
            _permissionService.InvalidateAll();
        }
        return (successCount, errors);
    }

    public async Task<UserDto?> GetDetailAsync(long id)
    {
        var user = await Repository.GetByIdAsync(id)
            ?? throw new BusinessException($"用户不存在（Id={id}）", ApiResultCode.NotFound, "SYS_USER_NOT_FOUND");
        return (await ToDtosAsync([user]))[0];
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

        var password = dto.Password.IsNullOrEmpty() ? await GetDefaultPasswordAsync() : dto.Password;
        // 密码复杂度服务端强制校验（默认密码本身满足策略）
        var policyError = PasswordPolicy.Validate(password);
        if (policyError != null)
        {
            throw new BusinessException(policyError, ApiResultCode.BadRequest);
        }
        if (!await DeptExistsAsync(dto.DeptId))
        {
            throw new BusinessException("所属部门不存在", ApiResultCode.BadRequest);
        }

        var user = new SysUser
        {
            UserName = dto.UserName,
            Password = PasswordHelper.Encrypt(password),
            NickName = dto.NickName,
            Phone = dto.Phone,
            Email = dto.Email,
            Status = dto.Status,
            DeptId = dto.DeptId,
            CreateBy = operatorName
        };
        // 用户与角色关联整体事务，避免中途失败产生孤儿数据
        await Repository.TransactionAsync(async () =>
        {
            await Repository.InsertAsync(user);
            // 回填数据归属人（仅本人范围依赖此列）
            await Repository.UpdateWhereAsync(x => x.Id == user.Id, x => new SysUser { OwnerUserId = user.Id });
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

        if (!await DeptExistsAsync(dto.DeptId))
        {
            throw new BusinessException("所属部门不存在", ApiResultCode.BadRequest);
        }

        user.NickName = dto.NickName;
        user.Phone = dto.Phone;
        user.Email = dto.Email;
        user.Status = dto.Status;
        user.DeptId = dto.DeptId;
        user.UpdateTime = DateTime.Now;
        user.UpdateBy = operatorName;
        // 停用用户立即踢下线
        if (dto.Status != (int)StatusEnum.Enabled)
        {
            await _userSessionRepository.DeleteWhereAsync(x => x.UserId == id);
        }
        await Repository.TransactionAsync(async () =>
        {
            await Repository.UpdateAsync(user);
            if (dto.RoleIds != null)
            {
                await SaveUserRolesAsync(id, dto.RoleIds);
            }
            return true;
        });
        _permissionService.InvalidateAll();
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
        // 停用/删除用户立即踢下线：清除其全部会话
        await _userSessionRepository.DeleteWhereAsync(x => x.UserId == id);
        await Repository.TransactionAsync(async () =>
        {
            await Repository.DeleteAsync(user);
            await _userRoleRepository.DeleteWhereAsync(x => x.UserId == id);
            return true;
        });
        _permissionService.InvalidateAll();
    }

    public async Task ResetPasswordAsync(long id, string? newPassword, string? operatorName = null)
    {
        var user = await GetRequiredAsync(id);
        var password = newPassword.IsNullOrEmpty() ? await GetDefaultPasswordAsync() : newPassword;
        // 密码复杂度服务端强制校验（管理员重置同样受策略约束）
        var policyError = PasswordPolicy.Validate(password);
        if (policyError != null)
        {
            throw new BusinessException(policyError, ApiResultCode.BadRequest);
        }
        // 哈希在表达式外计算，闭包变量会被 SqlSugar 参数化；静态方法调用放入表达式树无法翻译
        var hashed = PasswordHelper.Encrypt(password);
        await Repository.UpdateWhereAsync(
            x => x.Id == id,
            x => new SysUser { Password = hashed, UpdateTime = DateTime.Now, UpdateBy = operatorName });
        // 重置密码后清除该用户全部会话与登录失败计数，强制重新登录
        await _userSessionRepository.DeletePhysicalWhereAsync(x => x.UserId == id);
        var failKey = $"netbase:login:fail:{user.UserName.ToLowerInvariant()}";
        var lockKey = $"netbase:login:lock:{user.UserName.ToLowerInvariant()}";
        _cacheService.Remove(failKey);
        _cacheService.Remove(lockKey);
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
        _permissionService.InvalidateAll();
    }

    public Task<SysUser?> GetByUserNameAsync(string userName) =>
        Repository.GetFirstAsync(x => x.UserName == userName);

    public async Task UpdateProfileAsync(long userId, UpdateProfileDto dto)
    {
        var user = await GetRequiredAsync(userId);
        user.NickName = dto.NickName;
        user.Phone = dto.Phone;
        user.Email = dto.Email;
        user.UpdateTime = DateTime.Now;
        user.UpdateBy = user.UserName;
        await Repository.UpdateAsync(user);
    }

    public async Task SetAvatarAsync(long userId, string avatarUrl)
    {
        await Repository.UpdateWhereAsync(
            x => x.Id == userId,
            x => new SysUser { Avatar = avatarUrl, UpdateTime = DateTime.Now });
    }

    private Task<SysUser> GetRequiredAsync(long id) =>
        GetRequiredAsync(id, $"用户不存在（Id={id}）", "SYS_USER_NOT_FOUND");

    private async Task<Expression<Func<SysUser, bool>>> BuildPredicateAsync(UserQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        var deptIds = query.DeptId.HasValue ? await _deptService.GetDeptAndChildIdsAsync(query.DeptId.Value) : null;
        return Expressionable.Create<SysUser>()
            .AndIF(keyword.IsNotNullOrEmpty(), x => x.UserName.Contains(keyword!) || (x.NickName != null && x.NickName.Contains(keyword!)))
            .AndIF(query.Status.HasValue, x => x.Status == query.Status!.Value)
            .AndIF(deptIds != null, x => deptIds!.Contains(x.DeptId))
            .ToExpression();
    }

    private async Task<List<UserDto>> ToDtosAsync(List<SysUser> users)
    {
        if (users.Count == 0)
        {
            return [];
        }

        var userIds = users.Select(x => x.Id).ToList();
        var deptIds = users.Select(x => x.DeptId).Distinct().ToList();
        var depts = deptIds.Count == 0 ? [] : await _deptService.GetAllDeptsAsync();
        var deptMap = depts.Where(d => deptIds.Contains(d.Id)).ToDictionary(d => d.Id, d => d.DeptName);
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
            Avatar = x.Avatar,
            DeptId = x.DeptId,
            DeptName = deptMap.TryGetValue(x.DeptId, out var deptName) ? deptName : null,
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
