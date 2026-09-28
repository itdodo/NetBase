using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Users;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities.Biz;
using NetBase.Service.Biz;

namespace NetBase.Api.Controllers.Biz;

/// <summary>ContractMgr</summary>
[ApiController]
[Route("api/v1/biz/contract")]
public class BizContractController(
    IBizContractService service,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>分页查询</summary>
    [HasPermission("biz:contract:list")]
    [HttpGet("page")]
    public async Task<ApiResult<PageResult<ContractDto>>> GetPage([FromQuery] ContractQueryDto query)
        => Success(await service.GetPageListAsync(query));

    /// <summary>详情</summary>
    [HasPermission("biz:contract:list")]
    [HttpGet("{id:long}")]
    public async Task<ApiResult<ContractDto?>> GetDetail(long id)
        => Success(await service.GetDetailAsync(id));

    /// <summary>新增</summary>
    [HasPermission("biz:contract:add")]
    [NoRepeatSubmit]
    [HttpPost]
    public async Task<ApiResult<string>> Create([FromBody] ContractSaveDto dto)
        => SuccessId(await service.CreateAsync(dto, OperatorName));

    /// <summary>编辑</summary>
    [HasPermission("biz:contract:edit")]
    [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] ContractSaveDto dto)
    {
        await service.UpdateAsync(id, dto, OperatorName);
        return Success("更新成功");
    }

    /// <summary>删除</summary>
    [HasPermission("biz:contract:delete")]
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await service.DeleteAsync(id);
        return Success("删除成功");
    }

}

/// <summary>提交审批请求</summary>
public class FlowSubmitDto
{
    /// <summary>流程编码（空则走单据绑定的默认流程）</summary>
    public string FlowCode { get; set; } = string.Empty;
}
