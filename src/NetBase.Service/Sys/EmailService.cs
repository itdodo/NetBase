using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using NetBase.Common.Email;
using NetBase.Common.Security;
using NetBase.Service.Sys;

namespace NetBase.Service.Sys;

/// <summary>
/// SMTP 邮件服务（MailKit 实现）。配置走系统参数（sys.email.*，10 分钟缓存）：
/// enabled 开关、smtpHost、smtpPort、smtpAccount、smtpPassword（SensitiveCrypto 密文）、from。
/// </summary>
public class EmailService(ISysConfigService configService, ILogger<EmailService> logger) : IEmailService
{
    private const string KeyPrefix = "sys.email.";

    public async Task<bool> IsEnabledAsync()
    {
        var enabled = await configService.GetConfigValueAsync($"{KeyPrefix}enabled");
        if (enabled != "true")
        {
            return false;
        }
        var host = await configService.GetConfigValueAsync($"{KeyPrefix}host");
        return !string.IsNullOrWhiteSpace(host);
    }

    public async Task<bool> SendAsync(string to, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            return false;
        }

        string host, account, password, from;
        int port;
        bool ssl;
        try
        {
            // 未启用/配置不全：静默跳过（正常业务路径，非异常）
            if (!await IsEnabledAsync())
            {
                return false;
            }
            host = (await configService.GetConfigValueAsync($"{KeyPrefix}host")) ?? string.Empty;
            port = int.TryParse(await configService.GetConfigValueAsync($"{KeyPrefix}port"), out var p) ? p : 465;
            ssl = (await configService.GetConfigValueAsync($"{KeyPrefix}ssl")) != "false";
            account = (await configService.GetConfigValueAsync($"{KeyPrefix}account")) ?? string.Empty;
            password = SensitiveCrypto.Unprotect((await configService.GetConfigValueAsync($"{KeyPrefix}password")) ?? string.Empty) ?? string.Empty;
            from = (await configService.GetConfigValueAsync($"{KeyPrefix}from")) ?? account;
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            {
                logger.LogWarning("邮件未发送：SMTP 配置不完整（host/from 缺失）");
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "邮件配置读取失败，跳过发送");
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(from));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, ssl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable);
            if (!string.IsNullOrWhiteSpace(account))
            {
                await client.AuthenticateAsync(account, password);
            }
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
            logger.LogInformation("邮件已发送: {To} - {Subject}", to, subject);
            return true;
        }
        catch (Exception ex)
        {
            // 通知是尽力而为的辅助通道：SMTP 故障不阻断业务主流程（红线 19）
            logger.LogError(ex, "邮件发送失败: {To} - {Subject}", to, subject);
            return false;
        }
    }
}
