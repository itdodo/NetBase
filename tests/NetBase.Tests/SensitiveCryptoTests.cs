using NetBase.Common.Security;
using Xunit;

namespace NetBase.Tests;

/// <summary>SensitiveCrypto（AES-GCM 可逆加密）契约：往返一致、密文随机化、篡改/密钥变更拒绝</summary>
public class SensitiveCryptoTests
{
    [Fact]
    public void ProtectUnprotect_RoundTrip()
    {
        SensitiveCrypto.Init("test-master-key-0123456789abcdef");
        var cipher = SensitiveCrypto.Protect("smtp-auth-code-秘密123");
        Assert.NotEqual("smtp-auth-code-秘密123", cipher);
        Assert.StartsWith("v1:", cipher);
        Assert.Equal("smtp-auth-code-秘密123", SensitiveCrypto.Unprotect(cipher));
    }

    [Fact]
    public void Protect_SameInput_DifferentCipher()
    {
        SensitiveCrypto.Init("k1");
        var c1 = SensitiveCrypto.Protect("same-value");
        var c2 = SensitiveCrypto.Protect("same-value");
        Assert.NotEqual(c1, c2); // 随机 nonce，同明文不同密文
    }

    [Fact]
    public void Unprotect_TamperedCipher_ReturnsNull()
    {
        SensitiveCrypto.Init("k1");
        var cipher = SensitiveCrypto.Protect("value");
        // 篡改密文末位字符（GCM 认证标签应拦截）
        var tampered = cipher[..^2] + (cipher.EndsWith("A") ? "B" : "A");
        Assert.Null(SensitiveCrypto.Unprotect(tampered));
        Assert.Null(SensitiveCrypto.Unprotect("not-a-valid-cipher"));
        Assert.Null(SensitiveCrypto.Unprotect("v1:bad:bad"));
    }

    [Fact]
    public void Unprotect_DifferentMasterKey_ReturnsNull()
    {
        SensitiveCrypto.Init("key-A");
        var cipher = SensitiveCrypto.Protect("secret");
        SensitiveCrypto.Init("key-B");
        Assert.Null(SensitiveCrypto.Unprotect(cipher));
    }
}
