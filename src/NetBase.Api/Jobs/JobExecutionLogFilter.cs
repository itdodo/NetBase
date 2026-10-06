using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using Hangfire.Server;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;

namespace NetBase.Api.Jobs;

/// <summary>
/// 作业执行日志过滤器：Hangfire 在所有作业执行前后自动落库 sys_job_log
/// （成功走 OnPerformed、异常走 IServerExceptionFilter.OnServerException），耗时/触发方式/失败原因全记录。
/// 日志写入失败不影响作业本身。注册于 AddHangfire，全部内置作业底层统一覆盖。
/// </summary>
public class JobExecutionLogFilter(IServiceScopeFactory scopeFactory, ILogger<JobExecutionLogFilter> logger) : IServerFilter, IServerExceptionFilter
{
    private const int MaxErrorLength = 2000;

    /// <summary>执行中作业的计时与身份信息（key = BackgroundJob.Id）</summary>
    private static readonly ConcurrentDictionary<string, (Stopwatch Sw, string JobId, string JobName, string TriggerType)> Running = new();

    public void OnPerforming(PerformingContext context)
    {
        var (jobId, jobName, triggerType) = ResolveIdentity(context);
        Running[context.BackgroundJob.Id] = (Stopwatch.StartNew(), jobId, jobName, triggerType);
    }

    public void OnPerformed(PerformedContext context)
    {
        if (!Running.TryRemove(context.BackgroundJob.Id, out var info))
        {
            return;
        }
        info.Sw.Stop();
        // 注意：OnPerformed 在方法抛异常时同样会被调用（Hangfire 管道 finally 语义），
        // 必须按 context.Exception 判定成败，否则失败作业会被记成成功
        var error = context.Exception?.GetBaseException().Message;
        WriteLog(info.JobId, info.JobName, success: error == null, info.Sw.ElapsedMilliseconds, error, info.TriggerType);
    }

    public void OnServerException(ServerExceptionContext context)
    {
        if (!Running.TryRemove(context.BackgroundJob.Id, out var info))
        {
            return;
        }
        info.Sw.Stop();
        var error = context.Exception.GetBaseException().Message;
        WriteLog(info.JobId, info.JobName, success: false, info.Sw.ElapsedMilliseconds, error, info.TriggerType);
    }

    /// <summary>
    /// 解析作业身份：Cron 调度的作业带 RecurringJobId 参数（scheduled）；
    /// 手动触发按方法名映射内置作业定义（manual），未注册方法降级记录方法名。
    /// </summary>
    private static (string JobId, string JobName, string TriggerType) ResolveIdentity(PerformingContext context)
    {
        var recurringId = context.GetJobParameter<string>("RecurringJobId");
        if (!string.IsNullOrEmpty(recurringId))
        {
            var def = SystemJobService.FindBuiltIn(recurringId);
            return (recurringId, def?.DisplayName ?? recurringId, "scheduled");
        }

        var method = context.BackgroundJob.Job.Method;
        var byMethod = SystemJobService.FindBuiltInByMethod(method.Name);
        return byMethod == null
            ? (method.Name, $"{method.DeclaringType?.Name}.{method.Name}", "manual")
            : (byMethod.Value.JobId, byMethod.Value.DisplayName, "manual");
    }

    private void WriteLog(string jobId, string jobName, bool success, long durationMs, string? error, string triggerType)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<SysJobLog>>();
            repository.InsertAsync(new SysJobLog
            {
                JobId = jobId,
                JobName = jobName,
                Success = success,
                DurationMs = durationMs,
                Error = error is null ? null : error.Length <= MaxErrorLength ? error : error[..MaxErrorLength],
                TriggerType = triggerType
            }).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "作业执行日志写入失败（{JobId}）", jobId);
        }
    }
}
