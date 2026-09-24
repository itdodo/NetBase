using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetBase.Common.Exceptions;
using NetBase.Common.Security;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using NetBase.Common.Results;

namespace NetBase.Service.Sys;

/// <summary>文件存储配置</summary>
public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>存储根目录（缺省 ContentRoot/uploads）</summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>通用上传大小上限（字节），默认 10MB</summary>
    public long MaxSize { get; set; } = 10 * 1024 * 1024;

    /// <summary>图片（avatar）大小上限（字节），默认 2MB</summary>
    public long ImageMaxSize { get; set; } = 2 * 1024 * 1024;

    /// <summary>通用扩展名白名单</summary>
    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip"];
}

/// <summary>文件存储服务实现</summary>
public class SysFileService(
    IRepository<SysFile> repository,
    IOptions<FileStorageOptions> options,
    Microsoft.Extensions.Hosting.IHostEnvironment environment,
    ILogger<SysFileService> logger) : ISysFileService
{
    private readonly FileStorageOptions _options = options.Value;

    public async Task<FileUploadResult> UploadAsync(Stream content, string fileName, string? contentType, string? bizType, long uploadUserId)
    {
        // 文件名校验：取纯文件名防路径穿越
        fileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new BusinessException("文件名无效", NetBase.Common.Results.ApiResultCode.BadRequest, NetBase.Common.Results.ErrorCodes.SYS_FILE_NAME_INVALID);
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var isAvatar = string.Equals(bizType, "avatar", StringComparison.OrdinalIgnoreCase);

        // 扩展名白名单
        if (isAvatar)
        {
            string[] imageExts = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
            if (!imageExts.Contains(extension))
            {
                throw new BusinessException("头像仅支持 jpg/png/gif/webp 格式", NetBase.Common.Results.ApiResultCode.BadRequest, NetBase.Common.Results.ErrorCodes.SYS_FILE_AVATAR_TYPE_INVALID);
            }
        }
        else if (!_options.AllowedExtensions.Contains(extension))
        {
            throw new BusinessException($"不支持的文件类型：{extension}", NetBase.Common.Results.ApiResultCode.BadRequest, NetBase.Common.Results.ErrorCodes.SYS_FILE_TYPE_INVALID);
        }

        // 内容嗅探：文件头与扩展名比对，拦截改后缀伪装（txt 等无签名类型跳过）
        if (!FileSignatureValidator.IsValid(extension, content))
        {
            throw new BusinessException("文件内容与扩展名不符，已拒绝上传", NetBase.Common.Results.ApiResultCode.BadRequest, NetBase.Common.Results.ErrorCodes.SYS_FILE_SIGNATURE_MISMATCH);
        }

        // 大小限制
        var maxSize = isAvatar ? _options.ImageMaxSize : _options.MaxSize;
        if (content.Length > maxSize)
        {
            throw new BusinessException($"文件大小超过限制（最大 {maxSize / 1024 / 1024}MB）", NetBase.Common.Results.ApiResultCode.BadRequest, NetBase.Common.Results.ErrorCodes.SYS_FILE_SIZE_EXCEEDED);
        }

        // 落盘：uploads/yyyyMMdd/{uuid}{ext}
        var root = string.IsNullOrWhiteSpace(_options.RootPath)
            ? Path.Combine(environment.ContentRootPath, "uploads")
            : _options.RootPath;
        var dir = DateTime.Now.ToString("yyyyMMdd");
        Directory.CreateDirectory(Path.Combine(root, dir));
        var storageName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(root, dir, storageName);

        await using (var fileStream = File.Create(fullPath))
        {
            await content.CopyToAsync(fileStream);
        }

        var record = new SysFile
        {
            FileName = fileName,
            StorageName = storageName,
            StorageDir = dir,
            Size = content.Length,
            ContentType = contentType,
            BizType = bizType,
            UploadUserId = uploadUserId
        };
        await repository.InsertAsync(record);
        logger.LogInformation("文件已保存: {FileName} -> {Path} ({Size}B)", fileName, storageName, content.Length);

        return new FileUploadResult
        {
            Id = record.Id,
            // 按随机存储名访问，防自增 id 枚举下载
            Url = $"/api/v1/file/d/{record.StorageName}",
            FileName = fileName,
            Size = content.Length
        };
    }

    public async Task<(SysFile File, byte[] Content)?> GetAsync(long id)
    {
        var record = await repository.GetByIdAsync(id);
        return record == null ? null : await ReadContentAsync(record);
    }

    public async Task<(SysFile File, byte[] Content)?> GetByStorageNameAsync(string storageName)
    {
        // 存储名为 uuid 随机串，无法枚举
        var record = await repository.GetFirstAsync(x => x.StorageName == storageName);
        return record == null ? null : await ReadContentAsync(record);
    }

    private async Task<(SysFile File, byte[] Content)?> ReadContentAsync(SysFile record)
    {
        var root = string.IsNullOrWhiteSpace(_options.RootPath)
            ? Path.Combine(environment.ContentRootPath, "uploads")
            : _options.RootPath;
        var fullPath = Path.Combine(root, record.StorageDir, record.StorageName);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        return (record, await File.ReadAllBytesAsync(fullPath));
    }
}
