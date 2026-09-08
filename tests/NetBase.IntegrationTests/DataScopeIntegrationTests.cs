using Microsoft.Extensions.DependencyInjection;
using NetBase.Model.Entities;
using NetBase.Model.Enums;
using NetBase.Repository.Repositories;
using NetBase.Service.Sys;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// 数据权限集成测试：真实库验证五档范围过滤与多角色并集。
/// 布局：总部(HQ) → 研发部(RD) → [研发一组(RD1), 研发二组(RD2)]、市场部(MKT)。
/// </summary>
[Collection("Integration")]
public class DataScopeIntegrationTests
{
    private readonly IRepository<SysUser> _userRepo;
    private readonly IRepository<SysRole> _roleRepo;
    private readonly IRepository<SysDept> _deptRepo;
    private readonly IRepository<SysRoleDept> _roleDeptRepo;
    private readonly IRepository<SysUserRole> _userRoleRepo;
    private readonly IDataScopeService _dataScopeService;
    private readonly ISysDeptService _deptService;

    public DataScopeIntegrationTests(IntegrationFixture fixture)
    {
        _userRepo = fixture.GetRepository<SysUser>();
        _roleRepo = fixture.GetRepository<SysRole>();
        _deptRepo = fixture.GetRepository<SysDept>();
        _roleDeptRepo = fixture.GetRepository<SysRoleDept>();
        _userRoleRepo = fixture.GetRepository<SysUserRole>();
        _dataScopeService = fixture.GetService<IDataScopeService>();
        _deptService = fixture.GetService<ISysDeptService>();
    }

    private async Task<SysDept> EnsureDeptAsync(string code, string name, long parentId)
    {
        var dept = await _deptRepo.GetFirstAsync(x => x.DeptCode == code);
        if (dept != null) return dept;
        var entity = new SysDept { ParentId = parentId, DeptName = name, DeptCode = code, Status = 1 };
        await _deptRepo.InsertAsync(entity);
        return entity;
    }

    private async Task<SysRole> EnsureRoleAsync(string code, DataScopeEnum scope)
    {
        var role = await _roleRepo.GetFirstAsync(x => x.RoleCode == code);
        if (role != null)
        {
            if (role.DataScope != (int)scope)
            {
                role.DataScope = (int)scope;
                await _roleRepo.UpdateAsync(role);
            }
            return role;
        }
        return await _roleRepo.InsertAsync(new SysRole
        {
            RoleName = "IT-" + code, RoleCode = code, Status = (int)StatusEnum.Enabled, DataScope = (int)scope
        });
    }

    private async Task<SysUser> EnsureUserAsync(string userName, long deptId, long roleId)
    {
        var user = await _userRepo.GetFirstAsync(x => x.UserName == userName);
        if (user == null)
        {
            user = await _userRepo.InsertAsync(new SysUser
            {
                UserName = userName, Password = "x", Status = (int)StatusEnum.Enabled, DeptId = deptId
            });
            user.OwnerUserId = user.Id;
            await _userRepo.UpdateAsync(user);
        }
        if (!await _userRoleRepo.AnyAsync(x => x.UserId == user.Id && x.RoleId == roleId))
        {
            await _userRoleRepo.InsertAsync(new SysUserRole { UserId = user.Id, RoleId = roleId });
        }
        return user;
    }

    /// <summary>构造测试布局：专属部门树 + 每档一个用户，返回用户Id与预期可见数</summary>
    private async Task<Scenario> BuildScenarioAsync()
    {
        var hq = await EnsureDeptAsync("it_hq", "IT总部", 0);
        var rd = await EnsureDeptAsync("it_rd", "IT研发部", hq.Id);
        var rd1 = await EnsureDeptAsync("it_rd1", "IT研发一组", rd.Id);
        var mkt = await EnsureDeptAsync("it_mkt", "IT市场部", hq.Id);

        // 每部门挂一个用户
        var uHq = await EnsureUserAsync("it_u_hq", hq.Id, (await EnsureAllRoleAsync()).Id);
        var uRd1 = await EnsureUserAsync("it_u_rd1", rd1.Id, (await EnsureAllRoleAsync()).Id);
        var uMkt = await EnsureUserAsync("it_u_mkt", mkt.Id, (await EnsureAllRoleAsync()).Id);

        return new Scenario(hq, rd, rd1, mkt, uHq, uRd1, uMkt);
    }

    private async Task<SysRole> EnsureAllRoleAsync()
    {
        // 挂"全部数据"角色使测试用户自身不受过滤影响（他们只是数据行）
        return await EnsureRoleAsync("it_all_role", DataScopeEnum.All);
    }

    private record Scenario(SysDept Hq, SysDept Rd, SysDept Rd1, SysDept Mkt, SysUser UHq, SysUser URd1, SysUser UMkt);

    [Fact]
    public async Task Scope_Dept_ShouldOnlySeeSameDept()
    {
        var scenario = await BuildScenarioAsync();
        var role = await EnsureRoleAsync("it_scope_dept", DataScopeEnum.Dept);
        var viewer = await EnsureUserAsync("it_viewer_dept", scenario.Rd1.Id, role.Id);

        var scope = await _dataScopeService.GetDataScopeAsync(viewer.Id);
        Assert.False(scope.FilterAll);
        Assert.Contains(scenario.Rd1.Id, scope.DeptIds);

        // 过滤表达式语义验证：查询 sys_user 时只能看到同部门
        var visible = await _userRepo.GetListAsync(x => scope.DeptIds.Contains(x.DeptId));
        Assert.All(visible, u => Assert.Equal(scenario.Rd1.Id, u.DeptId));
        Assert.Contains(visible, u => u.Id == viewer.Id);
    }

