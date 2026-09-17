using System.Text.Json.Serialization;
using SqlSugar;

namespace NetBase.Model.Entities;

/// <summary>
/// 实体基类：主键 + 审计字段 + 软删除。
/// </summary>
public abstract class BaseEntity : ISoftDelete
{
    /// <summary>主键（雪花ID，插入时由 ID 生成器填充；序列化为字符串防前端精度丢失）</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    [SugarColumn(IsPrimaryKey = true, ColumnDescription = "主键ID")]
    public long Id { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(ColumnDescription = "创建时间")]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建人（接入认证后写入操作人）</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "创建人")]
    public string? CreateBy { get; set; }

    /// <summary>更新时间</summary>
    [SugarColumn(IsNullable = true, ColumnDescription = "更新时间")]
    public DateTime? UpdateTime { get; set; }

    /// <summary>更新人（接入认证后写入操作人）</summary>
    [SugarColumn(IsNullable = true, Length = 50, ColumnDescription = "更新人")]
    public string? UpdateBy { get; set; }

    /// <summary>是否已删除（软删除）</summary>
    [SugarColumn(ColumnDescription = "是否已删除")]
    public bool IsDeleted { get; set; }

    /// <summary>
    /// 乐观锁并发版本：Repository.UpdateWithVersionCheckAsync 更新时校验并自增，
    /// 冲突（他人已先修改）返回 false，由上层提示刷新重试。
    /// </summary>
    [SugarColumn(ColumnDescription = "并发版本", DefaultValue = "0")]
    public long Version { get; set; }
}
