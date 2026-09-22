using Microsoft.Extensions.DependencyInjection;
using NetBase.Common.Exceptions;
using NetBase.Model.Entities;
using NetBase.Model.Enums;
using NetBase.Repository.Repositories;
using NetBase.Service.Sys.Flow;
using Xunit;
using Xunit.Abstractions;

namespace NetBase.IntegrationTests;

/// <summary>
/// 审批流引擎语义集成测试（验收基准，抄钉钉）：
/// 依次/会签/或签、条件分支、发起人与空审批人自动通过、转办、撤回、拒绝重提、终态回调、通知。
/// </summary>
[Collection("Integration")]
public class FlowEngineTests
{
    private readonly IntegrationFixture _fixture;
    private readonly IFlowEngine _engine;
    private readonly ISysFlowDefinitionService _definitionService;
    private readonly IRepository<SysFlowInstance> _instanceRepo;
    private readonly IRepository<SysFlowTask> _taskRepo;
    private readonly IRepository<SysUser> _userRepo;
    private readonly ITestOutputHelper _output;

    public FlowEngineTests(IntegrationFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _engine = fixture.GetService<IFlowEngine>();
        _definitionService = fixture.GetService<ISysFlowDefinitionService>();
        _instanceRepo = fixture.GetRepository<SysFlowInstance>();
        _taskRepo = fixture.GetRepository<SysFlowTask>();
        _userRepo = fixture.GetRepository<SysUser>();
    }

    // ---------------- 场景辅助 ----------------

    private async Task<long> CreateUserAsync(string name)
    {
        var user = await _userRepo.InsertAsync(new SysUser
        {
            UserName = IntegrationFixture.Uid("fu"),
            NickName = name,
            Password = "x",
            Status = 1,
            DeptId = 0
        });
        return user.Id;
    }

    private static FlowNode Approval(string code, string name, FlowNodeMode mode, params long[] userIds) =>
        new()
        {
            Code = code,
            Type = FlowNodeType.Approval,
            Name = name,
            Mode = mode,
            Approvers = [new ApproverRule { Type = FlowApproverType.User, UserIds = [.. userIds] }]
        };

    private static FlowGraph Chain(params FlowNode[] nodes)
    {
        var graph = new FlowGraph
        {
            Nodes = [new FlowNode { Code = "start", Type = FlowNodeType.Start, Next = nodes[0].Code }]
        };
        for (var i = 0; i < nodes.Length; i++)
        {
            graph.Nodes.Add(nodes[i]);
            if (i < nodes.Length - 1)
            {
                nodes[i].Next = nodes[i + 1].Code;
            }
        }
        graph.Nodes.Add(new FlowNode { Code = "end", Type = FlowNodeType.End });
        nodes[^1].Next = "end";
        return graph;
    }

    private async Task CreateEnabledFlowAsync(string code, FlowGraph graph)
    {
        var id = await _definitionService.CreateAsync(new FlowDefinitionSaveDto
        {
            FlowCode = code,
            FlowName = $"测试流程-{code}",
            NodeJson = graph.ToJson()
        });
        await _definitionService.EnableAsync(id);
    }

    private async Task<long> SubmitAsync(string flowCode, long businessId, Dictionary<string, object?>? vars = null) =>
        await _engine.SubmitAsync(new FlowSubmitRequest
        {
            FlowCode = flowCode,
            BusinessTable = "test_biz",
            BusinessId = businessId,
            Variables = vars ?? new Dictionary<string, object?>()
        });

    private async Task<SysFlowTask> PendingTaskOfAsync(long instanceId, long userId)
    {
        var task = await _taskRepo.GetFirstAsync(t => t.InstanceId == instanceId
                                                      && t.ApproverUserId == userId
                                                      && t.Status == FlowTaskStatus.Pending);
        Assert.NotNull(task);
        return task!;
    }

    private async Task<SysFlowInstance> InstanceOfAsync(long instanceId) =>
        await _instanceRepo.GetByIdAsync(instanceId)
        ?? throw new InvalidOperationException("实例不存在");

    // ---------------- 语义场景 ----------------

