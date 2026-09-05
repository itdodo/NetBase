using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>字典类型（如：common_status 通用状态）</summary>
[SugarTable("sys_dict_type", TableDescription = "字典类型表")]
public class SysDictType : BaseEntity
{
    /// <summary>字典编码（唯一，业务按此取项）</summary>
    [SugarColumn(Length = 50, ColumnDescription = "字典编码")]
    public string DictCode { get; set; } = string.Empty;

    /// <summary>字典名称</summary>
    [SugarColumn(Length = 50, ColumnDescription = "字典名称")]
    public string DictName { get; set; } = string.Empty;

    /// <summary>状态：0-停用 1-启用</summary>
    [SugarColumn(ColumnDescription = "状态：0-停用 1-启用")]
    public int Status { get; set; } = 1;

    /// <summary>备注</summary>
    [SugarColumn(IsNullable = true, Length = 200, ColumnDescription = "备注")]
    public string? Remark { get; set; }
}

/// <summary>字典数据项</summary>
[SugarTable("sys_dict_data", TableDescription = "字典数据表")]
public class SysDictData : BaseEntity
{
    /// <summary>所属字典类型ID</summary>
    [SugarColumn(ColumnDescription = "字典类型ID")]
    public long DictTypeId { get; set; }

    /// <summary>显示标签</summary>
    [SugarColumn(Length = 50, ColumnDescription = "显示标签")]
    public string Label { get; set; } = string.Empty;

    /// <summary>存储值</summary>
    [SugarColumn(Length = 50, ColumnDescription = "存储值")]
    public string Value { get; set; } = string.Empty;

    /// <summary>排序号，越小越靠前</summary>
    [SugarColumn(ColumnDescription = "排序号")]
    public int Sort { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    [SugarColumn(ColumnDescription = "状态：0-停用 1-启用")]
    public int Status { get; set; } = 1;

    /// <summary>备注</summary>
    [SugarColumn(IsNullable = true, Length = 200, ColumnDescription = "备注")]
    public string? Remark { get; set; }
}
