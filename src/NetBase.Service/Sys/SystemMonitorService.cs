using NetBase.Common.Time;
using System.Diagnostics;
using NetBase.Common.Cache;
using NetBase.Common.Extensions;

namespace NetBase.Service.Sys;

/// <summary>系统监控实现</summary>
public class SystemMonitorService(ICacheService cacheService,

TimeProvider tp) : ISystemMonitorService
{
    private static DateTime? _startTime;

    public async Task<SystemMonitorInfo> GetInfoAsync()
    {
        using var process = Process.GetCurrentProcess();
        var info = new SystemMonitorInfo
        {
            MachineName = Environment.MachineName,
            ProcessArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            UptimeHours = (tp.LocalNow() - process.StartTime).TotalHours,
            WorkingSetMb = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 1),
            GcMemoryMb = Math.Round(GC.GetTotalMemory(false) / 1024.0 / 1024.0, 1),
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            CacheProvider = cacheService.GetType().Name
        };

        info.CacheDiagnostics = await GetCacheDiagnosticsAsync();
        return info;
    }

    /// <summary>Redis 提供方输出 info 关键指标；进程内缓存无诊断数据</summary>
    private async Task<object?> GetCacheDiagnosticsAsync()
    {
        var type = cacheService.GetType();
        if (!type.Name.StartsWith("Redis", StringComparison.Ordinal))
        {
            return new { provider = "MemoryCache", note = "进程内缓存无外部诊断指标" };
        }

        try
        {
            // 通过反射调用 Redis 实现的诊断方法，避免服务层直接依赖 StackExchange.Redis
            var method = type.GetMethod("GetRedisDiagnosticsAsync");
            if (method != null)
            {
                var result = await (Task<object?>)method.Invoke(cacheService, null)!;
                if (result is not null)
                {
                    return result;
                }
            }
            return new { provider = "Redis", note = "诊断数据不可用" };
        }
        catch (Exception)
        {
            return new { provider = "Redis", note = "诊断数据获取失败" };
        }
    }
}
