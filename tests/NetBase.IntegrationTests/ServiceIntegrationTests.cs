using Microsoft.Extensions.DependencyInjection;
using NetBase.Common.Cache;
using NetBase.Common.Exceptions;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using NetBase.Service.Sys;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// 服务层集成测试：审计 AOP、密码策略、权限码缓存失效、消息越权——走真实数据库与生产一致的 DI 组装。
/// </summary>
[Collection("Integration")]
public class ServiceIntegrationTests
{
    private readonly IntegrationFixture _fixture;

    public ServiceIntegrationTests(IntegrationFixture fixture) => _fixture = fixture;

    private ISysUserService UserService => _fixture.GetService<ISysUserService>();
    private IPermissionService PermissionService => _fixture.GetService<IPermissionService>();
    private ISysMessageService MessageService => _fixture.GetService<ISysMessageService>();
    private ISysLogService LogService => _fixture.GetService<ISysLogService>();
    private IRepository<SysRole> RoleRepo => _fixture.GetRepository<SysRole>();
    private IRepository<SysMenu> MenuRepo => _fixture.GetRepository<SysMenu>();
    private IRepository<SysRoleMenu> RoleMenuRepo => _fixture.GetRepository<SysRoleMenu>();

    private string Uid => IntegrationFixture.Uid("svc");

    private IRepository<SysDept> DeptRepo => _fixture.GetRepository<SysDept>();

    /// <summary>确保测试部门存在（用户服务要求 DeptId 有效）</summary>
    private async Task<long> EnsureDeptAsync()
    {
        var dept = await DeptRepo.GetFirstAsync(x => x.DeptCode == "it_svc_dept");
        if (dept != null) return dept.Id;
        var entity = await DeptRepo.InsertAsync(
            new SysDept { ParentId = 0, DeptName = "IT服务测试部", DeptCode = "it_svc_dept", Status = 1 });
        return entity.Id;
    }

    private async Task<long> CreateUserAsync(string? uid = null)
    {
        var deptId = await EnsureDeptAsync();
        var dto = new UserCreateDto
        {
            UserName = uid ?? Uid, NickName = "集成测试", Password = "It123456", Status = 1, DeptId = deptId
        };
        return await UserService.CreateAsync(dto, "it-operator");
    }

    [Fact]
    public async Task CreateUser_ShouldAuditFields_FilledByAop()
    {
        var id = await CreateUserAsync();

        var user = await _fixture.GetRepository<SysUser>().GetByIdAsync(id);
        Assert.NotNull(user);
        Assert.Equal("it-operator", user!.CreateBy); // 审计 AOP 自动填充操作人（实体层断言）
        Assert.NotEqual(default, user.CreateTime);

        await UserService.DeleteAsync(id, "it-test");
    }

    [Fact]
    public async Task CreateUser_WeakPassword_ShouldBeRejected()
    {
        var dto = new UserCreateDto { UserName = Uid, Password = "123456", Status = 1, DeptId = 0 };
        await Assert.ThrowsAsync<BusinessException>(async () => await UserService.CreateAsync(dto));
    }

    [Fact]
    public async Task ResetPassword_ShouldRejectWeakPassword()
    {
        var id = await CreateUserAsync();

        await Assert.ThrowsAsync<BusinessException>(
            async () => await UserService.ResetPasswordAsync(id, "123", "it-test"));

        await UserService.DeleteAsync(id, "it-test");
    }

    [Fact]
    public async Task PermissionCache_ShouldInvalidate_OnUserRoleChange()
    {
        var uid = Uid; // 用例内固定后缀，保证用户与权限码对应
        var userId = await CreateUserAsync(uid);
        var user = await _fixture.GetRepository<SysUser>().GetByIdAsync(userId);
        Assert.NotNull(user);

        Assert.Empty(await PermissionService.GetUserPermissionsAsync(user!.Id)); // 无角色无权限

        // 建角色+菜单权限码，绑定用户 → 缓存应随失效刷新出新权限
        var role = await RoleRepo.InsertAsync(new SysRole
        {
            RoleName = "IT-权限角色", RoleCode = Uid + "_role", Status = 1
        });
        var menu = await MenuRepo.InsertAsync(new SysMenu
        {
            ParentId = 0, MenuName = "IT测试菜单", MenuType = 3, Permission = "it:test:perm:" + uid, Status = 1
        });
        await UserService.AssignRolesAsync(user.Id, [role.Id]);
        RoleMenuRepo.Insert(new SysRoleMenu { RoleId = role.Id, MenuId = menu.Id });

        var perms = await PermissionService.GetUserPermissionsAsync(user.Id);
        Assert.Contains("it:test:perm:" + uid, perms);
    }

    [Fact]
    public async Task Message_ToNonExistentReceiver_ShouldBeRejected()
    {
        await Assert.ThrowsAsync<BusinessException>(
            async () => await MessageService.SendAsync(
                new MessageSendDto { ReceiverId = 999999999, Title = "t", Content = "c" }, "it-test"));
    }

    [Fact]
    public async Task Message_MarkRead_ByOtherUser_ShouldBeForbidden()
    {
        var ownerId = await CreateUserAsync();
        var msgId = await MessageService.SendAsync(
            new MessageSendDto { ReceiverId = ownerId, Title = "私密", Content = "x" }, "system");

        await Assert.ThrowsAsync<BusinessException>(
            async () => await MessageService.MarkReadAsync(ownerId + 1, msgId));

        await UserService.DeleteAsync(ownerId, "it-test");
    }

    [Fact]
    public async Task LoginLog_ShouldRecordAndCleanup()
    {
        await LogService.RecordLoginAsync(new SysLoginLog
        {
            UserName = Uid, Success = false, Message = "IT测试失败", Ip = "127.0.0.1"
        });

        var cleaned = await LogService.CleanupLoginLogsAsync(DateTime.Now.AddMinutes(1));
        Assert.True(cleaned >= 1);
    }
}
