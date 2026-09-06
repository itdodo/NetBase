using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NetBase.Common.Exceptions;
using NetBase.Common.Results;

namespace NetBase.Api.Filters;

/// <summary>
/// 全局异常过滤器：BusinessException 转 ApiResult 返回；
/// 未预期异常记录日志并返回统一错误信息（不泄露堆栈）。
/// </summary>
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled)
        {
            return;
        }

        var exception = context.Exception;
        switch (exception)
        {
            case BusinessException businessException:
                _logger.LogWarning("业务异常: {Message}", businessException.Message);
                context.Result = new JsonResult(new ApiResult
                {
                    Code = businessException.Code,
                    Message = businessException.Message,
                    ErrorCode = businessException.ErrorCode
                });
                break;

            case OperationCanceledException:
                // 客户端取消请求，无需返回内容
                context.ExceptionHandled = true;
                return;

            default:
                _logger.LogError(exception, "未处理异常: {Message}", exception.Message);
                context.Result = new JsonResult(ApiResult.Fail("系统繁忙，请稍后重试"));
                break;
        }

        context.ExceptionHandled = true;
    }
}
