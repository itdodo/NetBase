using SqlSugar;
using System.Text.Json.Serialization;

namespace NetBase.Model.Entities;

/// <summary>用户-角色关联</summary>
[SugarTable("sys_user_role", TableDescription = "用户角色关联表")]
public class SysUserRole : BaseEntity
{
    /// <summary>用户ID</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [SugarColumn(ColumnDescription = "用户ID")]
    public long UserId { get; set; }

    /// <summary>角色ID</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [SugarColumn(ColumnDescription = "角色ID")]
    public long RoleId { get; set; }
}
