using System.Globalization;
using System.Text.Json;
using AngleSharp.Html.Parser;

namespace PrinzipPriceChecker.Api.Parsing;

/// <summary>
/// Извлекает цену квартиры из разметки script type="application/ld+json" на странице объявления prinzip.su.
/// </summary>
public class JsonLdFlatParser
{
    private const string JsonLdMarker = "application/ld+json";

    private static readonly HtmlParser HtmlParser = new();

    public FlatSnapshot Parse(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            throw new FlatNotFoundException("Страница объявления вернула пустой ответ.");
        }

        var block = ExtractJsonLdBlock(html);

        if (block is null)
        {
            throw new FlatNotFoundException(
                "На странице не найдено разметки application/ld+json - не удалось определить цену.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(block);
        }
        catch (JsonException exception)
        {
            // Разметка есть, но она сломана: существует ли квартира, мы не знаем.
            throw new FlatPageParseException(
                $"Не удалось разобрать разметку application/ld+json: {exception.Message}");
        }

        using (document)
        {
            foreach (var node in EnumerateNodes(document.RootElement))
            {
                if (!IsProduct(node) || !TryReadOffer(node, out var snapshot))
                {
                    continue;
                }

                return snapshot with { Url = FlatUrl.NormalizeUrlFromJsonLd(snapshot.Url) };
            }
        }

        throw new FlatNotFoundException(
            "В разметке application/ld+json не найдено предложения (offers.price) с ценой квартиры.");
    }

    internal static string? ExtractJsonLdBlock(string html)
    {
        using var document = HtmlParser.ParseDocument(html);

        return document
            .QuerySelectorAll($"script[type='{JsonLdMarker}']")
            .Select(script => script.TextContent.Trim())
            .FirstOrDefault(content => content.Length > 0);
    }

    private static IEnumerable<JsonElement> EnumerateNodes(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                foreach (var node in EnumerateNodes(item))
                {
                    yield return node;
                }
            }

            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in graph.EnumerateArray())
            {
                foreach (var node in EnumerateNodes(item))
                {
                    yield return node;
                }
            }
        }

        yield return root;
    }

    private static bool IsProduct(JsonElement node) =>
        string.Equals(ReadString(node, "@type"), "Product", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadOffer(JsonElement product, out FlatSnapshot snapshot)
    {
        snapshot = null!;

        if (!product.TryGetProperty("offers", out var offers))
        {
            return false;
        }

        // offers может быть как объектом, так и массивом предложений.
        IEnumerable<JsonElement> candidates = offers.ValueKind == JsonValueKind.Array
            ? offers.EnumerateArray()
            : [offers];

        foreach (var offer in candidates)
        {
            if (offer.ValueKind != JsonValueKind.Object
                || !offer.TryGetProperty("price", out var priceElement)
                || !TryReadPrice(priceElement, out var price))
            {
                continue;
            }

            var url = ReadString(offer, "url")
                ?? ReadString(product, "url")
                ?? ReadString(product, "@id")
                ?? string.Empty;

            var currency = ReadString(offer, "priceCurrency") ?? "RUB";

            snapshot = new FlatSnapshot(
                url,
                price,
                currency,
                ReadString(product, "name"),
                ReadString(product, "description"));

            return true;
        }

        return false;
    }

    internal static bool TryReadPrice(JsonElement element, out long price)
    {
        price = 0L;

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetDecimal(out var number) && TryTruncate(number, out price),
            JsonValueKind.String => TryParsePrice(element.GetString(), out price),
            _ => false,
        };
    }

    internal static bool TryParsePrice(string? value, out long price)
    {
        price = 0L;

        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && TryTruncate(parsed, out price);
    }

    private static bool TryTruncate(decimal value, out long price)
    {
        price = 0L;

        if (value < 1m || value > long.MaxValue)
        {
            return false;
        }

        price = (long)decimal.Truncate(value);
        return true;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
