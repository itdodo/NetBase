using System.Security.Cryptography;
using System.Text;

namespace NetBase.Common.Security;

/// <summary>
/// 敏感值可逆加密（AES-GCM）：SMTP 授权码等需取回原文的配置项用。
/// 主密钥 = SHA256(Jwt:SecretKey)，程序启动时初始化（与雪花 IdGenerator 同模式）；
/// 密文格式：v1:{nonce Base64}:{ciphertext+tag Base64}。
/// </summary>
public static class SensitiveCrypto
{
    private static byte[]? _masterKey;

    /// <summary>程序启动时以主密钥字符串初始化（须在首次加解密前调用）</summary>
    public static void Init(string masterKey)
    {
        if (string.IsNullOrEmpty(masterKey))
        {
            throw new ArgumentException("SensitiveCrypto 主密钥不能为空", nameof(masterKey));
        }
        _masterKey = SHA256.HashData(Encoding.UTF8.GetBytes(masterKey));
    }

    public static bool IsInitialized => _masterKey != null;

    public static string Protect(string plain)
    {
        ArgumentNullException.ThrowIfNull(_masterKey);
        var plainBytes = Encoding.UTF8.GetBytes(plain);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_masterKey, 16);
        aes.Encrypt(nonce, plainBytes, cipher, tag);
        return $"v1:{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(cipher.Concat(tag).ToArray())}";
    }

    /// <summary>解密；密文非法或被篡改返回 null（GCM 认证失败）</summary>
    public static string? Unprotect(string cipherText)
    {
        ArgumentNullException.ThrowIfNull(_masterKey);
        try
        {
            var parts = cipherText.Split(':');
            if (parts.Length != 3 || parts[0] != "v1")
            {
                return null;
            }
            var nonce = Convert.FromBase64String(parts[1]);
            var cipherWithTag = Convert.FromBase64String(parts[2]);
            if (cipherWithTag.Length < 16)
            {
                return null;
            }
            var cipher = cipherWithTag[..^16];
            var tag = cipherWithTag[^16..];
            var plain = new byte[cipher.Length];
            using var aes = new AesGcm(_masterKey, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
