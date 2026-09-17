using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ICacheService = NetBase.Common.Cache.ICacheService;
using NetBase.Common.Cache;
using NetBase.Model.Entities;
using NetBase.Repository.Auditing;
using NetBase.Repository.DbContexts;
using NetBase.Repository.Repositories;
using NetBase.Middleware.Cache;
using NetBase.Service.Sys;
using SqlSugar;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// 集成测试 Fixture：连接专用测试库 NetBase_Test（与开发库隔离），
/// 容器组装与生产一致的仓储/服务管道（含软删除过滤器、审计 AOP）。
/// 数据策略：每测试类/方法使用唯一编码的行，不跨用例清理。
/// </summary>
public sealed class IntegrationFixture
{
    public IServiceProvider Services { get; }

    /// <summary>连接串环境变量名（CI 覆盖用）</summary>
    public const string ConnectionStringEnvVar = "NETBASE_TEST_CONNECTIONSTRING";

    private static readonly string DefaultConnectionString =
        "Server=localhost;Database=NetBase_Test;Uid=sa;Pwd=Abcd1234;TrustServerCertificate=True;";

    /// <summary>测试库连接串：环境变量 NETBASE_TEST_CONNECTIONSTRING 优先，缺省本机库</summary>
    public static string ConnectionString { get; } =
        Environment.GetEnvironmentVariable(ConnectionStringEnvVar) ?? DefaultConnectionString;

    public IntegrationFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<IOperatorProvider>(new TestOperatorProvider());

        var options = new SqlSugarOptions
        {
            ConnectionString = ConnectionString,
            InitEnabled = false,
            LogSql = false,
            SnowflakeWorkerId = 63 // 测试专用机器码，与生产/开发实例隔离
        };
        services.AddSingleton(options);
        services.AddSingleton<SqlSugarContext>();
        services.AddSingleton<ISqlSugarClient>(sp => sp.GetRequiredService<SqlSugarContext>().Client);
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // 服务层依赖（与生产注册一致的手动组装）
        services.AddScoped<ICacheService, MemoryCacheService>();
        services.AddSingleton<ISysConfigService, SysConfigService>();
        services.AddScoped<ISysDeptService, SysDeptService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IDataScopeService, DataScopeService>();
        services.AddScoped<ISysUserService, SysUserService>();
        services.AddScoped<ISysRoleService, SysRoleService>();
        services.AddScoped<ISysMenuService, SysMenuService>();
        services.AddScoped<ISysLogService, SysLogService>();
        services.AddScoped<ISysMessageService, SysMessageService>();
        services.AddScoped<ISysNoticeService, SysNoticeService>();

        // 审批流引擎（通知用测试替身收集，业务回调用测试 Handler）
        var notifications = new FlowTestNotifications();
        services.AddSingleton(notifications);
        services.AddSingleton<NetBase.Common.Realtime.INotifyService>(notifications);
        services.AddScoped<NetBase.Service.Sys.Flow.ApproverResolver>();
        services.AddScoped<NetBase.Service.Sys.Flow.IFlowEngine, NetBase.Service.Sys.Flow.FlowEngine>();
        services.AddScoped<NetBase.Service.Sys.Flow.ISysFlowDefinitionService, NetBase.Service.Sys.Flow.SysFlowDefinitionService>();
        services.AddScoped<NetBase.Service.Sys.Flow.IFlowQueryService, NetBase.Service.Sys.Flow.SysFlowQueryService>();
        services.AddScoped<NetBase.Service.Sys.Flow.IFlowBusinessHandler, FlowTestHandler>();

        Services = services.BuildServiceProvider();

        // 先连 master 确保测试库存在（应用连接指向测试库本身，库不存在时无法自建），再 CodeFirst 建表
        using (var master = new Microsoft.Data.SqlClient.SqlConnection(
            "Server=localhost;Database=master;Uid=sa;Pwd=Abcd1234;TrustServerCertificate=True;"))
        {
            master.Open();
            using var cmd = master.CreateCommand();
            cmd.CommandText = "IF DB_ID('NetBase_Test') IS NULL CREATE DATABASE NetBase_Test";
            cmd.ExecuteNonQuery();
        }

        var context = Services.GetRequiredService<SqlSugarContext>();
        context.InitDatabase();
        new NetBase.Repository.DbContexts.DbMigrationRunner(context).Run();

        // 过滤唯一索引：RoleCode 唯一性约束（跨运行的陈旧行不占用编码，与生产 DbSeeder 一致）
        using (var conn = new Microsoft.Data.SqlClient.SqlConnection(ConnectionString))
        {
            conn.Open();
            foreach (var (table, index, column) in new[]
                     {
                         ("sys_role", "uk_sys_role_rolecode", "RoleCode"),
                         ("sys_user", "uk_sys_user_username", "UserName")
                     })
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index}' AND object_id = OBJECT_ID('{table}')) " +
                                  $"CREATE UNIQUE INDEX [{index}] ON [{table}] ([{column}]) WHERE [IsDeleted] = 0";
                cmd.ExecuteNonQuery();
            }
        }
    }

    /// <summary>创建仓储</summary>
    public IRepository<T> GetRepository<T>() where T : BaseEntity, new()
        => Services.GetRequiredService<IRepository<T>>();

    /// <summary>解析服务</summary>
    public T GetService<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <summary>直接执行 SQL（断言用）</summary>
    public int Exec(string sql) => GetService<SqlSugarContext>().Client.Ado.ExecuteCommand(sql);

    /// <summary>唯一编码（避免并行用例互相污染）</summary>
    public static string Uid(string prefix) => $"{prefix}{Random.Shared.Next(100000, 999999)}";

    /// <summary>切换当前操作人（审批流等多用户场景测试用）</summary>
    public static void SetOperator(string name, long id) => TestOperatorProvider.SetOperator(name, id);

    private sealed class TestOperatorProvider : IOperatorProvider
    {
        /// <summary>多用户场景（审批流等）切换当前操作人</summary>
        public static void SetOperator(string name, long id)
        {
            Name = name;
            UserId = id;
        }

        public static string Name { get; private set; } = "it-test";

        public static long UserId { get; private set; } = 63;

        public string? OperatorName => Name;

        public long? OperatorUserId => UserId;
    }
}

/// <summary>测试集合：所有集成测试共用一个 Fixture（库只建一次）</summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationFixture>
{
}
