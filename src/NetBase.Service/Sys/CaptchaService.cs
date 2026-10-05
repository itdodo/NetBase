using Lazy.Captcha.Core;
using NetBase.Common.Cache;
using NetBase.Common.Extensions;
using NetBase.Model.Dtos;

namespace NetBase.Service.Sys;

/// <summary>验证码</summary>
public class CaptchaResult
{
    /// <summary>验证码ID（登录时回传）</summary>
    public string CaptchaId { get; set; } = string.Empty;

    /// <summary>图片（base64 data URI，前端 img 直接渲染）</summary>
    public string Svg { get; set; } = string.Empty;
}

/// <summary>
/// 图形验证码服务接口：缓存 2 分钟、一次性使用、忽略大小写。
/// 可通过系统参数 sys.captcha.enabled 关闭（内网场景）。
/// </summary>
public interface ICaptchaService
{
    Task<CaptchaResult> GenerateAsync();

    /// <summary>校验并销毁（一次性）；Enabled=false 时恒通过</summary>
    Task<bool> ValidateAsync(string captchaId, string code);

    Task<bool> IsEnabledAsync();
}

/// <summary>
/// 图形验证码服务：基于 Lazy.Captcha.Core（字母数字+干扰，内置多种风格），
/// 缓存 2 分钟、一次性使用、忽略大小写。可通过系统参数 sys.captcha.enabled 关闭（内网场景）。
/// </summary>
public class CaptchaService(
    ICaptcha captcha,
    ICacheService cacheService,
    ISysConfigService configService) : ICaptchaService
{
    private const string EnabledKey = "sys.captcha.enabled";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public async Task<bool> IsEnabledAsync()
    {
        var enabled = await configService.GetConfigValueAsync(EnabledKey);
        return enabled != "false"; // 缺省启用
    }

    public Task<CaptchaResult> GenerateAsync()
    {
        var captchaId = Guid.NewGuid().ToString("N");
        var data = captcha.Generate(captchaId);

        cacheService.Set($"captcha:{captchaId}", data.Code.ToUpperInvariant(), Ttl);
        return Task.FromResult(new CaptchaResult
        {
            CaptchaId = captchaId,
            Svg = $"data:image/gif;base64,{data.Base64}"
        });
    }

    public Task<bool> ValidateAsync(string captchaId, string code)
    {
        if (captchaId.IsNullOrWhiteSpace() || code.IsNullOrWhiteSpace())
        {
            return Task.FromResult(false);
        }

        var key = $"captcha:{captchaId}";
        var expected = cacheService.Get<string>(key);
        if (expected == null)
        {
            return Task.FromResult(false); // 过期或不存在
        }

        cacheService.Remove(key); // 一次性
        return Task.FromResult(string.Equals(expected, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
