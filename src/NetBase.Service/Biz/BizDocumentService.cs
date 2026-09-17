using System.ComponentModel.DataAnnotations;
using NetBase.Common.Exceptions;
using SqlSugar;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities.Biz;
using NetBase.Model.Enums;
using NetBase.Repository.Repositories;
using NetBase.Service.Sys.Flow;

// ===================== 报销单（业务样板一） =====================

namespace NetBase.Service.Biz;

/// <summary>报销单查询条件</summary>
public class ExpenseQueryDto : PageQuery
{
    /// <summary>标题关键字</summary>
    [StringLength(50)]
    public string? Keyword { get; set; }
}

/// <summary>报销单返回</summary>
public class ExpenseDto
{
    [System.Text.Json.Serialization.JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Reason { get; set; }

    /// <summary>状态：0草稿 1审批中 2已通过 3已拒绝 4已撤回</summary>
    public int Status { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>报销单保存请求</summary>
public class ExpenseSaveDto
{
    /// <summary>报销标题</summary>
    [Required(ErrorMessage = "标题不能为空")]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    /// <summary>报销金额</summary>
    [Range(0.01, 999999999, ErrorMessage = "金额须大于 0")]
    public decimal Amount { get; set; }

    /// <summary>事由说明</summary>
    [StringLength(500)]
    public string? Reason { get; set; }
}

/// <summary>报销单业务：单据 CRUD + 提交审批（审批逻辑零侵入，全部委托 FlowEngine）</summary>
public interface IBizExpenseService
{
    Task<PageResult<ExpenseDto>> GetPageListAsync(ExpenseQueryDto query);

    Task<long> CreateAsync(ExpenseSaveDto dto);

    Task UpdateAsync(long id, ExpenseSaveDto dto);

    Task DeleteAsync(long id);

    Task<ExpenseDto?> GetDetailAsync(long id);

    /// <summary>提交审批（草稿 → 审批中；金额作为流程变量供条件分支求值）</summary>
    Task<long> SubmitAsync(long id);
}

/// <summary>
/// 报销单审批回调：接入审批流的业务侧全部工作 = 实现本接口 + 提交时调用 FlowEngine。
/// 注册见 AddNetBaseBiz。
/// </summary>
public class ExpenseFlowHandler(IRepository<BizExpense> repository) : IFlowBusinessHandler
{
    public string BusinessTable => "biz_expense";

    public async Task<string> GetSummaryAsync(long businessId)
    {
        var doc = await repository.GetByIdAsync(businessId)
                  ?? throw new BusinessException("报销单不存在");
        return $"报销申请：{doc.Title}（¥{doc.Amount:N2}）";
    }

    /// <summary>终态回写单据状态（引擎事务内执行，与流程终态原子）</summary>
    public Task OnFinishedAsync(long businessId, FlowInstanceStatus finalStatus)
    {
        var status = finalStatus switch
        {
            FlowInstanceStatus.Approved => BizDocStatus.Approved,
            FlowInstanceStatus.Rejected => BizDocStatus.Rejected,
            FlowInstanceStatus.Revoked => BizDocStatus.Revoked,
            _ => BizDocStatus.Rejected
        };
        return repository.UpdateWhereAsync(x => x.Id == businessId, x => new BizExpense { Status = status });
    }
}

public class BizExpenseService(
    IRepository<BizExpense> repository,
    IFlowEngine flowEngine) : IBizExpenseService
{
    public async Task<PageResult<ExpenseDto>> GetPageListAsync(ExpenseQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        var predicate = Expressionable.Create<BizExpense>()
            .AndIF(!string.IsNullOrWhiteSpace(keyword), x => x.Title.Contains(keyword!))
            .ToExpression();
        var page = await repository.GetPageListAsync(predicate, query);
        return PageResult<ExpenseDto>.Of(page.Items.Select(ToDto).ToList(), page.Total, page.PageIndex, page.PageSize);
    }

    public async Task<long> CreateAsync(ExpenseSaveDto dto)
    {
        var doc = new BizExpense { Title = dto.Title, Amount = dto.Amount, Reason = dto.Reason };
        await repository.InsertAsync(doc);
        return doc.Id;
    }

    public async Task UpdateAsync(long id, ExpenseSaveDto dto)
    {
        var doc = await GetDraftAsync(id);
        doc.Title = dto.Title;
        doc.Amount = dto.Amount;
        doc.Reason = dto.Reason;
        await repository.UpdateAsync(doc);
    }

    public Task DeleteAsync(long id) => GetDraftAndDeleteAsync(id);

    public async Task<ExpenseDto?> GetDetailAsync(long id)
    {
        var doc = await repository.GetByIdAsync(id);
        return doc == null ? null : ToDto(doc);
    }

    public async Task<long> SubmitAsync(long id)
    {
        var doc = await repository.GetByIdAsync(id)
                  ?? throw new BusinessException("报销单不存在");
        if (doc.Status != BizDocStatus.Draft && doc.Status != BizDocStatus.Rejected)
        {
            throw new BusinessException("仅草稿或被拒绝的单据可提交审批");
        }

        doc.Status = BizDocStatus.InApproval;
        await repository.UpdateAsync(doc);

        // 提交审批：金额等变量供流程条件分支路由；重复提交走同一 FlowCode 最新启用版本
        await flowEngine.SubmitAsync(new FlowSubmitRequest
        {
            FlowCode = "expense",
            BusinessTable = "biz_expense",
            BusinessId = id,
            Variables = new Dictionary<string, object?> { ["amount"] = doc.Amount }
        });
        return id;
    }

    private async Task<BizExpense> GetDraftAsync(long id)
    {
        var doc = await repository.GetByIdAsync(id)
                  ?? throw new BusinessException("报销单不存在");
        if (doc.Status != BizDocStatus.Draft)
        {
            throw new BusinessException("仅草稿可修改");
        }
        return doc;
    }

    private async Task GetDraftAndDeleteAsync(long id)
    {
        var doc = await GetDraftAsync(id);
        await repository.DeleteAsync(doc);
    }

    private static ExpenseDto ToDto(BizExpense x) => new()
    {
        Id = x.Id,
        Title = x.Title,
        Amount = x.Amount,
        Reason = x.Reason,
        Status = x.Status,
        CreateTime = x.CreateTime
    };
}

// ===================== 采购申请单（业务样板二：复制接入验证） =====================

/// <summary>采购申请单查询条件</summary>
public class PurchaseQueryDto : PageQuery
{
    /// <summary>标题关键字</summary>
    [StringLength(50)]
    public string? Keyword { get; set; }
}

/// <summary>采购申请单返回</summary>
public class PurchaseRequestDto
{
    [System.Text.Json.Serialization.JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Reason { get; set; }

    public int Status { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>采购申请单保存请求</summary>
public class PurchaseRequestSaveDto
{
    /// <summary>申请标题</summary>
    [Required(ErrorMessage = "标题不能为空")]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    /// <summary>采购物品</summary>
    [Required(ErrorMessage = "采购物品不能为空")]
    [StringLength(100)]
    public string ItemName { get; set; } = string.Empty;

    /// <summary>预算金额</summary>
    [Range(0.01, 999999999, ErrorMessage = "金额须大于 0")]
    public decimal Amount { get; set; }

    /// <summary>申请事由</summary>
    [StringLength(500)]
    public string? Reason { get; set; }
}

/// <summary>采购申请单业务</summary>
public interface IBizPurchaseRequestService
{
    Task<PageResult<PurchaseRequestDto>> GetPageListAsync(PurchaseQueryDto query);

    Task<PurchaseRequestDetail?> GetDetailAsync(long id);

    Task<long> CreateAsync(PurchaseRequestSaveDto dto);

    Task UpdateAsync(long id, PurchaseRequestSaveDto dto);

    Task DeleteAsync(long id);

    Task<long> SubmitAsync(long id);
}

/// <summary>采购单详情（含金额，供审批详情页展示）</summary>
public class PurchaseRequestDetail : PurchaseRequestDto
{
}

/// <summary>采购单审批回调（FlowCode=purchase_request）</summary>
public class PurchaseRequestFlowHandler(IRepository<BizPurchaseRequest> repository) : IFlowBusinessHandler
{
    public string BusinessTable => "biz_purchase_request";

    public async Task<string> GetSummaryAsync(long businessId)
    {
        var doc = await repository.GetByIdAsync(businessId)
                  ?? throw new BusinessException("采购申请单不存在");
        return $"采购申请：{doc.Title}（¥{doc.Amount:N2}）";
    }

    public Task OnFinishedAsync(long businessId, FlowInstanceStatus finalStatus)
    {
        var status = finalStatus switch
        {
            FlowInstanceStatus.Approved => BizDocStatus.Approved,
            FlowInstanceStatus.Rejected => BizDocStatus.Rejected,
            FlowInstanceStatus.Revoked => BizDocStatus.Revoked,
            _ => BizDocStatus.Rejected
        };
        return repository.UpdateWhereAsync(x => x.Id == businessId, x => new BizPurchaseRequest { Status = status });
    }
}

public class BizPurchaseRequestService(
    IRepository<BizPurchaseRequest> repository,
    IFlowEngine flowEngine) : IBizPurchaseRequestService
{
    public async Task<PageResult<PurchaseRequestDto>> GetPageListAsync(PurchaseQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        var predicate = Expressionable.Create<BizPurchaseRequest>()
            .AndIF(!string.IsNullOrWhiteSpace(keyword), x => x.Title.Contains(keyword!))
            .ToExpression();
        var page = await repository.GetPageListAsync(predicate, query);
        return PageResult<PurchaseRequestDto>.Of(
            page.Items.Select(ToDto).ToList(), page.Total, page.PageIndex, page.PageSize);
    }

    public async Task<PurchaseRequestDetail?> GetDetailAsync(long id)
    {
        var doc = await repository.GetByIdAsync(id);
        return doc == null ? null : new PurchaseRequestDetail
        {
            Id = doc.Id, Title = doc.Title, ItemName = doc.ItemName, Amount = doc.Amount,
            Reason = doc.Reason, Status = doc.Status, CreateTime = doc.CreateTime
        };
    }

    public async Task<long> CreateAsync(PurchaseRequestSaveDto dto)
    {
        var doc = new BizPurchaseRequest
        {
            Title = dto.Title, ItemName = dto.ItemName, Amount = dto.Amount, Reason = dto.Reason
        };
        await repository.InsertAsync(doc);
        return doc.Id;
    }

    public async Task UpdateAsync(long id, PurchaseRequestSaveDto dto)
    {
        var doc = await GetEditableAsync(id);
        doc.Title = dto.Title;
        doc.ItemName = dto.ItemName;
        doc.Amount = dto.Amount;
        doc.Reason = dto.Reason;
        await repository.UpdateAsync(doc);
    }

    public async Task DeleteAsync(long id)
    {
        var doc = await GetEditableAsync(id);
        await repository.DeleteAsync(doc);
    }

    public async Task<long> SubmitAsync(long id)
    {
        var doc = await repository.GetByIdAsync(id)
                  ?? throw new BusinessException("采购申请单不存在");
        if (doc.Status != BizDocStatus.Draft && doc.Status != BizDocStatus.Rejected)
        {
            throw new BusinessException("仅草稿或被拒绝的单据可提交审批");
        }

        doc.Status = BizDocStatus.InApproval;
        await repository.UpdateAsync(doc);

        await flowEngine.SubmitAsync(new FlowSubmitRequest
        {
            FlowCode = "purchase_request",
            BusinessTable = "biz_purchase_request",
            BusinessId = id,
            Variables = new Dictionary<string, object?> { ["amount"] = doc.Amount }
        });
        return id;
    }

    private async Task<BizPurchaseRequest> GetEditableAsync(long id)
    {
        var doc = await repository.GetByIdAsync(id)
                  ?? throw new BusinessException("采购申请单不存在");
        if (doc.Status != BizDocStatus.Draft)
        {
            throw new BusinessException("仅草稿可操作");
        }
        return doc;
    }

    private static PurchaseRequestDto ToDto(BizPurchaseRequest x) => new()
    {
        Id = x.Id,
        Title = x.Title,
        ItemName = x.ItemName,
        Amount = x.Amount,
        Reason = x.Reason,
        Status = x.Status,
        CreateTime = x.CreateTime
    };
}
