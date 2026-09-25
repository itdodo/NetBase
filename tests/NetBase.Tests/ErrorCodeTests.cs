using NetBase.Common.Exceptions;
using NetBase.Common.Results;
using Xunit;

namespace NetBase.Tests;

/// <summary>
/// 全局错误码体系契约：BusinessException 默认码/重载、ApiResult.Fail errorCode 透传。
/// 码表语义锁定——业务默认 400、乐观锁 409、系统级 500，前端分支依赖这些值。
/// </summary>
public class ErrorCodeTests
{
    [Fact]
    public void BusinessException_DefaultCode_Is400()
    {
        // 业务规则错误默认 400（曾是 500——普通业务错误不该伪装系统故障）
        var ex = new BusinessException("仅草稿可修改");
        Assert.Equal(ApiResultCode.BadRequest, ex.Code);
        Assert.Null(ex.ErrorCode);
    }

    [Fact]
    public void BusinessException_WithErrorCode_DefaultsTo400()
    {
        var ex = new BusinessException("部门下存在用户，不允许删除", ErrorCodes.SYS_DEPT_HAS_USERS);
        Assert.Equal(ApiResultCode.BadRequest, ex.Code);
        Assert.Equal(ErrorCodes.SYS_DEPT_HAS_USERS, ex.ErrorCode);
    }

    [Fact]
    public void BusinessException_WithCodeAndErrorCode_KeepsBoth()
    {
        var ex = new BusinessException("用户名或密码错误", ApiResultCode.BadRequest, ErrorCodes.AUTH_BAD_CREDENTIALS);
        Assert.Equal(ApiResultCode.BadRequest, ex.Code);
        Assert.Equal(ErrorCodes.AUTH_BAD_CREDENTIALS, ex.ErrorCode);
    }

    [Fact]
    public void ApiResult_Fail_DefaultsTo500_WithoutErrorCode()
    {
        var result = ApiResult.Fail("系统繁忙，请稍后重试");
        Assert.Equal(ApiResultCode.Fail, result.Code);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void ApiResult_Fail_PassesErrorCode()
    {
        var result = ApiResult.Fail("系统繁忙，请稍后重试", ApiResultCode.Fail, ErrorCodes.COMMON_SYSTEM_ERROR);
        Assert.Equal(ApiResultCode.Fail, result.Code);
        Assert.Equal(ErrorCodes.COMMON_SYSTEM_ERROR, result.ErrorCode);
    }

    [Fact]
    public void ApiResultT_Fail_PassesErrorCode()
    {
        var result = ApiResult<string>.Fail("请选择头像文件", ApiResultCode.BadRequest, ErrorCodes.COMMON_FILE_REQUIRED);
        Assert.Equal(ApiResultCode.BadRequest, result.Code);
        Assert.Equal(ErrorCodes.COMMON_FILE_REQUIRED, result.ErrorCode);
    }

    [Fact]
    public void ApiResult_Ok_Is200()
    {
        Assert.Equal(ApiResultCode.Success, ApiResult.Ok().Code);
        Assert.True(ApiResult.Ok().Success);
    }

    [Fact]
    public void ErrorCodes_NameMatchesValue()
    {
        // 反射锁定「码名=码值」约定：前端镜像 types/errorCodes.ts 依赖此恒等
        var fields = typeof(ErrorCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.True(fields.Length >= 100, $"码表应至少 100 个码，实际 {fields.Length}");
        foreach (var field in fields)
        {
            Assert.Equal(field.Name, (string?)field.GetRawConstantValue());
        }
    }
}
