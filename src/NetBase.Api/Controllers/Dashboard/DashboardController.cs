using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetBase.Common.Results;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.Dashboard;

/// <summary>仪表盘：登录用户可见的聚合统计</summary>
[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>首页统计（卡片 + 登录趋势 + 部门分布）</summary>
    [HttpGet("stats")]
    public async Task<ApiResult<DashboardStatsDto>> Stats()
    {
        return ApiResult<DashboardStatsDto>.Ok(await dashboardService.GetStatsAsync());
    }
}
