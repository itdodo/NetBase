using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using NetBase.Common.Extensions;
using NetBase.Api.Extensions;
using NetBase.Common.Security;
using NetBase.Common.Users;
using NetBase.Model.Entities;
using NetBase.Service.Sys;

namespace NetBase.Api.Filters;

/// <summary>
/// 操作日志过滤器：自动记录全部写操作（POST/PUT/DELETE），
/// 参数经敏感字段脱敏后入库，异常也记录且不改变原异常流转。
/// </summary>
public class OperationLogFilter(ISysLogService logService, ICurrentUserService currentUser) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpMethod = context.HttpContext.Request.Method.ToUpperInvariant();
        if (httpMethod is not ("POST" or "PUT" or "DELETE" or "PATCH"))
        {
            await next();
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var executed = await next();
        stopwatch.Stop();

        var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
        var log = new SysOperationLog
        {
            UserId = currentUser.UserId ?? 0,
            UserName = currentUser.UserName ?? "anonymous",
            Module = descriptor?.ControllerName,
            Action = descriptor?.ActionName,
            HttpMethod = httpMethod,
            Path = context.HttpContext.Request.Path.Value.TruncateTo(200),
            Params = SensitiveData.Serialize(context.ActionArguments),
            // Canceled=被前置过滤器短路（模型验证/防重拒绝），同样视为失败操作
            Success = executed.Exception == null && !executed.Canceled,
            ErrorMessage = executed.Exception?.Message.TruncateTo(500),
            ElapsedMs = stopwatch.ElapsedMilliseconds,
            Ip = context.HttpContext.GetClientIp()
        };

        await logService.RecordOperationAsync(log);
    }

}
