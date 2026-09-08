using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetBase.Common.Cache;
using NetBase.Service.Sys;

namespace NetBase.Api.Jobs;

/// <summary>Hangfire 配置（appsettings.json 的 Hangfire 节点）</summary>
public class HangfireOptions
{
    public const string SectionName = "Hangfire";

    /// <summary>是否启用 Hangfire Dashboard（/hangfire，默认仅本机可访问）</summary>
    public bool DashboardEnabled { get; set; } = true;
}

/// <summary>作业实例信息（管理页展示）</summary>
public class JobInstanceDto
{
    /// <summary>作业标识（RecurringJobId）</summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>显示名称</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Cron 表达式</summary>
    public string Cron { get; set; } = string.Empty;

    /// <summary>上次执行时间</summary>
    public DateTime? LastExecution { get; set; }

    /// <summary>下次执行时间</summary>
    public DateTime? NextExecution { get; set; }

    /// <summary>上次执行是否成功</summary>
    public bool? LastJobSuccess { get; set; }

    /// <summary>是否暂停</summary>
    public bool Paused { get; set; }
}

/// <summary>
/// 内置定时任务：注册、执行与运行时管理（修改 Cron/触发/暂停）。
/// 基于 Hangfire RecurringJob（免费版，SqlServer 存储）。
/// </summary>
public interface ISystemJobService
{
    /// <summary>应用启动时注册全部内置作业（幂等）</summary>
    void RegisterJobs();

    /// <summary>作业实例列表</summary>
    Task<List<JobInstanceDto>> GetJobsAsync();

    /// <summary>修改作业 Cron 并恢复调度</summary>
    void UpdateCron(string jobId, string cron);

    /// <summary>立即触发一次</summary>
    void Trigger(string jobId);

    /// <summary>暂停（从调度移除，可重新添加恢复）</summary>
    void Pause(string jobId);

    /// <summary>恢复调度</summary>
    void Resume(string jobId);
}

/// <summary>作业定义与实现</summary>
public class SystemJobService(
    ISysLogService logService,
    ISysConfigService configService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<SystemJobService> logger) : ISystemJobService
{
    /// <summary>内置作业定义：Id / 名称 / 默认 Cron / 执行方法</summary>
    private static readonly (string JobId, string DisplayName, string Cron)[] BuiltInJobs =
    [
        ("sys.log.cleanup", "日志清理（保留期见参数 sys.log.retentionDays）", "0 2 * * *")
    ];

    public void RegisterJobs()
    {
        RecurringJob.AddOrUpdate<SystemJobService>(
            "sys.log.cleanup",
            svc => svc.RunLogCleanupAsync(null),
            BuiltInJobs[0].Cron);
        logger.LogInformation("定时任务已注册: {Count} 个内置作业", BuiltInJobs.Length);
    }

    /// <summary>日志清理作业：保留天数读系统参数 sys.log.retentionDays</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task RunLogCleanupAsync(int? retentionDaysOverride)
    {
        var keepDays = retentionDaysOverride
                        ?? await configService.GetIntConfigAsync("sys.log.retentionDays", 30);
        var before = DateTime.Now.AddDays(-keepDays);

        var opCount = await logService.CleanupOperationLogsAsync(before);
        var loginCount = await logService.CleanupLoginLogsAsync(before);
        logger.LogInformation("日志清理完成: 操作日志 {Op} 条, 登录日志 {Login} 条（保留 {Days} 天）",
            opCount, loginCount, keepDays);
    }

    public Task<List<JobInstanceDto>> GetJobsAsync()
    {
        using var connection = JobStorage.Current.GetConnection();
        var recurring = connection.GetRecurringJobs();

        var jobs = BuiltInJobs.Select(def =>
        {
            var r = recurring.FirstOrDefault(x => x.Id == def.JobId);
            return new JobInstanceDto
            {
                JobId = def.JobId,
                DisplayName = def.DisplayName,
                Cron = r?.Cron ?? def.Cron,
                LastExecution = r?.LastExecution,
                NextExecution = r?.NextExecution,
                // Removed=从调度移除（暂停态）；LastJobState=Succeeded 视为上次成功
                LastJobSuccess = r == null ? null : r.LastJobState == null ? (bool?)null : r.LastJobState == "Succeeded",
                Paused = r?.Removed ?? false
            };
        }).ToList();

        return Task.FromResult(jobs);
    }

    public void UpdateCron(string jobId, string cron)
    {
        EnsureBuiltIn(jobId);
        // RecurringJob 更新 Cron 即恢复调度（暂停态通过重新添加解除）
        RecurringJob.AddOrUpdate<SystemJobService>(
            jobId, svc => svc.RunLogCleanupAsync(null), cron);
    }

    public void Trigger(string jobId)
    {
        EnsureBuiltIn(jobId);
        backgroundJobClient.Enqueue<SystemJobService>(svc => svc.RunLogCleanupAsync(null));
    }

    public void Pause(string jobId)
    {
        EnsureBuiltIn(jobId);
        // 从调度移除即暂停（RecurringJobDto.Removed = true）
        RecurringJob.RemoveIfExists(jobId);
    }

    public void Resume(string jobId)
    {
        EnsureBuiltIn(jobId);
        RecurringJob.AddOrUpdate<SystemJobService>(
            jobId, svc => svc.RunLogCleanupAsync(null), BuiltInJobs[0].Cron);
    }

    private static void EnsureBuiltIn(string jobId)
    {
        if (!BuiltInJobs.Any(j => j.JobId == jobId))
        {
            throw new ArgumentException($"未知作业: {jobId}");
        }
    }
}
