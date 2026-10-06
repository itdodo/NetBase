using Microsoft.Extensions.DependencyInjection;
using NetBase.Common.Cache;
using NetBase.Common.Exceptions;
using NetBase.Common.Results;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using NetBase.Service.Sys;
using Xunit;

namespace NetBase.IntegrationTests;

/// <summary>
/// 忘记密码自助重置全流程：发码（防枚举/限频）→ 验证码重置（一次性/策略/限尝试）→ 新密码登录。
/// 邮件走 TestEmailService 替身；验证码从缓存键直接读取（pwdreset:code:{userName}）。
/// </summary>
[Collection("Integration")]
public class PasswordResetTests
{
    private readonly IntegrationFixture _fixture;
    private readonly TestEmailService _email;
    private readonly ICacheService _cache;

    public PasswordResetTests(IntegrationFixture fixture)
    {
        _fixture = fixture;
        _email = fixture.GetService<TestEmailService>();
        _cache = fixture.GetService<ICacheService>();
        _email.Clear();
        _email.Enabled = true;
        _email.SendResult = true;
    }

    private async Task<(SysUser User, string Password)> CreateUserAsync(string prefix)
    {
        var users = _fixture.GetService<IRepository<SysUser>>();
        var userName = IntegrationFixture.Uid(prefix);
        var password = "OldPwd@123";
        await users.InsertAsync(new SysUser
        {
            UserName = userName,
            Password = NetBase.Common.Security.PasswordHelper.Encrypt(password),
            NickName = prefix,
            Email = $"{userName}@test.local",
            Status = (int)NetBase.Model.Enums.StatusEnum.Enabled
        });
        return (await users.GetFirstAsync(x => x.UserName == userName)!, password);
    }

    private ISysAuthService Auth => _fixture.GetService<ISysAuthService>();

    private Task<string?> GetCachedCode(string userName) => _cache.GetAsync<string>($"pwdreset:code:{userName}");

    [Fact]
    public async Task FullFlow_ResetThenLoginWithNewPassword()
    {
        var (user, oldPassword) = await CreateUserAsync("pwdreset");

        // 发码：静默成功（对外统一提示由控制器层保证）
        await Auth.SendResetCodeAsync(user.UserName, user.Email, ip: "127.0.0.1");
        var code = await GetCachedCode(user.UserName);
        Assert.NotNull(code);
        Assert.Single(_email.Sent);
        Assert.Equal(user.Email, _email.Sent[0].To);

        // 重置成功 → 全端会话清除 + 验证码已消耗
        await Auth.ResetPasswordByCodeAsync(user.UserName, user.Email, code!, "NewPwd@456", ip: "127.0.0.1");
        Assert.Null(await GetCachedCode(user.UserName));

        // 新密码可登录，旧密码被拒
        await Auth.LoginAsync(user.UserName, "NewPwd@456", "127.0.0.1", "test");
        await Assert.ThrowsAsync<BusinessException>(
            () => Auth.LoginAsync(user.UserName, oldPassword, "127.0.0.1", "test"));
    }

    [Fact]
    public async Task EmailMismatch_ShouldNotSendAndStaySilent()
    {
        var (user, _) = await CreateUserAsync("pwdmm");

        // 账号存在但邮箱不匹配：不发码、不报错（防枚举）
        await Auth.SendResetCodeAsync(user.UserName, "wrong@test.local", ip: "127.0.0.1");
        Assert.Empty(_email.Sent);
        Assert.Null(await GetCachedCode(user.UserName));

        // 账号不存在：同样静默
        await Auth.SendResetCodeAsync("no_such_user_xxx", "any@test.local", ip: "127.0.0.1");
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task WrongCode_ShouldRejectWithResetCodeInvalid()
    {
        var (user, _) = await CreateUserAsync("pwdwrong");
        await Auth.SendResetCodeAsync(user.UserName, user.Email, ip: "127.0.0.1");

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => Auth.ResetPasswordByCodeAsync(user.UserName, user.Email, "000000", "NewPwd@456", ip: "127.0.0.1"));
        Assert.Equal(ErrorCodes.AUTH_RESET_CODE_INVALID, ex.ErrorCode);
    }

