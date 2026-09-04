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

        var result = ApiResult.Fail(errors.Count == 0 ? "请求参数错误" : string.Join("；", errors), ApiResultCode.BadRequest);
        context.Result = new JsonResult(result);
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
