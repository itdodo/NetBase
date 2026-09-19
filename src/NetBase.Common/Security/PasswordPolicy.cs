namespace NetBase.Common.Security;

/// <summary>
/// 密码复杂度策略（服务端强制，前端提示仅作辅助）。
/// 策略参数后续可迁移至系统参数配置。
/// </summary>
public static class PasswordPolicy
{
    /// <summary>最小长度</summary>
    public const int MinLength = 6;

    /// <summary>最大长度（与实体列长度对齐）</summary>
    public const int MaxLength = 64;

    /// <summary>
    /// 校验密码复杂度，不合规时返回错误消息。
    /// trustedPassword：系统配置的默认密码（如 sys.pwd.defaultPassword）——
    /// 由管理员掌控并配合"首次登录强制改密"策略使用，豁免复杂度校验。
    /// </summary>
    public static string? Validate(string? password, string? trustedPassword = null)
    {
        if (!string.IsNullOrEmpty(trustedPassword) && password == trustedPassword)
        {
            return null;
        }
        return ValidateCore(password);
    }

    private static string? ValidateCore(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "密码不能为空";
        }
        if (password.Length < MinLength || password.Length > MaxLength)
        {
            return $"密码长度须为 {MinLength}-{MaxLength} 位";
        }
        if (!password.Any(char.IsLetter))
        {
            return "密码必须包含字母";
        }
        if (!password.Any(char.IsDigit))
        {
            return "密码必须包含数字";
        }
        return null;
    }
}
