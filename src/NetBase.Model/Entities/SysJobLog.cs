using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>
/// 定时任务执行日志。由 Hangfire ServerFilter 在作业执行前后自动落库，
/// 支撑作业管理页的执行历史查询与失败排查；保留期随 sys.log.retentionDays 由清理作业维护。
/// </summary>
[SugarTable("sys_job_log", TableDescription = "定时任务执行日志")]
public class SysJobLog : BaseEntity
{
    /// <summary>作业标识（Hangfire RecurringJobId，如 sys.backup.daily）</summary>
    [SugarColumn(Length = 100, ColumnDescription = "作业标识")]
    public string JobId { get; set; } = string.Empty;

    /// <summary>作业显示名</summary>
    [SugarColumn(Length = 200, ColumnDescription = "作业显示名")]
    public string JobName { get; set; } = string.Empty;

    /// <summary>是否成功</summary>
    [SugarColumn(ColumnDescription = "是否成功")]
    public bool Success { get; set; }

    /// <summary>执行耗时（毫秒）</summary>
    [SugarColumn(ColumnDescription = "执行耗时(毫秒)")]
    public long DurationMs { get; set; }

    /// <summary>失败原因（异常消息，截断 2000）</summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "text", ColumnDescription = "失败原因")]
    public string? Error { get; set; }

    /// <summary>触发方式：scheduled=按 Cron 调度，manual=页面手动触发</summary>
    [SugarColumn(Length = 20, ColumnDescription = "触发方式")]
    public string TriggerType { get; set; } = "scheduled";
}
