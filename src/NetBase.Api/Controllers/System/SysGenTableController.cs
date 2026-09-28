using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Users;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.System;

/// <summary>代码生成器（sys:gentable:list 及子权限码）</summary>
[ApiController]
[Route("api/v1/sys/gen")]
public class SysGenTableController(
    SysGenTableService genService,
    GenMetaService metaService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>生成配置分页</summary>
    [HasPermission("sys:gentable:list")]
    [HttpGet("page")]
    public async Task<ApiResult<PageResult<SysGenTable>>> GetPage(
        [FromQuery] string? keyword, [FromQuery] PageQuery query)
        => Success(await genService.GetPageAsync(keyword, query));

    /// <summary>生成配置详情（含字段与子表，向导回显）</summary>
    [HasPermission("sys:gentable:list")]
    [HttpGet("{id:long}")]
    public async Task<ApiResult<SysGenTableService.GenDetail>> GetDetail(long id)
        => Success(await genService.GetDetailWithColumnsAsync(id));

    /// <summary>库内可选导入的表清单（排除框架表）</summary>
    [HasPermission("sys:gentable:list")]
    [HttpGet("db-tables")]
    public async Task<ApiResult<List<GenTableBrief>>> GetDbTables()
        => Success(await metaService.ListTablesAsync());

    /// <summary>读一张表的列元数据（手工新建时预填）</summary>
    [HasPermission("sys:gentable:list")]
    [HttpGet("db-columns")]
    public async Task<ApiResult<List<GenColumnMeta>>> GetDbColumns([FromQuery] string tableName)
        => Success(await metaService.GetColumnsAsync(tableName));

    /// <summary>从数据库导入一张表为生成配置</summary>
    [HasPermission("sys:gentable:add")]
    [HttpPost("import")]
    public async Task<ApiResult<string>> ImportTable([FromBody] GenImportDto dto)
        => SuccessId(await genService.ImportTableAsync(dto.TableName, dto.ModuleName, dto.FunctionName, dto.EntityName));

    /// <summary>保存生成配置（新建/编辑，含字段与子表）</summary>
    [HasPermission("sys:gentable:add")]
    [NoRepeatSubmit]
    [HttpPost]
    public async Task<ApiResult<string>> Save([FromBody] GenTableSaveDto dto)
        => SuccessId(await genService.SaveAsync(dto));

    /// <summary>删除生成配置</summary>
    [HasPermission("sys:gentable:delete")]
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await genService.DeleteAsync(id);
        return Success("删除成功");
    }

    /// <summary>预览生成代码（分文件）</summary>
    [HasPermission("sys:gentable:list")]
    [HttpGet("{id:long}/preview")]
    public async Task<ApiResult<List<GenPreviewFile>>> Preview(long id)
        => Success(await genService.GenerateAsync(id));

    /// <summary>下载生成代码 zip（按 Biz 目录结构摆放）</summary>
    [HasPermission("sys:gentable:download")]
    [HttpGet("{id:long}/download")]
    public async Task<IActionResult> Download(long id)
    {
        var bytes = await genService.DownloadAsync(id);
        var table = await genService.GetDetailAsync(id);
        return File(bytes, "application/zip", $"{table.TableName}-gen.zip");
    }
}

/// <summary>表导入请求</summary>
public class GenImportDto
{
    /// <summary>表名</summary>
    public string TableName { get; set; } = string.Empty;
    /// <summary>模块名（小写）</summary>
    public string ModuleName { get; set; } = string.Empty;
    /// <summary>功能名</summary>
    public string FunctionName { get; set; } = string.Empty;
    /// <summary>实体名（Pascal）</summary>
    public string EntityName { get; set; } = string.Empty;
}
