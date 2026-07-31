namespace PrinzipPriceChecker.Api.Services.Email;

/// <summary>Способ отправки писем.</summary>
public enum EmailProvider
{
    /// <summary>
    /// Письма только пишутся в лог и в журнал уведомлений - режим по умолчанию,
    /// чтобы сервис работал без настройки SMTP.
    /// </summary>
    Log,

    /// <summary>Реальная отправка через SMTP-сервер.</summary>
    Smtp,
}
