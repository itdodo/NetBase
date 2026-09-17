using System.ComponentModel.DataAnnotations;
using NetBase.Model.Dtos;
using Xunit;

namespace NetBase.Tests;

/// <summary>可选手机号/邮箱特性测试：空白通过、有值按格式校验</summary>
public class OptionalFormatTests
{
    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }

    private static UserCreateDto Create(string? phone = null, string? email = null) =>
        new() { UserName = "tester", Password = "Net123456", Status = 1, Phone = phone, Email = email };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPhoneOrEmail_ShouldPass(string? value)
    {
        var results = Validate(Create(value, value));
        Assert.DoesNotContain(results, r => r.ErrorMessage == "手机号格式不正确");
        Assert.DoesNotContain(results, r => r.ErrorMessage == "邮箱格式不正确");
    }

    [Fact]
    public void ValidPhoneOrEmail_ShouldPass()
    {
        var results = Validate(Create("13800138000", "a@b.com"));
        Assert.Empty(results);
    }

    [Fact]
    public void InvalidPhone_ShouldFail()
    {
        var results = Validate(Create("abc", null));
        Assert.Contains(results, r => r.ErrorMessage == "手机号格式不正确");
    }
}
