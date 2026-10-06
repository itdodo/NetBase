using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NetBase.Common.Cache;
using NetBase.Common.Exceptions;
using NetBase.Common.Results;
using NetBase.Common.Extensions;
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
    ICaptchaService captchaService,
    NetBase.Common.Email.IEmailService emailService,
    NetBase.Common.Realtime.INotifyService notifyService,
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

    public async Task<LoginResult> LoginAsync(string userName, string password, string? loginIp, string? userAgent, string? captchaId = null, string? captchaCode = null)
    {
        try
        {
            var result = await DoLoginAsync(userName, password, loginIp, userAgent, captchaId, captchaCode);
            if (result == null)
            {
                // 密码错误（不泄露账号是否存在）
                await WriteLoginLogAsync(userName, 0, false, "用户名或密码错误", loginIp, userAgent);
                throw new BusinessException("用户名或密码错误", ApiResultCode.BadRequest, ErrorCodes.AUTH_BAD_CREDENTIALS);
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
    private async Task<LoginResult?> DoLoginAsync(string userName, string password, string? loginIp, string? userAgent, string? captchaId = null, string? captchaCode = null)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            throw new BusinessException("用户名和密码不能为空", ApiResultCode.BadRequest, ErrorCodes.AUTH_PARAM_MISSING);
        }

        // 图形验证码（系统参数 sys.captcha.enabled 可关闭，内网场景）
        if (await captchaService.IsEnabledAsync() && !await captchaService.ValidateAsync(captchaId ?? string.Empty, captchaCode ?? string.Empty))
        {
            throw new BusinessException("验证码错误或已过期", ApiResultCode.BadRequest, ErrorCodes.AUTH_CAPTCHA_INVALID);
        }

        // 登录失败锁定：连续失败达阈值则锁定一段时间（阈值/时长支持参数配置）
        var key = userName.ToLowerInvariant();
        var failKey = $"login:fail:{key}";
        var lockKey = $"login:lock:{key}";
        if (cacheService.Get<bool>(lockKey))
        {
            var lockMinutes = await GetLockMinutesAsync();
            throw new BusinessException($"密码错误次数过多，账号已锁定，请 {lockMinutes} 分钟后重试", ApiResultCode.BadRequest, ErrorCodes.AUTH_ACCOUNT_LOCKED);
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
            throw new BusinessException("账号已被停用，请联系管理员", ApiResultCode.Forbidden, ErrorCodes.AUTH_ACCOUNT_DISABLED);
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
            UserAgent = userAgent.TruncateTo(255)
        });

    public async Task<LoginResult> RefreshAsync(string refreshToken, string? loginIp, string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new BusinessException("无效的刷新令牌", ApiResultCode.Unauthorized, ErrorCodes.AUTH_REFRESH_TOKEN_INVALID);
        }

        var session = await sessionRepository.GetFirstAsync(x => x.RefreshTokenHash == HashToken(refreshToken));
        if (session == null || session.ExpireTime <= DateTime.Now)
        {
            throw new BusinessException("登录已过期，请重新登录", ApiResultCode.Unauthorized, ErrorCodes.AUTH_SESSION_EXPIRED);
        }

        var user = await userRepository.GetByIdAsync(session.UserId)
            ?? throw new BusinessException("账号不存在", ApiResultCode.Unauthorized, ErrorCodes.AUTH_ACCOUNT_NOT_FOUND);
        if (user.Status != (int)StatusEnum.Enabled)
        {
            throw new BusinessException("账号已被停用，请联系管理员", ApiResultCode.Forbidden, ErrorCodes.AUTH_ACCOUNT_DISABLED);
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

    public async Task<PageResult<SessionDto>> GetSessionPageAsync(PageQuery query, string? currentTokenId = null)
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
            ExpireTime = s.ExpireTime,
            IsCurrent = currentTokenId != null && s.TokenId == currentTokenId
        }).ToList();

        return PageResult<SessionDto>.Of(items, page.Total, page.PageIndex, page.PageSize);
    }

    public async Task KickSessionAsync(long sessionId)
    {
        var session = await sessionRepository.GetByIdAsync(sessionId);
        await sessionRepository.DeletePhysicalWhereAsync(x => x.Id == sessionId);
        if (session != null)
        {
            await notifyService.PushForceLogoutAsync(session.UserId, "管理员已将您强制下线");
        }
    }

    public async Task<int> KickSessionsAsync(List<long> sessionIds, string? currentTokenId)
    {
        // 防自踢：排除当前请求自己的会话（批量误勾自己不应导致操作者被登出）
        if (!string.IsNullOrEmpty(currentTokenId))
        {
            var own = await sessionRepository.GetFirstAsync(x => x.TokenId == currentTokenId);
            if (own != null)
            {
                sessionIds = sessionIds.Where(id => id != own.Id).ToList();
            }
        }

        var kicked = 0;
        foreach (var id in sessionIds.Distinct())
        {
            var session = await sessionRepository.GetByIdAsync(id);
            if (session == null)
            {
                continue; // 已下线/不存在，跳过不计
            }
            await sessionRepository.DeletePhysicalWhereAsync(x => x.Id == id);
            await notifyService.PushForceLogoutAsync(session.UserId, "管理员已将您强制下线");
            kicked++;
        }
        return kicked;
    }

    public async Task<UserDto?> GetUserProfileAsync(long userId) => await userService.GetDetailAsync(userId);

    public async Task ChangePasswordAsync(long userId, string oldPassword, string newPassword, string? operatorName)
    {
        var policyError = PasswordPolicy.Validate(newPassword);
        if (policyError != null)
        {
            throw new BusinessException(policyError, ApiResultCode.BadRequest, ErrorCodes.AUTH_PWD_POLICY_VIOLATION);
        }

        var user = await userRepository.GetByIdAsync(userId)
            ?? throw new BusinessException("账号不存在", ApiResultCode.Unauthorized, ErrorCodes.AUTH_ACCOUNT_NOT_FOUND);
        if (!PasswordHelper.Verify(oldPassword, user.Password))
        {
            throw new BusinessException("旧密码不正确", ApiResultCode.BadRequest, ErrorCodes.AUTH_OLD_PWD_WRONG);
        }
        if (PasswordHelper.Verify(newPassword, user.Password))
        {
            throw new BusinessException("新密码不能与旧密码相同", ApiResultCode.BadRequest, ErrorCodes.AUTH_PWD_SAME_AS_OLD);
        }

        var hashed = PasswordHelper.Encrypt(newPassword);
        await userRepository.UpdateWhereAsync(
            x => x.Id == userId,
            x => new SysUser { Password = hashed, PwdUpdateTime = DateTime.Now, UpdateTime = DateTime.Now, UpdateBy = operatorName });

        // 改密后清除全部会话（含当前），强制重新登录
        await sessionRepository.DeletePhysicalWhereAsync(x => x.UserId == userId);
    }

    private const string ResetCodeKey = "pwdreset:code:";
    private const string ResetFreqKey = "pwdreset:freq:";
    private const string ResetTryKey = "pwdreset:try:";

    /// <inheritdoc />
    public async Task SendResetCodeAsync(string userName, string email, string? ip)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email))
        {
            return; // 缺参按"未命中"静默处理（对外统一成功，防枚举）
        }

        // 发码限频：同账号 60 秒一次（防轰炸；正常用户重发间隔足够）
        var freqKey = ResetFreqKey + userName;
        if (cacheService.Get<bool>(freqKey))
        {
            throw new BusinessException("验证码发送过于频繁，请稍后再试", ApiResultCode.BadRequest, ErrorCodes.AUTH_RESET_CODE_RATE_LIMITED);
        }

        var user = await userRepository.GetFirstAsync(x => x.UserName == userName && x.IsDeleted == false);
        var matched = user != null
            && user.Status == (int)StatusEnum.Enabled
            && !string.IsNullOrWhiteSpace(user.Email)
            && string.Equals(user.Email.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase);

        if (!matched)
        {
            // 防枚举：账号不存在/停用/邮箱不匹配 → 静默返回，前端统一提示"若信息匹配将发送"
            return;
        }

        var code = Random.Shared.Next(100000, 999999).ToString();
        cacheService.Set(ResetCodeKey + userName, code, TimeSpan.FromMinutes(5));
        var sent = await emailService.SendAsync(
            user!.Email!.Trim(),
            "NetBase 密码重置验证码",
            $"<p>您正在重置账号 <b>{userName}</b> 的密码，验证码：</p>" +
            $"<p style=\"font-size:22px;font-weight:bold;letter-spacing:4px\">{code}</p>" +
            "<p>验证码 5 分钟内有效且一次使用。若非本人操作，请忽略本邮件。</p>");
        if (sent)
        {
            cacheService.Set(freqKey, true, TimeSpan.FromSeconds(60));
            return;
        }

        // 邮件通道故障（未配置 SMTP/发送失败）：清码并明确报错——静默成功会让用户干等一封永远不来的邮件
        cacheService.Remove(ResetCodeKey + userName);
        throw new BusinessException("邮件发送失败，请联系管理员检查系统邮件配置", ApiResultCode.BadRequest, ErrorCodes.AUTH_RESET_CODE_INVALID);
    }

    /// <inheritdoc />
    public async Task ResetPasswordByCodeAsync(string userName, string email, string code, string newPassword, string? ip)
    {
        // 重置尝试限频：同账号每小时 10 次（验证码空间 10^6，防穷举）
        var tryKey = ResetTryKey + userName;
        var tries = cacheService.Get<int>(tryKey);
        if (tries >= 10)
        {
            throw new BusinessException("重置尝试过于频繁，请稍后再试", ApiResultCode.BadRequest, ErrorCodes.AUTH_RESET_ATTEMPT_RATE_LIMITED);
        }
        cacheService.Set(tryKey, tries + 1, TimeSpan.FromHours(1));

        // 验证码一次性：取后即删（无论对错都消耗本次尝试）
        var codeKey = ResetCodeKey + userName;
        var cached = cacheService.Get<string>(codeKey);
        cacheService.Remove(codeKey);
        if (cached == null || cached != code.Trim())
        {
            throw new BusinessException("验证码错误或已过期", ApiResultCode.BadRequest, ErrorCodes.AUTH_RESET_CODE_INVALID);
        }

        // 邮箱二次校验（发码与重置须同一邮箱，防拿到码后换邮箱语义混乱）
        var user = await userRepository.GetFirstAsync(x => x.UserName == userName && x.IsDeleted == false);
        if (user == null
            || user.Status != (int)StatusEnum.Enabled
            || !string.Equals(user.Email?.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("验证码错误或已过期", ApiResultCode.BadRequest, ErrorCodes.AUTH_RESET_CODE_INVALID);
        }

        var policyError = PasswordPolicy.Validate(newPassword);
        if (policyError != null)
        {
            throw new BusinessException(policyError, ApiResultCode.BadRequest, ErrorCodes.AUTH_PWD_POLICY_VIOLATION);
        }
        if (PasswordHelper.Verify(newPassword, user.Password))
        {
            throw new BusinessException("新密码不能与旧密码相同", ApiResultCode.BadRequest, ErrorCodes.AUTH_PWD_SAME_AS_OLD);
        }

        // 注意：此处不复用受信默认密码豁免——自助重置的密码必须满足完整复杂度策略
        var hashed = PasswordHelper.Encrypt(newPassword);
        await userRepository.UpdateWhereAsync(
            x => x.Id == user.Id,
            x => new SysUser { Password = hashed, PwdUpdateTime = DateTime.Now, UpdateTime = DateTime.Now, UpdateBy = "password-reset" });

        // 全端踢线：旧会话立即失效
        await sessionRepository.DeletePhysicalWhereAsync(x => x.UserId == user.Id);

        await notifyService.PushToUsersAsync([user.Id], new NetBase.Common.Realtime.NoticePayload
        {
            Title = "密码已重置",
            Content = "您的账号密码已通过「忘记密码」自助重置，请使用新密码重新登录。若非本人操作，请立即联系管理员。",
            MsgType = 1
        });
    }

    #region 内部

    /// <summary>签发 token 对并写入会话</summary>
    private async Task<LoginResult> CreateSessionAsync(SysUser user, string? loginIp, string? userAgent)
    {
        var now = DateTime.Now;
        var permissions = await permissionService.GetUserPermissionsAsync(user.Id);

        var tokenId = Guid.NewGuid().ToString("N");
        var refreshToken = GenerateRefreshToken();

        // 同账号互踢（sys.login.kickSameUser=1）：通知旧端友好提示后删除全部旧会话，
        // 旧端收到 SignalR 强下线事件或下个请求 401，统一走前端 2 秒自动回登录页
        if (await configService.GetIntConfigAsync("sys.login.kickSameUser", 0) == 1)
        {
            await notifyService.PushForceLogoutAsync(user.Id, "您的账号已在其他设备登录，如非本人操作请及时修改密码");
            await sessionRepository.DeletePhysicalWhereAsync(x => x.UserId == user.Id);
        }

        var session = new SysUserSession
        {
            UserId = user.Id,
            TokenId = tokenId,
            RefreshTokenHash = HashToken(refreshToken),
            LoginIp = loginIp,
            UserAgent = userAgent.TruncateTo(255),
            LoginTime = now,
            ExpireTime = now.AddDays(_jwt.RefreshTokenExpireDays)
        };
        await sessionRepository.InsertAsync(session);

        // 顺手物理清理该用户已过期会话，防表膨胀
        await sessionRepository.DeletePhysicalWhereAsync(x => x.UserId == user.Id && x.ExpireTime <= now);

        var accessToken = GenerateAccessToken(user, tokenId);
        var userDto = await userService.GetDetailAsync(user.Id) ?? new UserDto { Id = user.Id, UserName = user.UserName };

        // 密码有效期（sys.pwd.expireDays，0=不启用）：超期则登录后强制修改
        var expireDays = await configService.GetIntConfigAsync("sys.pwd.expireDays", 0);
        var mustChange = expireDays > 0
            && (user.PwdUpdateTime == null || now.AddDays(-expireDays) > user.PwdUpdateTime.Value);

        return new LoginResult
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _jwt.AccessTokenExpireMinutes * 60,
            User = userDto,
            Permissions = permissions,
            MustChangePassword = mustChange
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

    #endregion
}
