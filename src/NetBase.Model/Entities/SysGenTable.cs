using System.Text.Json.Serialization;
using NetBase.Common.Json;
using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>代码生成配置（一张业务表的生成方案；字段明细见 SysGenTableColumn）</summary>
[SugarTable("sys_gen_table", TableDescription = "代码生成配置")]
public class SysGenTable : BaseEntity
{
    /// <summary>表名（biz_xxx）</summary>
    [SugarColumn(Length = 100, ColumnDescription = "表名")]
    public string TableName { get; set; } = string.Empty;

    /// <summary>表备注/功能描述</summary>
    [SugarColumn(Length = 200, ColumnDescription = "表描述")]
    public string TableComment { get; set; } = string.Empty;

    /// <summary>模块名（决定 biz_{module} 前缀/路由/权限码前缀，如 expense）</summary>
    [SugarColumn(Length = 50, ColumnDescription = "模块名")]
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>功能名（如 报销管理）</summary>
    [SugarColumn(Length = 100, ColumnDescription = "功能名")]
    public string FunctionName { get; set; } = string.Empty;

    /// <summary>实体基类名前缀（如 Expense → BizExpense/ExpenseService）</summary>
    [SugarColumn(Length = 100, ColumnDescription = "实体名")]
    public string EntityName { get; set; } = string.Empty;

    /// <summary>作者</summary>
    [SugarColumn(Length = 50, ColumnDescription = "作者")]
    public string Author { get; set; } = "system";

    /// <summary>是否数据权限表（生成 DeptId/OwnerUserId 列并实现 IDataScope）</summary>
    [SugarColumn(ColumnDescription = "数据权限")]
    public bool DataScope { get; set; }

    /// <summary>是否审批流单据（生成 Status 列/BizDocStatus/IFlowBusinessHandler/前端审批时间线）</summary>
    [SugarColumn(ColumnDescription = "审批流单据")]
    public bool FlowDoc { get; set; }

    /// <summary>子表配置 JSON（数组：[{tableName, tableComment, fkColumn, entityName}]；主子表模式）</summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "text", ColumnDescription = "子表配置JSON")]
    public string? SubTablesJson { get; set; }

    /// <summary>配置来源：db-从库导入 manual-手工新建</summary>
    [SugarColumn(Length = 10, ColumnDescription = "来源")]
    public string SourceType { get; set; } = "manual";

    /// <summary>已解析的子表配置（不入库）</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [SugarColumn(IsIgnore = true)]
    public List<SysGenSubTable>? SubTables { get; set; }
}

/// <summary>主子表的子表配置</summary>
public class SysGenSubTable
{
    /// <summary>子表名</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>子表功能描述</summary>
    public string TableComment { get; set; } = string.Empty;

    /// <summary>关联外键列（子表内指向主表的列，物理列名小写）</summary>
    public string FkColumn { get; set; } = string.Empty;

    /// <summary>子表实体名前缀</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>子表列配置（编辑向导时由前端并入；生成时从列配置表取）</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SysGenTableColumn>? Columns { get; set; }
}

/// <summary>代码生成字段配置（表的一列）</summary>
[SugarTable("sys_gen_table_column", TableDescription = "代码生成字段配置")]
public class SysGenTableColumn : BaseEntity
{
    /// <summary>所属生成配置</summary>
    [SugarColumn(ColumnDescription = "配置ID")]
    [JsonConverter(typeof(LongToStringConverter))]
    public long GenTableId { get; set; }

    /// <summary>物理列名（小写，如 amount）</summary>
    [SugarColumn(Length = 100, ColumnDescription = "列名")]
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>C# 属性名（Pascal，如 Amount）</summary>
    [SugarColumn(Length = 100, ColumnDescription = "属性名")]
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>数据库类型（如 varchar/numeric/timestamp）</summary>
    [SugarColumn(Length = 50, ColumnDescription = "数据库类型")]
    public string ColumnType { get; set; } = string.Empty;

    /// <summary>C# 类型（如 string/decimal/DateTime）</summary>
    [SugarColumn(Length = 50, ColumnDescription = "C#类型")]
    public string CSharpType { get; set; } = string.Empty;

    /// <summary>显示名（表头/表单标签）</summary>
    [SugarColumn(Length = 100, ColumnDescription = "显示名")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>列注释（建表 SQL 用）</summary>
    [SugarColumn(Length = 200, ColumnDescription = "列注释")]
    public string ColumnComment { get; set; } = string.Empty;

    /// <summary>长度（字符类型）</summary>
    [SugarColumn(ColumnDescription = "长度")]
    public int Length { get; set; }

    /// <summary>是否主键</summary>
    [SugarColumn(ColumnDescription = "主键")]
    public bool IsPk { get; set; }

    /// <summary>是否必填</summary>
    [SugarColumn(ColumnDescription = "必填")]
    public bool IsRequired { get; set; }

    /// <summary>是否列表列</summary>
    [SugarColumn(ColumnDescription = "列表显示")]
    public bool IsList { get; set; }

    /// <summary>是否查询条件</summary>
    [SugarColumn(ColumnDescription = "查询条件")]
    public bool IsQuery { get; set; }

    /// <summary>是否表单字段</summary>
    [SugarColumn(ColumnDescription = "表单字段")]
    public bool IsForm { get; set; }

    /// <summary>查询方式：eq-like-ge-le-between（仅 IsQuery 时有意义）</summary>
    [SugarColumn(Length = 10, IsNullable = true, ColumnDescription = "查询方式")]
    public string? QueryType { get; set; }

    /// <summary>前端控件：input/number/date/datetime/select/textarea/switch</summary>
    [SugarColumn(Length = 20, IsNullable = true, ColumnDescription = "控件类型")]
    public string? UiType { get; set; }

    /// <summary>排序（表单/列表顺序）</summary>
    [SugarColumn(ColumnDescription = "排序")]
    public int Sort { get; set; }

    /// <summary>子表名（null=主表列；主子表模式下归组用）</summary>
    [SugarColumn(Length = 100, IsNullable = true, ColumnDescription = "子表名")]
    public string? SubTableName { get; set; }
}
