using NetBase.Model.Entities;
using NetBase.Repository.Repositories;

namespace NetBase.Service.Sys;

/// <summary>仪表盘聚合实现（轻量聚合，量级以中小系统为准）</summary>
public class DashboardService(
    IRepository<SysUser> userRepository,
    IRepository<SysRole> roleRepository,
    IRepository<SysNotice> noticeRepository,
    IRepository<SysUserSession> sessionRepository,
    IRepository<SysLoginLog> loginLogRepository,
    IRepository<SysOperationLog> operationLogRepository,
    ISysDeptService deptService) : IDashboardService
{
    public async Task<DashboardStatsDto> GetStatsAsync()
    {
        var today = DateTime.Today;
        var weekStart = today.AddDays(-6);

        var stats = new DashboardStatsDto
        {
            UserCount = (int)await userRepository.CountAsync(),
            RoleCount = (int)await roleRepository.CountAsync(),
            // 活跃会话 = RefreshToken 未过期（登录即建会话）
            OnlineSessions = (int)await sessionRepository.CountAsync(x => x.ExpireTime > DateTime.Now),
            TodayLogins = (int)await loginLogRepository.CountAsync(x => x.Success && x.CreateTime >= today),
            TodayOperations = (int)await operationLogRepository.CountAsync(x => x.CreateTime >= today),
            NoticeCount = (int)await noticeRepository.CountAsync()
        };

        // 近 7 天登录日志 → 内存按日聚合（日志量级可控；更大规模可改为 SQL GroupBy）
        var recentLogs = await loginLogRepository.GetListAsync(x => x.CreateTime >= weekStart);
        stats.LoginTrend = Enumerable.Range(0, 7)
            .Select(offset =>
            {
                var day = weekStart.AddDays(offset);
                var dayLogs = recentLogs.Where(x => x.CreateTime.Date == day).ToList();
                return new LoginTrendPoint
                {
                    Date = day.ToString("MM-dd"),
                    Success = dayLogs.Count(x => x.Success),
                    Failed = dayLogs.Count(x => !x.Success)
                };
            })
            .ToList();

        // 部门用户分布（含未分配用户归入"未分配"）
        var users = await userRepository.GetListAsync();
        var depts = await deptService.GetAllDeptsAsync();
        var deptNameMap = depts.ToDictionary(x => x.Id, x => x.DeptName);
        stats.DeptDistribution = users
            .GroupBy(x => x.DeptId)
            .Select(g => new DeptUserCount
            {
                DeptName = deptNameMap.TryGetValue(g.Key, out var name) ? name : "未分配",
                UserCount = g.Count()
            })
            .OrderByDescending(x => x.UserCount)
            .Take(8)
            .ToList();

        return stats;
    }
}
