using System.Net;
using PrinzipPriceChecker.Api.Parsing;

namespace PrinzipPriceChecker.Api.Services;

/// <summary>Забирает страницу объявления с prinzip.su и вытаскивает цену из JSON-LD.</summary>
public class PrinzipFlatPriceSource(
    HttpClient httpClient,
    JsonLdFlatParser parser,
    ILogger<PrinzipFlatPriceSource> logger) : IFlatPriceSource
{
    public const string HttpClientName = "prinzip";

    public async Task<FlatSnapshot> GetSnapshotAsync(string flatUrl, CancellationToken cancellationToken)
    {
        logger.LogDebug("Запрашиваем страницу объявления {FlatUrl}", flatUrl);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(flatUrl, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new FlatPageParseException(
                $"Не удалось загрузить страницу объявления: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FlatPageParseException(
                "Не удалось загрузить страницу объявления: превышено время ожидания ответа.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new FlatNotFoundException(
                    "Объявление не найдено на сайте (HTTP 404) - возможно, квартира снята с продажи.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new FlatPageParseException(
                    $"Сайт вернул неуспешный ответ: HTTP {(int)response.StatusCode}.");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            return parser.Parse(html);
        }
    }
}
