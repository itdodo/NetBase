using Microsoft.Extensions.DependencyInjection;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using Xunit;
using Xunit.Abstractions;

namespace NetBase.IntegrationTests;

/// <summary>
/// 乐观锁并发控制验证：陈旧版本更新必须失败（WHERE Version 不匹配 → 0 行）。
/// </summary>
[Collection("Integration")]
public class OptimisticLockTests
{
    private readonly IRepository<SysRole> _repo;

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public OptimisticLockTests(IntegrationFixture fixture, Xunit.Abstractions.ITestOutputHelper output)
    {
        _repo = fixture.GetRepository<SysRole>();
        _output = output;
    }

    [Fact]
    public async Task StaleVersion_Update_ShouldFail()
    {
        // 清理前缀旧数据，保证 WHERE Version=100 精确匹配本次插入的行
        await _repo.DeletePhysicalWhereAsync(x => x.RoleCode.StartsWith("olck"));

        var code = IntegrationFixture.Uid("olck");
        var role = await _repo.InsertAsync(new SysRole
        {
            RoleName = "OLCK", RoleCode = code, Status = 1, Version = 100
        });

        // 会话 A：读 v100 → 更新成功，库中版本变 101
        var copyA = await _repo.GetByIdAsync(role.Id);
        copyA!.RoleName = "A的修改";
        var okA = await _repo.UpdateWithVersionCheckAsync(copyA);
        var afterA = await _repo.GetByIdAsync(role.Id);
        _output.WriteLine($"A更新: ok={okA}, copyA.Ver={copyA.Version}, 库Ver={afterA?.Version}");

        // 会话 B：持有 A 更新前的陈旧副本（v100）→ 乐观锁应拦截返回 false
        var staleCopy = new SysRole
        {
            Id = role.Id,
            RoleName = "B的修改",
            RoleCode = code,
            Status = 1,
            Version = 100 // A 更新前的版本
        };
        Assert.False(await _repo.UpdateWithVersionCheckAsync(staleCopy));

        // 数据仍是 A 的修改，且版本继续推进（101）
        var final = await _repo.GetByIdAsync(role.Id);
        Assert.Equal("A的修改", final!.RoleName);
        Assert.Equal(101, final.Version);
    }
}
