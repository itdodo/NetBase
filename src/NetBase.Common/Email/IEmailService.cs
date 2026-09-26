namespace NetBase.Common.Email;

/// <summary>
/// 邮件发送服务（SMTP）：作业告警、审批通知镜像、忘记密码验证码等场景的统一出口。
/// 实现位于 Service 层（MailKit，跨平台/容器可用）；配置走系统参数（sys.email.*）。
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// 发送 HTML 邮件。系统参数 sys.email.enabled=false 或未配置 SMTP 时静默跳过（返回 false）；
    /// 发送异常不抛出（记录日志返回 false）——通知是尽力而为的辅助通道，不得阻断主流程。
    /// </summary>
    /// <param name="to">收件人邮箱</param>
    /// <param name="subject">主题（纯文本）</param>
    /// <param name="htmlBody">正文（HTML；调用方自行转义业务内容）</param>
    Task<bool> SendAsync(string to, string subject, string htmlBody);

    /// <summary>SMTP 是否已启用且配置完整（管理端展示与测试用）</summary>
    Task<bool> IsEnabledAsync();
}
