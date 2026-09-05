using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NetBase.Common.Users;
using NetBase.Model.Dtos;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.Auth;

/// <summary>认证：登录、刷新、登出、在线会话管理</summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    ISysAuthService authService,
    IPermissionService permissionService,
    ICaptchaService captchaService,
    ISysUserService userService,
    ISysFileService fileService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    private const string TokenIdClaim = "jti";

    /// <summary>登录（每 IP 每分钟限流 10 次；连续失败 5 次锁定 10 分钟）</summary>
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ApiResult<LoginResult>> Login([FromBody] LoginRequestDto request)
    {
        var result = await authService.LoginAsync(
            request.UserName,
            request.Password,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            request.CaptchaId,
            request.CaptchaCode);
        return Success(result, "登录成功");
    }

    /// <summary>刷新令牌（轮换：旧 RefreshToken 立即失效）</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ApiResult<LoginResult>> Refresh([FromBody] RefreshRequestDto request)
    {
        var result = await authService.RefreshAsync(
            request.RefreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
        return Success(result, "刷新成功");
    }

    /// <summary>登出（删除当前会话）</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<ApiResult> Logout()
    {
        var tokenId = User.FindFirst(TokenIdClaim)?.Value;
        if (!string.IsNullOrEmpty(tokenId))
        {
            await authService.LogoutAsync(tokenId);
        }
        return Success("已退出登录");
    }

    /// <summary>获取图形验证码（sys.captcha.enabled=true 时登录必填）</summary>
    [AllowAnonymous]
    [HttpGet("captcha")]
    public async Task<ApiResult<CaptchaResult>> Captcha()
    {
        return Success(await captchaService.GenerateAsync());
    }

    /// <summary>修改自己资料（昵称/手机/邮箱）</summary>
    [Authorize]
    [HttpPut("profile")]
    public async Task<ApiResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        await userService.UpdateProfileAsync(currentUserService.UserId ?? 0, dto);
        return Success("资料已更新");
    }

    /// <summary>上传头像（返回访问地址）</summary>
    [Authorize]
    [HttpPost("avatar")]
    public async Task<ApiResult<string>> Avatar([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return ApiResult<string>.Fail("请选择头像文件", ApiResultCode.BadRequest);
        }

        await using var stream = file.OpenReadStream();
        var upload = await fileService.UploadAsync(stream, file.FileName, file.ContentType, "avatar", currentUserService.UserId ?? 0);
        await userService.SetAvatarAsync(currentUserService.UserId ?? 0, upload.Url);
        return Success(upload.Url, "头像已更新");
    }

    /// <summary>当前用户信息 + 权限码（前端刷新后恢复用）</summary>
    [Authorize]
    [HttpGet("profile")]
    public async Task<ApiResult<object>> Profile()
    {
        var userId = currentUserService.UserId ?? 0;
        var user = userId > 0 ? await authService.GetUserProfileAsync(userId) : null;
        var permissions = userId > 0 ? await permissionService.GetUserPermissionsAsync(userId) : [];
        return Success(new { user, permissions } as object);
    }

    /// <summary>在线会话分页</summary>
    [HasPermission("monitor:online:list")]
    [HttpGet("sessions")]
    public async Task<ApiResult<PageResult<SessionDto>>> Sessions([FromQuery] PageQuery query)
    {
        return Success(await authService.GetSessionPageAsync(query));
    }

    /// <summary>强制下线（删除指定会话）</summary>
    [HasPermission("monitor:online:list")]
    [HttpDelete("sessions/{id:long}")]
    public async Task<ApiResult> Kick(long id)
    {
        await authService.KickSessionAsync(id);
        return Success("已强制下线");
    }

    /// <summary>修改自己密码（验证旧密码；成功后全部会话失效，需重新登录）</summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<ApiResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        await authService.ChangePasswordAsync(
            currentUserService.UserId ?? 0,
            dto.OldPassword,
            dto.NewPassword,
            OperatorName);
        return Success("密码修改成功，请重新登录");
    }
}
