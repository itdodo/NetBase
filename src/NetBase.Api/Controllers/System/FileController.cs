using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Common.Users;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.System;

/// <summary>文件上传与访问（本地存储，预留 OSS 切换）</summary>
[ApiController]
[Route("api/v1/file")]
public class FileController(ISysFileService fileService, ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    /// <summary>上传文件（bizType=avatar 时按图片白名单与 2MB 限制校验）</summary>
    [Authorize]
    [HttpPost("upload")]
    public async Task<ApiResult<FileUploadResult>> Upload([FromForm] IFormFile file, [FromForm] string? bizType)
    {
        if (file == null || file.Length == 0)
        {
            return ApiResult<FileUploadResult>.Fail("请选择要上传的文件", ApiResultCode.BadRequest);
        }

        await using var stream = file.OpenReadStream();
        var result = await fileService.UploadAsync(stream, file.FileName, file.ContentType, bizType, OperatorUserId ?? 0);
        return Success(result, "上传成功");
    }

    /// <summary>访问文件内容（按随机存储名，防 id 枚举；头像等公开资源匿名可读）</summary>
    [AllowAnonymous]
    [HttpGet("d/{storageName}")]
    public async Task<IActionResult> Download(string storageName)
    {
        var result = await fileService.GetByStorageNameAsync(storageName);
        if (result == null)
        {
            return NotFound();
        }

        return File(result.Value.Content, result.Value.File.ContentType ?? "application/octet-stream", result.Value.File.FileName);
    }
}
