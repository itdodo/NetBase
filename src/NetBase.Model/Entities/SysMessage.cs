using SqlSugar;
using System.Text.Json.Serialization;

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

    /// <summary>消息类型：1-系统 2-站内信 3-业务（审批/工单等，配合 BizType/BizId 跳转）</summary>
    [SugarColumn(ColumnDescription = "消息类型")]
    public int MsgType { get; set; } = 1;

    /// <summary>业务类型（如 approval/order，业务事件通知用）</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "业务类型")]
    public string? BizType { get; set; }

    /// <summary>业务单据ID（点击通知跳转定位用）</summary>
    [SugarColumn(IsNullable = true, Length = 64, ColumnDescription = "业务单据ID")]
    public string? BizId { get; set; }

    /// <summary>接收人ID</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [SugarColumn(ColumnDescription = "接收人ID")]
    public long ReceiverId { get; set; }

    /// <summary>是否已读</summary>
    [SugarColumn(ColumnDescription = "是否已读")]
    public bool IsRead { get; set; }

    /// <summary>阅读时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "阅读时间")]
    public DateTime? ReadTime { get; set; }
}
