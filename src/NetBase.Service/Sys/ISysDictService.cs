using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;

namespace NetBase.Service.Sys;

/// <summary>字典服务：类型 CRUD、数据项 CRUD、按编码取启用项（带缓存）</summary>
public interface ISysDictService
{
    Task<PageResult<SysDictType>> GetTypePageAsync(DictTypeQueryDto query);

    Task<SysDictType?> GetTypeDetailAsync(long id);

    Task<long> CreateTypeAsync(DictTypeSaveDto dto, string? operatorName = null);

    Task UpdateTypeAsync(long id, DictTypeSaveDto dto, string? operatorName = null);

    /// <summary>删除类型（含其全部数据项）</summary>
    Task DeleteTypeAsync(long id);

    /// <summary>字典数据项分页（按类型）</summary>
    Task<PageResult<SysDictData>> GetDataPageAsync(long dictTypeId, PageQuery query);

    Task<long> CreateDataAsync(DictDataSaveDto dto, string? operatorName = null);

    Task UpdateDataAsync(long id, DictDataSaveDto dto, string? operatorName = null);

    Task DeleteDataAsync(long id);

    /// <summary>按字典编码取启用数据项（业务取值入口，缓存 10 分钟）</summary>
    Task<List<DictDataDto>> GetEnabledDataByCodeAsync(string dictCode);
}
