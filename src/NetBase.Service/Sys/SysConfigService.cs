using ICacheService = NetBase.Common.Cache.ICacheService;
using NetBase.Common.Cache;
using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using SqlSugar;

namespace NetBase.Service.Sys;

/// <summary>系统参数服务：键取值（带缓存）与 CRUD，业务模块经此读取可运维参数</summary>
public interface ISysConfigService
{
    /// <summary>按键取参数值（缓存），不存在返回 null</summary>
    Task<string?> GetConfigValueAsync(string configKey);

    /// <summary>按键取参数值，不存在返回默认值</summary>
    Task<int> GetIntConfigAsync(string configKey, int defaultValue);

    /// <summary>参数分页</summary>
    Task<PageResult<SysConfig>> GetPageAsync(ConfigQueryDto query);

    Task<long> CreateAsync(ConfigSaveDto dto, string? operatorName = null);

    Task UpdateAsync(long id, ConfigSaveDto dto, string? operatorName = null);

    /// <summary>删除参数（内置参数拒绝删除，仅可改值）</summary>
    Task DeleteAsync(long id);
}

public class SysConfigService(
    IRepository<SysConfig> repository,
    ICacheService cacheService) : ISysConfigService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public async Task<string?> GetConfigValueAsync(string configKey)
    {
        var key = CacheKey(configKey);
        var cached = cacheService.Get<string>(key);
        if (cached != null)
        {
            return cached;
        }

        var config = await repository.GetFirstAsync(x => x.ConfigKey == configKey);
        if (config == null)
        {
            return null;
        }
        cacheService.Set(key, config.ConfigValue, CacheTtl);
        return config.ConfigValue;
    }

    public async Task<int> GetIntConfigAsync(string configKey, int defaultValue)
    {
        var value = await GetConfigValueAsync(configKey);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    public async Task<PageResult<SysConfig>> GetPageAsync(ConfigQueryDto query)
    {
        var hasCondition = false;
        var exp = Expressionable.Create<SysConfig>();
        if (query.Keyword.IsNotNullOrEmpty())
        {
            hasCondition = true;
            var keyword = query.Keyword!.Trim();
            exp.And(x => x.ConfigKey.Contains(keyword) || x.ConfigName.Contains(keyword));
        }
        return await repository.GetPageListAsync(hasCondition ? exp.ToExpression() : null, query);
    }

    public async Task<long> CreateAsync(ConfigSaveDto dto, string? operatorName = null)
    {
        if (await repository.AnyAsync(x => x.ConfigKey == dto.ConfigKey))
        {
            throw new BusinessException($"参数键 {dto.ConfigKey} 已存在", ApiResultCode.BadRequest);
        }

        var config = new SysConfig
        {
            ConfigKey = dto.ConfigKey,
            ConfigValue = dto.ConfigValue,
            ConfigName = dto.ConfigName,
            IsBuiltIn = false,
            Remark = dto.Remark,
            CreateBy = operatorName
        };
        await repository.InsertAsync(config);
        await cacheService.RemoveAsync(CacheKey(dto.ConfigKey));
        return config.Id;
    }

    public async Task UpdateAsync(long id, ConfigSaveDto dto, string? operatorName = null)
    {
        var config = await repository.GetByIdAsync(id)
            ?? throw new BusinessException($"参数不存在（Id={id}）", ApiResultCode.NotFound);
        // 内置参数的业务键不可变更（值/名称/备注可改），否则业务读取将静默失效
        if (config.IsBuiltIn && !string.Equals(config.ConfigKey, dto.ConfigKey, StringComparison.Ordinal))
        {
            throw new BusinessException("内置参数不允许修改参数键，仅可修改参数值", ApiResultCode.BadRequest);
        }
        if (await repository.AnyAsync(x => x.ConfigKey == dto.ConfigKey && x.Id != id))
        {
            throw new BusinessException($"参数键 {dto.ConfigKey} 已存在", ApiResultCode.BadRequest);
        }

        var oldKey = config.ConfigKey;
        config.ConfigKey = dto.ConfigKey;
        config.ConfigValue = dto.ConfigValue;
        config.ConfigName = dto.ConfigName;
        config.Remark = dto.Remark;
        config.UpdateTime = DateTime.Now;
        config.UpdateBy = operatorName;
        await repository.UpdateAsync(config);

        await cacheService.RemoveAsync(CacheKey(oldKey));
        await cacheService.RemoveAsync(CacheKey(dto.ConfigKey));
    }

    public async Task DeleteAsync(long id)
    {
        var config = await repository.GetByIdAsync(id)
            ?? throw new BusinessException($"参数不存在（Id={id}）", ApiResultCode.NotFound);
        if (config.IsBuiltIn)
        {
            throw new BusinessException("内置参数不允许删除，仅可修改值");
        }
        await repository.DeleteAsync(id);
        await cacheService.RemoveAsync(CacheKey(config.ConfigKey));
    }

    private static string CacheKey(string configKey) => $"netbase:config:{configKey}";
}
