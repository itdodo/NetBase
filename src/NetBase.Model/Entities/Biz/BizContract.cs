using System.Text.Json.Serialization;
using NetBase.Common.Json;
using SqlSugar;

namespace NetBase.Model.Entities.Biz;

/// <summary>ContractMgr</summary>
[SugarTable("biz_contract", TableDescription = "ContractMgr")]
public class BizContract : BaseEntity
{




    /// <summary>合同名称</summary>
    [SugarColumn(Length = 200, ColumnDescription = "合同名称")]
    public string ContractName { get; set; } = string.Empty;




    /// <summary>合同金额</summary>
    [SugarColumn(ColumnDescription = "合同金额")]
    public decimal Amount { get; set; }




    /// <summary>签订日期</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "签订日期")]
    public DateTime? SignDate { get; set; }




    /// <summary>备注</summary>
    [SugarColumn(Length = 500, IsNullable = true, ColumnDescription = "备注")]
    public string Remark { get; set; } = string.Empty;




    /// <summary>归属部门</summary>
    [SugarColumn(Length = 0, IsNullable = true, ColumnDescription = "归属部门")]
    public string DeptId { get; set; } = string.Empty;




    /// <summary>归属用户</summary>
    [SugarColumn(Length = 0, IsNullable = true, ColumnDescription = "归属用户")]
    public string OwnerUserId { get; set; } = string.Empty;



















}

