using System.ComponentModel.DataAnnotations;

using System.Text.Json.Serialization;
namespace NetBase.Model.Dtos;

/// <summary>菜单树节点</summary>
public class MenuTreeDto
{
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long Id { get; set; }

    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
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

    /// <summary>并发版本（编辑保存时原样回传，服务端比对拦截陈旧提交）</summary>
    public long Version { get; set; }

    public DateTime CreateTime { get; set; }

    /// <summary>子节点</summary>
    public List<MenuTreeDto> Children { get; set; } = [];
}

/// <summary>创建/更新菜单请求</summary>
public class MenuSaveDto
{
    /// <summary>父级菜单ID，顶级为 0</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [Range(0, long.MaxValue, ErrorMessage = "父级菜单ID无效")]
    public long ParentId { get; set; }

    /// <summary>菜单名称</summary>
    [Required(ErrorMessage = "菜单名称不能为空")]
    [StringLength(50, ErrorMessage = "菜单名称长度不能超过 50")]
    public string MenuName { get; set; } = string.Empty;

    /// <summary>类型：1-目录 2-菜单 3-按钮</summary>
    [Range(1, 3, ErrorMessage = "菜单类型无效（1-目录 2-菜单 3-按钮）")]
    public int MenuType { get; set; }

    /// <summary>路由地址</summary>
    [StringLength(200, ErrorMessage = "路由地址长度不能超过 200")]
    public string? Path { get; set; }

    /// <summary>前端组件路径</summary>
    [StringLength(200, ErrorMessage = "组件路径长度不能超过 200")]
    public string? Component { get; set; }

    /// <summary>权限标识</summary>
    [StringLength(100, ErrorMessage = "权限标识长度不能超过 100")]
    [RegularExpression(@"^[a-zA-Z0-9:_\-]*$", ErrorMessage = "权限标识只能包含字母、数字、冒号、下划线、中划线")]
    public string? Permission { get; set; }

    /// <summary>图标</summary>
    [StringLength(100, ErrorMessage = "图标长度不能超过 100")]
    public string? Icon { get; set; }

    /// <summary>排序号</summary>
    [Range(0, int.MaxValue, ErrorMessage = "排序号不能为负")]
    public int Sort { get; set; }

    /// <summary>是否可见</summary>
    public bool Visible { get; set; } = true;

    /// <summary>状态：0-停用 1-启用</summary>
    [Range(0, 1, ErrorMessage = "状态取值无效")]
    public int Status { get; set; } = 1;

    /// <summary>并发版本（编辑时读取、保存时回传；与他人先提交的版本不一致则拒绝，不传则跳过校验）</summary>
    public long? Version { get; set; }
}
