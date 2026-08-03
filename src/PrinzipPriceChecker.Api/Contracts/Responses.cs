using PrinzipPriceChecker.Api.Domain;
using PrinzipPriceChecker.Api.Services;

namespace PrinzipPriceChecker.Api.Contracts;

/// <summary>Квартира с актуальной ценой и ссылкой на объявление.</summary>
public record FlatPriceResponse(
    int FlatId,
    string Url,
    string? Name,
    string? Description,
    long? CurrentPrice,
    string CurrentPriceFormatted,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset? LastPriceChangeAt,
    string? LastCheckError)
{
    public static FlatPriceResponse From(TrackedFlat flat) => new(
        flat.Id,
        flat.Url,
        flat.Name,
        flat.Description,
        flat.CurrentPrice,
        PriceFormatter.Format(flat.CurrentPrice),
        flat.LastCheckedAt,
        flat.LastPriceChangeAt,
        flat.LastCheckError);
}

/// <summary>Оформленная подписка.</summary>
public record SubscriptionResponse(
    int Id,
    string Email,
    FlatPriceResponse Flat)
{
    public static SubscriptionResponse From(Subscription subscription) => new(
        subscription.Id,
        subscription.Email,
        FlatPriceResponse.From(subscription.TrackedFlat));
}

/// <summary>Запись истории: цена квартиры на момент фиксации.</summary>
public record PriceHistoryEntryResponse(
    int Id,
    long Price,
    DateTimeOffset DetectedAt)
{
    public static PriceHistoryEntryResponse From(PriceHistoryEntry entry) =>
        new(entry.Id, entry.Price, entry.DetectedAt);
}

/// <summary>Запись журнала уведомлений.</summary>
public record NotificationResponse(
    int Id,
    string Email,
    string FlatUrl,
    long? OldPrice,
    long NewPrice,
    string Subject,
    string Body,
    DateTimeOffset CreatedAt,
    bool IsSent,
    string? Error)
{
    public static NotificationResponse From(NotificationRecord record) => new(
        record.Id,
        record.Email,
        record.FlatUrl,
        record.OldPrice,
        record.NewPrice,
        record.Subject,
        record.Body,
        record.CreatedAt,
        record.IsSent,
        record.Error);
}

/// <summary>Результат проверки цены квартиры.</summary>
public record FlatCheckResponse(
    int FlatId,
    string Url,
    long? OldPrice,
    long? NewPrice,
    bool PriceChanged,
    int NotificationsSent,
    string? Error)
{
    public static FlatCheckResponse From(FlatCheckResult result) => new(
        result.FlatId,
        result.Url,
        result.OldPrice,
        result.NewPrice,
        result.PriceChanged,
        result.NotificationsSent,
        result.Error);
}
