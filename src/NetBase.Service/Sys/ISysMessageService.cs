using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;

namespace NetBase.Service.Sys;

/// <summary>消息发送请求</summary>
public class MessageSendDto
{
    /// <summary>接收人ID</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [Range(1, long.MaxValue, ErrorMessage = "接收人无效")]
    public long ReceiverId { get; set; }

    /// <summary>标题</summary>
    [Required(ErrorMessage = "标题不能为空")]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    /// <summary>内容</summary>
    [Required(ErrorMessage = "内容不能为空")]
    [StringLength(1000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>消息类型：1-系统 2-站内信 3-业务</summary>
    [Range(1, 3)]
    public int MsgType { get; set; } = 1;

    /// <summary>业务类型（approval/order 等，可选）</summary>
    [StringLength(50)]
    public string? BizType { get; set; }

    /// <summary>业务单据ID（可选）</summary>
    [StringLength(64)]
    public string? BizId { get; set; }
}

/// <summary>消息查询（收件箱）</summary>
public class MessageQueryDto : PageQuery
{
    /// <summary>标题关键字</summary>
    [StringLength(50)]
    public string? Keyword { get; set; }

    /// <summary>是否已读</summary>
    [Range(0, 1)]
    public int? IsRead { get; set; }

    /// <summary>发送人关键字</summary>
    [StringLength(50)]
    public string? SenderName { get; set; }
}

/// <summary>站内信服务</summary>
public interface ISysMessageService
{
    /// <summary>发送消息（管理员/系统）</summary>
    Task<long> SendAsync(MessageSendDto dto, string senderName);

    /// <summary>收件箱分页</summary>
    Task<PageResult<SysMessage>> GetMyPageAsync(long userId, MessageQueryDto query);

    /// <summary>未读数</summary>
    Task<int> GetUnreadCountAsync(long userId);

    /// <summary>标记已读（校验接收人）</summary>
    Task MarkReadAsync(long userId, long messageId);

    /// <summary>全部标记已读</summary>
    Task MarkAllReadAsync(long userId);
}
