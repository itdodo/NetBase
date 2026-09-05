namespace NetBase.Service.Sys;

/// <summary>Jwt 认证配置（appsettings.json 的 Jwt 节点）</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "NetBase";

    public string Audience { get; set; } = "NetBase.Web";

    /// <summary>签名密钥，长度至少 32 字符；生产环境用环境变量覆盖</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>AccessToken 有效期（分钟）</summary>
    public int AccessTokenExpireMinutes { get; set; } = 120;

    /// <summary>RefreshToken 有效期（天）</summary>
    public int RefreshTokenExpireDays { get; set; } = 7;
}
