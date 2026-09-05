using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetBase.Common.Users;
using NetBase.Api.Auth;
using NetBase.Common.Results;
using NetBase.Service.Sys;

namespace NetBase.Api.Controllers.Auth;

/// <summary>认证：登录、刷新、登出、在线会话管理</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(
    ISysAuthService authService,
    IPermissionService permissionService,
    ICurrentUserService currentUserService) : BaseController(currentUserService)
{
    private const string TokenIdClaim = "jti";

    /// <summary>登录</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ApiResult<LoginResult>> Login([FromBody] LoginRequestDto request)
    {
        var result = await authService.LoginAsync(
            request.UserName,
            request.Password,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
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
}

/// <summary>登录请求</summary>
public class LoginRequestDto
{
    /// <summary>用户名</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>密码</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>刷新令牌请求</summary>
public class RefreshRequestDto
{
    /// <summary>刷新令牌</summary>
    public string RefreshToken { get; set; } = string.Empty;
}
