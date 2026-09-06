using NetBase.Model.Enums;
using System.Text.Json.Serialization;
using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>系统用户</summary>
[SugarTable("sys_user", TableDescription = "系统用户表")]
public class SysUser : BaseEntity, IDataScope
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

    /// <summary>头像地址（/api/v1/file/{id}）</summary>
    [SugarColumn(IsNullable = true, Length = 255, ColumnDescription = "头像地址")]
    public string? Avatar { get; set; }

    /// <summary>所属部门ID（数据权限维度；存量用户为 0 表示未分配）</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [SugarColumn(ColumnDescription = "所属部门ID", DefaultValue = "0")]
    public long DeptId { get; set; }

    /// <summary>数据归属人（"仅本人"数据范围使用；创建时填充为自身ID，存量数据由种子回填）</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [SugarColumn(ColumnDescription = "数据归属人", DefaultValue = "0")]
    public long OwnerUserId { get; set; }
}
