using NetBase.Model.Entities;

namespace NetBase.Service.Sys;

/// <summary>数据范围计算结果</summary>
public class DataScopeInfo
{
    /// <summary>是否放行全部（不加过滤）</summary>
    public bool FilterAll { get; set; }

    /// <summary>可见部门集合（Custom/Dept/DeptAndChild 合并并集）</summary>
    public HashSet<long> DeptIds { get; set; } = [];

    /// <summary>是否附带本人数据（多角色含"仅本人"档时为 true）</summary>
    public bool IncludeSelfData { get; set; }

    /// <summary>是否存在部门条件（决定过滤器表达式形态）</summary>
    public bool HasDeptCondition => DeptIds.Count > 0;
}

/// <summary>
/// 数据范围服务：按当前用户多角色合并计算可见数据范围（取并集，RuoYi 同款策略）。
/// 例：角色A=本部门 + 角色B=仅本人 → 可见（本部门数据 ∪ 本人数据）。
/// </summary>
public interface IDataScopeService
{
    Task<DataScopeInfo> GetDataScopeAsync(long userId);

    /// <summary>部门及子孙部门ID集合（本部门及以下档使用）</summary>
    Task<List<long>> GetDeptAndChildIdsAsync(long deptId);
}
