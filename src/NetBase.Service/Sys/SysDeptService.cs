using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using Mapster;
using NetBase.Model.Enums;
using NetBase.Repository.Repositories;

namespace NetBase.Service.Sys;

/// <summary>部门服务实现</summary>
public class SysDeptService(
    IRepository<SysDept> repository,
    IRepository<SysUser> userRepository) : ISysDeptService
{
    public Task<List<SysDept>> GetAllDeptsAsync() => repository.GetListAsync();

    public async Task<List<DeptTreeDto>> GetTreeAsync()
    {
        var all = await repository.GetListAsync();
        return BuildTree(all, 0);
    }

    public async Task<List<long>> GetDeptAndChildIdsAsync(long deptId)
    {
        var all = await repository.GetListAsync();
        var ids = new List<long> { deptId };
        CollectChildren(all, deptId, ids);
        return ids;
    }

    public Task<SysDept?> GetDetailAsync(long id) => repository.GetByIdAsync(id);

    public async Task<long> CreateAsync(DeptSaveDto dto, string? operatorName = null)
    {
        Validate(dto);
        if (dto.ParentId != 0 && !await repository.AnyAsync(x => x.Id == dto.ParentId))
        {
            throw new BusinessException("父级部门不存在", ApiResultCode.BadRequest);
        }
        if (await repository.AnyAsync(x => x.DeptCode == dto.DeptCode))
        {
            throw new BusinessException($"部门编码 {dto.DeptCode} 已存在", ApiResultCode.BadRequest);
        }

        var dept = new SysDept
        {
            ParentId = dto.ParentId,
            DeptName = dto.DeptName,
            DeptCode = dto.DeptCode,
            Leader = dto.Leader,
            Sort = dto.Sort,
            Status = dto.Status,
            CreateBy = operatorName
        };
        await repository.InsertAsync(dept);
        return dept.Id;
    }

    public async Task UpdateAsync(long id, DeptSaveDto dto, string? operatorName = null)
    {
        var dept = await repository.GetByIdAsync(id)
            ?? throw new BusinessException($"部门不存在（Id={id}）", ApiResultCode.NotFound);
        Validate(dto);
        if (dto.ParentId == id)
        {
            throw new BusinessException("父级部门不能是自身", ApiResultCode.BadRequest);
        }
        if (dto.ParentId != 0 && !await repository.AnyAsync(x => x.Id == dto.ParentId))
        {
            throw new BusinessException("父级部门不存在", ApiResultCode.BadRequest);
        }
        if (await IsDescendantAsync(id, dto.ParentId))
        {
            throw new BusinessException("父级部门不能是自身的子孙部门", ApiResultCode.BadRequest);
        }
        if (await repository.AnyAsync(x => x.DeptCode == dto.DeptCode && x.Id != id))
        {
            throw new BusinessException($"部门编码 {dto.DeptCode} 已存在", ApiResultCode.BadRequest);
        }

        dept.ParentId = dto.ParentId;
        dept.DeptName = dto.DeptName;
        dept.DeptCode = dto.DeptCode;
        dept.Leader = dto.Leader;
        dept.Sort = dto.Sort;
        dept.Status = dto.Status;
        dept.UpdateTime = DateTime.Now;
        dept.UpdateBy = operatorName;
        await repository.UpdateAsync(dept);
    }

    public async Task DeleteAsync(long id)
    {
        _ = await repository.GetByIdAsync(id)
            ?? throw new BusinessException($"部门不存在（Id={id}）", ApiResultCode.NotFound);
        if (await repository.AnyAsync(x => x.ParentId == id))
        {
            throw new BusinessException("存在下级部门，不允许删除");
        }
        if (await userRepository.AnyAsync(x => x.DeptId == id))
        {
            throw new BusinessException("部门下存在用户，不允许删除");
        }

        await repository.DeleteAsync(id);
    }

    /// <summary>判断 candidateId 是否为 ancestorId 的子孙部门</summary>
    private async Task<bool> IsDescendantAsync(long ancestorId, long candidateId)
    {
        var all = await repository.GetListAsync();
        var parentMap = all.ToDictionary(x => x.Id, x => x.ParentId);
        var current = candidateId;
        while (current != 0 && parentMap.TryGetValue(current, out var parentId))
        {
            if (parentId == ancestorId)
            {
                return true;
            }
            current = parentId;
        }
        return false;
    }

    private static void CollectChildren(List<SysDept> all, long parentId, List<long> ids)
    {
        foreach (var dept in all.Where(x => x.ParentId == parentId))
        {
            ids.Add(dept.Id);
            CollectChildren(all, dept.Id, ids);
        }
    }

    private static void Validate(DeptSaveDto dto)
    {
        if (dto.DeptName.IsNullOrWhiteSpace())
        {
            throw new BusinessException("部门名称不能为空", ApiResultCode.BadRequest);
        }
        if (dto.DeptCode.IsNullOrWhiteSpace())
        {
            throw new BusinessException("部门编码不能为空", ApiResultCode.BadRequest);
        }
    }

    private static List<DeptTreeDto> BuildTree(List<SysDept> all, long parentId)
    {
        return all.Where(x => x.ParentId == parentId)
            .OrderBy(x => x.Sort)
            .Select(x =>
            {
                var node = x.Adapt<DeptTreeDto>();
                node.Children = BuildTree(all, x.Id);
                return node;
            })
            .ToList();
    }
}