    [Fact]
    public async Task Sequential_BothApprove_ShouldPass()
    {
        var (submitter, a, b) = (await CreateUserAsync("提交人"), await CreateUserAsync("审批A"), await CreateUserAsync("审批B"));
        await CreateEnabledFlowAsync("seq_flow", Chain(Approval("n1", "依次审批", FlowNodeMode.Sequential, a, b)));

        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        var instanceId = await SubmitAsync("seq_flow", 1001);

        // 依次审批：只有 A 有待办，B 处于等待
        var taskA = await PendingTaskOfAsync(instanceId, a);
        var taskB = await _taskRepo.GetFirstAsync(t => t.InstanceId == instanceId && t.ApproverUserId == b);
        Assert.NotNull(taskB);
        Assert.Equal(FlowTaskStatus.Waiting, taskB!.Status);

        IntegrationFixture.SetOperator($"user-{a}", a);
        await _engine.ActAsync(taskA.Id, FlowAction.Approve, "A 同意");

        // B 升为待办
        var taskB2 = await PendingTaskOfAsync(instanceId, b);
        IntegrationFixture.SetOperator($"user-{b}", b);
        await _engine.ActAsync(taskB2.Id, FlowAction.Approve, "B 同意");

        var instance = await InstanceOfAsync(instanceId);
        Assert.Equal(FlowInstanceStatus.Approved, instance.Status);
        Assert.Contains((1001L, FlowInstanceStatus.Approved), FlowTestHandler.Finished);
    }

    [Fact]
    public async Task CounterSign_AllApprove_ShouldPass_AnyReject_ShouldReject()
    {
        var (submitter, a, b) = (await CreateUserAsync("提交人"), await CreateUserAsync("会签A"), await CreateUserAsync("会签B"));
        await CreateEnabledFlowAsync("counter_flow", Chain(Approval("n1", "会签节点", FlowNodeMode.CounterSign, a, b)));

        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        var instanceId = await SubmitAsync("counter_flow", 1002);

        // 会签：A、B 同时有待办；A 通过后流程仍在 B
        var taskA = await PendingTaskOfAsync(instanceId, a);
        var taskB = await PendingTaskOfAsync(instanceId, b);
        IntegrationFixture.SetOperator($"user-{a}", a);
        await _engine.ActAsync(taskA.Id, FlowAction.Approve, "A 同意");
        Assert.Equal(FlowInstanceStatus.Running, (await InstanceOfAsync(instanceId)).Status);

        // B 拒绝 → 实例拒绝
        IntegrationFixture.SetOperator($"user-{b}", b);
        await _engine.ActAsync(taskB.Id, FlowAction.Reject, "预算不足");
        var instance = await InstanceOfAsync(instanceId);
        Assert.Equal(FlowInstanceStatus.Rejected, instance.Status);
        Assert.Contains((1002L, FlowInstanceStatus.Rejected), FlowTestHandler.Finished);
    }

    [Fact]
    public async Task OrSign_FirstAct_ShouldVoidOtherAndPass()
    {
        var (submitter, a, b) = (await CreateUserAsync("提交人"), await CreateUserAsync("或签A"), await CreateUserAsync("或签B"));
        await CreateEnabledFlowAsync("or_flow", Chain(Approval("n1", "或签节点", FlowNodeMode.OrSign, a, b)));

        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        var instanceId = await SubmitAsync("or_flow", 1003);

        var taskA = await PendingTaskOfAsync(instanceId, a);
        IntegrationFixture.SetOperator($"user-{a}", a);
        await _engine.ActAsync(taskA.Id, FlowAction.Approve, "A 先审");

        // 或签定局：B 任务失效，流程通过
        var taskB = await _taskRepo.GetFirstAsync(t => t.InstanceId == instanceId && t.ApproverUserId == b);
        Assert.NotNull(taskB);
        Assert.Equal(FlowTaskStatus.Voided, taskB!.Status);
        Assert.Equal(FlowInstanceStatus.Approved, (await InstanceOfAsync(instanceId)).Status);
    }

