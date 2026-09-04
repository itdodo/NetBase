using NetBase.Common.Results;

namespace NetBase.Common.Exceptions;

/// <summary>
/// 业务异常，由全局异常过滤器捕获后以 ApiResult 形式返回给前端。
/// </summary>
public class BusinessException : Exception
{
    /// <summary>错误码，默认 500</summary>
    public int Code { get; }

    public BusinessException(string message, int code = ApiResultCode.Fail) : base(message)
    {
        Code = code;
    }

    public BusinessException(int code, string message) : base(message)
    {
        Code = code;
    }
}
