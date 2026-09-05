using NetBase.Common.Results;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;

namespace NetBase.Service.Sys;

/// <summary>通知公告服务：管理端 CRUD 与登录用户读取</summary>
public interface ISysNoticeService
{
    /// <summary>管理端分页（含停用）</summary>
    Task<PageResult<SysNotice>> GetPageAsync(NoticeQueryDto query);

    Task<SysNotice?> GetDetailAsync(long id);

    Task<long> CreateAsync(NoticeSaveDto dto, string? operatorName = null);

    Task UpdateAsync(long id, NoticeSaveDto dto, string? operatorName = null);

    Task DeleteAsync(long id);

    /// <summary>登录用户取最新启用公告（顶栏铃铛，限 10 条）</summary>
    Task<List<SysNotice>> GetLatestAsync(int top = 10);
}
