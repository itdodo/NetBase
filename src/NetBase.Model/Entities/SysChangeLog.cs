using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>字段级变更审计（Update 时自动记录字段前后值）</summary>
[SugarTable("sys_change_log", TableDescription = "数据变更日志表")]
public class SysChangeLog : BaseEntity
{
    /// <summary>业务表名</summary>
    [SugarColumn(Length = 100, ColumnDescription = "业务表名")]
    public string TableName { get; set; } = string.Empty;

    /// <summary>数据主键</summary>
    [SugarColumn(Length = 64, ColumnDescription = "数据主键")]
    public string RecordId { get; set; } = string.Empty;

    /// <summary>变更明细 JSON：{ "字段": { "old": 旧值, "new": 新值 } }（密码类字段已脱敏）</summary>
    [SugarColumn(ColumnDataType = "nvarchar(max)", ColumnDescription = "变更明细")]
    public string Changes { get; set; } = string.Empty;

    /// <summary>操作人ID</summary>
    [SugarColumn(ColumnDescription = "操作人ID")]
    public long UserId { get; set; }

    /// <summary>操作人用户名</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "操作人用户名")]
    public string? UserName { get; set; }
}
