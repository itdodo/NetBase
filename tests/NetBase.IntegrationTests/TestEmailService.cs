using NetBase.Common.Email;

namespace NetBase.IntegrationTests;

/// <summary>
/// 测试邮件替身：收集发送请求供用例断言（从正文提取验证码）；Enabled/SendResult 可控，
/// 用于模拟"SMTP 已配置"与"发送成功/失败"各分支。
/// </summary>
public sealed class TestEmailService : IEmailService
{
    public sealed record SentMail(string To, string Subject, string HtmlBody);

    private readonly List<SentMail> _sent = [];

    /// <summary>模拟 SMTP 是否已配置完整（false 时 SendAsync 静默跳过）</summary>
    public bool Enabled { get; set; }

    /// <summary>模拟 SMTP 发送结果（true=投递成功；false=SMTP 故障）</summary>
    public bool SendResult { get; set; } = true;

    public IReadOnlyList<SentMail> Sent => _sent;

    public void Clear() => _sent.Clear();

    public Task<bool> IsEnabledAsync() => Task.FromResult(Enabled);

    public Task<bool> SendAsync(string to, string subject, string htmlBody)
    {
        if (!Enabled)
        {
            return Task.FromResult(false);
        }
        _sent.Add(new SentMail(to, subject, htmlBody));
        return Task.FromResult(SendResult);
    }
}

/// <summary>测试验证码替身：恒关闭（登录跳过图形验证码），聚焦密码重置流程</summary>
public sealed class FakeCaptchaService : NetBase.Service.Sys.ICaptchaService
{
    public Task<NetBase.Service.Sys.CaptchaResult> GenerateAsync() =>
        Task.FromResult(new NetBase.Service.Sys.CaptchaResult { CaptchaId = "test", Svg = "data:," });

    public Task<bool> ValidateAsync(string captchaId, string code) => Task.FromResult(true);

    public Task<bool> IsEnabledAsync() => Task.FromResult(false);
}
