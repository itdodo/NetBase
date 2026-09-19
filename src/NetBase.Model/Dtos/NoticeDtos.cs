using System.ComponentModel.DataAnnotations;
using NetBase.Common.Results;

namespace NetBase.Model.Dtos;

/// <summary>公告查询</summary>
public class NoticeQueryDto : PageQuery
{
    /// <summary>标题关键字</summary>
    [StringLength(50)]
    public string? Keyword { get; set; }

    /// <summary>类型：1-通知 2-公告</summary>
    [Range(1, 2)]
    public int? NoticeType { get; set; }
}

/// <summary>公告保存</summary>
public class NoticeSaveDto
{
    /// <summary>标题</summary>
    [Required(ErrorMessage = "标题不能为空")]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    /// <summary>类型：1-通知 2-公告</summary>
    [Range(1, 2, ErrorMessage = "类型无效（1-通知 2-公告）")]
    public int NoticeType { get; set; } = 1;

    /// <summary>内容</summary>
    [Required(ErrorMessage = "内容不能为空")]
    public string Content { get; set; } = string.Empty;

    /// <summary>状态：0-停用 1-发布 2-定时发布</summary>
    [Range(0, 2, ErrorMessage = "状态取值无效")]
    public int Status { get; set; } = 1;

    /// <summary>定时发布时间（状态=2 时必填）</summary>
    public DateTime? PublishTime { get; set; }
}
