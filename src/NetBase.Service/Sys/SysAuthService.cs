using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NetBase.Common.Cache;
using NetBase.Common.Exceptions;
using NetBase.Common.Results;
using NetBase.Common.Security;
using NetBase.Model.Dtos;
using NetBase.Model.Entities;
using NetBase.Model.Enums;
using NetBase.Repository.Repositories;

namespace NetBase.Service.Sys;

/// <summary>认证服务实现：JWT 签发、会话管理、刷新轮换</summary>
public class SysAuthService(
    IRepository<SysUser> userRepository,
    IRepository<SysUserRole> userRoleRepository,
    IRepository<SysUserSession> sessionRepository,
    ISysUserService userService,
    IPermissionService permissionService,
    IOptions<JwtOptions> jwtOptions) : ISysAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<LoginResult> LoginAsync(string userName, string password, string? loginIp, string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessException("用户名和密码不能为空", ApiResultCode.BadRequest);
        }

        var user = await userRepository.GetFirstAsync(x => x.UserName == userName);
        // 统一错误提示，不泄露账号是否存在
        if (user == null || !PasswordHelper.Verify(password, user.Password))
        {
            throw new BusinessException("用户名或密码错误", ApiResultCode.BadRequest);
        }
        if (user.Status != (int)StatusEnum.Enabled)
        {
            throw new BusinessException("账号已被停用，请联系管理员", ApiResultCode.Forbidden);
        }

        return await CreateSessionAsync(user, loginIp, userAgent);
    }

    public async Task<LoginResult> RefreshAsync(string refreshToken, string? loginIp, string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new BusinessException("无效的刷新令牌", ApiResultCode.Unauthorized);
        }

        var session = await sessionRepository.GetFirstAsync(x => x.RefreshTokenHash == HashToken(refreshToken));
        if (session == null || session.ExpireTime <= DateTime.Now)
        {
            throw new BusinessException("登录已过期，请重新登录", ApiResultCode.Unauthorized);
        }

        var user = await userRepository.GetByIdAsync(session.UserId)
            ?? throw new BusinessException("账号不存在", ApiResultCode.Unauthorized);
        if (user.Status != (int)StatusEnum.Enabled)
        {
            throw new BusinessException("账号已被停用，请联系管理员", ApiResultCode.Forbidden);
        }

        // 轮换：删除旧会话，签发全新 token 对
        await sessionRepository.DeleteAsync(session);
        return await CreateSessionAsync(user, loginIp, userAgent);
    }

    public async Task LogoutAsync(string tokenId) =>
        await sessionRepository.DeleteWhereAsync(x => x.TokenId == tokenId);

    public async Task RemoveUserSessionsAsync(long userId) =>
        await sessionRepository.DeleteWhereAsync(x => x.UserId == userId);

    public async Task<PageResult<SessionDto>> GetSessionPageAsync(PageQuery query)
    {
        var page = await sessionRepository.GetPageListAsync(null, query);
        var userIds = page.Items.Select(x => x.UserId).Distinct().ToList();
        var users = userIds.Count == 0
            ? []
            : await userRepository.GetListAsync(x => userIds.Contains(x.Id));
        var userMap = users.ToDictionary(x => x.Id);

        var items = page.Items.Select(s => new SessionDto
        {
            Id = s.Id,
            UserId = s.UserId,
            UserName = userMap.TryGetValue(s.UserId, out var u) ? u.UserName : $"已删除用户({s.UserId})",
            NickName = userMap.TryGetValue(s.UserId, out var u2) ? u2.NickName : null,
            LoginIp = s.LoginIp,
            UserAgent = s.UserAgent,
            LoginTime = s.LoginTime,
            ExpireTime = s.ExpireTime
        }).ToList();

        return PageResult<SessionDto>.Of(items, page.Total, page.PageIndex, page.PageSize);
    }

    public async Task KickSessionAsync(long sessionId) => await sessionRepository.DeleteAsync(sessionId);

    public async Task<UserDto?> GetUserProfileAsync(long userId) => await userService.GetDetailAsync(userId);

    #region 内部

    /// <summary>签发 token 对并写入会话</summary>
    private async Task<LoginResult> CreateSessionAsync(SysUser user, string? loginIp, string? userAgent)
    {
        var now = DateTime.Now;

        var userRoles = await userRoleRepository.GetListAsync(x => x.UserId == user.Id);
        var roleIds = userRoles.Select(x => x.RoleId).ToList();
        var permissions = await permissionService.GetUserPermissionsAsync(user.Id);

        var tokenId = Guid.NewGuid().ToString("N");
        var refreshToken = GenerateRefreshToken();

        var session = new SysUserSession
        {
            UserId = user.Id,
            TokenId = tokenId,
            RefreshTokenHash = HashToken(refreshToken),
            LoginIp = loginIp,
            UserAgent = Truncate(userAgent, 255),
            LoginTime = now,
            ExpireTime = now.AddDays(_jwt.RefreshTokenExpireDays)
        };
        await sessionRepository.InsertAsync(session);

        // 顺手清理该用户已过期会话，防表膨胀
        await sessionRepository.DeleteWhereAsync(x => x.UserId == user.Id && x.ExpireTime <= now);

        var accessToken = GenerateAccessToken(user, tokenId);
        var userDto = await userService.GetDetailAsync(user.Id) ?? new UserDto { Id = user.Id, UserName = user.UserName };

        return new LoginResult
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwt.AccessTokenExpireMinutes * 60,
            User = userDto,
            Permissions = permissions
        };
    }

    private string GenerateAccessToken(SysUser user, string tokenId)
    {
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, tokenId),
            new("uid", user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName)
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.Now,
            expires: DateTime.Now.AddMinutes(_jwt.AccessTokenExpireMinutes),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];

    #endregion
}
