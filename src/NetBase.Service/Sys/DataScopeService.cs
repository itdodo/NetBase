using NetBase.Model.Entities;
using NetBase.Common.Cache;
using NetBase.Model.Enums;
using NetBase.Repository.Repositories;

namespace NetBase.Service.Sys;

/// <summary>数据范围服务实现</summary>
public class DataScopeService(
    IRepository<SysUser> userRepository,
    IRepository<SysRole> roleRepository,
    IRepository<SysUserRole> userRoleRepository,
    IRepository<SysRoleDept> roleDeptRepository,
    ISysDeptService deptService,
    ICacheService cacheService) : IDataScopeService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    /// <summary>并发未命中合并（单飞）：同键只允许一个 DB 加载在途</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<DataScopeInfo>> InFlight = new();

    private string CacheKey(long userId) => $"datascope:{userId}";

    public async Task<DataScopeInfo> GetDataScopeAsync(long userId)
    {
        var cacheKey = CacheKey(userId);
        var cached = await cacheService.GetAsync<DataScopeInfo>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        // 单飞：L1 失效瞬间的并发未命中合并为一次 DB 加载，防惊群打爆连接池
        var loadTask = InFlight.GetOrAdd(cacheKey, _ => LoadDataScopeAsync(userId, cacheKey));
        try
        {
            return await loadTask;
        }
        finally
        {
            InFlight.TryRemove(cacheKey, out _);
        }
    }

    private async Task<DataScopeInfo> LoadDataScopeAsync(long userId, string cacheKey)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return new DataScopeInfo { FilterAll = false }; // 用户不存在：仅本人兜底
        }

        var userRoles = await userRoleRepository.GetListAsync(x => x.UserId == userId);
        var roleIds = userRoles.Select(x => x.RoleId).ToList();
        var roles = roleIds.Count == 0
            ? []
            : await roleRepository.GetListAsync(x => roleIds.Contains(x.Id) && x.Status == (int)NetBase.Model.Enums.StatusEnum.Enabled);

        var info = new DataScopeInfo();
        var hasCustom = false;

        foreach (var role in roles)
        {
            var scope = (DataScopeEnum)role.DataScope;
            switch (scope)
            {
                case DataScopeEnum.All:
                    // 任一角色为全部数据即放行
                    return new DataScopeInfo { FilterAll = true };

                case DataScopeEnum.Custom:
                    hasCustom = true;
                    var customDepts = await roleDeptRepository.GetListAsync(x => x.RoleId == role.Id);
                    foreach (var deptId in customDepts.Select(x => x.DeptId))
                    {
                        info.DeptIds.Add(deptId);
                    }
                    break;

                case DataScopeEnum.Dept:
                    info.DeptIds.Add(user.DeptId);
                    break;

                case DataScopeEnum.DeptAndChild:
                    var childIds = await deptService.GetDeptAndChildIdsAsync(user.DeptId);
                    foreach (var deptId in childIds)
                    {
                        info.DeptIds.Add(deptId);
                    }
                    break;

                case DataScopeEnum.Self:
                    info.IncludeSelfData = true;
                    break;
            }
        }

        // 自定义档未勾选任何部门时按"仅本人"兜底（避免范围外的越权空档）
        if (hasCustom && !info.HasDeptCondition)
        {
            info.IncludeSelfData = true;
        }

        await cacheService.SetAsync(cacheKey, info, CacheTtl);
        return info;
    }

    public async Task<List<long>> GetDeptAndChildIdsAsync(long deptId)
    {
        var all = await deptService.GetAllDeptsAsync();
        var ids = new List<long> { deptId };
        CollectChildren(all, deptId, ids);
        return ids;
    }

    private static void CollectChildren(List<SysDept> all, long parentId, List<long> ids)
    {
        foreach (var dept in all.Where(x => x.ParentId == parentId))
        {
            ids.Add(dept.Id);
            CollectChildren(all, dept.Id, ids);
        }
    }
}
