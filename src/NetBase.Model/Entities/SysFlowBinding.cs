using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>
/// 单据-审批流绑定表：业务表名 → 流程编码。
/// 提交审批时绑定记录优先于业务代码默认值；FlowCode 为空表示该单据不走审批流；
/// 无绑定记录则回退业务代码传入的默认编码（存量单据零回归）。
/// </summary>
[SugarTable("sys_flow_binding", TableDescription = "审批流单据绑定表")]
public class SysFlowBinding : BaseEntity
{
    /// <summary>业务表名（唯一，如 biz_purchase_request）</summary>
    [SugarColumn(Length = 100, ColumnDescription = "业务表名")]
    public string BusinessTable { get; set; } = string.Empty;

    /// <summary>流程编码（空 = 该单据不走审批流）</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "流程编码")]
    public string? FlowCode { get; set; }

    /// <summary>说明（如单据展示名，便于管理页识别）</summary>
    [SugarColumn(IsNullable = true, Length = 100, ColumnDescription = "说明")]
    public string? Remark { get; set; }
}
