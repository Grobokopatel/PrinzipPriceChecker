using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace PrinzipPriceChecker.Api.Services.Email;

/// <summary>Отправка писем через SMTP (MailKit).</summary>
public class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart(TextFormat.Plain) { Text = message.TextBody };

        var smtp = _options.Smtp;

        using var client = new SmtpClient();

        var secureSocketOptions = smtp.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.Auto;

        await client.ConnectAsync(smtp.Host, smtp.Port, secureSocketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(smtp.UserName))
        {
            await client.AuthenticateAsync(smtp.UserName, smtp.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation(
            "Письмо \"{Subject}\" отправлено на {Email} через SMTP {Host}:{Port}",
            message.Subject,
            message.To,
            smtp.Host,
            smtp.Port);
    }
}
