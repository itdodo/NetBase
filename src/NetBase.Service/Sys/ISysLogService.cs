using System.ComponentModel.DataAnnotations;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;

namespace NetBase.Service.Sys;

/// <summary>查询DTO</summary>
public class LogQueryDto : PageQuery
{
    /// <summary>用户名/模块关键字</summary>
    [StringLength(50)]
    public string? Keyword { get; set; }

    /// <summary>是否成功</summary>
    [Range(0, 1)]
    public int? Success { get; set; }

    /// <summary>开始时间</summary>
    public DateTime? BeginTime { get; set; }

    /// <summary>结束时间</summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>操作日志返回</summary>
public class OperationLogDto
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string? UserName { get; set; }

    public string? Module { get; set; }

    public string? Action { get; set; }

    public string? HttpMethod { get; set; }

    public string? Path { get; set; }

    public string? Params { get; set; }

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public long ElapsedMs { get; set; }

    public string? Ip { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>登录日志返回</summary>
public class LoginLogDto
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string? Message { get; set; }

    public string? Ip { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>变更日志查询条件</summary>
public class ChangeLogQueryDto : PageQuery
{
    /// <summary>业务表名关键字（如 SysRole）</summary>
    [StringLength(50)]
    public string? TableName { get; set; }

    /// <summary>操作人关键字</summary>
    [StringLength(50)]
    public string? UserName { get; set; }

    /// <summary>开始时间</summary>
    public DateTime? BeginTime { get; set; }

    /// <summary>结束时间</summary>
    public DateTime? EndTime { get; set; }
}

/// <summary>字段级变更日志返回</summary>
public class ChangeLogDto
{
    [System.Text.Json.Serialization.JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long Id { get; set; }

    public string TableName { get; set; } = string.Empty;

    public string RecordId { get; set; } = string.Empty;

    /// <summary>变更明细 JSON：{ "字段": { "old": 旧值, "new": 新值 } }（敏感字段已脱敏）</summary>
    public string Changes { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long UserId { get; set; }

    public string? UserName { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>日志服务：操作日志与登录日志的记录与查询</summary>
public interface ISysLogService
{
    /// <summary>记录操作日志（异步尽力写入，失败不影响业务）</summary>
    Task RecordOperationAsync(SysOperationLog log);

    /// <summary>记录登录日志（异步尽力写入，失败不影响业务）</summary>
    Task RecordLoginAsync(SysLoginLog log);

    /// <summary>操作日志分页</summary>
    Task<PageResult<OperationLogDto>> GetOperationLogPageAsync(LogQueryDto query, long? viewerUserId = null);

    /// <summary>清理指定日期前的操作日志（物理删除）</summary>
    Task<int> CleanupOperationLogsAsync(DateTime before);

    /// <summary>清理指定日期前的登录日志（物理删除）</summary>
    Task<int> CleanupLoginLogsAsync(DateTime before);

    /// <summary>操作日志导出（全量，条件同分页）</summary>
    Task<List<OperationLogDto>> GetOperationLogExportAsync(LogQueryDto query, long? viewerUserId = null);

    /// <summary>登录日志导出（全量，条件同分页）</summary>
    Task<List<LoginLogDto>> GetLoginLogExportAsync(LogQueryDto query, long? viewerUserId = null);

    /// <summary>登录日志分页</summary>
    Task<PageResult<LoginLogDto>> GetLoginLogPageAsync(LogQueryDto query, long? viewerUserId = null);

    /// <summary>字段级变更日志分页（sys_change_log）</summary>
    Task<PageResult<ChangeLogDto>> GetChangeLogPageAsync(ChangeLogQueryDto query, long? viewerUserId = null);

    /// <summary>清理指定日期前的变更日志（物理删除）</summary>
    Task<int> CleanupChangeLogsAsync(DateTime before);
}
