namespace NetBase.Model.Dtos;

/// <summary>菜单树节点</summary>
public class MenuTreeDto
{
    public long Id { get; set; }

    public long ParentId { get; set; }

    public string MenuName { get; set; } = string.Empty;

    /// <summary>类型：1-目录 2-菜单 3-按钮</summary>
    public int MenuType { get; set; }

    public string? Path { get; set; }

    public string? Component { get; set; }

    /// <summary>权限标识</summary>
    public string? Permission { get; set; }

    public string? Icon { get; set; }

    public int Sort { get; set; }

    public bool Visible { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    public int Status { get; set; }

    public DateTime CreateTime { get; set; }

    /// <summary>子节点</summary>
    public List<MenuTreeDto> Children { get; set; } = [];
}

/// <summary>创建/更新菜单请求</summary>
public class MenuSaveDto
{
    /// <summary>父级菜单ID，顶级为 0</summary>
    public long ParentId { get; set; }

    /// <summary>菜单名称</summary>
    public string MenuName { get; set; } = string.Empty;

    /// <summary>类型：1-目录 2-菜单 3-按钮</summary>
    public int MenuType { get; set; }

    /// <summary>路由地址</summary>
    public string? Path { get; set; }

    /// <summary>前端组件路径</summary>
    public string? Component { get; set; }

    /// <summary>权限标识</summary>
    public string? Permission { get; set; }

    /// <summary>图标</summary>
    public string? Icon { get; set; }

    /// <summary>排序号</summary>
    public int Sort { get; set; }

    /// <summary>是否可见</summary>
    public bool Visible { get; set; } = true;

    /// <summary>状态：0-停用 1-启用</summary>
    public int Status { get; set; } = 1;
}
