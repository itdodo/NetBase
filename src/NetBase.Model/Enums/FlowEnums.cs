namespace NetBase.Model.Enums;

/// <summary>流程实例状态</summary>
public enum FlowInstanceStatus
{
    /// <summary>审批中</summary>
    Running = 1,

    /// <summary>已通过</summary>
    Approved = 2,

    /// <summary>已拒绝（驳回退发起人）</summary>
    Rejected = 3,

    /// <summary>已撤回（发起人撤回）</summary>
    Revoked = 4,

    /// <summary>已作废（管理员终止）</summary>
    Voided = 5
}

/// <summary>审批任务状态</summary>
public enum FlowTaskStatus
{
    /// <summary>待审批</summary>
    Pending = 1,

    /// <summary>已同意</summary>
    Approved = 2,

    /// <summary>已拒绝</summary>
    Rejected = 3,

    /// <summary>已转办（原任务失效，转给新人）</summary>
    Transferred = 4,

    /// <summary>自动通过（审批人为发起人/重复审批人/审批人为空）</summary>
    AutoPassed = 5,

    /// <summary>已失效（或签出局/撤回/流程终止）</summary>
    Voided = 6,

    /// <summary>等待中（依次审批的后续签人，前序完成后升为待办）</summary>
    Waiting = 7
}

/// <summary>审批节点多人审批模式</summary>
public enum FlowNodeMode
{
    /// <summary>会签：全员同意才通过，任一拒绝即拒绝</summary>
    CounterSign = 1,

    /// <summary>或签：任一人处理即定局</summary>
    OrSign = 2,

    /// <summary>依次审批：按顺序逐人处理</summary>
    Sequential = 3
}

/// <summary>审批人类型（DSL 配置）</summary>
public enum FlowApproverType
{
    /// <summary>指定成员</summary>
    User = 1,

    /// <summary>指定角色</summary>
    Role = 2,

    /// <summary>部门主管（deptId=0 时取发起人所在部门）</summary>
    DeptLeader = 3,

    /// <summary>发起人自选（提交时指定）</summary>
    SubmitterChoice = 4
}
