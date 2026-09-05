using System.Security.Cryptography;
using System.Text;

namespace NetBase.Common.Security;

/// <summary>
/// 密码哈希工具。
/// 新哈希采用 PBKDF2（随机盐 + SHA256 + 10万次迭代），格式：PBKDF2$迭代次数$盐$哈希；
/// Verify 兼容框架早期版本的 SHA256+固定盐哈希，存量账号无需重置即可通过校验。
/// </summary>
public static class PasswordHelper
{
    /// <summary>旧版（SHA256 固定盐）盐值，仅用于兼容校验历史哈希</summary>
    private const string LegacySalt = "NetBase.v1";

    private const string Pbkdf2Prefix = "PBKDF2$";
    private const int Pbkdf2Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <summary>新用户默认密码</summary>
    public const string DefaultPassword = "Net123456";

    public static string Encrypt(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Pbkdf2Prefix}{Pbkdf2Iterations}${Convert.ToHexString(salt)}${Convert.ToHexString(hash)}";
    }

    public static bool Verify(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
        {
            return false;
        }

        if (hashedPassword.StartsWith(Pbkdf2Prefix, StringComparison.Ordinal))
        {
            return VerifyPbkdf2(password, hashedPassword);
        }

        // 历史哈希（SHA256 固定盐）兼容校验
        return FixedTimeEquals(EncryptLegacy(password), hashedPassword);
    }

    private static bool VerifyPbkdf2(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromHexString(parts[2]);
            var expected = Convert.FromHexString(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string EncryptLegacy(string password)
    {
        var bytes = Encoding.UTF8.GetBytes($"{LegacySalt}:{password}");
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static bool FixedTimeEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
