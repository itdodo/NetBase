using System.ComponentModel.DataAnnotations;

using System.Text.Json.Serialization;
namespace NetBase.Model.Dtos;

/// <summary>部门树节点</summary>
public class DeptTreeDto
{
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long Id { get; set; }

    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long ParentId { get; set; }

    public string DeptName { get; set; } = string.Empty;

    public string DeptCode { get; set; } = string.Empty;

    public string? Leader { get; set; }

    public int Sort { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    public int Status { get; set; }

    public DateTime CreateTime { get; set; }

    /// <summary>子部门</summary>
    public List<DeptTreeDto> Children { get; set; } = [];
}

/// <summary>部门保存请求</summary>
public class DeptSaveDto
{
    /// <summary>父级部门ID，顶级为 0</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [Range(0, long.MaxValue, ErrorMessage = "父级部门ID无效")]
    public long ParentId { get; set; }

    /// <summary>部门名称</summary>
    [Required(ErrorMessage = "部门名称不能为空")]
    [StringLength(50)]
    public string DeptName { get; set; } = string.Empty;

    /// <summary>部门编码（唯一）</summary>
    [Required(ErrorMessage = "部门编码不能为空")]
    [StringLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9_\-]+$", ErrorMessage = "部门编码只能包含字母、数字、下划线、中划线")]
    public string DeptCode { get; set; } = string.Empty;

    /// <summary>负责人</summary>
    [StringLength(50)]
    public string? Leader { get; set; }

    /// <summary>负责人用户ID（审批流"部门主管"解析用；0=未设置）</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long LeaderUserId { get; set; }

    /// <summary>排序号</summary>
    [Range(0, int.MaxValue)]
    public int Sort { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    [Range(0, 1)]
    public int Status { get; set; } = 1;
}
