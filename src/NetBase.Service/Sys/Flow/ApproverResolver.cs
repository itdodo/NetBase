using NetBase.Model.Entities;
using NetBase.Model.Enums;
using NetBase.Repository.Repositories;

namespace NetBase.Service.Sys.Flow;

/// <summary>解析出的审批人</summary>
public record FlowApprover(long UserId, string UserName);

/// <summary>
/// 审批人解析器：节点规则 → 具体用户列表（去重保序）。
/// 规则支持：指定成员 / 角色 / 部门主管（deptId=0 取发起人部门）/ 发起人自选（提交参数）。
/// </summary>
public class ApproverResolver(
    IRepository<SysUser> userRepository,
    IRepository<SysUserRole> userRoleRepository,
    IRepository<SysRole> roleRepository,
    IRepository<SysDept> deptRepository,
    IRepository<SysPosition> positionRepository,
    IRepository<SysUserPosition> userPositionRepository)
{
    /// <summary>
    /// 解析审批人。submitterDeptId 为发起人部门（DeptLeader 规则 fallback）；
    /// choiceUserIds 为发起人自选（SubmitterChoice 规则）。
    /// </summary>
    public async Task<List<FlowApprover>> ResolveAsync(
        IEnumerable<ApproverRule> rules, long submitterDeptId, IReadOnlyCollection<long> choiceUserIds)
    {
        var result = new List<FlowApprover>();
        var seen = new HashSet<long>();

        void Add(IEnumerable<long> userIds)
        {
            foreach (var id in userIds)
            {
                if (id > 0 && seen.Add(id))
                {
                    result.Add(new FlowApprover(id, string.Empty));
                }
            }
        }

        foreach (var rule in rules)
        {
            switch (rule.Type)
            {
                case FlowApproverType.User:
                    Add(rule.UserIds ?? []);
                    break;

                case FlowApproverType.Role when rule.RoleCodes is { Count: > 0 }:
                    var roleIds = roleRepository.GetList(x => rule.RoleCodes.Contains(x.RoleCode) && x.Status == 1)
                        .Select(r => r.Id).ToList();
                    if (roleIds.Count > 0)
                    {
                        Add((await userRoleRepository.GetListAsync(ur => roleIds.Contains(ur.RoleId)))
                            .Select(ur => ur.UserId));
                    }
                    break;

                case FlowApproverType.DeptLeader:
                    var deptId = rule.DeptId is > 0 ? rule.DeptId.Value : submitterDeptId;
                    var leaderId = deptRepository.GetFirst(d => d.Id == deptId)?.LeaderUserId ?? 0;
                    Add(leaderId > 0 ? [leaderId] : []);
                    break;

                case FlowApproverType.Position when rule.PositionCodes is { Count: > 0 }:
                    var posIds = positionRepository
                        .GetList(x => rule.PositionCodes.Contains(x.PositionCode) && x.Status == 1)
                        .Select(p => p.Id).ToList();
                    if (posIds.Count > 0)
                    {
                        Add((await userPositionRepository.GetListAsync(up => posIds.Contains(up.PositionId)))
                            .Select(up => up.UserId));
                    }
                    break;

                case FlowApproverType.SubmitterChoice:
                    Add(choiceUserIds);
                    break;
            }
        }

        // 批量补姓名（未命中的用户ID剔除，防脏数据生成幽灵任务）
        var ids = result.Select(a => a.UserId).ToList();
        var names = (await userRepository.GetListAsync(u => ids.Contains(u.Id) && u.Status == 1))
            .ToDictionary(u => u.Id, u => u.NickName ?? u.UserName);
        return result.Where(a => names.ContainsKey(a.UserId))
            .Select(a => new FlowApprover(a.UserId, names[a.UserId]))
            .ToList();
    }
}
