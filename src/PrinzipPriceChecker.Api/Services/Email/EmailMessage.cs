namespace PrinzipPriceChecker.Api.Services.Email;

/// <summary>Письмо для отправки подписчику.</summary>
public record EmailMessage(string To, string Subject, string TextBody);