    [Fact]
    public async Task ConditionBranch_ShouldRouteByVariable()
    {
        var (submitter, a, b) = (await CreateUserAsync("提交人"), await CreateUserAsync("小额审批"), await CreateUserAsync("大额审批"));
        var nLow = Approval("n_low", "主管审批", FlowNodeMode.OrSign, a);
        var nHigh = Approval("n_high", "总监审批", FlowNodeMode.OrSign, b);
        var graph = new FlowGraph
        {
            Nodes =
            [
                new FlowNode { Code = "start", Type = FlowNodeType.Start, Next = "c1" },
                new FlowNode
                {
                    Code = "c1",
                    Type = FlowNodeType.Condition,
                    Branches =
                    [
                        new FlowBranch
                        {
                            Name = "小额", Priority = 1, Next = "n_low",
                            Conditions = [new FlowCondition
                            {
                                Variable = "amount", Op = "lt",
                                Value = System.Text.Json.JsonSerializer.SerializeToElement(10000)
                            }]
                        },
                        new FlowBranch
                        {
                            Name = "大额", Priority = 2, Next = "n_high",
                            Conditions = [new FlowCondition
                            {
                                Variable = "amount", Op = "gte",
                                Value = System.Text.Json.JsonSerializer.SerializeToElement(10000)
                            }]
                        }
                    ],
                    DefaultNext = "n_high"
                },
                nLow, nHigh,
                new FlowNode { Code = "end", Type = FlowNodeType.End }
            ]
        };
        nLow.Next = "end";
        nHigh.Next = "end";
        await CreateEnabledFlowAsync("cond_flow", graph);

        // 小额：5000 → n_low（A 审）
        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        var lowId = await SubmitAsync("cond_flow", 2001, new Dictionary<string, object?> { ["amount"] = 5000 });
        await PendingTaskOfAsync(lowId, a);
        Assert.Empty(await _taskRepo.GetListAsync(t => t.InstanceId == lowId && t.ApproverUserId == b));

        // 大额：50000 → n_high（B 审）
        var highId = await SubmitAsync("cond_flow", 2002, new Dictionary<string, object?> { ["amount"] = 50000 });
        await PendingTaskOfAsync(highId, b);
        Assert.Empty(await _taskRepo.GetListAsync(t => t.InstanceId == highId && t.ApproverUserId == a));
    }

    [Fact]
    public async Task SubmitterApprover_AndEmptyApprover_ShouldAutoPass()
    {
        // 发起人是审批人 → 自动通过；部门主管未设置（空审批人）→ 自动通过；全流程即时通过
        var submitter = await CreateUserAsync("发起即审批");
        var n1 = new FlowNode
        {
            Code = "n1",
            Type = FlowNodeType.Approval,
            Name = "发起人节点",
            Mode = FlowNodeMode.OrSign,
            Approvers = [new ApproverRule { Type = FlowApproverType.User, UserIds = [submitter] }]
        };
        var n2 = new FlowNode
        {
            Code = "n2",
            Type = FlowNodeType.Approval,
            Name = "空主管节点",
            Mode = FlowNodeMode.OrSign,
            Approvers = [new ApproverRule { Type = FlowApproverType.DeptLeader }] // 发起人 DeptId=0 且无主管
        };
        var graph = new FlowGraph
        {
            Nodes =
            [
                new FlowNode { Code = "start", Type = FlowNodeType.Start, Next = "n1" }, n1, n2,
                new FlowNode { Code = "end", Type = FlowNodeType.End }
            ]
        };
        n1.Next = "n2";
        n2.Next = "end";
        await CreateEnabledFlowAsync("auto_flow", graph);

        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        var instanceId = await SubmitAsync("auto_flow", 3001);

        var instance = await InstanceOfAsync(instanceId);
        Assert.Equal(FlowInstanceStatus.Approved, instance.Status);

        var tasks = await _taskRepo.GetListAsync(t => t.InstanceId == instanceId);
        Assert.All(tasks, t => Assert.Equal(FlowTaskStatus.AutoPassed, t.Status));
        // 发起人审批人自动通过生成任务行；空审批人按设计只记流转记录不生成任务行
        Assert.Contains(tasks, t => t.NodeCode == "n1");
        Assert.DoesNotContain(tasks, t => t.NodeCode == "n2");
    }

