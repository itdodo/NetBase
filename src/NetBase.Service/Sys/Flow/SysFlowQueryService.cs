using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Entities;
using NetBase.Model.Enums;
using NetBase.Repository.Auditing;
using NetBase.Repository.Repositories;
using SqlSugar;

namespace NetBase.Service.Sys.Flow;

/// <summary>审批流查询：我的待办/已办、实例分页、审批详情（时间线）</summary>
public interface IFlowQueryService
{
    /// <summary>我的待办分页</summary>
    Task<PageResult<FlowTaskViewDto>> GetTodoPageAsync(FlowTaskQueryDto query);

    /// <summary>我的已办分页</summary>
    Task<PageResult<FlowTaskViewDto>> GetDonePageAsync(FlowTaskQueryDto query);

    /// <summary>我的待办数（顶栏角标）</summary>
    Task<int> GetTodoCountAsync();

    /// <summary>实例分页（管理视角）</summary>
    Task<PageResult<FlowInstanceDto>> GetInstancePageAsync(FlowInstanceQueryDto query);

    /// <summary>业务单据的审批实例（单据详情页嵌入审批信息用）</summary>
    Task<FlowInstanceDto?> GetByBusinessAsync(string businessTable, long businessId);

    /// <summary>我的申请分页（我提交的审批实例，含进行中与已结束）</summary>
    Task<PageResult<FlowInstanceDto>> GetMySubmitPageAsync(FlowInstanceQueryDto query);

    /// <summary>抄送我的分页（我在抄送对象里的审批实例）</summary>
    Task<PageResult<FlowCcViewDto>> GetCcMePageAsync(FlowCcQueryDto query);

    /// <summary>审批统计：汇总 + 按流程 + 按审批人（时间范围过滤提交时间）</summary>
    Task<FlowStatsDto> GetStatisticsAsync(FlowStatsQueryDto query);

    /// <summary>审批详情：实例 + 时间线（流转记录 + 任务态融合）</summary>
    Task<FlowInstanceDetailDto> GetDetailAsync(long instanceId);
}

