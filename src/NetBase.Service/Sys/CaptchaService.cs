using NetBase.Common.Cache;

namespace NetBase.Service.Sys;

/// <summary>验证码</summary>
public class CaptchaResult
{
    /// <summary>验证码ID（登录时回传）</summary>
    public string CaptchaId { get; set; } = string.Empty;

    /// <summary>SVG 图片（前端直接内嵌渲染）</summary>
    public string Svg { get; set; } = string.Empty;
}

/// <summary>
/// 图形验证码服务：4 位字符 SVG 手绘（无第三方依赖），
/// 缓存 2 分钟、一次性使用、忽略大小写。可通过 Captcha:Enabled 关闭（内网场景）。
/// </summary>
public interface ICaptchaService
{
    Task<CaptchaResult> GenerateAsync();

    /// <summary>校验并销毁（一次性）；Enabled=false 时恒通过</summary>
    Task<bool> ValidateAsync(string captchaId, string code);

    Task<bool> IsEnabledAsync();
}

public class CaptchaService(ICacheService cacheService, ISysConfigService configService) : ICaptchaService
{
    private const string EnabledKey = "sys.captcha.enabled";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);
    private static readonly char[] Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    public async Task<bool> IsEnabledAsync()
    {
        var enabled = await configService.GetConfigValueAsync(EnabledKey);
        return enabled != "false"; // 缺省启用
    }

    public Task<CaptchaResult> GenerateAsync()
    {
        var code = new string(Enumerable.Range(0, 4)
            .Select(_ => Chars[Random.Shared.Next(Chars.Length)])
            .ToArray());
        var captchaId = Guid.NewGuid().ToString("N");
        cacheService.Set($"netbase:captcha:{captchaId}", code.ToUpperInvariant(), Ttl);
        return Task.FromResult(new CaptchaResult { CaptchaId = captchaId, Svg = DrawSvg(code) });
    }

    public Task<bool> ValidateAsync(string captchaId, string code)
    {
        if (string.IsNullOrWhiteSpace(captchaId) || string.IsNullOrWhiteSpace(code))
        {
            return Task.FromResult(false);
        }

        var key = $"netbase:captcha:{captchaId}";
        var expected = cacheService.Get<string>(key);
        if (expected == null)
        {
            return Task.FromResult(false); // 过期或不存在
        }

        cacheService.Remove(key); // 一次性
        return Task.FromResult(string.Equals(expected, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>手绘 SVG：随机字符位置/旋转 + 干扰线</summary>
    private static string DrawSvg(string code)
    {
        const int width = 120;
        const int height = 40;
        var colors = new[] { "#409eff", "#67c23a", "#e6a23c", "#f56c6c", "#909399" };
        var sb = new System.Text.StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
        sb.Append($"<rect width=\"{width}\" height=\"{height}\" fill=\"#f5f7fa\"/>");

        // 干扰线
        for (var i = 0; i < 4; i++)
        {
            var color = colors[Random.Shared.Next(colors.Length)];
            sb.Append($"<line x1='{Random.Shared.Next(width)}' y1='{Random.Shared.Next(height)}' " +
                      $"x2='{Random.Shared.Next(width)}' y2='{Random.Shared.Next(height)}' stroke='{color}' stroke-width='1' opacity='0.5'/>");
        }

        // 字符
        for (var i = 0; i < code.Length; i++)
        {
            var x = 15 + i * 26 + Random.Shared.Next(-3, 3);
            var y = 27 + Random.Shared.Next(-4, 4);
            var rotate = Random.Shared.Next(-25, 25);
            var color = colors[Random.Shared.Next(colors.Length)];
            sb.Append($"<text x='{x}' y='{y}' font-size='24' font-family='Arial' font-weight='bold' fill='{color}' " +
                      $"transform='rotate({rotate} {x} {y})'>{code[i]}</text>");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }
}
