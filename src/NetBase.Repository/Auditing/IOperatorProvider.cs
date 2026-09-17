namespace NetBase.Repository.Auditing;

/// <summary>
/// 当前操作人提供者：SqlSugar 审计 AOP 借此自动填充 CreateBy/UpdateBy。
/// Api 层实现从 HttpContext 解析（认证未接入时回退 system）。
/// </summary>
public interface IOperatorProvider
{
    /// <summary>当前操作人名称；无登录上下文为 null（由 AOP 回退 system）</summary>
    string? OperatorName { get; }

    /// <summary>当前操作人用户ID；无登录上下文为 null（字段级变更审计记录 sys_change_log.UserId）</summary>
    long? OperatorUserId { get; }
}
