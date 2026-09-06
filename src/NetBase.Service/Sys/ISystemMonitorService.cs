namespace NetBase.Service.Sys;

/// <summary>系统监控信息</summary>
public class SystemMonitorInfo
{
    /// <summary>机器名</summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>进程架构</summary>
    public string ProcessArchitecture { get; set; } = string.Empty;

    /// <summary>运行时长（小时）</summary>
    public double UptimeHours { get; set; }

    /// <summary>进程内存（MB）</summary>
    public double WorkingSetMb { get; set; }

    /// <summary>GC 托管内存（MB）</summary>
    public double GcMemoryMb { get; set; }

    /// <summary>线程数</summary>
    public int ThreadCount { get; set; }

    /// <summary>句柄数</summary>
    public int HandleCount { get; set; }

    /// <summary>Gen0/1/2 回收次数</summary>
    public int Gen0Collections { get; set; }

    public int Gen1Collections { get; set; }

    public int Gen2Collections { get; set; }

    /// <summary>缓存提供方</summary>
    public string CacheProvider { get; set; } = string.Empty;

    /// <summary>缓存诊断（Redis 时含内存/命中率；进程内缓存为 null）</summary>
    public object? CacheDiagnostics { get; set; }
}

/// <summary>系统监控服务</summary>
public interface ISystemMonitorService
{
    Task<SystemMonitorInfo> GetInfoAsync();
}
