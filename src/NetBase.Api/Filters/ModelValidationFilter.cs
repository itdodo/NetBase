using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NetBase.Common.Results;

namespace NetBase.Api.Filters;

/// <summary>
/// 模型验证过滤器：验证失败时返回统一 ApiResult（code=400）。
/// </summary>
public class ModelValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
        {
            return;
        }

        var errors = context.ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .Where(e => !string.IsNullOrEmpty(e))
            .Distinct()
            .ToList();

        // 逐字段错误映射（字段名 → 首条文案），供前端按字段标红；message 仍保留拼接全文
        var fieldErrors = context.ModelState
            .Where(kv => kv.Value?.Errors.Count > 0)
            .ToDictionary(
                kv => kv.Key,
                kv => kv.Value!.Errors.Select(e => e.ErrorMessage).First(e => !string.IsNullOrEmpty(e)));

        var result = ApiResult.Fail(errors.Count == 0 ? "请求参数错误" : string.Join("；", errors), ApiResultCode.BadRequest, ErrorCodes.COMMON_PARAM_INVALID);
        result.Errors = fieldErrors.Count > 0 ? fieldErrors : null;
        context.Result = new JsonResult(result);
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
