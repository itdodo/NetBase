using Microsoft.Extensions.DependencyInjection;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Model.Enums;
using NetBase.Repository.Repositories;
using NetBase.Service.Sys;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// 日志类数据范围集成测试：登录/操作日志查询按查看者数据范围过滤
/// （仅本人=只看自己；FilterAll=全部），与角色配置的数据权限档位一致。
/// </summary>
[Collection("Integration")]
public class LogScopeTests
{
    private readonly IntegrationFixture _fixture;

    public LogScopeTests(IntegrationFixture fixture) => _fixture = fixture;

    private ISysLogService LogService => _fixture.GetService<ISysLogService>();
    private IRepository<SysLoginLog> LoginLogRepo => _fixture.GetRepository<SysLoginLog>();
    private IRepository<SysOperationLog> OperationLogRepo => _fixture.GetRepository<SysOperationLog>();
    private IRepository<SysRole> RoleRepo => _fixture.GetRepository<SysRole>();
    private IRepository<SysUserRole> UserRoleRepo => _fixture.GetRepository<SysUserRole>();
    private ISysUserService UserService => _fixture.GetService<ISysUserService>();

    private string Uid() => IntegrationFixture.Uid("logscope");

    private async Task<long> EnsureDeptAsync(string code)
    {
        var deptRepo = _fixture.GetRepository<SysDept>();
        var dept = await deptRepo.GetFirstAsync(x => x.DeptCode == code);
        if (dept != null) return dept.Id;
        return (await deptRepo.InsertAsync(
            new SysDept { ParentId = 0, DeptName = "日志范围测试部-" + code, DeptCode = code, Status = 1 })).Id;
    }

    /// <summary>建用户并挂到指定数据范围的新角色上</summary>
    private async Task<long> CreateUserWithScopeAsync(string uid, long deptId, DataScopeEnum scope)
    {
        var userId = await UserService.CreateAsync(new UserCreateDto
        {
            UserName = uid, NickName = "日志范围-" + uid, Password = "It123456", Status = 1, DeptId = deptId
        }, "it-operator");

        var role = await RoleRepo.InsertAsync(new SysRole
        {
            RoleName = "范围测试-" + uid, RoleCode = "scope_" + uid, DataScope = (int)scope, Status = 1
        });
        await UserRoleRepo.InsertAsync(new SysUserRole { UserId = userId, RoleId = role.Id });
        return userId;
    }

    private static SysLoginLog NewLoginLog(long userId, string userName) => new()
    {
        UserId = userId, UserName = userName, Success = true, Message = "登录成功", Ip = "127.0.0.1"
    };

    [Fact]
    public async Task LoginLog_SelfScope_ShouldOnlySeeOwn()
    {
        var uid = Uid();
        var deptA = await EnsureDeptAsync("logscope_a_" + uid);
        var deptB = await EnsureDeptAsync("logscope_b_" + uid);

        var viewerId = await CreateUserWithScopeAsync(uid + "_v", deptA, DataScopeEnum.Self);
        var otherId = await CreateUserWithScopeAsync(uid + "_o", deptB, DataScopeEnum.Self);

        await LoginLogRepo.InsertAsync(NewLoginLog(viewerId, uid + "_v"));
        await LoginLogRepo.InsertAsync(NewLoginLog(otherId, uid + "_o"));

        // 仅本人：只看自己
        var page = await LogService.GetLoginLogPageAsync(
            new LogQueryDto { PageIndex = 1, PageSize = 50 }, viewerId);
        Assert.All(page.Items, x => Assert.Equal(viewerId, x.UserId));
        Assert.Contains(page.Items, x => x.UserId == viewerId && x.UserName == uid + "_v");
        Assert.DoesNotContain(page.Items, x => x.UserId == otherId);

        // FilterAll（全部数据角色）：不受限
        var allViewerId = await CreateUserWithScopeAsync(uid + "_all", deptA, DataScopeEnum.All);
        var all = await LogService.GetLoginLogPageAsync(
            new LogQueryDto { PageIndex = 1, PageSize = 50 }, allViewerId);
        Assert.Contains(all.Items, x => x.UserId == viewerId);
        Assert.Contains(all.Items, x => x.UserId == otherId);

        // 收尾
        await LoginLogRepo.DeletePhysicalWhereAsync(x => x.UserId == viewerId || x.UserId == otherId);
    }

    [Fact]
    public async Task OperationLog_SelfScope_ShouldOnlySeeOwn()
    {
        var uid = Uid();
        var deptA = await EnsureDeptAsync("logscope_op_" + uid);

        var viewerId = await CreateUserWithScopeAsync(uid + "_v", deptA, DataScopeEnum.Self);
        var otherId = await CreateUserWithScopeAsync(uid + "_o", deptA, DataScopeEnum.Self);

        await OperationLogRepo.InsertAsync(new SysOperationLog { UserId = viewerId, UserName = uid + "_v", Module = "logscope", Action = "view", Success = true, Params = "-" });
        await OperationLogRepo.InsertAsync(new SysOperationLog { UserId = otherId, UserName = uid + "_o", Module = "logscope", Action = "view", Success = true, Params = "-" });

        var page = await LogService.GetOperationLogPageAsync(
            new LogQueryDto { PageIndex = 1, PageSize = 50 }, viewerId);
        Assert.All(page.Items.Where(x => x.Module == "logscope"), x => Assert.Equal(viewerId, x.UserId));
        Assert.DoesNotContain(page.Items, x => x.UserId == otherId);

        await OperationLogRepo.DeletePhysicalWhereAsync(x => x.UserId == viewerId || x.UserId == otherId);
    }
}
