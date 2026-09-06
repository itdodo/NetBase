using SqlSugar;
using System.Text.Json.Serialization;

namespace NetBase.Model.Entities;

/// <summary>角色-菜单关联</summary>
[SugarTable("sys_role_menu", TableDescription = "角色菜单关联表")]
public class SysRoleMenu : BaseEntity
{
    /// <summary>角色ID</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [SugarColumn(ColumnDescription = "角色ID")]
    public long RoleId { get; set; }

    /// <summary>菜单ID</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [SugarColumn(ColumnDescription = "菜单ID")]
    public long MenuId { get; set; }
}
