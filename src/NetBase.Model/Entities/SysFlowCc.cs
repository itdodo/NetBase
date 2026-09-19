using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>流程抄送记录（到达抄送节点时按人落一条，"抄送我的"数据源）</summary>
[SugarTable("sys_flow_cc", TableDescription = "流程抄送记录表")]
public class SysFlowCc : BaseEntity
{
    /// <summary>流程实例ID</summary>
    [SugarColumn(ColumnDescription = "流程实例ID")]
    public long InstanceId { get; set; }

    /// <summary>节点编码</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "节点编码")]
    public string? NodeCode { get; set; }

    /// <summary>被抄送人ID</summary>
    [SugarColumn(ColumnDescription = "被抄送人ID")]
    public long UserId { get; set; }
}
