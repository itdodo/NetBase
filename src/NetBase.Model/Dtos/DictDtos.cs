using System.ComponentModel.DataAnnotations;
using NetBase.Common.Results;

using System.Text.Json.Serialization;
namespace NetBase.Model.Dtos;

/// <summary>字典类型查询</summary>
public class DictTypeQueryDto : PageQuery
{
    /// <summary>编码/名称关键字</summary>
    [StringLength(50)]
    public string? Keyword { get; set; }
}

/// <summary>字典类型保存</summary>
public class DictTypeSaveDto
{
    /// <summary>字典编码（唯一）</summary>
    [Required(ErrorMessage = "字典编码不能为空")]
    [StringLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "字典编码只能包含字母、数字、下划线")]
    public string DictCode { get; set; } = string.Empty;

    /// <summary>字典名称</summary>
    [Required(ErrorMessage = "字典名称不能为空")]
    [StringLength(50)]
    public string DictName { get; set; } = string.Empty;

    /// <summary>状态：0-停用 1-启用</summary>
    [Range(0, 1)]
    public int Status { get; set; } = 1;

    /// <summary>备注</summary>
    [StringLength(200)]
    public string? Remark { get; set; }
}

/// <summary>字典数据项保存</summary>
public class DictDataSaveDto
{
    /// <summary>所属字典类型ID</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [Range(1, long.MaxValue, ErrorMessage = "字典类型ID无效")]
    public long DictTypeId { get; set; }

    /// <summary>显示标签</summary>
    [Required(ErrorMessage = "标签不能为空")]
    [StringLength(50)]
    public string Label { get; set; } = string.Empty;

    /// <summary>存储值</summary>
    [Required(ErrorMessage = "存储值不能为空")]
    [StringLength(50)]
    public string Value { get; set; } = string.Empty;

    /// <summary>排序号</summary>
    [Range(0, int.MaxValue)]
    public int Sort { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    [Range(0, 1)]
    public int Status { get; set; } = 1;

    /// <summary>备注</summary>
    [StringLength(200)]
    public string? Remark { get; set; }
}

/// <summary>字典数据项返回（下拉框/业务取值）</summary>
public class DictDataDto
{
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long Id { get; set; }

    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public int Sort { get; set; }

    public int Status { get; set; }

    /// <summary>扩展：所属类型编码</summary>
    public string? DictCode { get; set; }
}
