using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using SqlSugar;

namespace NetBase.Service.Sys;

/// <summary>通知公告实现</summary>
public class SysNoticeService(IRepository<SysNotice> repository) : ISysNoticeService
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
        var notice = new SysNotice
        {
            Title = dto.Title,
            NoticeType = dto.NoticeType,
            Content = dto.Content,
            Status = dto.Status,
            CreateBy = operatorName
        };
        await repository.InsertAsync(notice);
        return notice.Id;
    }

    public async Task UpdateAsync(long id, NoticeSaveDto dto, string? operatorName = null)
    {
        var notice = await repository.GetByIdAsync(id)
            ?? throw new BusinessException($"公告不存在（Id={id}）", ApiResultCode.NotFound);
        notice.Title = dto.Title;
        notice.NoticeType = dto.NoticeType;
        notice.Content = dto.Content;
        notice.Status = dto.Status;
        notice.UpdateTime = DateTime.Now;
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
}
