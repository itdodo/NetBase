using System.Text.Json;
using NetBase.Common.Auditing;
using NetBase.Model.Entities;
using NetBase.Repository.DbContexts;
using SqlSugar;

namespace NetBase.Service.Sys;

/// <summary>
/// 数据库表元数据读取：information_schema（PG，物理列名小写）。
/// 供代码生成器"从库导入表"使用；仅暴露 biz_ 前缀与用户选择的表。
/// </summary>
public class GenMetaService(ISqlSugarClient db)
{
    /// <summary>PG 类型 → C# 类型映射</summary>
    private static readonly Dictionary<string, string> PgToCSharp = new(StringComparer.OrdinalIgnoreCase)
    {
        ["int8"] = "long",
        ["int4"] = "int",
        ["int2"] = "short",
        ["varchar"] = "string",
        ["character varying"] = "string",
        ["text"] = "string",
        ["bool"] = "bool",
        ["boolean"] = "bool",
        ["numeric"] = "decimal",
        ["timestamp"] = "DateTime",
        ["timestamp without time zone"] = "DateTime",
        ["date"] = "DateTime",
        ["float4"] = "float",
        ["float8"] = "double",
        ["uuid"] = "string"
    };

    /// <summary>列出库内全部用户表（排除框架表/迁移表），供导入选择</summary>
    public Task<List<GenTableBrief>> ListTablesAsync()
    {
        var rows = db.Ado.SqlQuery<GenTableBrief>("""
SELECT c.relname AS "tableName",
       COALESCE(obj_description(c.oid, 'pg_class'), '') AS "tableComment"
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relkind = 'r'
  AND c.relname NOT LIKE 'sys_%'
  AND c.relname NOT LIKE 'hangfire%'
  AND c.relname NOT LIKE 'pg_%'
  AND c.relname NOT LIKE '__EFMigrations%'
ORDER BY c.relname
""");
        return Task.FromResult(rows);
    }

    /// <summary>读一张表的列元数据（列名/类型/可空/长度/注释/主键）</summary>
    public Task<List<GenColumnMeta>> GetColumnsAsync(string tableName)
    {
        var safe = tableName.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(safe, @"^[a-z][a-z0-9_]*$"))
        {
            throw new NetBase.Common.Exceptions.BusinessException("表名不合法", NetBase.Common.Results.ErrorCodes.COMMON_PARAM_INVALID);
        }

        var cols = db.Ado.SqlQuery<GenColumnRaw>("""
SELECT a.attname AS "columnName",
       format_type(a.atttypid, a.atttypmod) AS "dataType",
       CASE WHEN a.attnotnull THEN 1 ELSE 0 END AS "notNull",
       COALESCE(col_description(c.oid, a.attnum), '') AS "columnComment",
       CASE WHEN EXISTS (SELECT 1 FROM pg_index i WHERE i.indrelid = c.oid AND i.indisprimary AND a.attnum = ANY(i.indkey))
            THEN 1 ELSE 0 END AS "isPk"
FROM pg_attribute a
JOIN pg_class c ON c.oid = a.attrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relname = @table
  AND a.attnum > 0
  AND NOT a.attisdropped
ORDER BY a.attnum
""", new SugarParameter("@table", safe));

        var result = cols.Select(c =>
        {
            var baseType = c.DataType.Split('(')[0].Trim(); // numeric(18,2) → numeric
            var csharp = PgToCSharp.GetValueOrDefault(baseType, "string");
            var length = System.Text.RegularExpressions.Regex.Match(c.DataType, @"\((\d+)").Groups[1].Value;
            return new GenColumnMeta
            {
                ColumnName = c.ColumnName,
                DataType = c.DataType,
                CSharpType = csharp,
                ColumnComment = c.ColumnComment,
                IsPk = c.IsPk == 1,
                IsRequired = c.NotNull == 1,
                Length = int.TryParse(length, out var l) ? l : 0
            };
        }).ToList();
        return Task.FromResult(result);
    }

    /// <summary>建表 SQL 里的列注释写入（COMMENT ON COLUMN，PG 方言）</summary>
    public static string BuildCommentSql(string tableName, string columnName, string comment)
    {
        var t = EscapeIdent(tableName);
        var col = EscapeIdent(columnName);
        return $"COMMENT ON COLUMN {t}.{col} IS '{comment.Replace("'", "''")}';";
    }

    private static string EscapeIdent(string name) =>
        System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-z][a-z0-9_]*$")
            ? name
            : throw new NetBase.Common.Exceptions.BusinessException("标识符不合法", NetBase.Common.Results.ErrorCodes.COMMON_PARAM_INVALID);
}

/// <summary>表简要信息</summary>
public class GenTableBrief
{
    public string TableName { get; set; } = string.Empty;
    public string TableComment { get; set; } = string.Empty;
}

/// <summary>information_schema 原始行</summary>
public class GenColumnRaw
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public int NotNull { get; set; }
    public string ColumnComment { get; set; } = string.Empty;
    public int IsPk { get; set; }
}

/// <summary>列元数据（已映射 C# 类型）</summary>
public class GenColumnMeta
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string CSharpType { get; set; } = string.Empty;
    public string ColumnComment { get; set; } = string.Empty;
    public bool IsPk { get; set; }
    public bool IsRequired { get; set; }
    public int Length { get; set; }
}
