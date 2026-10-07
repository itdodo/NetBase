using Microsoft.Extensions.DependencyInjection;
using NetBase.Common.Exceptions;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Model.Enums;
using NetBase.Repository.Repositories;
using NetBase.Service.Sys;
using NetBase.Common.Results;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// 用户/角色/菜单三大模块守卫规则集成测试（接口矩阵的固化）：
/// 重名/重码拒绝、内置 admin 账号与角色删除保护、菜单子节点与引用保护、授权无效 ID 拒绝。
/// </summary>
[Collection("Integration")]
public class UserMenuRoleGuardsTests
{
    private readonly IntegrationFixture _fixture;

    public UserMenuRoleGuardsTests(IntegrationFixture fixture) => _fixture = fixture;

    private ISysUserService UserService => _fixture.GetService<ISysUserService>();
    private ISysRoleService RoleService => _fixture.GetService<ISysRoleService>();
    private ISysMenuService MenuService => _fixture.GetService<ISysMenuService>();
    private IRepository<SysUser> UserRepo => _fixture.GetRepository<SysUser>();
    private IRepository<SysRole> RoleRepo => _fixture.GetRepository<SysRole>();
    private IRepository<SysUserRole> UserRoleRepo => _fixture.GetRepository<SysUserRole>();

    private string Uid() => IntegrationFixture.Uid("guard");

    private async Task<long> CreateUserAsync(string userName)
    {
        return await UserService.CreateAsync(new UserCreateDto
        {
            UserName = userName, NickName = "守卫测试", Password = "It123456", Status = 1, DeptId = await EnsureDeptAsync()
        }, "guard-test");
    }

    private async Task<long> EnsureDeptAsync()
    {
        var deptRepo = _fixture.GetRepository<SysDept>();
        var dept = await deptRepo.GetFirstAsync(x => x.DeptCode == "guard_dept");
        if (dept != null) return dept.Id;
        return (await deptRepo.InsertAsync(
            new SysDept { ParentId = 0, DeptName = "守卫测试部", DeptCode = "guard_dept", Status = 1 })).Id;
    }

    private async Task<long> GetAdminUserIdAsync() => (await EnsureAdminAsync()).userId;

    private async Task<long> GetAdminRoleIdAsync() => (await EnsureAdminAsync()).roleId;

    /// <summary>测试库无生产种子：按需创建内置 admin 账号与 admin 角色（守卫按常量码判断）</summary>
    private async Task<(long userId, long roleId)> EnsureAdminAsync()
    {
        var role = await RoleRepo.GetFirstAsync(x => x.RoleCode == SysRoleService.AdminRoleCode);
        if (role == null)
        {
            role = await RoleRepo.InsertAsync(new SysRole
            { RoleName = "超级管理员", RoleCode = SysRoleService.AdminRoleCode, DataScope = (int)DataScopeEnum.All, Status = 1 });
        }
        var user = await UserRepo.GetFirstAsync(x => x.UserName == SysUserService.AdminUserName);
        if (user == null)
        {
            user = await UserRepo.InsertAsync(new SysUser
            {
                UserName = SysUserService.AdminUserName,
                Password = NetBase.Common.Security.PasswordHelper.Encrypt("Admin@12345"),
                NickName = "系统管理员", Status = (int)StatusEnum.Enabled, DeptId = await EnsureDeptAsync()
            });
            await UserRoleRepo.InsertAsync(new SysUserRole { UserId = user.Id, RoleId = role.Id });
        }
        return (user.Id, role.Id);
    }

    // ---------- 用户守卫 ----------

