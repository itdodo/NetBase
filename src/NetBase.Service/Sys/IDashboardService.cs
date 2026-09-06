using NetBase.Model.Dtos;

namespace NetBase.Service.Sys;

/// <summary>仪表盘统计</summary>
public class DashboardStatsDto
{
    /// <summary>用户总数</summary>
    public int UserCount { get; set; }

    /// <summary>角色数</summary>
    public int RoleCount { get; set; }

    /// <summary>在线会话数（活跃）</summary>
    public int OnlineSessions { get; set; }

    /// <summary>今日登录次数（成功）</summary>
    public int TodayLogins { get; set; }

    /// <summary>今日操作数</summary>
    public int TodayOperations { get; set; }

    /// <summary>公告数</summary>
    public int NoticeCount { get; set; }

    /// <summary>近 7 天登录趋势（成功/失败）</summary>
    public List<LoginTrendPoint> LoginTrend { get; set; } = [];

    /// <summary>部门用户分布（Top 8）</summary>
    public List<DeptUserCount> DeptDistribution { get; set; } = [];
}

/// <summary>登录趋势点</summary>
public class LoginTrendPoint
{
    /// <summary>日期（MM-dd）</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>成功次数</summary>
    public int Success { get; set; }

    /// <summary>失败次数</summary>
    public int Failed { get; set; }
}

/// <summary>部门用户分布</summary>
public class DeptUserCount
{
    public string DeptName { get; set; } = string.Empty;

    public int UserCount { get; set; }
}

/// <summary>仪表盘聚合服务</summary>
public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync();
}
