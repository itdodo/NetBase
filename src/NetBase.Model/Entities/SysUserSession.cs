using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>
/// 用户登录会话。支撑在线用户列表、强制下线与 RefreshToken 轮换；
/// AccessToken 校验时按 TokenId 验证会话存在性。
/// </summary>
[SugarTable("sys_user_session", TableDescription = "用户登录会话表")]
public class SysUserSession : BaseEntity
{
    /// <summary>用户ID</summary>
    [SugarColumn(ColumnDescription = "用户ID")]
    public long UserId { get; set; }

    /// <summary>会话标识（AccessToken 的 jti）</summary>
    [SugarColumn(Length = 64, ColumnDescription = "会话标识")]
    public string TokenId { get; set; } = string.Empty;

    /// <summary>RefreshToken 哈希（不存明文）</summary>
    [SugarColumn(Length = 128, ColumnDescription = "刷新令牌哈希")]
    public string RefreshTokenHash { get; set; } = string.Empty;

    /// <summary>登录IP</summary>
    [SugarColumn(IsNullable = true, Length = 64, ColumnDescription = "登录IP")]
    public string? LoginIp { get; set; }

    /// <summary>浏览器标识</summary>
    [SugarColumn(IsNullable = true, Length = 255, ColumnDescription = "浏览器标识")]
    public string? UserAgent { get; set; }

    /// <summary>登录时间</summary>
    [SugarColumn(ColumnDescription = "登录时间")]
    public DateTime LoginTime { get; set; } = DateTime.Now;

    /// <summary>会话过期时间（RefreshToken 到期，清理依据）</summary>
    [SugarColumn(ColumnDescription = "过期时间")]
    public DateTime ExpireTime { get; set; }
}
