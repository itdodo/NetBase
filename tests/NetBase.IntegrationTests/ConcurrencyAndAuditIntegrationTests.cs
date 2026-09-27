using Microsoft.Extensions.DependencyInjection;
using NetBase.Common.Auditing;
using NetBase.Common.Exceptions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using NetBase.Service.Sys;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// 工程增强集成测试：乐观锁并发冲突（显式 UpdateWithVersionCheckAsync）、字段级变更审计。
/// UnitOfWork 过滤器机制测试见 UnitOfWorkFilterTests。
/// </summary>
[Collection("Integration")]
public class ConcurrencyAndAuditIntegrationTests
{
    private readonly IntegrationFixture _fixture;
    private readonly IRepository<SysRole> _roleRepo;

    public ConcurrencyAndAuditIntegrationTests(IntegrationFixture fixture)
    {
        _fixture = fixture;
        _roleRepo = fixture.GetRepository<SysRole>();
    }

    private static SysRole NewRole(string code, string roleName) => new()
    {
        RoleName = roleName, RoleCode = code, Status = 1, Sort = 1
    };

    [Fact]
    public async Task OptimisticLock_ConcurrentUpdate_ShouldFailSecondWrite()
    {
        var code = IntegrationFixture.Uid("ol");
        var role = await _roleRepo.InsertAsync(NewRole(code, "原始名"));

        // 会话 A：读到实体（版本 0）后乐观锁更新 → 成功，内存与库中版本自增 1
        var copyA = await _roleRepo.GetByIdAsync(role.Id);
        copyA!.RoleName = "A修改";
        Assert.True(await _roleRepo.UpdateWithVersionCheckAsync(copyA));
        Assert.Equal(1, copyA.Version);

        // 会话 B：持有 A 更新前的旧版本（0）→ 冲突返回 false，数据不被覆盖
        var stale = new SysRole { Id = role.Id, RoleName = "B修改", RoleCode = code, Status = 1, Version = 0 };
        Assert.False(await _roleRepo.UpdateWithVersionCheckAsync(stale));

        // 最终值仍是 A 的修改，版本保持 1
        var final = await _roleRepo.GetByIdAsync(role.Id);
        Assert.Equal("A修改", final!.RoleName);
        Assert.Equal(1, final.Version);
    }

    [Fact]
    public async Task OptimisticLock_ServiceStaleVersion_ShouldRejectAndFreshVersionPass()
    {
        var code = IntegrationFixture.Uid("olsvc");
        var svc = _fixture.GetService<ISysRoleService>();

        // 创建（版本 0）→ 会话 A 持版本 0 编辑成功（库中版本推进到 1）
        var id = await svc.CreateAsync(new RoleSaveDto
        {
            RoleName = "跨会话原始", RoleCode = code, Status = 1, DataScope = 1
        });
        await svc.UpdateAsync(id, new RoleSaveDto
        {
            RoleName = "会话A的修改", RoleCode = code, Status = 1, DataScope = 1, Version = 0
        });

        // 会话 B 仍持第一次编辑前（版本 0）的副本 → 保存被拒，A 的修改不被覆盖
        var stale = await Assert.ThrowsAsync<BusinessException>(() => svc.UpdateAsync(id, new RoleSaveDto
        {
            RoleName = "会话B的修改", RoleCode = code, Status = 1, DataScope = 1, Version = 0
        }));
        Assert.Contains("他人修改", stale.Message);
        // 错误码契约：乐观锁冲突必须 code=409 + COMMON_CONCURRENCY_CONFLICT（前端按此分支）
        Assert.Equal(ApiResultCode.Conflict, stale.Code);
        Assert.Equal(ErrorCodes.COMMON_CONCURRENCY_CONFLICT, stale.ErrorCode);

        var afterConflict = await _roleRepo.GetByIdAsync(id);
        Assert.Equal("会话A的修改", afterConflict!.RoleName);
        Assert.Equal(1, afterConflict.Version);

        // 刷新后的会话 B 持最新版本（1）→ 正常保存
        await svc.UpdateAsync(id, new RoleSaveDto
        {
            RoleName = "会话B刷新后的修改", RoleCode = code, Status = 1, DataScope = 1, Version = 1
        });
        var final = await _roleRepo.GetByIdAsync(id);
        Assert.Equal("会话B刷新后的修改", final!.RoleName);
        Assert.Equal(2, final.Version);
    }

