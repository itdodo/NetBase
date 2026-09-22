using System.Text.Json;
using System.Text.Json.Serialization;
using NetBase.Model.Enums;

namespace NetBase.Service.Sys.Flow;

/// <summary>
/// 流程节点树 DSL（sys_flow_definition.NodeJson 的规范结构）。
/// 设计器产物经适配层转换为本结构存储；引擎只认本模型，与设计器解耦。
/// 链路：节点经 Next 串联；条件节点经分支（按 Priority 升序求值，首个真分支命中，全不中走 DefaultNext）。
/// </summary>
public class FlowGraph
{
    /// <summary>全部节点（编码唯一）</summary>
    public List<FlowNode> Nodes { get; set; } = [];

    /// <summary>入口节点编码（惯例 "start"）</summary>
    public string Entry { get; set; } = "start";

    private Dictionary<string, FlowNode>? _index;

    /// <summary>节点索引（编码 → 节点）</summary>
    [JsonIgnore]
    public Dictionary<string, FlowNode> Index => _index ??= Nodes.ToDictionary(n => n.Code);

    public static FlowGraph? Parse(string json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<FlowGraph>(json, JsonOpts);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All),
        // 设计器产出的雪花ID一律为字符串（前端防精度丢失），读取时宽容转数值
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}

/// <summary>流程节点</summary>
public class FlowNode
{
    /// <summary>节点编码（实例内唯一）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>节点类型：start/approval/condition/cc/end</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FlowNodeType Type { get; set; }

    /// <summary>节点名称（审批节点必填，待办与时间线展示）</summary>
    public string? Name { get; set; }

    /// <summary>后继节点编码（条件节点忽略此字段）</summary>
    public string? Next { get; set; }

    /// <summary>审批模式（approval 节点）</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FlowNodeMode Mode { get; set; } = FlowNodeMode.OrSign;

    /// <summary>
    /// 会签通过比例（百分比 1-100，默认 100=全员通过；仅会签模式生效）：
    /// 同意数（含自动通过）达到 ceil(参与人数 × 比例%) 即节点通过，剩余待办作废。
    /// </summary>
    public int ApproveRatio { get; set; } = 100;

    /// <summary>审批人规则（approval 节点，多规则取并集）</summary>
    public List<ApproverRule>? Approvers { get; set; }

    /// <summary>驳回动作（approval 节点）：backToSubmitter-退发起人重提（默认）/ terminate-直接终止</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FlowRejectAction RejectAction { get; set; } = FlowRejectAction.BackToSubmitter;

    /// <summary>条件分支（condition 节点，按 Priority 升序求值）</summary>
    public List<FlowBranch>? Branches { get; set; }

    /// <summary>全不命中时的后继（condition 节点，为空则视为流程终止异常）</summary>
    public string? DefaultNext { get; set; }

    /// <summary>抄送目标（cc 节点：用户ID）</summary>
    public List<long>? CcUserIds { get; set; }

    /// <summary>抄送目标角色编码（cc 节点，解析为用户并集）</summary>
    public List<string>? CcRoleCodes { get; set; }
}

/// <summary>节点类型</summary>
public enum FlowNodeType
{
    Start,
    Approval,
    Condition,
    Cc,
    End
}

/// <summary>审批人规则</summary>
public class ApproverRule
{
    /// <summary>审批人类型</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FlowApproverType Type { get; set; }

    /// <summary>指定成员ID（Type=User）</summary>
    public List<long>? UserIds { get; set; }

    /// <summary>角色编码（Type=Role）</summary>
    public List<string>? RoleCodes { get; set; }

    /// <summary>岗位编码（Type=Position）</summary>
    public List<string>? PositionCodes { get; set; }

    /// <summary>
    /// 岗位审批范围（Type=Position 生效）：company-全公司（默认）/
    /// submitterDept-发起人所在部门（岗位+用户主属部门双重过滤）
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>部门ID（Type=DeptLeader；0/null=发起人所在部门）</summary>
    public long? DeptId { get; set; }
}

/// <summary>条件分支</summary>
public class FlowBranch
{
    /// <summary>分支名称（展示用）</summary>
    public string? Name { get; set; }

    /// <summary>优先级（小者优先求值）</summary>
    public int Priority { get; set; }

    /// <summary>命中条件组（组内 AND）</summary>
    public List<FlowCondition> Conditions { get; set; } = [];

    /// <summary>命中后的后继节点编码</summary>
    public string Next { get; set; } = string.Empty;
}

/// <summary>单个条件：变量 操作符 值</summary>
public class FlowCondition
{
    /// <summary>流程变量名（对应实例 VariablesJson 的键）</summary>
    public string Variable { get; set; } = string.Empty;

    /// <summary>操作符：eq/ne/gt/gte/lt/lte/in/contains</summary>
    public string Op { get; set; } = "eq";

    /// <summary>比较值（in 为数组，其余单值）</summary>
    public JsonElement? Value { get; set; }
}

/// <summary>驳回动作</summary>
public enum FlowRejectAction
{
    /// <summary>退发起人重提（实例拒绝终态，重提走新实例）</summary>
    BackToSubmitter,

    /// <summary>直接终止</summary>
    Terminate
}
