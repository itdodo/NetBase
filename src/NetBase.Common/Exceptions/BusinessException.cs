using NetBase.Common.Results;

namespace NetBase.Common.Exceptions;

/// <summary>
/// 业务异常，由全局异常过滤器捕获后以 ApiResult 形式返回给前端。
/// </summary>
public class BusinessException : Exception
{
    /// <summary>状态码，默认 400（业务规则错误；系统级故障才是 500）</summary>
    public int Code { get; }

    /// <summary>全局业务错误码（ErrorCodes 常量，供前端按码处理）</summary>
    public string? ErrorCode { get; }

    public BusinessException(string message, int code = ApiResultCode.BadRequest, string? errorCode = null) : base(message)
    {
        Code = code;
        ErrorCode = errorCode;
    }

    /// <summary>常用重载：业务规则错误（code=400）+ 全局错误码</summary>
    public BusinessException(string message, string errorCode) : base(message)
    {
        Code = ApiResultCode.BadRequest;
        ErrorCode = errorCode;
    }

    public BusinessException(int code, string message) : base(message)
    {
        Code = code;
    }
}
