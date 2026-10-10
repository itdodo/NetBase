using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace NetBase.Middleware.Cache;

/// <summary>
/// 缓存业务指标（OTel Meter，经 /metrics 暴露）：命中/未命中计数与命中率计算。
/// 命中率是缓存体系健康度的核心信号——骤降意味着缓存失效风暴或容量问题。
/// </summary>
public static class CacheMetrics
{
    public const string MeterName = "NetBase.Cache";

    public static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> Hits = Meter.CreateCounter<long>("netbase_cache_hits_total", description: "L1/L2 命中次数");

    public static readonly Counter<long> Misses = Meter.CreateCounter<long>("netbase_cache_misses_total", description: "未命中（穿透到 DB）次数");

    /// <summary>读操作埋点：命中计 hits，未命中计 misses</summary>
    public static void Record(bool hit) => (hit ? Hits : Misses).Add(1);
}
