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

/// <summary>日志服务实现：记录失败仅记运行日志，不抛出（日志写入不阻断业务）</summary>
public class SysLogService(
    IRepository<SysOperationLog> operationLogRepository,
    IRepository<SysLoginLog> loginLogRepository,
    IRepository<SysChangeLog> changeLogRepository,
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
    public async Task<List<OperationLogDto>> GetOperationLogExportAsync(LogQueryDto query)
    {
        var logs = await operationLogRepository.GetListAsync(BuildOperationPredicate(query));
        return logs.Select(ToOperationDto).ToList();
    }

    /// <summary>登录日志导出（全量，条件同分页）</summary>
    public async Task<List<LoginLogDto>> GetLoginLogExportAsync(LogQueryDto query)
    {
        var logs = await loginLogRepository.GetListAsync(BuildLoginPredicate(query));
        return logs.Select(ToLoginDto).ToList();
    }

    public async Task<PageResult<OperationLogDto>> GetOperationLogPageAsync(LogQueryDto query)
    {
        var page = await operationLogRepository.GetPageListAsync(BuildOperationPredicate(query), query);
        var items = page.Items.Select(ToOperationDto).ToList();
        return PageResult<OperationLogDto>.Of(items, page.Total, page.PageIndex, page.PageSize);
    }

    private Expression<Func<SysOperationLog, bool>>? BuildOperationPredicate(LogQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        var success = query.Success.HasValue ? query.Success.Value == 1 : (bool?)null;
        return Expressionable.Create<SysOperationLog>()
            .AndIF(keyword.IsNotNullOrEmpty(), x => (x.UserName != null && x.UserName.Contains(keyword!))
                                                   || (x.Module != null && x.Module.Contains(keyword!))
                                                   || (x.Action != null && x.Action.Contains(keyword!)))
            .AndIF(success.HasValue, x => x.Success == success!.Value)
            .AndIF(query.BeginTime.HasValue, x => x.CreateTime >= query.BeginTime!.Value)
            .AndIF(query.EndTime.HasValue, x => x.CreateTime <= query.EndTime!.Value)
            .ToExpression();
    }

    private static OperationLogDto ToOperationDto(SysOperationLog x) => x.Adapt<OperationLogDto>();

    public async Task<PageResult<LoginLogDto>> GetLoginLogPageAsync(LogQueryDto query)
    {
        var page = await loginLogRepository.GetPageListAsync(BuildLoginPredicate(query), query);
        var items = page.Items.Select(ToLoginDto).ToList();
        return PageResult<LoginLogDto>.Of(items, page.Total, page.PageIndex, page.PageSize);
    }

    private Expression<Func<SysLoginLog, bool>>? BuildLoginPredicate(LogQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        var success = query.Success.HasValue ? query.Success.Value == 1 : (bool?)null;
        return Expressionable.Create<SysLoginLog>()
            .AndIF(keyword.IsNotNullOrEmpty(), x => x.UserName.Contains(keyword!)
                                                   || (x.Message != null && x.Message.Contains(keyword!))
                                                   || (x.Ip != null && x.Ip.Contains(keyword!)))
            .AndIF(success.HasValue, x => x.Success == success!.Value)
            .AndIF(query.BeginTime.HasValue, x => x.CreateTime >= query.BeginTime!.Value)
            .AndIF(query.EndTime.HasValue, x => x.CreateTime <= query.EndTime!.Value)
            .ToExpression();
    }

    private static LoginLogDto ToLoginDto(SysLoginLog x) => x.Adapt<LoginLogDto>();

    public async Task<PageResult<ChangeLogDto>> GetChangeLogPageAsync(ChangeLogQueryDto query)
    {
        var page = await changeLogRepository.GetPageListAsync(BuildChangePredicate(query), query);
        var items = page.Items.Select(ToChangeDto).ToList();
        return PageResult<ChangeLogDto>.Of(items, page.Total, page.PageIndex, page.PageSize);
    }

    private static Expression<Func<SysChangeLog, bool>>? BuildChangePredicate(ChangeLogQueryDto query)
    {
        var tableName = query.TableName?.Trim();
        var userName = query.UserName?.Trim();
        return Expressionable.Create<SysChangeLog>()
            .AndIF(tableName.IsNotNullOrEmpty(), x => x.TableName.Contains(tableName!))
            .AndIF(userName.IsNotNullOrEmpty(), x => x.UserName != null && x.UserName.Contains(userName!))
            .AndIF(query.BeginTime.HasValue, x => x.CreateTime >= query.BeginTime!.Value)
            .AndIF(query.EndTime.HasValue, x => x.CreateTime <= query.EndTime!.Value)
            .ToExpression();
    }

    private static ChangeLogDto ToChangeDto(SysChangeLog x) => x.Adapt<ChangeLogDto>();
}
