namespace NetBase.Model.Entities;

/// <summary>
/// 软删除标记接口。SqlSugar 全局查询过滤器会自动过滤已删除数据。
/// </summary>
public interface ISoftDelete
{
    /// <summary>是否已删除</summary>
    bool IsDeleted { get; set; }
}
