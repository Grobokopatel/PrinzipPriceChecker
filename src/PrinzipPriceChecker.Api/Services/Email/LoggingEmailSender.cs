namespace PrinzipPriceChecker.Api.Services.Email;

/// <summary>
/// Провайдер по умолчанию: письмо не уходит наружу, а пишется в лог приложения.
/// Позволяет проверить всю логику слежения за ценой без настройки SMTP -
/// текст письма при этом всегда виден в журнале GET /api/notifications.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Уведомление для {Email}\nТема: {Subject}\n{Body}",
            message.To,
            message.Subject,
            message.TextBody);

        return Task.CompletedTask;
    }
}
