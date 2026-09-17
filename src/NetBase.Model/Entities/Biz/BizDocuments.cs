using SqlSugar;

namespace NetBase.Model.Entities.Biz;

/// <summary>单据通用状态（业务样板：0草稿 1审批中 2已通过 3已拒绝 4已撤回）</summary>
public static class BizDocStatus
{
    public const int Draft = 0;
    public const int InApproval = 1;
    public const int Approved = 2;
    public const int Rejected = 3;
    public const int Revoked = 4;

    public static string Label(int status) => status switch
    {
        Draft => "草稿",
        InApproval => "审批中",
        Approved => "已通过",
        Rejected => "已拒绝",
        Revoked => "已撤回",
        _ => "未知"
    };
}

/// <summary>报销单（审批流业务样板一：FlowCode=expense）</summary>
[SugarTable("biz_expense", TableDescription = "报销单")]
public class BizExpense : BaseEntity
{
    /// <summary>报销标题</summary>
    [SugarColumn(Length = 100, ColumnDescription = "报销标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>报销金额</summary>
    [SugarColumn(ColumnDataType = "decimal(18,2)", ColumnDescription = "报销金额")]
    public decimal Amount { get; set; }

    /// <summary>事由说明</summary>
    [SugarColumn(IsNullable = true, Length = 500, ColumnDescription = "事由说明")]
    public string? Reason { get; set; }

    /// <summary>单据状态</summary>
    [SugarColumn(ColumnDescription = "状态", DefaultValue = "0")]
    public int Status { get; set; } = BizDocStatus.Draft;
}

/// <summary>采购申请单（审批流业务样板二：FlowCode=purchase_request，金额条件分支演示）</summary>
[SugarTable("biz_purchase_request", TableDescription = "采购申请单")]
public class BizPurchaseRequest : BaseEntity
{
    /// <summary>申请标题</summary>
    [SugarColumn(Length = 100, ColumnDescription = "申请标题")]
    public string Title { get; set; } = string.Empty;

    /// <summary>采购物品</summary>
    [SugarColumn(Length = 100, ColumnDescription = "采购物品")]
    public string ItemName { get; set; } = string.Empty;

    /// <summary>预算金额</summary>
    [SugarColumn(ColumnDataType = "decimal(18,2)", ColumnDescription = "预算金额")]
    public decimal Amount { get; set; }

    /// <summary>申请事由</summary>
    [SugarColumn(IsNullable = true, Length = 500, ColumnDescription = "申请事由")]
    public string? Reason { get; set; }

    /// <summary>单据状态</summary>
    [SugarColumn(ColumnDescription = "状态", DefaultValue = "0")]
    public int Status { get; set; } = BizDocStatus.Draft;
}
