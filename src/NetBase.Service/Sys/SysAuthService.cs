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

/// <summary>认证服务实现：JWT 签发、会话管理、刷新轮换、登录安全</summary>
public class SysAuthService(
    IRepository<SysUser> userRepository,
    IRepository<SysUserSession> sessionRepository,
    ISysUserService userService,
    IPermissionService permissionService,
    ISysLogService logService,
    ICacheService cacheService,
    ISysConfigService configService,
    IOptions<JwtOptions> jwtOptions) : ISysAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    /// <summary>失败锁定阈值/窗口：可经系统参数 sys.login.failThreshold / sys.login.lockMinutes 运维调整</summary>
    private const int FailThreshold = 5;
    private const int LockMinutes = 10;
    private static readonly TimeSpan FailWindow = TimeSpan.FromMinutes(10);

    /// <summary>读取锁定阈值（参数配置优先，缺省回退内置值）</summary>
    private Task<int> GetFailThresholdAsync() => configService.GetIntConfigAsync("sys.login.failThreshold", FailThreshold);

    private Task<int> GetLockMinutesAsync() => configService.GetIntConfigAsync("sys.login.lockMinutes", LockMinutes);

    public async Task<LoginResult> LoginAsync(string userName, string password, string? loginIp, string? userAgent)
    {
        try
        {
            var result = await DoLoginAsync(userName, password, loginIp, userAgent);
            if (result == null)
            {
                // 密码错误（不泄露账号是否存在）
                await WriteLoginLogAsync(userName, 0, false, "用户名或密码错误", loginIp, userAgent);
                throw new BusinessException("用户名或密码错误", ApiResultCode.BadRequest);
            }

            await WriteLoginLogAsync(userName, result.User.Id, true, "登录成功", loginIp, userAgent);
            return result;
        }
        catch (BusinessException ex)
        {
            // 锁定/停用等业务拒绝也记入登录日志（密码错误已在上分支记录）
            if (ex.Message != "用户名或密码错误")
            {
                await WriteLoginLogAsync(userName, 0, false, ex.Message, loginIp, userAgent);
            }
            throw;
        }
    }

    /// <summary>登录主体：锁定校验 → 账号校验 → 密码校验（失败计数）。返回 null 表示密码错误</summary>
    private async Task<LoginResult?> DoLoginAsync(string userName, string password, string? loginIp, string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessException("用户名和密码不能为空", ApiResultCode.BadRequest);
        }

        // 登录失败锁定：连续失败达阈值则锁定一段时间（阈值/时长支持参数配置）
        var key = userName.ToLowerInvariant();
        var failKey = $"netbase:login:fail:{key}";
        var lockKey = $"netbase:login:lock:{key}";
        if (cacheService.Get<bool>(lockKey))
        {
            var lockMinutes = await GetLockMinutesAsync();
            throw new BusinessException($"密码错误次数过多，账号已锁定，请 {lockMinutes} 分钟后重试", ApiResultCode.BadRequest);
        }

        var user = await userRepository.GetFirstAsync(x => x.UserName == userName);
        var passwordOk = user != null && PasswordHelper.Verify(password, user.Password);
        if (user == null || !passwordOk)
        {
            // 统一错误提示，不泄露账号是否存在；失败计数入缓存，达阈值锁定
            var threshold = await GetFailThresholdAsync();
            var fails = cacheService.Get<int>(failKey) + 1;
            cacheService.Set(failKey, fails, FailWindow);
            if (fails >= threshold)
            {
                var lockMinutes = await GetLockMinutesAsync();
                cacheService.Set(lockKey, true, TimeSpan.FromMinutes(lockMinutes));
            }
            return null;
        }

        if (user.Status != (int)StatusEnum.Enabled)
        {
            throw new BusinessException("账号已被停用，请联系管理员", ApiResultCode.Forbidden);
        }

        cacheService.Remove(failKey);
        return await CreateSessionAsync(user, loginIp, userAgent);
    }

    private Task WriteLoginLogAsync(string userName, long userId, bool success, string message, string? loginIp, string? userAgent) =>
        logService.RecordLoginAsync(new SysLoginLog
        {
            UserId = userId,
            UserName = userName,
            Success = success,
            Message = message,
            Ip = loginIp,
            UserAgent = userAgent?.Length > 255 ? userAgent[..255] : userAgent
        });

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
        await sessionRepository.DeletePhysicalWhereAsync(x => x.Id == session.Id);
        return await CreateSessionAsync(user, loginIp, userAgent);
    }

    // 会话记录无审计价值，登出/踢人/过期清理一律物理删除，防止软删记录无限膨胀
    public async Task LogoutAsync(string tokenId) =>
        await sessionRepository.DeletePhysicalWhereAsync(x => x.TokenId == tokenId);

    public async Task RemoveUserSessionsAsync(long userId) =>
        await sessionRepository.DeletePhysicalWhereAsync(x => x.UserId == userId);

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

    public async Task KickSessionAsync(long sessionId) =>
        await sessionRepository.DeletePhysicalWhereAsync(x => x.Id == sessionId);

    public async Task<UserDto?> GetUserProfileAsync(long userId) => await userService.GetDetailAsync(userId);

    public async Task ChangePasswordAsync(long userId, string oldPassword, string newPassword, string? operatorName)
    {
        var policyError = PasswordPolicy.Validate(newPassword);
        if (policyError != null)
        {
            throw new BusinessException(policyError, ApiResultCode.BadRequest);
        }

        var user = await userRepository.GetByIdAsync(userId)
            ?? throw new BusinessException("账号不存在", ApiResultCode.Unauthorized);
        if (!PasswordHelper.Verify(oldPassword, user.Password))
        {
            throw new BusinessException("旧密码不正确", ApiResultCode.BadRequest);
        }
        if (PasswordHelper.Verify(newPassword, user.Password))
        {
            throw new BusinessException("新密码不能与旧密码相同", ApiResultCode.BadRequest);
        }

        var hashed = PasswordHelper.Encrypt(newPassword);
        await userRepository.UpdateWhereAsync(
            x => x.Id == userId,
            x => new SysUser { Password = hashed, UpdateTime = DateTime.Now, UpdateBy = operatorName });

        // 改密后清除全部会话（含当前），强制重新登录
        await sessionRepository.DeletePhysicalWhereAsync(x => x.UserId == userId);
    }

    #region 内部

    /// <summary>签发 token 对并写入会话</summary>
    private async Task<LoginResult> CreateSessionAsync(SysUser user, string? loginIp, string? userAgent)
    {
        var now = DateTime.Now;
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

        // 顺手物理清理该用户已过期会话，防表膨胀
        await sessionRepository.DeletePhysicalWhereAsync(x => x.UserId == user.Id && x.ExpireTime <= now);

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
