using NetBase.Model.Entities;

namespace NetBase.Service.Sys;

/// <summary>文件上传结果</summary>
public class FileUploadResult
{
    public long Id { get; set; }

    /// <summary>访问地址（/api/v1/file/{id}）</summary>
    public string Url { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long Size { get; set; }
}

/// <summary>文件存储服务（本地磁盘起步，接口留 OSS 切换空间）</summary>
public interface ISysFileService
{
    /// <summary>
    /// 保存上传文件：扩展名白名单 + 大小限制校验，落盘 ContentRoot/uploads/yyyyMMdd。
    /// </summary>
    /// <param name="content">文件内容</param>
    /// <param name="fileName">原始文件名</param>
    /// <param name="contentType">内容类型</param>
    /// <param name="bizType">业务类型（avatar 等差异化校验规则）</param>
    /// <param name="uploadUserId">上传人</param>
    Task<FileUploadResult> UploadAsync(Stream content, string fileName, string? contentType, string? bizType, long uploadUserId);

    /// <summary>读取文件内容（不存在返回 null）</summary>
    Task<(SysFile File, byte[] Content)?> GetAsync(long id);

    /// <summary>按存储名读取文件（防 id 枚举的公开访问路径）</summary>
    Task<(SysFile File, byte[] Content)?> GetByStorageNameAsync(string storageName);
}
