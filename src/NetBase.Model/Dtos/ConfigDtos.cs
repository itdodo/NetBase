using System.ComponentModel.DataAnnotations;
using NetBase.Common.Results;

namespace NetBase.Model.Dtos;

/// <summary>参数查询</summary>
public class ConfigQueryDto : PageQuery
{
    /// <summary>键/名称关键字</summary>
    [StringLength(50)]
    public string? Keyword { get; set; }
}

/// <summary>参数保存</summary>
public class ConfigSaveDto
{
    /// <summary>参数键（唯一）</summary>
    [Required(ErrorMessage = "参数键不能为空")]
    [StringLength(100)]
    [RegularExpression(@"^[a-zA-Z0-9.]+$", ErrorMessage = "参数键只能包含字母、数字、点")]
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>参数值</summary>
    [Required(ErrorMessage = "参数值不能为空")]
    [StringLength(500)]
    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>参数名称</summary>
    [Required(ErrorMessage = "参数名称不能为空")]
    [StringLength(100)]
    public string ConfigName { get; set; } = string.Empty;

    /// <summary>备注</summary>
    [StringLength(200)]
    public string? Remark { get; set; }
}
