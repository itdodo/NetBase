using NetBase.Common.Time;
using System.ComponentModel.DataAnnotations;
using Mapster;
using NetBase.Common.Exceptions;
using NetBase.Common.Results;
using NetBase.Model.Entities;
using NetBase.Model.Enums;
using NetBase.Model.Entities.Biz;
using NetBase.Repository.Repositories;
using NetBase.Service.Base;
using SqlSugar;
using NetBase.Service.Sys.Flow;

namespace NetBase.Service.Biz;

/// <summary>GenTestExpense 查询入参</summary>
public class ExpenseTestQueryDto : PageQuery
{

    /// <summary>报销标题</summary>
    public string Title { get; set; } = string.Empty;


}

/// <summary>GenTestExpense 出参</summary>
public class ExpenseTestDto
{
    /// <summary>ID（字符串防精度丢失）</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>报销标题</summary>
    public string Title { get; set; } = string.Empty;


    /// <summary>报销金额</summary>
    public decimal Amount { get; set; }


    /// <summary>事由说明</summary>
    public string Reason { get; set; } = string.Empty;


    /// <summary>状态</summary>
    public string Status { get; set; } = string.Empty;


    /// <summary>是否已删除</summary>
    public bool Isdeleted { get; set; }


    /// <summary>并发版本</summary>



    /// <summary>乐观锁版本（编辑回显，保存时回传）</summary>
    public long Version { get; set; }
}

/// <summary>GenTestExpense 保存入参</summary>
public class ExpenseTestSaveDto
{

    /// <summary>报销标题</summary>

    [Required(ErrorMessage = "报销标题不能为空")]


    [StringLength(100)]

    public string Title { get; set; } = string.Empty;


    /// <summary>报销金额</summary>

    [Required(ErrorMessage = "报销金额不能为空")]


    public decimal Amount { get; set; }


    /// <summary>事由说明</summary>


    [StringLength(500)]

    public string Reason { get; set; } = string.Empty;


    /// <summary>状态</summary>

    [Required(ErrorMessage = "状态不能为空")]


    [StringLength(0)]

    public string Status { get; set; } = string.Empty;


    /// <summary>创建时间</summary>

    [Required(ErrorMessage = "创建时间不能为空")]


    public DateTime Createtime { get; set; }


    /// <summary>创建人</summary>


    [StringLength(50)]

    public string Createby { get; set; } = string.Empty;


    /// <summary>更新时间</summary>


    public DateTime? Updatetime { get; set; }


    /// <summary>更新人</summary>


    [StringLength(50)]

    public string Updateby { get; set; } = string.Empty;


    /// <summary>是否已删除</summary>

    [Required(ErrorMessage = "是否已删除不能为空")]


    public bool Isdeleted { get; set; }


    /// <summary>并发版本</summary>

    [Required(ErrorMessage = "并发版本不能为空")]


    [StringLength(0)]

    public string Version { get; set; } = string.Empty;


}

/// <summary>GenTestExpense 服务接口</summary>
public interface IBizExpenseTestService
{
    Task<PageResult<ExpenseTestDto>> GetPageListAsync(ExpenseTestQueryDto query);
    Task<ExpenseTestDto?> GetDetailAsync(long id);
    Task<long> CreateAsync(ExpenseTestSaveDto dto, string? operatorName = null);
    Task UpdateAsync(long id, ExpenseTestSaveDto dto, string? operatorName = null);
    Task DeleteAsync(long id);

    /// <summary>提交审批</summary>
    Task SubmitAsync(long id, string flowCode, string operatorName);

}

