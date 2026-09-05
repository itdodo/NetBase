using System.Text;
using System.Security.Cryptography;
using NetBase.Common.Security;
using Xunit;

namespace NetBase.Tests;

/// <summary>密码哈希测试：PBKDF2 新格式与历史 SHA256 兼容</summary>
public class PasswordHelperTests
{
    [Fact]
    public void Encrypt_ShouldUsePbkdf2Format()
    {
        var hash = PasswordHelper.Encrypt("abc123");
        Assert.StartsWith("PBKDF2$", hash);
    }

    [Fact]
    public void Encrypt_ShouldUseRandomSalt()
    {
        Assert.NotEqual(PasswordHelper.Encrypt("abc123"), PasswordHelper.Encrypt("abc123"));
    }

    [Theory]
    [InlineData("abc123", true)]
    [InlineData("wrong", false)]
    public void Verify_ShouldCheckPbkdf2Hash(string input, bool expected)
    {
        var hash = PasswordHelper.Encrypt("abc123");
        Assert.Equal(expected, PasswordHelper.Verify(input, hash));
    }

    [Fact]
    public void Verify_ShouldSupportLegacySha256Hash()
    {
        // 模拟历史版本的 SHA256+固定盐哈希（存量账号升级兼容）
        var legacy = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"NetBase.v1:123456")));
        Assert.True(PasswordHelper.Verify("123456", legacy));
        Assert.False(PasswordHelper.Verify("111111", legacy));
    }

    [Theory]
    [InlineData("PBKDF2$bad$data")]
    [InlineData("")]
    public void Verify_ShouldRejectMalformedHash(string stored)
    {
        Assert.False(PasswordHelper.Verify("any", stored));
    }
}

/// <summary>密码复杂度策略测试</summary>
public class PasswordPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldRejectEmpty(string? password)
    {
        Assert.NotNull(PasswordPolicy.Validate(password));
    }

    [Theory]
    [InlineData("a1")]
    [InlineData("12345")]
    public void Validate_ShouldRejectTooShort(string password)
    {
        Assert.Contains("长度", PasswordPolicy.Validate(password));
    }

    [Fact]
    public void Validate_ShouldRejectNoLetter()
    {
        Assert.Contains("字母", PasswordPolicy.Validate("123456"));
    }

    [Fact]
    public void Validate_ShouldRejectNoDigit()
    {
        Assert.Contains("数字", PasswordPolicy.Validate("abcdef"));
    }

    [Theory]
    [InlineData("abc123")]
    [InlineData("Abc12345")]
    public void Validate_ShouldAcceptValid(string password)
    {
        Assert.Null(PasswordPolicy.Validate(password));
    }

    [Fact]
    public void Validate_ShouldRejectTooLong()
    {
        Assert.Contains("长度", PasswordPolicy.Validate(new string('a', 65) + "1"));
    }
}
