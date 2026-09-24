using System.ComponentModel.DataAnnotations;
using NetBase.Common.Exceptions;
using NetBase.Common.Results;
using NetBase.Model.Entities;
using NetBase.Repository.Auditing;
using NetBase.Repository.Repositories;

namespace NetBase.Service.Sys.Flow;

/// <summary>委托返回</summary>
public class FlowDelegateDto
{
    [System.Text.Json.Serialization.JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long Id { get; set; }

    public string DelegatorName { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long AgentId { get; set; }

    public string AgentName { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int Status { get; set; }

    public string? Remark { get; set; }
}

/// <summary>创建委托请求</summary>
public class FlowDelegateCreateDto
{
    /// <summary>代理人ID</summary>
    [Range(1, long.MaxValue, ErrorMessage = "请选择代理人")]
    public long AgentId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    [StringLength(200)]
    public string? Remark { get; set; }
}

/// <summary>审批委托代理：委托人时间段内的新待办自动转由代理人审批</summary>
public interface ISysFlowDelegateService
{
    /// <summary>当前用户的委托列表</summary>
    Task<List<FlowDelegateDto>> GetMyListAsync();

    Task<long> CreateAsync(FlowDelegateCreateDto dto);

    /// <summary>删除（仅本人的委托）</summary>
    Task DeleteAsync(long id);
}

public class SysFlowDelegateService(
    IRepository<SysFlowDelegate> repository,
    IRepository<SysUser> userRepository,
    IOperatorProvider operatorProvider) : ISysFlowDelegateService
{
    public async Task<List<FlowDelegateDto>> GetMyListAsync()
    {
        var userId = operatorProvider.OperatorUserId ?? 0;
        var list = await repository.GetListAsync(x => x.DelegatorId == userId);
        return list.OrderByDescending(x => x.CreateTime).Select(ToDto).ToList();
    }

    public async Task<long> CreateAsync(FlowDelegateCreateDto dto)
    {
        var userId = operatorProvider.OperatorUserId
                     ?? throw new BusinessException("未登录", ApiResultCode.Unauthorized, ErrorCodes.FLOW_NOT_AUTHENTICATED);
        if (dto.EndTime <= dto.StartTime)
        {
            throw new BusinessException("结束时间必须晚于开始时间", ApiResultCode.BadRequest, ErrorCodes.FLOW_DELEGATE_TIME_INVALID);
        }
        if (dto.AgentId == userId)
        {
            throw new BusinessException("不能委托给自己", ApiResultCode.BadRequest, ErrorCodes.FLOW_DELEGATE_SELF);
        }
        var agent = await userRepository.GetFirstAsync(x => x.Id == dto.AgentId && x.Status == 1)
                    ?? throw new BusinessException("代理人不存在或已停用", ApiResultCode.BadRequest, ErrorCodes.FLOW_DELEGATE_AGENT_INVALID);

        var row = new SysFlowDelegate
        {
            DelegatorId = userId,
            DelegatorName = operatorProvider.OperatorName ?? string.Empty,
            AgentId = agent.Id,
            AgentName = agent.NickName ?? agent.UserName,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Remark = dto.Remark
        };
        await repository.InsertAsync(row);
        return row.Id;
    }

    public async Task DeleteAsync(long id)
    {
        var userId = operatorProvider.OperatorUserId ?? 0;
        var row = await repository.GetByIdAsync(id)
                  ?? throw new BusinessException("委托不存在", ErrorCodes.FLOW_DELEGATE_NOT_FOUND);
        if (row.DelegatorId != userId)
        {
            throw new BusinessException("仅可删除自己的委托", ApiResultCode.Forbidden, ErrorCodes.FLOW_DELEGATE_DELETE_FORBIDDEN);
        }
        await repository.DeleteAsync(row);
    }

    private static FlowDelegateDto ToDto(SysFlowDelegate x) => new()
    {
        Id = x.Id,
        DelegatorName = x.DelegatorName,
        AgentId = x.AgentId,
        AgentName = x.AgentName,
        StartTime = x.StartTime,
        EndTime = x.EndTime,
        Status = x.Status,
        Remark = x.Remark
    };
}
