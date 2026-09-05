using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetBase.Repository.DbContexts;

namespace NetBase.Api.Auth;

/// <summary>
/// 数据库健康探针：执行 SELECT 1 验证连接可用。
/// </summary>
public class DatabaseHealthCheck(SqlSugarContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext regulatedContext, CancellationToken cancellationToken = default)
    {
        if (!context.IsConfigured)
        {
            return HealthCheckResult.Degraded("数据库连接串未配置");
        }

        try
        {
            await context.Client.Ado.GetIntAsync("SELECT 1");
            return HealthCheckResult.Healthy("数据库连接正常");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("数据库连接失败", ex);
        }
    }
}
