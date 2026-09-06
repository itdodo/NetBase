using NetBase.Common.Users;
using SqlSugar;
using NetBase.Model.Entities;
using NetBase.Service.Sys;

namespace NetBase.Api.Middlewares;

/// <summary>
/// 数据权限中间件：登录请求按用户角色数据范围向 SqlSugar 当前连接注入
/// IDataScope 全局过滤器（业务实体实现接口即自动过滤，业务代码零侵入）。
/// 匿名请求显式清理过滤器，防止 scope 复用残留。
/// </summary>
public class DataScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ISqlSugarClient db, IDataScopeService dataScopeService, ICurrentUserService currentUser)
    {
        // 每请求清空（含备份）IDataScope 过滤器，防 scope 复用残留；随后按需注入
        db.QueryFilter.ClearAndBackup<IDataScope>();

        var userId = currentUser.UserId;
        if (userId.HasValue && userId.Value > 0)
        {
            var scope = await dataScopeService.GetDataScopeAsync(userId.Value);
            if (!scope.FilterAll)
            {
                var deptIds = scope.DeptIds;
                var includeSelf = scope.IncludeSelfData;
                var selfId = userId.Value;

                // 可见部门数据 ∪ 本人数据（多角色并集的两段式表达）
                // OwnerUserId 通过 MappingColumn 动态列引用（接口约定列，避免表达式引用被 IsIgnore 属性）
                if (scope.HasDeptCondition && includeSelf)
                {
                    db.QueryFilter.AddTableFilter<IDataScope>(x =>
                        deptIds.Contains(x.DeptId) || SqlFunc.MappingColumn(default(long), "OwnerUserId") == selfId);
                }
                else if (scope.HasDeptCondition)
                {
                    db.QueryFilter.AddTableFilter<IDataScope>(x => deptIds.Contains(x.DeptId));
                }
                else if (includeSelf)
                {
                    db.QueryFilter.AddTableFilter<IDataScope>(x =>
                        SqlFunc.MappingColumn(default(long), "OwnerUserId") == selfId);
                }
            }
        }

        await next(context);
    }
}
