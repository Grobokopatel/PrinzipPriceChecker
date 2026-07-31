using PrinzipPriceChecker.Api.Parsing;
using PrinzipPriceChecker.Api.Services;

namespace PrinzipPriceChecker.Tests.Fakes;

internal sealed class FakePriceSource : IFlatPriceSource
{
    public const string DefaultName = "Квартира с кухней-гостиной и двумя комнатами";

    public const string DefaultDescription = "Шарташ Парк, 1 дом, кв. № 398";

    private readonly Dictionary<string, Func<FlatSnapshot>> _responses = new(StringComparer.OrdinalIgnoreCase);

    public int RequestCount { get; private set; }

    /// <summary>Сбрасывает счётчик, чтобы запросы этапа подготовки не мешали проверке.</summary>
    public void ResetRequestCount() => RequestCount = 0;

    public void SetPrice(
        string url,
        long price,
        string? name = DefaultName,
        string? description = DefaultDescription)
    {
        _responses[url] = () => new FlatSnapshot(url, price, "RUB", name, description);
    }

    public void SetFailure(string url, string message)
    {
        _responses[url] = () => throw new FlatPageParseException(message);
    }

    public void SetNotFound(string url, string message = "Объявление не найдено на сайте (HTTP 404).")
    {
        _responses[url] = () => throw new FlatNotFoundException(message);
    }

    public Task<FlatSnapshot> GetSnapshotAsync(string flatUrl, CancellationToken cancellationToken)
    {
        RequestCount++;

        if (!_responses.TryGetValue(flatUrl, out var response))
        {
            throw new FlatPageParseException($"Для {flatUrl} в тесте не задан ответ сайта.");
        }

        return Task.FromResult(response());
    }
}
