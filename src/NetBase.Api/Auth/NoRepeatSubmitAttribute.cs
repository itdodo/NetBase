using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NetBase.Common.Cache;
using NetBase.Common.Users;

namespace NetBase.Api.Auth;

/// <summary>
/// 防重复提交：对登录用户的写操作做短窗口判重（默认 2 秒内相同 用户+路径+参数 直接拒绝）。
/// 用法：[NoRepeatSubmit]；未登录请求不做判重（登录本身有限流与锁定防护）。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class NoRepeatSubmitAttribute(int windowSeconds = 2) : Attribute
{
    public int WindowSeconds { get; } = windowSeconds;
}

/// <summary>防重复提交过滤器：与 NoRepeatSubmitAttribute 配套</summary>
public class NoRepeatSubmitFilter(ICacheService cacheService, ICurrentUserService currentUser) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.HttpContext.Request.Method is not ("POST" or "PUT" or "DELETE" or "PATCH"))
        {
            return;
        }

        var attribute = context.ActionDescriptor.EndpointMetadata.OfType<NoRepeatSubmitAttribute>().FirstOrDefault();
        if (attribute == null || currentUser.UserId == null)
        {
            return; // 未标注或未登录不判重
        }

        var argsJson = SafeSerialize(context.ActionArguments);
        var argsHash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(argsJson)));
        var key = $"netbase:norepeat:{currentUser.UserId}:{context.HttpContext.Request.Path}:{argsHash}";
        if (cacheService.Exists(key))
        {
            context.Result = new JsonResult(NetBase.Common.Results.ApiResult.Fail("请勿重复提交", NetBase.Common.Results.ApiResultCode.BadRequest, NetBase.Common.Results.ErrorCodes.COMMON_REPEAT_SUBMIT));
            return;
        }
        cacheService.Set(key, true, TimeSpan.FromSeconds(attribute.WindowSeconds));
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    /// <summary>参数序列化：IFormFile 等不可序列化对象降级为类型名</summary>
    private static string SafeSerialize(IDictionary<string, object?> args)
    {
        var safe = new Dictionary<string, object?>();
        foreach (var (name, value) in args)
        {
            safe[name] = value is IFormFile form
                ? $"[file:{form.FileName}:{form.Length}B]"
                : value;
        }
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(safe);
        }
        catch (Exception)
        {
            return string.Join("&", safe.Select(kv => $"{kv.Key}={kv.Value}"));
        }
    }
}
