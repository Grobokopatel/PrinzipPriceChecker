using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using PrinzipPriceChecker.Api.Data;
using PrinzipPriceChecker.Api.Domain;
using PrinzipPriceChecker.Api.Parsing;

namespace PrinzipPriceChecker.Api.Services;

public class SubscriptionService(
    AppDbContext db,
    IFlatPriceSource priceSource,
    TimeProvider timeProvider,
    ILogger<SubscriptionService> logger)
{
    public async Task<CreateSubscriptionOutcome> CreateAsync(
        string normalizedUrl,
        string email,
        CancellationToken cancellationToken)
    {
        var flat = await db.TrackedFlats
            .FirstOrDefaultAsync(f => f.Url == normalizedUrl, cancellationToken);

        var now = timeProvider.GetUtcNow();

        if (flat is null)
        {
            flat = new TrackedFlat { Url = normalizedUrl };
            db.TrackedFlats.Add(flat);
        }
        else if (await db.Subscriptions.AnyAsync(
            s => s.TrackedFlatId == flat.Id && s.Email == email,
            cancellationToken))
        {
            return CreateSubscriptionOutcome.AlreadyExists;
        }

        // Цену проверяем только у новой квартиры. За уже отслеживаемой квартирой следит
        // PriceMonitorWorker, её сохранённая цена свежая - лишний запрос к сайту не нужен.
        if (flat.CurrentPrice is null)
        {
            try
            {
                var snapshot = await priceSource.GetSnapshotAsync(normalizedUrl, cancellationToken);

                flat.Name = snapshot.Name ?? flat.Name;
                flat.Description = snapshot.Description ?? flat.Description;
                flat.LastCheckedAt = now;
                flat.LastCheckError = null;
                flat.CurrentPrice = snapshot.Price;
                flat.LastPriceChangeAt = now;

                db.PriceHistory.Add(new PriceHistoryEntry
                {
                    TrackedFlat = flat,
                    Price = snapshot.Price,
                    DetectedAt = now,
                });
            }
            catch (FlatNotFoundException exception)
            {
                // Квартиры на сайте нет - следить не за чем, подписку не создаём.
                // Добавленная выше квартира не сохраняется: SaveChangesAsync не вызывается.
                logger.LogWarning(
                    "Отказ в подписке на {FlatUrl}: {Error}",
                    normalizedUrl,
                    exception.Message);

                return CreateSubscriptionOutcome.FlatNotFound(exception.Message);
            }
            catch (FlatPageParseException exception)
            {
                // Сайт недоступен - существует ли такая квартира, мы не знаем.
                logger.LogWarning(
                    "Подписка на {FlatUrl} не оформлена, цену получить не удалось: {Error}",
                    normalizedUrl,
                    exception.Message);

                return CreateSubscriptionOutcome.SiteUnavailable(exception.Message);
            }
        }

        var subscription = new Subscription
        {
            Email = email,
            TrackedFlat = flat,
        };

        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Оформлена подписка {SubscriptionId}: {Email} -> {FlatUrl}",
            subscription.Id,
            email,
            normalizedUrl);

        return CreateSubscriptionOutcome.Created(subscription);
    }
}

public enum CreateSubscriptionStatus
{
    /// <summary>Подписка создана, цена квартиры получена.</summary>
    Created,

    /// <summary>Этот email уже подписан на эту квартиру.</summary>
    AlreadyExists,

    /// <summary>Квартиры по ссылке нет: страница не найдена или цены в ней не оказалось.</summary>
    FlatNotFound,

    /// <summary>Сайт недоступен.</summary>
    SiteUnavailable,
}

/// <summary>Результат попытки оформить подписку.</summary>
public sealed class CreateSubscriptionOutcome
{
    private CreateSubscriptionOutcome(
        CreateSubscriptionStatus status,
        Subscription? subscription,
        string? error)
    {
        Status = status;
        Subscription = subscription;
        Error = error;
    }

    public CreateSubscriptionStatus Status { get; }

    public Subscription? Subscription { get; }

    /// <summary>Причина отказа; <c>null</c>, если подписка создана или уже существовала.</summary>
    public string? Error { get; }

    [MemberNotNullWhen(true, nameof(Subscription))]
    public bool IsCreated => Status == CreateSubscriptionStatus.Created;

    public static CreateSubscriptionOutcome AlreadyExists { get; } =
        new(CreateSubscriptionStatus.AlreadyExists, subscription: null, error: null);

    public static CreateSubscriptionOutcome Created(Subscription subscription) =>
        new(CreateSubscriptionStatus.Created, subscription, error: null);

    public static CreateSubscriptionOutcome FlatNotFound(string error) =>
        new(CreateSubscriptionStatus.FlatNotFound, subscription: null, error);

    public static CreateSubscriptionOutcome SiteUnavailable(string error) =>
        new(CreateSubscriptionStatus.SiteUnavailable, subscription: null, error);
}
