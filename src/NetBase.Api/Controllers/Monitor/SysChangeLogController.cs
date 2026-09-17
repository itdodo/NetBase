using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Common.Users;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.Monitor;

/// <summary>字段级变更日志（sys_change_log）：编辑操作的前后值差异查询</summary>
[ApiController]
[Route("api/v1/sys/changelog")]
public class SysChangeLogController(ISysLogService logService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>变更日志分页</summary>
    [HasPermission("monitor:changelog:list")]
    [HttpGet("page")]
    public async Task<ApiResult<PageResult<ChangeLogDto>>> Page([FromQuery] ChangeLogQueryDto query)
    {
        return Success(await logService.GetChangeLogPageAsync(query));
    }

    /// <summary>清理变更日志（物理删除 before 之前记录）</summary>
    [HasPermission("monitor:changelog:list")]
    [HttpDelete("cleanup")]
    public async Task<ApiResult<int>> Cleanup([FromQuery] int keepDays = 30)
    {
        var count = await logService.CleanupChangeLogsAsync(DateTime.Now.AddDays(-keepDays));
        return Success(count, $"已清理 {count} 条变更日志");
    }
}
