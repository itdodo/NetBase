using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Users;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities.Biz;
using NetBase.Service.Biz;

namespace NetBase.Api.Controllers.Biz;

/// <summary>GenTestExpense</summary>
[ApiController]
[Route("api/v1/biz/expensetest")]
public class BizExpenseTestController(
    IBizExpenseTestService service,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>分页查询</summary>
    [HasPermission("biz:expensetest:list")]
    [HttpGet("page")]
    public async Task<ApiResult<PageResult<ExpenseTestDto>>> GetPage([FromQuery] ExpenseTestQueryDto query)
        => Success(await service.GetPageListAsync(query));

    /// <summary>详情</summary>
    [HasPermission("biz:expensetest:list")]
    [HttpGet("{id:long}")]
    public async Task<ApiResult<ExpenseTestDto?>> GetDetail(long id)
        => Success(await service.GetDetailAsync(id));

    /// <summary>新增</summary>
    [HasPermission("biz:expensetest:add")]
    [NoRepeatSubmit]
    [HttpPost]
    public async Task<ApiResult<string>> Create([FromBody] ExpenseTestSaveDto dto)
        => SuccessId(await service.CreateAsync(dto, OperatorName));

    /// <summary>编辑</summary>
    [HasPermission("biz:expensetest:edit")]
    [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] ExpenseTestSaveDto dto)
    {
        await service.UpdateAsync(id, dto, OperatorName);
        return Success("更新成功");
    }

    /// <summary>删除</summary>
    [HasPermission("biz:expensetest:delete")]
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await service.DeleteAsync(id);
        return Success("删除成功");
    }


    /// <summary>提交审批</summary>
    [HasPermission("biz:expensetest:edit")]
    [NoRepeatSubmit]
    [HttpPost("{id:long}/submit")]
    public async Task<ApiResult> Submit(long id, [FromBody] FlowSubmitDto dto)
    {
        await service.SubmitAsync(id, dto.FlowCode, OperatorName);
        return Success("已提交审批");
    }

}

/// <summary>提交审批请求</summary>
public class FlowSubmitDto
{
    /// <summary>流程编码（空则走单据绑定的默认流程）</summary>
    public string FlowCode { get; set; } = string.Empty;
}