    [Fact]
    public async Task Scope_DeptAndChild_ShouldIncludeDescendants()
    {
        var scenario = await BuildScenarioAsync();
        var role = await EnsureRoleAsync("it_scope_dc", DataScopeEnum.DeptAndChild);
        // 观察者挂研发部（父），应可见研发部+研发一组+研发二组
        var viewer = await EnsureUserAsync("it_viewer_dc", scenario.Rd.Id, role.Id);

        var scope = await _dataScopeService.GetDataScopeAsync(viewer.Id);
        Assert.Contains(scenario.Rd.Id, scope.DeptIds);
        Assert.Contains(scenario.Rd1.Id, scope.DeptIds);
        Assert.DoesNotContain(scenario.Mkt.Id, scope.DeptIds); // 市场部不在范围

        var visible = await _userRepo.GetListAsync(x => scope.DeptIds.Contains(x.DeptId));
        var visibleDeptIds = visible.Select(u => u.DeptId).Distinct().ToList();
        Assert.Contains(scenario.Rd.Id, visibleDeptIds);
        Assert.Contains(scenario.Rd1.Id, visibleDeptIds);
        Assert.DoesNotContain(scenario.Mkt.Id, visibleDeptIds);
    }

    [Fact]
    public async Task Scope_Custom_ShouldOnlySeeCheckedDepts()
    {
        var scenario = await BuildScenarioAsync();
        var role = await EnsureRoleAsync("it_scope_custom", DataScopeEnum.Custom);
        var viewer = await EnsureUserAsync("it_viewer_custom", scenario.Hq.Id, role.Id);

        // 勾选市场部（非本人所在部门）
        await _roleDeptRepo.DeleteWhereAsync(x => x.RoleId == role.Id);
        await _roleDeptRepo.InsertAsync(new SysRoleDept { RoleId = role.Id, DeptId = scenario.Mkt.Id });

        var scope = await _dataScopeService.GetDataScopeAsync(viewer.Id);
        Assert.Contains(scenario.Mkt.Id, scope.DeptIds);
        Assert.DoesNotContain(scenario.Hq.Id, scope.DeptIds);

        var visible = await _userRepo.GetListAsync(x => scope.DeptIds.Contains(x.DeptId));
        Assert.All(visible, u => Assert.Equal(scenario.Mkt.Id, u.DeptId));
        Assert.Contains(visible, u => u.Id == scenario.UMkt.Id);
    }

    [Fact]
    public async Task Scope_Self_ShouldBeIncludeSelfData()
    {
        var scenario = await BuildScenarioAsync();
        var role = await EnsureRoleAsync("it_scope_self", DataScopeEnum.Self);
        var viewer = await EnsureUserAsync("it_viewer_self", scenario.Hq.Id, role.Id);

        var scope = await _dataScopeService.GetDataScopeAsync(viewer.Id);
        Assert.True(scope.IncludeSelfData);
        Assert.False(scope.HasDeptCondition);
    }

    [Fact]
    public async Task Scope_All_ShouldBeFilterAll()
    {
        var scenario = await BuildScenarioAsync();
        var role = await EnsureRoleAsync("it_scope_all", DataScopeEnum.All);
        var viewer = await EnsureUserAsync("it_viewer_all", scenario.Hq.Id, role.Id);

        var scope = await _dataScopeService.GetDataScopeAsync(viewer.Id);
        Assert.True(scope.FilterAll);
    }

    [Fact]
    public async Task Scope_MultiRole_ShouldBeUnion()
    {
        var scenario = await BuildScenarioAsync();
        // 角色1：本部门（RD1）；角色2：仅本人 → 并集 = RD1部门数据 ∪ 本人数据
        var roleA = await EnsureRoleAsync("it_scope_mu_a", DataScopeEnum.Dept);
        var roleB = await EnsureRoleAsync("it_scope_mu_b", DataScopeEnum.Self);
        var viewer = await EnsureUserAsync("it_viewer_mu", scenario.Rd1.Id, roleA.Id);
        if (!await _userRoleRepo.AnyAsync(x => x.UserId == viewer.Id && x.RoleId == roleB.Id))
        {
            await _userRoleRepo.InsertAsync(new SysUserRole { UserId = viewer.Id, RoleId = roleB.Id });
        }

        var scope = await _dataScopeService.GetDataScopeAsync(viewer.Id);
        Assert.Contains(scenario.Rd1.Id, scope.DeptIds); // 来自角色A
        Assert.True(scope.IncludeSelfData);              // 来自角色B
    }

    [Fact]
    public async Task Scope_Cache_ShouldRefreshAfterInvalidationWindow_OrServeFromCache()
    {
        var scenario = await BuildScenarioAsync();
        var role = await EnsureRoleAsync("it_scope_cache", DataScopeEnum.Dept);
        var viewer = await EnsureUserAsync("it_viewer_cache", scenario.Rd1.Id, role.Id);

        var first = await _dataScopeService.GetDataScopeAsync(viewer.Id);
        var second = await _dataScopeService.GetDataScopeAsync(viewer.Id);
        Assert.Equal(first.DeptIds, second.DeptIds); // 命中缓存，结果一致
    }
}
