using NetBase.Common.Cache;
using NetBase.Common.Exceptions;
using NetBase.Model.Entities;
using NetBase.Model.Enums;
using NetBase.Repository.Repositories;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// 仓储层集成测试：真实库验证软删除、物理删除、雪花主键、事务回滚、批量填充、排序白名单。
/// 每用例使用唯一编码数据，不依赖执行顺序。
/// </summary>
[Collection("Integration")]
public class RepositoryIntegrationTests
{
    private readonly IRepository<SysRole> _roleRepo;
    private readonly IntegrationFixture _fixture;

    public RepositoryIntegrationTests(IntegrationFixture fixture)
    {
        _fixture = fixture;
        _roleRepo = fixture.GetRepository<SysRole>();
    }

    private static SysRole NewRole(string code, int dataScope = 1) => new()
    {
        RoleName = "IT-" + code,
        RoleCode = code,
        Status = (int)StatusEnum.Enabled,
        DataScope = dataScope,
        Sort = 1
    };

    [Fact]
    public async Task Insert_ShouldFillSnowflakeId_NotZero()
    {
        var role = NewRole(IntegrationFixture.Uid("it"));
        await _roleRepo.InsertAsync(role);

        Assert.NotEqual(0, role.Id);
        var db = await _roleRepo.GetByIdAsync(role.Id);
        Assert.NotNull(db);
        Assert.Equal(role.Id, db!.Id);
    }

    [Fact]
    public async Task SoftDelete_ShouldHideFromQuery_ButKeepRow()
    {
        var code = IntegrationFixture.Uid("sd");
        var role = await _roleRepo.InsertAsync(NewRole(code));

        await _roleRepo.DeleteAsync(role);

        // 查询自动过滤
        Assert.Null(await _roleRepo.GetByIdAsync(role.Id));
        Assert.False(await _roleRepo.AnyAsync(x => x.RoleCode == code));

        // 行还在且 IsDeleted=1（软删除可追溯）
        var raw = await _roleRepo.Db.Ado.SqlQuerySingleAsync<int>(
            $"SELECT COUNT(1)::int FROM sys_role WHERE rolecode = '{code}' AND isdeleted = true");
        Assert.Equal(1, raw);
    }

    [Fact]
    public async Task PhysicalDelete_ShouldRemoveRow()
    {
        var code = IntegrationFixture.Uid("pd");
        var role = await _roleRepo.InsertAsync(NewRole(code));

        await _roleRepo.DeletePhysicalWhereAsync(x => x.RoleCode == code);

        var raw = await _roleRepo.Db.Ado.SqlQuerySingleAsync<int>(
            $"SELECT COUNT(1) FROM sys_role WHERE RoleCode = '{code}'");
        Assert.Equal(0, raw);
    }

    [Fact]
    public async Task InsertRange_ShouldFillAllSnowflakeIds()
    {
        // 批量插入走 UNION ALL，AOP 不触发——Repository 必须显式填充（历史 bug 回归）
        var roles = Enumerable.Range(0, 3)
            .Select(i => NewRole(IntegrationFixture.Uid("batch") + i))
            .ToList();

        await _roleRepo.InsertRangeAsync(roles);

        Assert.All(roles, r => Assert.NotEqual(0, r.Id));
        Assert.Equal(3, roles.Select(r => r.Id).Distinct().Count());
        Assert.All(roles, async r => Assert.NotNull(await _roleRepo.GetByIdAsync(r.Id)));
    }

    [Fact]
    public async Task Transaction_ShouldRollback_OnFailure()
    {
        var code = IntegrationFixture.Uid("tx");
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await _roleRepo.TransactionAsync(async () =>
            {
                await _roleRepo.InsertAsync(NewRole(code));
                // 第二条插入违反唯一索引（重复 code），事务应整体回滚
                return await _roleRepo.InsertAsync(NewRole(code)) != null
                    ? throw new InvalidOperationException("trigger rollback")
                    : true;
            });
        });

        Assert.False(await _roleRepo.AnyAsync(x => x.RoleCode == code)); // 第一条已回滚
    }

    [Fact]
    public async Task Transaction_ShouldCommit_WhenSuccess()
    {
        var code = IntegrationFixture.Uid("txc");
        await _roleRepo.TransactionAsync(async () =>
        {
            await _roleRepo.InsertAsync(NewRole(code));
            return true;
        });

        Assert.True(await _roleRepo.AnyAsync(x => x.RoleCode == code));
    }

    [Fact]
    public async Task PageList_SortField_ShouldRespectWhitelist()
    {
        var code = IntegrationFixture.Uid("sort");
        var r1 = await _roleRepo.InsertAsync(NewRole(code + "a"));
        var r2 = await _roleRepo.InsertAsync(NewRole(code + "b"));
        r2.Sort = 5;
        await _roleRepo.UpdateAsync(r2);

        // 合法排序字段：按 Sort 降序
        var page = await _roleRepo.GetPageListAsync(
            x => x.RoleCode.StartsWith(code),
            new NetBase.Common.Results.PageQuery { PageIndex = 1, PageSize = 10, SortField = "Sort", SortDesc = true });
        Assert.Equal(r2.Id, page.Items[0].Id);

        // 非法排序字段（注入载荷）：应回退主键排序而非报错
        var safe = await _roleRepo.GetPageListAsync(
            x => x.RoleCode.StartsWith(code),
            new NetBase.Common.Results.PageQuery { PageIndex = 1, PageSize = 10, SortField = "1;DROP TABLE sys_role;--" });
        Assert.True(safe.Total >= 2);

        // 表仍存在（未被执行注入）
        Assert.True(await _roleRepo.AnyAsync(x => x.RoleCode.StartsWith(code)));
    }

    [Fact]
    public async Task AuditAop_ShouldFillCreateFields()
    {
        var code = IntegrationFixture.Uid("audit");
        var role = NewRole(code);
        // 不手动赋 CreateTime/CreateBy —— AOP 兜底应自动填充
        await _roleRepo.InsertAsync(role);

        var db = await _roleRepo.GetByIdAsync(role.Id);
        Assert.NotNull(db);
        Assert.NotEqual(default, db!.CreateTime);
        Assert.Equal("it-test", db.CreateBy); // Fixture 注册的测试操作人
    }
}
