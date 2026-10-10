using NetBase.Common.Time;
using System.ComponentModel.DataAnnotations;
using Mapster;
using SqlSugar;
using NetBase.Common.Exceptions;
using NetBase.Common.Results;
using NetBase.Model.Entities;
using NetBase.Model.Entities.Biz;
using NetBase.Repository.Repositories;
using NetBase.Service.Base;

namespace NetBase.Service.Biz;

/// <summary>ContractMgr 查询入参</summary>
public class ContractQueryDto : PageQuery
{

    /// <summary>合同名称</summary>
    public string ContractName { get; set; } = string.Empty;


}

/// <summary>ContractMgr 出参</summary>
public class ContractDto
{
    /// <summary>ID（字符串防精度丢失）</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>合同名称</summary>
    public string ContractName { get; set; } = string.Empty;


    /// <summary>合同金额</summary>
    public decimal Amount { get; set; }


    /// <summary>签订日期</summary>
    public DateTime? SignDate { get; set; }


    /// <summary>备注</summary>
    public string Remark { get; set; } = string.Empty;


    /// <summary>归属部门</summary>
    public string DeptId { get; set; } = string.Empty;


    /// <summary>归属用户</summary>
    public string OwnerUserId { get; set; } = string.Empty;


    /// <summary>单据状态</summary>
    public string Status { get; set; } = string.Empty;


    /// <summary>乐观锁版本（编辑回显，保存时回传）</summary>
    public long Version { get; set; }
}

/// <summary>ContractMgr 保存入参</summary>
public class ContractSaveDto
{

    /// <summary>合同名称</summary>

    [Required(ErrorMessage = "合同名称不能为空")]


    [StringLength(200)]

    public string ContractName { get; set; } = string.Empty;


    /// <summary>合同金额</summary>

    [Required(ErrorMessage = "合同金额不能为空")]


    public decimal Amount { get; set; }


    /// <summary>签订日期</summary>


    public DateTime? SignDate { get; set; }


    /// <summary>备注</summary>


    [StringLength(500)]

    public string Remark { get; set; } = string.Empty;


    /// <summary>归属部门</summary>


    [StringLength(0)]

    public string DeptId { get; set; } = string.Empty;


    /// <summary>归属用户</summary>


    [StringLength(0)]

    public string OwnerUserId { get; set; } = string.Empty;


    /// <summary>单据状态</summary>

    [Required(ErrorMessage = "单据状态不能为空")]


    [StringLength(0)]

    public string Status { get; set; } = string.Empty;


}

/// <summary>ContractMgr 服务接口</summary>
public interface IBizContractService
{
    Task<PageResult<ContractDto>> GetPageListAsync(ContractQueryDto query);
    Task<ContractDto?> GetDetailAsync(long id);
    Task<long> CreateAsync(ContractSaveDto dto, string? operatorName = null);
    Task UpdateAsync(long id, ContractSaveDto dto, string? operatorName = null);
    Task DeleteAsync(long id);

}

/// <summary>ContractMgr 服务实现</summary>
public class BizContractService(
    IRepository<BizContract> repository,
    TimeProvider tp) : BaseService<BizContract>(repository), IBizContractService
{
    private const string NotFoundCode = "CONTRACT_NOT_FOUND";

    public async Task<PageResult<ContractDto>> GetPageListAsync(ContractQueryDto query)
    {
        var expr = Expressionable.Create<BizContract>()


            .AndIF(!string.IsNullOrEmpty(query.ContractName), x => x.ContractName.Contains(query.ContractName!))


            .ToExpression();

        var page = await Repository.GetPageListAsync(expr, query);
        return new PageResult<ContractDto>
        {
            Items = page.Items.Adapt<List<ContractDto>>(),
            Total = page.Total,
            PageIndex = page.PageIndex,
            PageSize = page.PageSize,
        };
    }

    public async Task<ContractDto?> GetDetailAsync(long id)
    {
        var entity = await Repository.GetByIdAsync(id);
        return entity?.Adapt<ContractDto>();
    }

    public async Task<long> CreateAsync(ContractSaveDto dto, string? operatorName = null)
    {
        var entity = dto.Adapt<BizContract>();
        entity.Id = 0; // 雪花 AOP 填充

        await Repository.InsertAsync(entity);
        return entity.Id;
    }

    public async Task UpdateAsync(long id, ContractSaveDto dto, string? operatorName = null)
    {
        var entity = await Repository.GetByIdAsync(id)
            ?? throw new BusinessException("数据不存在", ApiResultCode.NotFound, NotFoundCode);
        dto.Adapt(entity);
        entity.Id = id;
        entity.UpdateTime = tp.LocalNow();
        entity.UpdateBy = operatorName;
        await UpdateWithConcurrencyCheckAsync(entity);
    }

    public async Task DeleteAsync(long id)
    {
        var entity = await Repository.GetByIdAsync(id)
            ?? throw new BusinessException("数据不存在", ApiResultCode.NotFound, NotFoundCode);
        await Repository.DeleteAsync(entity);
    }

}