    [Fact]
    public async Task CodeIsSingleUse_SecondAttemptRejects()
    {
        var (user, _) = await CreateUserAsync("pwdsingle");
        await Auth.SendResetCodeAsync(user.UserName, user.Email, ip: "127.0.0.1");
        var code = (await GetCachedCode(user.UserName))!;

        await Auth.ResetPasswordByCodeAsync(user.UserName, user.Email, code, "NewPwd@456", ip: "127.0.0.1");

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => Auth.ResetPasswordByCodeAsync(user.UserName, user.Email, code, "NewPwd@789", ip: "127.0.0.1"));
        Assert.Equal(ErrorCodes.AUTH_RESET_CODE_INVALID, ex.ErrorCode);
    }

    [Fact]
    public async Task WeakPassword_ShouldRejectWithPolicyViolation()
    {
        var (user, _) = await CreateUserAsync("pwdpolicy");
        await Auth.SendResetCodeAsync(user.UserName, user.Email, ip: "127.0.0.1");
        var code = (await GetCachedCode(user.UserName))!;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => Auth.ResetPasswordByCodeAsync(user.UserName, user.Email, code, "123456", ip: "127.0.0.1"));
        Assert.Equal(ErrorCodes.AUTH_PWD_POLICY_VIOLATION, ex.ErrorCode);
        // 验证码已消耗（安全优先）
        Assert.Null(await GetCachedCode(user.UserName));
    }

    [Fact]
    public async Task SendCode_ShouldRateLimitWithin60Seconds()
    {
        var (user, _) = await CreateUserAsync("pwdrate");
        await Auth.SendResetCodeAsync(user.UserName, user.Email, ip: "127.0.0.1");

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => Auth.SendResetCodeAsync(user.UserName, user.Email, ip: "127.0.0.1"));
        Assert.Equal(ErrorCodes.AUTH_RESET_CODE_RATE_LIMITED, ex.ErrorCode);
    }

    [Fact]
    public async Task EmailDisabled_ShouldFailLoudly()
    {
        var (user, _) = await CreateUserAsync("pwddisabled");
        _email.Enabled = false;

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => Auth.SendResetCodeAsync(user.UserName, user.Email, ip: "127.0.0.1"));
        Assert.Equal(ErrorCodes.AUTH_RESET_CODE_INVALID, ex.ErrorCode);
        Assert.Null(await GetCachedCode(user.UserName));
        _email.Enabled = true;
    }

    [Fact]
    public async Task DisabledUser_ShouldNotSend()
    {
        var users = _fixture.GetService<IRepository<SysUser>>();
        var userName = IntegrationFixture.Uid("pwddis");
        await users.InsertAsync(new SysUser
        {
            UserName = userName,
            Password = NetBase.Common.Security.PasswordHelper.Encrypt("OldPwd@123"),
            NickName = "dis",
            Email = $"{userName}@test.local",
            Status = (int)NetBase.Model.Enums.StatusEnum.Disabled
        });

        await Auth.SendResetCodeAsync(userName, $"{userName}@test.local", ip: "127.0.0.1");
        Assert.Null(await GetCachedCode(userName));
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Login_ShouldUpdateLastLoginTime()
    {
        var (user, password) = await CreateUserAsync("pwdlastlogin");
        Assert.Null(user.LastLoginTime); // 建号时为空

        await Auth.LoginAsync(user.UserName, password, "127.0.0.1", "test");

        var after = await _fixture.GetService<IRepository<SysUser>>()
            .GetFirstAsync(x => x.UserName == user.UserName);
        Assert.NotNull(after!.LastLoginTime);
    }
}
