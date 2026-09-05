using Microsoft.Extensions.Logging;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using SqlSugar;
using System.Linq.Expressions;

namespace NetBase.Service.Sys;

/// <summary>日志服务实现：记录失败仅记运行日志，不抛出（日志写入不阻断业务）</summary>
public class SysLogService(
    IRepository<SysOperationLog> operationLogRepository,
    IRepository<SysLoginLog> loginLogRepository,
    ILogger<SysLogService> logger) : ISysLogService
{
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

    public async Task<PageResult<OperationLogDto>> GetOperationLogPageAsync(LogQueryDto query)
    {
        var hasCondition = false;
        var exp = Expressionable.Create<SysOperationLog>();
        if (query.Keyword.IsNotNullOrEmpty())
        {
            hasCondition = true;
            var keyword = query.Keyword!.Trim();
            exp.And(x => (x.UserName != null && x.UserName.Contains(keyword))
                      || (x.Module != null && x.Module.Contains(keyword))
                      || (x.Action != null && x.Action.Contains(keyword)));
        }
        if (query.Success.HasValue)
        {
            hasCondition = true;
            var success = query.Success.Value == 1;
            exp.And(x => x.Success == success);
        }

        var page = await operationLogRepository.GetPageListAsync(hasCondition ? exp.ToExpression() : null, query);
        var items = page.Items.Select(x => new OperationLogDto
        {
            Id = x.Id,
            UserId = x.UserId,
            UserName = x.UserName,
            Module = x.Module,
            Action = x.Action,
            HttpMethod = x.HttpMethod,
            Path = x.Path,
            Params = x.Params,
            Success = x.Success,
            ErrorMessage = x.ErrorMessage,
            ElapsedMs = x.ElapsedMs,
            Ip = x.Ip,
            CreateTime = x.CreateTime
        }).ToList();
        return PageResult<OperationLogDto>.Of(items, page.Total, page.PageIndex, page.PageSize);
    }

    public async Task<PageResult<LoginLogDto>> GetLoginLogPageAsync(LogQueryDto query)
    {
        var hasCondition = false;
        var exp = Expressionable.Create<SysLoginLog>();
        if (query.Keyword.IsNotNullOrEmpty())
        {
            hasCondition = true;
            var keyword = query.Keyword!.Trim();
            exp.And(x => x.UserName.Contains(keyword)
                      || (x.Message != null && x.Message.Contains(keyword))
                      || (x.Ip != null && x.Ip.Contains(keyword)));
        }
        if (query.Success.HasValue)
        {
            hasCondition = true;
            var success = query.Success.Value == 1;
            exp.And(x => x.Success == success);
        }

        var page = await loginLogRepository.GetPageListAsync(hasCondition ? exp.ToExpression() : null, query);
        var items = page.Items.Select(x => new LoginLogDto
        {
            Id = x.Id,
            UserId = x.UserId,
            UserName = x.UserName,
            Success = x.Success,
            Message = x.Message,
            Ip = x.Ip,
            UserAgent = x.UserAgent,
            CreateTime = x.CreateTime
        }).ToList();
        return PageResult<LoginLogDto>.Of(items, page.Total, page.PageIndex, page.PageSize);
    }
}
