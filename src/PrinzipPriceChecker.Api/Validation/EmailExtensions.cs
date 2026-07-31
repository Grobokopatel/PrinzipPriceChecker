using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;

namespace PrinzipPriceChecker.Api.Validation;

public static class EmailExtensions
{
    /// <summary>Нормализует и валидирует email</summary>
    public static bool TryNormalize(
        string? email,
        [NotNullWhen(true)] out string? normalized,
        [NotNullWhen(false)] out string? error)
    {
        normalized = null;
        error = null;

        if (string.IsNullOrWhiteSpace(email))
        {
            error = "Email пустой";
            return false;
        }

        var trimmedEmail = email.Trim();

        if (!MailAddress.TryCreate(trimmedEmail, out var address)
            || !string.Equals(address.Address, trimmedEmail, StringComparison.OrdinalIgnoreCase)
            || !address.Host.Contains('.', StringComparison.OrdinalIgnoreCase))
        {
            error = "Email имеет некорректный формат";
            return false;
        }

        normalized = trimmedEmail;
        return true;
    }
}
