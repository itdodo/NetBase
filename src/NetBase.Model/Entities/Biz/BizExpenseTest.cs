using System.Text.Json.Serialization;
using NetBase.Common.Json;
using SqlSugar;

namespace NetBase.Model.Entities.Biz;

/// <summary>GenTestExpense</summary>
[SugarTable("biz_expense", TableDescription = "GenTestExpense")]
public class BizExpenseTest : BaseEntity, IDataScope
{




    /// <summary>报销标题</summary>
    [SugarColumn(Length = 100, ColumnDescription = "报销标题")]
    public string Title { get; set; } = string.Empty;




    /// <summary>报销金额</summary>
    [SugarColumn(ColumnDescription = "报销金额")]
    public decimal Amount { get; set; }




    /// <summary>事由说明</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "事由说明")]
    public string Reason { get; set; } = string.Empty;




















    /// <summary>数据权限：归属部门</summary>
    [SugarColumn(ColumnDescription = "归属部门")]
    public long DeptId { get; set; }

    /// <summary>数据权限：归属用户</summary>
    [SugarColumn(ColumnDescription = "归属用户")]
    public long OwnerUserId { get; set; }



    /// <summary>单据状态：0草稿 1审批中 2已通过 3已拒绝 4已撤回</summary>
    [SugarColumn(ColumnDescription = "单据状态", DefaultValue = "0")]
    public int Status { get; set; }


}

