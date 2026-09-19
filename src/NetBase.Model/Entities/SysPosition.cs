using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>岗位（审批权限的载体：角色管菜单/接口权限，岗位管审批人解析）</summary>
[SugarTable("sys_position", TableDescription = "岗位表")]
public class SysPosition : BaseEntity
{
    /// <summary>岗位编码（唯一，审批流节点按此引用）</summary>
    [SugarColumn(Length = 50, ColumnDescription = "岗位编码")]
    public string PositionCode { get; set; } = string.Empty;

    /// <summary>岗位名称</summary>
    [SugarColumn(Length = 50, ColumnDescription = "岗位名称")]
    public string PositionName { get; set; } = string.Empty;

    /// <summary>排序号，越小越靠前</summary>
    [SugarColumn(ColumnDescription = "排序号")]
    public int Sort { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    [SugarColumn(ColumnDescription = "状态：0-停用 1-启用")]
    public int Status { get; set; } = 1;

    /// <summary>备注</summary>
    [SugarColumn(IsNullable = true, Length = 200, ColumnDescription = "备注")]
    public string? Remark { get; set; }
}

/// <summary>用户-岗位关联（多对多，一个用户可兼多岗）</summary>
[SugarTable("sys_user_position", TableDescription = "用户岗位关联表")]
public class SysUserPosition : BaseEntity
{
    /// <summary>用户ID</summary>
    [SugarColumn(ColumnDescription = "用户ID")]
    public long UserId { get; set; }

    /// <summary>岗位ID</summary>
    [SugarColumn(ColumnDescription = "岗位ID")]
    public long PositionId { get; set; }
}
