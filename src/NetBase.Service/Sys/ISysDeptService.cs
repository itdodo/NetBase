using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;

namespace NetBase.Service.Sys;

/// <summary>部门服务：组织树 CRUD 与数据范围辅助</summary>
public interface ISysDeptService
{
    /// <summary>全量部门（树形组装用，量级小）</summary>
    Task<List<SysDept>> GetAllDeptsAsync();

    /// <summary>部门树（可选状态过滤）</summary>
    Task<List<DeptTreeDto>> GetTreeAsync();

    /// <summary>部门及子孙部门ID集合（数据权限"本部门及以下"档使用）</summary>
    Task<List<long>> GetDeptAndChildIdsAsync(long deptId);

    Task<SysDept?> GetDetailAsync(long id);

    Task<long> CreateAsync(DeptSaveDto dto, string? operatorName = null);

    Task UpdateAsync(long id, DeptSaveDto dto, string? operatorName = null);

    /// <summary>删除部门（校验子部门与在职用户）</summary>
    Task DeleteAsync(long id);
}
