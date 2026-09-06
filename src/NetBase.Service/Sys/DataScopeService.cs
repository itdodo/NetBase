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

    private string CacheKey(long userId) => $"netbase:datascope:{userId}";

    public async Task<DataScopeInfo> GetDataScopeAsync(long userId)
    {
        var cacheKey = CacheKey(userId);
        var cached = cacheService.Get<DataScopeInfo>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

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

        cacheService.Set(cacheKey, info, CacheTtl);
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
