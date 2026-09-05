using NetBase.Common.Results;
using NetBase.Model.Dtos;

namespace NetBase.Service.Sys;

/// <summary>登录结果（token 对 + 用户信息 + 权限码）</summary>
public class LoginResult
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>AccessToken 有效期（秒）</summary>
    public int ExpiresIn { get; set; }

    public UserDto User { get; set; } = new();

    public HashSet<string> Permissions { get; set; } = [];
}

/// <summary>在线会话条目</summary>
public class SessionDto
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? NickName { get; set; }

    public string? LoginIp { get; set; }

    public string? UserAgent { get; set; }

    public DateTime LoginTime { get; set; }

    public DateTime ExpireTime { get; set; }
}

/// <summary>认证服务：登录 / 刷新 / 登出 / 会话管理</summary>
public interface ISysAuthService
{
    /// <summary>登录：校验账号与密码，签发 token 对并创建会话</summary>
    Task<LoginResult> LoginAsync(string userName, string password, string? loginIp, string? userAgent);

    /// <summary>刷新：校验 RefreshToken 并轮换（旧 token 对全部作废）</summary>
    Task<LoginResult> RefreshAsync(string refreshToken, string? loginIp, string? userAgent);

    /// <summary>登出：按会话标识删除当前会话</summary>
    Task LogoutAsync(string tokenId);

    /// <summary>按用户ID清除全部会话（停用/删除用户时调用，立即踢下线）</summary>
    Task RemoveUserSessionsAsync(long userId);

    /// <summary>查询用户资料（profile 接口）</summary>
    Task<UserDto?> GetUserProfileAsync(long userId);

    /// <summary>在线会话分页</summary>
    Task<PageResult<SessionDto>> GetSessionPageAsync(PageQuery query);

    /// <summary>强制下线（删除指定会话）</summary>
    Task KickSessionAsync(long sessionId);
}
