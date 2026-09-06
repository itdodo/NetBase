using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>站内消息（点对点；公告见 SysNotice）</summary>
[SugarTable("sys_message", TableDescription = "站内消息表")]
public class SysMessage : BaseEntity
{
    /// <summary>标题</summary>
    [SugarColumn(Length = 100, ColumnDescription = "标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>内容</summary>
    [SugarColumn(Length = 1000, ColumnDescription = "内容")]
    public string Content { get; set; } = string.Empty;

    /// <summary>发送者名称（系统/管理员）</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "发送者")]
    public string? SenderName { get; set; }

    /// <summary>接收人ID</summary>
    [SugarColumn(ColumnDescription = "接收人ID")]
    public long ReceiverId { get; set; }

    /// <summary>是否已读</summary>
    [SugarColumn(ColumnDescription = "是否已读")]
    public bool IsRead { get; set; }

    /// <summary>阅读时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "阅读时间")]
    public DateTime? ReadTime { get; set; }
}
