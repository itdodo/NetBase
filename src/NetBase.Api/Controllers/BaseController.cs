using Microsoft.AspNetCore.Mvc;
using NetBase.Common.Results;
using NetBase.Common.Users;

namespace NetBase.Api.Controllers;

/// <summary>控制器基类：统一路由、返回封装与当前用户</summary>
[ApiController]
public abstract class BaseController(ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>当前登录用户名（认证接入前回退为 system，保证审计字段始终有值）</summary>
    protected string OperatorName => currentUserService.UserName ?? "system";

    /// <summary>成功返回（无数据）</summary>
    protected static ApiResult Success(string message = "操作成功") => ApiResult.Ok(message);

    /// <summary>成功返回（带数据）</summary>
    protected static ApiResult<T> Success<T>(T data, string message = "操作成功") => ApiResult<T>.Ok(data, message);

    /// <summary>失败返回</summary>
    protected static ApiResult Fail(string message, int code = ApiResultCode.Fail) => ApiResult.Fail(message, code);
}
