using System.ComponentModel.DataAnnotations;
using NetBase.Common.Security;
using NetBase.Model.Dtos;
using Xunit;

namespace NetBase.Tests;

/// <summary>DTO 验证特性测试：服务端强制校验生效</summary>
public class DtoValidationTests
{
    private static List<ValidationResult> Validate(object instance)
    {
        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void LoginRequest_Empty_ShouldFail()
    {
        var results = Validate(new LoginRequestDto { UserName = "", Password = "" });
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void LoginRequest_ShortPassword_ShouldFail()
    {
        var results = Validate(new LoginRequestDto { UserName = "admin", Password = "123" });
        Assert.Contains(results, r => r.MemberNames.Contains("Password"));
    }

    [Fact]
    public void LoginRequest_Valid_ShouldPass()
    {
        Assert.Empty(Validate(new LoginRequestDto { UserName = "admin", Password = "123456" }));
    }

    [Fact]
    public void UserCreate_InvalidUserName_ShouldFail()
    {
        var dto = new UserCreateDto { UserName = "非法名称!", Password = "abc123" };
        var results = Validate(dto);
        Assert.Contains(results, r => r.MemberNames.Contains("UserName"));
    }

    [Fact]
    public void UserCreate_BadEmail_ShouldFail()
    {
        var dto = new UserCreateDto { UserName = "tester", Email = "not-an-email" };
        var results = Validate(dto);
        Assert.Contains(results, r => r.MemberNames.Contains("Email"));
    }

    [Fact]
    public void MenuSave_InvalidType_ShouldFail()
    {
        var dto = new MenuSaveDto { MenuName = "菜单", MenuType = 9 };
        var results = Validate(dto);
        Assert.Contains(results, r => r.MemberNames.Contains("MenuType"));
    }

    [Fact]
    public void RoleSave_Empty_ShouldFail()
    {
        var results = Validate(new RoleSaveDto { RoleName = "", RoleCode = "" });
        Assert.Equal(2, results.Count);
    }
}

/// <summary>敏感数据脱敏测试（操作日志参数记录的核心安全逻辑）</summary>
public class SensitiveDataTests
{
    [Fact]
    public void Serialize_ShouldMaskPasswordField()
    {
        var json = SensitiveData.Serialize(new { userName = "admin", password = "Secret123!" });
        Assert.DoesNotContain("Secret123!", json);
        Assert.Contains("***", json);
        Assert.Contains("admin", json);
    }

    [Fact]
    public void Serialize_ShouldMaskNestedPasswordField()
    {
        var json = SensitiveData.Serialize(new
        {
            oldPassword = "Old123!",
            NewPassword = "New456!"
        });
        Assert.DoesNotContain("Old123!", json);
        Assert.DoesNotContain("New456!", json);
    }

    [Theory]
    [InlineData("refreshToken", "RFSecretValue")]
    [InlineData("accessToken", "ACSecretValue")]
    [InlineData("clientSecret", "CSecretValue")]
    public void Serialize_ShouldMaskTokenLikeFieldsByKeyword(string fieldName, string value)
    {
        // 词根匹配：refreshToken/accessToken/clientSecret 等衍生字段均需脱敏
        var json = SensitiveData.Serialize(new Dictionary<string, string> { [fieldName] = value });
        Assert.DoesNotContain(value, json);
        Assert.Contains("***", json);
    }

    [Fact]
    public void Serialize_ShouldHandleNull()
    {
        Assert.Equal(string.Empty, SensitiveData.Serialize(null));
    }

    [Fact]
    public void Serialize_ShouldTruncateLongContent()
    {
        var json = SensitiveData.Serialize(new { data = new string('x', 5000) }, maxLength: 100);
        Assert.True(json.Length <= 120);
        Assert.EndsWith("...(truncated)", json);
    }
}
