using SqlSugar;

using System.Text.Json.Serialization;
namespace NetBase.Model.Entities;

/// <summary>操作日志（写操作自动记录，参数脱敏）</summary>
[SugarTable("sys_operation_log", TableDescription = "操作日志表")]
public class SysOperationLog : BaseEntity
{
    /// <summary>操作人ID</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [SugarColumn(ColumnDescription = "操作人ID")]
    public long UserId { get; set; }

    /// <summary>操作人用户名</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "操作人用户名")]
    public string? UserName { get; set; }

    /// <summary>业务模块（控制器名）</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "业务模块")]
    public string? Module { get; set; }

    /// <summary>操作动作（Action 名）</summary>
    [SugarColumn(IsNullable = true, Length = 100, ColumnDescription = "操作动作")]
    public string? Action { get; set; }

    /// <summary>HTTP 方法</summary>
    [SugarColumn(IsNullable = true, Length = 10, ColumnDescription = "HTTP方法")]
    public string? HttpMethod { get; set; }

    /// <summary>请求路径</summary>
    [SugarColumn(IsNullable = true, Length = 200, ColumnDescription = "请求路径")]
    public string? Path { get; set; }

    /// <summary>请求参数（JSON，密码字段脱敏，超长截断）</summary>
    [SugarColumn(ColumnDataType = "nvarchar(max)", ColumnDescription = "请求参数")]
    public string? Params { get; set; }

    /// <summary>是否成功</summary>
    [SugarColumn(ColumnDescription = "是否成功")]
    public bool Success { get; set; }

    /// <summary>错误消息（失败时）</summary>
    [SugarColumn(IsNullable = true, Length = 500, ColumnDescription = "错误消息")]
    public string? ErrorMessage { get; set; }

    /// <summary>耗时（毫秒）</summary>
    [SugarColumn(ColumnDescription = "耗时(毫秒)")]
    public long ElapsedMs { get; set; }

    /// <summary>操作IP</summary>
    [SugarColumn(IsNullable = true, Length = 64, ColumnDescription = "操作IP")]
    public string? Ip { get; set; }
}