    [Fact]
    public async Task Transfer_NewApproverActs_ShouldPass()
    {
        var (submitter, a, c) = (await CreateUserAsync("提交人"), await CreateUserAsync("转出人"), await CreateUserAsync("转入人"));
        await CreateEnabledFlowAsync("transfer_flow", Chain(Approval("n1", "转办节点", FlowNodeMode.OrSign, a)));

        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        var instanceId = await SubmitAsync("transfer_flow", 4001);

        var taskA = await PendingTaskOfAsync(instanceId, a);
        IntegrationFixture.SetOperator($"user-{a}", a);
        await _engine.TransferAsync(taskA.Id, [c], "出差转办");

        // A 任务已转办（重查库），C 生成新待办；C 通过 → 流程通过
        var taskA2 = await _taskRepo.GetByIdAsync(taskA.Id);
        Assert.Equal(FlowTaskStatus.Transferred, taskA2!.Status);
        var taskC = await PendingTaskOfAsync(instanceId, c);
        IntegrationFixture.SetOperator($"user-{c}", c);
        await _engine.ActAsync(taskC.Id, FlowAction.Approve, "C 代审");
        Assert.Equal(FlowInstanceStatus.Approved, (await InstanceOfAsync(instanceId)).Status);
    }

    [Fact]
    public async Task Withdraw_OnlySubmitter_BeforeAnyAct()
    {
        var (submitter, a) = (await CreateUserAsync("撤回发起人"), await CreateUserAsync("撤回审批"));
        await CreateEnabledFlowAsync("withdraw_flow", Chain(Approval("n1", "撤回节点", FlowNodeMode.OrSign, a)));

        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        var instanceId = await SubmitAsync("withdraw_flow", 5001);

        // 非发起人不可撤
        IntegrationFixture.SetOperator($"user-{a}", a);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => _engine.WithdrawAsync(instanceId));
        Assert.Contains("发起人", ex.Message);

        // 发起人撤回 → 撤回终态 + 回调
        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        await _engine.WithdrawAsync(instanceId);

