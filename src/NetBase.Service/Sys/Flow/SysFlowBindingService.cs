using NetBase.Common.Exceptions;
using NetBase.Common.Results;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;

namespace NetBase.Service.Sys.Flow;

/// <summary>单据绑定返回</summary>
public class FlowBindingDto
{
    public long Id { get; set; }

    public string BusinessTable { get; set; } = string.Empty;

    /// <summary>绑定的流程编码（空 = 不走审批）</summary>
    public string? FlowCode { get; set; }

    public string? Remark { get; set; }
}

/// <summary>单据绑定保存请求</summary>
public class FlowBindingSaveDto
{
    /// <summary>业务表名（唯一）</summary>
    public string BusinessTable { get; set; } = string.Empty;

    /// <summary>流程编码（空/不传 = 该单据不走审批流）</summary>
    public string? FlowCode { get; set; }

    /// <summary>说明</summary>
    public string? Remark { get; set; }
}

/// <summary>单据-审批流绑定管理：换流程/停用审批全部运行时配置，不发版</summary>
public interface ISysFlowBindingService
{
    Task<List<FlowBindingDto>> GetListAsync();

    /// <summary>保存绑定（存在则更新，不存在则创建）</summary>
    Task SaveAsync(FlowBindingSaveDto dto);

    Task DeleteAsync(long id);
}

public class SysFlowBindingService(IRepository<SysFlowBinding> repository,
    IRepository<SysFlowDefinition> definitionRepository) : ISysFlowBindingService
{
    public async Task<List<FlowBindingDto>> GetListAsync() =>
        (await repository.GetListAsync())
        .OrderBy(b => b.BusinessTable)
        .Select(b => new FlowBindingDto
        {
            Id = b.Id,
            BusinessTable = b.BusinessTable,
            FlowCode = b.FlowCode,
            Remark = b.Remark
        }).ToList();

    public async Task SaveAsync(FlowBindingSaveDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.BusinessTable))
        {
            throw new NetBase.Common.Exceptions.BusinessException("业务表名不能为空");
        }

        // 绑定了编码时校验其存在（启用/停用均可绑定，停用流程在提交时才会拒绝）
        if (!string.IsNullOrWhiteSpace(dto.FlowCode)
            && !await definitionRepository.AnyAsync(d => d.FlowCode == dto.FlowCode))
        {
            throw new NetBase.Common.Exceptions.BusinessException($"流程编码 {dto.FlowCode} 不存在");
        }

        var existing = await repository.GetFirstAsync(b => b.BusinessTable == dto.BusinessTable);
        if (existing != null)
        {
            existing.FlowCode = string.IsNullOrWhiteSpace(dto.FlowCode) ? null : dto.FlowCode;
            existing.Remark = dto.Remark;
            await repository.UpdateAsync(existing);
        }
        else
        {
            await repository.InsertAsync(new SysFlowBinding
            {
                BusinessTable = dto.BusinessTable.Trim(),
                FlowCode = string.IsNullOrWhiteSpace(dto.FlowCode) ? null : dto.FlowCode,
                Remark = dto.Remark
            });
        }
    }

    public async Task DeleteAsync(long id)
    {
        var binding = await repository.GetByIdAsync(id)
                      ?? throw new NetBase.Common.Exceptions.BusinessException("绑定记录不存在");
        await repository.DeleteAsync(binding);
    }
}
