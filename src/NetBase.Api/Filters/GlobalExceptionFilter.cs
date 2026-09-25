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

            case UnauthorizedAccessException:
                // 权限语义异常按 401 归类（如会话失效后的越权访问），不混入"系统繁忙 500"
                _logger.LogWarning("未授权访问: {Message}", exception.Message);
                context.Result = new JsonResult(ApiResult.Fail("未登录或登录已过期，请重新登录", ApiResultCode.Unauthorized, ErrorCodes.AUTH_SESSION_EXPIRED));
                break;

            default:
                _logger.LogError(exception, "未处理异常: {Message}", exception.Message);
                context.Result = new JsonResult(ApiResult.Fail("系统繁忙，请稍后重试", ApiResultCode.Fail, ErrorCodes.COMMON_SYSTEM_ERROR));
                break;
        }

        context.ExceptionHandled = true;
    }
}
