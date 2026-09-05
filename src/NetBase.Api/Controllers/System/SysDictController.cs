using Microsoft.AspNetCore.Authorization;
using NetBase.Common.Users;
using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.System;

/// <summary>字典管理：类型与数据项</summary>
[ApiController]
[Route("api/v1/sys/dict")]
public class SysDictController(ISysDictService dictService, ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>字典类型分页</summary>
    [HasPermission("sys:dict:list")]
    [HttpGet("type/page")]
    public async Task<ApiResult<PageResult<SysDictType>>> TypePage([FromQuery] DictTypeQueryDto query)
    {
        return Success(await dictService.GetTypePageAsync(query));
    }

    /// <summary>字典类型详情</summary>
    [HasPermission("sys:dict:list")]
    [HttpGet("type/{id:long}")]
    public async Task<ApiResult<SysDictType?>> TypeDetail(long id)
    {
        return Success(await dictService.GetTypeDetailAsync(id));
    }

    /// <summary>创建字典类型</summary>
    [HasPermission("sys:dict:add")]
    [HttpPost("type")]
    public async Task<ApiResult<long>> CreateType([FromBody] DictTypeSaveDto dto)
    {
        return Success(await dictService.CreateTypeAsync(dto, OperatorName), "创建成功");
    }

    /// <summary>更新字典类型</summary>
    [HasPermission("sys:dict:edit")]
    [HttpPut("type/{id:long}")]
    public async Task<ApiResult> UpdateType(long id, [FromBody] DictTypeSaveDto dto)
    {
        await dictService.UpdateTypeAsync(id, dto, OperatorName);
        return Success();
    }

    /// <summary>删除字典类型（含全部数据项）</summary>
    [HasPermission("sys:dict:delete")]
    [HttpDelete("type/{id:long}")]
    public async Task<ApiResult> DeleteType(long id)
    {
        await dictService.DeleteTypeAsync(id);
        return Success();
    }

    /// <summary>字典数据项分页</summary>
    [HasPermission("sys:dict:list")]
    [HttpGet("data/page")]
    public async Task<ApiResult<PageResult<SysDictData>>> DataPage([FromQuery] long dictTypeId, [FromQuery] PageQuery query)
    {
        return Success(await dictService.GetDataPageAsync(dictTypeId, query));
    }

    /// <summary>按字典编码取启用数据项（业务取值入口，无需权限码）</summary>
    [Authorize]
    [HttpGet("data/by-code/{dictCode}")]
    public async Task<ApiResult<List<DictDataDto>>> DataByCode(string dictCode)
    {
        return Success(await dictService.GetEnabledDataByCodeAsync(dictCode));
    }

    /// <summary>创建字典数据项</summary>
    [HasPermission("sys:dict:add")]
    [HttpPost("data")]
    public async Task<ApiResult<long>> CreateData([FromBody] DictDataSaveDto dto)
    {
        return Success(await dictService.CreateDataAsync(dto, OperatorName), "创建成功");
    }

    /// <summary>更新字典数据项</summary>
    [HasPermission("sys:dict:edit")]
    [HttpPut("data/{id:long}")]
    public async Task<ApiResult> UpdateData(long id, [FromBody] DictDataSaveDto dto)
    {
        await dictService.UpdateDataAsync(id, dto, OperatorName);
        return Success();
    }

    /// <summary>删除字典数据项</summary>
    [HasPermission("sys:dict:delete")]
    [HttpDelete("data/{id:long}")]
    public async Task<ApiResult> DeleteData(long id)
    {
        await dictService.DeleteDataAsync(id);
        return Success();
    }
}
