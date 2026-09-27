using System.Text.Json;
using NetBase.Common.Exceptions;
using NetBase.Common.Realtime;
using NetBase.Model.Entities;
using NetBase.Model.Enums;
using NetBase.Repository.Auditing;
using NetBase.Repository.Repositories;
using NetBase.Common.Results;

namespace NetBase.Service.Sys.Flow;

/// <summary>提交审批请求</summary>
public class FlowSubmitRequest
{
    /// <summary>流程编码</summary>
    public string FlowCode { get; set; } = string.Empty;

    /// <summary>业务表名（如 biz_expense）</summary>
    public string BusinessTable { get; set; } = string.Empty;

    /// <summary>业务单据ID</summary>
    public long BusinessId { get; set; }

    /// <summary>流程变量（条件分支求值依据，如 amount/deptId）</summary>
    public Dictionary<string, object?> Variables { get; set; } = new();

    /// <summary>发起人自选审批人（键=审批节点编码，节点配置 SubmitterChoice 时使用）</summary>
    public Dictionary<string, List<long>> Choices { get; set; } = new();
}

/// <summary>审批动作</summary>
public enum FlowAction
{
    /// <summary>同意</summary>
    Approve,

    /// <summary>拒绝（终态）</summary>
    Reject,

    /// <summary>驳回至指定节点重审（实例保持运行中）</summary>
    Return
}

/// <summary>
/// 审批流引擎：提交、审批、转办、加签、撤回与节点游走。
/// 语义基准（抄钉钉）：会签全员通过/任一拒即拒；或签任一人定局；依次逐人生成；
/// 发起人/重复审批人自动通过；审批人为空自动通过；拒绝即终态，重提走新实例；
/// 前加签并入当前节点共同把关，后加签在本节点通过后追加审批再前进。
/// 每个公开操作一个事务；内部方法不得再开事务（UseTran 不可嵌套）。
/// </summary>
public interface IFlowEngine
{
    /// <summary>提交审批（创建实例并驱动至首个需人工审批的节点）</summary>
    Task<long> SubmitAsync(FlowSubmitRequest request);

    /// <summary>审批（同意/拒绝/驳回，仅任务归属人；驳回时 returnNodeCode 指定退回目标）</summary>
    Task ActAsync(long taskId, FlowAction action, string? comment, string? returnNodeCode = null);

    /// <summary>转办：当前任务作废，转给目标人</summary>
    Task TransferAsync(long taskId, List<long> targetUserIds, string? comment);

    /// <summary>加签：before=true 并入当前节点共同把关；false 本节点通过后追加审批再前进</summary>
    Task AddSignAsync(long taskId, List<long> targetUserIds, bool before, string? comment);

    /// <summary>撤回：仅发起人、且尚无人处理时</summary>
    Task WithdrawAsync(long instanceId);

    /// <summary>催办：发起人对运行中实例催促当前审批人（4 小时内仅一次）</summary>
    Task UrgeAsync(long instanceId);

    /// <summary>超时自动提醒：待办超过 remindDays 天未处理的审批人收到站内信催办（返回提醒条数）</summary>
    Task<int> RemindOverdueAsync(int remindDays);
}

