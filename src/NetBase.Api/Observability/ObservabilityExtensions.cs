using Hangfire;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NetBase.Api.Observability;

/// <summary>
/// 可观测性配置：OpenTelemetry traces + metrics，/metrics 端点输出 Prometheus 格式。
/// 覆盖 ASP.NET Core 请求、HttpClient 出站调用、.NET Runtime 指标与 Hangfire。
/// 采集端（Grafana/Prometheus/Jaeger）按 OTEL_* 环境变量或 /metrics 抓取接入。
/// </summary>
public static class ObservabilityExtensions
{
    public static IServiceCollection AddNetBaseObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = $"netbase-api::{Environment.MachineName}";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName: "netbase-api"))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o =>
                {
                    // 健康探针/指标抓取不产生 trace 噪音
                    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health")
                                      && !ctx.Request.Path.StartsWithSegments("/metrics")
                                      && !ctx.Request.Path.StartsWithSegments("/hub");
                })
                .AddHttpClientInstrumentation()
                .AddSource("Hangfire"))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Hangfire")
                .AddMeter("NetBase.Cache")
                // Prometheus 抓取端点（/metrics），默认仅本机可达语义由管线授权控制
                .AddPrometheusExporter());

        return services;
    }
}
