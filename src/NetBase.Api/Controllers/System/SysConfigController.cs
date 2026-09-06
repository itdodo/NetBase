using Microsoft.AspNetCore.Authorization;
using NetBase.Common.Users;
using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.System;

/// <summary>系统参数配置</summary>
[ApiController]
[Route("api/v1/sys/config")]
public class SysConfigController(ISysConfigService configService, ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>参数分页</summary>
    [HasPermission("sys:config:list")]
    [HttpGet("page")]
    public async Task<ApiResult<PageResult<SysConfig>>> Page([FromQuery] ConfigQueryDto query)
    {
        return Success(await configService.GetPageAsync(query));
    }

    /// <summary>创建参数</summary>
    [HasPermission("sys:config:add")]
    [HttpPost]
    public async Task<ApiResult<string>> Create([FromBody] ConfigSaveDto dto)
    {
        return SuccessId(await configService.CreateAsync(dto, OperatorName), "创建成功");
    }

    /// <summary>更新参数</summary>
    [HasPermission("sys:config:edit")]
    [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] ConfigSaveDto dto)
    {
        await configService.UpdateAsync(id, dto, OperatorName);
        return Success();
    }

    /// <summary>删除参数（内置参数仅可改值）</summary>
    [HasPermission("sys:config:delete")]
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await configService.DeleteAsync(id);
        return Success();
    }
}
