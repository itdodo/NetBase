using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Common.Users;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.Monitor;

/// <summary>日志查询：操作日志、登录日志</summary>
[ApiController]
[Route("api/v1/sys/log")]
public class SysLogController(ISysLogService logService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>操作日志分页</summary>
    [HasPermission("monitor:operlog:list")]
    [HttpGet("operation/page")]
    public async Task<ApiResult<PageResult<OperationLogDto>>> OperationPage([FromQuery] LogQueryDto query)
    {
        return Success(await logService.GetOperationLogPageAsync(query, currentUserService.UserId));
    }

    /// <summary>清理操作日志（物理删除 before 之前记录）</summary>
    [HasPermission("monitor:operlog:list")]
    [HttpDelete("operation/cleanup")]
    public async Task<ApiResult<int>> CleanupOperation([FromQuery] int keepDays = 30)
    {
        var count = await logService.CleanupOperationLogsAsync(DateTime.Now.AddDays(-keepDays));
        return Success(count, $"已清理 {count} 条操作日志");
    }

    /// <summary>清理登录日志（物理删除 before 之前记录）</summary>
    [HasPermission("monitor:loginlog:list")]
    [HttpDelete("login/cleanup")]
    public async Task<ApiResult<int>> CleanupLogin([FromQuery] int keepDays = 30)
    {
        var count = await logService.CleanupLoginLogsAsync(DateTime.Now.AddDays(-keepDays));
        return Success(count, $"已清理 {count} 条登录日志");
    }

    /// <summary>导出操作日志（xlsx，条件同分页）</summary>
    [HasPermission("monitor:operlog:list")]
    [HttpGet("operation/export")]
    public async Task<IActionResult> OperationExport([FromQuery] LogQueryDto query)
    {
        var list = await logService.GetOperationLogExportAsync(query, currentUserService.UserId);
        var rows = list.Select(x => new
        {
            操作人 = x.UserName,
            模块 = x.Module,
            动作 = x.Action,
            方法 = x.HttpMethod,
            路径 = x.Path,
            结果 = x.Success ? "成功" : "失败",
            错误消息 = x.ErrorMessage,
            耗时ms = x.ElapsedMs,
            IP = x.Ip,
            时间 = x.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
        });
        return ExcelResult(rows, $"操作日志_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>登录日志分页</summary>
    [HasPermission("monitor:loginlog:list")]
    [HttpGet("login/page")]
    public async Task<ApiResult<PageResult<LoginLogDto>>> LoginPage([FromQuery] LogQueryDto query)
    {
        return Success(await logService.GetLoginLogPageAsync(query, currentUserService.UserId));
    }

    /// <summary>导出登录日志（xlsx，条件同分页）</summary>
    [HasPermission("monitor:loginlog:list")]
    [HttpGet("login/export")]
    public async Task<IActionResult> LoginExport([FromQuery] LogQueryDto query)
    {
        var list = await logService.GetLoginLogExportAsync(query, currentUserService.UserId);
        var rows = list.Select(x => new
        {
            用户名 = x.UserName,
            结果 = x.Success ? "成功" : "失败",
            描述 = x.Message,
            IP = x.Ip,
            浏览器标识 = x.UserAgent,
            时间 = x.CreateTime.ToString("yyyy-MM-dd HH:mm:ss")
        });
        return ExcelResult(rows, $"登录日志_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }
}
