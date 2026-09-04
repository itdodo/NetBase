using System.Security.Cryptography;
using System.Text;

namespace NetBase.Common.Security;

/// <summary>
/// 密码哈希工具（SHA256 加盐）。
/// 注意：当前阶段仅作为占位实现，接入正式认证时建议升级为 BCrypt/PBKDF2 等慢哈希算法。
/// </summary>
public static class PasswordHelper
{
    private const string Salt = "NetBase.v1";

    /// <summary>新用户默认密码</summary>
    public const string DefaultPassword = "123456";

    public static string Encrypt(string password)
    {
        var bytes = Encoding.UTF8.GetBytes($"{Salt}:{password}");
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public static bool Verify(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
        {
            return false;
        }

        return string.Equals(Encrypt(password), hashedPassword, StringComparison.OrdinalIgnoreCase);
    }
}
