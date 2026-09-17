using NetBase.Model.Enums;
using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>审批任务（实例节点 x 审批人，一条待办）</summary>
[SugarTable("sys_flow_task", TableDescription = "审批任务表")]
public class SysFlowTask : BaseEntity
{
    /// <summary>流程实例ID</summary>
    [SugarColumn(ColumnDescription = "流程实例ID")]
    public long InstanceId { get; set; }

    /// <summary>节点编码</summary>
    [SugarColumn(Length = 50, ColumnDescription = "节点编码")]
    public string NodeCode { get; set; } = string.Empty;

    /// <summary>节点名称（冗余展示用）</summary>
    [SugarColumn(Length = 100, ColumnDescription = "节点名称")]
    public string NodeName { get; set; } = string.Empty;

    /// <summary>节点审批模式（冗余，语义判定用）</summary>
    [SugarColumn(ColumnDescription = "审批模式")]
    public FlowNodeMode NodeMode { get; set; }

    /// <summary>审批人ID</summary>
    [SugarColumn(ColumnDescription = "审批人ID")]
    public long ApproverUserId { get; set; }

    /// <summary>审批人姓名</summary>
    [SugarColumn(Length = 50, ColumnDescription = "审批人姓名")]
    public string ApproverName { get; set; } = string.Empty;

    /// <summary>任务状态</summary>
    [SugarColumn(ColumnDescription = "任务状态", DefaultValue = "1")]
    public FlowTaskStatus Status { get; set; } = FlowTaskStatus.Pending;

    /// <summary>依次审批顺序号（同节点内从小到大逐个生成待办）</summary>
    [SugarColumn(ColumnDescription = "顺序号", DefaultValue = "0")]
    public int Sequence { get; set; }

    /// <summary>审批意见</summary>
    [SugarColumn(IsNullable = true, Length = 500, ColumnDescription = "审批意见")]
    public string? Comment { get; set; }

    /// <summary>处理时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "处理时间")]
    public DateTime? ActTime { get; set; }
}