public class FlowEngine(
    IRepository<SysFlowDefinition> definitionRepository,
    IRepository<SysFlowBinding> bindingRepository,
    IRepository<SysFlowInstance> instanceRepository,
    IRepository<SysFlowTask> taskRepository,
    IRepository<SysFlowRecord> recordRepository,
    IRepository<SysUser> userRepository,
    IRepository<SysRole> roleRepository,
    IRepository<SysUserRole> userRoleRepository,
    IRepository<SysPosition> positionRepository,
    IRepository<SysUserPosition> userPositionRepository,
    IRepository<SysFlowCc> ccRepository,
    IRepository<SysFlowDelegate> delegateRepository,
    ApproverResolver approverResolver,
    INotifyService notifyService,
    IOperatorProvider operatorProvider,
    IEnumerable<IFlowBusinessHandler> handlers) : IFlowEngine
{
    private const string TaskBizType = "flow_task";
    private const string InstanceBizType = "flow_instance";

    // ---------------- 提交 ----------------

    public async Task<long> SubmitAsync(FlowSubmitRequest request)
    {
        var operatorId = operatorProvider.OperatorUserId
                         ?? throw new BusinessException("未登录无法提交审批", ErrorCodes.FLOW_NOT_AUTHENTICATED);
        var operatorName = operatorProvider.OperatorName ?? "system";

        // 有效流程编码：绑定表记录优先（FlowCode 空 = 该单据不走审批）；无记录回退业务代码默认值
        var binding = await bindingRepository.GetFirstAsync(b => b.BusinessTable == request.BusinessTable);
        var flowCode = binding == null
            ? request.FlowCode
            : !string.IsNullOrWhiteSpace(binding.FlowCode)
                ? binding.FlowCode
                : throw new BusinessException(
                    "该单据未绑定审批流，请联系管理员在「流程管理 → 单据绑定」中配置", ErrorCodes.FLOW_BINDING_MISSING);

        var definition = (await definitionRepository.GetListAsync(
                d => d.FlowCode == flowCode && d.Status == 1))
            .OrderByDescending(d => d.FlowVersion).FirstOrDefault()
            ?? throw new BusinessException($"流程 {flowCode} 未配置或未启用", ErrorCodes.FLOW_DEF_NOT_ENABLED);

        var graph = FlowGraph.Parse(definition.NodeJson)
                    ?? throw new BusinessException($"流程 {request.FlowCode} 节点配置无效", ErrorCodes.FLOW_DEF_NODES_INVALID);
        ValidateGraph(graph);

        var submitter = await userRepository.GetByIdAsync(operatorId);
        var handler = ResolveHandler(request.BusinessTable);
        var summary = handler == null
            ? $"流程{request.FlowCode} 单据{request.BusinessId}"
            : await handler.GetSummaryAsync(request.BusinessId);

        var instance = new SysFlowInstance
        {
            FlowCode = flowCode,
            DefinitionId = definition.Id,
            BusinessTable = request.BusinessTable,
            BusinessId = request.BusinessId.ToString(),
            Summary = summary,
            Status = FlowInstanceStatus.Running,
            VariablesJson = BuildVariablesJson(request),
            SubmitterId = operatorId,
            SubmitterName = operatorName,
            SubmitterDeptId = submitter?.DeptId ?? 0,
            SubmitTime = DateTime.Now
        };

        await instanceRepository.TransactionAsync(async () =>
        {
            await instanceRepository.InsertAsync(instance);
            await RecordAsync(instance.Id, "submit", null, operatorId, operatorName, "提交审批");
            await EnterNodeAsync(instance, graph, graph.Entry);
            return true;
        });
        return instance.Id;
    }

    // ---------------- 审批 / 转办 / 加签 / 撤回 ----------------

    public async Task ActAsync(long taskId, FlowAction action, string? comment, string? returnNodeCode = null)
    {
        var userId = operatorProvider.OperatorUserId ?? throw new BusinessException("未登录", ErrorCodes.FLOW_NOT_AUTHENTICATED);
        var task = await taskRepository.GetByIdAsync(taskId)
                   ?? throw new BusinessException("审批任务不存在", ErrorCodes.FLOW_TASK_NOT_FOUND);
        EnsureOwner(task, userId);
        var instance = await instanceRepository.GetByIdAsync(task.InstanceId)
                       ?? throw new BusinessException("流程实例不存在", ErrorCodes.FLOW_INSTANCE_NOT_FOUND);
        EnsureRunning(instance);
        var graph = await LoadGraphAsync(instance);
        var operatorName = task.ApproverName;

        if (action == FlowAction.Return)
        {
            if (string.IsNullOrWhiteSpace(returnNodeCode))
            {
                throw new BusinessException("驳回请选择退回目标节点", ErrorCodes.FLOW_RETURN_TARGET_REQUIRED);
            }
            if (returnNodeCode != "start" && !graph.Index.TryGetValue(returnNodeCode, out _))
            {
                throw new BusinessException("退回目标节点不存在", ErrorCodes.FLOW_RETURN_TARGET_NOT_FOUND);
            }
            if (returnNodeCode == task.NodeCode)
            {
                throw new BusinessException("不能驳回至当前节点自身", ErrorCodes.FLOW_RETURN_TARGET_SELF);
            }
        }

        await instanceRepository.TransactionAsync(async () =>
        {
            task.Status = action == FlowAction.Approve ? FlowTaskStatus.Approved
                        : action == FlowAction.Return ? FlowTaskStatus.Returned
                        : FlowTaskStatus.Rejected;
            task.Comment = comment;
            task.ActTime = DateTime.Now;
            await taskRepository.UpdateAsync(task);
            await RecordAsync(instance.Id, action == FlowAction.Approve ? "approve"
                        : action == FlowAction.Return ? "return" : "reject",
                task.NodeCode, userId, operatorName, comment);

            if (action == FlowAction.Reject)
            {
                await VoidNodePendingTasksAsync(instance.Id, task.NodeCode);
                await FinishCoreAsync(instance, FlowInstanceStatus.Rejected, $"已被 {operatorName} 拒绝");
                return true;
            }

            if (action == FlowAction.Return)
            {
                await ReturnToNodeAsync(instance, graph, task.NodeCode, returnNodeCode!, operatorName, comment);
                return true;
            }

            // 同意：按模式判定节点是否完成（会签等他人或达比例 / 依次升下一位）
            var actedNode = task.NodeCode != null && graph.Index.TryGetValue(task.NodeCode, out var defNode)
                ? defNode
                : null; // 后加签追加节点不在图索引内（或签，无比例语义）
            if (await CompleteOrWaitAsync(instance, task, actedNode))
            {
                if (task.NodeCode == "start")
                {
                    // 驳回至发起人后的重提：从流程入口重走
                    await EnterNodeAsync(instance, graph, graph.Entry);
                    return true;
                }
                await AdvanceFromNodeAsync(instance, graph, task.NodeCode);
            }
            return true;
        });
    }

    /// <summary>
    /// 驳回至节点：作废实例全部待办，目标节点重新生成审批任务（实例保持运行中）；
    /// 目标为 start 时给发起人生成「重新提交」待办，其提交后从流程入口重走。
    /// 退回后本实例的后续节点需重新审批（重复审批人自动通过仅对退回前的历史生效）。
    /// </summary>
    private async Task ReturnToNodeAsync(SysFlowInstance instance, FlowGraph graph,
        string fromNodeCode, string returnNodeCode, string operatorName, string? comment)
    {
        await VoidInstanceTasksAsync(instance.Id);

        if (returnNodeCode == "start")
        {
            await InsertTaskAsync(instance, "start", "重新提交", FlowNodeMode.OrSign,
                instance.SubmitterId, instance.SubmitterName);
            instance.CurrentNodeCode = "start";
            await instanceRepository.UpdateAsync(instance);
            await RecordAsync(instance.Id, "return", "start", 0, "system",
                $"退回发起人修改后重新提交（{operatorName}）");
            await notifyService.PushToUsersAsync([instance.SubmitterId], new NoticePayload
            {
                MsgType = 3,
                Title = $"【待重新提交】{instance.Summary}",
                Content = $"审批被驳回，请修改后重新提交。驳回人：{operatorName}，意见：{comment ?? "无"}",
                SenderName = operatorName,
                BizType = TaskBizType,
                BizId = instance.Id.ToString()
            });
            return;
        }

        // 退回至审批节点：重新解析审批人并生成待办（模式沿用节点定义）
        var node = graph.Index[returnNodeCode];
        var approvers = await ApplyDelegationAsync(await approverResolver.ResolveAsync(
            node.Approvers ?? [], instance.SubmitterDeptId, ReadChoices(instance, node.Code)));
        foreach (var approver in approvers)
        {
            await InsertTaskAsync(instance, node.Code, node.Name ?? node.Code, node.Mode,
                approver.UserId, approver.UserName);
        }
        instance.CurrentNodeCode = node.Code;
        await instanceRepository.UpdateAsync(instance);
        await RecordAsync(instance.Id, "return", node.Code, 0, "system",
            $"退回至「{node.Name ?? node.Code}」重新审批（{operatorName}）");
        if (approvers.Count > 0)
        {
            await NotifyTaskUsersAsync(instance, approvers.Select(a => a.UserId).ToList());
        }
    }

    public async Task TransferAsync(long taskId, List<long> targetUserIds, string? comment)
    {
        var userId = operatorProvider.OperatorUserId ?? throw new BusinessException("未登录", ErrorCodes.FLOW_NOT_AUTHENTICATED);
        var task = await taskRepository.GetByIdAsync(taskId)
                   ?? throw new BusinessException("审批任务不存在", ErrorCodes.FLOW_TASK_NOT_FOUND);
        EnsureOwner(task, userId);
        var instance = await instanceRepository.GetByIdAsync(task.InstanceId)
                       ?? throw new BusinessException("流程实例不存在", ErrorCodes.FLOW_INSTANCE_NOT_FOUND);
        EnsureRunning(instance);

        var targets = await ValidUsersAsync(targetUserIds);
        if (targets.Count == 0)
        {
            throw new BusinessException("转办目标人无效", ErrorCodes.FLOW_TRANSFER_TARGET_INVALID);
        }

        await instanceRepository.TransactionAsync(async () =>
        {
            task.Status = FlowTaskStatus.Transferred;
            task.Comment = comment;
            task.ActTime = DateTime.Now;
            await taskRepository.UpdateAsync(task);

            foreach (var (id, name) in targets)
            {
                await InsertTaskAsync(instance, task.NodeCode, task.NodeName, task.NodeMode, id, name, task.Sequence);
            }
            await RecordAsync(instance.Id, "transfer", task.NodeCode, userId, task.ApproverName,
                comment, ToJson(new { targets }));
            await NotifyTaskUsersAsync(instance, targets.Select(t => t.id).ToList());
            return true;
        });
    }

    public async Task AddSignAsync(long taskId, List<long> targetUserIds, bool before, string? comment)
    {
        var userId = operatorProvider.OperatorUserId ?? throw new BusinessException("未登录", ErrorCodes.FLOW_NOT_AUTHENTICATED);
        var task = await taskRepository.GetByIdAsync(taskId)
                   ?? throw new BusinessException("审批任务不存在", ErrorCodes.FLOW_TASK_NOT_FOUND);
        EnsureOwner(task, userId);
        var instance = await instanceRepository.GetByIdAsync(task.InstanceId)
                       ?? throw new BusinessException("流程实例不存在", ErrorCodes.FLOW_INSTANCE_NOT_FOUND);
        EnsureRunning(instance);

        var targets = await ValidUsersAsync(targetUserIds);
        if (targets.Count == 0)
        {
            throw new BusinessException("加签人无效", ErrorCodes.FLOW_ADDSIGN_TARGET_INVALID);
        }

        await instanceRepository.TransactionAsync(async () =>
        {
            if (before)
            {
                // 前加签：并入当前节点共同把关（本节点须加签人也处理后才能完成）
                foreach (var (id, name) in targets)
                {
                    await InsertTaskAsync(instance, task.NodeCode, task.NodeName, task.NodeMode, id, name, task.Sequence);
                }
            }
            else
            {
                // 后加签：本节点通过后先进追加节点（或签，任一人审过即过）再按原链前进
                var append = new FlowNode
                {
                    Code = $"addsign_{Guid.NewGuid():N}",
                    Type = FlowNodeType.Approval,
                    Name = "加签审批",
                    Mode = FlowNodeMode.OrSign,
                    Approvers = [new ApproverRule { Type = FlowApproverType.User, UserIds = targets.Select(t => t.id).ToList() }]
                };
                await AppendNodeAsync(instance, task.NodeCode, append);
            }
            await RecordAsync(instance.Id, "addsign", task.NodeCode, userId, task.ApproverName,
                comment, ToJson(new { before, targets }));
            return true;
        });
    }

    public async Task WithdrawAsync(long instanceId)
    {
        var userId = operatorProvider.OperatorUserId ?? throw new BusinessException("未登录", ErrorCodes.FLOW_NOT_AUTHENTICATED);
        var instance = await instanceRepository.GetByIdAsync(instanceId)
                       ?? throw new BusinessException("流程实例不存在", ErrorCodes.FLOW_INSTANCE_NOT_FOUND);
        if (instance.SubmitterId != userId)
        {
            throw new BusinessException("仅发起人可撤回", ErrorCodes.FLOW_WITHDRAW_FORBIDDEN);
        }
        EnsureRunning(instance);

        // 撤回窗口：尚无任何已处理的任务（首审批节点未有人动作）
        var acted = await taskRepository.AnyAsync(
            t => t.InstanceId == instanceId
                 && t.Status != FlowTaskStatus.Pending
                 && t.Status != FlowTaskStatus.Waiting);
        if (acted)
        {
            throw new BusinessException("审批已开始处理，无法撤回", ErrorCodes.FLOW_WITHDRAW_PROCESSING);
        }

        await instanceRepository.TransactionAsync(async () =>
        {
            await VoidInstanceTasksAsync(instanceId);
            await RecordAsync(instanceId, "withdraw", instance.CurrentNodeCode,
                userId, instance.SubmitterName, "发起人撤回");
            await FinishCoreAsync(instance, FlowInstanceStatus.Revoked, "发起人撤回");
            return true;
        });
    }

    /// <summary>催办：发起人对运行中实例催促当前审批人（4 小时内仅一次）</summary>
    public async Task UrgeAsync(long instanceId)
    {
        var userId = operatorProvider.OperatorUserId ?? throw new BusinessException("未登录", ErrorCodes.FLOW_NOT_AUTHENTICATED);
        var instance = await instanceRepository.GetByIdAsync(instanceId)
                       ?? throw new BusinessException("流程实例不存在", ErrorCodes.FLOW_INSTANCE_NOT_FOUND);
        if (instance.SubmitterId != userId)
        {
            throw new BusinessException("仅发起人可催办", ErrorCodes.FLOW_URGE_FORBIDDEN);
        }
        EnsureRunning(instance);

        // 限频：4 小时内已催办过则拒绝（查催办流转记录）
        var recent = await recordRepository.AnyAsync(r => r.InstanceId == instanceId
            && r.Action == "urge" && r.ActTime >= DateTime.Now.AddHours(-4));
        if (recent)
        {
            throw new BusinessException("4 小时内已催办过，请稍后再试", ErrorCodes.FLOW_URGE_RATE_LIMITED);
        }

        var pending = await taskRepository.GetListAsync(
            t => t.InstanceId == instanceId && t.Status == FlowTaskStatus.Pending);
        if (pending.Count == 0)
        {
            throw new BusinessException("当前没有待处理的审批任务", ErrorCodes.FLOW_URGE_NO_PENDING_TASK);
        }

        var approverIds = pending.Select(t => t.ApproverUserId).Distinct().ToList();
        await RecordAsync(instanceId, "urge", instance.CurrentNodeCode, userId,
            instance.SubmitterName, "发起人催办");
        await NotifyTaskUsersAsync(instance, approverIds, "【催办】");
    }

    /// <summary>超时自动提醒：待办创建超过 remindDays 天未处理，按任务提醒审批人（每天每任务最多一次）</summary>
    public async Task<int> RemindOverdueAsync(int remindDays)
    {
        if (remindDays <= 0)
        {
            return 0;
        }
        var deadline = DateTime.Now.AddDays(-remindDays);
        var overdue = await taskRepository.GetListAsync(
            t => t.Status == FlowTaskStatus.Pending && t.CreateTime <= deadline);
        if (overdue.Count == 0)
        {
            return 0;
        }

        var reminded = 0;
        foreach (var group in overdue.GroupBy(t => t.InstanceId))
        {
            // 每实例每天最多提醒一次（查催办/提醒记录防重复轰炸）
            var todayReminded = await recordRepository.AnyAsync(
                r => r.InstanceId == group.Key && r.Action == "urge" && r.ActTime >= DateTime.Now.Date);
            if (todayReminded)
            {
                continue;
            }

            var instance = await instanceRepository.GetByIdAsync(group.Key);
            if (instance == null || instance.Status != FlowInstanceStatus.Running)
            {
                continue;
            }

            var userIds = group.Select(t => t.ApproverUserId).Distinct().ToList();
            await RecordAsync(instance.Id, "urge", instance.CurrentNodeCode, 0, "system",
                $"待办超过 {remindDays} 天未处理，系统自动提醒");
            await notifyService.PushToUsersAsync(userIds, new NoticePayload
            {
                MsgType = 3,
                Title = $"【超时提醒】{instance.Summary}",
                Content = $"该审批已超过 {remindDays} 天未处理，请尽快处理",
                SenderName = "system",
                BizType = TaskBizType,
                BizId = instance.Id.ToString()
            });
            reminded += userIds.Count;
        }
        return reminded;
    }

    // ---------------- 节点游走（调用方须已在事务内） ----------------

    /// <summary>节点完成后的前进入口：先走该节点的后加签队列，再按图前进</summary>
    private async Task AdvanceFromNodeAsync(SysFlowInstance instance, FlowGraph graph, string nodeCode)
    {
        var append = await PopAppendNodeAsync(instance, nodeCode);
        if (append != null)
        {
            append.Next = graph.Index[nodeCode].Next; // 加签完成后接原链路
            await EnterNodeAsync(instance, graph, append.Code, append);
            return;
        }
        await EnterNodeAsync(instance, graph, graph.Index[nodeCode].Next);
    }

    /// <summary>
    /// 进入节点并驱动：start/抄送/条件/全员自动通过的审批节点连续推进，
    /// 暂停于首个需人工审批的节点，或到达终态。extraNode 用于后加签的追加节点（不在图索引内）。
    /// </summary>
    private async Task EnterNodeAsync(SysFlowInstance instance, FlowGraph graph, string? nodeCode, FlowNode? extraNode = null)
    {
        var visited = new HashSet<string>();
        var current = nodeCode;

        while (!string.IsNullOrEmpty(current))
        {
            if (!visited.Add(current))
            {
                throw new BusinessException("流程节点配置成环，请检查流程设计", ErrorCodes.FLOW_DEF_CYCLE);
            }
            var node = current == extraNode?.Code ? extraNode : graph.Index[current];

            switch (node.Type)
            {
                case FlowNodeType.Start:
                    current = node.Next;
                    continue;

                case FlowNodeType.Cc:
                    await HandleCcAsync(instance, node);
                    current = node.Next;
                    continue;

                case FlowNodeType.Condition:
                    current = ResolveCondition(instance, node);
                    continue;

                case FlowNodeType.Approval:
                {
                    var waitHuman = await EnterApprovalAsync(instance, node);
                    if (waitHuman)
                    {
                        return; // 暂停等待人工审批
                    }
                    // 全员自动通过：继续前进
                    var append = await PopAppendNodeAsync(instance, node.Code);
                    if (append != null)
                    {
                        append.Next = node.Next;
                        await EnterNodeAsync(instance, graph, append.Code, append);
                        return;
                    }
                    current = node.Next;
                    continue;
                }

                case FlowNodeType.End:
                    await FinishCoreAsync(instance, FlowInstanceStatus.Approved, "审批通过");
                    return;
            }
        }

        // 链路走完未显式指向 end：视为通过（宽容处理，避免配置疏漏卡死业务）
        await FinishCoreAsync(instance, FlowInstanceStatus.Approved, "流程结束");
    }

    /// <summary>
    /// 进入审批节点：解析审批人 → 自动通过过滤（发起人/重复审批人/为空）→ 生成任务。
    /// 返回 true=暂停等待人工；false=全员自动已推进。
    /// </summary>
    private async Task<bool> EnterApprovalAsync(SysFlowInstance instance, FlowNode node)
    {
        var approvers = await ApplyDelegationAsync(await approverResolver.ResolveAsync(
            node.Approvers ?? [], instance.SubmitterDeptId, ReadChoices(instance, node.Code)));

        // 本实例已产生审批结论的人（跨节点去重：重复审批人自动通过）；
        // 发生过「驳回至节点」后关闭该规则——退回重审时后续节点须重新审批
        var hasReturn = await recordRepository.AnyAsync(
            r => r.InstanceId == instance.Id && r.Action == "return");
        var actedUserIds = hasReturn
            ? new HashSet<long>()
            : (await taskRepository.GetListAsync(
                t => t.InstanceId == instance.Id
                     && (t.Status == FlowTaskStatus.Approved || t.Status == FlowTaskStatus.AutoPassed)))
            .Select(t => t.ApproverUserId).ToHashSet();

        var needHuman = new List<FlowApprover>();
        foreach (var approver in approvers)
        {
            var auto = approver.UserId == instance.SubmitterId || (!hasReturn && actedUserIds.Contains(approver.UserId));
            await InsertTaskAsync(instance, node.Code, node.Name ?? node.Code, node.Mode,
                approver.UserId, approver.UserName,
                status: auto ? FlowTaskStatus.AutoPassed : FlowTaskStatus.Pending);
            if (auto)
            {
                await RecordAsync(instance.Id, "auto", node.Code, approver.UserId, approver.UserName,
                    approver.UserId == instance.SubmitterId ? "审批人为发起人，自动通过" : "重复审批人，自动通过");
            }
            else
            {
                needHuman.Add(approver);
            }
        }

        if (approvers.Count == 0)
        {
            await RecordAsync(instance.Id, "auto", node.Code, 0, "system", "审批人为空，自动通过");
        }

        if (needHuman.Count == 0)
        {
            return false; // 全员自动，继续推进
        }

        // 依次审批：仅首人生成待办，其余 Waiting
        if (node.Mode == FlowNodeMode.Sequential)
        {
            needHuman = needHuman.OrderBy(a => IndexOfApprover(approvers, a.UserId)).ToList();
            var first = needHuman[0];
            var pending = await taskRepository.GetListAsync(
                t => t.InstanceId == instance.Id && t.NodeCode == node.Code && t.Status == FlowTaskStatus.Pending);
            foreach (var t in pending.Where(t => t.ApproverUserId != first.UserId))
            {
                t.Status = FlowTaskStatus.Waiting;
                await taskRepository.UpdateAsync(t);
            }
        }

        instance.CurrentNodeCode = node.Code;
        await instanceRepository.UpdateAsync(instance);
        await NotifyTaskUsersAsync(instance, needHuman.Select(a => a.UserId).ToList());
        return true;
    }

    private static int IndexOfApprover(List<FlowApprover> approvers, long userId)
    {
        for (var i = 0; i < approvers.Count; i++)
        {
            if (approvers[i].UserId == userId)
            {
                return i;
            }
        }
        return int.MaxValue;
    }

    /// <summary>按节点模式判定：本任务处理后节点是否完成（true=完成可前进，false=等待）；node 为节点定义（追加节点为 null）</summary>
    private async Task<bool> CompleteOrWaitAsync(SysFlowInstance instance, SysFlowTask task, FlowNode? node = null)
    {
        var nodeTasks = await taskRepository.GetListAsync(
            t => t.InstanceId == instance.Id && t.NodeCode == task.NodeCode);

        switch (task.NodeMode)
        {
            case FlowNodeMode.OrSign:
                // 任一人处理即定局：其余待办/等待全部作废
                foreach (var other in nodeTasks.Where(t => t.Id != task.Id
                             && (t.Status == FlowTaskStatus.Pending || t.Status == FlowTaskStatus.Waiting)))
                {
                    other.Status = FlowTaskStatus.Voided;
                    other.ActTime = DateTime.Now;
                    other.Comment = "或签已定局，任务失效";
                    await taskRepository.UpdateAsync(other);
                }
                return true;

            case FlowNodeMode.CounterSign:
            {
                // 参与人数 = 待出结论的任务（同意/自动通过/待办/等待）；转办由新任务接替、失效不参与
                var eligible = nodeTasks.Count(t =>
                    t.Status is FlowTaskStatus.Approved or FlowTaskStatus.AutoPassed
                        or FlowTaskStatus.Pending or FlowTaskStatus.Waiting);
                var approvedCount = nodeTasks.Count(t =>
                    t.Status is FlowTaskStatus.Approved or FlowTaskStatus.AutoPassed);

                var ratio = node is { ApproveRatio: > 0 and < 100 } ? node.ApproveRatio : 100;
                if (ratio < 100)
                {
                    var required = (int)Math.Ceiling(eligible * ratio / 100.0);
                    if (approvedCount >= required)
                    {
                        // 达到比例：作废剩余待办，节点通过
                        foreach (var other in nodeTasks.Where(t => t.Id != task.Id
                                     && (t.Status == FlowTaskStatus.Pending || t.Status == FlowTaskStatus.Waiting)))
                        {
                            other.Status = FlowTaskStatus.Voided;
                            other.ActTime = DateTime.Now;
                            other.Comment = "会签已达通过比例，任务失效";
                            await taskRepository.UpdateAsync(other);
                        }
                        return true;
                    }
                    return false; // 未达比例：等待
                }

                // 100%：全员有结论（同意/自动/转办替换/失效）才算过；任一拒绝在 Reject 分支已终止
                return nodeTasks.All(t =>
                    t.Status is FlowTaskStatus.Approved or FlowTaskStatus.AutoPassed
                        or FlowTaskStatus.Voided or FlowTaskStatus.Transferred);
            }

            case FlowNodeMode.Sequential:
            {
                var next = nodeTasks
                    .Where(t => t.Status == FlowTaskStatus.Waiting)
                    .OrderBy(t => t.Sequence).ThenBy(t => t.Id)
                    .FirstOrDefault();
                if (next != null)
                {
                    next.Status = FlowTaskStatus.Pending;
                    await taskRepository.UpdateAsync(next);
                    await NotifyTaskUsersAsync(instance, [next.ApproverUserId]);
                    return false;
                }
                return true;
            }

            default:
                return true;
        }
    }

    // ---------------- 内部辅助 ----------------

    private string ResolveCondition(SysFlowInstance instance, FlowNode node)
    {
        var variables = JsonDocument.Parse(string.IsNullOrEmpty(instance.VariablesJson) ? "{}" : instance.VariablesJson).RootElement;
        var hit = (node.Branches ?? [])
            .OrderBy(b => b.Priority)
            .FirstOrDefault(b => ConditionEvaluator.Evaluate(b.Conditions, variables));
        var next = hit?.Next ?? node.DefaultNext;
        return next ?? throw new BusinessException(
            $"条件节点 {node.Name ?? node.Code} 无命中分支且未配置默认分支", ErrorCodes.FLOW_CONDITION_NO_BRANCH);
    }

    private async Task HandleCcAsync(SysFlowInstance instance, FlowNode node)
    {
        var ccUsers = await ResolveCcUsersAsync(node);
        if (ccUsers.Count > 0)
        {
            await notifyService.PushToUsersAsync(ccUsers, new NoticePayload
            {
                MsgType = 3,
                Title = $"【抄送】{instance.Summary}",
                Content = $"{instance.SubmitterName} 提交的审批已抄送给您",
                SenderName = instance.SubmitterName,
                BizType = InstanceBizType,
                BizId = instance.Id.ToString()
            });
        }
        foreach (var ccUserId in ccUsers)
        {
            await ccRepository.InsertAsync(new SysFlowCc
            {
                InstanceId = instance.Id,
                NodeCode = node.Code,
                UserId = ccUserId
            });
        }
        await RecordAsync(instance.Id, "cc", node.Code, instance.SubmitterId, instance.SubmitterName,
            $"抄送 {ccUsers.Count} 人");
    }

    /// <summary>抄送目标：指定用户 ∪ 角色下的启用用户</summary>
    private async Task<List<long>> ResolveCcUsersAsync(FlowNode node)
    {
        var result = new List<long>(node.CcUserIds ?? []);
        if (node.CcRoleCodes is { Count: > 0 })
        {
            var roleIds = roleRepository.GetList(r => node.CcRoleCodes.Contains(r.RoleCode) && r.Status == 1)
                .Select(r => r.Id).ToList();
            if (roleIds.Count > 0)
            {
                result.AddRange((await userRoleRepository.GetListAsync(ur => roleIds.Contains(ur.RoleId)))
                    .Select(ur => ur.UserId));
            }
        }
        return result.Distinct().ToList();
    }

    /// <summary>
    /// 应用审批委托：审批人存在生效中的委托（时间段覆盖当前）时，任务改由代理人审批。
    /// 仅影响新生成的任务；已生成的待办不迁移。委托给自己无效。
    /// </summary>
    private async Task<List<FlowApprover>> ApplyDelegationAsync(List<FlowApprover> approvers)
    {
        if (approvers.Count == 0)
        {
            return approvers;
        }
        var now = DateTime.Now;
        var result = new List<FlowApprover>(approvers.Count);
        foreach (var approver in approvers)
        {
            var delegation = await delegateRepository.GetFirstAsync(
                x => x.DelegatorId == approver.UserId && x.Status == 1
                     && x.StartTime <= now && x.EndTime > now);
            result.Add(delegation != null && delegation.AgentId != approver.UserId
                ? new FlowApprover(delegation.AgentId, delegation.AgentName)
                : approver);
        }
        return result;
    }

    /// <summary>读取发起人自选审批人（节点级 __choice_{code}）</summary>
    private static List<long> ReadChoices(SysFlowInstance instance, string nodeCode)
    {
        var variables = JsonDocument.Parse(string.IsNullOrEmpty(instance.VariablesJson) ? "{}" : instance.VariablesJson).RootElement;
        if (variables.ValueKind != JsonValueKind.Object
            || !variables.TryGetProperty($"__choice_{nodeCode}", out var arr)
            || arr.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        return arr.EnumerateArray().Select(v => (long)v.GetDouble()).ToList();
    }

    private static void ValidateGraph(FlowGraph graph)
    {
        if (graph.Index.Count == 0 || !graph.Index.ContainsKey(graph.Entry))
        {
            throw new BusinessException("流程缺少入口节点", ErrorCodes.FLOW_DEF_NO_ENTRY);
        }
        var dup = graph.Nodes.GroupBy(n => n.Code).FirstOrDefault(g => g.Count() > 1);
        if (dup != null)
        {
            throw new BusinessException($"节点编码重复: {dup.Key}", ErrorCodes.FLOW_DEF_NODE_CODE_DUP);
        }
        foreach (var node in graph.Nodes)
        {
            if (node.Type == FlowNodeType.Approval && (node.Approvers == null || node.Approvers.Count == 0))
            {
                throw new BusinessException($"审批节点 {node.Name ?? node.Code} 未配置审批人规则", ErrorCodes.FLOW_DEF_APPROVER_MISSING);
            }
        }
    }

    private async Task<FlowGraph> LoadGraphAsync(SysFlowInstance instance)
    {
        var definition = await definitionRepository.GetByIdAsync(instance.DefinitionId)
                         ?? throw new BusinessException("流程定义已删除", ErrorCodes.FLOW_DEF_DELETED);
        return FlowGraph.Parse(definition.NodeJson)
               ?? throw new BusinessException("流程节点配置无效", ErrorCodes.FLOW_DEF_NODES_INVALID);
    }

    private static string BuildVariablesJson(FlowSubmitRequest request)
    {
        var vars = new Dictionary<string, object?>(request.Variables);
        foreach (var (nodeCode, userIds) in request.Choices)
        {
            vars[$"__choice_{nodeCode}"] = userIds;
        }
        return JsonSerializer.Serialize(vars, FlowGraph.JsonOpts);
    }

    private async Task InsertTaskAsync(SysFlowInstance instance, string nodeCode, string nodeName,
        FlowNodeMode mode, long userId, string userName,
        int sequence = 0, FlowTaskStatus status = FlowTaskStatus.Pending)
    {
        await taskRepository.InsertAsync(new SysFlowTask
        {
            InstanceId = instance.Id,
            NodeCode = nodeCode,
            NodeName = nodeName,
            NodeMode = mode,
            ApproverUserId = userId,
            ApproverName = userName,
            Status = status,
            Sequence = sequence
        });
    }

    private async Task RecordAsync(long instanceId, string action, string? nodeCode,
        long operatorId, string operatorName, string? comment, string? extraJson = null)
    {
        await recordRepository.InsertAsync(new SysFlowRecord
        {
            InstanceId = instanceId,
            NodeCode = nodeCode,
            Action = action,
            OperatorId = operatorId,
            OperatorName = operatorName,
            Comment = comment,
            ExtraJson = extraJson,
            ActTime = DateTime.Now
        });
    }

    private async Task NotifyTaskUsersAsync(SysFlowInstance instance, List<long> userIds, string titlePrefix = "【待审批】")
    {
        if (userIds.Count == 0)
        {
            return;
        }
        await notifyService.PushToUsersAsync(userIds, new NoticePayload
        {
            MsgType = 3,
            Title = $"{titlePrefix}{instance.Summary}",
            Content = $"{instance.SubmitterName} 提交的审批等待您处理",
            SenderName = instance.SubmitterName,
            BizType = TaskBizType,
            BizId = instance.Id.ToString()
        });
    }

    /// <summary>终态落库 + 回调业务 + 通知发起人（调用方须已在事务内）</summary>
    private async Task FinishCoreAsync(SysFlowInstance instance, FlowInstanceStatus status, string? comment = null)
    {
        instance.Status = status;
        instance.EndTime = DateTime.Now;
        await instanceRepository.UpdateAsync(instance);

        if (status is FlowInstanceStatus.Rejected or FlowInstanceStatus.Revoked or FlowInstanceStatus.Voided)
        {
            await VoidInstanceTasksAsync(instance.Id);
        }

        await RecordAsync(instance.Id, status switch
        {
            FlowInstanceStatus.Approved => "approve",
            FlowInstanceStatus.Rejected => "reject",
            FlowInstanceStatus.Revoked => "withdraw",
            _ => "void"
        }, null, 0, "system", comment ?? status.ToString());

        var handler = ResolveHandler(instance.BusinessTable);
        if (handler != null)
        {
            await handler.OnFinishedAsync(long.Parse(instance.BusinessId), status);
        }

        await notifyService.PushToUsersAsync([instance.SubmitterId], new NoticePayload
        {
            MsgType = 3,
            Title = status == FlowInstanceStatus.Approved
                ? $"【已通过】{instance.Summary}"
                : $"【未通过】{instance.Summary}",
            Content = comment ?? "您的审批单已有结果",
            SenderName = "system",
            BizType = InstanceBizType,
            BizId = instance.Id.ToString()
        });
    }

    private async Task VoidNodePendingTasksAsync(long instanceId, string nodeCode) =>
        await VoidTasksAsync(await taskRepository.GetListAsync(
            t => t.InstanceId == instanceId && t.NodeCode == nodeCode
                 && (t.Status == FlowTaskStatus.Pending || t.Status == FlowTaskStatus.Waiting)));

    private async Task VoidInstanceTasksAsync(long instanceId) =>
        await VoidTasksAsync(await taskRepository.GetListAsync(
            t => t.InstanceId == instanceId
                 && (t.Status == FlowTaskStatus.Pending || t.Status == FlowTaskStatus.Waiting)));

    private async Task VoidTasksAsync(List<SysFlowTask> tasks)
    {
        foreach (var t in tasks)
        {
            t.Status = FlowTaskStatus.Voided;
            t.ActTime = DateTime.Now;
            await taskRepository.UpdateAsync(t);
        }
    }

    private async Task AppendNodeAsync(SysFlowInstance instance, string nodeCode, FlowNode append)
    {
        var queue = string.IsNullOrEmpty(instance.AppendNodesJson)
            ? new Dictionary<string, FlowNode>()
            : JsonSerializer.Deserialize<Dictionary<string, FlowNode>>(instance.AppendNodesJson, FlowGraph.JsonOpts)!;
        queue[nodeCode] = append;
        instance.AppendNodesJson = JsonSerializer.Serialize(queue, FlowGraph.JsonOpts);
        await instanceRepository.UpdateAsync(instance);
    }

    private async Task<FlowNode?> PopAppendNodeAsync(SysFlowInstance instance, string nodeCode)
    {
        if (string.IsNullOrEmpty(instance.AppendNodesJson))
        {
            return null;
        }
        var queue = JsonSerializer.Deserialize<Dictionary<string, FlowNode>>(instance.AppendNodesJson, FlowGraph.JsonOpts)!;
        if (!queue.Remove(nodeCode, out var append))
        {
            return null;
        }
        instance.AppendNodesJson = queue.Count == 0 ? null : JsonSerializer.Serialize(queue, FlowGraph.JsonOpts);
        await instanceRepository.UpdateAsync(instance);
        return append;
    }

    private async Task<List<(long id, string name)>> ValidUsersAsync(List<long>? userIds)
    {
        var ids = (userIds ?? []).Distinct().ToList();
        var users = await userRepository.GetListAsync(u => ids.Contains(u.Id) && u.Status == 1);
        return users.Select(u => (u.Id, u.NickName ?? u.UserName)).ToList();
    }

    private void EnsureOwner(SysFlowTask task, long userId)
    {
        if (task.ApproverUserId != userId)
        {
            throw new BusinessException("仅任务归属人可处理", ErrorCodes.FLOW_TASK_OWNER_ONLY);
        }
        if (task.Status != FlowTaskStatus.Pending)
        {
            throw new BusinessException("该任务已处理或已失效", ErrorCodes.FLOW_TASK_ALREADY_HANDLED);
        }
    }

    private static void EnsureRunning(SysFlowInstance instance)
    {
        if (instance.Status != FlowInstanceStatus.Running)
        {
            throw new BusinessException("流程已结束", ErrorCodes.FLOW_INSTANCE_FINISHED);
        }
    }

    /// <summary>按业务表名解析业务回调（"*" 为通配 Handler，测试/日志类通用回调用）</summary>
    private IFlowBusinessHandler? ResolveHandler(string businessTable) =>
        handlers.FirstOrDefault(h => h.BusinessTable == businessTable)
        ?? handlers.FirstOrDefault(h => h.BusinessTable == "*");

    private static string ToJson(object o) => JsonSerializer.Serialize(o, FlowGraph.JsonOpts);
}
