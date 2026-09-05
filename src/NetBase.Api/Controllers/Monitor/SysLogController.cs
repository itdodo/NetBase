using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Common.Users;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.Monitor;

/// <summary>日志查询：操作日志、登录日志</summary>
[ApiController]
[Route("api/sys/log")]
public class SysLogController(ISysLogService logService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>操作日志分页</summary>
    [HasPermission("monitor:operlog:list")]
    [HttpGet("operation/page")]
    public async Task<ApiResult<PageResult<OperationLogDto>>> OperationPage([FromQuery] LogQueryDto query)
    {
        return Success(await logService.GetOperationLogPageAsync(query));
    }

    /// <summary>登录日志分页</summary>
    [HasPermission("monitor:loginlog:list")]
    [HttpGet("login/page")]
    public async Task<ApiResult<PageResult<LoginLogDto>>> LoginPage([FromQuery] LogQueryDto query)
    {
        return Success(await logService.GetLoginLogPageAsync(query));
    }
}