    [Fact]
    public async Task CreateUser_DuplicateName_ShouldReject()
    {
        var uid = Uid();
        var id = await CreateUserAsync(uid);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateUserAsync(uid));
        Assert.Equal(ErrorCodes.SYS_USER_NAME_EXISTS, ex.ErrorCode);
        await UserService.DeleteAsync(id, "guard-test");
    }

    [Fact]
    public async Task DeleteUser_AdminAccount_ShouldReject()
    {
        var adminId = await GetAdminUserIdAsync();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => UserService.DeleteAsync(adminId, "guard-test"));
        Assert.Equal(ErrorCodes.SYS_USER_ADMIN_DELETE_FORBIDDEN, ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateUser_DisableAdmin_ShouldReject()
    {
        var adminId = await GetAdminUserIdAsync();
        var admin = await UserRepo.GetByIdAsync(adminId);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => UserService.UpdateAsync(adminId,
            new UserUpdateDto { NickName = admin!.NickName, Status = (int)StatusEnum.Disabled }, "guard-test"));
        Assert.Equal(ErrorCodes.SYS_USER_ADMIN_DISABLE_FORBIDDEN, ex.ErrorCode);
    }

    // ---------- 角色守卫 ----------

    [Fact]
    public async Task CreateRole_DuplicateCode_ShouldReject()
    {
        var uid = Uid();
        var rid = await RoleService.CreateAsync(new RoleSaveDto
        { RoleName = "守卫-" + uid, RoleCode = "guard_" + uid, DataScope = (int)DataScopeEnum.Self, Status = 1 }, "guard-test");
        var ex = await Assert.ThrowsAsync<BusinessException>(() => RoleService.CreateAsync(new RoleSaveDto
        { RoleName = "守卫重码", RoleCode = "guard_" + uid, DataScope = (int)DataScopeEnum.Self, Status = 1 }, "guard-test"));
        Assert.Equal(ErrorCodes.SYS_ROLE_CODE_EXISTS, ex.ErrorCode);
        await RoleService.DeleteAsync(rid, "guard-test");
    }

    [Fact]
    public async Task DeleteRole_AdminRole_ShouldReject()
    {
        var adminRoleId = await GetAdminRoleIdAsync();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => RoleService.DeleteAsync(adminRoleId, "guard-test"));
        Assert.Equal(ErrorCodes.SYS_ROLE_ADMIN_DELETE_FORBIDDEN, ex.ErrorCode);
    }

    [Fact]
    public async Task AssignMenus_InvalidMenuId_ShouldReject()
    {
        var uid = Uid();
        var rid = await RoleService.CreateAsync(new RoleSaveDto
        { RoleName = "守卫授权-" + uid, RoleCode = "guardm_" + uid, DataScope = (int)DataScopeEnum.Self, Status = 1 }, "guard-test");
        var ex = await Assert.ThrowsAsync<BusinessException>(() => RoleService.AssignMenusAsync(rid, [999999999999L]));
        await RoleService.DeleteAsync(rid, "guard-test");
        Assert.NotEmpty(ex.Message);
    }

    // ---------- 菜单守卫 ----------

    [Fact]
    public async Task DeleteMenu_WithChildren_ShouldReject()
    {
        var uid = Uid();
        var dirId = await MenuService.CreateAsync(new MenuSaveDto
        { MenuName = "守卫目录-" + uid, MenuType = (int)MenuTypeEnum.Directory, Path = "/guard_" + uid, Sort = 99 }, "guard-test");
        var pageId = await MenuService.CreateAsync(new MenuSaveDto
        { MenuName = "守卫页", MenuType = (int)MenuTypeEnum.Menu, Path = "/guard_" + uid + "/p", Permission = "guard:" + uid + ":list", ParentId = dirId, Sort = 1 }, "guard-test");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => MenuService.DeleteAsync(dirId, "guard-test"));
        Assert.Equal(ErrorCodes.SYS_MENU_HAS_CHILDREN, ex.ErrorCode);

        await MenuService.DeleteAsync(pageId, "guard-test");
        await MenuService.DeleteAsync(dirId, "guard-test");
    }

    [Fact]
    public async Task DeleteMenu_ReferencedByRole_ShouldReject()
    {
        var uid = Uid();
        var pageId = await MenuService.CreateAsync(new MenuSaveDto
        { MenuName = "守卫引用页-" + uid, MenuType = (int)MenuTypeEnum.Menu, Path = "/guardref_" + uid, Permission = "guardref:" + uid + ":list", ParentId = 0, Sort = 99 }, "guard-test");
        var rid = await RoleService.CreateAsync(new RoleSaveDto
        { RoleName = "引用角色-" + uid, RoleCode = "guardref_" + uid, DataScope = (int)DataScopeEnum.Self, Status = 1 }, "guard-test");
        await RoleService.AssignMenusAsync(rid, [pageId]);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => MenuService.DeleteAsync(pageId, "guard-test"));
        Assert.Equal(ErrorCodes.SYS_MENU_IN_USE, ex.ErrorCode);

        await RoleService.DeleteAsync(rid, "guard-test");
        await MenuService.DeleteAsync(pageId, "guard-test");
    }
}
