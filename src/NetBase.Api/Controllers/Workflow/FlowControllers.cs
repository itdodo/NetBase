using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Realtime;
using NetBase.Common.Results;
using NetBase.Common.Users;
using NetBase.Service.Sys;
using NetBase.Service.Sys.Flow;

namespace NetBase.Api.Controllers.Workflow;

/// <summary>审批操作请求</summary>
public class FlowActDto
{
    /// <summary>动作：approve-同意 reject-拒绝 return-驳回至节点</summary>
    [RegularExpression("^(approve|reject|return)$", ErrorMessage = "动作仅支持 approve/reject/return")]
    public string Action { get; set; } = "approve";

    /// <summary>驳回目标节点编码（action=return 时必填；start=退回发起人）</summary>
    public string? ReturnNodeCode { get; set; }

    /// <summary>审批意见</summary>
    [StringLength(500)]
    public string? Comment { get; set; }
}

/// <summary>转办请求</summary>
public class FlowTransferDto
{
    /// <summary>转办目标人</summary>
    [Required(ErrorMessage = "请选择转办目标人")]
    public List<long> UserIds { get; set; } = [];

    /// <summary>转办说明</summary>
    [StringLength(500)]
    public string? Comment { get; set; }
}

/// <summary>加签请求</summary>
public class FlowAddSignDto
{
    /// <summary>加签人</summary>
    [Required(ErrorMessage = "请选择加签人")]
    public List<long> UserIds { get; set; } = [];

    /// <summary>true-前加签（并入当前节点共同把关）false-后加签（本节点通过后追加审批）</summary>
    public bool Before { get; set; } = true;

    /// <summary>加签说明</summary>
    [StringLength(500)]
    public string? Comment { get; set; }
}

/// <summary>流程定义管理（设计器产物的存取与启停）</summary>
[ApiController]
[Route("api/v1/sys/flow/def")]
public class FlowDefinitionController(ISysFlowDefinitionService service,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>流程定义分页</summary>
    [HasPermission("sys:flow:list")]
    [HttpGet("page")]
    public async Task<ApiResult<PageResult<FlowDefinitionDto>>> Page([FromQuery] FlowDefinitionQueryDto query) =>
        Success(await service.GetPageListAsync(query));

    /// <summary>流程定义详情（含节点 JSON）</summary>
    [HasPermission("sys:flow:list")]
    [HttpGet("{id:long}")]
    public async Task<ApiResult<FlowDefinitionDto?>> Get(long id) => Success(await service.GetDetailAsync(id));

    /// <summary>创建流程定义（新版本）</summary>
    [HasPermission("sys:flow:add")]
    [NoRepeatSubmit]
    [HttpPost]
    public async Task<ApiResult<string>> Create([FromBody] FlowDefinitionSaveDto dto) =>
        SuccessId(await service.CreateAsync(dto), "创建成功");

    /// <summary>更新流程定义（停用态）</summary>
    [HasPermission("sys:flow:edit")]
    [HttpPut("{id:long}")]
    public async Task<ApiResult> Update(long id, [FromBody] FlowDefinitionSaveDto dto)
    {
        await service.UpdateAsync(id, dto);
        return Success();
    }

    /// <summary>删除流程定义（停用态）</summary>
    [HasPermission("sys:flow:delete")]
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await service.DeleteAsync(id);
        return Success();
    }

    /// <summary>启用（同编码其他版本自动停用）</summary>
    [HasPermission("sys:flow:edit")]
    [HttpPut("{id:long}/enable")]
    public async Task<ApiResult> Enable(long id)
    {
        await service.EnableAsync(id);
        return Success();
    }

    /// <summary>停用</summary>
    [HasPermission("sys:flow:edit")]
    [HttpPut("{id:long}/disable")]
    public async Task<ApiResult> Disable(long id)
    {
        await service.DisableAsync(id);
        return Success();
    }

    /// <summary>分类列表（名称 + 流程数）</summary>
    [HasPermission("sys:flow:list")]
    [HttpGet("categories")]
    public async Task<ApiResult<List<FlowCategoryDto>>> Categories() =>
        Success(await service.GetCategoriesAsync());

    /// <summary>重命名分类（新名已存在时自动合并）</summary>
    [HasPermission("sys:flow:edit")]
    [HttpPut("category")]
    public async Task<ApiResult> RenameCategory([FromBody] CategoryRenameDto dto)
    {
        await service.RenameCategoryAsync(dto.OldName, dto.NewName);
        return Success();
    }

    /// <summary>删除分类（仅未被流程使用时）</summary>
    [HasPermission("sys:flow:edit")]
    [HttpDelete("category")]
    public async Task<ApiResult> DeleteCategory([FromQuery] string name)
    {
        await service.DeleteCategoryAsync(name);
        return Success();
    }
}

/// <summary>分类重命名请求</summary>
public class CategoryRenameDto
{
    /// <summary>原分类名</summary>
    public string OldName { get; set; } = string.Empty;

    /// <summary>新分类名</summary>
    public string NewName { get; set; } = string.Empty;
}

