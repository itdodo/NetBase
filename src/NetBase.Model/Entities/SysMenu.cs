using NetBase.Model.Enums;
using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>
/// 系统菜单。目录/菜单/按钮统一存储，按钮类型的 Permission 字段即 API 权限码，
/// 为后续接入接口鉴权预留。
/// </summary>
[SugarTable("sys_menu", TableDescription = "系统菜单表")]
public class SysMenu : BaseEntity
{
    /// <summary>父级菜单ID，顶级为 0</summary>
    [SugarColumn(ColumnDescription = "父级菜单ID")]
    public long ParentId { get; set; }

    /// <summary>菜单名称</summary>
    [SugarColumn(Length = 50, ColumnDescription = "菜单名称")]
    public string MenuName { get; set; } = string.Empty;

    /// <summary>类型：1-目录 2-菜单 3-按钮</summary>
    [SugarColumn(ColumnDescription = "类型：1-目录 2-菜单 3-按钮")]
    public int MenuType { get; set; } = (int)MenuTypeEnum.Menu;

    /// <summary>路由地址（目录/菜单）</summary>
    [SugarColumn(IsNullable = true, Length = 200, ColumnDescription = "路由地址")]
    public string? Path { get; set; }

    /// <summary>前端组件路径（菜单）</summary>
    [SugarColumn(IsNullable = true, Length = 200, ColumnDescription = "组件路径")]
    public string? Component { get; set; }

    /// <summary>权限标识，如 sys:user:add（按钮）</summary>
    [SugarColumn(IsNullable = true, Length = 100, ColumnDescription = "权限标识")]
    public string? Permission { get; set; }

    /// <summary>图标</summary>
    [SugarColumn(IsNullable = true, Length = 100, ColumnDescription = "图标")]
    public string? Icon { get; set; }

    /// <summary>排序号，越小越靠前</summary>
    [SugarColumn(ColumnDescription = "排序号")]
    public int Sort { get; set; }

    /// <summary>是否可见</summary>
    [SugarColumn(ColumnDescription = "是否可见")]
    public bool Visible { get; set; } = true;

    /// <summary>状态：0-停用 1-启用</summary>
    [SugarColumn(ColumnDescription = "状态：0-停用 1-启用")]
    public int Status { get; set; } = (int)StatusEnum.Enabled;
}
