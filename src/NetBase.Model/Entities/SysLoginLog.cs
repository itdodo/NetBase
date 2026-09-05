using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>登录日志（成功与失败均记录，满足安全审计要求）</summary>
[SugarTable("sys_login_log", TableDescription = "登录日志表")]
public class SysLoginLog : BaseEntity
{
    /// <summary>用户ID（登录失败且账号不存在时为 0）</summary>
    [SugarColumn(ColumnDescription = "用户ID")]
    public long UserId { get; set; }

    /// <summary>尝试登录的用户名</summary>
    [SugarColumn(Length = 50, ColumnDescription = "用户名")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>是否成功</summary>
    [SugarColumn(ColumnDescription = "是否成功")]
    public bool Success { get; set; }

    /// <summary>结果描述（成功 / 用户名或密码错误 / 账号停用 / 锁定等）</summary>
    [SugarColumn(IsNullable = true, Length = 200, ColumnDescription = "结果描述")]
    public string? Message { get; set; }

    /// <summary>登录IP</summary>
    [SugarColumn(IsNullable = true, Length = 64, ColumnDescription = "登录IP")]
    public string? Ip { get; set; }

    /// <summary>浏览器标识</summary>
    [SugarColumn(IsNullable = true, Length = 255, ColumnDescription = "浏览器标识")]
    public string? UserAgent { get; set; }
}
