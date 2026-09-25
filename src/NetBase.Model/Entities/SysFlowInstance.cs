using NetBase.Model.Enums;
using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>流程实例（一次单据审批的运行态，业务单据经 BusinessTable+BusinessId 关联）</summary>
[SugarTable("sys_flow_instance", TableDescription = "审批流程实例表")]
public class SysFlowInstance : BaseEntity
{
    /// <summary>流程编码</summary>
    [SugarColumn(Length = 50, ColumnDescription = "流程编码")]
    public string FlowCode { get; set; } = string.Empty;

    /// <summary>流程定义ID（实例锁定启动时版本）</summary>
    [SugarColumn(ColumnDescription = "流程定义ID")]
    public long DefinitionId { get; set; }

    /// <summary>业务表名（如 biz_expense）</summary>
    [SugarColumn(Length = 100, ColumnDescription = "业务表名")]
    public string BusinessTable { get; set; } = string.Empty;

    /// <summary>业务单据ID（雪花ID字符串）</summary>
    [SugarColumn(Length = 64, ColumnDescription = "业务单据ID")]
    public string BusinessId { get; set; } = string.Empty;

    /// <summary>待办标题（业务 Handler 提供，如"张三的报销单 ¥1,200"）</summary>
    [SugarColumn(Length = 200, ColumnDescription = "待办标题")]
    public string Summary { get; set; } = string.Empty;

    /// <summary>当前节点编码（到达 end 或终态后为空）</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "当前节点")]
    public string? CurrentNodeCode { get; set; }

    /// <summary>实例状态</summary>
    [SugarColumn(ColumnDescription = "实例状态", DefaultValue = "1")]
    public FlowInstanceStatus Status { get; set; } = FlowInstanceStatus.Running;

    /// <summary>流程变量 JSON（条件分支求值依据，提交时由业务传入）</summary>
    [SugarColumn(ColumnDataType = "text", ColumnDescription = "流程变量")]
    public string VariablesJson { get; set; } = "{}";

    /// <summary>后加签追加节点 JSON（节点通过后先走追加节点再前进，不回写流程定义）</summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "text", ColumnDescription = "追加节点")]
    public string? AppendNodesJson { get; set; }

    /// <summary>发起人ID</summary>
    [SugarColumn(ColumnDescription = "发起人ID")]
    public long SubmitterId { get; set; }

    /// <summary>发起人姓名</summary>
    [SugarColumn(Length = 50, ColumnDescription = "发起人姓名")]
    public string SubmitterName { get; set; } = string.Empty;

    /// <summary>发起人部门ID（部门主管审批人解析用）</summary>
    [SugarColumn(ColumnDescription = "发起人部门ID")]
    public long SubmitterDeptId { get; set; }

    /// <summary>提交时间</summary>
    [SugarColumn(ColumnDescription = "提交时间")]
    public DateTime SubmitTime { get; set; }

    /// <summary>结束时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "结束时间")]
    public DateTime? EndTime { get; set; }
}
