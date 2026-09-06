using NetBase.Common.Results;

namespace NetBase.Common.Exceptions;

/// <summary>
/// 业务异常，由全局异常过滤器捕获后以 ApiResult 形式返回给前端。
/// </summary>
public class BusinessException : Exception
{
    /// <summary>状态码，默认 500</summary>
    public int Code { get; }

    /// <summary>业务错误码（可选，供前端按码处理）</summary>
    public string? ErrorCode { get; }

    public BusinessException(string message, int code = ApiResultCode.Fail, string? errorCode = null) : base(message)
    {
        Code = code;
        ErrorCode = errorCode;
    }

    public BusinessException(int code, string message) : base(message)
    {
        Code = code;
    }
}
