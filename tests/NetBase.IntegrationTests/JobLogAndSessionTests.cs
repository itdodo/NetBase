using Microsoft.Extensions.DependencyInjection;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using NetBase.Service.Sys;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// 定时任务执行日志（sys_job_log）与批量踢线集成测试：走真实 PostgreSQL。
/// </summary>
[Collection("Integration")]
public class JobLogAndSessionTests
{
    private readonly IntegrationFixture _fixture;

    public JobLogAndSessionTests(IntegrationFixture fixture) => _fixture = fixture;

    private IRepository<SysJobLog> JobLogRepo => _fixture.GetRepository<SysJobLog>();
    private IRepository<SysUserSession> SessionRepo => _fixture.GetRepository<SysUserSession>();
    private ISysAuthService Auth => _fixture.GetService<ISysAuthService>();
    private ISysUserService UserService => _fixture.GetService<ISysUserService>();

    private string Uid() => IntegrationFixture.Uid("jl");

    // ---------- 作业执行日志 ----------

    [Fact]
    public async Task JobLog_InsertAndPageQuery_ShouldFilterBySuccess()
    {
        var uid = Uid();
        var ok = await JobLogRepo.InsertAsync(new SysJobLog
        {
            JobId = uid + ".ok", JobName = "成功示例", Success = true, DurationMs = 120, TriggerType = "scheduled"
        });
        var fail = await JobLogRepo.InsertAsync(new SysJobLog
        {
            JobId = uid + ".fail", JobName = "失败示例", Success = false, DurationMs = 30,
            Error = "boom", TriggerType = "manual"
        });

        // 按结果筛选（服务层同款谓词）
        var onlyFailed = await JobLogRepo.GetPageListAsync(
            SqlSugar.Expressionable.Create<SysJobLog>().And(x => x.Success == false).ToExpression(),
            new PageQuery { PageIndex = 1, PageSize = 50 });
        Assert.Contains(onlyFailed.Items, x => x.Id == fail.Id);
        Assert.DoesNotContain(onlyFailed.Items, x => x.Id == ok.Id);

        // 清理（保留期语义：删 CreateTime < 阈值）
        var removed = await JobLogRepo.DeletePhysicalWhereAsync(x => x.CreateTime < DateTime.Now.AddMinutes(1));
        Assert.True(removed >= 2);
        Assert.Null(await JobLogRepo.GetByIdAsync(ok.Id));
        Assert.Null(await JobLogRepo.GetByIdAsync(fail.Id));
    }

    // ---------- 批量踢线 ----------

    private async Task<long> CreateUserAsync(string uid)
    {
        return await UserService.CreateAsync(new UserCreateDto
        {
            UserName = uid, NickName = "批量踢线测试", Password = "It123456", Status = 1, DeptId = await EnsureDeptAsync()
        }, "it-operator");
    }

    private async Task<long> EnsureDeptAsync()
    {
        var deptRepo = _fixture.GetRepository<SysDept>();
        var dept = await deptRepo.GetFirstAsync(x => x.DeptCode == "it_jl_dept");
        if (dept != null) return dept.Id;
        return (await deptRepo.InsertAsync(
            new SysDept { ParentId = 0, DeptName = "批量踢线测试部", DeptCode = "it_jl_dept", Status = 1 })).Id;
    }

    private static SysUserSession NewSession(long userId, string tokenId) => new()
    {
        UserId = userId,
        TokenId = tokenId,
        RefreshTokenHash = "hash-" + tokenId,
        LoginTime = DateTime.Now,
        ExpireTime = DateTime.Now.AddDays(1)
    };

    [Fact]
    public async Task BatchKick_ShouldDeleteTargetsAndExcludeCurrentSession()
    {
        var uid = Uid();
        var userId = await CreateUserAsync(uid);
        var otherId = await CreateUserAsync(uid + "_o");

        var s1 = await SessionRepo.InsertAsync(NewSession(userId, uid + "-tok1"));
        var s2 = await SessionRepo.InsertAsync(NewSession(userId, uid + "-tok2"));
        var s3 = await SessionRepo.InsertAsync(NewSession(otherId, uid + "-tok3"));
        // 操作者自己的会话（TokenId 对应 currentTokenId）
        var own = await SessionRepo.InsertAsync(NewSession(otherId, uid + "-own"));

        // 批量踢 s1/s2/own，currentTokenId 指向 own → own 必须被排除
        var kicked = await Auth.KickSessionsAsync([s1.Id, s2.Id, own.Id], uid + "-own");

        Assert.Equal(2, kicked);
        Assert.Null(await SessionRepo.GetByIdAsync(s1.Id));
        Assert.Null(await SessionRepo.GetByIdAsync(s2.Id));
        Assert.NotNull(await SessionRepo.GetByIdAsync(own.Id));

        // 收尾：清理残余会话
        await SessionRepo.DeletePhysicalWhereAsync(x => x.UserId == userId || x.UserId == otherId);
    }

    [Fact]
    public async Task BatchKick_UnknownSessions_ShouldBeSkipped()
    {
        var uid = Uid();
        var userId = await CreateUserAsync(uid);
        var s1 = await SessionRepo.InsertAsync(NewSession(userId, uid + "-tokA"));

        var kicked = await Auth.KickSessionsAsync([s1.Id, 999999999999L, 888888888888L], null);

        Assert.Equal(1, kicked);
        Assert.Null(await SessionRepo.GetByIdAsync(s1.Id));
    }
}
