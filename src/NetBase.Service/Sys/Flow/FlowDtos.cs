using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using NetBase.Common.Results;
using NetBase.Model.Dtos;

namespace NetBase.Service.Sys.Flow;

// ---------------- 流程定义 ----------------

/// <summary>流程定义返回</summary>
public class FlowDefinitionDto
{
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long Id { get; set; }

    public string FlowCode { get; set; } = string.Empty;

    /// <summary>分类</summary>
    public string? Category { get; set; }

    public string FlowName { get; set; } = string.Empty;

    public int Version { get; set; }

    public string NodeJson { get; set; } = string.Empty;

    /// <summary>状态：0-停用 1-启用</summary>
    public int Status { get; set; }

    public string? Remark { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>流程定义保存请求</summary>
public class FlowDefinitionSaveDto
{
    /// <summary>流程编号（留空则系统自动生成数字编号，从 100 起自增；兼容显式指定）</summary>
    [RegularExpression(@"^[a-zA-Z0-9_]{1,50}$", ErrorMessage = "流程编号只能包含字母、数字、下划线")]
    public string? FlowCode { get; set; }

    /// <summary>分类（如 财务类/采购类）</summary>
    [StringLength(50)]
    public string? Category { get; set; }

    /// <summary>流程名称</summary>
    [Required(ErrorMessage = "流程名称不能为空")]
    [StringLength(100)]
    public string FlowName { get; set; } = string.Empty;

    /// <summary>节点树 JSON（设计器产物）</summary>
    [Required(ErrorMessage = "节点配置不能为空")]
    public string NodeJson { get; set; } = string.Empty;

    /// <summary>是否启用（启用会使同编码其他版本停用）</summary>
    public bool Enabled { get; set; }

    /// <summary>备注</summary>
    [StringLength(200)]
    public string? Remark { get; set; }
}

/// <summary>流程定义查询</summary>
public class FlowDefinitionQueryDto : PageQuery
{
    /// <summary>编码/名称关键字</summary>
    [StringLength(50)]
    public string? Keyword { get; set; }

    /// <summary>分类过滤</summary>
    [StringLength(50)]
    public string? Category { get; set; }
}

// ---------------- 待办 / 已办 ----------------

/// <summary>审批任务视图（联实例冗余展示字段）</summary>
public class FlowTaskViewDto
{
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long TaskId { get; set; }

    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long InstanceId { get; set; }

    public string NodeName { get; set; } = string.Empty;

    /// <summary>待办标题（实例 Summary）</summary>
    public string Summary { get; set; } = string.Empty;

    public string FlowCode { get; set; } = string.Empty;

    /// <summary>业务表名（前端据此跳转单据详情）</summary>
    public string BusinessTable { get; set; } = string.Empty;

    /// <summary>业务单据ID（字符串化）</summary>
    public string BusinessId { get; set; } = string.Empty;

    public string SubmitterName { get; set; } = string.Empty;

    public DateTime SubmitTime { get; set; }

    /// <summary>处理时间（已办）</summary>
    public DateTime? ActTime { get; set; }

    /// <summary>处理结果（已办）：同意/拒绝/转办</summary>
    public string? ActResult { get; set; }
}

// ---------------- 实例 ----------------

/// <summary>流程实例查询</summary>
public class FlowInstanceQueryDto : PageQuery
{
    /// <summary>流程编码</summary>
    [StringLength(50)]
    public string? FlowCode { get; set; }

    /// <summary>发起人关键字</summary>
    [StringLength(50)]
    public string? Submitter { get; set; }

    /// <summary>状态：1审批中 2通过 3拒绝 4撤回 5作废</summary>
    [Range(1, 5)]
    public int? Status { get; set; }
}

/// <summary>流程实例返回</summary>
public class FlowInstanceDto
{
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long Id { get; set; }

    public string FlowCode { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string BusinessTable { get; set; } = string.Empty;

    public string BusinessId { get; set; } = string.Empty;

    /// <summary>状态：1审批中 2通过 3拒绝 4撤回 5作废</summary>
    public int Status { get; set; }

    public string SubmitterName { get; set; } = string.Empty;

    public DateTime SubmitTime { get; set; }

    public DateTime? EndTime { get; set; }
}

/// <summary>审批详情（时间线数据源：实例 + 流转记录 + 任务态）</summary>
public class FlowInstanceDetailDto
{
    public FlowInstanceDto Instance { get; set; } = new();

    /// <summary>节点树 JSON（前端可渲染设计器只读视图）</summary>
    public string NodeJson { get; set; } = string.Empty;

    /// <summary>当前节点编码（审批中时）</summary>
    public string? CurrentNodeCode { get; set; }

    /// <summary>时间线条目（按时间正序）</summary>
    public List<FlowTimelineItem> Timeline { get; set; } = [];

    /// <summary>当前用户可撤回（是发起人且审批中）</summary>
    public bool CanWithdraw { get; set; }
}

/// <summary>时间线条目（融合流转记录与任务）</summary>
public class FlowTimelineItem
{
    /// <summary>节点编码</summary>
    public string? NodeCode { get; set; }

    /// <summary>节点名称</summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>条目类型：submit/approve/reject/transfer/addsign/withdraw/cc/auto</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>操作人</summary>
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>意见/说明</summary>
    public string? Comment { get; set; }

    /// <summary>时间</summary>
    public DateTime Time { get; set; }
}
