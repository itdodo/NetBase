using NetBase.Common.Time;
using ICacheService = NetBase.Common.Cache.ICacheService;
using NetBase.Common.Cache;
using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Model.Enums;
using NetBase.Repository.Repositories;
using SqlSugar;

namespace NetBase.Service.Sys;

/// <summary>字典服务实现</summary>
public class SysDictService(
    IRepository<SysDictType> typeRepository,
    IRepository<SysDictData> dataRepository,
    ICacheService cacheService,

TimeProvider tp) : ISysDictService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public async Task<PageResult<SysDictType>> GetTypePageAsync(DictTypeQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        return await typeRepository.GetPageListAsync(
            Expressionable.Create<SysDictType>()
                .AndIF(keyword.IsNotNullOrEmpty(), x => x.DictCode.Contains(keyword!) || x.DictName.Contains(keyword!))
                .ToExpression(), query);
    }

    public Task<SysDictType?> GetTypeDetailAsync(long id) => typeRepository.GetByIdAsync(id);

    public async Task<long> CreateTypeAsync(DictTypeSaveDto dto, string? operatorName = null)
    {
        if (await typeRepository.AnyAsync(x => x.DictCode == dto.DictCode))
        {
            throw new BusinessException($"字典编码 {dto.DictCode} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_DICT_CODE_EXISTS);
        }

        var type = new SysDictType
        {
            DictCode = dto.DictCode,
            DictName = dto.DictName,
            Status = dto.Status,
            Remark = dto.Remark,
            CreateBy = operatorName
        };
        await typeRepository.InsertAsync(type);
        return type.Id;
    }

    public async Task UpdateTypeAsync(long id, DictTypeSaveDto dto, string? operatorName = null)
    {
        var type = await typeRepository.GetByIdAsync(id)
            ?? throw new BusinessException($"字典类型不存在（Id={id}）", ApiResultCode.NotFound, ErrorCodes.SYS_DICT_TYPE_NOT_FOUND);
        if (await typeRepository.AnyAsync(x => x.DictCode == dto.DictCode && x.Id != id))
        {
            throw new BusinessException($"字典编码 {dto.DictCode} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_DICT_CODE_EXISTS);
        }

        var codeChanged = type.DictCode != dto.DictCode;
        type.DictCode = dto.DictCode;
        type.DictName = dto.DictName;
        type.Status = dto.Status;
        type.Remark = dto.Remark;
        type.UpdateTime = tp.LocalNow();
        type.UpdateBy = operatorName;
        await typeRepository.UpdateAsync(type);

        if (codeChanged)
        {
            await cacheService.RemoveAsync(CacheKey(type.DictCode));
        }
        await cacheService.RemoveAsync(CacheKey(dto.DictCode));
    }

    public async Task DeleteTypeAsync(long id)
    {
        var type = await typeRepository.GetByIdAsync(id)
            ?? throw new BusinessException($"字典类型不存在（Id={id}）", ApiResultCode.NotFound, ErrorCodes.SYS_DICT_TYPE_NOT_FOUND);
        await typeRepository.DeleteAsync(id);
        // 数据项与类型保持一致的软删除（级联可追溯）
        await dataRepository.DeleteWhereAsync(x => x.DictTypeId == id);
        await cacheService.RemoveAsync(CacheKey(type.DictCode));
    }

    public Task<PageResult<SysDictData>> GetDataPageAsync(long dictTypeId, PageQuery query) =>
        dataRepository.GetPageListAsync(x => x.DictTypeId == dictTypeId, query);

    public async Task<long> CreateDataAsync(DictDataSaveDto dto, string? operatorName = null)
    {
        _ = await typeRepository.GetByIdAsync(dto.DictTypeId)
            ?? throw new BusinessException("字典类型不存在", ApiResultCode.BadRequest, ErrorCodes.SYS_DICT_TYPE_NOT_FOUND);
        if (await dataRepository.AnyAsync(x => x.DictTypeId == dto.DictTypeId && x.Value == dto.Value))
        {
            throw new BusinessException($"字典值 {dto.Value} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_DICT_VALUE_EXISTS);
        }

        var data = new SysDictData
        {
            DictTypeId = dto.DictTypeId,
            Label = dto.Label,
            Value = dto.Value,
            Sort = dto.Sort,
            Status = dto.Status,
            Remark = dto.Remark,
            CreateBy = operatorName
        };
        await dataRepository.InsertAsync(data);
        await InvalidateDataCacheAsync(dto.DictTypeId);
        return data.Id;
    }

    public async Task UpdateDataAsync(long id, DictDataSaveDto dto, string? operatorName = null)
    {
        var data = await dataRepository.GetByIdAsync(id)
            ?? throw new BusinessException($"字典项不存在（Id={id}）", ApiResultCode.BadRequest, ErrorCodes.SYS_DICT_DATA_NOT_FOUND);
        _ = await typeRepository.GetByIdAsync(dto.DictTypeId)
            ?? throw new BusinessException("字典类型不存在", ApiResultCode.BadRequest, ErrorCodes.SYS_DICT_TYPE_NOT_FOUND);
        if (await dataRepository.AnyAsync(x => x.DictTypeId == dto.DictTypeId && x.Value == dto.Value && x.Id != id))
        {
            throw new BusinessException($"字典值 {dto.Value} 已存在", ApiResultCode.BadRequest, ErrorCodes.SYS_DICT_VALUE_EXISTS);
        }

        data.DictTypeId = dto.DictTypeId;
        data.Label = dto.Label;
        data.Value = dto.Value;
        data.Sort = dto.Sort;
        data.Status = dto.Status;
        data.Remark = dto.Remark;
        data.UpdateTime = tp.LocalNow();
        data.UpdateBy = operatorName;
        await dataRepository.UpdateAsync(data);
        await InvalidateDataCacheAsync(dto.DictTypeId);
    }

    public async Task DeleteDataAsync(long id)
    {
        var data = await dataRepository.GetByIdAsync(id)
            ?? throw new BusinessException($"字典项不存在（Id={id}）", ApiResultCode.NotFound, ErrorCodes.SYS_DICT_DATA_NOT_FOUND);
        await dataRepository.DeleteAsync(id);
        await InvalidateDataCacheAsync(data.DictTypeId);
    }

    public async Task<List<DictDataDto>> GetEnabledDataByCodeAsync(string dictCode)
    {
        var key = CacheKey(dictCode);
        var cached = await cacheService.GetAsync<List<DictDataDto>>(key);
        if (cached != null)
        {
            return cached;
        }

        var type = await typeRepository.GetFirstAsync(x => x.DictCode == dictCode);
        if (type == null)
        {
            return [];
        }

        var items = await dataRepository.GetListAsync(x =>
            x.DictTypeId == type.Id && x.Status == (int)StatusEnum.Enabled);
        var result = items.OrderBy(x => x.Sort)
            .Select(x => new DictDataDto { Id = x.Id, Label = x.Label, Value = x.Value, Sort = x.Sort, Status = x.Status, DictCode = type.DictCode })
            .ToList();
        await cacheService.SetAsync(key, result, CacheTtl);
        return result;
    }

    /// <summary>字典项变更后按类型编码失效缓存</summary>
    private async Task InvalidateDataCacheAsync(long dictTypeId)
    {
        var type = await typeRepository.GetByIdAsync(dictTypeId);
        if (type != null)
        {
            await cacheService.RemoveAsync(CacheKey(type.DictCode));
        }
    }

    private static string CacheKey(string dictCode) => $"dict:{dictCode}";
}
