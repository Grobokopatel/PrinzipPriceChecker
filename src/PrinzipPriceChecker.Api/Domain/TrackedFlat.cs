namespace PrinzipPriceChecker.Api.Domain;

/// <summary>
/// Квартира, за ценой которой следит сервис. Одна и та же квартира может отслеживаться сразу несколькими email.
/// </summary>
public class TrackedFlat
{
    public int Id { get; set; }

    /// <summary>Нормализованная ссылка на объявление (без слэша в конце, без query).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Название из JSON-LD.</summary>
    public string? Name { get; set; }

    /// <summary>Описание из JSON-LD.</summary>
    public string? Description { get; set; }

    public long? CurrentPrice { get; set; }

    /// <summary>Когда последний раз пытались получить цену с сайта.</summary>
    public DateTimeOffset? LastCheckedAt { get; set; }

    /// <summary>Когда последний раз было зафиксировано изменение цены.</summary>
    public DateTimeOffset? LastPriceChangeAt { get; set; }

    /// <summary>Ошибка последней проверки; null - последняя проверка прошла успешно.</summary>
    public string? LastCheckError { get; set; }

    public List<Subscription> Subscriptions { get; set; } = [];

    public List<PriceHistoryEntry> PriceHistory { get; set; } = [];
}
