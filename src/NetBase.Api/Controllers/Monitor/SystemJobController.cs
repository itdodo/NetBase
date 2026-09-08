using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Api.Jobs;
using NetBase.Model.Dtos;
using NetBase.Service.Sys;
using System.ComponentModel.DataAnnotations;

namespace NetBase.Api.Controllers.Monitor;

/// <summary>定时任务管理：列表/修改Cron/立即触发/暂停恢复</summary>
[ApiController]
[Authorize]
[Route("api/v1/monitor/job")]
public class SystemJobController(ISystemJobService jobService) : ControllerBase
{
    /// <summary>作业实例列表</summary>
    [HasPermission("monitor:job:list")]
    [HttpGet("list")]
    public async Task<ApiResult<List<JobInstanceDto>>> List()
    {
        return ApiResult<List<JobInstanceDto>>.Ok(await jobService.GetJobsAsync());
    }

    /// <summary>修改作业 Cron（立即恢复调度）</summary>
    [HasPermission("monitor:job:edit")]
    [HttpPut("{jobId}/cron")]
    public ApiResult UpdateCron(string jobId, [FromBody] UpdateCronDto dto)
    {
        jobService.UpdateCron(jobId, dto.Cron);
        return ApiResult.Ok("调度已更新");
    }

    /// <summary>立即触发一次</summary>
    [HasPermission("monitor:job:trigger")]
    [HttpPost("{jobId}/trigger")]
    public ApiResult Trigger(string jobId)
    {
        jobService.Trigger(jobId);
        return ApiResult.Ok("已触发执行");
    }

    /// <summary>暂停作业</summary>
    [HasPermission("monitor:job:edit")]
    [HttpPut("{jobId}/pause")]
    public ApiResult Pause(string jobId)
    {
        jobService.Pause(jobId);
        return ApiResult.Ok("已暂停");
    }

    /// <summary>恢复作业</summary>
    [HasPermission("monitor:job:edit")]
    [HttpPut("{jobId}/resume")]
    public ApiResult Resume(string jobId)
    {
        jobService.Resume(jobId);
        return ApiResult.Ok("已恢复");
    }
}

/// <summary>修改 Cron 请求</summary>
public class UpdateCronDto
{
    /// <summary>Cron 表达式（5 段式：分 时 日 月 周）</summary>
    [Required(ErrorMessage = "Cron 不能为空")]
    [RegularExpression(@"^\S+\s+\S+\s+\S+\s+\S+\s+\S+$", ErrorMessage = "Cron 须为 5 段式表达式")]
    public string Cron { get; set; } = string.Empty;
}
