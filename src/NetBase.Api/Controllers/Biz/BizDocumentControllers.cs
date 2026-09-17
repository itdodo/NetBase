using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Common.Users;
using NetBase.Service.Biz;

namespace NetBase.Api.Controllers.Biz;

/// <summary>报销单（审批流业务样板一）</summary>
[ApiController]
[Route("api/v1/biz/expense")]
public class BizExpenseController(IBizExpenseService service,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>报销单分页</summary>
    [HasPermission("biz:expense:list")]
    [HttpGet("page")]
    public async Task<ApiResult<PageResult<ExpenseDto>>> Page([FromQuery] ExpenseQueryDto query) =>
        Success(await service.GetPageListAsync(query));

    /// <summary>报销单详情</summary>
    [HasPermission("biz:expense:list")]
    [HttpGet("{id:long}")]
    public async Task<ApiResult<ExpenseDto?>> Get(long id) => Success(await service.GetDetailAsync(id));

    /// <summary>创建报销单（草稿）</summary>
    [HasPermission("biz:expense:add")]
    [NoRepeatSubmit]
    [HttpPost]
    public async Task<ApiResult<string>> Create([FromBody] ExpenseSaveDto dto) =>
        SuccessId(await service.CreateAsync(dto), "创建成功");

    /// <summary>修改报销单（草稿）</summary>
    [HasPermission("biz:expense:edit")]
    [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] ExpenseSaveDto dto)
    {
        await service.UpdateAsync(id, dto);
        return Success();
    }

    /// <summary>删除报销单（草稿）</summary>
    [HasPermission("biz:expense:delete")]
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await service.DeleteAsync(id);
        return Success();
    }

    /// <summary>提交审批</summary>
    [HasPermission("biz:expense:edit")]
    [NoRepeatSubmit]
    [HttpPost("{id:long}/submit")]
    public async Task<ApiResult> Submit(long id)
    {
        await service.SubmitAsync(id);
        return Success("已提交审批");
    }
}

/// <summary>采购申请单（审批流业务样板二：金额条件分支演示）</summary>
[ApiController]
[Route("api/v1/biz/purchase")]
public class BizPurchaseRequestController(IBizPurchaseRequestService service,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>采购申请单分页</summary>
    [HasPermission("biz:purchase:list")]
    [HttpGet("page")]
    public async Task<ApiResult<PageResult<PurchaseRequestDto>>> Page([FromQuery] PurchaseQueryDto query) =>
        Success(await service.GetPageListAsync(query));

    /// <summary>采购申请单详情</summary>
    [HasPermission("biz:purchase:list")]
    [HttpGet("{id:long}")]
    public async Task<ApiResult<PurchaseRequestDetail?>> Get(long id) => Success(await service.GetDetailAsync(id));

    /// <summary>创建采购申请单（草稿）</summary>
    [HasPermission("biz:purchase:add")]
    [NoRepeatSubmit]
    [HttpPost]
    public async Task<ApiResult<string>> Create([FromBody] PurchaseRequestSaveDto dto) =>
        SuccessId(await service.CreateAsync(dto), "创建成功");

    /// <summary>修改采购申请单（草稿）</summary>
    [HasPermission("biz:purchase:edit")]
    [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] PurchaseRequestSaveDto dto)
    {
        await service.UpdateAsync(id, dto);
        return Success();
    }

    /// <summary>删除采购申请单（草稿）</summary>
    [HasPermission("biz:purchase:delete")]
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await service.DeleteAsync(id);
        return Success();
    }

    /// <summary>提交审批（金额≥阈值走总监分支）</summary>
    [HasPermission("biz:purchase:edit")]
    [NoRepeatSubmit]
    [HttpPost("{id:long}/submit")]
    public async Task<ApiResult> Submit(long id)
    {
        await service.SubmitAsync(id);
        return Success("已提交审批");
    }
}