/// <summary>审批任务：我的待办/已办与审批操作（无需权限码，任务归属人校验在引擎）</summary>
[ApiController]
[Route("api/v1/sys/flow/task")]
[Authorize]
public class FlowTaskController(IFlowEngine engine, IFlowQueryService queryService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>我的待办分页</summary>
    [HttpGet("todo")]
    public async Task<ApiResult<PageResult<FlowTaskViewDto>>> Todo([FromQuery] FlowTaskQueryDto query) =>
        Success(await queryService.GetTodoPageAsync(query));

    /// <summary>我的待办数（顶栏角标）</summary>
    [HttpGet("todo-count")]
    public async Task<ApiResult<int>> TodoCount() => Success(await queryService.GetTodoCountAsync());

    /// <summary>我的已办分页</summary>
    [HttpGet("done")]
    public async Task<ApiResult<PageResult<FlowTaskViewDto>>> Done([FromQuery] FlowTaskQueryDto query) =>
        Success(await queryService.GetDonePageAsync(query));

    /// <summary>审批（同意/拒绝/驳回，仅任务归属人）</summary>
    [NoRepeatSubmit]
    [HttpPost("{taskId:long}/act")]
    public async Task<ApiResult> Act(long taskId, [FromBody] FlowActDto dto)
    {
        var action = dto.Action switch
        {
            "approve" => FlowAction.Approve,
            "return" => FlowAction.Return,
            _ => FlowAction.Reject
        };
        await engine.ActAsync(taskId, action, dto.Comment, dto.ReturnNodeCode);
        return Success();
    }

    /// <summary>转办</summary>
    [NoRepeatSubmit]
    [HttpPost("{taskId:long}/transfer")]
    public async Task<ApiResult> Transfer(long taskId, [FromBody] FlowTransferDto dto)
    {
        await engine.TransferAsync(taskId, dto.UserIds, dto.Comment);
        return Success();
    }

    /// <summary>加签（前加签并入当前节点 / 后加签追加审批）</summary>
    [NoRepeatSubmit]
    [HttpPost("{taskId:long}/addsign")]
    public async Task<ApiResult> AddSign(long taskId, [FromBody] FlowAddSignDto dto)
    {
        await engine.AddSignAsync(taskId, dto.UserIds, dto.Before, dto.Comment);
        return Success();
    }
}

/// <summary>审批实例：查询/详情/撤回</summary>
[ApiController]
[Route("api/v1/sys/flow/instance")]
public class FlowInstanceController(IFlowEngine engine, IFlowQueryService queryService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>实例分页（管理视角）</summary>
    [HasPermission("sys:flow:list")]
    [HttpGet("page")]
    public async Task<ApiResult<PageResult<FlowInstanceDto>>> Page([FromQuery] FlowInstanceQueryDto query) =>
        Success(await queryService.GetInstancePageAsync(query));

    /// <summary>审批详情（含时间线；前端 FlowTimeline 数据源）</summary>
    [Authorize]
    [HttpGet("{id:long}")]
    public async Task<ApiResult<FlowInstanceDetailDto>> Detail(long id) => Success(await queryService.GetDetailAsync(id));

    /// <summary>我的申请分页（我提交的审批单，含进行中与已结束）</summary>
    [Authorize]
    [HttpGet("my")]
    public async Task<ApiResult<PageResult<FlowInstanceDto>>> My([FromQuery] FlowInstanceQueryDto query) =>
        Success(await queryService.GetMySubmitPageAsync(query));

    /// <summary>按业务单据取审批实例（单据详情页嵌入审批信息）</summary>
    [Authorize]
    [HttpGet("by-business")]
    public async Task<ApiResult<FlowInstanceDto?>> ByBusiness([FromQuery] string businessTable, [FromQuery] long businessId) =>
        Success(await queryService.GetByBusinessAsync(businessTable, businessId));

    /// <summary>抄送我的分页</summary>
    [Authorize]
    [HttpGet("cc-me")]
    public async Task<ApiResult<PageResult<FlowCcViewDto>>> CcMe([FromQuery] FlowCcQueryDto query) =>
        Success(await queryService.GetCcMePageAsync(query));

    /// <summary>催办：发起人对运行中实例催促当前审批人（4 小时内仅一次）</summary>
    [Authorize]
    [HttpPost("{id:long}/urge")]
    public async Task<ApiResult> Urge(long id)
    {
        await engine.UrgeAsync(id);
        return Success("已提醒审批人尽快处理");
    }

    /// <summary>撤回（仅发起人、审批尚未开始处理）</summary>
    [NoRepeatSubmit]
    [Authorize]
    [HttpPost("{id:long}/withdraw")]
    public async Task<ApiResult> Withdraw(long id)
    {
        await engine.WithdrawAsync(id);
        return Success();
    }
}

/// <summary>单据-审批流绑定管理（换流程/停用审批，运行时生效）</summary>
[ApiController]
[Route("api/v1/sys/flow/binding")]
public class FlowBindingController(ISysFlowBindingService bindingService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>绑定列表</summary>
    [HasPermission("sys:flow:list")]
    [HttpGet]
    public async Task<ApiResult<List<FlowBindingDto>>> List() => Success(await bindingService.GetListAsync());

    /// <summary>保存绑定（存在则更新；流程编码留空 = 该单据不走审批）</summary>
    [HasPermission("sys:flow:edit")]
    [NoRepeatSubmit]
    [HttpPost]
    public async Task<ApiResult> Save([FromBody] FlowBindingSaveDto dto)
    {
        await bindingService.SaveAsync(dto);
        return Success("绑定已保存");
    }

    /// <summary>删除绑定（回退业务代码默认流程）</summary>
    [HasPermission("sys:flow:edit")]
    [HttpDelete("{id:long}")]
    public async Task<ApiResult> Delete(long id)
    {
        await bindingService.DeleteAsync(id);
        return Success();
    }
}

/// <summary>审批流提交入口（业务单据页调用）</summary>
[ApiController]
[Route("api/v1/sys/flow")]
[Authorize]
public class FlowSubmitController(IFlowEngine engine, ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>提交审批（businessTable + businessId 关联业务单据，variables 供条件分支求值）</summary>
    [NoRepeatSubmit]
    [HttpPost("submit")]
    public async Task<ApiResult<string>> Submit([FromBody] FlowSubmitRequest request) =>
        SuccessId(await engine.SubmitAsync(request), "已提交审批");
}
