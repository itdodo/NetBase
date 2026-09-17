using Microsoft.Extensions.DependencyInjection;
using NetBase.Repository.DbContexts;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// 数据库迁移执行器测试：sys_db_migration 记录表建立、脚本按版本应用、重复执行幂等。
/// （夹具启动时已执行过一轮，此处验证结果与二次执行安全。）
/// </summary>
[Collection("Integration")]
public class MigrationRunnerTests
{
    private readonly IntegrationFixture _fixture;

    public MigrationRunnerTests(IntegrationFixture fixture) => _fixture = fixture;

    [Fact]
    public void Run_Twice_ShouldBeIdempotentAndRecorded()
    {
        var context = _fixture.GetService<SqlSugarContext>();

        new DbMigrationRunner(context).Run(); // 第二轮执行（夹具启动时为第一轮）

        var versions = context.Client.Ado.SqlQuery<string>(
            "SELECT Version FROM sys_db_migration ORDER BY Version");
        Assert.Contains("0001", versions);

        // 记录唯一：重复执行不产生重复版本行
        var distinct = versions.Distinct().Count();
        Assert.Equal(versions.Count, distinct);
    }
}
