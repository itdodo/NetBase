using Microsoft.AspNetCore.Mvc;
using NetBase.Common.Results;

namespace NetBase.Api.Controllers;

/// <summary>控制器基类：统一路由与返回封装</summary>
[ApiController]
public abstract class BaseController : ControllerBase
{
    /// <summary>成功返回（无数据）</summary>
    protected static ApiResult Success(string message = "操作成功") => ApiResult.Ok(message);

    /// <summary>成功返回（带数据）</summary>
    protected static ApiResult<T> Success<T>(T data, string message = "操作成功") => ApiResult<T>.Ok(data, message);

    /// <summary>失败返回</summary>
    protected static ApiResult Fail(string message, int code = ApiResultCode.Fail) => ApiResult.Fail(message, code);
}
