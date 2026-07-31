namespace PrinzipPriceChecker.Api.Parsing;

/// <summary>Данные о квартире, полученные со страницы объявления.</summary>
/// <param name="Url">Ссылка на объявление в нормализованном виде.</param>
/// <param name="Price">Актуальная цена с сайта.</param>
/// <param name="Currency">Валюта цены (у Prinzip вроде всегда RUB).</param>
/// <param name="Name">Название объявления из JSON-LD.</param>
/// <param name="Description">Описание объявления из JSON-LD.</param>
public record FlatSnapshot(
    string Url,
    long Price,
    string Currency,
    string? Name,
    string? Description);