    [Fact]
    public async Task ChangeAudit_Update_ShouldRecordFieldDiff()
    {
        var code = IntegrationFixture.Uid("ca");
        var role = await _roleRepo.InsertAsync(NewRole(code, "变更前名称"));

        var repo = _fixture.GetRepository<SysChangeLog>();
        // 计数限定本条记录：测试库跨运行复用，全表计数会误把历史运行的日志算进基数
        var beforeCount = await repo.CountAsync(x => x.TableName == "SysRole" && x.RecordId == role.Id.ToString());

        // 走带审计的更新（Service 层 UpdateWithAuditAsync 的仓储路径）
        var entity = await _roleRepo.GetByIdAsync(role.Id);
        entity!.RoleName = "变更后名称";
        await _roleRepo.UpdateWithAuditAsync(entity);

        var logs = await repo.GetListAsync(x => x.TableName == "SysRole" && x.RecordId == role.Id.ToString());
        Assert.Equal(beforeCount + 1, logs.Count);
        Assert.Contains("RoleName", logs[^1].Changes);
        Assert.Contains("变更前名称", logs[^1].Changes);
        Assert.Contains("变更后名称", logs[^1].Changes);
    }

    [Fact]
    public async Task ChangeAudit_Conflict_ShouldNotRecord()
    {
        var code = IntegrationFixture.Uid("cac");
        var role = await _roleRepo.InsertAsync(NewRole(code, "冲突原始名"));

        var logRepo = _fixture.GetRepository<SysChangeLog>();
        var before = await logRepo.CountAsync(x => x.TableName == "SysRole" && x.RecordId == role.Id.ToString());

        // 持陈旧版本（99）更新 → 乐观锁冲突，不落审计
        var ok = await _roleRepo.UpdateWithAuditAsync(new SysRole
        {
            Id = role.Id, RoleName = "陈旧修改", RoleCode = code, Status = 1, Version = 99
        });

        Assert.False(ok);
        var after = await logRepo.CountAsync(x => x.TableName == "SysRole" && x.RecordId == role.Id.ToString());
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task ChangeAudit_NoChange_ShouldNotRecord()
    {
        var code = IntegrationFixture.Uid("ca2");
        var role = await _roleRepo.InsertAsync(NewRole(code, "无变化名称"));

        var repo = _fixture.GetRepository<SysChangeLog>();
        var before = await repo.CountAsync(x => x.TableName == "SysRole" && x.RecordId == role.Id.ToString());

        var entity = await _roleRepo.GetByIdAsync(role.Id);
        await _roleRepo.UpdateWithAuditAsync(entity!); // 无字段变化

        var after = await repo.CountAsync(x => x.TableName == "SysRole" && x.RecordId == role.Id.ToString());
        Assert.Equal(before, after);
    }

    [Fact]
    public void AuditDiff_ShouldMaskSensitiveFields()
    {
        var before = new { UserName = "admin", Password = "Old123" };
        var after = new { UserName = "admin", Password = "New456" };

        var diff = AuditDiff.Diff(before, after);

        Assert.NotNull(diff);
        Assert.Contains("Password", diff);
        Assert.DoesNotContain("Old123", diff);
        Assert.DoesNotContain("New456", diff);
    }

    [Fact]
    public async Task DeptChangeAudit_UpdateLeader_ShouldRecordAndConsecutiveEditsPass()
    {
        var deptSvc = _fixture.GetService<ISysDeptService>();
        var changeRepo = _fixture.GetRepository<SysChangeLog>();
        var code = IntegrationFixture.Uid("deptaudit");

        // 创建部门（无负责人）→ 编辑设置负责人 → 变更日志应记录字段级 diff
        var id = await deptSvc.CreateAsync(new DeptSaveDto
        {
            DeptName = "审计部门" + code, DeptCode = code, ParentId = 0, Sort = 1, Status = 1
        });

        var baseCount = await changeRepo.CountAsync(
            x => x.TableName == "SysDept" && x.RecordId == id.ToString());
        await deptSvc.UpdateAsync(id, new DeptSaveDto
        {
            DeptName = "审计部门" + code, DeptCode = code, ParentId = 0, Sort = 1, Status = 1,
            Leader = "负责人甲", LeaderUserId = 852698478059589
        });
        var afterFirst = await changeRepo.CountAsync(
            x => x.TableName == "SysDept" && x.RecordId == id.ToString());
        Assert.Equal(baseCount + 1, afterFirst); // 负责人变更落审计

        // 连续第二次编辑（服务端读时版本）：不冲突、正常保存
        await deptSvc.UpdateAsync(id, new DeptSaveDto
        {
            DeptName = "审计部门" + code, DeptCode = code, ParentId = 0, Sort = 2, Status = 1,
            Leader = "负责人甲", LeaderUserId = 852698478059589
        });
        var afterSecond = await changeRepo.CountAsync(
            x => x.TableName == "SysDept" && x.RecordId == id.ToString());
        Assert.Equal(afterFirst + 1, afterSecond); // Sort 变更也落审计
    }


    [Fact]
    public async Task RepositoryAudit_OrdinaryUpdate_ShouldAutoRecord()
    {
        // 底层统一审计：未经服务层显式接入的模块，普通仓储更新也自动落变更日志
        var dictRepo = _fixture.GetRepository<SysDictType>();
        var changeRepo = _fixture.GetRepository<SysChangeLog>();
        var code = IntegrationFixture.Uid("repoaudit");
        var dict = await dictRepo.InsertAsync(new SysDictType
        {
            DictName = "底层审计字典" + code, DictCode = code, Status = 1
        });

        var baseCount = await changeRepo.CountAsync(x => x.TableName == "SysDictType" && x.RecordId == dict.Id.ToString());
        dict.Remark = "底层审计验证";
        await dictRepo.UpdateAsync(dict);

        var after = await changeRepo.CountAsync(x => x.TableName == "SysDictType" && x.RecordId == dict.Id.ToString());
        Assert.Equal(baseCount + 1, after);
    }

    [Fact]
    public async Task RepositoryAudit_BatchUpdateWhere_ShouldRecordEachRow()
    {
        var dictRepo = _fixture.GetRepository<SysDictType>();
        var changeRepo = _fixture.GetRepository<SysChangeLog>();
        var code = IntegrationFixture.Uid("repobatch");
        var d1 = await dictRepo.InsertAsync(new SysDictType { DictName = "批审一" + code, DictCode = code + "a", Status = 1 });
        var d2 = await dictRepo.InsertAsync(new SysDictType { DictName = "批审二" + code, DictCode = code + "b", Status = 1 });

        var baseCount = await changeRepo.CountAsync(x => x.TableName == "SysDictType");
        await dictRepo.UpdateWhereAsync(
            x => x.DictCode == code + "a" || x.DictCode == code + "b",
            x => new SysDictType { Status = 0 });
        var after = await changeRepo.CountAsync(x => x.TableName == "SysDictType");

        Assert.Equal(baseCount + 2, after); // 两行各落一条（批量审计逐行）
    }

    [Fact]
    public async Task RepositoryAudit_SoftDelete_ShouldRecordDeletion()
    {
        var dictRepo = _fixture.GetRepository<SysDictType>();
        var changeRepo = _fixture.GetRepository<SysChangeLog>();
        var code = IntegrationFixture.Uid("repodel");
        var dict = await dictRepo.InsertAsync(new SysDictType
        {
            DictName = "删除审计" + code, DictCode = code, Status = 1
        });

        await dictRepo.DeleteWhereAsync(x => x.DictCode == code);

        var logs = await changeRepo.GetListAsync(x => x.TableName == "SysDictType" && x.RecordId == dict.Id.ToString());
        var last = logs.OrderByDescending(l => l.Id).First();
        Assert.Contains("IsDeleted", last.Changes); // 软删以 diff 形态留痕
    }


;

        // 路径 A：批量 UpdateWhere（审计路径）
        // A 已注释

        // 路径 B：单实体 UpdateAsync（审计路径）
        def.Status = 1;
        await repo.UpdateAsync(def);
    }

}
