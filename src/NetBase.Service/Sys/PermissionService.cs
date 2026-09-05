using Microsoft.Extensions.Logging;
using NetBase.Common.Cache;
using NetBase.Common.Results;
using NetBase.Repository.Repositories;
using NetBase.Model.Entities;

namespace NetBase.Service.Sys;

/// <summary>
/// 用户权限码服务（角色 → 菜单权限码），带进程内缓存。
/// 角色菜单/用户角色变更时调用 InvalidateAll 使缓存整体失效。
/// </summary>
public interface IPermissionService
{
    /// <summary>获取用户全部权限码（如 sys:user:add）</summary>
    Task<HashSet<string>> GetUserPermissionsAsync(long userId);

    /// <summary>清除全部用户权限缓存（角色授权、菜单变更后调用）</summary>
    void InvalidateAll();
}

public class PermissionService : IPermissionService
{
    private readonly IRepository<SysUserRole> _userRoleRepository;
    private readonly IRepository<SysRoleMenu> _roleMenuRepository;
    private readonly IRepository<SysMenu> _menuRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<PermissionService> _logger;

    private const string VersionKey = "netbase:perm:version";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public PermissionService(
        IRepository<SysUserRole> userRoleRepository,
        IRepository<SysRoleMenu> roleMenuRepository,
        IRepository<SysMenu> menuRepository,
        ICacheService cacheService,
        ILogger<PermissionService> logger)
    {
        _userRoleRepository = userRoleRepository;
        _roleMenuRepository = roleMenuRepository;
        _menuRepository = menuRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<HashSet<string>> GetUserPermissionsAsync(long userId)
    {
        var version = _cacheService.Get<long>(VersionKey);
        var key = $"netbase:perm:v{version}:{userId}";

        var cached = _cacheService.Get<HashSet<string>>(key);
        if (cached != null)
        {
            return cached;
        }

        // 用户 → 角色 → 菜单权限码
        var userRoles = await _userRoleRepository.GetListAsync(x => x.UserId == userId);
        var roleIds = userRoles.Select(x => x.RoleId).ToList();
        var permissions = new HashSet<string>();
        if (roleIds.Count > 0)
        {
            var roleMenus = await _roleMenuRepository.GetListAsync(x => roleIds.Contains(x.RoleId));
            var menuIds = roleMenus.Select(x => x.MenuId).Distinct().ToList();
            if (menuIds.Count > 0)
            {
                var menus = await _menuRepository.GetListAsync(x => menuIds.Contains(x.Id));
                permissions = menus
                    .Where(x => !string.IsNullOrWhiteSpace(x.Permission))
                    .Select(x => x.Permission!)
                    .ToHashSet();
            }
        }

        _cacheService.Set(key, permissions, CacheTtl);
        return permissions;
    }

    public void InvalidateAll()
    {
        // 版本号递增使全部权限缓存 key 失效，无需遍历清除
        var version = _cacheService.Get<long>(VersionKey);
        _cacheService.Set(VersionKey, version + 1);
        _logger.LogDebug("权限缓存已整体失效（version={Version}）", version + 1);
    }
}
