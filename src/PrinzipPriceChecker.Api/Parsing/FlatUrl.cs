using System.Diagnostics.CodeAnalysis;

namespace PrinzipPriceChecker.Api.Parsing;

/// <summary>
/// Валидация и нормализация ссылок на квартиры prinzip.su.
/// Нормализация нужна, чтобы ".../65040/", ".../65040" и ".../65040?utm=1" считались одной и той же квартирой.
/// </summary>
public static class FlatUrl
{
    private const string CanonicalHost = "prinzip.su";

    /// <summary>
    /// Приводит ссылку к каноническому виду <c>https://prinzip.su/flats/{комплекс}/{id}</c>.
    /// </summary>
    /// <returns><c>true</c>, если ссылка похожа на страницу квартиры prinzip.su.</returns>
    public static bool TryNormalize(
        string? url,
        [NotNullWhen(true)] out string? normalized,
        [NotNullWhen(false)] out string? error)
    {
        normalized = null;
        error = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            error = "Пустая ссылка.";
            return false;
        }

        var raw = url.Trim();

        if (!raw.Contains("://", StringComparison.Ordinal))
        {
            raw = "https://" + raw;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            error = "Ссылка на объявление имеет некорректный формат.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Поддерживаются только ссылки по http/https.";
            return false;
        }
        
        if (!uri.Host.Equals(CanonicalHost, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Поддерживаются только ссылки на {CanonicalHost}.";
            return false;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Ожидаем /flats/{комплекс}/{id}
        if (segments.Length != 3 || !segments[0].Equals("flats", StringComparison.OrdinalIgnoreCase))
        {
            error = "Ссылка должна вести на страницу квартиры, например "
                + "https://prinzip.su/flats/shartashpark/65040/.";
            return false;
        }

        normalized = $"https://{CanonicalHost}/flats/{segments[1]}/{segments[2]}";
        return true;
    }

    public static string NormalizeUrlFromJsonLd(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }
        
        var hashIndex = url.IndexOf('#', StringComparison.Ordinal);
        if (hashIndex >= 0)
        {
            url = url[..hashIndex];
        }
        
        return url.TrimEnd('/');
    }
}
