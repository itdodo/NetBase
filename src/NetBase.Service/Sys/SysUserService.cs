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
    private readonly IRepository<SysPosition> _positionRepository;
    private readonly IRepository<SysUserPosition> _userPositionRepository;
    private readonly NetBase.Common.Realtime.INotifyService _notifyService;

    /// <summary>内置管理员账号，不允许停用/删除</summary>
    public const string AdminUserName = "admin";

    public SysUserService(
        IRepository<SysUser> repository,
        IRepository<SysRole> roleRepository,
        IRepository<SysUserRole> userRoleRepository,
        IRepository<SysUserSession> userSessionRepository,
        IRepository<SysPosition> positionRepository,
        IRepository<SysUserPosition> userPositionRepository,
        NetBase.Common.Realtime.INotifyService notifyService,
        IPermissionService permissionService,
        ISysConfigService configService,
        ISysDeptService deptService,
        ICacheService cacheService) : base(repository)
    {
        _roleRepository = roleRepository;
        _notifyService = notifyService;
        _userRoleRepository = userRoleRepository;
        _positionRepository = positionRepository;
        _userPositionRepository = userPositionRepository;
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

    /// <summary>内置超级管理员角色ID（无则 0）</summary>
    private async Task<long> GetAdminRoleIdAsync() =>
        (await _roleRepository.GetFirstAsync(x => x.RoleCode == SysRoleService.AdminRoleCode))?.Id ?? 0;

    /// <summary>
    /// 防提权：向用户授予内置超级管理员角色时，操作者必须是该账号本人。
    /// 否则任何拥有 sys:user:edit 的人都能给自己/他人挂管理员角色实现越权。
    /// </summary>
    private async Task EnsureAdminRoleAssignmentAllowedAsync(IEnumerable<long> roleIds, string? operatorName)
    {
        var adminRoleId = await GetAdminRoleIdAsync();
        if (adminRoleId > 0 && roleIds.Contains(adminRoleId) && operatorName != AdminUserName)
        {
            throw new BusinessException("仅内置管理员可以分配超级管理员角色", ApiResultCode.Forbidden, ErrorCodes.SYS_USER_ADMIN_ROLE_GRANT_FORBIDDEN);
        }
    }

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
            var policyError = PasswordPolicy.Validate(password, defaultPassword);
            if (policyError != null)
            {
                errors.Add($"第 {index} 行：{userName} 密码不合规（{policyError}），已跳过");
                continue;
            }

            var user = new SysUser
            {
                UserName = userName,
                Password = PasswordHelper.Encrypt(password),
                PwdUpdateTime = DateTime.Now,
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
            await EnsureAdminRoleAssignmentAllowedAsync(roleIds, operatorName);
            await SaveUserRolesAsync(user.Id, roleIds);
            await SaveUserPositionsAsync(user.Id, null); // 导入模板无岗位列
                return true;
            });
            successCount++;
        }

        if (successCount > 0)
        {
            await _permissionService.InvalidateAllAsync();
        }
        return (successCount, errors);
    }

    public async Task<UserDto?> GetDetailAsync(long id)
    {
        var user = await Repository.GetByIdAsync(id)
            ?? throw new BusinessException($"用户不存在（Id={id}）", ApiResultCode.NotFound, ErrorCodes.SYS_USER_NOT_FOUND);
        return (await ToDtosAsync([user]))[0];
    }

    public async Task<long> CreateAsync(UserCreateDto dto, string? operatorName = null)
    {
        if (dto.UserName.IsNullOrEmpty())
        {
            throw new BusinessException("用户名不能为空", ApiResultCode.BadRequest, ErrorCodes.SYS_USER_NAME_REQUIRED);
        }

        if (await Repository.AnyAsync(x => x.UserName == dto.UserName))
        {
            throw new BusinessException($"用户名 {dto.UserName} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_USER_NAME_EXISTS);
        }

        if (dto.RoleIds.Count > 0)
        {
            await EnsureRolesExistAsync(dto.RoleIds);
        }

        var defaultPassword = await GetDefaultPasswordAsync();
        var password = dto.Password.IsNullOrEmpty() ? defaultPassword : dto.Password;
        // 密码复杂度服务端强制校验；使用系统配置的默认密码时豁免复杂度
        var policyError = PasswordPolicy.Validate(password, defaultPassword);
        if (policyError != null)
        {
            throw new BusinessException(policyError, ApiResultCode.BadRequest, ErrorCodes.SYS_USER_PWD_POLICY_VIOLATION);
        }
        // 部门选填：传了才校验存在性；未分配落 0（数据权限按"未分配"处理）
        if (dto.DeptId.HasValue && !await DeptExistsAsync(dto.DeptId.Value))
        {
            throw new BusinessException("所属部门不存在", ApiResultCode.BadRequest, ErrorCodes.SYS_USER_DEPT_NOT_FOUND);
        }

        var user = new SysUser
        {
            UserName = dto.UserName,
            Password = PasswordHelper.Encrypt(password),
            PwdUpdateTime = DateTime.Now,
            NickName = dto.NickName,
            Phone = dto.Phone,
            Email = dto.Email,
            Status = dto.Status,
            DeptId = dto.DeptId ?? 0,
            CreateBy = operatorName
        };
        // 用户与角色关联整体事务，避免中途失败产生孤儿数据
        await Repository.TransactionAsync(async () =>
        {
            await Repository.InsertAsync(user);
            // 回填数据归属人（仅本人范围依赖此列）
            await Repository.UpdateWhereAsync(x => x.Id == user.Id, x => new SysUser { OwnerUserId = user.Id });
            await EnsureAdminRoleAssignmentAllowedAsync(dto.RoleIds, operatorName);
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
            throw new BusinessException("不允许停用内置管理员账号", ErrorCodes.SYS_USER_ADMIN_DISABLE_FORBIDDEN);
        }

        // 部门选填：传了才校验存在性；清空部门 = 未分配（0）
        if (dto.DeptId.HasValue && !await DeptExistsAsync(dto.DeptId.Value))
        {
            throw new BusinessException("所属部门不存在", ApiResultCode.BadRequest, ErrorCodes.SYS_USER_DEPT_NOT_FOUND);
        }

        // 防提权/防降权：涉及超级管理员角色（授予任何用户）或修改 admin 账号的角色，仅限内置管理员本人
        var adminRoleId = await GetAdminRoleIdAsync();
        var targetIsAdmin = user.UserName == AdminUserName;
        if (dto.RoleIds != null)
        {
            await EnsureAdminRoleAssignmentAllowedAsync(dto.RoleIds, operatorName);
            if (targetIsAdmin && operatorName != AdminUserName)
            {
                throw new BusinessException("内置管理员账号的角色仅允许本人修改", ApiResultCode.Forbidden, ErrorCodes.SYS_USER_ADMIN_ROLE_SELF_ONLY);
            }
        }

        user.NickName = dto.NickName;
        user.Phone = dto.Phone;
        user.Email = dto.Email;
        user.Status = dto.Status;
        user.DeptId = dto.DeptId ?? 0;
        user.UpdateTime = DateTime.Now;
        user.UpdateBy = operatorName;
        // 停用用户立即踢下线并通知
        if (dto.Status != (int)StatusEnum.Enabled)
        {
            await _userSessionRepository.DeleteWhereAsync(x => x.UserId == id);
            await _notifyService.PushForceLogoutAsync(id, "账号已被停用，如有疑问请联系管理员");
        }
        await Repository.TransactionAsync(async () =>
        {
            // 跨会话陈旧检测：客户端回传读取时的版本参与比对（未传则用现读版本，兼容旧客户端）
            user.Version = dto.Version ?? user.Version;
            await UpdateWithConcurrencyCheckAsync(user);
            if (dto.RoleIds != null)
            {
                await SaveUserRolesAsync(id, dto.RoleIds);
            await SaveUserPositionsAsync(id, dto.PositionIds);
            }
            return true;
        });
        await _permissionService.InvalidateAllAsync();
    }

    public async new Task DeleteAsync(long id, string? operatorName = null)
    {
        var user = await GetRequiredAsync(id);
        if (user.UserName == AdminUserName)
        {
            throw new BusinessException("不允许删除内置管理员账号", ErrorCodes.SYS_USER_ADMIN_DELETE_FORBIDDEN);
        }

        user.UpdateBy = operatorName;
        user.UpdateTime = DateTime.Now;
        // 停用/删除用户立即踢下线并通知
        await _userSessionRepository.DeleteWhereAsync(x => x.UserId == id);
        await _notifyService.PushForceLogoutAsync(id, "账号已被删除，如有疑问请联系管理员");
        await Repository.TransactionAsync(async () =>
        {
            await Repository.DeleteAsync(user);
            await _userRoleRepository.DeleteWhereAsync(x => x.UserId == id);
            return true;
        });
        await _permissionService.InvalidateAllAsync();
    }

    public async Task ResetPasswordAsync(long id, string? newPassword, string? operatorName = null)
    {
        var user = await GetRequiredAsync(id);
        // 防接管：内置管理员账号的密码不允许被他人重置（本人走修改密码接口，需验旧密码）
        if (user.UserName == AdminUserName && operatorName != AdminUserName)
        {
            throw new BusinessException("内置管理员账号的密码不允许重置", ApiResultCode.Forbidden, ErrorCodes.SYS_USER_ADMIN_RESET_FORBIDDEN);
        }
        var password = newPassword.IsNullOrEmpty() ? await GetDefaultPasswordAsync() : newPassword;
        var defaultPassword = await GetDefaultPasswordAsync();
        // 密码复杂度服务端强制校验；使用系统默认密码时豁免（默认密码由管理员掌控）
        var policyError = PasswordPolicy.Validate(password, defaultPassword);
        if (policyError != null)
        {
            throw new BusinessException(policyError, ApiResultCode.BadRequest, ErrorCodes.SYS_USER_PWD_POLICY_VIOLATION);
        }
        // 哈希在表达式外计算，闭包变量会被 SqlSugar 参数化；静态方法调用放入表达式树无法翻译
        var hashed = PasswordHelper.Encrypt(password);
        await Repository.UpdateWhereAsync(
            x => x.Id == id,
            x => new SysUser { Password = hashed, PwdUpdateTime = DateTime.Now, UpdateTime = DateTime.Now, UpdateBy = operatorName });
        // 重置密码后清除该用户全部会话与登录失败计数，强制重新登录
        // （先清会话有效性标记再删行，删后查不到 tokenId）
        var userSessions = await _userSessionRepository.GetListAsync(x => x.UserId == id);
        foreach (var s2 in userSessions)
        {
            await _cacheService.RemoveAsync($"session:valid:{s2.TokenId}");
        }
        await _userSessionRepository.DeletePhysicalWhereAsync(x => x.UserId == id);
        var failKey = $"login:fail:{user.UserName.ToLowerInvariant()}";
        var lockKey = $"login:lock:{user.UserName.ToLowerInvariant()}";
        await _cacheService.RemoveAsync(failKey);
        await _cacheService.RemoveAsync(lockKey);
    }

    public async Task AssignRolesAsync(long userId, List<long> roleIds, string? operatorName = null)
    {
        _ = await GetRequiredAsync(userId);
        if (roleIds.Count > 0)
        {
            await EnsureRolesExistAsync(roleIds);
        }
        await EnsureAdminRoleAssignmentAllowedAsync(roleIds, operatorName);
        await Repository.TransactionAsync(async () =>
        {
            await SaveUserRolesAsync(userId, roleIds);
            return true;
        });
        await _permissionService.InvalidateAllAsync();
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
        var userPositions = await _userPositionRepository.GetListAsync(x => userIds.Contains(x.UserId));
        var positionIds = userPositions.Select(x => x.PositionId).Distinct().ToList();
        var positions = positionIds.Count == 0
            ? []
            : await _positionRepository.GetListAsync(x => positionIds.Contains(x.Id));
        var positionNamesByUser = userPositions
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.PositionId).ToList())
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
                    .Select(pid => positions.FirstOrDefault(p => p.Id == pid)?.PositionName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Cast<string>()
                    .ToList());

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
            Version = x.Version,
            Roles = userRoles.Where(ur => ur.UserId == x.Id && roleMap.ContainsKey(ur.RoleId))
                .Select(ur => roleMap[ur.RoleId])
                .ToList(),
            Positions = positionNamesByUser.TryGetValue(x.Id, out var pn) ? pn : []
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

    /// <summary>全量重设用户岗位（null = 不动）</summary>
    private async Task SaveUserPositionsAsync(long userId, List<long>? positionIds)
    {
        if (positionIds == null) return;
        var distinct = positionIds.Distinct().ToList();
        await _userPositionRepository.DeleteWhereAsync(x => x.UserId == userId);
        if (distinct.Count > 0)
        {
            await _userPositionRepository.InsertRangeAsync(distinct.Select(positionId => new SysUserPosition
            {
                UserId = userId,
                PositionId = positionId
            }));
        }
    }

    private async Task EnsureRolesExistAsync(List<long> roleIds)
    {
        var existCount = await _roleRepository.CountAsync(x => roleIds.Contains(x.Id));
        if (existCount != roleIds.Distinct().Count())
        {
            throw new BusinessException("存在无效的角色ID", ApiResultCode.BadRequest, ErrorCodes.SYS_USER_ROLE_INVALID);
        }
    }
}
