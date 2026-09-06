using SqlSugar;
using System.Text.Json.Serialization;

namespace NetBase.Model.Entities;

/// <summary>部门（组织架构树）</summary>
[SugarTable("sys_dept", TableDescription = "部门表")]
public class SysDept : BaseEntity
{
    /// <summary>父级部门ID，顶级为 0</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [SugarColumn(ColumnDescription = "父级部门ID")]
    public long ParentId { get; set; }

    /// <summary>部门名称</summary>
    [SugarColumn(Length = 50, ColumnDescription = "部门名称")]
    public string DeptName { get; set; } = string.Empty;

    /// <summary>部门编码（唯一）</summary>
    [SugarColumn(Length = 50, ColumnDescription = "部门编码")]
    public string DeptCode { get; set; } = string.Empty;

    /// <summary>负责人</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "负责人")]
    public string? Leader { get; set; }

    /// <summary>排序号，越小越靠前</summary>
    [SugarColumn(ColumnDescription = "排序号")]
    public int Sort { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    [SugarColumn(ColumnDescription = "状态：0-停用 1-启用")]
    public int Status { get; set; } = 1;
}

/// <summary>角色自定义数据权限-部门勾选</summary>
[SugarTable("sys_role_dept", TableDescription = "角色部门关联表")]
public class SysRoleDept : BaseEntity
{
    /// <summary>角色ID</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [SugarColumn(ColumnDescription = "角色ID")]
    public long RoleId { get; set; }

    /// <summary>部门ID</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [SugarColumn(ColumnDescription = "部门ID")]
    public long DeptId { get; set; }
}