        var instance = await InstanceOfAsync(instanceId);
        Assert.Equal(FlowInstanceStatus.Revoked, instance.Status);
        Assert.Contains((5001L, FlowInstanceStatus.Revoked), FlowTestHandler.Finished);
    }

    [Fact]
    public async Task Reject_ThenResubmit_ShouldCreateNewRunningInstance()
    {
        var (submitter, a) = (await CreateUserAsync("重提发起人"), await CreateUserAsync("重提审批"));
        await CreateEnabledFlowAsync("resubmit_flow", Chain(Approval("n1", "重提节点", FlowNodeMode.OrSign, a)));

        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        var first = await SubmitAsync("resubmit_flow", 6001);

        IntegrationFixture.SetOperator($"user-{a}", a);
        var taskA = await PendingTaskOfAsync(first, a);
        await _engine.ActAsync(taskA.Id, FlowAction.Reject, "材料不全");
        Assert.Equal(FlowInstanceStatus.Rejected, (await InstanceOfAsync(first)).Status);

        // 重提：切回发起人（否则发起人=审批人触发自动通过语义），新实例从头走
        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        var second = await SubmitAsync("resubmit_flow", 6001);
        Assert.NotEqual(first, second);
        Assert.Equal(FlowInstanceStatus.Running, (await InstanceOfAsync(second)).Status);
        await PendingTaskOfAsync(second, a); // 第二次同样到 A
    }

    [Fact]
    public async Task CounterSign_RatioReached_ShouldPassAndVoidRemainder()
    {
        var submitter = await CreateUserAsync("比例提交人");
        var (a, b, c) = (await CreateUserAsync("会签甲"), await CreateUserAsync("会签乙"), await CreateUserAsync("会签丙"));
        var n1 = new FlowNode
        {
            Code = "n1", Type = FlowNodeType.Approval, Name = "半数会签", Mode = FlowNodeMode.CounterSign,
            ApproveRatio = 50,
            Approvers = [new ApproverRule { Type = FlowApproverType.User, UserIds = [a, b, c] }]
        };
        var graph = new FlowGraph
        {
            Nodes = [new FlowNode { Code = "start", Type = FlowNodeType.Start, Next = "n1" }, n1,
                     new FlowNode { Code = "end", Type = FlowNodeType.End }]
        };
        n1.Next = "end";
        await CreateEnabledFlowAsync("ratio_flow", graph);

        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        var instanceId = await SubmitAsync("ratio_flow", 9400);

        // 3 人各拿到待办；甲乙同意（2/3 ≥ ceil(3×50%)=2）→ 节点通过，丙的待办作废
        var tA = await PendingTaskOfAsync(instanceId, a);
        var tB = await PendingTaskOfAsync(instanceId, b);
        var tC = await PendingTaskOfAsync(instanceId, c);
        IntegrationFixture.SetOperator($"user-{a}", a);
        await _engine.ActAsync(tA.Id, FlowAction.Approve, "甲同意");
        IntegrationFixture.SetOperator($"user-{b}", b);
        await _engine.ActAsync(tB.Id, FlowAction.Approve, "乙同意");

        var final = await InstanceOfAsync(instanceId);
        Assert.Equal(FlowInstanceStatus.Approved, final.Status); // 达比例即整体通过

        var taskC = await _taskRepo.GetByIdAsync(tC.Id);
        Assert.Equal(FlowTaskStatus.Voided, taskC!.Status); // 丙的待办被作废
    }

    [Fact]
    public async Task PositionApprover_SubmitterDeptScope_ShouldFilterByDept()
    {
        // 岗位+发起人所在部门范围：同岗位、不同部门的人不应被解析
        var deptRepo = _fixture.GetRepository<SysDept>();
        var posRepo = _fixture.GetRepository<SysPosition>();
        var upRepo = _fixture.GetRepository<SysUserPosition>();
        var userRepo = _fixture.GetRepository<SysUser>();

        var deptA = deptRepo.InsertAsync(new SysDept
        {
            DeptName = IntegrationFixture.Uid("部门A"), DeptCode = IntegrationFixture.Uid("da"), Status = 1
        }).GetAwaiter().GetResult();
        var deptB = deptRepo.InsertAsync(new SysDept
        {
            DeptName = IntegrationFixture.Uid("部门B"), DeptCode = IntegrationFixture.Uid("db"), Status = 1
        }).GetAwaiter().GetResult();

        var (submitter, holderA, holderB) = (
            await CreateUserAsync("范围提交人"), await CreateUserAsync("A部经理"), await CreateUserAsync("B部经理"));

        // 两人主属部门不同，但担任同一岗位
        await userRepo.UpdateWhereAsync(x => x.Id == holderA, x => new SysUser { DeptId = deptA.Id });
        await userRepo.UpdateWhereAsync(x => x.Id == holderB, x => new SysUser { DeptId = deptB.Id });
        var position = posRepo.InsertAsync(new SysPosition
        {
            PositionCode = IntegrationFixture.Uid("scope"), PositionName = "部门经理", Status = 1
        }).GetAwaiter().GetResult();
        await upRepo.InsertAsync(new SysUserPosition { UserId = holderA, PositionId = position.Id });
        await upRepo.InsertAsync(new SysUserPosition { UserId = holderB, PositionId = position.Id });

        var n1 = new FlowNode
        {
            Code = "n1", Type = FlowNodeType.Approval, Name = "本部门经理审批", Mode = FlowNodeMode.OrSign,
            Approvers = [new ApproverRule
            {
                Type = FlowApproverType.Position,
                PositionCodes = [position.PositionCode],
                Scope = "submitterDept"
            }]
        };
        var graph = new FlowGraph
        {
            Nodes = [new FlowNode { Code = "start", Type = FlowNodeType.Start, Next = "n1" }, n1,
                     new FlowNode { Code = "end", Type = FlowNodeType.End }]
        };
        n1.Next = "end";
        await CreateEnabledFlowAsync("pos_scope_flow", graph);

        // 提交人属于部门 A → 只有 A 部经理拿到待办
        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        await userRepo.UpdateWhereAsync(x => x.Id == submitter, x => new SysUser { DeptId = deptA.Id });
        var instanceId = await SubmitAsync("pos_scope_flow", 9300);
        await PendingTaskOfAsync(instanceId, holderA);
        Assert.Empty(await _taskRepo.GetListAsync(t => t.InstanceId == instanceId && t.ApproverUserId == holderB));
    }

    [Fact]
    public async Task PositionApprover_ShouldResolveUsers()
    {
        // 岗位管审批：节点审批人按岗位解析
        var (submitter, holder) = (await CreateUserAsync("岗位提交人"), await CreateUserAsync("岗位持有者"));
        var posRepo = _fixture.GetRepository<SysPosition>();
        var upRepo = _fixture.GetRepository<SysUserPosition>();
        var position = posRepo.InsertAsync(new SysPosition
        {
            PositionCode = IntegrationFixture.Uid("pos"), PositionName = "财务经理", Status = 1
        }).GetAwaiter().GetResult();
        await upRepo.InsertAsync(new SysUserPosition { UserId = holder, PositionId = position.Id });

        var n1 = new FlowNode
        {
            Code = "n1", Type = FlowNodeType.Approval, Name = "岗位审批", Mode = FlowNodeMode.OrSign,
            Approvers = [new ApproverRule { Type = FlowApproverType.Position, PositionCodes = [position.PositionCode] }]
        };
        var graph = new FlowGraph
        {
            Nodes = [new FlowNode { Code = "start", Type = FlowNodeType.Start, Next = "n1" }, n1,
                     new FlowNode { Code = "end", Type = FlowNodeType.End }]
        };
        n1.Next = "end";
        await CreateEnabledFlowAsync("pos_flow", graph);

        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        var instanceId = await SubmitAsync("pos_flow", 9001);
        await PendingTaskOfAsync(instanceId, holder); // 岗位持有者拿到待办
    }

    [Fact]
    public async Task ReturnToNode_ShouldReReview_AndGateRepeatAutoPass()
    {
        var (submitter, a, b) = (await CreateUserAsync("退回提交人"), await CreateUserAsync("一审A"), await CreateUserAsync("复审B"));
        var n1 = Approval("n1", "初审", FlowNodeMode.OrSign, a);
        var n2 = Approval("n2", "复审", FlowNodeMode.OrSign, b);
        var graph = new FlowGraph
        {
            Nodes = [new FlowNode { Code = "start", Type = FlowNodeType.Start, Next = "n1" }, n1, n2,
                     new FlowNode { Code = "end", Type = FlowNodeType.End }]
        };
        n1.Next = "n2";
        n2.Next = "end";
        await CreateEnabledFlowAsync("return_flow", graph);

        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        var instanceId = await SubmitAsync("return_flow", 9100);

        // A 同意初审 → B 复审；B 用「驳回至节点」退回 n1 → A 重审
        var tA = await PendingTaskOfAsync(instanceId, a);
        IntegrationFixture.SetOperator($"user-{a}", a);
        await _engine.ActAsync(tA.Id, FlowAction.Approve, "初审通过");
        var tB = await PendingTaskOfAsync(instanceId, b);
        IntegrationFixture.SetOperator($"user-{b}", b);
        await _engine.ActAsync(tB.Id, FlowAction.Return, "材料有问题，退回初审", "n1");

        Assert.Equal(FlowInstanceStatus.Running, (await InstanceOfAsync(instanceId)).Status);

        // A 重新审 → 通过后 B 重新拿到复审待办（退回后关闭重复审批人自动通过）
        var tA2 = await PendingTaskOfAsync(instanceId, a);
        IntegrationFixture.SetOperator($"user-{a}", a);
        await _engine.ActAsync(tA2.Id, FlowAction.Approve, "已修改，初审通过");
        var tB2 = await PendingTaskOfAsync(instanceId, b);
        IntegrationFixture.SetOperator($"user-{b}", b);
        await _engine.ActAsync(tB2.Id, FlowAction.Approve, "复审通过");

        Assert.Equal(FlowInstanceStatus.Approved, (await InstanceOfAsync(instanceId)).Status);
    }

    [Fact]
    public async Task ReturnToStart_SubmitterResubmits_ShouldRewalkFromEntry()
    {
        var (submitter, a) = (await CreateUserAsync("重提发起人"), await CreateUserAsync("重提审批"));
        await CreateEnabledFlowAsync("return_start_flow", Chain(Approval("n1", "唯一审批", FlowNodeMode.OrSign, a)));

        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        var instanceId = await SubmitAsync("return_start_flow", 9200);
        var tA = await PendingTaskOfAsync(instanceId, a);
        IntegrationFixture.SetOperator($"user-{a}", a);
        await _engine.ActAsync(tA.Id, FlowAction.Return, "退回发起人", "start");

        // 发起人收到「重新提交」待办，其同意后流程从入口重走（同一实例）
        var taskRepo = _fixture.GetRepository<SysFlowTask>();
        var resubmit = await taskRepo.GetFirstAsync(t => t.InstanceId == instanceId && t.NodeCode == "start");
        Assert.NotNull(resubmit);
        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        await _engine.ActAsync(resubmit!.Id, FlowAction.Approve, "修改后重新提交");
        await PendingTaskOfAsync(instanceId, a);
        Assert.Equal(FlowInstanceStatus.Running, (await InstanceOfAsync(instanceId)).Status);
    }

    [Fact]
    public async Task Definition_CreateWithoutCode_ShouldAutoGenerateNumericCode()
    {
        // 未传编码 → 系统生成数字编号（≥100）；传编码 → 兼容沿用
        var autoId = await _definitionService.CreateAsync(new FlowDefinitionSaveDto
        {
            FlowName = "自动编号流程", NodeJson = Chain(Approval("n1", "审", FlowNodeMode.OrSign, 1)).ToJson()
        });
        var auto = await _definitionService.GetDetailAsync(autoId);
        Assert.NotNull(auto);
        Assert.True(int.TryParse(auto!.FlowCode, out var numeric) && numeric >= 100,
            $"自动编号应为 ≥100 的数字，实际 {auto.FlowCode}");

        var manualId = await _definitionService.CreateAsync(new FlowDefinitionSaveDto
        {
            FlowCode = "custom_code", FlowName = "显式编码流程",
            NodeJson = Chain(Approval("n1", "审", FlowNodeMode.OrSign, 1)).ToJson(), Category = "测试类"
        });
        var manual = await _definitionService.GetDetailAsync(manualId);
        Assert.Equal("custom_code", manual!.FlowCode);
        Assert.Equal("测试类", manual.Category);
    }

    [Fact]
    public async Task Binding_ShouldOverrideDefault_AndEmptyBindingShouldReject()
    {
        var bindingRepo = _fixture.GetRepository<SysFlowBinding>();
        var (submitter, a) = (await CreateUserAsync("绑定发起人"), await CreateUserAsync("绑定审批"));
        await CreateEnabledFlowAsync("bind_default", Chain(Approval("n1", "默认流", FlowNodeMode.OrSign, a)));
        await CreateEnabledFlowAsync("bind_other", Chain(Approval("n1", "绑定流", FlowNodeMode.OrSign, a)));

        try
        {
            // 绑定指向 bind_other：提交 test_biz 时即使代码传 bind_default 也走 bind_other
            await bindingRepo.InsertAsync(new SysFlowBinding
            {
                BusinessTable = "test_biz", FlowCode = "bind_other", Remark = "覆盖默认"
            });

            IntegrationFixture.SetOperator($"user-{submitter}", submitter);
            var inst1 = await SubmitAsync("bind_default", 8001);
            var instance1 = await InstanceOfAsync(inst1);
            Assert.Equal("bind_other", instance1.FlowCode); // 绑定覆盖默认

            // 绑定清空（FlowCode=null）：提交被拒
            var binding = await bindingRepo.GetFirstAsync(b => b.BusinessTable == "test_biz");
            binding!.FlowCode = null;
            await bindingRepo.UpdateAsync(binding);
            var ex = await Assert.ThrowsAsync<BusinessException>(() => SubmitAsync("bind_default", 8002));
            Assert.Contains("未绑定审批流", ex.Message);

            // 删除绑定：回退代码默认 bind_default
            await bindingRepo.DeleteAsync(binding);
            var inst3 = await SubmitAsync("bind_default", 8003);
            Assert.Equal("bind_default", (await InstanceOfAsync(inst3)).FlowCode);
        }
        finally
        {
            var leftovers = await bindingRepo.GetListAsync(b => b.BusinessTable == "test_biz");
            foreach (var b in leftovers)
            {
                await bindingRepo.DeleteAsync(b);
            }
        }
    }

    [Fact]
    public async Task Notify_OnTaskCreated_ShouldPushToApprover()
    {
        var (submitter, a) = (await CreateUserAsync("通知发起人"), await CreateUserAsync("通知审批"));
        await CreateEnabledFlowAsync("notify_flow", Chain(Approval("n1", "通知节点", FlowNodeMode.OrSign, a)));

        var notifications = _fixture.GetService<FlowTestNotifications>();
        notifications.Clear();

        IntegrationFixture.SetOperator($"user-{submitter}", submitter);
        await SubmitAsync("notify_flow", 7001);

        Assert.Contains(notifications.Pushes, p =>
            p.Users.Contains(a) && p.Notice.Title.Contains("待审批") && p.Notice.BizType == "flow_task");
    }
}
