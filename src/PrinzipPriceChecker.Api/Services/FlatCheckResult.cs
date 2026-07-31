namespace PrinzipPriceChecker.Api.Services;

/// <summary>Результат одной проверки цены квартиры.</summary>
/// <param name="FlatId">Идентификатор отслеживаемой квартиры.</param>
/// <param name="Url">Ссылка на объявление.</param>
/// <param name="OldPrice">Цена, известная сервису до проверки.</param>
/// <param name="NewPrice">Цена, полученная с сайта; null, если проверка не удалась.</param>
/// <param name="PriceChanged">Было ли зафиксировано изменение цены.</param>
/// <param name="NotificationsSent">Сколько писем успешно отправлено подписчикам.</param>
/// <param name="Error">Текст ошибки, если проверка не удалась.</param>
public record FlatCheckResult(
    int FlatId,
    string Url,
    long? OldPrice,
    long? NewPrice,
    bool PriceChanged,
    int NotificationsSent,
    string? Error)
{
    public bool Success => Error is null;
}
