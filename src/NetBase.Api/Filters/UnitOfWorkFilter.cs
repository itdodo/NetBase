using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NetBase.Common.Extensions;
using SqlSugar;

namespace NetBase.Api.Filters;

/// <summary>
/// 工作单元事务过滤器：标注 [UnitOfWork] 的 Action 在同一事务中执行，
/// 正常返回提交、任何异常/验证失败回滚。用于跨服务复杂写操作的一致性保障。
/// 注意：控制器构造的 ISqlSugarClient 为同一 Scoped 实例，事务覆盖 Action 内全部数据库操作。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class UnitOfWorkAttribute : Attribute
{
}

/// <summary>工作单元过滤器实现</summary>
public class UnitOfWorkFilter(ISqlSugarClient db, ILogger<UnitOfWorkFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.Request.Method is not ("POST" or "PUT" or "DELETE" or "PATCH") ||
            context.ActionDescriptor.EndpointMetadata.OfType<UnitOfWorkAttribute>().FirstOrDefault() == null)
        {
            await next();
            return;
        }

        db.Ado.BeginTran();
        var executed = await next();

        try
        {
            if (executed.Exception == null && !executed.Canceled)
            {
                db.Ado.CommitTran();
            }
            else
            {
                db.Ado.RollbackTran();
                logger.LogWarning("UnitOfWork 回滚: {Path}", context.HttpContext.Request.Path);
            }
        }
        catch (Exception ex)
        {
            db.Ado.RollbackTran();
            logger.LogError(ex, "UnitOfWork 提交失败已回滚: {Path}", context.HttpContext.Request.Path);
            throw;
        }
    }
}
