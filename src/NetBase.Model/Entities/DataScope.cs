namespace NetBase.Model.Entities;

/// <summary>
/// 数据范围过滤接口：业务实体实现此接口后，
/// SqlSugar 请求级全局过滤器自动按当前用户角色的数据范围过滤（业务代码零侵入）。
/// </summary>
public interface IDataScope
{
    /// <summary>数据所属部门</summary>
    long DeptId { get; }

    /// <summary>数据归属人（"仅本人"范围使用；0 表示无归属人。约定为表内真实列）</summary>
    long OwnerUserId { get; }
}