public class SysFlowQueryService(
    IRepository<SysFlowInstance> instanceRepository,
    IRepository<SysFlowCc> ccRepository,
    ISqlSugarClient db,
    IOperatorProvider operatorProvider) : IFlowQueryService
{
    public Task<PageResult<FlowTaskViewDto>> GetTodoPageAsync(FlowTaskQueryDto query) =>
        QueryTaskPageAsync(query, onlyTodo: true);

    public Task<PageResult<FlowTaskViewDto>> GetDonePageAsync(FlowTaskQueryDto query) =>
        QueryTaskPageAsync(query, onlyTodo: false);

    public async Task<int> GetTodoCountAsync()
    {
        var userId = operatorProvider.OperatorUserId ?? 0;
        return (int)await db.Queryable<SysFlowTask>()
            .Where(t => t.ApproverUserId == userId && t.Status == FlowTaskStatus.Pending)
            .CountAsync();
    }

    private async Task<PageResult<FlowTaskViewDto>> QueryTaskPageAsync(FlowTaskQueryDto query, bool onlyTodo)
    {
        var userId = operatorProvider.OperatorUserId ?? 0;
        RefAsync<int> total = 0;

        // 状态与排序条件按分支拆开——SqlSugar 对表达式内三元（引用宿主变量）翻译不稳定
        var query2 = db.Queryable<SysFlowTask>()
            .InnerJoin<SysFlowInstance>((t, i) => t.InstanceId == i.Id)
            .Where((t, i) => t.ApproverUserId == userId);
        var keyword = query.Keyword?.Trim();
        var flowCode = query.FlowCode?.Trim();
        query2 = query2
            .WhereIF(!string.IsNullOrWhiteSpace(keyword), (t, i) =>
                i.Summary.Contains(keyword!) || i.SubmitterName.Contains(keyword!))
            .WhereIF(!string.IsNullOrWhiteSpace(flowCode), (t, i) => i.FlowCode == flowCode);
        query2 = onlyTodo
            ? query2.Where((t, i) => t.Status == FlowTaskStatus.Pending)
            : query2.Where((t, i) => t.Status != FlowTaskStatus.Pending && t.Status != FlowTaskStatus.Waiting);
        query2 = onlyTodo
            ? query2.OrderBy((t, i) => i.SubmitTime, OrderByType.Asc)
            : query2.OrderBy((t, i) => t.ActTime, OrderByType.Desc);

        var rows = await query2
            .Select((t, i) => new FlowTaskViewDto
            {
                TaskId = t.Id,
                InstanceId = i.Id,
                NodeName = t.NodeName,
                Summary = i.Summary,
                FlowCode = i.FlowCode,
                BusinessTable = i.BusinessTable,
                BusinessId = i.BusinessId,
                SubmitterName = i.SubmitterName,
                SubmitTime = i.SubmitTime,
                ActTime = t.ActTime,
                ActResult = t.Status == FlowTaskStatus.Approved ? "同意"
                    : t.Status == FlowTaskStatus.Rejected ? "拒绝"
                    : t.Status == FlowTaskStatus.Transferred ? "转办" : null
            })
            .ToPageListAsync(query.PageIndex, query.PageSize, total);

        return PageResult<FlowTaskViewDto>.Of(rows, total, query.PageIndex, query.PageSize);
    }

    public Task<PageResult<FlowInstanceDto>> GetMySubmitPageAsync(FlowInstanceQueryDto query)
    {
        query.Submitter = null; // 我的申请固定按当前用户过滤，忽略提交人筛选
        return GetInstancePageCoreAsync(query, mineOnly: true);
    }

    public Task<PageResult<FlowInstanceDto>> GetInstancePageAsync(FlowInstanceQueryDto query) =>
        GetInstancePageCoreAsync(query, mineOnly: false);

    private async Task<PageResult<FlowInstanceDto>> GetInstancePageCoreAsync(FlowInstanceQueryDto query, bool mineOnly)
    {
        var submitter = query.Submitter?.Trim();
        var predicate = Expressionable.Create<SysFlowInstance>()
            .AndIF(!string.IsNullOrWhiteSpace(query.FlowCode), x => x.FlowCode == query.FlowCode)
            .AndIF(!string.IsNullOrWhiteSpace(submitter), x => x.SubmitterName.Contains(submitter!))
            .AndIF(query.Status.HasValue, x => (int)x.Status == query.Status!.Value)
            .AndIF(mineOnly, x => x.SubmitterId == (operatorProvider.OperatorUserId ?? 0))
            .ToExpression();
        var page = await instanceRepository.GetPageListAsync(predicate, query);
        return PageResult<FlowInstanceDto>.Of(
            page.Items.Select(ToInstanceDto).ToList(), page.Total, page.PageIndex, page.PageSize);
    }

    public async Task<PageResult<FlowCcViewDto>> GetCcMePageAsync(FlowCcQueryDto query)
    {
        var userId = operatorProvider.OperatorUserId ?? 0;
        var ccKeyword = query.Keyword?.Trim();
        var ccFlowCode = query.FlowCode?.Trim();
        RefAsync<int> total = 0;

        var rows = await db.Queryable<SysFlowCc>()
            .InnerJoin<SysFlowInstance>((c, i) => c.InstanceId == i.Id)
            .Where((c, i) => c.UserId == userId)
            .WhereIF(!string.IsNullOrWhiteSpace(ccKeyword), (c, i) =>
                i.Summary.Contains(ccKeyword!) || i.SubmitterName.Contains(ccKeyword!))
            .WhereIF(!string.IsNullOrWhiteSpace(ccFlowCode), (c, i) => i.FlowCode == ccFlowCode)
            .WhereIF(query.Status.HasValue, (c, i) => (int)i.Status == query.Status!.Value)
            .OrderBy((c, i) => c.CreateTime, OrderByType.Desc)
            .Select((c, i) => new FlowCcViewDto
            {
                InstanceId = i.Id,
                Summary = i.Summary,
                FlowCode = i.FlowCode,
                BusinessTable = i.BusinessTable,
                BusinessId = i.BusinessId,
                Status = (int)i.Status,
                SubmitterName = i.SubmitterName,
                CcTime = c.CreateTime
            })
            .ToPageListAsync(query.PageIndex, query.PageSize, total);

        return PageResult<FlowCcViewDto>.Of(rows, total, query.PageIndex, query.PageSize);
    }

    public async Task<FlowStatsDto> GetStatisticsAsync(FlowStatsQueryDto query)
    {
        var begin = query.BeginTime ?? new DateTime(2000, 1, 1);
        var end = query.EndTime ?? new DateTime(2099, 1, 1);
        var args = new SugarParameter[] { new("@b", begin), new("@e", end) };

        // 汇总（实例级；超期待办为运行中实例的 3 天以上待办）
        var summary = (await db.Ado.SqlQueryAsync<FlowStatsSummaryDto>("""
SELECT COUNT(1) AS Total,
       SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) AS Running,
       SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS Approved,
       SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) AS Rejected,
       ISNULL(AVG(CASE WHEN Status = 2 THEN DATEDIFF(MINUTE, SubmitTime, EndTime) END), -60) / 60.0 AS AvgApproveHours,
       (SELECT COUNT(1) FROM sys_flow_task t
          JOIN sys_flow_instance i2 ON t.InstanceId = i2.Id AND i2.Status = 1
         WHERE t.Status = 1 AND t.IsDeleted = 0
           AND t.CreateTime <= DATEADD(DAY, -3, GETDATE())) AS OverduePending
FROM sys_flow_instance
WHERE IsDeleted = 0 AND SubmitTime >= @b AND SubmitTime <= @e
""", args))[0];

        // 按流程
        var byFlow = await db.Ado.SqlQueryAsync<FlowStatsByFlowDto>("""
SELECT FlowCode,
       COUNT(1) AS Total,
       SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) AS Running,
       SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS Approved,
       SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) AS Rejected,
       ISNULL(AVG(CASE WHEN Status = 2 THEN DATEDIFF(MINUTE, SubmitTime, EndTime) END), -60) / 60.0 AS AvgApproveHours
FROM sys_flow_instance
WHERE IsDeleted = 0 AND SubmitTime >= @b AND SubmitTime <= @e
GROUP BY FlowCode
ORDER BY COUNT(1) DESC
""", args);

        // 按审批人（任务级；时间范围按任务归属实例的提交时间过滤）
        var byApprover = await db.Ado.SqlQueryAsync<FlowStatsByApproverDto>("""
SELECT t.ApproverName AS UserName,
       SUM(CASE WHEN t.Status IN (2,8) THEN 1 ELSE 0 END) AS Handled,
       SUM(CASE WHEN t.Status = 2 THEN 1 ELSE 0 END) AS Approved,
       SUM(CASE WHEN t.Status = 3 THEN 1 ELSE 0 END) AS Rejected,
       SUM(CASE WHEN t.Status = 1 THEN 1 ELSE 0 END) AS Pending,
       ISNULL(AVG(CASE WHEN t.ActTime IS NOT NULL THEN DATEDIFF(MINUTE, t.CreateTime, t.ActTime) END), -60) / 60.0 AS AvgHandleHours
FROM sys_flow_task t
JOIN sys_flow_instance i ON t.InstanceId = i.Id
WHERE t.IsDeleted = 0 AND i.IsDeleted = 0 AND i.SubmitTime >= @b AND i.SubmitTime <= @e
GROUP BY t.ApproverName
ORDER BY SUM(CASE WHEN t.Status IN (2,3,8) THEN 1 ELSE 0 END) DESC
""", args);

        return new FlowStatsDto { Summary = summary, ByFlow = byFlow, ByApprover = byApprover };
    }

    public async Task<FlowInstanceDto?> GetByBusinessAsync(string businessTable, long businessId) =>
        (await instanceRepository.GetListAsync(
                x => x.BusinessTable == businessTable && x.BusinessId == businessId.ToString()))
            .OrderByDescending(x => x.SubmitTime)
            .Select(ToInstanceDto)
            .FirstOrDefault();

    public async Task<FlowInstanceDetailDto> GetDetailAsync(long instanceId)
    {
        var instance = await instanceRepository.GetByIdAsync(instanceId)
                       ?? throw new BusinessException("流程实例不存在");

        var records = await db.Queryable<SysFlowRecord>()
            .Where(r => r.InstanceId == instanceId)
            .OrderBy(r => r.ActTime).OrderBy(r => r.Id)
            .ToListAsync();

        var nodeNames = await LoadNodeNamesAsync(instance);
        var timeline = records.Select(r =>
        {
            var nodeName = r.NodeCode != null && nodeNames.TryGetValue(r.NodeCode, out var name) ? name : r.NodeCode ?? string.Empty;
            return new FlowTimelineItem
            {
                NodeCode = r.NodeCode,
                NodeName = nodeName,
                Action = r.Action,
                OperatorName = r.OperatorName,
                Comment = r.Comment,
                Time = r.ActTime
            };
        }).ToList();

        // 可驳回目标：本实例已产生审批结论的节点（去重）+ 发起人重提入口
        var returnTargets = records
            .Where(r => r.NodeCode != null && (r.Action == "approve" || r.Action == "auto"))
            .Select(r => r.NodeCode!)
            .Distinct()
            .Select(code => new FlowReturnTarget
            {
                Code = code,
                Name = nodeNames.TryGetValue(code, out var name) ? name : code
            })
            .ToList();
        returnTargets.Insert(0, new FlowReturnTarget { Code = "start", Name = "发起人（修改后重新提交）" });

        return new FlowInstanceDetailDto
        {
            Instance = ToInstanceDto(instance),
            NodeJson = (await LoadNodeJsonAsync(instance)) ?? string.Empty,
            CurrentNodeCode = instance.CurrentNodeCode,
            Timeline = timeline,
            CanWithdraw = instance.Status == FlowInstanceStatus.Running
                          && instance.SubmitterId == (operatorProvider.OperatorUserId ?? 0),
            ReturnTargets = returnTargets
        };
    }

    /// <summary>节点编码 → 名称映射（时间线展示用）</summary>
    private async Task<Dictionary<string, string>> LoadNodeNamesAsync(SysFlowInstance instance)
    {
        var json = await LoadNodeJsonAsync(instance);
        var graph = FlowGraph.Parse(json ?? string.Empty);
        return graph?.Nodes.ToDictionary(n => n.Code, n => n.Name ?? n.Code)
               ?? new Dictionary<string, string>();
    }

    private async Task<string?> LoadNodeJsonAsync(SysFlowInstance instance)
    {
        var definition = await db.Queryable<SysFlowDefinition>()
            .FirstAsync(d => d.Id == instance.DefinitionId);
        return definition?.NodeJson;
    }

    private static FlowInstanceDto ToInstanceDto(SysFlowInstance x) => new()
    {
        Id = x.Id,
        FlowCode = x.FlowCode,
        Summary = x.Summary,
        BusinessTable = x.BusinessTable,
        BusinessId = x.BusinessId,
        Status = (int)x.Status,
        SubmitterName = x.SubmitterName,
        SubmitTime = x.SubmitTime,
        EndTime = x.EndTime
    };
}
