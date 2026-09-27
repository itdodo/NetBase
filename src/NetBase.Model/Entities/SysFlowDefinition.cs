using NetBase.Model.Enums;
using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>流程定义（设计器产出的节点树 JSON + 多版本管理）</summary>
[SugarTable("sys_flow_definition", TableDescription = "审批流程定义表")]
public class SysFlowDefinition : BaseEntity
{
    /// <summary>
    /// 流程编号（唯一键，业务绑定引用）。新流程由系统自动生成数字串（从 100 起自增），
    /// 无需人工填写；兼容历史手工字符串编码。
    /// </summary>
    [SugarColumn(Length = 50, ColumnDescription = "流程编号")]
    public string FlowCode { get; set; } = string.Empty;

    /// <summary>分类（如 财务类/采购类，便于流程管理）</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "分类")]
    public string? Category { get; set; }

    /// <summary>流程名称</summary>
    [SugarColumn(Length = 100, ColumnDescription = "流程名称")]
    public string FlowName { get; set; } = string.Empty;

    /// <summary>业务版本号（同编码多版本并存；与基类乐观锁 Version 不同列）</summary>
    [SugarColumn(ColumnDescription = "业务版本号", DefaultValue = "1")]
    public int FlowVersion { get; set; } = 1;

    /// <summary>节点树 JSON（设计器产物，见 FlowGraph 模型）</summary>
    [SugarColumn(ColumnDataType = "text", ColumnDescription = "节点树JSON")]
    public string NodeJson { get; set; } = string.Empty;

    /// <summary>状态：0-草稿/停用 1-启用（同编码仅一个启用版本）</summary>
    [SugarColumn(ColumnDescription = "状态", DefaultValue = "0")]
    public int Status { get; set; }

    /// <summary>备注</summary>
    [SugarColumn(IsNullable = true, Length = 200, ColumnDescription = "备注")]
    public string? Remark { get; set; }
}
