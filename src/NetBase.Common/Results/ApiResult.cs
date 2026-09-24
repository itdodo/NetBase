using System.Text.Json.Serialization;

namespace NetBase.Common.Results;

/// <summary>统一返回码定义</summary>
public static class ApiResultCode
{
    /// <summary>成功</summary>
    public const int Success = 200;

    /// <summary>请求参数错误</summary>
    public const int BadRequest = 400;

    /// <summary>未认证</summary>
    public const int Unauthorized = 401;

    /// <summary>无权限</summary>
    public const int Forbidden = 403;

    /// <summary>资源不存在</summary>
    public const int NotFound = 404;

    /// <summary>并发冲突（乐观锁版本不匹配）</summary>
    public const int Conflict = 409;

    /// <summary>业务处理失败（系统级）</summary>
    public const int Fail = 500;
}

/// <summary>统一 API 返回结果</summary>
public class ApiResult
{
    /// <summary>状态码，200 表示成功</summary>
    [JsonPropertyOrder(0)]
    public int Code { get; set; } = ApiResultCode.Success;

    /// <summary>业务错误码（可选，如 SYS_USER_NOT_FOUND；供前端按码处理）</summary>
    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    /// <summary>提示信息</summary>
    [JsonPropertyOrder(1)]
    public string Message { get; set; } = "操作成功";

    /// <summary>时间戳（毫秒）</summary>
    [JsonPropertyOrder(3)]
    public long Timestamp { get; set; } = DateTimeOffset.Now.ToUnixTimeMilliseconds();

    /// <summary>是否成功</summary>
    [JsonIgnore]
    public bool Success => Code == ApiResultCode.Success;

    public static ApiResult Ok(string message = "操作成功") => new() { Message = message };

    public static ApiResult Fail(string message, int code = ApiResultCode.Fail, string? errorCode = null) =>
        new() { Code = code, Message = message, ErrorCode = errorCode };

    public override string ToString() => $"[{Code}] {Message}";
}

/// <summary>带数据的统一 API 返回结果</summary>
public class ApiResult<T> : ApiResult
{
    [JsonPropertyOrder(2)]
    public T? Data { get; set; }

    public static ApiResult<T> Ok(T? data, string message = "操作成功") => new() { Data = data, Message = message };

    public static ApiResult<T> Ok(T? data, string message, int code) => new() { Data = data, Message = message, Code = code };

    public static new ApiResult<T> Fail(string message, int code = ApiResultCode.Fail, string? errorCode = null) =>
        new() { Code = code, Message = message, ErrorCode = errorCode };
}
