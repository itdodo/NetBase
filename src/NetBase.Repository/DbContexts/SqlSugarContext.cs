using System.Reflection;
using Microsoft.Extensions.Logging;
using NetBase.Common.Extensions;
using NetBase.Model.Entities;
using NetBase.Repository.Auditing;
using SqlSugar;

namespace NetBase.Repository.DbContexts;

/// <summary>
/// SqlSugar 配置选项（对应 appsettings.json 的 Db 节点）。
/// </summary>
public class SqlSugarOptions
{
    public const string SectionName = "Db";

    /// <summary>连接字符串</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>是否在启动时自动建表并写入种子数据</summary>
    public bool InitEnabled { get; set; } = true;

    /// <summary>是否输出 SQL 日志</summary>
    public bool LogSql { get; set; } = true;

    /// <summary>慢SQL阈值（毫秒）</summary>
    public int SlowSqlThresholdMs { get; set; } = 3000;
}

/// <summary>
/// SqlSugarScope 单例封装（线程安全，官方推荐的单例使用方式）。
/// 内置软删除全局过滤器、AOP SQL 日志、慢查询告警与错误日志。
/// </summary>
public class SqlSugarContext
{
    /// <summary>默认库配置ID</summary>
    public const string ConfigId = "netbase";

    private readonly SqlSugarScope? _client;

    public SqlSugarContext(SqlSugarOptions options, IOperatorProvider? operatorProvider, ILogger<SqlSugarContext>? logger = null)
    {
        Options = options;
        if (options.ConnectionString.IsNullOrEmpty())
        {
            // 未配置连接串时允许启动（仅提供非数据库能力），首次使用 Client 时给出明确错误
            return;
        }

        _client = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = ConfigId,
            ConnectionString = options.ConnectionString,
            DbType = DbType.SqlServer,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        }, db =>
        {
            // 全局软删除过滤器：ISoftDelete 实体自动追加 IsDeleted = 0 条件
            db.QueryFilter.AddTableFilter<ISoftDelete>(x => !x.IsDeleted);

            // 审计字段 AOP 兜底：Insert 自动填创建时间/创建人，Update 自动填更新时间/更新人。
            // 服务层显式赋值仍优先生效（CreateBy 仅在为空时填充），杜绝遗漏。
            var operatorName = operatorProvider?.OperatorName ?? "system";
            db.Aop.DataExecuting = (oldValue, entityInfo) =>
            {
                switch (entityInfo.OperationType)
                {
                    case DataFilterType.InsertByObject when entityInfo.PropertyName == nameof(BaseEntity.CreateTime):
                        entityInfo.SetValue(DateTime.Now);
                        break;
                    case DataFilterType.InsertByObject when entityInfo.PropertyName == nameof(BaseEntity.CreateBy)
                                                             && string.IsNullOrEmpty(oldValue as string):
                        entityInfo.SetValue(operatorName);
                        break;
                    case DataFilterType.UpdateByObject when entityInfo.PropertyName == nameof(BaseEntity.UpdateTime):
                        entityInfo.SetValue(DateTime.Now);
                        break;
                    case DataFilterType.UpdateByObject when entityInfo.PropertyName == nameof(BaseEntity.UpdateBy):
                        entityInfo.SetValue(operatorName);
                        break;
                }
            };

            // AOP：SQL 日志 + 慢查询告警 + 错误日志
            db.Aop.OnLogExecuting = (sql, parameters) =>
            {
                if (options.LogSql)
                {
                    logger?.LogDebug("执行SQL: {Sql} 参数: {Params}", sql,
                        parameters.ToDictionary(p => p.ParameterName, p => p.Value).ToJson());
                }
            };
            db.Aop.OnLogExecuted = (sql, _) =>
            {
                var elapsed = db.Ado.SqlExecutionTime.TotalMilliseconds;
                if (elapsed > options.SlowSqlThresholdMs)
                {
                    logger?.LogWarning("慢SQL({Elapsed:F0}ms): {Sql}", elapsed, sql);
                }
            };
            db.Aop.OnError = ex => logger?.LogError(ex, "SQL执行出错: {Sql}", ex.Sql);
        });
    }

    /// <summary>数据库连接配置</summary>
    public SqlSugarOptions Options { get; }

    public ISqlSugarClient Client => _client ?? throw new InvalidOperationException(
        "数据库连接串未配置，请检查 appsettings.json 中的 Db:ConnectionString 节点。");

    public bool IsConfigured => _client != null;

    /// <summary>
    /// 按实体所在程序集初始化 CodeFirst 建表。
    /// </summary>
    public void InitDatabase()
    {
        var entityTypes = typeof(BaseEntity).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(BaseEntity).IsAssignableFrom(t))
            .ToArray();

        Client.CodeFirst.InitTables(entityTypes);
    }
}