/// <summary>GenTestExpense 服务实现</summary>
public class BizExpenseTestService(
    IRepository<BizExpenseTest> repository,
    NetBase.Service.Sys.Flow.IFlowEngine flowEngine,
    TimeProvider tp) : BaseService<BizExpenseTest>(repository), IBizExpenseTestService
{
    private const string NotFoundCode = "EXPENSETEST_NOT_FOUND";

    public async Task<PageResult<ExpenseTestDto>> GetPageListAsync(ExpenseTestQueryDto query)
    {
        var expr = Expressionable.Create<BizExpenseTest>()


            .AndIF(query.Title != null, x => x.Title == query.Title)


            .ToExpression();

        var page = await Repository.GetPageListAsync(expr, query);
        return new PageResult<ExpenseTestDto>
        {
            Items = page.Items.Adapt<List<ExpenseTestDto>>(),
            Total = page.Total,
            PageIndex = page.PageIndex,
            PageSize = page.PageSize,
        };
    }

    public async Task<ExpenseTestDto?> GetDetailAsync(long id)
    {
        var entity = await Repository.GetByIdAsync(id);
        return entity?.Adapt<ExpenseTestDto>();
    }

    public async Task<long> CreateAsync(ExpenseTestSaveDto dto, string? operatorName = null)
    {
        var entity = dto.Adapt<BizExpenseTest>();
        entity.Id = 0; // 雪花 AOP 填充

        entity.Status = 0; // 草稿

        await Repository.InsertAsync(entity);
        return entity.Id;
    }

    public async Task UpdateAsync(long id, ExpenseTestSaveDto dto, string? operatorName = null)
    {
        var entity = await Repository.GetByIdAsync(id)
            ?? throw new BusinessException("数据不存在", ApiResultCode.NotFound, NotFoundCode);
        dto.Adapt(entity);
        entity.Id = id;
        entity.UpdateTime = tp.LocalNow();
        entity.UpdateBy = operatorName;
        await UpdateWithConcurrencyCheckAsync(entity);
    }

    public async Task DeleteAsync(long id)
    {
        var entity = await Repository.GetByIdAsync(id)
            ?? throw new BusinessException("数据不存在", ApiResultCode.NotFound, NotFoundCode);
        await Repository.DeleteAsync(entity);
    }


    /// <summary>提交审批：仅草稿/拒绝状态可提交；送审后置为审批中</summary>
    public async Task SubmitAsync(long id, string flowCode, string operatorName)
    {
        var entity = await Repository.GetByIdAsync(id)
            ?? throw new BusinessException("数据不存在", ApiResultCode.NotFound, NotFoundCode);
        if (entity.Status is not (0 or 3))
        {
            throw new BusinessException("仅草稿或被拒绝的单据可提交审批", ApiResultCode.BadRequest, ErrorCodes.BIZ_DOC_SUBMIT_STATUS_INVALID);
        }

        entity.Status = 1;
        entity.UpdateTime = tp.LocalNow();
        entity.UpdateBy = operatorName;
        await UpdateWithConcurrencyCheckAsync(entity);

        await flowEngine.SubmitAsync(new FlowSubmitRequest
        {
            FlowCode = flowCode,
            BusinessTable = "biz_expensetest",
            BusinessId = entity.Id,
            Variables = new Dictionary<string, object?>()
        });
    }

}


/// <summary>GenTestExpense 审批流业务回调（按 BusinessTable 注册）</summary>
public class BizExpenseTestFlowHandler : IFlowBusinessHandler
{
    public string BusinessTable => "biz_expensetest";

    private readonly IRepository<BizExpenseTest> _repository;

    private readonly TimeProvider _tp;

    public BizExpenseTestFlowHandler(IRepository<BizExpenseTest> repository, TimeProvider tp) => (_repository, _tp) = (repository, tp);

    public async Task<string> GetSummaryAsync(long businessId)
    {
        var entity = await _repository.GetByIdAsync(businessId);
        return entity == null ? BusinessTable : $"GenTestExpense {entity.Id}";
    }

    /// <summary>终态回写：2 通过 / 3 拒绝 / 4 撤回 / 5 作废</summary>
    public async Task OnFinishedAsync(long businessId, FlowInstanceStatus finalStatus)
    {
        var status = (int)finalStatus;
        var now = _tp.LocalNow(); // 表达式树不翻译方法调用，树外求值
        await _repository.UpdateWhereAsync(
            x => x.Id == businessId,
            x => new BizExpenseTest { Status = status, UpdateTime = now });
    }
}

