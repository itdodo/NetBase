using System.ComponentModel.DataAnnotations;
using NetBase.Common.Results;

using System.Text.Json.Serialization;
namespace NetBase.Model.Dtos;

/// <summary>角色分页查询条件</summary>
public class RoleQueryDto : PageQuery
{
    /// <summary>角色名称/编码关键字</summary>
    [StringLength(50, ErrorMessage = "关键字长度不能超过 50")]
    public string? Keyword { get; set; }

    /// <summary>状态过滤</summary>
    [Range(0, 1, ErrorMessage = "状态取值无效")]
    public int? Status { get; set; }
}

/// <summary>角色返回</summary>
public class RoleDto
{
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long Id { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string RoleCode { get; set; } = string.Empty;

    /// <summary>状态：0-停用 1-启用</summary>
    public int Status { get; set; }

    public int Sort { get; set; }

    /// <summary>数据范围：1-全部 2-自定义 3-本部门 4-本部门及以下 5-仅本人</summary>
    public int DataScope { get; set; }

    /// <summary>并发版本（编辑保存时原样回传，服务端比对拦截陈旧提交）</summary>
    public long Version { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>角色简要信息（用于下拉框、用户角色展示）</summary>
public class RoleSimpleDto
{
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long Id { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string RoleCode { get; set; } = string.Empty;
}

/// <summary>创建/更新角色请求</summary>
public class RoleSaveDto
{
    /// <summary>角色名称</summary>
    [Required(ErrorMessage = "角色名称不能为空")]
    [StringLength(50, ErrorMessage = "角色名称长度不能超过 50")]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>角色编码（唯一）</summary>
    [Required(ErrorMessage = "角色编码不能为空")]
    [StringLength(50, ErrorMessage = "角色编码长度不能超过 50")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "角色编码只能包含字母、数字、下划线")]
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>状态：0-停用 1-启用</summary>
    [Range(0, 1, ErrorMessage = "状态取值无效")]
    public int Status { get; set; } = 1;

    /// <summary>排序号</summary>
    [Range(0, int.MaxValue, ErrorMessage = "排序号不能为负")]
    public int Sort { get; set; }

    /// <summary>数据范围：1-全部 2-自定义 3-本部门 4-本部门及以下 5-仅本人</summary>
    [Range(1, 5)]
    public int DataScope { get; set; } = 1;

    /// <summary>自定义数据权限的部门ID列表（DataScope=2 时生效）</summary>
    public List<long>? DeptIds { get; set; }

    /// <summary>并发版本（编辑时读取、保存时回传；与他人先提交的版本不一致则拒绝，不传则跳过校验）</summary>
    public long? Version { get; set; }
}

/// <summary>分配菜单请求</summary>
public class AssignMenusDto
{
    /// <summary>菜单ID列表</summary>
    public List<long> MenuIds { get; set; } = [];
}
