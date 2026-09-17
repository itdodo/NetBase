using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>
/// 流程流转记录（只追加流水，审批详情时间线数据源）。
/// Action 取值：submit/approve/reject/transfer/addsign/withdraw/cc/auto/void
/// </summary>
[SugarTable("sys_flow_record", TableDescription = "流程流转记录表")]
public class SysFlowRecord : BaseEntity
{
    /// <summary>流程实例ID</summary>
    [SugarColumn(ColumnDescription = "流程实例ID")]
    public long InstanceId { get; set; }

    /// <summary>节点编码（可选，节点级动作为空）</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "节点编码")]
    public string? NodeCode { get; set; }

    /// <summary>动作标识</summary>
    [SugarColumn(Length = 20, ColumnDescription = "动作")]
    public string Action { get; set; } = string.Empty;

    /// <summary>操作人ID</summary>
    [SugarColumn(ColumnDescription = "操作人ID")]
    public long OperatorId { get; set; }

    /// <summary>操作人姓名</summary>
    [SugarColumn(Length = 50, ColumnDescription = "操作人姓名")]
    public string OperatorName { get; set; } = string.Empty;

    /// <summary>意见/说明</summary>
    [SugarColumn(IsNullable = true, Length = 500, ColumnDescription = "意见")]
    public string? Comment { get; set; }

    /// <summary>扩展信息 JSON（如转办目标人、加签人列表）</summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "nvarchar(max)", ColumnDescription = "扩展JSON")]
    public string? ExtraJson { get; set; }

    /// <summary>操作时间</summary>
    [SugarColumn(ColumnDescription = "操作时间")]
    public DateTime ActTime { get; set; }
}
