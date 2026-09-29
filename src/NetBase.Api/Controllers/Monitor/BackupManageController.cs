using Microsoft.AspNetCore.Mvc;
using IOFile = System.IO.File;
using Microsoft.Extensions.Options;
using NetBase.Api.Auth;
using NetBase.Api.Jobs;
using NetBase.Common.Results;
using NetBase.Common.Users;

namespace NetBase.Api.Controllers.Monitor;

/// <summary>
/// 数据备份管理：备份文件列表 / 立即备份 / 下载备份文件。
/// 目录为 Backup:Directory 配置（容器内挂载卷）；下载走文件流（FileStreamResult 支持断点）。
/// </summary>
[ApiController]
[Route("api/v1/monitor/backup")]
public class BackupManageController(
    IBackupService backupService,
    IOptions<BackupOptions> backupOptions,
    IHostEnvironment environment,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>备份文件分页列表（按时间倒序）</summary>
    [HasPermission("monitor:backup:list")]
    [HttpGet("files")]
    public ApiResult<List<BackupFileDto>> ListFiles()
    {
        var opt = backupOptions.Value;
        var baseDir = Path.IsPathRooted(opt.Directory)
            ? opt.Directory
            : Path.Combine(environment.ContentRootPath, opt.Directory);
        if (!Directory.Exists(baseDir))
        {
            return Success(new List<BackupFileDto>());
        }

        var files = new DirectoryInfo(baseDir)
            .GetFiles("*.backup")
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new BackupFileDto
            {
                FileName = f.Name,
                SizeBytes = f.Length,
                LastWriteTime = f.LastWriteTime
            })
            .ToList();
        return Success(files);
    }

    /// <summary>立即执行一次全量备份（同步返回结果；pg_dump 拼库大小通常秒级）</summary>
    [HasPermission("monitor:backup:run")]
    [NoRepeatSubmit]
    [HttpPost("run")]
    public async Task<ApiResult<string>> Run()
    {
        var result = await backupService.RunAsync();
        return Success(Path.GetFileName(result.BakFile), $"备份完成（{result.BakSizeBytes / 1024.0 / 1024:F1}MB，镜像 {result.MirroredFiles} 个文件）");
    }

    /// <summary>下载备份文件（文件名白名单校验防目录穿越）</summary>
    [HasPermission("monitor:backup:list")]
    [HttpGet("files/{fileName}/download")]
    public IActionResult Download(string fileName)
    {
        if (!fileName.EndsWith(".backup", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\')
            || fileName.Contains(':'))
        {
            return BadRequest(ApiResult.Fail("文件名不合法", ApiResultCode.BadRequest, NetBase.Common.Results.ErrorCodes.COMMON_PARAM_INVALID));
        }

        var opt = backupOptions.Value;
        var baseDir = Path.IsPathRooted(opt.Directory)
            ? opt.Directory
            : Path.Combine(environment.ContentRootPath, opt.Directory);
        var fullPath = Path.Combine(baseDir, fileName);
        if (!IOFile.Exists(fullPath))
        {
            return BadRequest(ApiResult.Fail("备份文件不存在", ApiResultCode.NotFound, NetBase.Common.Results.ErrorCodes.COMMON_NOT_FOUND));
        }

        return File(
            new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read),
            "application/octet-stream",
            fileDownloadName: fileName);
    }
}

/// <summary>备份文件信息</summary>
public class BackupFileDto
{
    /// <summary>文件名</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>大小（字节）</summary>
    public long SizeBytes { get; set; }

    /// <summary>最后写入时间（备份生成时间）</summary>
    public DateTime LastWriteTime { get; set; }
}
