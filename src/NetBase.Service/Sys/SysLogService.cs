using Microsoft.Extensions.Logging;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using Mapster;
using SqlSugar;
using System.Linq.Expressions;

namespace NetBase.Service.Sys;

/// <summary>日志服务实现：记录失败仅记运行日志，不抛出（日志写入不阻断业务）。
/// 查询/导出按查看者数据范围过滤（FilterAll 不限 / 仅本人=自己 / 部门档=可见部门操作人∪本人）。</summary>
public class SysLogService(
    IRepository<SysOperationLog> operationLogRepository,
    IRepository<SysLoginLog> loginLogRepository,
    IRepository<SysChangeLog> changeLogRepository,
    IDataScopeService dataScopeService,
    IRepository<SysUser> userRepository,
    ILogger<SysLogService> logger) : ISysLogService
{
    /// <summary>清理指定日期前的日志（物理删除）</summary>
    public Task<int> CleanupOperationLogsAsync(DateTime before) =>
        operationLogRepository.DeletePhysicalWhereAsync(x => x.CreateTime < before);

    /// <summary>清理指定日期前的登录日志（物理删除）</summary>
    public Task<int> CleanupLoginLogsAsync(DateTime before) =>
        loginLogRepository.DeletePhysicalWhereAsync(x => x.CreateTime < before);

    /// <summary>清理指定日期前的变更日志（物理删除）</summary>
    public Task<int> CleanupChangeLogsAsync(DateTime before) =>
        changeLogRepository.DeletePhysicalWhereAsync(x => x.CreateTime < before);

    public async Task RecordOperationAsync(SysOperationLog log)
    {
        try
        {
            await operationLogRepository.InsertAsync(log);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "操作日志写入失败: {Module}/{Action}", log.Module, log.Action);
        }
    }

    public async Task RecordLoginAsync(SysLoginLog log)
    {
        try
        {
            await loginLogRepository.InsertAsync(log);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "登录日志写入失败: {UserName}", log.UserName);
        }
    }

    /// <summary>操作日志导出（全量，条件同分页；不走分页以免上限截断）</summary>
    /// <summary>操作日志导出（条件同分页，受查看者数据范围约束）</summary>
    public async Task<List<OperationLogDto>> GetOperationLogExportAsync(LogQueryDto query, long? viewerUserId = null)
    {
        var scope = await BuildOperationScopeAsync(viewerUserId);
        var logs = await operationLogRepository.GetListAsync(BuildOperationPredicate(query, scope));
        return logs.Select(ToOperationDto).ToList();
    }

    /// <summary>登录日志导出（条件同分页，受查看者数据范围约束）</summary>
    public async Task<List<LoginLogDto>> GetLoginLogExportAsync(LogQueryDto query, long? viewerUserId = null)
    {
        var scope = await BuildLoginScopeAsync(viewerUserId);
        var logs = await loginLogRepository.GetListAsync(BuildLoginPredicate(query, scope));
        return logs.Select(ToLoginDto).ToList();
    }

    public async Task<PageResult<OperationLogDto>> GetOperationLogPageAsync(LogQueryDto query, long? viewerUserId = null)
    {
        var scope = await BuildOperationScopeAsync(viewerUserId);
        var page = await operationLogRepository.GetPageListAsync(BuildOperationPredicate(query, scope), query);
        var items = page.Items.Select(ToOperationDto).ToList();
        return PageResult<OperationLogDto>.Of(items, page.Total, page.PageIndex, page.PageSize);
    }

    private Expression<Func<SysOperationLog, bool>>? BuildOperationPredicate(LogQueryDto query, Expression<Func<SysOperationLog, bool>>? scope)
    {
        var keyword = query.Keyword?.Trim();
        var success = query.Success.HasValue ? query.Success.Value == 1 : (bool?)null;
        var action = query.Action?.Trim();
        return Expressionable.Create<SysOperationLog>()
            .AndIF(scope != null, scope!)
            .AndIF(action.IsNotNullOrEmpty(), x => x.Action == action)
            .AndIF(keyword.IsNotNullOrEmpty(), x => (x.UserName != null && x.UserName.Contains(keyword!))
                                                   || (x.Module != null && x.Module.Contains(keyword!))
                                                   || (x.Action != null && x.Action.Contains(keyword!)))
            .AndIF(success.HasValue, x => x.Success == success!.Value)
            .AndIF(query.BeginTime.HasValue, x => x.CreateTime >= query.BeginTime!.Value)
            .AndIF(query.EndTime.HasValue, x => x.CreateTime <= query.EndTime!.Value)
            .ToExpression();
    }

    private static OperationLogDto ToOperationDto(SysOperationLog x) => x.Adapt<OperationLogDto>();

    public async Task<PageResult<LoginLogDto>> GetLoginLogPageAsync(LogQueryDto query, long? viewerUserId = null)
    {
        var scope = await BuildLoginScopeAsync(viewerUserId);
        var page = await loginLogRepository.GetPageListAsync(BuildLoginPredicate(query, scope), query);
        var items = page.Items.Select(ToLoginDto).ToList();
        return PageResult<LoginLogDto>.Of(items, page.Total, page.PageIndex, page.PageSize);
    }

    private Expression<Func<SysLoginLog, bool>>? BuildLoginPredicate(LogQueryDto query, Expression<Func<SysLoginLog, bool>>? scope)
    {
        var keyword = query.Keyword?.Trim();
        var success = query.Success.HasValue ? query.Success.Value == 1 : (bool?)null;
        return Expressionable.Create<SysLoginLog>()
            .AndIF(scope != null, scope!)
            .AndIF(keyword.IsNotNullOrEmpty(), x => x.UserName.Contains(keyword!)
                                                   || (x.Message != null && x.Message.Contains(keyword!))
                                                   || (x.Ip != null && x.Ip.Contains(keyword!)))
            .AndIF(success.HasValue, x => x.Success == success!.Value)
            .AndIF(query.BeginTime.HasValue, x => x.CreateTime >= query.BeginTime!.Value)
            .AndIF(query.EndTime.HasValue, x => x.CreateTime <= query.EndTime!.Value)
            .ToExpression();
    }

    private static LoginLogDto ToLoginDto(SysLoginLog x) => x.Adapt<LoginLogDto>();

    public async Task<PageResult<ChangeLogDto>> GetChangeLogPageAsync(ChangeLogQueryDto query, long? viewerUserId = null)
    {
        var scope = await BuildChangeScopeAsync(viewerUserId);
        var page = await changeLogRepository.GetPageListAsync(BuildChangePredicate(query, scope), query);
        var items = page.Items.Select(ToChangeDto).ToList();
        return PageResult<ChangeLogDto>.Of(items, page.Total, page.PageIndex, page.PageSize);
    }

    /// <summary>
    /// 日志类数据范围谓词（查看者维度）：FilterAll 不限；仅本人=操作人是自己；
    /// 部门档=可见部门内的操作人 ∪ 本人。日志表无 DeptId 列，按操作人所属部门展开。
    /// </summary>
    private async Task<Expression<Func<SysOperationLog, bool>>?> BuildOperationScopeAsync(long? viewerUserId) =>
        await BuildScopeAsync(viewerUserId,
            self => (Expression<Func<SysOperationLog, bool>>)(x => x.UserId == self),
            ids => (Expression<Func<SysOperationLog, bool>>)(x => ids.Contains(x.UserId)));

    private async Task<Expression<Func<SysLoginLog, bool>>?> BuildLoginScopeAsync(long? viewerUserId) =>
        await BuildScopeAsync(viewerUserId,
            self => (Expression<Func<SysLoginLog, bool>>)(x => x.UserId == self),
            ids => (Expression<Func<SysLoginLog, bool>>)(x => ids.Contains(x.UserId)));

    private async Task<Expression<Func<SysChangeLog, bool>>?> BuildChangeScopeAsync(long? viewerUserId) =>
        await BuildScopeAsync(viewerUserId,
            self => (Expression<Func<SysChangeLog, bool>>)(x => x.UserId == self),
            ids => (Expression<Func<SysChangeLog, bool>>)(x => ids.Contains(x.UserId)));

    /// <summary>数据范围 → 谓词的公共骨架（三日志同构，避免三份重复逻辑）</summary>
    private async Task<Expression<Func<T, bool>>?> BuildScopeAsync<T>(
        long? viewerUserId,
        Func<long, Expression<Func<T, bool>>> selfPredicate,
        Func<List<long>, Expression<Func<T, bool>>> visiblePredicate)
    {
        if (viewerUserId is not > 0)
        {
            return null;
        }
        var scope = await dataScopeService.GetDataScopeAsync(viewerUserId.Value);
        if (scope.FilterAll)
        {
            return null;
        }
        if (!scope.HasDeptCondition)
        {
            return selfPredicate(viewerUserId.Value);
        }
        return visiblePredicate(await GetVisibleUserIdsAsync(viewerUserId.Value, scope.DeptIds));
    }

    /// <summary>可见部门内的用户 ID 集合 ∪ 本人</summary>
    private async Task<List<long>> GetVisibleUserIdsAsync(long selfId, HashSet<long> deptIds)
    {
        var users = await userRepository.GetListAsync(u => deptIds.Contains(u.DeptId));
        var ids = users.Select(u => u.Id).ToList();
        ids.Add(selfId);
        return ids;
    }

    private Expression<Func<SysChangeLog, bool>>? BuildChangePredicate(ChangeLogQueryDto query, Expression<Func<SysChangeLog, bool>>? scope)
    {
        var tableName = query.TableName?.Trim();
        var userName = query.UserName?.Trim();
        return Expressionable.Create<SysChangeLog>()
            .AndIF(scope != null, scope!)
            .AndIF(tableName.IsNotNullOrEmpty(), x => x.TableName.Contains(tableName!))
            .AndIF(userName.IsNotNullOrEmpty(), x => x.UserName != null && x.UserName.Contains(userName!))
            .AndIF(query.BeginTime.HasValue, x => x.CreateTime >= query.BeginTime!.Value)
            .AndIF(query.EndTime.HasValue, x => x.CreateTime <= query.EndTime!.Value)
            .ToExpression();
    }

    private static ChangeLogDto ToChangeDto(SysChangeLog x) => x.Adapt<ChangeLogDto>();
}
