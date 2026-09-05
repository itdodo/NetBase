using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>通知公告</summary>
[SugarTable("sys_notice", TableDescription = "通知公告表")]
public class SysNotice : BaseEntity
{
    /// <summary>标题</summary>
    [SugarColumn(Length = 100, ColumnDescription = "标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>类型：1-通知 2-公告</summary>
    [SugarColumn(ColumnDescription = "类型：1-通知 2-公告")]
    public int NoticeType { get; set; } = 1;

    /// <summary>内容（富文本/纯文本）</summary>
    [SugarColumn(ColumnDataType = "nvarchar(max)", ColumnDescription = "内容")]
    public string Content { get; set; } = string.Empty;

    /// <summary>状态：0-停用 1-启用（停用不下发）</summary>
    [SugarColumn(ColumnDescription = "状态：0-停用 1-启用")]
    public int Status { get; set; } = 1;
}
