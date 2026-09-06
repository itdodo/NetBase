using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Common.Users;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.Monitor;

/// <summary>系统监控：进程指标与缓存诊断</summary>
[ApiController]
[Route("api/v1/monitor/system")]
public class SystemMonitorController(
    ISystemMonitorService monitorService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>系统监控信息（进程/CPU/GC/缓存）</summary>
    [HasPermission("monitor:system:list")]
    [HttpGet]
    public async Task<ApiResult<SystemMonitorInfo>> Get()
    {
        return Success(await monitorService.GetInfoAsync());
    }
}
