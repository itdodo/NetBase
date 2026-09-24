using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using SqlSugar;

namespace NetBase.Service.Sys;

/// <summary>岗位返回</summary>
public class PositionDto
{
    [System.Text.Json.Serialization.JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long Id { get; set; }

    public string PositionCode { get; set; } = string.Empty;

    public string PositionName { get; set; } = string.Empty;

    public int Sort { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    public int Status { get; set; }

    public string? Remark { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>岗位保存请求</summary>
public class PositionSaveDto
{
    /// <summary>岗位编码（唯一）</summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "岗位编码不能为空")]
    [System.ComponentModel.DataAnnotations.StringLength(50)]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "岗位编码只能包含字母、数字、下划线")]
    public string PositionCode { get; set; } = string.Empty;

    /// <summary>岗位名称</summary>
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "岗位名称不能为空")]
    [System.ComponentModel.DataAnnotations.StringLength(50)]
    public string PositionName { get; set; } = string.Empty;

    /// <summary>排序号</summary>
    [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
    public int Sort { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    [System.ComponentModel.DataAnnotations.Range(0, 1)]
    public int Status { get; set; } = 1;

    /// <summary>备注</summary>
    [System.ComponentModel.DataAnnotations.StringLength(200)]
    public string? Remark { get; set; }
}

/// <summary>岗位查询条件</summary>
public class PositionQueryDto : PageQuery
{
    /// <summary>名称/编码关键字</summary>
    [System.ComponentModel.DataAnnotations.StringLength(50)]
    public string? Keyword { get; set; }
}

/// <summary>岗位管理：审批权限的载体（角色管菜单/接口权限，岗位管审批人解析）</summary>
public interface ISysPositionService
{
    Task<PageResult<PositionDto>> GetPageListAsync(PositionQueryDto query);

    /// <summary>全部启用岗位（下拉框/审批人选择用）</summary>
    Task<List<PositionDto>> GetEnabledListAsync();

    Task<long> CreateAsync(PositionSaveDto dto, string? operatorName = null);

    Task UpdateAsync(long id, PositionSaveDto dto, string? operatorName = null);

    Task DeleteAsync(long id);
}

public class SysPositionService(
    IRepository<SysPosition> repository,
    IRepository<SysUserPosition> userPositionRepository) : ISysPositionService
{
    public async Task<PageResult<PositionDto>> GetPageListAsync(PositionQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        var predicate = Expressionable.Create<SysPosition>()
            .AndIF(!string.IsNullOrWhiteSpace(keyword),
                x => x.PositionName.Contains(keyword!) || x.PositionCode.Contains(keyword!))
            .ToExpression();
        var page = await repository.GetPageListAsync(predicate, query);
        return PageResult<PositionDto>.Of(
            page.Items.Select(ToDto).ToList(), page.Total, page.PageIndex, page.PageSize);
    }

    public async Task<List<PositionDto>> GetEnabledListAsync()
    {
        var list = await repository.GetListAsync(x => x.Status == 1);
        return list.OrderBy(x => x.Sort).Select(ToDto).ToList();
    }

    public async Task<long> CreateAsync(PositionSaveDto dto, string? operatorName = null)
    {
        if (await repository.AnyAsync(x => x.PositionCode == dto.PositionCode))
        {
            throw new BusinessException($"岗位编码 {dto.PositionCode} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_POSITION_CODE_EXISTS);
        }

        var position = new SysPosition
        {
            PositionCode = dto.PositionCode,
            PositionName = dto.PositionName,
            Sort = dto.Sort,
            Status = dto.Status,
            Remark = dto.Remark,
            CreateBy = operatorName
        };
        await repository.InsertAsync(position);
        return position.Id;
    }

    public async Task UpdateAsync(long id, PositionSaveDto dto, string? operatorName = null)
    {
        var position = await repository.GetByIdAsync(id)
                       ?? throw new BusinessException("岗位不存在", ErrorCodes.SYS_POSITION_NOT_FOUND);
        if (await repository.AnyAsync(x => x.PositionCode == dto.PositionCode && x.Id != id))
        {
            throw new BusinessException($"岗位编码 {dto.PositionCode} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_POSITION_CODE_EXISTS);
        }

        position.PositionCode = dto.PositionCode;
        position.PositionName = dto.PositionName;
        position.Sort = dto.Sort;
        position.Status = dto.Status;
        position.Remark = dto.Remark;
        position.UpdateTime = DateTime.Now;
        position.UpdateBy = operatorName;
        await repository.UpdateAsync(position);
    }

    public async Task DeleteAsync(long id)
    {
        if (await userPositionRepository.AnyAsync(x => x.PositionId == id))
        {
            throw new BusinessException("该岗位下仍有用户，请先移出", ErrorCodes.SYS_POSITION_HAS_USERS);
        }
        var position = await repository.GetByIdAsync(id)
                      ?? throw new BusinessException("岗位不存在", ErrorCodes.SYS_POSITION_NOT_FOUND);
        await repository.DeleteAsync(position);
    }

    private static PositionDto ToDto(SysPosition x) => new()
    {
        Id = x.Id,
        PositionCode = x.PositionCode,
        PositionName = x.PositionName,
        Sort = x.Sort,
        Status = x.Status,
        Remark = x.Remark,
        CreateTime = x.CreateTime
    };
}
