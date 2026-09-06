using NetBase.Model.Enums;
using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>系统角色</summary>
[SugarTable("sys_role", TableDescription = "系统角色表")]
public class SysRole : BaseEntity
{
    /// <summary>角色名称</summary>
    [SugarColumn(Length = 50, ColumnDescription = "角色名称")]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>角色编码（唯一）</summary>
    [SugarColumn(Length = 50, ColumnDescription = "角色编码")]
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>状态：0-停用 1-启用</summary>
    [SugarColumn(ColumnDescription = "状态：0-停用 1-启用")]
    public int Status { get; set; } = (int)StatusEnum.Enabled;

    /// <summary>排序号，越小越靠前</summary>
    [SugarColumn(ColumnDescription = "排序号")]
    public int Sort { get; set; }

    /// <summary>数据范围：1-全部 2-自定义 3-本部门 4-本部门及以下 5-仅本人</summary>
    [SugarColumn(ColumnDescription = "数据范围", DefaultValue = "1")]
    public int DataScope { get; set; } = (int)DataScopeEnum.All;
}
