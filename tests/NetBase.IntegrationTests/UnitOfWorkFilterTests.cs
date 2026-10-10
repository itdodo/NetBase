using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using NetBase.Api.Filters;
using NetBase.Model.Entities;
using SqlSugar;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// UnitOfWorkFilter 机制测试：[UnitOfWork] 写操作 Action 的事务包裹——
/// 正常返回提交、Action 抛异常回滚（ActionExecutedContext.Exception 模拟下游异常）。
/// </summary>
[Collection("Integration")]
public class UnitOfWorkFilterTests
{
    private readonly IntegrationFixture _fixture;

    public UnitOfWorkFilterTests(IntegrationFixture fixture) => _fixture = fixture;

    private static ActionExecutingContext BuildContext(string method)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        var actionContext = new ActionContext(http, new RouteData(), new ControllerActionDescriptor
        {
            EndpointMetadata = new List<object> { new UnitOfWorkAttribute() }
        });
        return new ActionExecutingContext(
            actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller: null);
    }

    private UnitOfWorkFilter CreateFilter() =>
        new(_fixture.GetService<ISqlSugarClient>(), NullLogger<UnitOfWorkFilter>.Instance);

    [Fact]
    public async Task UnitOfWork_Success_ShouldCommit()
    {
        var repo = _fixture.GetRepository<SysRole>();
        var code = IntegrationFixture.Uid("uowc");
        var roleId = 0L;

        var filter = CreateFilter();
        var context = BuildContext("POST");
        await filter.OnActionExecutionAsync(context, async () =>
        {
            roleId = (await repo.InsertAsync(new SysRole { RoleName = "UOW提交", RoleCode = code, Status = 1 })).Id;
            return new ActionExecutedContext(context, new List<IFilterMetadata>(), null);
        });

        Assert.NotNull(await repo.GetByIdAsync(roleId));
    }

    [Fact]
    public async Task UnitOfWork_ActionThrew_ShouldRollback()
    {
        var repo = _fixture.GetRepository<SysRole>();
        var code = IntegrationFixture.Uid("uowr");
        var roleId = 0L;

        var filter = CreateFilter();
        var context = BuildContext("POST");
        await filter.OnActionExecutionAsync(context, async () =>
        {
            roleId = (await repo.InsertAsync(new SysRole { RoleName = "UOW回滚", RoleCode = code, Status = 1 })).Id;
            return new ActionExecutedContext(context, new List<IFilterMetadata>(), null)
            {
                Exception = new InvalidOperationException("模拟业务异常")
            };
        });

        Assert.Null(await repo.GetByIdAsync(roleId));
    }

    [Fact]
    public async Task UnitOfWork_NonWriteMethod_ShouldNotWrapTransaction()
    {
        var repo = _fixture.GetRepository<SysRole>();
        var roleId = 0L;

        var filter = CreateFilter();
        var context = BuildContext("GET"); // 读操作不走事务包裹，Action 内写入即时生效
        await filter.OnActionExecutionAsync(context, async () =>
        {
            roleId = (await repo.InsertAsync(new SysRole
            {
                RoleName = "UOW直通", RoleCode = IntegrationFixture.Uid("uowg"), Status = 1
            })).Id;
            return new ActionExecutedContext(context, new List<IFilterMetadata>(), null);
        });

        Assert.NotNull(await repo.GetByIdAsync(roleId));
    }
}
