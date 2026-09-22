using System.Linq.Expressions;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetBase.Common.Cache;
using NetBase.Common.Realtime;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
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
    ISysNoticeService noticeService,
    INotifyService notifyService,
    IRepository<SysRole> roleRepository,
    IRepository<SysUserRole> userRoleRepository,
    IRepository<SysUserSession> userSessionRepository,
    NetBase.Service.Sys.Flow.IFlowEngine flowEngine,
    IBackupService backupService,
    IOptions<BackupOptions> backupOptions,
    IBackgroundJobClient backgroundJobClient,
    ILogger<SystemJobService> logger) : ISystemJobService
{
    /// <summary>数据备份作业标识（Backup:Enabled=false 时不注册并移除既有调度）</summary>
    public const string BackupJobId = "sys.backup.daily";

    /// <summary>内置作业定义：Id / 名称 / 默认 Cron / 执行入口（按 Id 分发，新增作业只加一行）</summary>
    private static readonly (string JobId, string DisplayName, string Cron, Expression<Action<SystemJobService>> Run)[] BuiltInJobs =
    [
        ("sys.log.cleanup", "日志与过期会话清理（日志保留期 sys.log.retentionDays）", "0 2 * * *",
            svc => svc.RunLogCleanupAsync(null)),
        (BackupJobId, "数据备份（SqlServer 全量 + 上传文件镜像）", "0 3 * * *",
            svc => svc.RunBackupAsync()),
        ("sys.notice.publish", "公告定时发布（每分钟检查到期定时公告）", "* * * * *",
            svc => svc.RunNoticePublishAsync()),
        ("sys.flow.remind", "审批超时提醒（每早 9 点提醒超期待办）", "0 9 * * *",
            svc => svc.RunFlowRemindAsync())
    ];

    public void RegisterJobs()
    {
        var registered = 0;
        foreach (var def in BuiltInJobs)
        {
            if (def.JobId == BackupJobId && !backupOptions.Value.Enabled)
            {
                RecurringJob.RemoveIfExists(def.JobId);
                continue;
            }
            RecurringJob.AddOrUpdate(def.JobId, def.Run, def.Cron);
            registered++;
        }
        logger.LogInformation("定时任务已注册: {Count} 个内置作业", registered);
    }

    /// <summary>日志与过期会话清理作业：日志保留天数读系统参数 sys.log.retentionDays，过期会话（关浏览器未登出的残留行）物理删除</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task RunLogCleanupAsync(int? retentionDaysOverride)
    {
        var keepDays = retentionDaysOverride
                        ?? await configService.GetIntConfigAsync("sys.log.retentionDays", 30);
        var before = DateTime.Now.AddDays(-keepDays);

        try
        {
            var opCount = await logService.CleanupOperationLogsAsync(before);
            var loginCount = await logService.CleanupLoginLogsAsync(before);
            var changeCount = await logService.CleanupChangeLogsAsync(before);
            var sessionCount = await userSessionRepository.DeletePhysicalWhereAsync(x => x.ExpireTime <= DateTime.Now);
            logger.LogInformation("清理完成: 操作日志 {Op} 条, 登录日志 {Login} 条, 变更日志 {Change} 条, 过期会话 {Session} 条（日志保留 {Days} 天）",
                opCount, loginCount, changeCount, sessionCount, keepDays);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "日志清理作业执行失败");
            await NotifyAdminsAsync("定时任务失败通知",
                $"日志清理作业（sys.log.cleanup）执行失败：{ex.Message}。Hangfire 将按策略自动重试。");
            throw; // 上抛让 Hangfire 标记失败并自动重试
        }
    }

    /// <summary>公告定时发布：将到期的定时公告（Status=2）翻转为发布（Status=1）</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task RunNoticePublishAsync()
    {
        var count = await noticeService.PublishDueNoticesAsync();
        if (count > 0)
        {
            logger.LogInformation("公告定时发布: {Count} 条已发布", count);
        }
    }

    /// <summary>审批超时提醒：待办超过 sys.flow.remindDays 天（默认 3，0=关闭）未处理，站内信提醒审批人</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task RunFlowRemindAsync()
    {
        var remindDays = await configService.GetIntConfigAsync("sys.flow.remindDays", 3);
        if (remindDays <= 0)
        {
            return;
        }
        var reminded = await flowEngine.RemindOverdueAsync(remindDays);
        if (reminded > 0)
        {
            logger.LogInformation("审批超时提醒: 已提醒 {Count} 个待办", reminded);
        }
    }

    /// <summary>数据备份作业：SqlServer 全量备份 + 上传文件增量镜像（见 BackupService），保留期外 .bak 自动清理</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task RunBackupAsync()
    {
        try
        {
            var result = await backupService.RunAsync();
            logger.LogInformation("数据备份完成: {File}（{Size:F1}MB）, 文件镜像 {Files} 个",
                result.BakFile, result.BakSizeBytes / 1024.0 / 1024, result.MirroredFiles);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "数据备份作业执行失败");
            await NotifyAdminsAsync("定时任务失败通知",
                $"数据备份作业（{BackupJobId}）执行失败：{ex.Message}");
            throw;
        }
    }

    /// <summary>作业异常通知：站内信（落库+在线实时推）告知全部管理员，通知失败不影响主流程</summary>
    private async Task NotifyAdminsAsync(string title, string content)
    {
        try
        {
            var adminRoleId = roleRepository.GetFirst(x => x.RoleCode == SysRoleService.AdminRoleCode)?.Id;
            if (adminRoleId == null)
            {
                return;
            }

            var adminUserIds = (await userRoleRepository.GetListAsync(x => x.RoleId == adminRoleId))
                .Select(x => x.UserId).Distinct().ToList();
            if (adminUserIds.Count > 0)
            {
                await notifyService.PushToUsersAsync(adminUserIds, new NoticePayload
                {
                    MsgType = 1, Title = title, Content = content, SenderName = "system"
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "管理员失败通知发送失败");
        }
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
        var def = EnsureBuiltIn(jobId);
        // RecurringJob 更新 Cron 即恢复调度（暂停态通过重新添加解除）
        RecurringJob.AddOrUpdate(jobId, def.Run, cron);
    }

    public void Trigger(string jobId)
    {
        var def = EnsureBuiltIn(jobId);
        backgroundJobClient.Enqueue(def.Run);
    }

    public void Pause(string jobId)
    {
        EnsureBuiltIn(jobId);
        // 从调度移除即暂停（RecurringJobDto.Removed = true）
        RecurringJob.RemoveIfExists(jobId);
    }

    public void Resume(string jobId)
    {
        var def = EnsureBuiltIn(jobId);
        RecurringJob.AddOrUpdate(jobId, def.Run, def.Cron);
    }

    private static (string JobId, string DisplayName, string Cron, Expression<Action<SystemJobService>> Run) EnsureBuiltIn(string jobId)
    {
        var def = BuiltInJobs.FirstOrDefault(j => j.JobId == jobId);
        if (def.JobId == null)
        {
            throw new ArgumentException($"未知作业: {jobId}");
        }
        return def;
    }
}
