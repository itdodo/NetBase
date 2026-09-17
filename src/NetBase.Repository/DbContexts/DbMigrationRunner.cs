using System.Reflection;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace NetBase.Repository.DbContexts;

/// <summary>
/// 数据库迁移执行器：按文件名顺序执行 db/migrations 下的嵌入式 SQL 脚本，
/// 已应用版本记录在 sys_db_migration，不重复执行。
/// 约定：脚本必须幂等（IF NOT EXISTS / IF EXISTS 守卫）；历史脚本禁止修改；
/// 文件命名 `NNNN_描述.sql`（NNNN 为四位版本号）；单文件一个批次（不含 GO）。
/// CodeFirst 仍负责实体建表/加列；凡数据回填、索引调优、不可逆或需评审的 DDL 一律走迁移脚本。
/// </summary>
public class DbMigrationRunner(SqlSugarContext context, ILogger? logger = null)
{
    private const string TableName = "sys_db_migration";

    public void Run()
    {
        var db = context.Client;

        // 执行记录表自举
        db.Ado.ExecuteCommand(
            $"""IF OBJECT_ID(N'dbo.{TableName}') IS NULL CREATE TABLE dbo.{TableName} ("""
            + " Version VARCHAR(20) NOT NULL PRIMARY KEY,"
            + " Name NVARCHAR(200) NOT NULL,"
            + " AppliedTime DATETIME2 NOT NULL DEFAULT SYSDATETIME())");

        var applied = db.Ado.SqlQuery<string>($"SELECT Version FROM {TableName}").ToHashSet();

        var assembly = Assembly.GetExecutingAssembly();
        var scripts = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".db.migrations.", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".sql"))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var executed = 0;
        foreach (var resource in scripts)
        {
            var fileName = resource.Substring(resource.IndexOf(".db.migrations.") + ".db.migrations.".Length);
            var stem = fileName[..fileName.LastIndexOf('.')]; // 0001_baseline
            var version = stem.Split('_')[0];                 // 0001
            var name = stem[(version.Length + 1)..];          // baseline

            if (applied.Contains(version))
            {
                continue;
            }

            var sql = new StreamReader(assembly.GetManifestResourceStream(resource)!).ReadToEnd();
            var result = db.Ado.UseTran(() =>
            {
                db.Ado.ExecuteCommand(sql);
                db.Ado.ExecuteCommand(
                    $"INSERT INTO {TableName} (Version, Name) VALUES (@v, @n)",
                    new SugarParameter[] { new("@v", version), new("@n", name) });
            });
            if (!result.IsSuccess)
            {
                throw result.ErrorException ?? new InvalidOperationException($"迁移 {version} 执行失败");
            }
            executed++;
            logger?.LogInformation("数据库迁移 {Version} ({Name}) 已应用", version, name);
        }

        if (executed > 0)
        {
            logger?.LogInformation("数据库迁移完成：本次应用 {Count} 个脚本", executed);
        }
    }
}
