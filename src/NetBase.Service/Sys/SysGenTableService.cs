using System.IO.Compression;
using System.Text;
using System.Text.Json;
using NetBase.Common.Exceptions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using Microsoft.Extensions.Logging;
using NetBase.Repository.DbContexts;
using NetBase.Repository.Repositories;
using SqlSugar;
using Scriban;
using Scriban.Runtime;

namespace NetBase.Service.Sys;

/// <summary>代码生成配置保存请求（表信息 + 字段清单）</summary>
public class GenTableSaveDto
{
    public long? Id { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string TableComment { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string Author { get; set; } = "system";
    public bool DataScope { get; set; }
    public bool FlowDoc { get; set; }
    public string SourceType { get; set; } = "manual";
    /// <summary>子表配置（主子表模式）</summary>
    public List<SysGenSubTable>? SubTables { get; set; }
    /// <summary>字段配置（主表字段；子表字段在 SubTables[].Columns）</summary>
    public List<GenColumnSaveDto> Columns { get; set; } = [];
}

public class GenColumnSaveDto
{
    public long? Id { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public string ColumnType { get; set; } = string.Empty;
    public string CSharpType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ColumnComment { get; set; } = string.Empty;
    public int Length { get; set; }
    public bool IsPk { get; set; }
    public bool IsRequired { get; set; }
    public bool IsList { get; set; }
    public bool IsQuery { get; set; }
    public bool IsForm { get; set; }
    public string? QueryType { get; set; }
    public string? UiType { get; set; }
    public int Sort { get; set; }
    public string? SubTableName { get; set; }
}

/// <summary>预览文件（生成代码的一个文件）</summary>
public record GenPreviewFile(string Path, string Content);

/// <summary>代码生成服务：配置 CRUD、表导入、预览、zip 下载。模板见 Templates/Gen/*.sbn</summary>
public class SysGenTableService(
    IRepository<SysGenTable> tableRepository,
    IRepository<SysGenTableColumn> columnRepository,
    GenMetaService metaService,
    SqlSugarContext context,
    ILogger<SysGenTableService> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private ISqlSugarClient Db => context.Client;

    // ---------- 配置 CRUD ----------

    public Task<PageResult<SysGenTable>> GetPageAsync(string? keyword, PageQuery query)
    {
        var expr = Expressionable.Create<SysGenTable>()
            .AndIF(!string.IsNullOrEmpty(keyword), t => t.TableName.Contains(keyword!) || t.FunctionName.Contains(keyword!));
        return tableRepository.GetPageListAsync(expr.ToExpression(), query);
    }

    public async Task<SysGenTable> GetDetailAsync(long id)
    {
        var table = await tableRepository.GetByIdAsync(id)
            ?? throw new BusinessException("生成配置不存在", ApiResultCode.NotFound, ErrorCodes.COMMON_NOT_FOUND);
        table.SubTables = ParseSubTables(table);
        return table;
    }

    /// <summary>读配置及其全部列（主表列 + 子表列按 SubTableName 归组），供编辑向导回显</summary>
    public async Task<GenDetail> GetDetailWithColumnsAsync(long id)
    {
        var table = await GetDetailAsync(id);
        var columns = (await columnRepository.GetListAsync(x => x.GenTableId == id))
            .OrderBy(c => c.Sort).ToList();
        var detail = new GenDetail { Table = table, Columns = columns.Where(c => c.SubTableName == null).ToList() };
        if (table.SubTables != null)
        {
            foreach (var sub in table.SubTables)
            {
                sub.Columns = columns.Where(c => c.SubTableName == sub.TableName).ToList();
            }
        }

        return detail;
    }

    public class GenDetail
    {
        public SysGenTable Table { get; set; } = new();
        public List<SysGenTableColumn> Columns { get; set; } = [];
    }

    // ---------- 表导入（从库） ----------

    /// <summary>从数据库导入一张表：读列元数据 → 建配置行（子表字段同理）</summary>
    public async Task<long> ImportTableAsync(string tableName, string module, string functionName, string entityName)
    {
        if (await tableRepository.AnyAsync(x => x.TableName == tableName && x.IsDeleted == false))
        {
            throw new BusinessException($"表 {tableName} 已导入过", ApiResultCode.BadRequest, ErrorCodes.COMMON_PARAM_INVALID);
        }

        var cols = await metaService.GetColumnsAsync(tableName);
        var commentRow = (await metaService.ListTablesAsync()).FirstOrDefault(t => t.TableName == tableName);

        var table = new SysGenTable
        {
            TableName = tableName,
            TableComment = commentRow?.TableComment ?? tableName,
            ModuleName = module,
            FunctionName = functionName,
            EntityName = entityName,
            SourceType = "db"
        };
        await tableRepository.InsertAsync(table);

        var sort = 0;
        foreach (var col in cols)
        {
            await columnRepository.InsertAsync(new SysGenTableColumn
            {
                GenTableId = table.Id,
                ColumnName = col.ColumnName,
                PropertyName = ToPascal(col.ColumnName),
                ColumnType = col.DataType.Split('(')[0],
                CSharpType = col.CSharpType,
                DisplayName = string.IsNullOrWhiteSpace(col.ColumnComment) ? ToPascal(col.ColumnName) : col.ColumnComment,
                ColumnComment = col.ColumnComment,
                Length = col.Length,
                IsPk = col.IsPk,
                IsRequired = col.IsRequired && !col.IsPk,
                IsList = !col.IsPk && col.ColumnName is not ("createtimes" or "createtime" or "updateby" or "updatetime"),
                IsQuery = col.ColumnName is "title" or "name" or "user_name" or "dict_name",
                IsForm = !col.IsPk,
                Sort = sort++
            });
        }
        return table.Id;
    }

    // ---------- 配置保存（新建/编辑，含字段与子表） ----------

    public async Task<long> SaveAsync(GenTableSaveDto dto)
    {
        ValidateNames(dto);
        SysGenTable table;
        if (dto.Id == null)
        {
            if (await tableRepository.AnyAsync(x => x.TableName == dto.TableName && x.IsDeleted == false))
            {
                throw new BusinessException($"表 {dto.TableName} 已存在配置", ApiResultCode.BadRequest, ErrorCodes.COMMON_PARAM_INVALID);
            }
            table = new SysGenTable();
            MapToEntity(dto, table);
            table.Id = 0;
            await tableRepository.InsertAsync(table);
        }
        else
        {
            table = await tableRepository.GetByIdAsync(dto.Id.Value)
                ?? throw new BusinessException("生成配置不存在", ApiResultCode.NotFound, ErrorCodes.COMMON_NOT_FOUND);
            MapToEntity(dto, table);
            await tableRepository.UpdateAsync(table);
            // 全量重置字段行
            await columnRepository.DeletePhysicalWhereAsync(x => x.GenTableId == table.Id);
        }

        // 主表字段
        var sort = 0;
        foreach (var col in dto.Columns)
        {
            await columnRepository.InsertAsync(new SysGenTableColumn
            {
                GenTableId = table.Id,
                ColumnName = col.ColumnName,
                PropertyName = col.PropertyName,
                ColumnType = col.ColumnType,
                CSharpType = col.CSharpType,
                DisplayName = col.DisplayName,
                ColumnComment = col.ColumnComment,
                Length = col.Length,
                IsPk = col.IsPk,
                IsRequired = col.IsRequired,
                IsList = col.IsList,
                IsQuery = col.IsQuery,
                IsForm = col.IsForm,
                QueryType = col.QueryType,
                UiType = col.UiType,
                Sort = sort++
            });
        }

        // 子表：每张子表也建独立配置行（GenTableId 用主表 Id，TableName 区分；以 SubKey=主表Id:子表名 归组）
        if (dto.SubTables != null)
        {
            foreach (var sub in dto.SubTables)
            {
                sub.Columns ??= [];
                var subSort = 0;
                foreach (var col in sub.Columns)
                {
                    await columnRepository.InsertAsync(new SysGenTableColumn
                    {
                        GenTableId = table.Id,
                        ColumnName = col.ColumnName,
                        PropertyName = col.PropertyName,
                        ColumnType = col.ColumnType,
                        CSharpType = col.CSharpType,
                        DisplayName = col.DisplayName,
                        ColumnComment = col.ColumnComment,
                        Length = col.Length,
                        IsPk = col.IsPk,
                        IsRequired = col.IsRequired,
                        IsList = col.IsList,
                        IsQuery = col.IsQuery,
                        IsForm = col.IsForm,
                        QueryType = col.QueryType,
                        UiType = col.UiType,
                        Sort = subSort++
                    });
                }
            }
        }

        return table.Id;
    }

    public async Task DeleteAsync(long id)
    {
        await columnRepository.DeletePhysicalWhereAsync(x => x.GenTableId == id);
        await tableRepository.DeleteAsync(id);
    }

    // ---------- 生成与打包 ----------

    /// <summary>生成全部文件（内存），供预览与 zip</summary>
    public async Task<List<GenPreviewFile>> GenerateAsync(long id)
    {
        var detail = await LoadGenModelAsync(id);
        var files = new List<GenPreviewFile>();
        foreach (var (tplName, outPath) in TemplatesFor(detail))
        {
            var content = await RenderTemplateAsync(tplName, detail);
            files.Add(new GenPreviewFile(outPath, content));
        }
        return files;
    }

    /// <summary>打包 zip（目录结构按 Biz 规范摆放）</summary>
    public async Task<byte[]> DownloadAsync(long id)
    {
        var files = await GenerateAsync(id);
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = zip.CreateEntry(file.Path, CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(file.Content);
            }
        }
        return ms.ToArray();
    }

    // ---------- 模板渲染 ----------

    private static readonly (string Template, string OutputPath)[] SingleTableTemplates =
    [
        ("entity.sbn", "src/NetBase.Model/Entities/Biz/Biz{Entity}.cs"),
        ("service.sbn", "src/NetBase.Service/Biz/Biz{Entity}Service.cs"),
        ("controller.sbn", "src/NetBase.Api/Controllers/Biz/Biz{Entity}Controller.cs"),
        ("api-ts.sbn", "web/src/api/biz/{module}.ts"),
        ("view.sbn", "web/src/views/biz/{module}/index.vue"),
        ("sql-table.sbn", "sql/01_建表脚本.sql"),
        ("sql-menu.sbn", "sql/02_菜单权限种子.sql"),
        ("readme.sbn", "README.md")
    ];

    private static IEnumerable<(string Template, string OutputPath)> TemplatesFor(GenModel model) =>
        SingleTableTemplates.Select(t => (t.Template, t.OutputPath
            .Replace("{Entity}", model.Table.EntityName)
            .Replace("{module}", model.Table.ModuleName)));

    /// <summary>装载渲染模型（表 + 主表列 + 子表及其列），并裁掉非生成字段</summary>
    private async Task<GenModel> LoadGenModelAsync(long id)
    {
        var detail = await GetDetailWithColumnsAsync(id);
        var table = detail.Table;

        var model = new GenModel
        {
            Table = table,
            Columns = detail.Columns,
            SubTables = []
        };

        if (table.SubTables != null)
        {
            foreach (var sub in table.SubTables)
            {
                model.SubTables.Add(new GenSubModel
                {
                    Table = sub,
                    Columns = FilterColumns(detail.Columns, sub.TableName)
                });
            }
        }
        return model;
    }

    /// <summary>裁列：主表取无子表标记的行；子表按 SubTableName 归组</summary>
    private static List<SysGenTableColumn> FilterColumns(List<SysGenTableColumn> all, string? subTableName)
    {
        return all.Where(c => c.SubTableName == subTableName).ToList();
    }

    private async Task<string> RenderTemplateAsync(string templateName, GenModel model)
    {
        var assembly = typeof(SysGenTableService).Assembly;
        var resourceName = $"NetBase.Service.Templates.Gen.{templateName}";
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"模板资源缺失: {resourceName}");
        using var reader = new StreamReader(stream);
        var templateText = await reader.ReadToEndAsync();

        var template = Scriban.Template.Parse(templateText);
        if (template.HasErrors)
        {
            throw new InvalidOperationException($"模板解析失败: {string.Join("; ", template.Messages)}");
        }

        // 模板统一 snake_case 取成员（table.function_name / col.is_required / query_columns ...）。
        // 用字典模型（Scriban 原生最稳；实体反射访问在 List<T> 场景下 item 解析异常）
        return template.Render(BuildTemplateModel(model));
    }

    /// <summary>GenModel → snake_case 字典模板模型（列转为字典列表，Scriban 访问零反射问题）</summary>
    private static Dictionary<string, object?> BuildTemplateModel(GenModel model)
    {
        var table = new Dictionary<string, object?>
        {
            ["table_name"] = model.Table.TableName,
            ["table_comment"] = model.Table.TableComment,
            ["module_name"] = model.Table.ModuleName,
            ["function_name"] = model.Table.FunctionName,
            ["entity_name"] = model.Table.EntityName,
            ["author"] = model.Table.Author,
            ["data_scope"] = model.Table.DataScope,
            ["flow_doc"] = model.Table.FlowDoc,
        };
        var columns = model.Columns.Select(c => (object?)ColumnDict(c, null)).ToList();
        var query_columns = model.QueryColumns.Select(c => (object?)ColumnDict(c, null)).ToList();
        var list_columns = model.ListColumns.Select(c => (object?)ColumnDict(c, null)).ToList();
        var form_columns = model.FormColumns.Select(c => (object?)ColumnDict(c, null)).ToList();
        var sub_tables = model.SubTables.Select(st => (object?)new Dictionary<string, object?>
        {
            ["table_name"] = st.Table.TableName,
            ["table_comment"] = st.Table.TableComment,
            ["fk_column"] = st.Table.FkColumn,
            ["entity_name"] = st.Table.EntityName,
            ["columns"] = st.Columns.Select(c => (object?)ColumnDict(c, st.Table.TableName)).ToList(),
        }).ToList();

        return new Dictionary<string, object?>
        {
            ["table"] = table,
            ["columns"] = columns,
            ["query_columns"] = query_columns,
            ["list_columns"] = list_columns,
            ["form_columns"] = form_columns,
            ["sub_tables"] = sub_tables,
            ["now"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["comment_sql"] = new Func<string, string, string, string>((t, c, cm) => GenMetaService.BuildCommentSql(t, c, cm)),
        };
    }

    private static Dictionary<string, object?> ColumnDict(SysGenTableColumn c, string? subTableName) => new()
    {
        ["column_name"] = c.ColumnName,
        ["property_name"] = c.PropertyName,
        ["column_type"] = c.ColumnType,
        ["c_sharp_type"] = c.CSharpType,
        ["display_name"] = c.DisplayName,
        ["column_comment"] = c.ColumnComment,
        ["length"] = c.Length,
        ["is_pk"] = c.IsPk,
        ["is_required"] = c.IsRequired,
        ["is_list"] = c.IsList,
        ["is_query"] = c.IsQuery,
        ["is_form"] = c.IsForm,
        ["query_type"] = c.QueryType,
        ["ui_type"] = c.UiType,
        ["sort"] = c.Sort,
        ["sub_table_name"] = subTableName ?? (object?)string.Empty,
    };

    // ---------- 辅助 ----------

    private static void ValidateNames(GenTableSaveDto dto)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(dto.ModuleName, @"^[a-z][a-z0-9_]*$"))
        {
            throw new BusinessException("模块名只能为小写字母数字下划线", ApiResultCode.BadRequest, ErrorCodes.COMMON_PARAM_INVALID);
        }
        if (!System.Text.RegularExpressions.Regex.IsMatch(dto.EntityName, @"^[A-Z][A-Za-z0-9]*$"))
        {
            throw new BusinessException("实体名须为 PascalCase", ApiResultCode.BadRequest, ErrorCodes.COMMON_PARAM_INVALID);
        }
        foreach (var col in dto.Columns)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(col.PropertyName, @"^[A-Z][A-Za-z0-9]*$"))
            {
                throw new BusinessException($"字段属性名 {col.PropertyName} 须为 PascalCase", ApiResultCode.BadRequest, ErrorCodes.COMMON_PARAM_INVALID);
            }
        }
    }

    private static void MapToEntity(GenTableSaveDto dto, SysGenTable table)
    {
        table.TableName = dto.TableName.Trim().ToLowerInvariant();
        table.TableComment = dto.TableComment;
        table.ModuleName = dto.ModuleName;
        table.FunctionName = dto.FunctionName;
        table.EntityName = dto.EntityName;
        table.Author = dto.Author;
        table.DataScope = dto.DataScope;
        table.FlowDoc = dto.FlowDoc;
        table.SourceType = dto.SourceType;
        table.SubTablesJson = dto.SubTables is { Count: > 0 } ? JsonSerializer.Serialize(dto.SubTables, JsonOpts) : null;
    }

    private static List<SysGenSubTable>? ParseSubTables(SysGenTable table) =>
        string.IsNullOrEmpty(table.SubTablesJson)
            ? null
            : JsonSerializer.Deserialize<List<SysGenSubTable>>(table.SubTablesJson, JsonOpts);

    /// <summary>C# 类型 → TypeScript 类型（模板过滤器）</summary>
    private static string ToTsType(string csharpType) => csharpType switch
    {
        "string" => "string",
        "long" or "int" or "short" or "decimal" or "double" or "float" => "number",
        "bool" => "boolean",
        "DateTime" => "string",
        _ => "unknown"
    };

    private static string ToPascal(string name) =>
        string.Concat(name.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
}

/// <summary>Scriban 渲染模型（表 + 列 + 子表树）</summary>
public class GenModel
{
    /// <summary>模板变量 now</summary>
    public string Now => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>模板函数：生成 PG 列注释语句</summary>
    public string CommentSql(string tableName, string columnName, string comment) =>
        GenMetaService.BuildCommentSql(tableName, columnName, comment);

    public SysGenTable Table { get; set; } = new();
    public List<SysGenTableColumn> Columns { get; set; } = [];
    public List<GenSubModel> SubTables { get; set; } = [];
    /// <summary>主表业务字段（非主键非审计列）</summary>
    public List<SysGenTableColumn> FormColumns => Columns.Where(c => c.IsForm).ToList();
    public List<SysGenTableColumn> ListColumns => Columns.Where(c => c.IsList).ToList();
    public List<SysGenTableColumn> QueryColumns => Columns.Where(c => c.IsQuery).ToList();
}

public class GenSubModel
{
    public SysGenSubTable Table { get; set; } = new();
    public List<SysGenTableColumn> Columns { get; set; } = [];
}
