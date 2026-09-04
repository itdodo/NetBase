using NetBase.Model.Enums;
using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>系统用户</summary>
[SugarTable("sys_user", TableDescription = "系统用户表")]
public class SysUser : BaseEntity
{
    /// <summary>用户名（登录账号）</summary>
    [SugarColumn(Length = 50, ColumnDescription = "用户名")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>密码（哈希存储）</summary>
    [SugarColumn(Length = 128, ColumnDescription = "密码哈希")]
    public string Password { get; set; } = string.Empty;

    /// <summary>昵称</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "昵称")]
    public string? NickName { get; set; }

    /// <summary>手机号</summary>
    [SugarColumn(IsNullable = true, Length = 20, ColumnDescription = "手机号")]
    public string? Phone { get; set; }

    /// <summary>邮箱</summary>
    [SugarColumn(IsNullable = true, Length = 100, ColumnDescription = "邮箱")]
    public string? Email { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    [SugarColumn(ColumnDescription = "状态：0-停用 1-启用")]
    public int Status { get; set; } = (int)StatusEnum.Enabled;

    /// <summary>最后登录时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "最后登录时间")]
    public DateTime? LastLoginTime { get; set; }
}
