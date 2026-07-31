namespace PrinzipPriceChecker.Api.Services.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Способ отправки писем, для подробностей смотрите <see cref="EmailProvider"/>.</summary>
    public EmailProvider Provider { get; set; } = EmailProvider.Log;

    public string FromAddress { get; set; } = "noreply@pricechecker.local";

    public string FromName { get; set; } = "Prinzip Price Checker";

    public SmtpOptions Smtp { get; set; } = new();

    public class SmtpOptions
    {
        public string Host { get; set; } = "localhost";

        public int Port { get; set; } = 1025;

        /// <summary>Использовать ли STARTTLS. Для локального Mailpit не требуется.</summary>
        public bool UseStartTls { get; set; }

        public string? UserName { get; set; }

        public string? Password { get; set; }
    }
}
