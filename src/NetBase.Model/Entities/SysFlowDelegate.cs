using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>
/// 审批委托代理：委托人在时间段内的待办自动转由代理人审批（任务生成时替换，已生成的任务不变）。
/// </summary>
[SugarTable("sys_flow_delegate", TableDescription = "审批委托代理表")]
public class SysFlowDelegate : BaseEntity
{
    /// <summary>委托人ID</summary>
    [SugarColumn(ColumnDescription = "委托人ID")]
    public long DelegatorId { get; set; }

    /// <summary>委托人姓名</summary>
    [SugarColumn(Length = 50, ColumnDescription = "委托人姓名")]
    public string DelegatorName { get; set; } = string.Empty;

    /// <summary>代理人ID</summary>
    [SugarColumn(ColumnDescription = "代理人ID")]
    public long AgentId { get; set; }

    /// <summary>代理人姓名</summary>
    [SugarColumn(Length = 50, ColumnDescription = "代理人姓名")]
    public string AgentName { get; set; } = string.Empty;

    /// <summary>生效开始时间</summary>
    [SugarColumn(ColumnDescription = "生效开始时间")]
    public DateTime StartTime { get; set; }

    /// <summary>生效结束时间（含）</summary>
    [SugarColumn(ColumnDescription = "生效结束时间")]
    public DateTime EndTime { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    [SugarColumn(ColumnDescription = "状态", DefaultValue = "1")]
    public int Status { get; set; } = 1;

    /// <summary>备注（如请假事由）</summary>
    [SugarColumn(IsNullable = true, Length = 200, ColumnDescription = "备注")]
    public string? Remark { get; set; }
}
