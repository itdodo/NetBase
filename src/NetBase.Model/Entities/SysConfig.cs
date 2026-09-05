using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>系统参数（键值对，运行时可调，带缓存）</summary>
[SugarTable("sys_config", TableDescription = "系统参数配置表")]
public class SysConfig : BaseEntity
{
    /// <summary>参数键（唯一），约定 sys.模块.名称</summary>
    [SugarColumn(Length = 100, ColumnDescription = "参数键")]
    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>参数值</summary>
    [SugarColumn(Length = 500, ColumnDescription = "参数值")]
    public string ConfigValue { get; set; } = string.Empty;

    /// <summary>参数名称（展示用）</summary>
    [SugarColumn(Length = 100, ColumnDescription = "参数名称")]
    public string ConfigName { get; set; } = string.Empty;

    /// <summary>是否内置（内置参数不允许删除，仅可改值）</summary>
    [SugarColumn(ColumnDescription = "是否内置")]
    public bool IsBuiltIn { get; set; }

    /// <summary>备注</summary>
    [SugarColumn(IsNullable = true, Length = 200, ColumnDescription = "备注")]
    public string? Remark { get; set; }
}
