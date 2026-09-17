using NetBase.Model.Enums;

namespace NetBase.Service.Sys.Flow;

/// <summary>
/// 审批流业务回调：业务单据接入审批流的唯一契约（按业务表名注册，DI 注册 IEnumerable 注入引擎）。
/// 业务侧实现本接口 + 提交时调用 FlowEngine.SubmitAsync，即可获得完整审批能力——单据代码零审批逻辑。
/// 注意：关联键是业务表名而非流程编码——流程编号系统自增且绑定可运行时更换，按流程编码关联会失联。
/// </summary>
public interface IFlowBusinessHandler
{
    /// <summary>关联的业务表名（与提交时的 BusinessTable 一致，如 biz_expense）</summary>
    string BusinessTable { get; }

    /// <summary>待办标题（如"张三的报销单 ¥1,200"；未注册 Handler 时回退"流程{FlowCode} 单据{BusinessId}"）</summary>
    Task<string> GetSummaryAsync(long businessId);

    /// <summary>流程终态回调（通过/拒绝/撤回/作废），业务在此回写单据状态。实现内自包事务勿过长</summary>
    Task OnFinishedAsync(long businessId, FlowInstanceStatus finalStatus);
}
