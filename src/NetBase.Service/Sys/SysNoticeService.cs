using NetBase.Common.Time;
using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using SqlSugar;

namespace NetBase.Service.Sys;

/// <summary>通知公告实现</summary>
public class SysNoticeService(IRepository<SysNotice> repository,

TimeProvider tp) : ISysNoticeService
{
    public async Task<PageResult<SysNotice>> GetPageAsync(NoticeQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        return await repository.GetPageListAsync(
            Expressionable.Create<SysNotice>()
                .AndIF(keyword.IsNotNullOrEmpty(), x => x.Title.Contains(keyword!))
                .AndIF(query.NoticeType.HasValue, x => x.NoticeType == query.NoticeType!.Value)
                .ToExpression(), query);
    }

    public Task<SysNotice?> GetDetailAsync(long id) => repository.GetByIdAsync(id);

    public async Task<long> CreateAsync(NoticeSaveDto dto, string? operatorName = null)
    {
        ValidatePublishTime(dto);
        var notice = new SysNotice
        {
            Title = dto.Title,
            NoticeType = dto.NoticeType,
            Content = dto.Content,
            Status = dto.Status,
            PublishTime = dto.PublishTime,
            CreateBy = operatorName
        };
        await repository.InsertAsync(notice);
        return notice.Id;
    }

    public async Task UpdateAsync(long id, NoticeSaveDto dto, string? operatorName = null)
    {
        var notice = await repository.GetByIdAsync(id)
            ?? throw new BusinessException($"公告不存在（Id={id}）", ApiResultCode.NotFound, ErrorCodes.SYS_NOTICE_NOT_FOUND);
        ValidatePublishTime(dto);
        notice.Title = dto.Title;
        notice.NoticeType = dto.NoticeType;
        notice.Content = dto.Content;
        notice.Status = dto.Status;
        notice.PublishTime = dto.PublishTime;
        notice.UpdateTime = tp.LocalNow();
        notice.UpdateBy = operatorName;
        await repository.UpdateAsync(notice);
    }

    public Task DeleteAsync(long id) => repository.DeleteAsync(id);

    public async Task<List<SysNotice>> GetLatestAsync(int top = 10) =>
        await repository.Queryable
            .Where(x => x.Status == 1)
            .OrderByDescending(x => x.CreateTime)
            .Take(top)
            .ToListAsync();

    /// <summary>到期公告自动发布（分钟级作业调用）：定时状态翻转为发布，返回翻转条数</summary>
    public Task<int> PublishDueNoticesAsync()
    {
        return repository.UpdateWhereAsync(
            x => x.Status == 2 && x.PublishTime != null && x.PublishTime <= DateTime.Now,
            x => new SysNotice { Status = 1 });
    }

    /// <summary>定时发布校验：状态=2 必须带发布时间且在未来</summary>
    private void ValidatePublishTime(NoticeSaveDto dto)
    {
        if (dto.Status == 2 && (!dto.PublishTime.HasValue || dto.PublishTime.Value <= tp.LocalNow()))
        {
            throw new BusinessException("定时发布时间必须为当前时间之后", ApiResultCode.BadRequest, ErrorCodes.SYS_NOTICE_PUBLISH_TIME_INVALID);
        }
    }
}
