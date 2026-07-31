using PrinzipPriceChecker.Api.Services.Email;

namespace PrinzipPriceChecker.Tests.Fakes;

internal sealed class FakeEmailSender : IEmailSender
{
    private readonly HashSet<string> _failingRecipients = new(StringComparer.OrdinalIgnoreCase);

    public List<EmailMessage> SentMessages { get; } = [];
    
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (_failingRecipients.Contains(message.To))
        {
            throw new InvalidOperationException($"SMTP-сервер отклонил письмо для {message.To}.");
        }

        SentMessages.Add(message);

        return Task.CompletedTask;
    }
}
